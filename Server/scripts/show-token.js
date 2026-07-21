import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const tokenFile = path.join(__dirname, '..', '.admin-token');

if (process.env.ADMIN_TOKEN) {
  console.log('Admin token (from ADMIN_TOKEN env var):');
  console.log(process.env.ADMIN_TOKEN);
} else if (fs.existsSync(tokenFile)) {
  console.log('Admin token (from .admin-token file):');
  console.log(fs.readFileSync(tokenFile, 'utf8').trim());
} else {
  console.log('No admin token found. Start the server first to generate one.');
}
