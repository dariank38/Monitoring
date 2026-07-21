import { useState, useEffect, useRef, useCallback } from 'react'
import { Link } from 'react-router-dom'
import { Monitor, Grid, Activity, ArrowLeft } from 'lucide-react'
import { fetchMachines, thumbnailUrl, getPassword, clearPassword } from '../lib/api'
import { formatDateTimeLaos, isOnline, cn } from '../lib/utils'

export default function LiveView() {
  const [machines, setMachines] = useState([])
  const [liveScreens, setLiveScreens] = useState({})
  const [connected, setConnected] = useState(false)
  const wsRef = useRef(null)
  const reconnectTimer = useRef(null)

  const loadMachines = useCallback(async () => {
    try {
      const data = await fetchMachines()
      setMachines(data)
    } catch (e) {
      if (e.message === 'Unauthorized') { clearPassword(); window.location.reload(); return }
      console.error('Failed to fetch machines', e)
    }
  }, [])

  useEffect(() => {
    loadMachines()
    const interval = setInterval(loadMachines, 10000)
    return () => clearInterval(interval)
  }, [loadMachines])

  useEffect(() => {
    const connect = () => {
      const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:'
      const wsUrl = `${protocol}//${window.location.host}/ws?token=${encodeURIComponent(getPassword())}`
      const ws = new WebSocket(wsUrl)
      wsRef.current = ws

      ws.onopen = () => {
        setConnected(true)
        console.log('[ws] Connected')
      }

      ws.onmessage = (event) => {
        try {
          const msg = JSON.parse(event.data)
          if (msg.type === 'screenshot') {
            setLiveScreens(prev => ({
              ...prev,
              [msg.hardware_id]: {
                screenshot_id: msg.screenshot_id,
                computer_name: msg.computer_name,
                captured_at: msg.captured_at,
                timestamp: Date.now(),
              }
            }))
          }
        } catch (e) {
          console.error('[ws] Parse error', e)
        }
      }

      ws.onclose = () => {
        setConnected(false)
        console.log('[ws] Disconnected, reconnecting in 3s...')
        reconnectTimer.current = setTimeout(connect, 3000)
      }

      ws.onerror = () => {
        ws.close()
      }
    }

    connect()

    return () => {
      if (reconnectTimer.current) clearTimeout(reconnectTimer.current)
      if (wsRef.current) {
        wsRef.current.onclose = null
        wsRef.current.close()
      }
    }
  }, [])

  // Merge machine data with live screenshots
  const displayMachines = machines.map(m => {
    const live = liveScreens[m.hardware_id]
    if (live && live.screenshot_id) {
      return {
        ...m,
        _liveScreenshotId: live.screenshot_id,
        _liveCapturedAt: live.captured_at,
        _liveTimestamp: live.timestamp,
      }
    }
    return m
  })

  const onlineMachines = displayMachines.filter(m => isOnline(m.last_seen))
  const offlineMachines = displayMachines.filter(m => !isOnline(m.last_seen))

  return (
    <div className="min-h-screen bg-slate-50">
      <header className="bg-white border-b px-6 py-4">
        <div className="flex items-center gap-3">
          <Link to="/" className="text-slate-400 hover:text-slate-700">
            <ArrowLeft className="w-5 h-5" />
          </Link>
          <Monitor className="w-6 h-6 text-slate-700" />
          <h1 className="text-xl font-semibold text-slate-900">Live View</h1>
          <span className={cn(
            'ml-auto inline-flex items-center gap-1.5 text-xs px-2.5 py-1 rounded-full',
            connected ? 'bg-green-50 text-green-700 border border-green-200' : 'bg-red-50 text-red-700 border border-red-200'
          )}>
            <Activity className="w-3 h-3" />
            {connected ? 'Connected' : 'Disconnected'}
          </span>
          <span className="text-sm text-slate-500">
            {onlineMachines.length} online / {displayMachines.length} total
          </span>
        </div>
      </header>

      <main className="max-w-7xl mx-auto p-6">
        {displayMachines.length === 0 ? (
          <div className="text-center py-12 text-slate-400">No machines registered</div>
        ) : (
          <>
            <div className="mb-4 flex items-center gap-2 text-sm text-slate-500">
              <Grid className="w-4 h-4" />
              <span>Real-time screenshots — updates automatically as captures arrive</span>
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
              {onlineMachines.map((m) => {
                const online = isOnline(m.last_seen)
                const thumbId = m._liveScreenshotId || m.latest_screenshot_id
                const isFresh = m._liveTimestamp && (Date.now() - m._liveTimestamp < 60000)
                return (
                  <Link
                    key={m.hardware_id}
                    to={`/machine/${m.hardware_id}`}
                    className="bg-white rounded-lg border overflow-hidden hover:shadow-md transition-shadow group"
                  >
                    <div className="relative aspect-video bg-slate-900 overflow-hidden">
                      {thumbId ? (
                        <img
                          src={thumbnailUrl(thumbId) + (m._liveScreenshotId ? `&_t=${m._liveTimestamp}` : '')}
                          alt={m.computer_name}
                          className="w-full h-full object-cover group-hover:opacity-80 transition-opacity"
                          loading="lazy"
                          decoding="async"
                        />
                      ) : (
                        <div className="w-full h-full flex items-center justify-center">
                          <Monitor className="w-8 h-8 text-slate-600" />
                        </div>
                      )}
                      <div className="absolute top-2 right-2 flex items-center gap-1.5">
                        {isFresh && (
                          <span className="inline-flex items-center gap-1 text-xs px-2 py-0.5 rounded-full bg-blue-500/90 text-white">
                            <Activity className="w-2.5 h-2.5 animate-pulse" />
                            LIVE
                          </span>
                        )}
                        <span className={cn(
                          'inline-flex items-center gap-1 text-xs px-2 py-0.5 rounded-full',
                          online ? 'bg-green-500/90 text-white' : 'bg-slate-500/90 text-white'
                        )}>
                          {online ? 'Online' : 'Offline'}
                        </span>
                      </div>
                    </div>
                    <div className="p-3">
                      <div className="font-medium text-slate-900 truncate">
                        {m.computer_name}
                      </div>
                      <div className="text-xs text-slate-400 mt-1">
                        {m._liveCapturedAt ? `Last capture: ${formatDateTimeLaos(m._liveCapturedAt)}` : `Last seen: ${formatDateTimeLaos(m.last_seen)}`}
                      </div>
                    </div>
                  </Link>
                )
              })}
            </div>

            {offlineMachines.length > 0 && (
              <details className="mt-6">
                <summary className="cursor-pointer text-sm text-slate-500 hover:text-slate-700">
                  {offlineMachines.length} offline machine{offlineMachines.length !== 1 ? 's' : ''}
                </summary>
                <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4 mt-3">
                  {offlineMachines.map((m) => (
                    <Link
                      key={m.hardware_id}
                      to={`/machine/${m.hardware_id}`}
                      className="bg-white rounded-lg border overflow-hidden hover:shadow-md transition-shadow group opacity-60"
                    >
                      <div className="relative aspect-video bg-slate-800 overflow-hidden">
                        {m.latest_screenshot_id ? (
                          <img
                            src={thumbnailUrl(m.latest_screenshot_id)}
                            alt={m.computer_name}
                            className="w-full h-full object-cover grayscale"
                            loading="lazy"
                          />
                        ) : (
                          <div className="w-full h-full flex items-center justify-center">
                            <Monitor className="w-8 h-8 text-slate-600" />
                          </div>
                        )}
                        <div className="absolute top-2 right-2">
                          <span className="inline-flex items-center gap-1 text-xs px-2 py-0.5 rounded-full bg-slate-500/90 text-white">
                            Offline
                          </span>
                        </div>
                      </div>
                      <div className="p-3">
                        <div className="font-medium text-slate-900 truncate">{m.computer_name}</div>
                        <div className="text-xs text-slate-400 mt-1">Last seen: {formatDateTimeLaos(m.last_seen)}</div>
                      </div>
                    </Link>
                  ))}
                </div>
              </details>
            )}
          </>
        )}
      </main>
    </div>
  )
}
