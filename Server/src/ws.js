import { WebSocketServer } from 'ws';
import { ADMIN_PASSWORD } from './auth.js';

let wss = null;
const clients = new Set();

export function initWebSocket(server) {
  wss = new WebSocketServer({ server, path: '/ws' });

  wss.on('connection', (ws, req) => {
    // Authenticate via query param ?token=<password>
    const url = new URL(req.url, 'http://localhost');
    const token = url.searchParams.get('token');
    if (token !== ADMIN_PASSWORD) {
      ws.close(4001, 'Unauthorized');
      return;
    }

    clients.add(ws);
    console.log(`[ws] Client connected (${clients.size} total)`);

    ws.on('close', () => {
      clients.delete(ws);
      console.log(`[ws] Client disconnected (${clients.size} total)`);
    });

    ws.on('error', () => {
      clients.delete(ws);
    });
  });
}

export function broadcastScreenshot(hardwareId, computerName, screenshotId, capturedAt) {
  if (clients.size === 0) return;
  const msg = JSON.stringify({
    type: 'screenshot',
    hardware_id: hardwareId,
    computer_name: computerName,
    screenshot_id: screenshotId,
    captured_at: capturedAt,
  });
  for (const ws of clients) {
    if (ws.readyState === ws.OPEN) {
      ws.send(msg);
    }
  }
}
