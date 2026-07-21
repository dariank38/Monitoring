import express from 'express';
import cors from 'cors';
import multer from 'multer';
import path from 'path';
import fs from 'fs';
import sharp from 'sharp';
import { fileURLToPath } from 'url';

import db from './db.js';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const app = express();
const PORT = process.env.PORT || 3000;

const uploadsDir = path.join(__dirname, '..', 'uploads');
const thumbsDir = path.join(__dirname, '..', 'uploads', '_thumbs');
if (!fs.existsSync(uploadsDir)) fs.mkdirSync(uploadsDir, { recursive: true });
if (!fs.existsSync(thumbsDir)) fs.mkdirSync(thumbsDir, { recursive: true });

const RETENTION_DAYS = 7;
const LAOS_OFFSET_HOURS = 7;

// Convert UTC ISO string to Laos time (UTC+7) in 'YYYY-MM-DD HH:MM:SS' format
function toLaosTime(utcIso) {
  const d = new Date(utcIso);
  if (isNaN(d)) return new Date().toISOString().replace('T', ' ').substring(0, 19);
  const laos = new Date(d.getTime() + LAOS_OFFSET_HOURS * 3600000);
  return laos.toISOString().replace('T', ' ').substring(0, 19);
}

// Get current Laos time for SQLite datetime()
function laosNowSqlite() {
  return `datetime('now', '+${LAOS_OFFSET_HOURS} hours')`;
}

app.use(cors());
app.use(express.json({ limit: '10mb' }));

const storage = multer.diskStorage({
  destination: (req, file, cb) => {
    const machineDir = path.join(uploadsDir, req.headers['x-hardware-id'] || 'unknown');
    if (!fs.existsSync(machineDir)) {
      fs.mkdirSync(machineDir, { recursive: true });
    }
    cb(null, machineDir);
  },
  filename: (req, file, cb) => {
    cb(null, file.originalname);
  }
});
const upload = multer({ storage });

function upsertMachine(hardwareId, computerName, timezone) {
  const stmt = db.prepare(`
    INSERT INTO machines (hardware_id, computer_name, timezone, last_seen, is_online)
    VALUES (?, ?, ?, ${laosNowSqlite()}, 1)
    ON CONFLICT(hardware_id) DO UPDATE SET
      computer_name = excluded.computer_name,
      timezone = excluded.timezone,
      last_seen = ${laosNowSqlite()},
      is_online = 1
  `);
  stmt.run(hardwareId, computerName, timezone || '');
}

function markOfflineMachines() {
  db.prepare(`
    UPDATE machines SET is_online = 0
    WHERE last_seen < datetime('now', '+${LAOS_OFFSET_HOURS} hours', '-60 seconds')
  `).run();
}

setInterval(markOfflineMachines, 10000);

function cleanupOldScreenshots() {
  const cutoffUtc = new Date(Date.now() - RETENTION_DAYS * 86400000).toISOString();
  const cutoff = toLaosTime(cutoffUtc);
  const old = db.prepare('SELECT id, hardware_id, filename FROM screenshots WHERE captured_at < ?').all(cutoff);
  for (const s of old) {
    const filePath = path.join(uploadsDir, s.hardware_id, s.filename);
    const thumbPath = path.join(thumbsDir, s.filename.replace(/\.png$/i, '.jpg'));
    try { if (fs.existsSync(filePath)) fs.unlinkSync(filePath); } catch {}
    try { if (fs.existsSync(thumbPath)) fs.unlinkSync(thumbPath); } catch {}
  }
  if (old.length > 0) {
    db.prepare('DELETE FROM screenshots WHERE captured_at < ?').run(cutoff);
    console.log(`[cleanup] Deleted ${old.length} screenshots older than ${RETENTION_DAYS} days`);
  }
}
setInterval(cleanupOldScreenshots, 3600000);

// --- Client API -----

function getSetting(key, defaultValue = null) {
  const row = db.prepare('SELECT value FROM settings WHERE key = ?').get(key);
  return row ? row.value : defaultValue;
}

