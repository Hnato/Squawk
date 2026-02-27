using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Fleck;
using Newtonsoft.Json;
using Squawk.Server.Models;
using Squawk.Server.Tests;
using Squawk.Server.Gui;
using System.Windows.Forms;
using System.IO;

namespace Squawk.Server
{
    public class Program
    {
        private const int Port = 5004;
        private const int HttpPort = 5006;
        private const string Host = "0.0.0.0";
        private static readonly GameEngine _engine = new GameEngine();
        private static readonly Dictionary<IWebSocketConnection, string> _clients = new Dictionary<IWebSocketConnection, string>();
        private static WebSocketServer? _server;
        private static bool _serverRunning;
        private static bool _gameRunning;
        private static bool _networkRunning;
        private static bool _botsEnabled;
        private static Thread? _gameThread;
        private static TcpListener? _http;
        private static Thread? _httpThread;
        public static event Action<bool>? GameStateChanged;
        public static event Action<bool>? NetworkStateChanged;
        public static event Action<bool>? BotsStateChanged;
        public static bool IsGameRunning => _gameRunning;
        public static bool IsNetworkRunning => _networkRunning;
        public static bool AreBotsEnabled => _botsEnabled;

        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                SafeConsole.TryClear();
                SafeConsole.TrySetColor(ConsoleColor.Cyan);
                SafeConsole.TryWriteLine(@"
   _____  ____  _    _         __          ___  __ 
  / ____|/ __ \| |  | |  /\    \ \        / / |/ / 
 | (___ | |  | | |  | | /  \    \ \  /\  / /| ' /  
  \___ \| |  | | |  | |/ /\ \    \ \/  \/ / |  <   
  ____) | |__| | |__| / ____ \    \  /\  /  | . \  
 |_____/ \___\_\\____/_/    \_\    \/  \/   |_|\_\ 
                                                   
            [ MULTIPLAYER PARROT BATTLE ]
            ");
                SafeConsole.TryResetColor();
                Log.Info("========================================");
                Log.Info("       SQUAWK SERVER INITIALIZING       ");
                Log.Info("========================================");

                _botsEnabled = true;

                if (Environment.UserInteractive)
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new MainForm());
                }
                else
                {
                    WaitForExit();
                }
            }
            catch (Exception ex)
            {
                try
                {
                    var logPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Squawk_error.log");
                    System.IO.File.WriteAllText(logPath, ex.ToString());
                }
                catch { }
                try
                {
                    System.Windows.Forms.MessageBox.Show("Squawk nie mógł się uruchomić. Sprawdź plik Squawk_error.log.", "Squawk", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                }
                catch
                {
                }
            }
        }

        public static void StartServer()
        {
            if (_serverRunning) return;
            var server = new WebSocketServer($"ws://{Host}:{Port}");
            _server = server;
            _serverRunning = true;
            server.Start(socket =>
            {
                socket.OnOpen = () =>
                {
                    string playerId = Guid.NewGuid().ToString();
                    _clients[socket] = playerId;
                    var welcome = new WelcomeMessage
                    {
                        PlayerId = playerId,
                        MapRadius = GameEngine.MapRadius
                    };
                    socket.Send(JsonConvert.SerializeObject(welcome));
                    Log.Info($"[{DateTime.Now:HH:mm:ss}] Client connected: {playerId}");
                };

                socket.OnClose = () =>
                {
                    if (_clients.TryGetValue(socket, out string? playerId) && playerId != null)
                    {
                        _engine.RemoveParrot(playerId);
                        _clients.Remove(socket);
                        Log.Info($"[{DateTime.Now:HH:mm:ss}] Client disconnected: {playerId}");
                    }
                };

                socket.OnMessage = message =>
                {
                    try
                    {
                        var baseMsg = JsonConvert.DeserializeObject<BaseMessage>(message);
                        string? currentPlayerId;
                        if (baseMsg != null && _clients.TryGetValue(socket, out currentPlayerId) && currentPlayerId != null)
                        {
                            switch (baseMsg.Type)
                            {
                                case MessageType.Join:
                                    var joinMsg = JsonConvert.DeserializeObject<JoinMessage>(message);
                                    if (joinMsg != null && joinMsg.Name != null)
                                    {
                                        _engine.AddParrot(currentPlayerId, joinMsg.Name);
                                    }
                                    break;
                                case MessageType.Input:
                                    var inputMsg = JsonConvert.DeserializeObject<InputMessage>(message);
                                    if (inputMsg != null)
                                    {
                                        _engine.UpdateInput(currentPlayerId, inputMsg.TargetX, inputMsg.TargetY, inputMsg.IsBoosting);
                                    }
                                    break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[{DateTime.Now:HH:mm:ss}] Error processing message: {ex.Message}");
                    }
                };
            });

            Thread.Sleep(500);
            SocketValidator.ValidateOnlyTargetPort(Port);

            Log.Info($"\n[{DateTime.Now:HH:mm:ss}] WebSocket Server ACTIVE on:");
            Log.Info($" -> ws://{Host}:{Port}");
            Log.Info($" -> ws://localhost:{Port}");
            Log.Info("========================================");
        }

        public static void StopServer()
        {
            if (!_serverRunning) return;
            _serverRunning = false;
            foreach (var client in _clients.Keys.ToList())
            {
                try
                {
                    client.Close();
                }
                catch
                {
                }
            }
            _clients.Clear();
            if (_server != null)
            {
                _server.Dispose();
                _server = null;
            }
            Log.Info($"[{DateTime.Now:HH:mm:ss}] WebSocket Server stopped");
        }

        public static void StartGame()
        {
            if (_gameRunning) return;
            _gameRunning = true;
            _gameThread = new Thread(GameLoop);
            _gameThread.IsBackground = true;
            _gameThread.Start();
            Log.Info($"[{DateTime.Now:HH:mm:ss}] Game loop started");
            try { GameStateChanged?.Invoke(true); } catch { }
        }

        public static void StopGame()
        {
            if (!_gameRunning) return;
            _gameRunning = false;
            Log.Info($"[{DateTime.Now:HH:mm:ss}] Game loop stopping");
            try { GameStateChanged?.Invoke(false); } catch { }
        }

        public static void StartNetwork()
        {
            if (_networkRunning) return;
            try
            {
                StartServer();
                StartHttpInternal();
                _networkRunning = true;
                try { NetworkStateChanged?.Invoke(true); } catch { }
            }
            catch (Exception ex)
            {
                Log.Error($"Network start error: {ex.Message}");
                try { NetworkStateChanged?.Invoke(false); } catch { }
            }
        }

        public static void StopNetwork()
        {
            if (!_networkRunning) return;
            StopServer();
            StopHttpInternal();
            _networkRunning = false;
            try { NetworkStateChanged?.Invoke(false); } catch { }
        }

        public static void SetBotsEnabled(bool enabled)
        {
            _engine.SetBotsEnabled(enabled);
            _botsEnabled = enabled;
            if (enabled) Log.Info($"[{DateTime.Now:HH:mm:ss}] Bots enabled");
            else Log.Info($"[{DateTime.Now:HH:mm:ss}] Bots disabled");
            try { BotsStateChanged?.Invoke(enabled); } catch { }
        }

        private static readonly Dictionary<string, byte[]> _resourceCache = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        public static void GameLoop()
        {
            var stopwatch = Stopwatch.StartNew();
            long lastTick = 0;
            int frameCount = 0;
            
            var deathMsgJson = JsonConvert.SerializeObject(new BaseMessage { Type = MessageType.Death });
            
            while (_gameRunning)
            {
                long currentTick = stopwatch.ElapsedMilliseconds;
                float deltaTime = (currentTick - lastTick) / 1000.0f;
                lastTick = currentTick;
                frameCount++;

                _engine.Update(deltaTime);
                _engine.CleanupDead();

                var state = _engine.GetState();
                var stateJson = JsonConvert.SerializeObject(state);
                
                string? leaderboardJson = null;
                if (frameCount % 30 == 0)
                {
                    var leaderboard = _engine.GetLeaderboard();
                    leaderboardJson = JsonConvert.SerializeObject(leaderboard);
                }

                foreach (var client in _clients.Keys.ToList())
                {
                    try
                    {
                        if (client != null && client.IsAvailable && _clients.TryGetValue(client, out string? playerId))
                        {
                            client.Send(stateJson);
                            if (leaderboardJson != null) client.Send(leaderboardJson);

                            var parrot = _engine.GetParrot(playerId);
                            if (parrot != null && !parrot.IsAlive)
                            {
                                client.Send(deathMsgJson);
                                _engine.RemoveParrot(playerId);
                            }
                        }
                        else if (client != null && !client.IsAvailable)
                        {
                            if (_clients.TryGetValue(client, out string? pid))
                            {
                                _engine.RemoveParrot(pid);
                                _clients.Remove(client);
                            }
                        }
                    }
                    catch
                    {
                        if (_clients.ContainsKey(client))
                        {
                            if (_clients.TryGetValue(client, out string? pid))
                            {
                                _engine.RemoveParrot(pid);
                            }
                            _clients.Remove(client);
                        }
                    }
                }

                int elapsed = (int)(stopwatch.ElapsedMilliseconds - currentTick);
                int wait = Math.Max(1, 33 - elapsed);
                Thread.Sleep(wait);
            }
        }

        private static void StartHttpInternal()
        {
            if (_http != null) return;
            _http = new TcpListener(IPAddress.Any, HttpPort);
            _http.Start();
            Log.Info($"\n[{DateTime.Now:HH:mm:ss}] HTTP server ACTIVE on:");
            Log.Info($" -> http://0.0.0.0:{HttpPort}/");
            Log.Info($" -> http://localhost:{HttpPort}/");
            _httpThread = new Thread(HttpAcceptLoop);
            _httpThread.IsBackground = true;
            _httpThread.Start();
        }

        private static void StopHttpInternal()
        {
            try
            {
                _http?.Stop();
            }
            catch
            {
            }
            _http = null;
            try
            {
                if (_httpThread != null && _httpThread.IsAlive)
                {
                    _httpThread.Join(200);
                }
            }
            catch
            {
            }
            _httpThread = null;
            Log.Info($"[{DateTime.Now:HH:mm:ss}] HTTP server stopped");
        }

        private static void HttpAcceptLoop()
        {
            while (_http != null)
            {
                try
                {
                    var client = _http.AcceptTcpClient();
                    _ = Task.Run(() => HandleTcpClient(client));
                }
                catch
                {
                    break;
                }
            }
        }

        private static void HandleTcpClient(TcpClient client)
        {
            try
            {
                using var stream = client.GetStream();
                using var reader = new StreamReader(stream, leaveOpen: true);
                using var writer = new StreamWriter(stream, leaveOpen: true);
                writer.AutoFlush = true;
                string? requestLine = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(requestLine))
                {
                    client.Close();
                    return;
                }
                var parts = requestLine.Split(' ');
                string path = parts.Length >= 2 ? parts[1] : "/";
                if (path == "/") path = "/index.html";
                path = path.TrimStart('/');
                string? line;
                while (!string.IsNullOrEmpty(line = reader.ReadLine())) { }
                var bytes = ReadEmbeddedClientResource(path);
                if (bytes == null)
                {
                    var notFound = "HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\n\r\n";
                    var headerBytes = System.Text.Encoding.ASCII.GetBytes(notFound);
                    stream.Write(headerBytes, 0, headerBytes.Length);
                    client.Close();
                    return;
                }
                var extension = Path.GetExtension(path).ToLowerInvariant();
                var contentType = extension switch
                {
                    ".html" => "text/html; charset=utf-8",
                    ".htm" => "text/html; charset=utf-8",
                    ".js" => "application/javascript; charset=utf-8",
                    ".css" => "text/css; charset=utf-8",
                    ".png" => "image/png",
                    ".jpg" => "image/jpeg",
                    ".jpeg" => "image/jpeg",
                    ".ico" => "image/x-icon",
                    _ => "application/octet-stream"
                };
                var header = $"HTTP/1.1 200 OK\r\nContent-Type: {contentType}\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n";
                var headerBytes2 = System.Text.Encoding.ASCII.GetBytes(header);
                stream.Write(headerBytes2, 0, headerBytes2.Length);
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush();
                client.Close();
            }
            catch
            {
                try { client.Close(); } catch { }
            }
        }

        private static byte[]? ReadEmbeddedClientResource(string requestPath)
        {
            var relativePath = requestPath.TrimStart('/');
            if (string.IsNullOrWhiteSpace(relativePath)) relativePath = "index.html";

            if (_resourceCache.TryGetValue(relativePath, out var cached))
            {
                return cached;
            }

            var assembly = Assembly.GetExecutingAssembly();
            var normalizedWanted = ("Client/" + relativePath.Replace('\\', '/')).Replace('/', '.');
            Stream? stream = null;
            
            foreach (var name in assembly.GetManifestResourceNames())
            {
                if (name.EndsWith(normalizedWanted, StringComparison.OrdinalIgnoreCase))
                {
                    stream = assembly.GetManifestResourceStream(name);
                    break;
                }
            }
            
            if (stream == null)
            {
                foreach (var name in assembly.GetManifestResourceNames())
                {
                    if (name.EndsWith(relativePath, StringComparison.OrdinalIgnoreCase))
                    {
                        stream = assembly.GetManifestResourceStream(name);
                        break;
                    }
                }
            }

            if (stream == null) return null;
            
            using (var ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                var bytes = ms.ToArray();
                _resourceCache[relativePath] = bytes;
                return bytes;
            }
        }

        private static void WaitForExit()
        {
            if (SafeConsole.IsAvailable())
            {
                SafeConsole.TryWriteLine("Serwer Squawk uruchomiony. Zamknij okno aby zakończyć.");
                try
                {
                    System.Console.ReadLine();
                }
                catch
                {
                    Thread.Sleep(Timeout.Infinite);
                }
            }
            else
            {
                Thread.Sleep(Timeout.Infinite);
            }
        }

        internal static class SafeConsole
        {
            public static bool IsAvailable()
            {
                try
                {
                    return !Console.IsOutputRedirected && !Console.IsErrorRedirected && !Console.IsInputRedirected;
                }
                catch
                {
                    return false;
                }
            }
            public static void TryClear()
            {
                if (!IsAvailable()) return;
                try { Console.Clear(); } catch { }
            }
            public static void TrySetColor(ConsoleColor color)
            {
                if (!IsAvailable()) return;
                try { Console.ForegroundColor = color; } catch { }
            }
            public static void TryResetColor()
            {
                if (!IsAvailable()) return;
                try { Console.ResetColor(); } catch { }
            }
            public static void TryWriteLine(string text)
            {
                if (!IsAvailable()) return;
                try { Console.WriteLine(text); } catch { }
            }
        }

        public static class Log
        {
            private static readonly string PathLog = System.IO.Path.Combine(AppContext.BaseDirectory, "Squawk.log");
            public static event Action<string>? MessageLogged;
            private static void Write(string level, string msg)
            {
                var line = $"[{DateTime.Now:O}] {level} {msg}{Environment.NewLine}";
                try { File.AppendAllText(PathLog, line); } catch { }
                SafeConsole.TryWriteLine(msg);
                try
                {
                    MessageLogged?.Invoke(line);
                }
                catch
                {
                }
            }
            public static void Info(string msg) => Write("INFO", msg);
            public static void Error(string msg) => Write("ERROR", msg);
            public static void Subscribe(Action<string> handler)
            {
                MessageLogged += handler;
            }
        }
    }
}
