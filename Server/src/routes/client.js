import express from 'express';
import multer from 'multer';
import fs from 'fs';
import path from 'path';
import sharp from 'sharp';
import db from '../db.js';
import { uploadsDir, thumbsDir } from '../config.js';
import { toLaosTime, laosNowSqlite, sanitizePathComponent } from '../utils.js';

const router = express.Router();

const storage = multer.diskStorage({
  destination: (req, file, cb) => {
    const machineDir = path.join(uploadsDir, sanitizePathComponent(req.headers['x-hardware-id']));
    if (!fs.existsSync(machineDir)) {
      fs.mkdirSync(machineDir, { recursive: true });
    }
    cb(null, machineDir);
  },
  filename: (req, file, cb) => {
    cb(null, sanitizePathComponent(file.originalname));
  }
});
const upload = multer({ storage, limits: { fileSize: 20 * 1024 * 1024 } });

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

function getSetting(key, defaultValue = null) {
  const row = db.prepare('SELECT value FROM settings WHERE key = ?').get(key);
  return row ? row.value : defaultValue;
}

router.post('/heartbeat', (req, res) => {
  const { hardware_id, computer_name, timezone } = req.body;
  if (!hardware_id) return res.status(400).json({ error: 'hardware_id required' });

  upsertMachine(hardware_id, computer_name || 'unknown', timezone || '');
  const captureIntervalSec = parseInt(getSetting('capture_interval_sec', '90'), 10);
  console.log(`[heartbeat] ${computer_name} (${hardware_id.substring(0, 12)}...) tz=${timezone || 'n/a'}`);
  res.json({ ok: true, capture_interval_sec: captureIntervalSec });
});

router.post('/screenshots', upload.single('screenshot'), async (req, res) => {
  const hardwareId = req.headers['x-hardware-id'];
  const computerName = req.headers['x-computer-name'] || 'unknown';
  if (!hardwareId) return res.status(400).json({ error: 'x-hardware-id header required' });

  upsertMachine(hardwareId, computerName, req.headers['x-timezone'] || '');

  if (!req.file) return res.status(400).json({ error: 'screenshot file required' });

  const safeHardwareId = sanitizePathComponent(hardwareId);
  const safeFilename = sanitizePathComponent(req.file.originalname);
  const capturedAt = toLaosTime(req.body.captured_at || new Date().toISOString());
  const origPath = path.join(uploadsDir, safeHardwareId, safeFilename);

  const isJpeg = /\.jpe?g$/i.test(safeFilename);
  let finalFilename = safeFilename;
  let finalPath = origPath;

  if (!isJpeg) {
    const jpegFilename = safeFilename.replace(/\.png$/i, '.jpg');
    const jpegPath = path.join(uploadsDir, safeHardwareId, jpegFilename);
    try {
      await sharp(origPath).jpeg({ quality: 80 }).toFile(jpegPath);
    } catch (e) {
      console.error('[screenshot] sharp conversion failed:', e.message);
      return res.status(422).json({ error: 'image conversion failed' });
    }
    try { fs.unlinkSync(origPath); } catch {}
    finalFilename = jpegFilename;
    finalPath = jpegPath;
  }

  const thumbName = `${safeHardwareId}_${finalFilename}`;
  const thumbPath = path.join(thumbsDir, thumbName);
  try {
    await sharp(finalPath).resize(320, 200, { fit: 'cover' }).jpeg({ quality: 70 }).toFile(thumbPath);
  } catch (e) {
    console.error('[screenshot] thumbnail generation failed:', e.message);
  }

  let fileSize = req.file.size;
  try { fileSize = fs.statSync(finalPath).size; } catch {}

  const stmt = db.prepare(`
    INSERT OR IGNORE INTO screenshots (hardware_id, filename, captured_at, file_size)
    VALUES (?, ?, ?, ?)
  `);
  stmt.run(hardwareId, finalFilename, capturedAt, fileSize);

  res.json({ ok: true, filename: finalFilename });
});

router.post('/worklogs', (req, res) => {
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

export default router;
export { upsertMachine, getSetting };
