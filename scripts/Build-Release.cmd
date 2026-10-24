@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build-Release.ps1" %*
