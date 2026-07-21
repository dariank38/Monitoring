import React, { useState, useEffect } from 'react'
import ReactDOM from 'react-dom/client'
import { BrowserRouter, Routes, Route } from 'react-router-dom'
import './index.css'
import { isAuthenticated } from './lib/api'
import Login from './pages/Login.jsx'
import Dashboard from './pages/Dashboard.jsx'
import MachineDetail from './pages/MachineDetail.jsx'
import Settings from './pages/Settings.jsx'
import LiveView from './pages/LiveView.jsx'

function App() {
  const [authed, setAuthed] = useState(isAuthenticated())

  if (!authed) {
    return <Login onSuccess={() => setAuthed(true)} />
  }

  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<Dashboard />} />
        <Route path="/live" element={<LiveView />} />
        <Route path="/machine/:hardwareId" element={<MachineDetail />} />
        <Route path="/settings" element={<Settings />} />
      </Routes>
    </BrowserRouter>
  )
}

ReactDOM.createRoot(document.getElementById('root')).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>,
)
