import { useState } from 'react'
import Landing from './pages/Landing.jsx'
import Login from './pages/Login.jsx'
import PensionerHome from './pages/PensionerHome.jsx'
import AdminHome from './pages/AdminHome.jsx'

export default function App() {
  const [role, setRole] = useState(localStorage.getItem('role'))
  const [view, setView] = useState('landing') // landing | auth
  const [startMode, setStartMode] = useState('pensioner')
  const logout = () => { localStorage.clear(); setRole(null); setView('landing') }

  if (role === 'Pensioner') return <PensionerHome onLogout={logout} />
  if (role === 'Admin') return <AdminHome onLogout={logout} />
  if (view === 'landing')
    return <Landing onStart={(m) => { setStartMode(m); setView('auth') }} />
  return <Login key={startMode} startMode={startMode} onBack={() => setView('landing')} onLoggedIn={(r) => setRole(r)} />
}
