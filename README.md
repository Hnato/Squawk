<div align="center"> 
 
 # 🦜 Squawk V1 
 **Fast-Paced Multiplayer Parrot Survival Game** 
 
 ![.NET](https://img.shields.io/badge/.NET-11.0-blueviolet?style=for-the-badge&logo=dotnet) 
 ![Websocket](https://img.shields.io/badge/Network-WatsonWebsocket-5C2D91?style=for-the-badge&logo=dotnet) 
 ![DB](https://img.shields.io/badge/DB-SQLite-blue?style=for-the-badge&logo=sqlite) 
 ![Version](https://img.shields.io/badge/Version-V12.0_Parrot_Cove_Edition-brightgreen?style=for-the-badge) 
 
 </div> 
 
 --- 
 
 ## ✨ Overview 
 
 **Squawk** is a modern multiplayer game where you take control of a colorful parrot. Compete with other players and bots in a circular arena, eat food to grow your tail, and avoid colliding with others.  
 Version **V12 "Parrot Cove Edition"** brings a massive technological overhaul, migrating to **.NET 11** and **C# 15**, with enhanced physics and bot AI. 🦜🌊 
 
 The system consists of: 
 
 - 🖥 **Windows Desktop Host** – a WinForms-based server manager with real-time logging. 
 - 🚀 **WebSocket Engine** – a high-performance backend powered by WatsonWebsocket for low-latency gameplay. 
 - 🌐 **Web Client** – a smooth Vanilla JS + Canvas interface with a futuristic neon aesthetic. 
 - 💾 **Database Layer** – SQLite + Dapper for persistent player states and global leaderboards. 
 
 --- 
 
 ## 🆕 What's New in V1 (Changelog) 
 
 ### 🚀 Technology Migration 
 - **.NET 11 & C# 15:** Full migration to the latest .NET preview, utilizing primary constructors and collection expressions. 
 - **Performance:** Optimized game loop (Tick) for stable 60 FPS server-side processing. 
 - **Modern Locking:** Implementation of the new `System.Threading.Lock` for thread-safe food and player management. 
 
 ### 🎮 Gameplay Enhancements 
 - **Improved Spawn System:** Safe random spawning with collision checks and database position recovery. 
 - **Smart Bots:** Exactly 4 active bots (Bot1-Bot4) with improved boundary avoidance and hunting logic. 
 - **Food Respawn:** Fixed 400 food items on map with a precise 3-second respawn timer. 
 
 ### 🛠 Fixes & UI 
 - **Camera Fix:** Resolved the (0,0) spawn camera lock; players now center correctly on spawn. 
 - **Visual Overhaul:** New neon-grid background, glowing food items, and smooth parrot animations. 
 - **UI Stats:** Real-time counters for online players and food items on the map. 
 
 --- 
 
 ## 🚀 Key Features 
 
 ### 💬 Competition 
 - **Global Leaderboard** – top 10 players of all time. 
 - **24h Records** – compete for the best score of the day. 
 - **Dynamic Scoring** – eat food and power-ups to grow and speed up. 
 - **Bot System** – always-on AI competitors to keep the world alive. 
 
 ### 🎨 Aesthetics & UI 
 - **Parrot Customization** – random vibrant colors for every player. 
 - **Minimap** – real-time tracking of your position and other players. 
 - **Death Screen** – detailed death reasons and final score reporting. 
 - **Responsiveness** – full-screen canvas that adapts to any resolution. 
 
 --- 
 
 ## 💻 System Requirements 
 
 ### Server (Host) 
 - **OS:** Windows 10 (1809+) or Windows 11. 
 - **Runtime:** .NET 11 Runtime (Desktop). 
 - **Disk Space:** ~50MB for application + database. 
 
 ### Client (Web) 
 - **Browser:** Modern browser (Chrome, Firefox, Edge, Safari). 
 - **Protocol:** Support for standard WebSockets. 
 
 --- 
 
 ## 📥 Running the Game 
 
 1. **Launch Server:** Run `SquawkServer.exe` and click **"Włącz Serwer"**. 
 2. **Access Client:** Open `index.html` in your browser (served by the host). 
 3. **Login:** Enter your parrot name and password to start your journey. 
 4. **Grow:** Eat food, avoid other parrots, and reach the top of the leaderboard! 
 
 --- 
 
 ## 🛠 Technology Stack 
 
 - **Backend:** .NET 11.0, WatsonWebsocket, Dapper, SQLite. 
 - **Frontend:** Vanilla JavaScript, Canvas API, CSS3. 
 - **Tools:** 
   - `build.ps1` – automated build and cleanup script. 
   - `SquawkTests` – XUnit-based unit and integration testing suite. 
 
 --- 
 
 ## 📂 Project Structure 
 
 ```text 
 Squawk/ 
 ├─ server/                  # Backend C# (WinForms + WebSocket) 
 │  ├─ Models/               # Data models (Player, Food, User) 
 │  ├─ SquawkTests/          # Test suite 
 │  ├─ GameEngine.cs         # Core game logic & physics 
 │  └─ WebSocketServer.cs    # Network communication 
 ├─ client/                  # Frontend (JS, HTML, Assets) 
 │  ├─ ico/                  # App icons 
 │  ├─ img/                  # UI images 
 │  ├─ game.js               # Client-side engine 
 │  └─ index.html            # Main entry point 
 └─ build.ps1                # Build automation script 
 ``` 
 
 --- 
 
 ## 👑 Development Team 
 
 The project is developed by: 
 
 - 👨‍💻 Adam Hnatko ("Hnato") 
 - 🛠 ThomasWack 
 
 --- 
 
 <div align="center"> 
 &copy; 2026 Squawk Project. 
 </div>