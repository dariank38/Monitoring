import express from 'express';
import cors from 'cors';
import fs from 'fs';
import { PORT, uploadsDir, thumbsDir, adminDist } from './config.js';
import { adminAuth } from './auth.js';
import { markOfflineMachines, cleanupOldScreenshots, cleanupOrphanedFiles } from './cleanup.js';
import clientRoutes from './routes/client.js';
import adminRoutes from './routes/admin.js';

const app = express();

app.use(cors());
app.use(express.json({ limit: '10mb' }));

// --- Background tasks ---

setInterval(markOfflineMachines, 10000);
setInterval(cleanupOldScreenshots, 3600000);
cleanupOrphanedFiles();

// --- Routes ---

app.use('/api', clientRoutes);
app.use('/api', adminRoutes);
app.use('/uploads', adminAuth, express.static(uploadsDir));

// Unknown API routes return JSON 404
app.use('/api', (req, res) => {
  res.status(404).json({ error: 'Not found' });
});

// Serve React admin panel (production build)
if (fs.existsSync(adminDist)) {
  app.use(express.static(adminDist));
  app.get('*', (req, res) => {
    res.sendFile(`${adminDist}/index.html`);
  });
}

app.listen(PORT, '0.0.0.0', () => {
  console.log(`Monitoring server running on http://0.0.0.0:${PORT}`);
});