app.post('/api/heartbeat', (req, res) => {
  const { hardware_id, computer_name, timezone } = req.body;
  if (!hardware_id) return res.status(400).json({ error: 'hardware_id required' });

  upsertMachine(hardware_id, computer_name || 'unknown', timezone || '');
  const captureIntervalSec = parseInt(getSetting('capture_interval_sec', '90'), 10);
  console.log(`[heartbeat] ${computer_name} (${hardware_id.substring(0, 12)}...) tz=${timezone || 'n/a'}`);
  res.json({ ok: true, capture_interval_sec: captureIntervalSec });
});

app.post('/api/screenshots', upload.single('screenshot'), async (req, res) => {
  const hardwareId = req.headers['x-hardware-id'];
  const computerName = req.headers['x-computer-name'] || 'unknown';
  if (!hardwareId) return res.status(400).json({ error: 'x-hardware-id header required' });

  upsertMachine(hardwareId, computerName, req.headers['x-timezone'] || '');

  if (!req.file) return res.status(400).json({ error: 'screenshot file required' });

  const capturedAt = toLaosTime(req.body.captured_at || new Date().toISOString());
  const origPath = path.join(uploadsDir, hardwareId, req.file.originalname);

  const isJpeg = /\.jpe?g$/i.test(req.file.originalname);
  let finalFilename = req.file.originalname;
  let finalPath = origPath;

  if (!isJpeg) {
    const jpegFilename = req.file.originalname.replace(/\.png$/i, '.jpg');
    const jpegPath = path.join(uploadsDir, hardwareId, jpegFilename);
    try {
      await sharp(origPath).jpeg({ quality: 80 }).toFile(jpegPath);
    } catch (e) {
      console.error('[screenshot] sharp conversion failed:', e.message);
    }
    try { fs.unlinkSync(origPath); } catch {}
    finalFilename = jpegFilename;
    finalPath = jpegPath;
  }

  const thumbName = finalFilename;
  const thumbPath = path.join(thumbsDir, thumbName);
  sharp(finalPath).resize(320, 200, { fit: 'cover' }).jpeg({ quality: 70 }).toFile(thumbPath).catch(() => {});

  const stmt = db.prepare(`
    INSERT OR IGNORE INTO screenshots (hardware_id, filename, captured_at, file_size)
    VALUES (?, ?, ?, ?)
  `);
  stmt.run(hardwareId, finalFilename, capturedAt, req.file.size);

  res.json({ ok: true, filename: finalFilename });
});

app.post('/api/worklogs', (req, res) => {
  const hardwareId = req.headers['x-hardware-id'];
  const computerName = req.headers['x-computer-name'] || 'unknown';
  if (!hardwareId) return res.status(400).json({ error: 'x-hardware-id header required' });

  upsertMachine(hardwareId, computerName, req.headers['x-timezone'] || '');

  const { logs } = req.body;
  if (!Array.isArray(logs)) return res.status(400).json({ error: 'logs array required' });

  const stmt = db.prepare(`
    INSERT OR IGNORE INTO work_logs (hardware_id, log_date, start_time, end_time, duration_sec, status, key_count, mouse_count)
    VALUES (?, ?, ?, ?, ?, ?, ?, ?)
  `);

  db.exec('BEGIN');
  try {
    for (const log of logs) {
      stmt.run(
        hardwareId,
        log.date,
        log.start,
        log.end,
        log.duration_sec,
        log.status,
        log.key_count || 0,
        log.mouse_count || 0
      );
    }
    db.exec('COMMIT');
  } catch (err) {
    db.exec('ROLLBACK');
    throw err;
  }
  res.json({ ok: true, count: logs.length });
});

// --- Admin API ---

app.get('/api/machines', (req, res) => {
  const machines = db.prepare(`
    SELECT m.*,
      (SELECT COUNT(*) FROM screenshots WHERE hardware_id = m.hardware_id) as screenshot_count,
      (SELECT SUM(duration_sec) FROM work_logs WHERE hardware_id = m.hardware_id AND status = 'Active') as total_active_sec,
      (SELECT id FROM screenshots WHERE hardware_id = m.hardware_id ORDER BY id DESC LIMIT 1) as latest_screenshot_id
    FROM machines m
    ORDER BY m.last_seen DESC
  `).all();
  res.json(machines);
});

