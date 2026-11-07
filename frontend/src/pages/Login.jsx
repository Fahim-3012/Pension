import { useRef, useState } from 'react'
import api, { errText } from '../api.js'
import { PENSION_TYPES, BANKS, MIN_AGE, MIN_DATE, MAX_DATE, GENDERS, RELATIONS, ageAt } from '../constants.js'
import { checkSignup, fieldFromServerMessage } from '../validate.js'

export default function Login({ onLoggedIn, onBack, startMode }) {
  const [mode, setMode] = useState(startMode || 'pensioner') // pensioner | admin | register
  const [f, setF] = useState({})
  const [mfa, setMfa] = useState(false)
  const [showPw, setShowPw] = useState(false)
  const formRef = useRef(null)
  const [msg, setMsg] = useState('')
  const [err, setErr] = useState('')
  const [badField, setBadField] = useState('')
  const set = (k) => (e) => { setF({ ...f, [k]: e.target.value }); if (badField === k) setBadField('') }
  // red mark helper: adds the "bad" class (red border) to the wrong field
  const fp = (k) => ({ className: badField === k ? 'bad' : '', 'data-f': k })
  const showBad = (b) => {
    setErr(b.m); setBadField(b.field)
    setTimeout(() => formRef.current?.querySelector(`[data-f="${b.field}"]`)?.focus(), 50)
  }

  const finish = (d) => {
    localStorage.setItem('token', d.token)
    localStorage.setItem('role', d.role)
    onLoggedIn(d.role)
  }

  const submit = async (e) => {
    e.preventDefault(); setErr(''); setMsg('')
    try {
      if (mode === 'register') {
        const bad = checkSignup(f)
        if (bad) { showBad(bad); return }
        const { data } = await api.post('/auth/register', f)
        // auto login: the server returned a token, so go straight to the dashboard
        localStorage.setItem('welcome', `Welcome! Pension ID: ${data.pensionId}. Your 11-digit Pension Code: ${data.pensionCode}. Please save both.`)
        finish(data)
      } else if (mode === 'pensioner') {
        const { data } = await api.post('/auth/login', { identifier: f.identifier, password: f.password })
        finish(data)
      } else {
        const { data } = await api.post('/auth/admin/login', {
          email: f.email, password: f.password, mfaCode: mfa ? f.mfaCode : null
        })
        if (data.mfaRequired) { setMfa(true); setMsg('MFA code generated. Check the API console (demo).') }
        else finish(data)
      }
    } catch (ex) {
      const m = errText(ex)
      setErr(m)
      if (mode === 'register') {
        const b = fieldFromServerMessage(m)
        if (b) { setBadField(b.field); setTimeout(() => formRef.current?.querySelector(`[data-f="${b.field}"]`)?.focus(), 50) }
      }
    }
  }

  const title = mode === 'register' ? 'Sign Up' : mode === 'admin' ? 'Admin Login' : 'Login'
  return (
    <div className="auth-page">
      <div className="auth-card">
        <button type="button" className="link" onClick={onBack}>← Home</button>
        <h2>🏦 {title}</h2>
        {mode !== 'admin' && (
          <div className="pill">
            <button className={mode === 'pensioner' ? 'on' : ''} onClick={() => { setMode('pensioner'); setErr(''); setMsg('') }}>Login</button>
            <button className={mode === 'register' ? 'on' : ''} onClick={() => { setMode('register'); setErr(''); setMsg('') }}>Sign Up</button>
          </div>
        )}
        {msg && <div className="msg">{msg}</div>}
        {err && <div className="msg err">{err}</div>}
        <form onSubmit={submit} autoComplete="off" noValidate={mode === 'register'} ref={formRef}>
          {mode === 'pensioner' && (<>
            <label>Email, Pension ID or Pension Code</label><input required autoComplete="off" onChange={set('identifier')} />
            <label>Password</label><input required type={showPw ? 'text' : 'password'} autoComplete="new-password" onChange={set('password')} />
            <label className="chk"><input type="checkbox" checked={showPw} onChange={(e) => setShowPw(e.target.checked)} /> Show password</label>
          </>)}
          {mode === 'admin' && (<>
            <label>Email</label><input required type="email" autoComplete="off" onChange={set('email')} />
            <label>Password</label><input required type={showPw ? 'text' : 'password'} autoComplete="new-password" onChange={set('password')} />
            {mfa && (<><label>MFA Code</label><input required autoComplete="off" inputMode="numeric" onChange={set('mfaCode')} /></>)}
          </>)}
          {mode === 'register' && (<>
            <h3 className="sec-h first">Personal details</h3>
            <div>
              <label>Full Name</label><input required {...fp('fullName')} onChange={set('fullName')} />
              <label>NID Number (10, 13 or 17 digits)</label><input required inputMode="numeric" {...fp('nid')} onChange={set('nid')} />
              <label>Mobile Number (e.g. 01712345678)</label><input required type="tel" inputMode="numeric" maxLength={11} {...fp('phone')} onChange={set('phone')} />
              <label>Email</label><input required type="email" autoComplete="off" {...fp('email')} onChange={set('email')} />
              <label>Date of Birth</label><input required type="date" min={MIN_DATE} max={new Date().toISOString().slice(0, 10)} {...fp('dateOfBirth')} onChange={set('dateOfBirth')} />
              <label>Gender</label>
              <select required defaultValue="" {...fp('gender')} onChange={set('gender')}><option value="" disabled>Select...</option>{GENDERS.map((t) => <option key={t}>{t}</option>)}</select>
              <label>Address</label><input required {...fp('address')} onChange={set('address')} />
            </div>

            <h3 className="sec-h">Pension and bank</h3>
            <div>
              <label>Pension ID (6-20 letters or numbers, e.g. PEN-123456)</label><input required autoComplete="off" {...fp('pensionId')} onChange={set('pensionId')} />
              <label>Pension Type</label>
              <select required defaultValue="" {...fp('pensionType')} onChange={set('pensionType')}><option value="" disabled>Select...</option>{PENSION_TYPES.map((t) => <option key={t}>{t}</option>)}</select>
              <label>Date of Retirement</label><input required type="date" min={MIN_DATE} max={MAX_DATE} {...fp('retirementDate')} onChange={set('retirementDate')} />
              {f.dateOfBirth && f.retirementDate && f.pensionType &&
                <p className="mut">Age at retirement: {ageAt(f.dateOfBirth, f.retirementDate)} years. Minimum for this pension type: {MIN_AGE[f.pensionType]} years.</p>}
              <label>Nominee Name</label><input required {...fp('nomineeName')} onChange={set('nomineeName')} />
              <label>Nominee Relation</label>
              <select required defaultValue="" {...fp('nomineeRelation')} onChange={set('nomineeRelation')}><option value="" disabled>Select...</option>{RELATIONS.map((t) => <option key={t}>{t}</option>)}</select>
              <label>Bank Type</label>
              <select required defaultValue="" {...fp('bankType')} onChange={(e) => { setF({ ...f, bankType: e.target.value, bankName: '' }); if (badField === 'bankType') setBadField('') }}><option value="" disabled>Select...</option>{Object.keys(BANKS).map((t) => <option key={t}>{t}</option>)}</select>
              <label>Bank Name</label>
              <select required value={f.bankName || ''} {...fp('bankName')} onChange={set('bankName')}><option value="" disabled>Select...</option>{(BANKS[f.bankType] || []).map((t) => <option key={t}>{t}</option>)}</select>
              <label>Branch</label><input required {...fp('branch')} onChange={set('branch')} />
              <label>{f.bankType === 'Mobile Financial Service' ? 'Mobile Banking Number' : 'Account Number (8-20 digits)'}</label><input required inputMode="numeric" {...fp('accountNumber')} onChange={set('accountNumber')} />
              <label>Account Holder Name (must match your full name)</label><input required {...fp('accountHolder')} onChange={set('accountHolder')} />
            </div>

            <h3 className="sec-h">Security</h3>
            <div>
              <label>Password</label><input required type={showPw ? 'text' : 'password'} autoComplete="new-password" {...fp('password')} onChange={set('password')} />
              <label>Confirm Password</label><input required type={showPw ? 'text' : 'password'} autoComplete="new-password" {...fp('confirmPassword')} onChange={set('confirmPassword')} />
              <label className="chk"><input type="checkbox" checked={showPw} onChange={(e) => setShowPw(e.target.checked)} /> Show password</label>
              <ul className="rules">
                <li>{(f.password || '').length >= 8 ? '✅' : '⬜'} At least 8 characters</li>
                <li>{/[A-Z]/.test(f.password || '') ? '✅' : '⬜'} One capital letter</li>
                <li>{/\d/.test(f.password || '') ? '✅' : '⬜'} One digit</li>
                <li>{f.password && f.password === f.confirmPassword ? '✅' : '⬜'} Both passwords match</li>
              </ul>
              <label className={'chk ' + (badField === 'acceptedTerms' ? 'bad-text' : '')} data-f="acceptedTerms"><input type="checkbox" onChange={(e) => { setF({ ...f, acceptedTerms: e.target.checked }); setBadField('') }} /> I agree to the Terms and Privacy Policy and confirm that my information is correct.</label>
            </div>
          </>)}
          {mode === 'register' ? (
            <button type="submit">Create Account</button>
          ) : <button type="submit">{mfa ? 'Verify & Login' : 'Login'}</button>}
        </form>
        {mode === 'admin'
          ? <button type="button" className="link" onClick={() => { setMode('pensioner'); setMfa(false) }}>← Pensioner login</button>
          : <button type="button" className="link" onClick={() => { setMode('admin'); setMfa(false) }}>Administrator login</button>}
      </div>
    </div>
  )
}
