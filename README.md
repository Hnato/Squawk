# Squawk Multiplayer Parrot Battle - Server & Control Panel

A high-performance multiplayer game server and aesthetic control panel for the **Squawk** game. This project provides a robust backend for parrot battles, featuring real-time synchronization, bot AI, and a modern desktop management interface built with **Avalonia UI**.

## 🚀 Key Features

- **High-Performance Game Engine**: Real-time parrot movement, collisions, and energy mechanics.
- **WebSocket Communication**: Powered by `Fleck` for low-latency client-server interaction.
- **Embedded Web Server**: Serves the game client directly from the executable resources.
- **Aesthetic Control Panel**: A jungle-themed UI built with `Avalonia UI` to manage:
  - Game Engine (Start/Stop)
  - Network Services (WebSocket & HTTP)
  - Bot AI (Enable/Disable toggle)
- **Real-time Logging**: Live console output integrated into the desktop application.
- **Single-File Executable**: Fully self-contained build for easy deployment.

## 🛠 Tech Stack

- **Server Core**: .NET 10.0 (Windows-compatible)
- **GUI Framework**: Avalonia UI (Modern, Cross-platform styling)
- **Networking**: Fleck (WebSockets), TcpListener (HTTP)
- **Serialization**: Newtonsoft.Json
- **Game Client**: Phaser 3 (JavaScript)

## 📁 Project Structure

- `Server/`: The core logic, networking, and game mechanics.
- `AvaloniaPanel/`: The modern desktop interface that controls the server.
- `Client/`: Frontend game assets and Phaser 3 logic (embedded into the server).

## 🔨 Build and Run

### Prerequisites
- .NET 10 SDK

### Building the Project
To build the complete solution:
```bash
dotnet build
```

### Running the Control Panel
Navigate to the `AvaloniaPanel` directory and run:
```bash
dotnet run --project AvaloniaPanel/Squawk.AvaloniaPanel.csproj -c Debug
```

## 📜 Server Logic Details

### Networking
- **WebSocket Port**: `5004` (Game state updates)
- **HTTP Port**: `5006` (Serves the game client assets)

### Game Mechanics
The server manages the "World" state, including:
- **Parrots**: Position, direction, energy, and segment management.
- **Feathers**: Energy pickups spawned across the map.
- **Bots**: Automated AI parrots that wander, feed, and evade danger.

### Embedded Resources
The server serves the `Client/` files using an embedded resource provider. This ensures that the `SquawkServer.exe` is the only file needed to host the entire game.

## 🤝 Contributing
Developed by **Hnato.**

## 📄 License
MIT License - see the LICENSE file for details.
