import { useState, useEffect } from 'react'
import { Link } from 'react-router-dom'
import { ArrowLeft, Settings as SettingsIcon, Save, Check } from 'lucide-react'
import { fetchSettings, updateSettings } from '../lib/api'

export default function Settings() {
  const [interval, setInterval] = useState(90)
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [saved, setSaved] = useState(false)

  useEffect(() => {
    const load = async () => {
      try {
        const settings = await fetchSettings()
        if (settings.capture_interval_sec) {
          setInterval(parseInt(settings.capture_interval_sec, 10))
        }
      } catch (e) {
        console.error('Failed to fetch settings', e)
      } finally {
        setLoading(false)
      }
    }
    load()
  }, [])

  const handleSave = async () => {
    const val = parseInt(interval, 10)
    if (isNaN(val) || val < 30) {
      setError('Interval must be a number of at least 30 seconds')
      return
    }
    setError('')
    setSaving(true)
    setSaved(false)
    try {
      await updateSettings({ capture_interval_sec: val })
      setInterval(val)
      setSaved(true)
      setTimeout(() => setSaved(false), 2000)
    } catch (e) {
      console.error('Failed to save settings', e)
    } finally {
      setSaving(false)
    }
  }

  if (loading) return <div className="min-h-screen flex items-center justify-center text-slate-400">Loading...</div>

  return (
    <div className="min-h-screen bg-slate-50">
      <header className="bg-white border-b px-6 py-4">
        <div className="flex items-center gap-3">
          <Link to="/" className="flex items-center gap-2 text-slate-500 hover:text-slate-900">
            <ArrowLeft className="w-5 h-5" />
            <span>Back</span>
          </Link>
          <div className="h-6 w-px bg-slate-200" />
          <div className="flex items-center gap-2">
            <SettingsIcon className="w-5 h-5 text-slate-700" />
            <h1 className="text-xl font-semibold text-slate-900">Settings</h1>
          </div>
        </div>
      </header>

      <main className="max-w-2xl mx-auto p-6">
        <div className="bg-white rounded-lg border p-6">
          <h2 className="text-sm font-medium text-slate-700 mb-1">Capture Interval</h2>
          <p className="text-xs text-slate-400 mb-4">
            Set how often client apps capture screenshots. Changes take effect on each client's next heartbeat (within ~30 seconds).
          </p>

          <div className="flex items-center gap-4">
            <div className="flex-1">
              <label className="text-xs text-slate-500 mb-1 block">Interval (seconds)</label>
              <input
                type="number"
                value={interval}
                onChange={(e) => setInterval(e.target.value)}
                className="w-full border rounded-md px-3 py-2 text-sm text-slate-700 focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
            </div>
            <div className="text-sm text-slate-400 mt-5">
              = {Math.floor((parseInt(interval, 10) || 0) / 60)}m {(parseInt(interval, 10) || 0) % 60}s
            </div>
          </div>

          <div className="flex items-center gap-3 mt-6">
            <button
              onClick={handleSave}
              disabled={saving}
              className="flex items-center gap-2 px-4 py-2 rounded-lg bg-slate-900 text-white text-sm font-medium hover:bg-slate-800 disabled:opacity-50"
            >
              <Save className="w-4 h-4" />
              {saving ? 'Saving...' : 'Save'}
            </button>
            {saved && (
              <span className="flex items-center gap-1 text-sm text-green-600">
                <Check className="w-4 h-4" />
                Saved
              </span>
            )}
            {error && (
              <span className="text-sm text-red-600">{error}</span>
            )}
          </div>
        </div>
      </main>
    </div>
  )
}
