import { useEffect, useRef, useState } from 'react'
import api, { errText } from '../api.js'
import { BANKS, MIN_DATE, MAX_DATE } from '../constants.js'

const fmt = (d) => (d ? new Date(d).toLocaleDateString() : '-')

export default function PensionerHome({ onLogout }) {
  const [tab, setTab] = useState('dash')
  const [dash, setDash] = useState(null)
  const [hist, setHist] = useState({ items: [], totalWithdrawn: 0, totalRequested: 0, count: 0 })
  const [notes, setNotes] = useState([])
  const [support, setSupport] = useState(null)
  const [msg, setMsg] = useState(() => { const w = localStorage.getItem('welcome'); localStorage.removeItem('welcome'); return w || '' })
  const [err, setErr] = useState('')

  const load = async () => {
    const [d, h, n] = await Promise.all([
      api.get('/pensioner/dashboard'), api.get('/pensioner/withdrawals'), api.get('/pensioner/notifications')
    ])
    setDash(d.data); setHist(h.data); setNotes(n.data)
  }
  useEffect(() => { load(); api.get('/auth/support').then((r) => setSupport(r.data)) }, [])

  const run = async (fn, ok) => {
    setMsg(''); setErr('')
    try { await fn(); setMsg(ok); await load() } catch (e) { setErr(errText(e)) }
  }

  if (!dash) return <div className="container">Loading...</div>
  const el = dash.eligibility

  return (
    <div className="container">
      <div className="card">
        <h1>Welcome, {dash.fullName}</h1>
        <button className="secondary" onClick={onLogout}>Logout</button>
      </div>
      <div className="tabs">
        {[['dash', 'Dashboard'], ['hist', 'History'], ['verify', 'Verification'], ['bank', 'Bank'], ['calc', 'Calculator'], ['edit', 'Edit Info'],
          ['notes', 'Notifications'], ['ai', 'Assistant'], ['help', 'Customer Care']].map(([k, l]) => (
          <button key={k} className={tab === k ? 'active' : ''} onClick={() => { setTab(k); setMsg(''); setErr('') }}>{l}</button>
        ))}
      </div>
      {msg && <div className="msg">{msg}</div>}
      {err && <div className="msg err">{err}</div>}

      {!dash.isApproved && <div className="msg">⏳ Your account is waiting for admin approval. You can use your account, but withdrawals stay locked until it is approved.</div>}

      {tab === 'dash' && (<>
        <div className="card"><h2>Customer Information</h2>
          <button onClick={() => setTab('edit')}>✏️ Edit Information</button>
          <div className="grid">
            {[['Full Name', dash.fullName], ['NID', dash.nid], ['Phone', dash.phone], ['Date of Birth', fmt(dash.dateOfBirth)],
              ['Date of Retirement', fmt(dash.retirementDate)], ['Age at Retirement', `${dash.ageAtRetirement} years (minimum ${dash.minAge})`], ['Pension Type', dash.pensionType], ['Pension Code (11 digits)', dash.pensionCode], ['Gender', dash.gender], ['Address', dash.address], ['Nominee', `${dash.nomineeName} (${dash.nomineeRelation})`]]
              .map(([k, v]) => <div key={k}><div className="mut">{k}</div><div><b>{v}</b></div></div>)}
          </div>
        </div>
        <div className="card grid">
          <div><div>Pension ID</div><div className="big">{dash.pensionId}</div></div>
          <div><div>Monthly Pension</div><div className="big">{dash.monthlyPension.toLocaleString()} BDT</div></div>
          <div><div>Bank</div><div className="big">{dash.bankName}</div><div>{dash.accountMasked} ({dash.bankVerified ? 'Verified' : 'Not verified'})</div></div>
        </div>
        <div className="card">
          <h2>Next Withdrawal</h2>
          <p>Eligible date: <b>{fmt(el.nextDate)}</b> &nbsp; Amount: <b>{el.amount.toLocaleString()} BDT</b></p>
          {!el.canRequest && <div className="msg">{el.reason}</div>}
          <button className="green" disabled={!el.canRequest}
            onClick={() => run(() => api.post('/pensioner/withdrawals'), 'Withdrawal request submitted.')}>
            Request Withdrawal
          </button>
        </div>
      </>)}

      {tab === 'hist' && (
        <div className="card"><h2>Withdrawal History</h2>
          <div className="grid">
            <div className="msg"><div className="mut">Total Amount Withdrawn (Completed)</div><div className="big">{hist.totalWithdrawn.toLocaleString()} BDT</div></div>
            <div className="msg"><div className="mut">Total Requested (excl. rejected)</div><div className="big">{hist.totalRequested.toLocaleString()} BDT</div></div>
            <div className="msg"><div className="mut">Number of Requests</div><div className="big">{hist.count}</div></div>
          </div>
          <div className="scroll"><table><thead><tr><th>Date</th><th>Amount</th><th>Status</th><th>Note</th></tr></thead>
            <tbody>{hist.items.map((h) => (
              <tr key={h.id}><td>{fmt(h.requestedAt)}</td><td>{h.amount.toLocaleString()}</td>
                <td><span className="badge">{h.status}</span></td><td>{h.adminNote}</td></tr>))}
            </tbody></table></div>
          {!hist.items.length && <p>No withdrawals yet.</p>}
        </div>
      )}

      {tab === 'verify' && (
        <div className="card">
          <h2>Life Verification (Live Camera)</h2>
          <p>Last verified: <b>{fmt(dash.lastLifeVerification)}</b></p>
          <div className="msg">Verification uses your live camera only. You cannot upload a photo from the gallery or files.</div>
          <FaceVerify enrolled={dash.faceEnrolled} onDone={(m) => { setMsg(m); setErr(''); load() }} onErr={(m) => { setErr(m); setMsg('') }} />
        </div>
      )}

      {tab === 'bank' && <BankForm dash={dash} onSave={(b) => run(() => api.put('/pensioner/bank', b), 'Bank updated. Admin will verify it.')} />}

      {tab === 'edit' && <EditInfo dash={dash} onSave={(p) => run(async () => { await api.put('/pensioner/profile', p); setTab('dash') }, 'Information updated.')} />}

      {tab === 'calc' && <Calculator />}

      {tab === 'notes' && (
        <div className="card"><h2>Notifications</h2>
          {notes.map((n) => <div className="msg" key={n.id}>{fmt(n.createdAt)}: {n.message}</div>)}
          {!notes.length && <p>No notifications.</p>}
        </div>
      )}

      {tab === 'ai' && <Assistant />}

      {tab === 'help' && support && (
        <div className="card"><h2>Customer Care</h2>
          <p>Phone: <b>{support.phone}</b></p><p>Email: <b>{support.email}</b></p><p>Hours: {support.hours}</p>
        </div>
      )}
    </div>
  )
}

