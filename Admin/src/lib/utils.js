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

// Times stored in DB are Laos time (UTC+7), no timezone indicator.
// Parse them with +07:00 offset so toLocaleString with timeZone works correctly.
function parseLaosTime(iso) {
  if (!iso) return null
  let s = iso.includes('T') ? iso : iso.replace(' ', 'T')
  // Remove any trailing Z or timezone offset — stored times are Laos time
  s = s.replace(/[Zz].*$/, '').replace(/[+-]\d{2}:?\d{2}$/, '')
  // Tag as UTC+7 (Laos timezone)
  return new Date(s + '+07:00')
}

export function formatDateTime(iso) {
  const d = parseLaosTime(iso)
  if (!d) return '--'
  return d.toLocaleString('en-GB', { year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', second: '2-digit' })
}

const LAOS_TZ = 'Asia/Vientiane'

export function formatDateTimeLaos(iso) {
  const d = parseLaosTime(iso)
  if (!d) return '--'
  return d.toLocaleString('en-GB', { timeZone: LAOS_TZ, year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', second: '2-digit' })
}

export function formatDateTimeClientTZ(iso, timezone) {
  const d = parseLaosTime(iso)
  if (!d) return '--'
  try {
    return d.toLocaleString('en-GB', { timeZone: timezone || LAOS_TZ, year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', second: '2-digit' })
  } catch {
    return d.toLocaleString()
  }
}

export function isOnline(lastSeen) {
  if (!lastSeen) return false
  const d = parseLaosTime(lastSeen)
  if (!d) return false
  return (Date.now() - d.getTime()) < 60000
}