app.get('/api/machines/:hardwareId', (req, res) => {
  const { hardwareId } = req.params;
  const machine = db.prepare('SELECT * FROM machines WHERE hardware_id = ?').get(hardwareId);
  if (!machine) return res.status(404).json({ error: 'Machine not found' });
  res.json(machine);
});

app.get('/api/machines/:hardwareId/screenshots', (req, res) => {
  const { hardwareId } = req.params;
  const { limit = 24, cursor, date, hour, from, to } = req.query;

  let query = 'SELECT * FROM screenshots WHERE hardware_id = ?';
  const params = [hardwareId];

  if (date) {
    query += ' AND captured_at LIKE ?';
    params.push(`${date}%`);
    if (hour !== undefined) {
      const h = String(hour).padStart(2, '0');
      query += ' AND captured_at LIKE ?';
      params.push(`%T${h}:%`);
    }
  }

  if (from) {
    query += ' AND captured_at >= ?';
    params.push(from);
  }
  if (to) {
    query += ' AND captured_at <= ?';
    params.push(to);
  }

  if (cursor) {
    query += ' AND id < ?';
    params.push(Number(cursor));
  }

  query += ' ORDER BY id DESC LIMIT ?';
  params.push(Number(limit) + 1);

  const rows = db.prepare(query).all(...params);
  const hasMore = rows.length > Number(limit);
  const items = hasMore ? rows.slice(0, Number(limit)) : rows;
  const nextCursor = hasMore ? items[items.length - 1].id : null;

  res.json({ items, nextCursor, hasMore });
});

app.get('/api/screenshots/:id/file', (req, res) => {
  const { id } = req.params;
  const screenshot = db.prepare('SELECT * FROM screenshots WHERE id = ?').get(id);
  if (!screenshot) return res.status(404).json({ error: 'Screenshot not found' });

  const filePath = path.join(uploadsDir, screenshot.hardware_id, screenshot.filename);
  if (!fs.existsSync(filePath)) return res.status(404).json({ error: 'File not found on disk' });

  res.set('Cache-Control', 'public, max-age=86400');
  res.sendFile(filePath);
});

app.get('/api/screenshots/:id/thumbnail', (req, res) => {
  const { id } = req.params;
  const screenshot = db.prepare('SELECT * FROM screenshots WHERE id = ?').get(id);
  if (!screenshot) return res.status(404).json({ error: 'Screenshot not found' });

  const thumbPath = path.join(thumbsDir, screenshot.filename);

  if (fs.existsSync(thumbPath)) {
    res.set('Cache-Control', 'public, max-age=86400');
    return res.sendFile(thumbPath);
  }

  const filePath = path.join(uploadsDir, screenshot.hardware_id, screenshot.filename);
  if (!fs.existsSync(filePath)) return res.status(404).json({ error: 'File not found on disk' });

  sharp(filePath).resize(320, 200, { fit: 'cover' }).jpeg({ quality: 70 }).toFile(thumbPath)
    .then(() => {
      res.set('Cache-Control', 'public, max-age=86400');
      res.sendFile(thumbPath);
    })
    .catch(() => res.status(500).json({ error: 'Thumbnail generation failed' }));
});

app.get('/api/machines/:hardwareId/worklogs', (req, res) => {
  const { hardwareId } = req.params;
  const { date } = req.query;

  let query = 'SELECT * FROM work_logs WHERE hardware_id = ?';
  const params = [hardwareId];

  if (date) {
    query += ' AND log_date = ?';
    params.push(date);
  }

  query += ' ORDER BY log_date DESC, start_time DESC';
  const logs = db.prepare(query).all(...params);
  res.json(logs);
});

