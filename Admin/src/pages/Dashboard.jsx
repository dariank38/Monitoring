import { useState, useEffect } from 'react'
import { Link } from 'react-router-dom'
import { Monitor, Circle, Camera, Clock, Settings as SettingsIcon } from 'lucide-react'
import { fetchMachines, thumbnailUrl, clearPassword } from '../lib/api'
import { formatDuration, formatDateTimeLaos, formatDateTimeClientTZ, isOnline, cn } from '../lib/utils'

export default function Dashboard() {
  const [machines, setMachines] = useState([])
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    const load = async () => {
      try {
        const data = await fetchMachines()
        setMachines(data)
      } catch (e) {
        if (e.message === 'Unauthorized') { clearPassword(); window.location.reload(); return }
        console.error('Failed to fetch machines', e)
      } finally {
        setLoading(false)
      }
    }
    load()
    const interval = setInterval(load, 5000)
    return () => clearInterval(interval)
  }, [])

  return (
    <div className="min-h-screen bg-slate-50">
      <header className="bg-white border-b px-6 py-4">
        <div className="flex items-center gap-3">
          <Monitor className="w-6 h-6 text-slate-700" />
          <h1 className="text-xl font-semibold text-slate-900">Monitoring Admin</h1>
          <span className="ml-auto text-sm text-slate-500">
            {machines.length} machine{machines.length !== 1 ? 's' : ''}
          </span>
          <Link to="/settings" className="text-slate-400 hover:text-slate-700 ml-2">
            <SettingsIcon className="w-5 h-5" />
          </Link>
          <button
            onClick={() => { clearPassword(); window.location.reload() }}
            className="text-slate-400 hover:text-red-600 ml-2 text-sm"
            title="Logout"
          >
            Logout
          </button>
        </div>
      </header>

      <main className="max-w-7xl mx-auto p-6">
        {loading ? (
          <div className="text-center py-12 text-slate-400">Loading...</div>
        ) : machines.length === 0 ? (
          <div className="text-center py-12 text-slate-400">No machines registered yet</div>
        ) : (
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
            {machines.map((m) => {
              const online = isOnline(m.last_seen)
              return (
                <Link
                  key={m.hardware_id}
                  to={`/machine/${m.hardware_id}`}
                  className="bg-white rounded-lg border overflow-hidden hover:shadow-md transition-shadow group"
                >
                  <div className="relative aspect-video bg-slate-900 overflow-hidden">
                    {m.latest_screenshot_id ? (
                      <img
                        src={thumbnailUrl(m.latest_screenshot_id)}
                        alt={m.computer_name}
                        className="w-full h-full object-cover group-hover:opacity-80 transition-opacity"
                        loading="lazy"
                        decoding="async"
                      />
                    ) : (
                      <div className="w-full h-full flex items-center justify-center">
                        <Camera className="w-8 h-8 text-slate-600" />
                      </div>
                    )}
                    <div className="absolute top-2 right-2">
                      <span className={cn(
                        'inline-flex items-center gap-1 text-xs px-2 py-0.5 rounded-full',
                        online ? 'bg-green-500/90 text-white' : 'bg-slate-500/90 text-white'
                      )}>
                        <Circle className="w-2 h-2 fill-current" />
                        {online ? 'Online' : 'Offline'}
                      </span>
                    </div>
                  </div>

                  <div className="p-3">
                    <div className="font-medium text-slate-900 truncate">
                      {m.computer_name}
                    </div>
                    <div className="flex items-center gap-4 mt-2 text-xs text-slate-500">
                      <div className="flex items-center gap-1">
                        <Camera className="w-3.5 h-3.5 text-slate-400" />
                        <span>{m.screenshot_count || 0}</span>
                      </div>
                      <div className="flex items-center gap-1">
                        <Clock className="w-3.5 h-3.5 text-slate-400" />
                        <span>{formatDuration(m.total_active_sec)}</span>
                      </div>
                      <span className="ml-auto text-slate-400 text-right">
                        <div className="text-xs">🇱🇦 {formatDateTimeLaos(m.last_seen)}</div>
                        <div className="text-[10px] text-slate-400">💻 {formatDateTimeClientTZ(m.last_seen, m.timezone)}</div>
                      </span>
                    </div>
                  </div>
                </Link>
              )
            })}
          </div>
        )}
      </main>
    </div>
  )
}
