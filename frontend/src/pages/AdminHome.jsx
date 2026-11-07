import { useEffect, useState } from 'react'
import api, { errText } from '../api.js'

const fmt = (d) => (d ? new Date(d).toLocaleString() : '-')

export default function AdminHome({ onLogout }) {
  const [tab, setTab] = useState('wd')
  const [pens, setPens] = useState([])
  const [wds, setWds] = useState([])
  const [filter, setFilter] = useState('Pending')
  const [rep, setRep] = useState(null)
  const [audit, setAudit] = useState([])
  const [err, setErr] = useState('')

  const load = async () => {
    try {
      const [p, w, r, a] = await Promise.all([
        api.get('/admin/pensioners'), api.get('/admin/withdrawals', { params: { status: filter } }),
        api.get('/admin/reports'), api.get('/admin/audit')
      ])
      setPens(p.data); setWds(w.data); setRep(r.data); setAudit(a.data)
    } catch (e) { setErr(errText(e)) }
  }
  useEffect(() => { load() }, [filter])

  const decide = async (id, status) => {
    const note = status === 'Rejected' ? prompt('Reason for rejection?') || '' : ''
    await api.post(`/admin/withdrawals/${id}/decision`, { status, note }); load()
  }
  const savePen = async (p) => {
    await api.put(`/admin/pensioners/${p.id}`, { monthlyPension: Number(p.monthlyPension), isActive: p.isActive, bankVerified: p.bankVerified, isApproved: p.isApproved })
    load()
  }
  const edit = (id, k, v) => setPens(pens.map((p) => (p.id === id ? { ...p, [k]: v } : p)))

  return (
    <div className="container">
      <div className="card"><h1>Admin Portal</h1><button className="secondary" onClick={onLogout}>Logout</button></div>
      {err && <div className="msg err">{err}</div>}
      <div className="tabs">
        {[['wd', 'Withdrawals'], ['pen', 'Pensioners'], ['rep', 'Reports'], ['aud', 'Audit Log']].map(([k, l]) => (
          <button key={k} className={tab === k ? 'active' : ''} onClick={() => setTab(k)}>{l}</button>))}
      </div>

      {tab === 'wd' && (
        <div className="card"><h2>Withdrawal Requests</h2>
          <select value={filter} onChange={(e) => setFilter(e.target.value)}>
            {['Pending', 'Approved', 'Rejected', 'Completed'].map((s) => <option key={s}>{s}</option>)}
          </select>
          <table><thead><tr><th>ID</th><th>Pensioner</th><th>Amount</th><th>Date</th><th>Action</th></tr></thead>
            <tbody>{wds.map((w) => (
              <tr key={w.id}><td>{w.id}</td><td>{w.name} ({w.pensionId})</td><td>{w.amount.toLocaleString()}</td><td>{fmt(w.requestedAt)}</td>
                <td>
                  {w.status === 'Pending' && <><button className="green" onClick={() => decide(w.id, 'Approved')}>Approve</button>
                    <button className="red" onClick={() => decide(w.id, 'Rejected')}>Reject</button></>}
                  {w.status === 'Approved' && <button onClick={() => decide(w.id, 'Completed')}>Mark Completed</button>}
                </td></tr>))}
            </tbody></table>
        </div>
      )}

      {tab === 'pen' && (
        <div className="card"><h2>Manage Pensioners</h2>
          <table><thead><tr><th>ID / Code</th><th>Name / NID</th><th>Bank / Account</th><th>Monthly</th><th>Bank OK</th><th>Approved</th><th>Active</th><th></th></tr></thead>
            <tbody>{pens.map((p) => (
              <tr key={p.id}><td>{p.pensionId}<br />{p.pensionCode}</td><td>{p.fullName}<br />NID {p.nid}<br />{p.phone}<br />Nominee: {p.nomineeName} ({p.nomineeRelation})</td><td>{p.bankName} ({p.bankType})<br />{p.accountNumber}</td>
                <td><input type="number" style={{ width: 110 }} value={p.monthlyPension} onChange={(e) => edit(p.id, 'monthlyPension', e.target.value)} /></td>
                <td><input type="checkbox" checked={p.bankVerified} onChange={(e) => edit(p.id, 'bankVerified', e.target.checked)} /></td>
                <td><input type="checkbox" checked={p.isApproved} onChange={(e) => edit(p.id, 'isApproved', e.target.checked)} /></td>
                <td><input type="checkbox" checked={p.isActive} onChange={(e) => edit(p.id, 'isActive', e.target.checked)} /></td>
                <td><button onClick={() => savePen(p)}>Save</button></td></tr>))}
            </tbody></table>
        </div>
      )}

      {tab === 'rep' && rep && (
        <div className="card"><h2>Reports & Monitoring</h2>
          <div className="grid">
            {Object.entries(rep).map(([k, v]) => (
              <div className="msg" key={k}><div>{k}</div><div className="big">{typeof v === 'number' ? v.toLocaleString() : v}</div></div>))}
          </div>
        </div>
      )}

      {tab === 'aud' && (
        <div className="card"><h2>Audit Log (latest 100)</h2>
          <table><thead><tr><th>Time</th><th>User</th><th>Action</th></tr></thead>
            <tbody>{audit.map((a) => <tr key={a.id}><td>{fmt(a.at)}</td><td>{a.userName}</td><td>{a.action}</td></tr>)}</tbody></table>
        </div>
      )}
    </div>
  )
}
