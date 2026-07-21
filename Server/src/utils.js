import { LAOS_OFFSET_HOURS } from './config.js';

export function toLaosTime(utcIso) {
  const d = new Date(utcIso);
  if (isNaN(d)) return new Date().toISOString().replace('T', ' ').substring(0, 19);
  const laos = new Date(d.getTime() + LAOS_OFFSET_HOURS * 3600000);
  return laos.toISOString().replace('T', ' ').substring(0, 19);
}

export function laosNowSqlite() {
  return `datetime('now', '+${LAOS_OFFSET_HOURS} hours')`;
}

export function sanitizePathComponent(name) {
  return String(name || '').replace(/[^a-zA-Z0-9_\-.]/g, '_').replace(/\.\.+/g, '_') || 'unknown';
}
