import express from 'express';
import cors from 'cors';
import fs from 'fs';
import { PORT, uploadsDir, thumbsDir, adminDist } from './config.js';
import { adminAuth } from './auth.js';
import { markOfflineMachines, cleanupOrphanedFiles } from './cleanup.js';
import clientRoutes from './routes/client.js';
import adminRoutes from './routes/admin.js';
import { initWebSocket } from './ws.js';

const app = express();

app.use(cors());
app.use(express.json({ limit: '10mb' }));

// --- Background tasks ---

setInterval(markOfflineMachines, 10000);
setImmediate(cleanupOrphanedFiles);

// --- Routes ---

app.use('/api', clientRoutes);
app.use('/api', adminRoutes);
app.use('/uploads', adminAuth, express.static(uploadsDir));

// Unknown API routes return JSON 404
app.use('/api', (req, res) => {
  res.status(404).json({ error: 'Not found' });
});

// Global error handler — catches unhandled errors from all routes
app.use((err, req, res, _next) => {
  console.error('[error]', err.message);
  res.status(500).json({ error: 'Internal server error' });
});

// Serve React admin panel (production build)
if (fs.existsSync(adminDist)) {
  app.use(express.static(adminDist));
  app.get('*', (req, res) => {
    res.sendFile(`${adminDist}/index.html`);
  });
}

const server = app.listen(PORT, '0.0.0.0', () => {
  console.log(`Monitoring server running on http://0.0.0.0:${PORT}`);
  initWebSocket(server);
  console.log(`WebSocket server running on ws://0.0.0.0:${PORT}/ws`);
});

// Graceful shutdown
function shutdown(signal) {
  console.log(`\n[${signal}] Shutting down gracefully...`);
  server.close(() => {
    console.log('[shutdown] Server closed.');
    process.exit(0);
  });
  // Force exit after 10s if connections are stuck
  setTimeout(() => {
    console.error('[shutdown] Forcing exit after timeout.');
    process.exit(1);
  }, 10000);
}
process.on('SIGTERM', () => shutdown('SIGTERM'));
process.on('SIGINT', () => shutdown('SIGINT'));
process.on('uncaughtException', (err) => {
  console.error('[uncaughtException]', err);
});
process.on('unhandledRejection', (err) => {
  console.error('[unhandledRejection]', err);
});
