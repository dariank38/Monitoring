const API_BASE = ''

export async function fetchMachines() {
  const res = await fetch(`${API_BASE}/api/machines`)
  return res.json()
}

export async function fetchMachine(hardwareId) {
  const res = await fetch(`${API_BASE}/api/machines/${hardwareId}`)
  return res.json()
}

export async function fetchScreenshots(hardwareId, limit = 24, cursor, date, hour, from, to) {
  let url = `${API_BASE}/api/machines/${hardwareId}/screenshots?limit=${limit}`
  if (cursor) url += `&cursor=${cursor}`
  if (date) url += `&date=${date}`
  if (hour !== undefined) url += `&hour=${hour}`
  if (from) url += `&from=${from}`
  if (to) url += `&to=${to}`
  const res = await fetch(url)
  return res.json()
}

export async function fetchWorkLogs(hardwareId, date) {
  const url = date
    ? `${API_BASE}/api/machines/${hardwareId}/worklogs?date=${date}`
    : `${API_BASE}/api/machines/${hardwareId}/worklogs`
  const res = await fetch(url)
  return res.json()
}

export async function fetchWorkLogSummary(hardwareId, days, from, to) {
  let url = `${API_BASE}/api/machines/${hardwareId}/worklogs/summary`
  const params = []
  if (days) params.push(`days=${days}`)
  if (from) params.push(`from=${from}`)
  if (to) params.push(`to=${to}`)
  if (params.length) url += `?${params.join('&')}`
  const res = await fetch(url)
  return res.json()
}

export async function fetchHeatmap(hardwareId, days, from, to) {
  let url = `${API_BASE}/api/machines/${hardwareId}/worklogs/heatmap`
  const params = []
  if (days) params.push(`days=${days}`)
  if (from) params.push(`from=${from}`)
  if (to) params.push(`to=${to}`)
  if (params.length) url += `?${params.join('&')}`
  const res = await fetch(url)
  return res.json()
}

export function screenshotUrl(id) {
  return `${API_BASE}/api/screenshots/${id}/file`
}

export function thumbnailUrl(id) {
  return `${API_BASE}/api/screenshots/${id}/thumbnail`
}

export async function fetchSettings() {
  const res = await fetch(`${API_BASE}/api/settings`)
  return res.json()
}

export async function updateSettings(settings) {
  const res = await fetch(`${API_BASE}/api/settings`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(settings)
  })
  return res.json()
}
