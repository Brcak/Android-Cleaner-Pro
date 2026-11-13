"""Technician-only desktop workstation for Android package triage."""
import sys
from PySide6.QtCore import QObject, QThread, Signal, Qt
from PySide6.QtGui import QColor
from PySide6.QtWidgets import (
    QApplication, QMainWindow, QWidget, QVBoxLayout, QHBoxLayout, QLabel,
    QComboBox, QPushButton, QTableWidget, QTableWidgetItem, QMessageBox,
    QFileDialog, QHeaderView, QFrame, QAbstractItemView, QSplitter
)
from .core import devices, inventory, remove_for_user, export_report

STYLE = """
QWidget { background:#0b1020; color:#e7edf8; font-family:'Segoe UI'; font-size:13px; }
QMainWindow { background:#0b1020; }
QFrame#sidebar { background:#101a2e; border-right:1px solid #24334d; }
QFrame#panel { background:#131f35; border:1px solid #263750; border-radius:14px; }
QLabel#brand { color:#f1f6ff; font-size:22px; font-weight:800; }
QLabel#heading { font-size:18px; font-weight:700; }
QLabel#muted { color:#90a3bd; }
QLabel#metric { color:#69c7ff; font-size:27px; font-weight:800; }
QPushButton { background:#1b2c48; border:1px solid #324969; border-radius:9px;
 padding:11px 14px; font-weight:600; }
QPushButton:hover { background:#284568; border-color:#56baff; }
QPushButton:disabled { color:#677a92; background:#142036; }
QPushButton#primary { background:#087ac9; border-color:#219de9; color:white; }
QPushButton#primary:hover { background:#1098ee; }
QPushButton#danger { background:#402334; border-color:#a34a64; color:#ffb9c5; }
QPushButton#danger:hover { background:#6b2a42; }
QComboBox { background:#17253e; border:1px solid #344967; border-radius:8px; padding:9px; }
QComboBox QAbstractItemView { background:#17253e; selection-background-color:#215780; }
QTableWidget { background:#101a2d; alternate-background-color:#142039; gridline-color:#243550;
 border:1px solid #2c405c; border-radius:9px; selection-background-color:#1d4d77; }
QHeaderView::section { background:#1c2b44; color:#a9c5e4; padding:11px;
 border:0; border-bottom:1px solid #36506d; font-weight:700; }
QTableWidget::item { padding:8px; }
"""

class Worker(QObject):
    done = Signal(object)
    error = Signal(str)
    finished = Signal()
    def __init__(self, fn):
        super().__init__()
        self.fn = fn
    def run(self):
        try:
            self.done.emit(self.fn())
        except Exception as exc:
            self.error.emit(str(exc))
        finally:
            self.finished.emit()

