import { useState, useEffect, useRef } from 'react'
import { ChevronLeft, ChevronRight, Camera, X } from 'lucide-react'
import { screenshotUrl, thumbnailUrl } from '../lib/api'
import { formatDateTimeLaos, formatDateTimeClientTZ, cn } from '../lib/utils'

export default function ScreenshotSlider({ screenshots, onClose, onImageClick, timezone }) {
  const [index, setIndex] = useState(0)
  const containerRef = useRef(null)

  const sorted = [...screenshots].sort((a, b) =>
    new Date(a.captured_at) - new Date(b.captured_at)
  )

  const current = sorted[index]

  useEffect(() => {
    const handler = (e) => {
      if (e.key === 'ArrowLeft') setIndex(i => Math.max(0, i - 1))
      if (e.key === 'ArrowRight') setIndex(i => Math.min(sorted.length - 1, i + 1))
      if (e.key === 'Escape') onClose?.()
    }
    window.addEventListener('keydown', handler)
    return () => window.removeEventListener('keydown', handler)
  }, [sorted.length, onClose])

  useEffect(() => {
    const container = containerRef.current
    if (!container) return
    const thumb = container.children[index]
    if (thumb) {
      thumb.scrollIntoView({ behavior: 'smooth', inline: 'center', block: 'nearest' })
    }
  }, [index])

  if (sorted.length === 0) {
    return (
      <div className="bg-white rounded-lg border p-8 text-center text-slate-400">
        No screenshots for this period
      </div>
    )
  }

  return (
    <div className="bg-white rounded-lg border overflow-hidden">
      <div className="flex items-center justify-between px-4 py-3 border-b bg-slate-50">
        <div className="flex items-center gap-2">
          <Camera className="w-4 h-4 text-slate-500" />
          <span className="text-sm font-medium text-slate-700">
            Screenshot {index + 1} of {sorted.length}
          </span>
        </div>
        <div className="flex items-center gap-3">
          <span className="text-sm text-slate-600">🇱🇦 {formatDateTimeLaos(current.captured_at)}</span>
          <span className="text-xs text-slate-400">💻 {formatDateTimeClientTZ(current.captured_at, timezone)}</span>
          {onClose && (
            <button onClick={onClose} className="text-slate-400 hover:text-slate-700">
              <X className="w-4 h-4" />
            </button>
          )}
        </div>
      </div>

      <div className="relative bg-slate-900 flex items-center justify-center" style={{ minHeight: 400 }}>
        <button
          onClick={() => setIndex(i => Math.max(0, i - 1))}
          disabled={index === 0}
          className="absolute left-2 p-2 rounded-lg bg-white/10 hover:bg-white/20 text-white disabled:opacity-30 disabled:cursor-not-allowed z-10"
        >
          <ChevronLeft className="w-6 h-6" />
        </button>

        <img
          key={current.id}
          src={screenshotUrl(current.id)}
          alt="Screenshot"
          className="max-h-[500px] max-w-full object-contain cursor-zoom-in"
          onClick={() => onImageClick?.(index)}
        />

        <button
          onClick={() => setIndex(i => Math.min(sorted.length - 1, i + 1))}
          disabled={index === sorted.length - 1}
          className="absolute right-2 p-2 rounded-lg bg-white/10 hover:bg-white/20 text-white disabled:opacity-30 disabled:cursor-not-allowed z-10"
        >
          <ChevronRight className="w-6 h-6" />
        </button>
      </div>

      <div className="px-4 py-3 border-t bg-slate-50">
        <div className="flex items-center gap-2 mb-2">
          <span className="text-xs text-slate-400">Timeline</span>
          <span className="text-xs text-slate-400 ml-auto">
            {sorted.length > 1 ? 'Use arrow keys to navigate' : ''}
          </span>
        </div>
        <div
          ref={containerRef}
          className="flex gap-2 overflow-x-auto pb-2 scroll-smooth"
        >
          {sorted.map((s, i) => (
            <button
              key={s.id}
              onClick={() => setIndex(i)}
              className={cn(
                'flex-shrink-0 rounded overflow-hidden border-2 transition-all',
                i === index ? 'border-blue-500 opacity-100' : 'border-transparent opacity-50 hover:opacity-80'
              )}
            >
              <img
                src={thumbnailUrl(s.id)}
                alt="Screenshot"
                className="w-20 h-12 object-cover"
                loading="lazy"
                decoding="async"
              />
            </button>
          ))}
        </div>
      </div>
    </div>
  )
}
