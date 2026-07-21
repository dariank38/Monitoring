const API_BASE = ''

export function getPassword() {
  return localStorage.getItem('admin_password') || ''
}

export function setPassword(password) {
  localStorage.setItem('admin_password', password)
}

export function clearPassword() {
  localStorage.removeItem('admin_password')
}

export function isAuthenticated() {
  return !!getPassword()
}

function authHeaders() {
  const password = getPassword()
  return password ? { Authorization: `Bearer ${password}` } : {}
}

function authJson(url, options = {}) {
  return fetch(url, {
    ...options,
    headers: {
      ...authHeaders(),
      ...(options.headers || {}),
    },
  })
}

export async function verifyPassword(password) {
  const res = await fetch(`${API_BASE}/api/auth/verify`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ password }),
  })
  return res.ok
}

export async function fetchMachines() {
  const res = await authJson(`${API_BASE}/api/machines`)
  if (res.status === 401) throw new Error('Unauthorized')
  return res.json()
}

export async function fetchMachine(hardwareId) {
  const res = await authJson(`${API_BASE}/api/machines/${hardwareId}`)
  if (res.status === 401) throw new Error('Unauthorized')
  return res.json()
}

export async function fetchScreenshots(hardwareId, limit = 24, cursor, date, hour, from, to) {
  let url = `${API_BASE}/api/machines/${hardwareId}/screenshots?limit=${limit}`
  if (cursor) url += `&cursor=${cursor}`
  if (date) url += `&date=${date}`
  if (hour !== undefined) url += `&hour=${hour}`
  if (from) url += `&from=${from}`
  if (to) url += `&to=${to}`
  const res = await authJson(url)
  if (res.status === 401) throw new Error('Unauthorized')
  return res.json()
}

export async function fetchWorkLogs(hardwareId, date) {
  const url = date
    ? `${API_BASE}/api/machines/${hardwareId}/worklogs?date=${date}`
    : `${API_BASE}/api/machines/${hardwareId}/worklogs`
  const res = await authJson(url)
  if (res.status === 401) throw new Error('Unauthorized')
  return res.json()
}

export async function fetchWorkLogSummary(hardwareId, days, from, to) {
  let url = `${API_BASE}/api/machines/${hardwareId}/worklogs/summary`
  const params = []
  if (days) params.push(`days=${days}`)
  if (from) params.push(`from=${from}`)
  if (to) params.push(`to=${to}`)
  if (params.length) url += `?${params.join('&')}`
  const res = await authJson(url)
  if (res.status === 401) throw new Error('Unauthorized')
  return res.json()
}

export async function fetchHeatmap(hardwareId, days, from, to) {
  let url = `${API_BASE}/api/machines/${hardwareId}/worklogs/heatmap`
  const params = []
  if (days) params.push(`days=${days}`)
  if (from) params.push(`from=${from}`)
  if (to) params.push(`to=${to}`)
  if (params.length) url += `?${params.join('&')}`
  const res = await authJson(url)
  if (res.status === 401) throw new Error('Unauthorized')
  return res.json()
}

export function screenshotUrl(id) {
  const password = getPassword()
  return `${API_BASE}/api/screenshots/${id}/file?token=${encodeURIComponent(password)}`
}

export function thumbnailUrl(id) {
  const password = getPassword()
  return `${API_BASE}/api/screenshots/${id}/thumbnail?token=${encodeURIComponent(password)}`
}

export async function fetchSettings() {
  const res = await authJson(`${API_BASE}/api/settings`)
  if (res.status === 401) throw new Error('Unauthorized')
  return res.json()
}

export async function updateSettings(settings) {
  const res = await authJson(`${API_BASE}/api/settings`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(settings),
  })
  if (res.status === 401) throw new Error('Unauthorized')
  return res.json()
}
