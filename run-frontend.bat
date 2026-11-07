@echo off
title Smart Pension - FRONTEND (keep this window open)
cd /d "%~dp0frontend"
if not exist node_modules call npm install
echo Starting website on http://localhost:5173
call npm run dev
pause
