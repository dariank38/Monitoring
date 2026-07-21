import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const passwordFile = path.join(__dirname, '..', '.admin-password');

function loadAdminPassword() {
  if (process.env.ADMIN_PASSWORD) return process.env.ADMIN_PASSWORD;
  try {
    if (fs.existsSync(passwordFile)) return fs.readFileSync(passwordFile, 'utf8').trim();
  } catch {}
  // Default password on first run
  const defaultPassword = 'admin';
  try { fs.writeFileSync(passwordFile, defaultPassword, { mode: 0o600 }); } catch {}
  console.log(`[auth] Created default admin password "admin". Change it in Server/.admin-password or set ADMIN_PASSWORD env var.`);
  return defaultPassword;
}

export const ADMIN_PASSWORD = loadAdminPassword();

export function adminAuth(req, res, next) {
  const authHeader = req.headers.authorization || '';
  const bearerMatch = authHeader.match(/^Bearer\s+(.+)$/i);
  const password = bearerMatch ? bearerMatch[1] : req.query.token;
  if (password !== ADMIN_PASSWORD) {
    return res.status(401).json({ error: 'Unauthorized' });
  }
  next();
}
