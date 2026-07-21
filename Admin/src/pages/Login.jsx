import { useState } from 'react'
import { Lock, Loader2 } from 'lucide-react'
import { verifyPassword, setPassword } from '../lib/api'

export default function Login({ onSuccess }) {
  const [password, setPasswordInput] = useState('')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')

  const handleSubmit = async (e) => {
    e.preventDefault()
    setLoading(true)
    setError('')
    try {
      const ok = await verifyPassword(password)
      if (ok) {
        setPassword(password)
        onSuccess()
      } else {
        setError('Invalid password')
      }
    } catch (err) {
      setError('Connection failed')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="min-h-screen bg-slate-50 flex items-center justify-center">
      <div className="bg-white rounded-lg border p-8 w-full max-w-sm">
        <div className="flex items-center gap-3 mb-6">
          <div className="w-10 h-10 rounded-lg bg-slate-900 flex items-center justify-center">
            <Lock className="w-5 h-5 text-white" />
          </div>
          <div>
            <h1 className="text-lg font-semibold text-slate-900">Admin Login</h1>
            <p className="text-xs text-slate-400">Enter your password</p>
          </div>
        </div>

        <form onSubmit={handleSubmit}>
          <input
            type="password"
            value={password}
            onChange={(e) => setPasswordInput(e.target.value)}
            placeholder="Password"
            autoFocus
            className="w-full border rounded-md px-3 py-2 text-sm text-slate-700 focus:outline-none focus:ring-2 focus:ring-blue-500 mb-4"
          />
          {error && (
            <p className="text-sm text-red-600 mb-4">{error}</p>
          )}
          <button
            type="submit"
            disabled={loading || !password}
            className="w-full flex items-center justify-center gap-2 px-4 py-2 rounded-lg bg-slate-900 text-white text-sm font-medium hover:bg-slate-800 disabled:opacity-50"
          >
            {loading ? <Loader2 className="w-4 h-4 animate-spin" /> : 'Login'}
          </button>
        </form>

        <p className="text-xs text-slate-400 mt-4 text-center">
          Default password is <code className="bg-slate-100 px-1 rounded">admin</code>. Change it in <code className="bg-slate-100 px-1 rounded">Server/.admin-password</code>.
        </p>
      </div>
    </div>
  )
}
