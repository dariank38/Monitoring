import { DatabaseSync } from 'node:sqlite';

const db = new DatabaseSync('monitoring.db');

db.exec(`
  CREATE TABLE IF NOT EXISTS machines (
    hardware_id TEXT PRIMARY KEY,
    computer_name TEXT NOT NULL,
    timezone TEXT NOT NULL DEFAULT '',
    first_seen TEXT NOT NULL DEFAULT (datetime('now')),
    last_seen TEXT NOT NULL DEFAULT (datetime('now')),
    is_online INTEGER NOT NULL DEFAULT 0
  );

  CREATE TABLE IF NOT EXISTS screenshots (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    hardware_id TEXT NOT NULL,
    filename TEXT NOT NULL,
    captured_at TEXT NOT NULL,
    file_size INTEGER NOT NULL DEFAULT 0,
    FOREIGN KEY (hardware_id) REFERENCES machines(hardware_id)
  );

  CREATE TABLE IF NOT EXISTS work_logs (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    hardware_id TEXT NOT NULL,
    log_date TEXT NOT NULL,
    start_time TEXT NOT NULL,
    end_time TEXT NOT NULL,
    duration_sec INTEGER NOT NULL,
    status TEXT NOT NULL,
    key_count INTEGER NOT NULL DEFAULT 0,
    mouse_count INTEGER NOT NULL DEFAULT 0,
    UNIQUE(hardware_id, log_date, start_time, end_time),
    FOREIGN KEY (hardware_id) REFERENCES machines(hardware_id)
  );

  CREATE INDEX IF NOT EXISTS idx_screenshots_hw ON screenshots(hardware_id);
  CREATE INDEX IF NOT EXISTS idx_screenshots_date ON screenshots(captured_at);
  CREATE INDEX IF NOT EXISTS idx_screenshots_hw_date ON screenshots(hardware_id, captured_at);
  CREATE INDEX IF NOT EXISTS idx_worklogs_hw ON work_logs(hardware_id);
  CREATE INDEX IF NOT EXISTS idx_worklogs_date ON work_logs(log_date);
  CREATE INDEX IF NOT EXISTS idx_worklogs_hw_date ON work_logs(hardware_id, log_date);
`);

// Settings table for global configuration (capture interval, etc.)
db.exec(`
  CREATE TABLE IF NOT EXISTS settings (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL
  );
`);

// Default settings
db.prepare(`INSERT OR IGNORE INTO settings (key, value) VALUES ('capture_interval_sec', '90')`).run();

// Migration: add timezone column if missing (for existing databases)
try {
  db.exec(`ALTER TABLE machines ADD COLUMN timezone TEXT NOT NULL DEFAULT ''`);
} catch (e) {
  // Column already exists
}

export default db;
