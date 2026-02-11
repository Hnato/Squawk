using System.IO;
using System.Net;
using System.Text;
using Fleck;
using Newtonsoft.Json;
using Squawk.Server.Models;

namespace Squawk.Server
{
    class Program
    {
        private static GameEngine _engine = new GameEngine();
        private static Dictionary<IWebSocketConnection, string> _clients = new Dictionary<IWebSocketConnection, string>();
        private static string _clientPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Client");

        static void Main(string[] args)
        {
            // Fallback for client path if running from root
            if (!Directory.Exists(_clientPath))
            {
                _clientPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "Client");
                if (!Directory.Exists(_clientPath))
                {
                    _clientPath = Path.Combine(Directory.GetCurrentDirectory(), "Client");
                }
            }

            Console.WriteLine($"Client path: {_clientPath}");

            // Start WebSocket Server
            var server = new WebSocketServer("ws://0.0.0.0:5004");
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
                    Console.WriteLine($"Client connected: {playerId}");
                };

                socket.OnClose = () =>
                {
                    if (_clients.TryGetValue(socket, out string playerId))
                    {
                        _engine.RemoveParrot(playerId);
                        _clients.Remove(socket);
                        Console.WriteLine($"Client disconnected: {playerId}");
                    }
                };

                socket.OnMessage = message =>
                {
                    try
                    {
                        var baseMsg = JsonConvert.DeserializeObject<BaseMessage>(message);
                        if (baseMsg != null && _clients.TryGetValue(socket, out string? playerId) && playerId != null)
                        {
                            switch (baseMsg.Type)
                            {
                                case MessageType.Join:
                                    var joinMsg = JsonConvert.DeserializeObject<JoinMessage>(message);
                                    if (joinMsg != null && joinMsg.Name != null)
                                    {
                                        _engine.AddParrot(playerId, joinMsg.Name);
                                    }
                                    break;
                                case MessageType.Input:
                                    var inputMsg = JsonConvert.DeserializeObject<InputMessage>(message);
                                    if (inputMsg != null)
                                    {
                                        _engine.UpdateInput(playerId, inputMsg.TargetX, inputMsg.TargetY, inputMsg.IsBoosting);
                                    }
                                    break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error processing message: " + ex.Message);
                    }
                };
            });

            Console.WriteLine("WebSocket Server started on ws://0.0.0.0:5004");
            Console.WriteLine("HTTP Server is DISABLED. Use a separate web server for the Client/ folder.");
            // Console.WriteLine("OPEN http://localhost:12345 TO PLAY!");

            // Game Loop
            DateTime lastTick = DateTime.Now;
            int frameCount = 0;
            while (true)
            {
                DateTime now = DateTime.Now;
                float deltaTime = (float)(now - lastTick).TotalSeconds;
                lastTick = now;
                frameCount++;

                _engine.Update(deltaTime);
                _engine.CleanupDead();

                // Broadcast state
                var state = _engine.GetState();
                var stateJson = JsonConvert.SerializeObject(state);
                
                string? leaderboardJson = null;
                if (frameCount % 30 == 0) // Every ~1 second
                {
                    var leaderboard = _engine.GetLeaderboard();
                    leaderboardJson = JsonConvert.SerializeObject(leaderboard);
                }

                var deathMsgJson = JsonConvert.SerializeObject(new BaseMessage { Type = MessageType.Death });

                foreach (var client in _clients.Keys.ToList())
                {
                    try
                    {
                        if (client != null && client.IsAvailable && _clients.TryGetValue(client, out string? playerId))
                        {
                            client.Send(stateJson);
                            if (leaderboardJson != null) client.Send(leaderboardJson);

                            // Check if this player just died
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
                    catch (Exception)
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

                Thread.Sleep(33); // ~30 FPS
            }
        }

        static void StartHttpServer(int port)
        {
            var listener = new HttpListener();
            listener.Prefixes.Add($"http://*:{port}/");
            try
            {
                listener.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not start HTTP server on port {port}: {ex.Message}");
                Console.WriteLine("Make sure to run as Administrator or use a different port.");
                return;
            }

            Task.Run(() =>
            {
                while (listener.IsListening)
                {
                    try
                    {
                        var context = listener.GetContext();
                        ProcessRequest(context);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"HTTP Request error: {ex.Message}");
                    }
                }
            });
        }

        static void ProcessRequest(HttpListenerContext context)
        {
            if (context.Request.Url == null) return;
            string filename = context.Request.Url.AbsolutePath.Substring(1);
            if (string.IsNullOrEmpty(filename)) filename = "index.html";

            string filePath = Path.Combine(_clientPath, filename);

            if (File.Exists(filePath))
            {
                try
                {
                    byte[] content = File.ReadAllBytes(filePath);
                    context.Response.ContentType = GetContentType(filePath);
                    context.Response.ContentLength64 = content.Length;
                    context.Response.OutputStream.Write(content, 0, content.Length);
                }
                catch (Exception ex)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    Console.WriteLine($"Error serving {filename}: {ex.Message}");
                }
            }
            else
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                byte[] errorMsg = Encoding.UTF8.GetBytes("404 - Not Found");
                context.Response.OutputStream.Write(errorMsg, 0, errorMsg.Length);
            }
            context.Response.OutputStream.Close();
        }

        static string GetContentType(string path)
        {
            string ext = Path.GetExtension(path).ToLower();
            return ext switch
            {
                ".html" => "text/html",
                ".js" => "application/javascript",
                ".css" => "text/css",
                ".png" => "image/png",
                ".ico" => "image/x-icon",
                _ => "application/octet-stream"
            };
        }
    }
}
