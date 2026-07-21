import { useState, useEffect, useCallback } from 'react'
import { X, ZoomIn, ZoomOut, Maximize, ChevronLeft, ChevronRight } from 'lucide-react'
import { screenshotUrl } from '../lib/api'
import { formatDateTimeLaos, formatDateTimeClientTZ, cn } from '../lib/utils'

export default function ImageModal({ screenshots, index, onClose, onNavigate, timezone }) {
  const [zoom, setZoom] = useState(1)
  const [pan, setPan] = useState({ x: 0, y: 0 })
  const [dragging, setDragging] = useState(false)
  const [dragStart, setDragStart] = useState({ x: 0, y: 0 })

  const current = screenshots[index]

  const resetView = useCallback(() => {
    setZoom(1)
    setPan({ x: 0, y: 0 })
  }, [])

  useEffect(() => {
    const handler = (e) => {
      if (e.key === 'Escape') onClose()
      if (e.key === 'ArrowLeft') onNavigate(Math.max(0, index - 1))
      if (e.key === 'ArrowRight') onNavigate(Math.min(screenshots.length - 1, index + 1))
      if (e.key === '+' || e.key === '=') setZoom(z => Math.min(5, z + 0.5))
      if (e.key === '-') setZoom(z => Math.max(0.5, z - 0.5))
      if (e.key === '0') resetView()
    }
    window.addEventListener('keydown', handler)
    return () => window.removeEventListener('keydown', handler)
  }, [index, screenshots.length, onClose, onNavigate, resetView])

  useEffect(() => { resetView() }, [index, resetView])

  useEffect(() => {
    const handler = (e) => {
      e.preventDefault()
      const delta = e.deltaY > 0 ? -0.2 : 0.2
      setZoom(z => Math.max(0.5, Math.min(5, z + delta)))
    }
    const el = document.getElementById('image-modal-overlay')
    if (el) el.addEventListener('wheel', handler, { passive: false })
    return () => { if (el) el.removeEventListener('wheel', handler) }
  }, [])

  if (!current) return null

  const handleMouseDown = (e) => {
    if (zoom <= 1) return
    setDragging(true)
    setDragStart({ x: e.clientX - pan.x, y: e.clientY - pan.y })
  }

  const handleMouseMove = (e) => {
    if (!dragging) return
    setPan({ x: e.clientX - dragStart.x, y: e.clientY - dragStart.y })
  }

  const handleMouseUp = () => setDragging(false)

  return (
    <div
      id="image-modal-overlay"
      className="fixed inset-0 z-50 bg-black/90 flex flex-col"
    >
      {/* Header */}
      <div className="flex items-center justify-between px-4 py-3 text-white">
        <div className="flex items-center gap-3">
          <span className="text-sm font-medium">
            {index + 1} / {screenshots.length}
          </span>
          <span className="text-sm text-white/60">🇱� {formatDateTimeLaos(current.captured_at)}</span>
          <span className="text-xs text-white/40">💻 {formatDateTimeClientTZ(current.captured_at, timezone)}</span>
        </div>
        <div className="flex items-center gap-1">
          <button
            onClick={() => setZoom(z => Math.max(0.5, z - 0.5))}
            className="p-2 rounded-lg hover:bg-white/10 text-white/80"
            title="Zoom out (-)"
          >
            <ZoomOut className="w-5 h-5" />
          </button>
          <span className="text-xs text-white/60 w-12 text-center">{Math.round(zoom * 100)}%</span>
          <button
            onClick={() => setZoom(z => Math.min(5, z + 0.5))}
            className="p-2 rounded-lg hover:bg-white/10 text-white/80"
            title="Zoom in (+)"
          >
            <ZoomIn className="w-5 h-5" />
          </button>
          <button
            onClick={resetView}
            className="p-2 rounded-lg hover:bg-white/10 text-white/80"
            title="Reset (0)"
          >
            <Maximize className="w-5 h-5" />
          </button>
          <div className="w-px h-6 bg-white/20 mx-1" />
          <button
            onClick={onClose}
            className="p-2 rounded-lg hover:bg-white/10 text-white/80"
            title="Close (Esc)"
          >
            <X className="w-5 h-5" />
          </button>
        </div>
      </div>

      {/* Image area */}
      <div
        className="flex-1 flex items-center justify-center overflow-hidden relative"
        onMouseDown={handleMouseDown}
        onMouseMove={handleMouseMove}
        onMouseUp={handleMouseUp}
        onMouseLeave={handleMouseUp}
        style={{ cursor: zoom > 1 ? (dragging ? 'grabbing' : 'grab') : 'default' }}
      >
        {index > 0 && (
          <button
            onClick={(e) => { e.stopPropagation(); onNavigate(index - 1) }}
            className="absolute left-4 p-2 rounded-lg bg-white/10 hover:bg-white/20 text-white z-10"
          >
            <ChevronLeft className="w-6 h-6" />
          </button>
        )}

        <img
          src={screenshotUrl(current.id)}
          alt="Screenshot"
          className="max-h-full max-w-full object-contain select-none transition-transform"
          style={{
            transform: `scale(${zoom}) translate(${pan.x / zoom}px, ${pan.y / zoom}px)`,
            transition: dragging ? 'none' : 'transform 0.15s ease-out',
          }}
          draggable="false"
        />

        {index < screenshots.length - 1 && (
          <button
            onClick={(e) => { e.stopPropagation(); onNavigate(index + 1) }}
            className="absolute right-4 p-2 rounded-lg bg-white/10 hover:bg-white/20 text-white z-10"
          >
            <ChevronRight className="w-6 h-6" />
          </button>
        )}
      </div>

      {/* Footer hint */}
      <div className="px-4 py-2 text-center text-xs text-white/40">
        Scroll to zoom · Drag to pan · Arrow keys to navigate · Esc to close
      </div>
    </div>
  )
}
