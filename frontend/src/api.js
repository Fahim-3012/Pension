import axios from 'axios'
 
const api = axios.create({ baseURL: (import.meta.env.VITE_API_URL || 'http://localhost:5080') + '/api' })
 
api.interceptors.request.use((cfg) => {
  const t = localStorage.getItem('token')
  if (t) cfg.headers.Authorization = `Bearer ${t}`
  return cfg
})
 
api.interceptors.response.use(
  (r) => r,
  (err) => {
    if (err.response?.status === 401 && localStorage.getItem('token')) {
      localStorage.clear()
      window.location.reload()
    }
    return Promise.reject(err)
  }
)
 
// Turns any API / network failure into a message that tells you what is really wrong.
export const errText = (e) => {
  if (!e.response)
    return 'Cannot reach the server. Start the backend (run-backend.bat) and open http://localhost:5080/api/auth/support to check it.'
  const d = e.response.data
  if (typeof d === 'string' && d.trim()) return d
  if (Array.isArray(d)) return d.join(', ')
  if (d && typeof d === 'object') {
    if (d.errors) return Object.values(d.errors).flat().join(' ')            // validation errors
    const m = [d.title, d.detail].filter(Boolean).join(': ')                   // server exception
    if (m) return m.slice(0, 400)
  }
  return `Server error (${e.response.status}). Look at the backend window for the red error text.`
}
 
export default api
 