export default function Landing({ onStart }) {
  return (
    <div className="land">
      <div className="nav">
        <div className="brand">🏦 Smart Pension</div>
        <div>
          <button className="btn-ghost" onClick={() => onStart('pensioner')}>Login</button>
          <button className="btn-light" onClick={() => onStart('register')}>Sign Up</button>
        </div>
      </div>

      <div className="hero">
        <div>
          <h1>Smart Pension Management System</h1>
          <p>Check your pension, request withdrawals, verify with your live camera and get help from the AI assistant. Simple, secure and made for everyone.</p>
          <div className="cta">
            <button className="btn-light" onClick={() => onStart('pensioner')}>Get Started →</button>
            <button className="btn-ghost" onClick={() => onStart('admin')}>Admin Portal</button>
          </div>
        </div>
      </div>

      <div className="foot">© Smart Pension &middot; Customer care +880-1700-000000 &middot; Sun-Thu 9 AM - 5 PM</div>
    </div>
  )
}