function BankForm({ dash, onSave }) {
  const [b, setB] = useState({ bankType: dash.bankType || Object.keys(BANKS)[0], bankName: dash.bankName, branch: dash.branch || '',
    accountNumber: '', accountHolder: dash.accountHolder, dateOfBirth: '', pensionCode: '' })
  const s = (k) => (e) => setB({ ...b, [k]: e.target.value })
  const ok = b.accountNumber && b.dateOfBirth && b.pensionCode.length === 11 && b.bankName
  return (
    <div className="card"><h2>Bank Account</h2>
      <p>Status: <b>{dash.bankVerified ? 'Verified' : 'Waiting for admin verification'}</b></p>
      <label>Bank Type</label>
      <select value={b.bankType} onChange={(e) => setB({ ...b, bankType: e.target.value, bankName: BANKS[e.target.value][0] })}>
        {Object.keys(BANKS).map((t) => <option key={t}>{t}</option>)}</select>
      <label>Bank Name</label>
      <select value={b.bankName} onChange={s('bankName')}>{(BANKS[b.bankType] || []).map((t) => <option key={t}>{t}</option>)}</select>
      <label>Branch</label><input value={b.branch} onChange={s('branch')} />
      <label>New Account Number</label><input value={b.accountNumber} onChange={s('accountNumber')} />
      <label>Account Holder</label><input value={b.accountHolder} onChange={s('accountHolder')} />
      <h3 style={{ marginTop: 18 }}>Identity check</h3>
      <label>Date of Birth</label><input type="date" value={b.dateOfBirth} onChange={s('dateOfBirth')} />
      <label>Pension Code (11 digits)</label><input inputMode="numeric" maxLength={11} value={b.pensionCode} onChange={s('pensionCode')} />
      <button disabled={!ok} onClick={() => onSave(b)}>Save</button>
    </div>
  )
}

function EditInfo({ dash, onSave }) {
  const d10 = (x) => (x ? String(x).slice(0, 10) : '')
  const [f, setF] = useState({ fullName: dash.fullName, nid: dash.nid, phone: dash.phone,
    dateOfBirth: d10(dash.dateOfBirth), retirementDate: d10(dash.retirementDate) })
  const s = (k) => (e) => setF({ ...f, [k]: e.target.value })
  return (
    <div className="card"><h2>Edit Information</h2>
      <label>Full Name</label><input value={f.fullName} onChange={s('fullName')} />
      <label>NID Number (10, 13 or 17 digits)</label><input inputMode="numeric" value={f.nid} onChange={s('nid')} />
      <label>Phone Number</label><input type="tel" value={f.phone} onChange={s('phone')} />
      <label>Date of Birth</label><input type="date" min={MIN_DATE} max={MAX_DATE} value={f.dateOfBirth} onChange={s('dateOfBirth')} />
      <label>Date of Retirement</label><input type="date" min={MIN_DATE} max={MAX_DATE} value={f.retirementDate} onChange={s('retirementDate')} />
      <p className="mut">Pension Code {dash.pensionCode} cannot be changed.</p>
      <button className="green" onClick={() => onSave(f)}>Save Changes</button>
    </div>
  )
}

