import fs from 'fs';
import path from 'path';
import db from './db.js';
import { uploadsDir, thumbsDir, RETENTION_DAYS, LAOS_OFFSET_HOURS } from './config.js';
import { toLaosTime, sanitizePathComponent } from './utils.js';

export function markOfflineMachines() {
  db.prepare(`
    UPDATE machines SET is_online = 0
    WHERE last_seen < datetime('now', '+${LAOS_OFFSET_HOURS} hours', '-60 seconds')
  `).run();
}

export function cleanupOldScreenshots() {
  const cutoffUtc = new Date(Date.now() - RETENTION_DAYS * 86400000).toISOString();
  const cutoff = toLaosTime(cutoffUtc);
  const old = db.prepare('SELECT id, hardware_id, filename FROM screenshots WHERE captured_at < ?').all(cutoff);
  for (const s of old) {
    const safeHw = sanitizePathComponent(s.hardware_id);
    const filePath = path.join(uploadsDir, safeHw, s.filename);
    const thumbPath = path.join(thumbsDir, `${safeHw}_${s.filename.replace(/\.png$/i, '.jpg')}`);
    try { if (fs.existsSync(filePath)) fs.unlinkSync(filePath); } catch {}
    try { if (fs.existsSync(thumbPath)) fs.unlinkSync(thumbPath); } catch {}
  }
  if (old.length > 0) {
    db.prepare('DELETE FROM screenshots WHERE captured_at < ?').run(cutoff);
    console.log(`[cleanup] Deleted ${old.length} screenshots older than ${RETENTION_DAYS} days`);
  }
}

export function cleanupOrphanedFiles() {
  const dbFiles = new Set();
  const rows = db.prepare('SELECT hardware_id, filename FROM screenshots').all();
  for (const r of rows) {
    dbFiles.add(path.join(uploadsDir, sanitizePathComponent(r.hardware_id), r.filename));
  }

  let deleted = 0;
  const machineDirs = fs.existsSync(uploadsDir)
    ? fs.readdirSync(uploadsDir).filter(d => d !== '_thumbs' && fs.statSync(path.join(uploadsDir, d)).isDirectory())
    : [];

  for (const dir of machineDirs) {
    const dirPath = path.join(uploadsDir, dir);
    for (const file of fs.readdirSync(dirPath)) {
      const fullPath = path.join(dirPath, file);
      if (!dbFiles.has(fullPath)) {
        try { fs.unlinkSync(fullPath); deleted++; } catch {}
      }
    }
  }

  if (fs.existsSync(thumbsDir)) {
    const dbThumbs = new Set(rows.map(r => path.join(thumbsDir, `${sanitizePathComponent(r.hardware_id)}_${r.filename}`)));
    for (const file of fs.readdirSync(thumbsDir)) {
      const thumbPath = path.join(thumbsDir, file);
      if (!dbThumbs.has(thumbPath)) {
        try { fs.unlinkSync(thumbPath); deleted++; } catch {}
      }
    }
  }

  if (deleted > 0) console.log(`[startup-cleanup] Deleted ${deleted} orphaned file(s)`);
}
