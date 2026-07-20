import { useState, useEffect } from 'react'
import { Link } from 'react-router-dom'
import { Monitor, Circle, Activity, Camera, Clock } from 'lucide-react'
import { fetchMachines } from '../lib/api'
import { formatDuration, formatDateTime, isOnline, cn } from '../lib/utils'

export default function Dashboard() {
  const [machines, setMachines] = useState([])
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    const load = async () => {
      try {
        const data = await fetchMachines()
        setMachines(data)
      } catch (e) {
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
        </div>
      </header>

      <main className="max-w-6xl mx-auto p-6">
        {loading ? (
          <div className="text-center py-12 text-slate-400">Loading...</div>
        ) : machines.length === 0 ? (
          <div className="text-center py-12 text-slate-400">No machines registered yet</div>
        ) : (
          <div className="grid gap-4">
            {machines.map((m) => {
              const online = isOnline(m.last_seen)
              return (
                <Link
                  key={m.hardware_id}
                  to={`/machine/${m.hardware_id}`}
                  className="bg-white rounded-lg border p-5 hover:shadow-md transition-shadow flex items-center gap-4"
                >
                  <div className={cn(
                    'w-3 h-3 rounded-full flex-shrink-0',
                    online ? 'bg-green-500' : 'bg-slate-300'
                  )} />

                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2">
                      <span className="font-medium text-slate-900 truncate">
                        {m.computer_name}
                      </span>
                      <span className={cn(
                        'text-xs px-2 py-0.5 rounded-full',
                        online ? 'bg-green-100 text-green-700' : 'bg-slate-100 text-slate-500'
                      )}>
                        {online ? 'Online' : 'Offline'}
                      </span>
                    </div>
                    <div className="text-xs text-slate-400 mt-1 font-mono truncate">
                      {m.hardware_id.substring(0, 16)}...
                    </div>
                  </div>

                  <div className="flex items-center gap-6 text-sm">
                    <div className="flex items-center gap-1.5 text-slate-600">
                      <Camera className="w-4 h-4 text-slate-400" />
                      <span>{m.screenshot_count || 0}</span>
                    </div>
                    <div className="flex items-center gap-1.5 text-slate-600">
                      <Clock className="w-4 h-4 text-slate-400" />
                      <span>{formatDuration(m.total_active_sec)}</span>
                    </div>
                    <div className="text-xs text-slate-400 w-32 text-right">
                      {formatDateTime(m.last_seen)}
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
