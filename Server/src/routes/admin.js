import express from 'express';
import fs from 'fs';
import path from 'path';
import sharp from 'sharp';
import db from '../db.js';
import { uploadsDir, thumbsDir } from '../config.js';
import { sanitizePathComponent } from '../utils.js';
import { adminAuth, ADMIN_PASSWORD } from '../auth.js';

const router = express.Router();

// --- Auth (public, no adminAuth) ---

router.post('/auth/verify', (req, res) => {
  const authHeader = req.headers.authorization || '';
  const bearerMatch = authHeader.match(/^Bearer\s+(.+)$/i);
  const password = bearerMatch ? bearerMatch[1] : req.body?.password;
  if (password === ADMIN_PASSWORD) {
    return res.json({ ok: true });
  }
  res.status(401).json({ error: 'Invalid password' });
});

// --- All routes below require admin auth ---

router.use(adminAuth);

// --- Machines ---

router.get('/machines', (req, res) => {
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

router.get('/machines/:hardwareId', (req, res) => {
  const { hardwareId } = req.params;
  const machine = db.prepare('SELECT * FROM machines WHERE hardware_id = ?').get(hardwareId);
  if (!machine) return res.status(404).json({ error: 'Machine not found' });
  res.json(machine);
});

// --- Screenshots ---

router.get('/machines/:hardwareId/screenshots', (req, res) => {
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
      params.push(`${date} ${h}:%`);
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

router.get('/screenshots/:id/file', (req, res) => {
  const { id } = req.params;
  const screenshot = db.prepare('SELECT * FROM screenshots WHERE id = ?').get(id);
  if (!screenshot) return res.status(404).json({ error: 'Screenshot not found' });

  const filePath = path.join(uploadsDir, sanitizePathComponent(screenshot.hardware_id), screenshot.filename);
  if (!fs.existsSync(filePath)) return res.status(404).json({ error: 'File not found on disk' });

  res.sendFile(filePath);
});

router.get('/screenshots/:id/thumbnail', (req, res) => {
  const { id } = req.params;
  const screenshot = db.prepare('SELECT * FROM screenshots WHERE id = ?').get(id);
  if (!screenshot) return res.status(404).json({ error: 'Screenshot not found' });

  const safeHw = sanitizePathComponent(screenshot.hardware_id);
  const thumbPath = path.join(thumbsDir, `${safeHw}_${screenshot.filename}`);

  if (fs.existsSync(thumbPath)) {
    return res.sendFile(thumbPath);
  }

  const filePath = path.join(uploadsDir, safeHw, screenshot.filename);
  if (!fs.existsSync(filePath)) return res.status(404).json({ error: 'File not found on disk' });

  sharp(filePath).resize(320, 200, { fit: 'cover' }).jpeg({ quality: 70 }).toFile(thumbPath)
    .then(() => {
      res.sendFile(thumbPath);
    })
    .catch(() => res.status(500).json({ error: 'Thumbnail generation failed' }));
});

// --- Work Logs ---

router.get('/machines/:hardwareId/worklogs', (req, res) => {
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

router.get('/machines/:hardwareId/worklogs/summary', (req, res) => {
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

router.get('/machines/:hardwareId/worklogs/heatmap', (req, res) => {
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
      const y = current.getFullYear();
      const mo = String(current.getMonth() + 1).padStart(2, '0');
      const da = String(current.getDate()).padStart(2, '0');
      const dateKey = `${y}-${mo}-${da}`;
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

      const ratio = log.duration_sec > 0 ? segSec / log.duration_sec : 0;
      buckets[key].active_sec += segSec;
      buckets[key].key_count += Math.round((log.key_count || 0) * ratio);
      buckets[key].mouse_count += Math.round((log.mouse_count || 0) * ratio);

      current = segEnd;
    }
  }

  res.json(Object.values(buckets));
});

// --- Settings ---

router.get('/settings', (req, res) => {
  const rows = db.prepare('SELECT key, value FROM settings').all();
  const settings = {};
  for (const row of rows) settings[row.key] = row.value;
  res.json(settings);
});

router.put('/settings', (req, res) => {
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

export default router;
