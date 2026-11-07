# Smart Pension Management System

Backend: ASP.NET Core 8 Web API + Identity + MySQL (Pomelo EF Core)
Frontend: React + Vite

## Setup
1. Install .NET 8 SDK, Node.js 20+, MySQL 8, and `dotnet tool install --global dotnet-ef`
2. Create DB:  CREATE DATABASE smart_pension CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
3. Edit backend/SmartPension.Api/appsettings.json (MySQL password, JWT key)

## Backend (migrate + run)
cd backend/SmartPension.Api
dotnet restore
dotnet ef migrations add InitialCreate
dotnet ef database update
dotnet run --urls http://localhost:5080

Seeded admin: admin@pension.local / Admin@12345  (MFA code is printed in the API console)

## Frontend
cd frontend
npm install
npm run dev        # http://localhost:5173

## Test tip (make withdrawal eligible quickly)
UPDATE Pensioners SET RegisteredAt = DATE_SUB(NOW(), INTERVAL 40 DAY);
Then: admin ticks "Bank OK" for the pensioner, pensioner does life verification, then requests withdrawal.

## Life verification (camera only)
- Manual "I am alive" button and its API endpoint were removed.
- Frontend uses only navigator.mediaDevices.getUserMedia (no file input, so no gallery).
- Flow: GET /api/pensioner/face/challenge -> live challenge + nonce -> two live frames -> POST /api/pensioner/face-verify.
- Server rejects expired/reused nonces, identical frames, non-JPEG data and non-camera source.
- Prototype limit: a browser cannot be forced to prove pixels came from a camera. Use a real liveness provider in production.
- Camera needs https or http://localhost.

## Customer information, calculator, bank (new)
- Registration now collects NID, phone, date of birth, retirement date, pension type, bank type/name/branch.
- Every pensioner gets a unique 11-digit Pension Code (shown on the dashboard).
- Pension types and bank types are in `Services.cs` (Catalog) and `frontend/src/constants.js`.
- Calculator: Net Pension at Retirement Date (demo formula in `PensionCalculator`; replace with your rules).
- Bank update requires date of birth + 11-digit pension code to match the record.
- History shows Total Amount Withdrawn (Completed), total requested and request count.
- Because the database changed, run: `dotnet ef migrations add AddCustomerInfo` then `dotnet ef database update`
  (or drop the demo DB and re-run the original InitialCreate migration).

## Pages
Landing (Get Started) -> Login / Sign Up (Admin link) -> dashboard. See src/pages/Landing.jsx and Login.jsx.

## Mobile app (Android/iOS) with Capacitor
The UI is responsive and installable as a PWA. To build a real app:
  cd frontend
  cp .env.example .env   # set VITE_API_URL to your PC LAN IP or https API URL
  npm i @capacitor/core @capacitor/cli @capacitor/android
  npx cap init SmartPension com.example.smartpension --web-dir=dist
  npm run build && npx cap add android && npx cap sync && npx cap open android
Add the CAMERA permission in android/app/src/main/AndroidManifest.xml for live verification.
Backend CORS already allows capacitor://localhost and http(s)://localhost. Use HTTPS in production.

## Login
Pensioners log in with Email, Pension ID (PEN-xxxxxx) or the 11-digit Pension Code, plus password.

## Year limit and minimum age
- Dates of birth/retirement cannot be after 2026 (`Catalog.MaxYear`, `MAX_YEAR` in constants.js).
- Minimum age at retirement per pension type (`Catalog.MinRetirementAge`, `MIN_AGE`): Superannuation 59, Compulsory 50, Invalid 18, Family 0, Compensation 18. Demo values - change to your rules.
- Checked on sign up and on Edit Information; age at retirement is shown on the dashboard.

## Database setup (XAMPP / phpMyAdmin) - recommended, no `dotnet ef` needed
1. Start **MySQL** in XAMPP Control Panel (default port 3306, user `root`, empty password).
2. Open http://localhost/phpmyadmin -> **Import** -> choose `database/smart_pension.sql` -> **Go**.
3. In `backend/SmartPension.Api/appsettings.json` set the connection string:
   `Server=localhost;Port=3306;Database=smart_pension;User=root;Password=;`
   (put your own password after `Password=` if you set one).
4. Run the API: `dotnet run --urls http://localhost:5080`. It creates the admin user on first start.
Do NOT also run `dotnet ef database update` after importing the SQL (tables already exist).

### Common database errors
- `Unable to connect to any of the specified MySQL hosts`: MySQL is not running or wrong port/password.
- `Table ... already exists` after `dotnet ef database update`: you imported the SQL AND ran migrations. Use one method only. Fix: run `database/reset_database.sql`, then import `smart_pension.sql` (and skip migrations).
- `Unknown column 'Nid' / 'PensionCode' ...`: your DB was created from an older version. Run `reset_database.sql`, then import `smart_pension.sql`.
- `Duplicate entry ... for key IX_Pensioners_Nid`: that NID or Pension ID is already registered.

## Sign-up rules (v2)
- One-page form with sections: Personal details, Pension and bank, Security (no steps). Same rules are enforced in the backend.
- Mobile must be a Bangladesh number (01[3-9]xxxxxxxx) and unique; NID and Pension ID unique; 17-digit NID must start with the birth year.
- Date of birth cannot be in the future (year limit 2026 for retirement). Gender, address, nominee name/relation required.
- Account number 8-20 digits (or a mobile number for Mobile Financial Service); account holder must match the full name.
- Terms and Privacy consent is required (stored as ConsentAt).
- **Admin approval:** new accounts cannot log in until the admin ticks "Approved" (Admin > Pensioners > Save).
- Sign-up is rolled back (user deleted) if saving the pensioner record fails.
- Rate limit: 20 auth requests per minute per IP (Program.cs).
- Database changed: for a fresh start import database/smart_pension.sql; to keep old data run database/upgrade_existing_db.sql.
- NOT included (needs external services): email/phone OTP verification, CAPTCHA, NID photo upload, check against an official pensioner list.

## Auto login and red marks (v3)
- After a successful sign up the server returns a token and the user lands on the dashboard (no second login).
- New accounts can log in, but **withdrawals stay locked until the admin approves** the account (banner on the dashboard).
- Wrong or duplicate sign-up values (NID, phone, email, Pension ID, bank/account ...) get a **red border** and the field is focused; the message is shown at the top. Server-side errors (e.g. duplicate NID) are mapped to the same red mark.

## No admin approval (v4)
- A new account is active immediately and the user is logged in automatically after sign up. No admin approval is needed.
- The "Approved" tick in Admin > Pensioners stays only as an optional switch: if the admin unticks it, that user's withdrawals are blocked.
- Older README lines about mandatory approval no longer apply.

## API quick check (after `dotnet run`)
appsettings.json now defaults to XAMPP (user root, empty password) and port 5080, so plain `dotnet run` works.
- http://localhost:5080/api/auth/support  -> should return JSON (public endpoint, proves API + CORS are up)
- http://localhost:5080/api/auth/meta     -> pension types and banks
- Everything else needs a token (sign up or log in first). If MySQL has a password, edit `Password=` in appsettings.json.

## Windows quick start (double-click)
1. `check-tools.bat`     - shows if .NET 8, Node and npm are installed.
2. Start MySQL in XAMPP and import `database/smart_pension.sql` in phpMyAdmin (once).
3. `run-backend.bat`     - starts the API (http://localhost:5080). Keep the window open.
4. `run-frontend.bat`    - starts the website (http://localhost:5173). Keep the window open.
Admin: admin@pension.local / Admin@12345 (MFA code is printed in the backend window).