app.get('/api/machines/:hardwareId/worklogs/summary', (req, res) => {
  const { hardwareId } = req.params;
  const { days, from, to } = req.query;
  let query = `
    SELECT log_date,
      SUM(CASE WHEN status = 'Active' THEN duration_sec ELSE 0 END) as active_sec,
      SUM(CASE WHEN status = 'Idle' THEN duration_sec ELSE 0 END) as idle_sec,
      SUM(key_count) as total_keys,
      SUM(mouse_count) as total_mouse
    FROM work_logs
    WHERE hardware_id = ?`;
  const params = [hardwareId];
  if (from) {
    query += ` AND log_date >= ?`;
    params.push(from);
  }
  if (to) {
    query += ` AND log_date <= ?`;
    params.push(to);
  }
  if (days) {
    query += ` AND log_date >= date('now', ?)`;
    params.push(`-${days} days`);
  }
  query += ` GROUP BY log_date ORDER BY log_date DESC`;
  const summary = db.prepare(query).all(...params);
  res.json(summary);
});

app.get('/api/machines/:hardwareId/worklogs/heatmap', (req, res) => {
  const { hardwareId } = req.params;
  const { days, from, to } = req.query;
  let query = `
    SELECT log_date, start_time, end_time, duration_sec, status, key_count, mouse_count
    FROM work_logs
    WHERE hardware_id = ? AND status = 'Active'`;
  const params = [hardwareId];
  if (from) {
    query += ` AND log_date >= ?`;
    params.push(from);
  }
  if (to) {
    query += ` AND log_date <= ?`;
    params.push(to);
  }
  if (days) {
    query += ` AND log_date >= date('now', ?)`;
    params.push(`-${days} days`);
  }
  query += ` ORDER BY log_date ASC, start_time ASC`;
  const logs = db.prepare(query).all(...params);

  const buckets = {};

  for (const log of logs) {
    const startStr = `${log.log_date} ${log.start_time}`;
    const endStr = `${log.log_date} ${log.end_time}`;
    const start = new Date(startStr);
    const end = new Date(endStr);
    if (isNaN(start) || isNaN(end)) continue;

    let current = start;
    while (current < end) {
      const hourEnd = new Date(current);
      hourEnd.setMinutes(0, 0, 0);
      hourEnd.setHours(hourEnd.getHours() + 1);
      const segEnd = end < hourEnd ? end : hourEnd;
      const segSec = Math.floor((segEnd - current) / 1000);
      const dateKey = current.toISOString().slice(0, 10);
      const hour = current.getHours();
      const key = `${dateKey}|${hour}`;

      if (!buckets[key]) {
        buckets[key] = {
          date: dateKey,
          hour,
          active_sec: 0,
          key_count: 0,
          mouse_count: 0,
        };
      }

      const ratio = segSec / log.duration_sec;
      buckets[key].active_sec += segSec;
      buckets[key].key_count += Math.round(log.key_count * ratio);
      buckets[key].mouse_count += Math.round(log.mouse_count * ratio);

      current = segEnd;
    }
  }

  res.json(Object.values(buckets));
});

app.use('/uploads', express.static(uploadsDir));

// --- Admin Settings API ---

app.get('/api/settings', (req, res) => {
  const rows = db.prepare('SELECT key, value FROM settings').all();
  const settings = {};
  for (const row of rows) settings[row.key] = row.value;
  res.json(settings);
});

app.put('/api/settings', (req, res) => {
  const { capture_interval_sec } = req.body;
  const updates = [];
  if (capture_interval_sec !== undefined) {
    const val = parseInt(capture_interval_sec, 10);
    if (isNaN(val) || val < 10) return res.status(400).json({ error: 'capture_interval_sec must be a number >= 10' });
    updates.push(['capture_interval_sec', String(val)]);
  }
  const stmt = db.prepare(`INSERT INTO settings (key, value) VALUES (?, ?) ON CONFLICT(key) DO UPDATE SET value = excluded.value`);
  for (const [key, value] of updates) stmt.run(key, value);
  res.json({ ok: true });
});

// Serve React admin panel (production build)
const adminDist = path.join(__dirname, '..', 'admin', 'dist');
if (fs.existsSync(adminDist)) {
  app.use(express.static(adminDist));
  app.get('*', (req, res) => {
    res.sendFile(path.join(adminDist, 'index.html'));
  });
}

app.listen(PORT, '0.0.0.0', () => {
  console.log(`Monitoring server running on http://0.0.0.0:${PORT}`);
});