class Window(QMainWindow):
    def __init__(self):
        super().__init__()
        self.setWindowTitle("Android Cleaner Pro | Technician Workstation")
        self.resize(1260, 780)
        self.setMinimumSize(950, 620)
        self.apps, self.actions = [], []
        self.serial = None
        self.busy = False
        self.setStyleSheet(STYLE)

        shell = QWidget()
        self.setCentralWidget(shell)
        horizontal = QHBoxLayout(shell)
        horizontal.setContentsMargins(0, 0, 0, 0)
        horizontal.setSpacing(0)

        sidebar = QFrame()
        sidebar.setObjectName("sidebar")
        sidebar.setFixedWidth(245)
        side = QVBoxLayout(sidebar)
        side.setContentsMargins(22, 28, 22, 22)
        side.setSpacing(17)
        brand = QLabel("◈  ANDROID\n     CLEANER PRO")
        brand.setObjectName("brand")
        side.addWidget(brand)
        edition = QLabel("TECHNICIAN WORKSTATION  •  v0.2")
        edition.setObjectName("muted")
        side.addWidget(edition)
        side.addSpacing(28)
        side.addWidget(self._side_label("●   DEVICE INSPECTION"))
        side.addWidget(self._side_label("▤   PACKAGE ANALYSIS"))
        side.addWidget(self._side_label("⚑   REMOVAL REVIEW"))
        side.addWidget(self._side_label("▧   REPAIR REPORTS"))
        side.addStretch()
        side.addWidget(self._side_label("ADB CONNECTION REQUIRED"))
        side.addWidget(self._side_label("No automatic app deletion"))
        horizontal.addWidget(sidebar)

        main = QWidget()
        body = QVBoxLayout(main)
        body.setContentsMargins(26, 26, 26, 24)
        body.setSpacing(16)
        horizontal.addWidget(main, 1)
        title = QLabel("Device Security Overview")
        title.setObjectName("heading")
        body.addWidget(title)
        subtitle = QLabel("Inspect third-party Android packages, review findings, and document service actions.")
        subtitle.setObjectName("muted")
        body.addWidget(subtitle)

        connection = QFrame()
        connection.setObjectName("panel")
        connect_layout = QHBoxLayout(connection)
        connect_layout.setContentsMargins(16, 14, 16, 14)
        connect_layout.addWidget(QLabel("CONNECTED DEVICE"))
        self.device = QComboBox()
        connect_layout.addWidget(self.device, 1)
        self.refresh = QPushButton("↻  Refresh devices")
        self.scan = QPushButton("◉  Run inspection")
        self.scan.setObjectName("primary")
        connect_layout.addWidget(self.refresh)
        connect_layout.addWidget(self.scan)
        body.addWidget(connection)

        metrics = QHBoxLayout()
        self.count_label = self._metric("0", "PACKAGES INVENTORIED", metrics)
        self.review_label = self._metric("0", "REQUIRING REVIEW", metrics)
        self.action_label = self._metric("0", "REMOVAL ACTIONS", metrics)
        body.addLayout(metrics)

        section = QHBoxLayout()
        section.addWidget(QLabel("Package Analysis"))
        section.addStretch()
        section.addWidget(QLabel("Heuristic indicators • Not a malware verdict"))
        body.addLayout(section)

        self.table = QTableWidget(0, 4)
        self.table.setHorizontalHeaderLabels(["PACKAGE IDENTIFIER", "INSTALLER", "ASSESSMENT", "REVIEW NOTES"])
        self.table.horizontalHeader().setSectionResizeMode(0, QHeaderView.ResizeMode.Stretch)
        self.table.horizontalHeader().setSectionResizeMode(1, QHeaderView.ResizeMode.ResizeToContents)
        self.table.horizontalHeader().setSectionResizeMode(2, QHeaderView.ResizeMode.ResizeToContents)
        self.table.horizontalHeader().setSectionResizeMode(3, QHeaderView.ResizeMode.Stretch)
        self.table.setSelectionBehavior(QAbstractItemView.SelectionBehavior.SelectRows)
        self.table.setSelectionMode(QAbstractItemView.SelectionMode.SingleSelection)
        self.table.setEditTriggers(QAbstractItemView.EditTrigger.NoEditTriggers)
        self.table.setAlternatingRowColors(True)
        self.table.verticalHeader().hide()
        body.addWidget(self.table, 1)

        actions = QHBoxLayout()
        self.remove = QPushButton("✕  Uninstall selected package")
        self.remove.setObjectName("danger")
        self.report = QPushButton("▧  Export diagnostic report")
        actions.addWidget(self.remove)
        actions.addStretch()
        actions.addWidget(self.report)
        body.addLayout(actions)
        self.status = QLabel("Ready • Connect a phone and approve USB debugging.")
        self.status.setObjectName("muted")
        body.addWidget(self.status)

        self.refresh.clicked.connect(self.find_devices)
        self.scan.clicked.connect(self.scan_apps)
        self.remove.clicked.connect(self.uninstall)
        self.report.clicked.connect(self.save_report)
        self._update_actions()
        self.find_devices()

    def _side_label(self, value):
        label = QLabel(value)
        label.setObjectName("muted")
        return label

    def _metric(self, number, caption, row):
        card = QFrame()
        card.setObjectName("panel")
        box = QVBoxLayout(card)
        box.setContentsMargins(18, 13, 18, 13)
        value = QLabel(number)
        value.setObjectName("metric")
        box.addWidget(value)
        label = QLabel(caption)
        label.setObjectName("muted")
        box.addWidget(label)
        row.addWidget(card, 1)
        return value

    def _update_actions(self):
        self.refresh.setEnabled(not self.busy)
        self.scan.setEnabled(not self.busy)
        self.remove.setEnabled(not self.busy and bool(self.apps))
        self.report.setEnabled(not self.busy and self.serial is not None)

    def run_job(self, fn, callback):
        if self.busy:
            return
        self.busy = True
        self._update_actions()
        thread = QThread(self)
        worker = Worker(fn)
        worker.moveToThread(thread)
        thread.started.connect(worker.run)
        worker.done.connect(callback)
        worker.error.connect(self.show_error)
        worker.finished.connect(thread.quit)
        worker.finished.connect(worker.deleteLater)
        thread.finished.connect(thread.deleteLater)
        thread.finished.connect(lambda: self.job_finished(thread))
        self._thread = thread
        thread.start()

    def job_finished(self, thread):
        self.busy = False
        self._update_actions()

    def show_error(self, message):
        self.status.setText("Error • " + message)
        QMessageBox.warning(self, "ADB operation failed", message)

    def find_devices(self):
        self.status.setText("Searching for connected Android devices…")
        def done(found):
            self.device.clear()
            for entry in found:
                self.device.addItem(f"{entry['serial']}  •  {entry['state']}", entry)
            self.status.setText(f"{len(found)} device(s) discovered. Select an authorized device.")
        self.run_job(devices, done)

    def selected_serial(self):
        entry = self.device.currentData()
        if not entry or entry["state"] != "device":
            QMessageBox.warning(self, "No authorized device", "Connect an Android phone and authorize USB debugging.")
            return None
        return entry["serial"]

    def scan_apps(self):
        serial = self.selected_serial()
        if not serial:
            return
        self.status.setText("Inspecting third-party packages…")
        def done(apps):
            self.serial = serial
            self.apps = apps
            self.actions = []
            self.table.setRowCount(len(apps))
            for row, item in enumerate(apps):
                values = (item["package"], item["installer"], item["risk"], "; ".join(item["reasons"]))
                for col, value in enumerate(values):
                    cell = QTableWidgetItem(value)
                    if col == 2:
                        cell.setForeground(QColor("#ffbc74" if item["risk"] == "Review" else "#87cbb2"))
                    if item["protected"]:
                        cell.setToolTip("Protected vendor/platform namespace: removal blocked")
                    self.table.setItem(row, col, cell)
                self.table.setRowHeight(row, 40)
            self.count_label.setText(str(len(apps)))
            self.review_label.setText(str(sum(item["risk"] == "Review" for item in apps)))
            self.action_label.setText("0")
            self.status.setText("Inspection complete • Findings require technician interpretation.")
            self._update_actions()
        self.run_job(lambda: inventory(serial), done)

    def uninstall(self):
        row = self.table.currentRow()
        if row < 0 or row >= len(self.apps):
            QMessageBox.information(self, "Select package", "Select a package in the table first.")
            return
        package = self.apps[row]["package"]
        if self.apps[row]["protected"]:
            QMessageBox.warning(self, "Protected package", "Vendor/platform namespace is protected.")
            return
        if self.selected_serial() != self.serial:
            QMessageBox.warning(self, "Device changed", "Rescan the currently selected device before removing packages.")
            return
        confirmation = QMessageBox.question(
            self, "Confirm technician removal",
            f"Uninstall {package} for Android user 0?\n\nThis is not quarantine and can delete customer app data. "
            "Confirm customer authorization and backup before proceeding.",
            QMessageBox.StandardButton.Yes | QMessageBox.StandardButton.No,
            QMessageBox.StandardButton.No
        )
        if confirmation != QMessageBox.StandardButton.Yes:
            return
        self.status.setText("Uninstalling selected package…")
        def done(result):
            self.actions.append({"action": "uninstall_user_0", "package": package, "result": result})
            self.action_label.setText(str(len(self.actions)))
            self.status.setText(f"{package}: {result} • Rescan to refresh package list.")
        self.run_job(lambda: remove_for_user(self.serial, package), done)

    def save_report(self):
        if not self.serial:
            QMessageBox.information(self, "No inspection", "Inspect a device first.")
            return
        path, _ = QFileDialog.getSaveFileName(
            self, "Export technician report", "android-cleaner-report.json", "JSON files (*.json)"
        )
        if path:
            try:
                export_report(path, self.serial, self.apps, self.actions)
                self.status.setText("Diagnostic report saved • " + path)
            except OSError as exc:
                QMessageBox.warning(self, "Export failed", str(exc))

def main():
    app = QApplication(sys.argv)
    app.setStyle("Fusion")
    window = Window()
    window.show()
    sys.exit(app.exec())

if __name__ == "__main__":
    main()
