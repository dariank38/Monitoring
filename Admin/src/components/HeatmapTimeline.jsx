import { useState, useMemo, useRef } from 'react'
import { formatDuration, cn } from '../lib/utils'

const CELL_SIZE = 20
const CELL_GAP = 3
const LABEL_LEFT = 50
const LABEL_TOP = 22

function getHeatColor(ratio) {
  if (ratio <= 0) return '#282828'
  if (ratio < 0.25) return '#145020'
  if (ratio < 0.50) return '#1e7832'
  if (ratio < 0.75) return '#28a046'
  return '#32c85a'
}

export default function HeatmapTimeline({ data, onCellClick, selectedCell }) {
  const [hover, setHover] = useState(null)
  const containerRef = useRef(null)

  const { dates, cells, maxActive } = useMemo(() => {
    const cellMap = {}
    let max = 1

    for (const item of data) {
      const key = `${item.date}|${item.hour}`
      if (!cellMap[key]) {
        cellMap[key] = { ...item }
      } else {
        cellMap[key].active_sec += item.active_sec
        cellMap[key].key_count += item.key_count
        cellMap[key].mouse_count += item.mouse_count
      }
      if (cellMap[key].active_sec > max) max = cellMap[key].active_sec
    }

    const uniqueDates = [...new Set(data.map(d => d.date))].sort()
    return { dates: uniqueDates, cells: cellMap, maxActive: max }
  }, [data])

  if (dates.length === 0) {
    return <div className="text-center py-12 text-slate-400">No work logs yet</div>
  }

  return (
    <div className="bg-white rounded-lg border p-6 overflow-x-auto" ref={containerRef}>
      <div className="flex items-center gap-2 mb-4">
        <h3 className="text-sm font-medium text-slate-700">Activity Heatmap</h3>
        <span className="text-xs text-slate-400">({dates.length} day{dates.length !== 1 ? 's' : ''})</span>
      </div>

      <div className="relative" style={{ width: LABEL_LEFT + 24 * (CELL_SIZE + CELL_GAP) + 20 }}>
        {/* Hour labels */}
        <div className="flex" style={{ marginLeft: LABEL_LEFT }}>
          {Array.from({ length: 24 }, (_, h) => (
            <div
              key={h}
              className="text-[10px] text-slate-400 text-center"
              style={{ width: CELL_SIZE, marginRight: CELL_GAP }}
            >
              {h % 3 === 0 ? `${h.toString().padStart(2, '0')}` : ''}
            </div>
          ))}
        </div>

        {/* Grid rows */}
        {dates.map((date, di) => (
          <div key={date} className="flex items-center mt-1" style={{ height: CELL_SIZE }}>
            <div
              className="text-[10px] text-slate-400 flex items-center justify-end pr-2"
              style={{ width: LABEL_LEFT }}
            >
              {di % 2 === 0 || di === dates.length - 1
                ? new Date(date).toLocaleDateString('en-US', { month: '2-digit', day: '2-digit' })
                : ''}
            </div>
            {Array.from({ length: 24 }, (_, h) => {
              const key = `${date}|${h}`
              const cell = cells[key]
              const ratio = cell ? cell.active_sec / maxActive : 0
              const isHovered = hover?.key === key
              const isSelected = selectedCell === key

              return (
                <div
                  key={h}
                  className="rounded-[3px] cursor-pointer transition-all"
                  style={{
                    width: CELL_SIZE,
                    height: CELL_SIZE,
                    marginRight: CELL_GAP,
                    backgroundColor: getHeatColor(ratio),
                    outline: isHovered ? '2px solid white' : 'none',
                    outlineOffset: isHovered ? '-1px' : 0,
                    boxShadow: isHovered
                      ? '0 0 0 1px #3b82f6'
                      : isSelected
                        ? '0 0 0 2px #f59e0b'
                        : 'none',
                  }}
                  onMouseEnter={(e) => setHover({ key, cell, date, hour: h, x: e.clientX, y: e.clientY })}
                  onMouseMove={(e) => setHover(prev => prev ? { ...prev, x: e.clientX, y: e.clientY } : prev)}
                  onMouseLeave={() => setHover(null)}
                  onClick={() => onCellClick?.(date, h)}
                />
              )
            })}
          </div>
        ))}

        {/* Legend */}
        <div className="flex items-center gap-2 mt-4" style={{ marginLeft: LABEL_LEFT }}>
          <span className="text-[10px] text-slate-400">Less</span>
          {[0, 0.2, 0.4, 0.6, 0.8, 1].map((r, i) => (
            <div
              key={i}
              className="rounded-[2px]"
              style={{ width: 12, height: 12, backgroundColor: getHeatColor(r) }}
            />
          ))}
          <span className="text-[10px] text-slate-400">More</span>
        </div>
      </div>

      {/* Hover tooltip - fixed position so it's never clipped */}
      {hover && hover.cell && (
        <div
          className="fixed bg-slate-800 text-white text-xs rounded-lg px-3 py-2 pointer-events-none shadow-xl z-50"
          style={{
            left: Math.min(hover.x + 14, window.innerWidth - 180),
            top: Math.min(hover.y + 14, window.innerHeight - 80),
          }}
        >
          <div className="font-medium">{hover.date} {hover.hour.toString().padStart(2, '0')}:00</div>
          <div className="text-green-400 mt-0.5">Active: {formatDuration(hover.cell.active_sec)}</div>
        </div>
      )}
    </div>
  )
}
