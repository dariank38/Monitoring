import crypto from 'crypto';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const tokenFile = path.join(__dirname, '..', '.admin-token');

function loadAdminToken() {
  if (process.env.ADMIN_TOKEN) return process.env.ADMIN_TOKEN;
  try {
    if (fs.existsSync(tokenFile)) return fs.readFileSync(tokenFile, 'utf8').trim();
  } catch {}
  const token = crypto.randomBytes(32).toString('hex');
  try { fs.writeFileSync(tokenFile, token, { mode: 0o600 }); } catch {}
  console.log(`[auth] Generated new admin token. Run 'node scripts/show-token.js' to view it.`);
  return token;
}

export const ADMIN_TOKEN = loadAdminToken();

export function adminAuth(req, res, next) {
  const authHeader = req.headers.authorization || '';
  const bearerMatch = authHeader.match(/^Bearer\s+(.+)$/i);
  const token = bearerMatch ? bearerMatch[1] : req.query.token;
  if (token !== ADMIN_TOKEN) {
    return res.status(401).json({ error: 'Unauthorized' });
  }
  next();
}
