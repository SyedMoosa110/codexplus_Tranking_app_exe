# WorkTrack Pro - Windows Employee Work Agent

Native C# .NET 8 desktop background agent with native Win32 input monitoring, foreground window tracking, local SQLite resilience queue, and seamless automatic updates.

## 📥 Download & Install

1. Download **[WorkAgent-v1.0.0.zip](WorkAgent-v1.0.0.zip)** from this repository.
2. Extract the ZIP folder on any Windows 10/11 laptop.
3. Open `WorkAgent.exe`.
4. Enter your employee corporate email and password.

## 🔄 Automatic Silent Updates
This agent includes a built-in background auto-updater:
- Every 15 minutes, it checks `version.json` in this GitHub repository.
- Whenever a new version is pushed to this repository:
  1. The agent downloads the update in the background.
  2. Silently replaces the binary.
  3. Seamlessly restarts tracking with zero employee interruption.
