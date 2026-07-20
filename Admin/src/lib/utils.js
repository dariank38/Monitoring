import { clsx } from 'clsx'
import { twMerge } from 'tailwind-merge'

export function cn(...inputs) {
  return twMerge(clsx(inputs))
}

export function formatDuration(seconds) {
  if (!seconds) return '0s'
  const h = Math.floor(seconds / 3600)
  const m = Math.floor((seconds % 3600) / 60)
  const s = seconds % 60
  const parts = []
  if (h > 0) parts.push(`${h}h`)
  if (m > 0) parts.push(`${m}m`)
  if (s > 0 || parts.length === 0) parts.push(`${s}s`)
  return parts.join(' ')
}

function parseISO(iso) {
  if (!iso) return null
  let s = iso.includes('T') ? iso : iso.replace(' ', 'T')
  if (!s.endsWith('Z') && !s.includes('+') && !s.includes('-', 10)) s += 'Z'
  return new Date(s)
}

export function formatDateTime(iso) {
  const d = parseISO(iso)
  if (!d) return '--'
  return d.toLocaleString()
}

const LAOS_TZ = 'Asia/Vientiane'

export function formatDateTimeLaos(iso) {
  const d = parseISO(iso)
  if (!d) return '--'
  return d.toLocaleString('en-GB', { timeZone: LAOS_TZ, year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', second: '2-digit' })
}

export function formatDateTimeClientTZ(iso, timezone) {
  const d = parseISO(iso)
  if (!d) return '--'
  try {
    return d.toLocaleString('en-GB', { timeZone: timezone || undefined, year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', second: '2-digit' })
  } catch {
    return d.toLocaleString()
  }
}

export function isOnline(lastSeen) {
  if (!lastSeen) return false
  const d = parseISO(lastSeen)
  return (Date.now() - d.getTime()) < 60000
}
