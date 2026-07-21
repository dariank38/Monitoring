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

// Wrap async route handlers so rejected promises are forwarded to Express error handler
// instead of crashing the process
export function asyncHandler(fn) {
  return (req, res, next) => Promise.resolve(fn(req, res, next)).catch(next);
}

// Build SQL WHERE clause + params for screenshot date/hour/from/to/cursor filtering
export function buildScreenshotFilter({ date, hour, from, to, cursor } = {}) {
  let clause = '';
  const params = [];

  if (date) {
    clause += ' AND captured_at LIKE ?';
    params.push(`${date}%`);
    if (hour !== undefined && hour !== null) {
      const h = String(hour).padStart(2, '0');
      clause += ' AND captured_at LIKE ?';
      params.push(`${date} ${h}:%`);
    }
  }
  if (from) {
    clause += ' AND captured_at >= ?';
    params.push(from);
  }
  if (to) {
    clause += ' AND captured_at <= ?';
    params.push(to);
  }
  if (cursor) {
    clause += ' AND id < ?';
    params.push(Number(cursor));
  }

  return { clause, params };
}
