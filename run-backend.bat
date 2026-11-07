@echo off
title Smart Pension - BACKEND (keep this window open)
cd /d "%~dp0backend\SmartPension.Api"
echo Starting API on http://localhost:5080  (MySQL must be running)
dotnet run
pause
