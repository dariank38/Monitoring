import React from 'react'
import ReactDOM from 'react-dom/client'
import { BrowserRouter, Routes, Route } from 'react-router-dom'
import './index.css'
import Dashboard from './pages/Dashboard.jsx'
import MachineDetail from './pages/MachineDetail.jsx'

ReactDOM.createRoot(document.getElementById('root')).render(
  <React.StrictMode>
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<Dashboard />} />
        <Route path="/machine/:hardwareId" element={<MachineDetail />} />
      </Routes>
    </BrowserRouter>
  </React.StrictMode>,
)
