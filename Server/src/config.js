import path from 'path';
import fs from 'fs';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

export const PORT = process.env.PORT || 3000;
export const RETENTION_DAYS = 180;
export const LAOS_OFFSET_HOURS = parseInt(process.env.TZ_OFFSET_HOURS || '7', 10);

export const uploadsDir = path.join(__dirname, '..', 'uploads');
export const thumbsDir = path.join(__dirname, '..', 'uploads', '_thumbs');
export const adminDist = path.join(__dirname, '..', 'admin', 'build');

if (!fs.existsSync(uploadsDir)) fs.mkdirSync(uploadsDir, { recursive: true });
if (!fs.existsSync(thumbsDir)) fs.mkdirSync(thumbsDir, { recursive: true });
