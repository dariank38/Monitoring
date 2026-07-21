import fs from 'fs';
import path from 'path';
import db from './db.js';
import { uploadsDir, thumbsDir, LAOS_OFFSET_HOURS } from './config.js';
import { toLaosTime, sanitizePathComponent } from './utils.js';

export function markOfflineMachines() {
  db.prepare(`
    UPDATE machines SET is_online = 0
    WHERE last_seen < datetime('now', '+${LAOS_OFFSET_HOURS} hours', '-60 seconds')
  `).run();
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
