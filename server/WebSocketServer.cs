using System.Text.Json;
using WatsonWebsocket;
using SquawkServer.Models;
using System.Net;
using System.IO;
using System.Collections.Concurrent;


namespace SquawkServer;

public class WebSocketServer
{
    private WatsonWsServer? _server;
    private readonly IGameEngine _engine;
    private readonly DatabaseManager _db;
    private readonly System.Timers.Timer _updateTimer;
    private readonly string _ip;
    private readonly int _port;
    private HttpListener? _httpListener;
    private bool _isRunning = false;
    private readonly System.Threading.Lock _serverLock = new();
    private readonly ConcurrentDictionary<Guid, string> _authenticatedUsers = [];
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = null };

    public event Action<string>? OnLog;

    public WebSocketServer(IGameEngine engine, DatabaseManager db, string ip = "127.0.0.1", int port = 5005)
    {
        _engine = engine;
        _db = db;
        _ip = ip;
        _port = port;
        
        _updateTimer = new System.Timers.Timer(50); // 20 FPS updates
        _updateTimer.Elapsed += (s, e) => BroadcastState();

        _engine.OnPlayerSpawned += (player) => {
            var client = _server?.ListClients().FirstOrDefault(c => c.IpPort == player.Id);
            if (client != null)
            {
                Send(client.Guid, new WsMessage { 
                    Type = "playerSpawned", 
                    Data = JsonSerializer.SerializeToElement(new { 
                        id = player.Id, 
                        x = player.Body[0].X, 
                        y = player.Body[0].Y 
                    }, _jsonOptions) 
                });
            }
        };

        _engine.OnPlayerDeath += (player, reason) => {
            if (!player.IsBot)
            {
                // Save state on death (reset to center with 0 score)
                var clientGuid = _authenticatedUsers.FirstOrDefault(x => x.Value == player.Name).Key;
                if (clientGuid != default)
                {
                    _db.SavePlayerState(player.Name, 1500f, 1500f, 0);
                }

                // Find client Guid for this player
                var client = _server?.ListClients().FirstOrDefault(c => c.IpPort == player.Id);
                if (client != null)
                {
                    Send(client.Guid, new WsMessage { 
                        Type = "death", 
                        Data = JsonSerializer.SerializeToElement(new { reason }, _jsonOptions) 
                    });
                }
            }
            OnLog?.Invoke($"Player {player.Name} died: {reason}");
        };
    }

    public void Start()
    {
        lock (_serverLock)
        {
            if (_isRunning) return;

            try 
            {
                _isRunning = true;

                // --- 1. WebSocket Server (WatsonWsServer) ---
                int wsPort = _port;
                bool wsStarted = false;
                Exception? lastWsEx = null;

                // Try up to 50 ports starting from _port
                for (int i = 0; i < 50; i++)
                {
                    try 
                    {
                        string host = "0.0.0.0";
                        try 
                        {
                            _server = new WatsonWsServer(host, wsPort, false);
                            _server.ClientConnected += (s, e) => OnLog?.Invoke($"Client connected: {e.Client.IpPort}");
                            _server.ClientDisconnected += (s, e) => {
                                // Save state on disconnect
                                if (_authenticatedUsers.TryRemove(e.Client.Guid, out var username))
                                {
                                    var player = _engine.Players.FirstOrDefault(p => p.Id == e.Client.IpPort);
                                    if (player != null && player.Body.Count > 0)
                                    {
                                        var head = player.Body[0];
                                        _db.SavePlayerState(username, head.X, head.Y, player.Score);
                                    }
                                }
                                _engine.RemovePlayer(e.Client.IpPort);
                                OnLog?.Invoke($"Client disconnected: {e.Client.IpPort}");
                            };
                            _server.MessageReceived += OnMessageReceived;
                            _server.Start();
                            wsStarted = true;
                            OnLog?.Invoke($"WebSocket server started on {host}:{wsPort}");
                            break;
                        }
                        catch (Exception ex) when (ex.Message.Contains("nie jest obsługiwane") || ex.Message.Contains("not supported") || ex.Message.Contains("Access is denied"))
                        {
                            // Fallback to localhost if 0.0.0.0 fails due to permissions
                            host = "localhost";
                            OnLog?.Invoke($"WS 0.0.0.0 failed, trying {host}:{wsPort}...");
                            _server = new WatsonWsServer(host, wsPort, false);
                            _server.ClientConnected += (s, e) => OnLog?.Invoke($"Client connected: {e.Client.IpPort}");
                            _server.ClientDisconnected += (s, e) => {
                                _engine.RemovePlayer(e.Client.IpPort);
                                OnLog?.Invoke($"Client disconnected: {e.Client.IpPort}");
                            };
                            _server.MessageReceived += OnMessageReceived;
                            _server.Start();
                            wsStarted = true;
                            OnLog?.Invoke($"WebSocket server started on {host}:{wsPort}");
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        lastWsEx = ex;
                        OnLog?.Invoke($"WS attempt {i+1} (port {wsPort}) failed: {ex.Message}");
                        wsPort++; // Try next port
                    }
                }

                if (!wsStarted) throw new Exception($"WebSocket failure: {lastWsEx?.Message}", lastWsEx);

                // --- 2. HTTP Web Server (HttpListener) ---
                _httpListener = new HttpListener();
                int webPort = wsPort + 1;
                bool webStarted = false;
                Exception? lastWebEx = null;

                for (int i = 0; i < 50; i++)
                {
                    try 
                    {
                        _httpListener.Prefixes.Clear();
                        try 
                        {
                            // Try global wildcard first
                            _httpListener.Prefixes.Add($"http://*:{webPort}/");
                            _httpListener.Start();
                            webStarted = true;
                            OnLog?.Invoke($"Web server started on *:{webPort}");
                            break;
                        }
                        catch (Exception ex) when (ex.Message.Contains("nie jest obsługiwane") || ex.Message.Contains("not supported") || ex.Message.Contains("Access is denied"))
                        {
                            // Fallback to localhost wildcard or specific loopback
                            OnLog?.Invoke($"Web global wildcard failed, trying localhost:{webPort}...");
                            _httpListener.Prefixes.Clear();
                            _httpListener.Prefixes.Add($"http://localhost:{webPort}/");
                            _httpListener.Start();
                            webStarted = true;
                            OnLog?.Invoke($"Web server started on localhost:{webPort}");
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        lastWebEx = ex;
                        OnLog?.Invoke($"Web attempt {i+1} (port {webPort}) failed: {ex.Message}");
                        webPort++;
                    }
                }

                if (!webStarted) throw new Exception($"Web server failure: {lastWebEx?.Message}", lastWebEx);

                Task.Run(HandleHttpRequests);
                _updateTimer.Start();
            }
            catch (Exception ex)
            {
                _isRunning = false;
                OnLog?.Invoke($"FATAL ERROR: {ex.Message}");
                if (ex.InnerException != null) OnLog?.Invoke($"Details: {ex.InnerException.Message}");
                Stop();
                throw;
            }
        }
    }

    public void Stop()
    {
        lock (_serverLock)
        {
            if (!_isRunning) return;
            _isRunning = false;
            
            _updateTimer.Stop();
            
            try 
            {
                if (_server != null)
                {
                    if (_server.IsListening) _server.Stop();
                    _server = null;
                }
            }
            catch { }

            try 
            {
                if (_httpListener != null)
                {
                    // Do NOT check IsListening here as it might throw ObjectDisposedException
                    _httpListener.Stop();
                    _httpListener.Close();
                    _httpListener = null;
                }
            }
            catch { }

            OnLog?.Invoke("Servers stopped.");
        }
    }

    private async Task HandleHttpRequests()
    {
        while (_isRunning)
        {
            HttpListener? listener;
            lock (_serverLock)
            {
                listener = _httpListener;
            }

            if (listener == null) break;

            try
            {
                var context = await listener.GetContextAsync();
                _ = Task.Run(() => ProcessHttpRequest(context));
            }
            catch (HttpListenerException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (Exception ex)
            {
                if (_isRunning) OnLog?.Invoke($"HTTP Loop Error: {ex.Message}");
                break;
            }
        }
    }

    private void ProcessHttpRequest(HttpListenerContext context)
    {
        try
        {
            // Only allow GET method for serving static files
            if (context.Request.HttpMethod != "GET")
            {
                context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                context.Response.StatusDescription = "Method Not Allowed - Squawk server only supports GET for static assets";
                return;
            }

            string urlPath = context.Request.Url?.LocalPath ?? "/";
            if (urlPath == "/") urlPath = "/index.html";
            
            // Format resource name: SquawkServer.client.subfolders.filename
            // Replace / with . for resource path, but keep the original extension
            string resourcePath = "SquawkServer.client" + urlPath.Replace('/', '.');
            
            using var stream = GetType().Assembly.GetManifestResourceStream(resourcePath);

            if (stream != null)
            {
                byte[] buffer = new byte[stream.Length];
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                
                context.Response.ContentLength64 = bytesRead;
                context.Response.ContentType = GetContentType(urlPath);
                context.Response.OutputStream.Write(buffer, 0, bytesRead);
            }
            else
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                context.Response.StatusDescription = "Resource not found in embedded assets";
            }
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            OnLog?.Invoke($"Web Server Error: {ex.Message}");
        }
        finally
        {
            try { context.Response.Close(); } catch { }
        }
    }

    private string GetContentType(string filePath)
    {
        string ext = Path.GetExtension(filePath).ToLower();
        return ext switch
        {
            ".html" => "text/html",
            ".js" => "application/javascript",
            ".css" => "text/css",
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".gif" => "image/gif",
            ".ico" => "image/x-icon",
            ".json" => "application/json",
            _ => "application/octet-stream"
        };
    }

    private void OnMessageReceived(object? sender, MessageReceivedEventArgs e)
    {
        try
        {
            var json = System.Text.Encoding.UTF8.GetString(e.Data);
            var msg = JsonSerializer.Deserialize<WsMessage>(json);
            if (msg == null) return;

            switch (msg.Type)
            {
                case "auth":
                    HandleAuth(e.Client.Guid, msg.Data);
                    break;
                case "join":
                    HandleJoin(e.Client.Guid, e.Client.IpPort, msg.Data);
                    break;
                case "move":
                    HandleMove(e.Client.IpPort, msg.Data);
                    break;
            }
        }
        catch (Exception ex)
        {
            OnLog?.Invoke($"Error processing message: {ex.Message}");
        }
    }

    private void HandleAuth(Guid clientGuid, JsonElement data)
    {
        string username = "";
        string password = "";
        bool isRegister = false;

        try 
        {
            username = data.GetProperty("username").GetString() ?? "";
            password = data.GetProperty("password").GetString() ?? "";
            isRegister = data.GetProperty("register").GetBoolean();
        }
        catch (Exception)
        {
            Send(clientGuid, new WsMessage { Type = "auth_response", Data = JsonSerializer.SerializeToElement(new { success = false, message = "Błędne dane wejściowe" }, _jsonOptions) });
            return;
        }

        if (string.IsNullOrWhiteSpace(username) || username.Length < 3)
        {
            Send(clientGuid, new WsMessage { Type = "auth_response", Data = JsonSerializer.SerializeToElement(new { success = false, message = "Nazwa użytkownika musi mieć min. 3 znaki" }, _jsonOptions) });
            return;
        }

        bool success = false;
        string message = "";

        if (isRegister)
        {
            success = _db.RegisterUser(username, password);
            message = success ? "Zarejestrowano pomyślnie" : "Użytkownik już istnieje lub błąd bazy";
        }
        else
        {
            var user = _db.AuthenticateUser(username, password);
            success = user != null;
            message = success ? "Zalogowano" : "Błędne hasło lub użytkownik nie istnieje";
        }

        if (success)
        {
            _authenticatedUsers[clientGuid] = username;
        }

        Send(clientGuid, new WsMessage { Type = "auth_response", Data = JsonSerializer.SerializeToElement(new { success, message }, _jsonOptions) });
    }

    private void HandleJoin(Guid clientGuid, string ipPort, JsonElement data)
    {
        if (_authenticatedUsers.TryGetValue(clientGuid, out var username))
        {
            var (x, y, score) = _db.GetPlayerState(username);
            _engine.AddPlayer(ipPort, username, x, y, score);
            // Consistently use uppercase Id to match the state update
            Send(clientGuid, new WsMessage { Type = "joined", Data = JsonSerializer.SerializeToElement(new { Id = ipPort }, _jsonOptions) });
        }
        else
        {
            OnLog?.Invoke($"Unauthenticated join attempt from {ipPort}");
        }
    }

    private void HandleMove(string ipPort, JsonElement data)
    {
        if (data.TryGetProperty("angle", out var angleProp))
        {
            _engine.UpdatePlayerAngle(ipPort, (float)angleProp.GetDouble());
        }
    }

    private void BroadcastState()
    {
        if (!_engine.IsRunning || _server == null) return;

        var players = _engine.Players.Select(p => new {
            Id = p.Id,
            Name = p.Name,
            Body = p.Body.Select(b => new { X = b.X, Y = b.Y }).ToList(),
            Angle = p.Angle,
            Score = p.Score,
            Color = p.Color,
            IsBot = p.IsBot
        }).ToList();

        var food = _engine.FoodItems.Select(f => new {
            Id = f.Id,
            Position = new { X = f.Position.X, Y = f.Position.Y },
            Value = f.Value,
            Color = f.Color,
            IsPowerUp = f.IsPowerUp,
            Type = f.Type
        }).ToList();

        var state = new
        {
            players,
            food,
            leaderboard = _engine.Players.OrderByDescending(p => p.Score).Take(10).Select(p => new { Name = p.Name, Score = p.Score, Length = p.Body.Count }).ToList(),
            top10 = _db.GetTop10().Select(e => new { Name = e.Name, Score = e.Score, Length = (int)(15 + (int)e.Score / 2) }),
            records24h = _db.GetTop24h().Select(e => new { Name = e.Name, Score = e.Score, Length = (int)(15 + (int)e.Score / 2) })
        };

        var json = JsonSerializer.Serialize(new WsMessage { Type = "state", Data = JsonSerializer.SerializeToElement(state, _jsonOptions) }, _jsonOptions);
        
        foreach (var client in _server.ListClients())
        {
            _server.SendAsync(client.Guid, json);
        }
    }

    private void Send(Guid clientGuid, WsMessage msg)
    {
        _server?.SendAsync(clientGuid, JsonSerializer.Serialize(msg, _jsonOptions));
    }

    private class WsMessage
    {
        public string Type { get; set; } = "";
        public JsonElement Data { get; set; }
    }
}