function Calculator() {
  const [f, setF] = useState({ lastBasic: 60000, years: 25, commutePct: 0, taxPct: 0 })
  const [r, setR] = useState(null)
  const [e, setE] = useState('')
  const set = (k) => (ev) => setF({ ...f, [k]: ev.target.value })
  const go = async () => {
    try {
      const { data } = await api.post('/pensioner/calculate', { lastBasic: +f.lastBasic, years: +f.years, commutePct: +f.commutePct, taxPct: +f.taxPct })
      setR(data); setE('')
    } catch (x) { setE(errText(x)) }
  }
  return (
    <div className="card"><h2>Pension Calculator</h2>
      <label>Last Basic Salary (BDT)</label><input type="number" value={f.lastBasic} onChange={set('lastBasic')} />
      <label>Years of Service</label><input type="number" value={f.years} onChange={set('years')} />
      <label>Commutation (lump sum)</label>
      <select value={f.commutePct} onChange={set('commutePct')}><option value="0">0%</option><option value="50">50%</option><option value="100">100%</option></select>
      <label>Tax / Deduction %</label><input type="number" value={f.taxPct} onChange={set('taxPct')} />
      <button onClick={go}>Calculate</button>
      {e && <div className="msg err">{e}</div>}
      {r && <div className="msg"><div className="mut">Net Pension at Retirement Date ({new Date(r.retirementDate).toLocaleDateString()}) - {r.pensionType}</div>
        <div className="big">{r.netMonthly.toLocaleString()} BDT / month</div>
        <p>Gross monthly: {r.gross.toLocaleString()}<br />Commuted lump sum: {r.lumpSum.toLocaleString()}<br />Deductions: {r.tax.toLocaleString()}</p></div>}
      <div className="mut">Demo formula: gross = last basic x type rate x min(years,25)/25. Edit PensionCalculator in Services.cs for your real rules.</div>
    </div>
  )
}

function FaceVerify({ enrolled, onDone, onErr }) {
  const video = useRef(null)
  const canvas = useRef(null)
  const [on, setOn] = useState(false)
  const [ch, setCh] = useState(null)
  const [busy, setBusy] = useState(false)

  const stop = () => { video.current?.srcObject?.getTracks().forEach((t) => t.stop()); setOn(false); setCh(null); setBusy(false) }
  const start = async () => {
    try {
      // Only getUserMedia is used. There is intentionally no <input type="file"> anywhere.
      const s = await navigator.mediaDevices.getUserMedia({ video: { facingMode: 'user' }, audio: false })
      video.current.srcObject = s; setOn(true)
      const { data } = await api.get('/pensioner/face/challenge'); setCh(data)
    } catch { stop(); onErr('Camera permission is required. Gallery or file upload is not allowed.') }
  }
  const grab = () => {
    const c = canvas.current, v = video.current
    c.width = v.videoWidth; c.height = v.videoHeight
    c.getContext('2d').drawImage(v, 0, 0)
    return c.toDataURL('image/jpeg', 0.7)
  }
  const capture = async () => {
    setBusy(true)
    const image = grab()
    await new Promise((r) => setTimeout(r, 3000)) // user performs the challenge
    const image2 = grab()
    try {
      const { data } = await api.post('/pensioner/face-verify', { nonce: ch.nonce, image, image2, source: 'camera' })
      onDone(data.enrolled ? data.message : `Life verification successful (score ${data.score}).`)
    } catch (e) { onErr(errText(e)) }
    stop()
  }

  return (
    <div style={{ marginTop: 20 }}>
      <p>{enrolled ? 'Your face is enrolled. Open the camera to verify.' : 'First time: the camera capture will enroll your face.'}</p>
      <video ref={video} autoPlay playsInline muted style={{ display: on ? 'block' : 'none', transform: 'scaleX(-1)' }} />
      <canvas ref={canvas} style={{ display: 'none' }} />
      {on && ch && <div className="msg"><b>Challenge:</b> {ch.instruction}. {busy ? 'Do it now, capturing...' : 'Press Capture, then do it for 3 seconds.'}</div>}
      {!on ? <button onClick={start}>Open Camera</button>
        : <><button className="green" disabled={busy || !ch} onClick={capture}>Capture</button><button className="secondary" disabled={busy} onClick={stop}>Cancel</button></>}
    </div>
  )
}

function Assistant() {
  const [q, setQ] = useState('')
  const [log, setLog] = useState([{ me: false, t: 'Hello! Ask me about your withdrawal, verification, bank or pension amount.' }])
  const ask = async () => {
    if (!q.trim()) return
    const question = q; setQ(''); setLog((l) => [...l, { me: true, t: question }])
    try {
      const { data } = await api.post('/assistant/ask', { question })
      setLog((l) => [...l, { me: false, t: data.answer }])
    } catch { setLog((l) => [...l, { me: false, t: 'Sorry, I could not answer now.' }]) }
  }
  return (
    <div className="card"><h2>AI Pension Assistant</h2>
      <div className="chat">{log.map((m, i) => <p key={i} className={m.me ? 'me' : ''}>{m.t}</p>)}</div>
      <input value={q} onChange={(e) => setQ(e.target.value)} onKeyDown={(e) => e.key === 'Enter' && ask()} placeholder="Type your question..." />
      <button onClick={ask}>Ask</button>
    </div>
  )
}
