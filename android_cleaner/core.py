"""Conservative ADB inventory and heuristic review. Not a malware verdict."""
import json
import re
import shutil
import subprocess
from datetime import datetime, timezone
from pathlib import Path

PROTECTED_PREFIXES = ('android', 'com.android.', 'com.google.android.', 'com.samsung.android.', 'com.sec.', 'com.qualcomm.', 'com.mediatek.')
SUSPICIOUS_TOKENS = ('cleaner', 'booster', 'speedup', 'batteryoptimizer', 'fakeupdate', 'adpopup')

class AdbError(RuntimeError):
    pass

def adb(*args, serial=None, timeout=25):
    if not shutil.which('adb'):
        raise AdbError('ADB not found. Install Android SDK Platform Tools and add adb to PATH.')
    command = ['adb'] + (['-s', serial] if serial else []) + list(args)
    try:
        p = subprocess.run(command, capture_output=True, text=True, timeout=timeout, check=False, encoding='utf-8', errors='replace')
    except subprocess.TimeoutExpired as exc:
        raise AdbError('ADB command timed out') from exc
    if p.returncode:
        raise AdbError((p.stderr or p.stdout).strip() or 'ADB command failed')
    return p.stdout

def devices():
    found = []
    for line in adb('devices', '-l').splitlines()[1:]:
        parts = line.split()
        if len(parts) >= 2:
            found.append({'serial': parts[0], 'state': parts[1], 'details': ' '.join(parts[2:])})
    return found

def inventory(serial):
    raw = adb('shell', 'pm', 'list', 'packages', '-3', '-i', serial=serial, timeout=60)
    apps = []
    for line in raw.splitlines():
        match = re.match(r'^package:([^\s]+)(?:\s+installer=(.*))?$', line.strip())
        if not match:
            continue
        package, installer = match.group(1), (match.group(2) or '').strip()
        if not re.fullmatch(r'[A-Za-z0-9_]+(?:\.[A-Za-z0-9_]+)+', package):
            continue
        score, reasons = 0, []
        if installer in ('', 'null'):
            score += 1
            reasons.append('Installer not reported (may be normal)')
        if any(token in package.lower().replace('_', '') for token in SUSPICIOUS_TOKENS):
            score += 2
            reasons.append('Package name matches a review keyword; not proof of malware')
        if package.startswith(PROTECTED_PREFIXES):
            reasons.append('Protected platform/vendor namespace')
        risk = 'Review' if score >= 2 else 'Low/unknown'
        apps.append({'package': package, 'installer': installer or 'Unknown', 'score': score,
                     'risk': risk, 'reasons': reasons, 'protected': package.startswith(PROTECTED_PREFIXES)})
    return sorted(apps, key=lambda a: (-a['score'], a['package']))

def remove_for_user(serial, package):
    if not re.fullmatch(r'[A-Za-z0-9_]+(?:\.[A-Za-z0-9_]+)+', package):
        raise AdbError('Invalid package name')
    if package.startswith(PROTECTED_PREFIXES):
        raise AdbError('Protected platform/vendor package: removal blocked')
    output = adb('shell', 'pm', 'uninstall', '--user', '0', package, serial=serial, timeout=45)
    if 'Success' not in output:
        raise AdbError(output.strip() or 'Uninstall was not confirmed')
    return output.strip()

def export_report(path, serial, apps, actions):
    report = {'schema_version': 1, 'generated_utc': datetime.now(timezone.utc).isoformat(),
              'device_serial': serial, 'disclaimer': 'Heuristic inventory only; not a comprehensive malware scan.',
              'apps': apps, 'actions': actions}
    Path(path).write_text(json.dumps(report, indent=2), encoding='utf-8')
