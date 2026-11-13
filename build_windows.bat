@echo off
py -m pip install -r requirements.txt pyinstaller
py -m PyInstaller --noconfirm --onefile --windowed --name AndroidCleanerPro launcher.py
pause
