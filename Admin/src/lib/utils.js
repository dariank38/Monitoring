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

export function formatDateTime(iso) {
  if (!iso) return '--'
  let s = iso.includes('T') ? iso : iso.replace(' ', 'T')
  if (!s.endsWith('Z') && !s.includes('+') && !s.includes('-', 10)) s += 'Z'
  return new Date(s).toLocaleString()
}

export function isOnline(lastSeen) {
  if (!lastSeen) return false
  let iso = lastSeen.includes('T') ? lastSeen : lastSeen.replace(' ', 'T')
  if (!iso.endsWith('Z') && !iso.includes('+') && !iso.includes('-', 10)) iso += 'Z'
  const d = new Date(iso)
  return (Date.now() - d.getTime()) < 60000
}
