import { useState, useEffect, useCallback, useRef, useMemo } from 'react'
import { useParams, Link } from 'react-router-dom'
import { ArrowLeft, Grid, Film, Loader2, Calendar, X, Trash2 } from 'lucide-react'
import { fetchMachine, fetchScreenshots, fetchHeatmap, fetchWorkLogSummary, screenshotUrl, thumbnailUrl, clearPassword, deleteScreenshot, deleteScreenshotsBulk } from '../lib/api'
import { formatDuration, formatDateTime, formatDateTimeLaos, formatDateTimeClientTZ, isOnline, cn } from '../lib/utils'
import HeatmapTimeline from '../components/HeatmapTimeline.jsx'
import ScreenshotSlider from '../components/ScreenshotSlider.jsx'
import ImageModal from '../components/ImageModal.jsx'

const PAGE_SIZE = 24

function todayISO() {
  return new Date().toISOString().slice(0, 10)
}

function daysAgoISO(n) {
  const d = new Date()
  d.setDate(d.getDate() - n)
  return d.toISOString().slice(0, 10)
}

export default function MachineDetail() {
  const { hardwareId } = useParams()
  const [machine, setMachine] = useState(null)
  const [screenshots, setScreenshots] = useState([])
  const [heatmap, setHeatmap] = useState([])
  const [summary, setSummary] = useState([])
  const [tab, setTab] = useState('screenshots')
  const [loading, setLoading] = useState(true)

  const [viewMode, setViewMode] = useState('grid')
  const [filterDate, setFilterDate] = useState(null)
  const [filterHour, setFilterHour] = useState(null)
  const [selectedCell, setSelectedCell] = useState(null)
  const [modalIndex, setModalIndex] = useState(null)

  const [cursor, setCursor] = useState(null)
  const [hasMore, setHasMore] = useState(false)
  const [loadingMore, setLoadingMore] = useState(false)
  const [deleting, setDeleting] = useState(false)
  const sentinelRef = useRef(null)

  // Shared date range
  const [dateFrom, setDateFrom] = useState(daysAgoISO(7))
  const [dateTo, setDateTo] = useState(todayISO())
  const [appliedFrom, setAppliedFrom] = useState(daysAgoISO(7))
  const [appliedTo, setAppliedTo] = useState(todayISO())

  const rangeParams = useMemo(() => {
    const params = {}
    if (appliedFrom) params.from = appliedFrom
    if (appliedTo) params.to = appliedTo + 'T23:59:59'
    return params
  }, [appliedFrom, appliedTo])

  const totalActiveSec = useMemo(() => {
    return summary.reduce((sum, r) => sum + (r.active_sec || 0), 0)
  }, [summary])

  const loadScreenshots = useCallback(async (date, hour) => {
    const data = await fetchScreenshots(hardwareId, PAGE_SIZE, undefined, date, hour, rangeParams.from, rangeParams.to)
    setScreenshots(data.items)
    setCursor(data.nextCursor)
    setHasMore(data.hasMore)
  }, [hardwareId, rangeParams])

  const loadMore = useCallback(async () => {
    if (!hasMore || loadingMore) return
    setLoadingMore(true)
    try {
      const data = await fetchScreenshots(hardwareId, PAGE_SIZE, cursor, filterDate, filterHour, rangeParams.from, rangeParams.to)
      setScreenshots(prev => [...prev, ...data.items])
      setCursor(data.nextCursor)
      setHasMore(data.hasMore)
    } finally {
      setLoadingMore(false)
    }
  }, [hasMore, loadingMore, cursor, hardwareId, filterDate, filterHour, rangeParams])

  useEffect(() => {
    const load = async () => {
      try {
        const [m, ss, hm, sm] = await Promise.all([
          fetchMachine(hardwareId),
          fetchScreenshots(hardwareId, PAGE_SIZE, undefined, undefined, undefined, rangeParams.from, rangeParams.to),
          fetchHeatmap(hardwareId, undefined, rangeParams.from, rangeParams.to?.replace('T23:59:59', '')),
          fetchWorkLogSummary(hardwareId, undefined, rangeParams.from, rangeParams.to?.replace('T23:59:59', '')),
        ])
        setMachine(m)
        setScreenshots(ss.items)
        setCursor(ss.nextCursor)
        setHasMore(ss.hasMore)
        setHeatmap(hm)
        setSummary(sm)
      } catch (e) {
        if (e.message === 'Unauthorized') { clearPassword(); window.location.reload(); return }
        console.error('Failed to fetch machine detail', e)
      } finally {
        setLoading(false)
      }
    }
    load()
    const interval = setInterval(load, 10000)
    return () => clearInterval(interval)
  }, [hardwareId, rangeParams])

  // Infinite scroll via IntersectionObserver
  useEffect(() => {
    if (tab !== 'screenshots' || viewMode !== 'grid') return
    const sentinel = sentinelRef.current
    if (!sentinel) return
    const observer = new IntersectionObserver(
      (entries) => { if (entries[0].isIntersecting) loadMore() },
      { rootMargin: '200px' }
    )
    observer.observe(sentinel)
    return () => observer.disconnect()
  }, [tab, viewMode, loadMore])

  const handleHeatmapClick = async (date, hour) => {
    const cellKey = `${date}|${hour}`
    if (selectedCell === cellKey) {
      setSelectedCell(null)
      setFilterDate(null)
      setFilterHour(null)
      setTab('screenshots')
      await loadScreenshots()
      return
    }

    setSelectedCell(cellKey)
    setFilterDate(date)
    setFilterHour(hour)
    setTab('screenshots')
    await loadScreenshots(date, hour)
  }

  const clearFilter = async () => {
    setSelectedCell(null)
    setFilterDate(null)
    setFilterHour(null)
    await loadScreenshots()
  }

  const handleDeleteOne = async (e, screenshotId) => {
    e.stopPropagation()
    if (!confirm('Delete this screenshot?')) return
    setDeleting(true)
    try {
      await deleteScreenshot(screenshotId)
      setScreenshots(prev => prev.filter(s => s.id !== screenshotId))
    } catch (e) {
      if (e.message === 'Unauthorized') { clearPassword(); window.location.reload(); return }
      console.error('Failed to delete screenshot', e)
    } finally {
      setDeleting(false)
    }
  }

  const handleDeleteBulk = async () => {
    const label = filterDate
      ? `${filterDate}${filterHour !== null ? ` ${String(filterHour).padStart(2, '0')}:00` : ''}`
      : `${appliedFrom} to ${appliedTo}`
    if (!confirm(`Delete ALL screenshots${filterDate ? ' from' : ' in range'} ${label}? This cannot be undone.`)) return
    setDeleting(true)
    try {
      const result = await deleteScreenshotsBulk(hardwareId, filterDate
        ? { date: filterDate, hour: filterHour }
        : { from: rangeParams.from, to: rangeParams.to }
      )
      setScreenshots([])
      setCursor(null)
      setHasMore(false)
      alert(`Deleted ${result.deleted} screenshot(s)`)
    } catch (e) {
      if (e.message === 'Unauthorized') { clearPassword(); window.location.reload(); return }
      console.error('Failed to bulk delete screenshots', e)
    } finally {
      setDeleting(false)
    }
  }

  const applyDateRange = () => {
    setAppliedFrom(dateFrom)
    setAppliedTo(dateTo)
  }

  const clearDateRange = () => {
    const from = daysAgoISO(7)
    const to = todayISO()
    setDateFrom(from)
    setDateTo(to)
    setAppliedFrom(from)
    setAppliedTo(to)
  }

  if (loading) return <div className="min-h-screen flex items-center justify-center text-slate-400">Loading...</div>
  if (!machine) return <div className="min-h-screen flex items-center justify-center text-slate-400">Machine not found</div>

  return (
    <div className="min-h-screen bg-slate-50">
      <header className="bg-white border-b px-6 py-4">
        <div className="flex items-center gap-3">
          <Link to="/" className="flex items-center gap-2 text-slate-500 hover:text-slate-900">
            <ArrowLeft className="w-5 h-5" />
            <span>Back</span>
          </Link>
          <div className="h-6 w-px bg-slate-200" />
          <h1 className="text-xl font-semibold text-slate-900">{machine.computer_name}</h1>
          <span className={cn(
            'text-xs px-2 py-0.5 rounded-full',
            isOnline(machine.last_seen) ? 'bg-green-100 text-green-700' : 'bg-slate-100 text-slate-500'
          )}>
            {isOnline(machine.last_seen) ? 'Online' : 'Offline'}
          </span>
        </div>
      </header>

      <main className="max-w-6xl mx-auto p-6">
        {/* Date range picker - shared across tabs */}
        <div className="flex items-center gap-2 mb-4 bg-white rounded-lg border p-3">
          <Calendar className="w-4 h-4 text-slate-400" />
          <span className="text-xs font-medium text-slate-500">Date Range:</span>
          <input
            type="date"
            value={dateFrom}
            onChange={(e) => setDateFrom(e.target.value)}
            className="text-sm border rounded-md px-2 py-1 text-slate-700 focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
          <span className="text-slate-400 text-sm">to</span>
          <input
            type="date"
            value={dateTo}
            onChange={(e) => setDateTo(e.target.value)}
            className="text-sm border rounded-md px-2 py-1 text-slate-700 focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
          <button
            onClick={applyDateRange}
            className="px-3 py-1 rounded-lg bg-slate-900 text-white text-xs font-medium hover:bg-slate-800"
          >
            Apply
          </button>
          <button
            onClick={clearDateRange}
            className="px-3 py-1 rounded-lg text-slate-500 text-xs font-medium hover:bg-slate-50"
          >
            Reset
          </button>
        </div>

        <div className="flex items-center gap-2 mb-6">
          <button
            onClick={() => setTab('screenshots')}
            className={cn(
              'px-4 py-2 rounded-lg text-sm font-medium transition-colors',
              tab === 'screenshots' ? 'bg-slate-900 text-white' : 'bg-white text-slate-600 border hover:bg-slate-50'
            )}
          >
            Screenshots
          </button>
          <button
            onClick={() => setTab('worklogs')}
            className={cn(
              'px-4 py-2 rounded-lg text-sm font-medium transition-colors',
              tab === 'worklogs' ? 'bg-slate-900 text-white' : 'bg-white text-slate-600 border hover:bg-slate-50'
            )}
          >
            Work Time
          </button>

          {tab === 'screenshots' && (
            <div className="ml-auto flex items-center gap-2">
              {filterDate && (
                <button
                  onClick={clearFilter}
                  className="text-xs px-3 py-1.5 rounded-lg bg-amber-50 text-amber-700 border border-amber-200 hover:bg-amber-100"
                >
                  {filterDate} {filterHour !== null && `${String(filterHour).padStart(2, '0')}:00`} ✕
                </button>
              )}
              {screenshots.length > 0 && (
                <button
                  onClick={handleDeleteBulk}
                  disabled={deleting}
                  className="flex items-center gap-1 text-xs px-3 py-1.5 rounded-lg bg-red-50 text-red-600 border border-red-200 hover:bg-red-100 disabled:opacity-50"
                >
                  <Trash2 className="w-3.5 h-3.5" />
                  {filterDate ? 'Delete filtered' : 'Delete range'}
                </button>
              )}
              <div className="flex bg-white border rounded-lg overflow-hidden">
                <button
                  onClick={() => setViewMode('grid')}
                  className={cn(
                    'px-3 py-1.5 flex items-center gap-1.5 text-xs font-medium',
                    viewMode === 'grid' ? 'bg-slate-900 text-white' : 'text-slate-600 hover:bg-slate-50'
                  )}
                >
                  <Grid className="w-3.5 h-3.5" /> Grid
                </button>
                <button
                  onClick={() => setViewMode('slider')}
                  className={cn(
                    'px-3 py-1.5 flex items-center gap-1.5 text-xs font-medium',
                    viewMode === 'slider' ? 'bg-slate-900 text-white' : 'text-slate-600 hover:bg-slate-50'
                  )}
                >
                  <Film className="w-3.5 h-3.5" /> Timeline
                </button>
              </div>
            </div>
          )}
        </div>

        {tab === 'screenshots' && (
          <div>
            {screenshots.length === 0 ? (
              <div className="text-center py-12 text-slate-400">
                {filterDate ? 'No screenshots for this period' : 'No screenshots in this date range'}
              </div>
            ) : viewMode === 'grid' ? (
              <>
                <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-4">
                  {screenshots.map((s, i) => (
                    <div
                      key={s.id}
                      className="bg-white rounded-lg border overflow-hidden group cursor-pointer relative"
                      onClick={() => setModalIndex(i)}
                    >
                      <button
                        onClick={(e) => handleDeleteOne(e, s.id)}
                        disabled={deleting}
                        className="absolute top-1 right-1 z-10 w-7 h-7 rounded-full bg-black/50 text-white flex items-center justify-center opacity-0 group-hover:opacity-100 transition-opacity hover:bg-red-600 disabled:opacity-50"
                        title="Delete screenshot"
                      >
                        <Trash2 className="w-3.5 h-3.5" />
                      </button>
                      <img
                        src={thumbnailUrl(s.id)}
                        alt={s.filename}
                        className="w-full h-32 object-cover group-hover:opacity-80 transition-opacity"
                        loading="lazy"
                        decoding="async"
                      />
                      <div className="p-2">
                        <div className="text-xs text-slate-500 truncate">{s.filename}</div>
                        <div className="text-xs text-slate-600 mt-0.5">🇱🇦 {formatDateTimeLaos(s.captured_at)}</div>
                        <div className="text-[10px] text-slate-400">💻 {formatDateTimeClientTZ(s.captured_at, machine.timezone)}</div>
                      </div>
                    </div>
                  ))}
                </div>
                <div ref={sentinelRef} className="h-10 flex items-center justify-center mt-4">
                  {loadingMore && (
                    <Loader2 className="w-5 h-5 text-slate-400 animate-spin" />
                  )}
                </div>
              </>
            ) : (
              <ScreenshotSlider
                screenshots={screenshots}
                onClose={filterDate ? clearFilter : undefined}
                onImageClick={setModalIndex}
                timezone={machine.timezone}
              />
            )}
          </div>
        )}

        {tab === 'worklogs' && (
          <div className="space-y-6">
            {/* Total work time summary */}
            <div className="bg-white rounded-lg border p-5">
              <h3 className="text-sm font-medium text-slate-700 mb-4">
                Total Work Time ({appliedFrom} to {appliedTo})
              </h3>
              <div className="grid grid-cols-1 gap-4">
                <div className="rounded-lg bg-green-50 border border-green-100 p-4">
                  <div className="text-xs text-green-600 mb-1">Total Work Time</div>
                  <div className="text-2xl font-bold text-green-700">{formatDuration(totalActiveSec)}</div>
                </div>
              </div>
            </div>

            {/* Daily breakdown */}
            {summary.length > 0 && (
              <div className="bg-white rounded-lg border p-5">
                <h3 className="text-sm font-medium text-slate-700 mb-4">Daily Breakdown</h3>
                <div className="grid grid-cols-2 md:grid-cols-4 lg:grid-cols-7 gap-3">
                  {summary.map((row) => (
                    <div key={row.log_date} className="rounded-lg bg-slate-50 border p-3">
                      <div className="text-xs text-slate-400 mb-1">
                        {new Date(row.log_date).toLocaleDateString('en-US', { weekday: 'short', month: '2-digit', day: '2-digit' })}
                      </div>
                      <div className="text-lg font-semibold text-green-600">
                        {formatDuration(row.active_sec)}
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            )}
            <HeatmapTimeline
              data={heatmap}
              onCellClick={handleHeatmapClick}
              selectedCell={selectedCell}
            />
            <p className="text-xs text-slate-400 text-center">
              Click a cell to view screenshots from that hour
            </p>
          </div>
        )}
      </main>

      {modalIndex !== null && (
        <ImageModal
          screenshots={screenshots}
          index={modalIndex}
          onClose={() => setModalIndex(null)}
          onNavigate={setModalIndex}
          timezone={machine.timezone}
        />
      )}
    </div>
  )
}
