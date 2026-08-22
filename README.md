<div align="center">
<img src="./Images/baner.png" alt="Squawk" width="100%" />
<img src="https://readme-typing-svg.herokuapp.com?font=Fira+Code&size=28&pause=1000&color=1b5e20&center=true&vCenter=true&width=600&lines=🚀+Squawk+V1+;🔥+Web+Game;🛠️+Built+by+Hnato;" alt="Squawk" />

**Fast-Paced Real-Time Multiplayer Snake Arena Game**

![.NET](https://img.shields.io/badge/.NET-11.0-blueviolet?style=for-the-badge&logo=dotnet)
![SignalR](https://img.shields.io/badge/Network-ASP.NET_SignalR-5C2D91?style=for-the-badge&logo=dotnet)
![React](https://img.shields.io/badge/Frontend-React_Vite-61DAFB?style=for-the-badge&logo=react)
![SQLite](https://img.shields.io/badge/DB-SQLite_EF_Core-003B57?style=for-the-badge&logo=sqlite)
![Author](https://img.shields.io/badge/Author-Hnato-brightgreen?style=for-the-badge&logo=github)

</div>

---

## ✨ Overview

**Squawk** is a modern, real-time multiplayer snake survival game. Compete against other players and intelligent AI bots in a circular arena. Eat food, grow your tail, boost strategically, and eliminate opponents to climb the global 24h leaderboard!

The project is packaged as a **single standalone executable (`Squawk.exe`)** for Windows, containing an embedded C# desktop host, high-frequency SignalR game server, and a sleek React/Vite Canvas web client.

---

## 🚀 Key Features

- 🎮 **Real-Time Multiplayer Engine:** High-performance tick rate driven by SignalR WebSockets.
- 🌐 **LAN & Local Broadcast:** Listens on `0.0.0.0:5007` (all network interfaces for LAN play) and `127.0.0.1:5006`.
- ⚡ **Full-Body Boost Energy Aura:** Sprinting activates a full-body glowing energy core and pulse aura across all snake body segments.
- 🤖 **Smart Growth AI Bots:** AI competitors actively harvest high-value food clusters (dead snake drops) and grow to massive sizes.
- 🏆 **SQLite Leaderboard System:** EF Core persistence for user accounts, 24-hour high scores, and player game history.
- 🎨 **Arcade Cyberpunk Design:** Dark cyber grid, metallic rank badges, custom snake skin customization, and interactive HUD.
- 📦 **Single File Executable (`Squawk.exe`):** Zero external dependencies required for deployment.

---

## 🛠 Technology Stack

### Backend & Server Host
- **Framework:** .NET 11.0 (C#)
- **Networking:** ASP.NET Core SignalR WebSockets
- **Database & ORM:** Entity Framework Core + SQLite
- **Host Interface:** Windows Forms (WinForms) for live server monitoring & bot control

### Frontend & Game Engine
- **UI Framework:** React 19 + TypeScript + Vite
- **Graphics Engine:** HTML5 Canvas 2D API with high-DPI rendering & smooth interpolation
- **Styling:** Custom Vanilla CSS (Cyberpunk Arcade design system)

---

## 📥 Quick Start & Usage

1. **Launch the Game:**
   Run `Squawk.exe` directly in the project root or move it to any Windows machine.
2. **Start the Engine:**
   Click **"Włącz Serwer"** in the C# GUI window. The application will log your local LAN IP address (e.g. `http://192.168.x.x:5007`).
3. **Play:**
   Open `http://localhost:5007` (or your LAN IP) in any modern web browser to log in, customize your snake skin, and join the arena!

---

## 🛠 Building from Source

To compile the entire client and bundle it into a standalone `Squawk.exe`:

```powershell
powershell -ExecutionPolicy Bypass -File .\build_release.ps1
```

The script will:
1. Build the React/Vite client (`npm run build`).
2. Embed the client assets and Win32 icon (`logo.ico`) into the C# project.
3. Publish a single-file self-contained `Squawk.exe` in the root workspace folder.

---

## 📂 Project Structure

```text
Squawk/
├─ Client/                  # React + Vite + TypeScript frontend
│  ├─ src/
│  │  ├─ components/       # UI Components (Login, Dashboard, Game, Leaderboards)
│  │  ├─ game/             # Canvas 2D Game Renderer & Client State
│  │  └─ index.css         # Cyberpunk Arcade Design System
│  └─ package.json
├─ Server/                  # C# .NET 11 Backend Host
│  ├─ Data/                # EF Core DbContext & User entities
│  ├─ Game/                # GameEngine tick loop, physics & bot AI
│  ├─ Hubs/                # SignalR GameHub
│  ├─ Form1.cs             # WinForms Host Controller
│  └─ Server.csproj
├─ build_release.ps1        # Automated Single-File Executable Build Script
└─ Squawk.exe               # Standalone Ready-to-Run Executable
```

---

## 👑 Credits & Authors

- 👨‍💻 **Hnato** – Lead Developer ([GitHub](https://github.com/Hnato))
- 🛠 **ThomasWack** – Original contributions

---

<div align="center">

&copy; 2026 Squawk Project by **Hnato**. All rights reserved.

</div>
