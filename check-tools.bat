@echo off
echo --- .NET SDK (need 8.x) ---
dotnet --version
echo --- Node.js (need 18+) ---
node -v
echo --- npm ---
npm -v
echo.
echo If any line above shows an error, install that tool first.
pause
