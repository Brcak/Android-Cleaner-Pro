import sys
from PySide6.QtCore import QObject, QThread, Signal
from PySide6.QtWidgets import (QApplication, QMainWindow, QWidget, QVBoxLayout, QHBoxLayout, QLabel,
 QComboBox, QPushButton, QTableWidget, QTableWidgetItem, QMessageBox, QFileDialog, QHeaderView)
from .core import devices, inventory, remove_for_user, export_report

class Worker(QObject):
    done = Signal(object)
    error = Signal(str)
    finished = Signal()
    def __init__(self, fn):
        super().__init__(); self.fn = fn
    def run(self):
        try: self.done.emit(self.fn())
        except Exception as e: self.error.emit(str(e))
        finally: self.finished.emit()

class Window(QMainWindow):
    def __init__(self):
        super().__init__()
        self.setWindowTitle('Android Cleaner Pro — v0.1')
        self.resize(1020, 640)
        self.apps = []; self.actions = []; self.serial = None; self.busy = False
        root = QWidget(); self.setCentralWidget(root); layout = QVBoxLayout(root)
        header = QHBoxLayout(); layout.addLayout(header)
        header.addWidget(QLabel('Mode:'))
        self.mode = QComboBox(); self.mode.addItems(['Consumer', 'Technician']); header.addWidget(self.mode)
        header.addWidget(QLabel('Device:'))
        self.device = QComboBox(); header.addWidget(self.device, 1)
        self.refresh = QPushButton('Find devices'); header.addWidget(self.refresh)
        self.scan = QPushButton('Scan installed apps'); header.addWidget(self.scan)
        self.status = QLabel('Connect a phone and authorize USB debugging.'); layout.addWidget(self.status)
        self.table = QTableWidget(0, 4)
        self.table.setHorizontalHeaderLabels(['Package', 'Installer', 'Risk', 'Reasons'])
        self.table.horizontalHeader().setSectionResizeMode(0, QHeaderView.ResizeMode.Stretch)
        self.table.horizontalHeader().setSectionResizeMode(3, QHeaderView.ResizeMode.Stretch)
        self.table.setSelectionBehavior(QTableWidget.SelectionBehavior.SelectRows)
        self.table.setEditTriggers(QTableWidget.EditTrigger.NoEditTriggers)
        layout.addWidget(self.table, 1)
        footer = QHBoxLayout(); layout.addLayout(footer)
        self.remove = QPushButton('Uninstall selected app (user 0)'); footer.addWidget(self.remove)
        self.report = QPushButton('Export JSON report'); footer.addWidget(self.report)
        self.notice = QLabel('Risk labels are heuristic review prompts, not malware detections.'); layout.addWidget(self.notice)
        self.mode.currentTextChanged.connect(self.update_mode)
        self.refresh.clicked.connect(self.find_devices)
        self.scan.clicked.connect(self.scan_apps)
        self.remove.clicked.connect(self.uninstall)
        self.report.clicked.connect(self.save_report)
        self.update_mode(); self.find_devices()

    def update_mode(self):
        self.remove.setVisible(self.mode.currentText() == 'Technician')
        self.table.setColumnHidden(1, self.mode.currentText() == 'Consumer')
        self.table.setColumnHidden(3, self.mode.currentText() == 'Consumer')

    def run_job(self, fn, on_done):
        if self.busy: return
        self.busy = True
        for button in (self.refresh, self.scan, self.remove, self.report): button.setEnabled(False)
        self.thread = QThread(); self.worker = Worker(fn); self.worker.moveToThread(self.thread)
        self.thread.started.connect(self.worker.run)
        self.worker.done.connect(on_done)
        self.worker.error.connect(lambda e: (self.status.setText(e), QMessageBox.warning(self, 'ADB error', e)))
        self.worker.finished.connect(self.thread.quit)
        self.worker.finished.connect(self.worker.deleteLater)
        self.thread.finished.connect(self.thread.deleteLater)
        self.thread.finished.connect(self.job_finished)
        self.thread.start()

    def job_finished(self):
        self.busy = False
        for button in (self.refresh, self.scan, self.remove, self.report): button.setEnabled(True)

    def find_devices(self):
        self.status.setText('Finding connected devices…')
        def done(found):
            self.device.clear()
            for item in found:
                self.device.addItem(f"{item['serial']} ({item['state']})", item)
            self.status.setText(f'{len(found)} device(s) found. Select an authorized device.')
        self.run_job(devices, done)

    def selected_serial(self):
        item = self.device.currentData()
        if not item or item['state'] != 'device':
            QMessageBox.warning(self, 'Device unavailable', 'Connect a device and approve the USB debugging prompt.')
            return None
        return item['serial']

    def scan_apps(self):
        serial = self.selected_serial()
        if not serial: return
        self.status.setText('Scanning user-installed packages…')
        def done(apps):
            self.serial = serial; self.apps = apps; self.actions = []
            self.table.setRowCount(len(apps))
            for row, app in enumerate(apps):
                for col, val in enumerate((app['package'], app['installer'], app['risk'], '; '.join(app['reasons']))):
                    cell = QTableWidgetItem(val)
                    if app['protected']: cell.setToolTip('Protected package: removal blocked')
                    self.table.setItem(row, col, cell)
            self.status.setText(f'{len(apps)} third-party packages inventoried; {sum(a["risk"] == "Review" for a in apps)} flagged for manual review.')
        self.run_job(lambda: inventory(serial), done)

    def uninstall(self):
        if self.mode.currentText() != 'Technician': return
        row = self.table.currentRow()
        if row < 0 or row >= len(self.apps): return
        app = self.apps[row]; package = app['package']
        if app['protected']:
            QMessageBox.warning(self, 'Blocked', 'Protected platform/vendor package.'); return
        answer = QMessageBox.question(self, 'Confirm uninstall',
            f'Uninstall {package} for Android user 0?\n\nThis can delete app data and may be difficult to reverse. Back up customer data first.\nThis is NOT quarantine. Continue?',
            QMessageBox.StandardButton.Yes | QMessageBox.StandardButton.No, QMessageBox.StandardButton.No)
        if answer != QMessageBox.StandardButton.Yes: return
        self.status.setText(f'Uninstalling {package}…')
        def done(result):
            self.actions.append({'action': 'uninstall_user_0', 'package': package, 'result': result})
            self.status.setText(f'{package}: {result}. Rescan to refresh inventory.')
        self.run_job(lambda: remove_for_user(self.serial, package), done)

    def save_report(self):
        if not self.serial:
            QMessageBox.information(self, 'No scan', 'Scan a phone first.'); return
        path, _ = QFileDialog.getSaveFileName(self, 'Save report', 'android-cleaner-report.json', 'JSON (*.json)')
        if path:
            try:
                export_report(path, self.serial, self.apps, self.actions)
                self.status.setText(f'Report saved: {path}')
            except OSError as e: QMessageBox.warning(self, 'Export failed', str(e))

def main():
    app = QApplication(sys.argv)
    app.setStyle('Fusion')
    window = Window(); window.show()
    sys.exit(app.exec())

if __name__ == '__main__': main()
