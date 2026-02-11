using Fleck;
using Newtonsoft.Json;
using Squawk.Server.Models;
using Squawk.Server.Tests;

namespace Squawk.Server
{
    class Program
    {
        private const int Port = 5004;
        private const string Host = "0.0.0.0";
        private static GameEngine _engine = new GameEngine();
        private static Dictionary<IWebSocketConnection, string> _clients = new Dictionary<IWebSocketConnection, string>();

        static void Main(string[] args)
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(@"
   _____  ____  _    _         __          ___  __ 
  / ____|/ __ \| |  | |  /\    \ \        / / |/ / 
 | (___ | |  | | |  | | /  \    \ \  /\  / /| ' /  
  \___ \| |  | | |  | |/ /\ \    \ \/  \/ / |  <   
  ____) | |__| | |__| / ____ \    \  /\  /  | . \  
 |_____/ \___\_\\____/_/    \_\    \/  \/   |_|\_\ 
                                                   
            [ MULTIPLAYER PARROT BATTLE ]
            ");
            Console.ResetColor();
            Console.WriteLine("========================================");
            Console.WriteLine("       SQUAWK SERVER INITIALIZING       ");
            Console.WriteLine("========================================");

            var server = new WebSocketServer($"ws://{Host}:{Port}");
            
            try 
            {
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
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Client connected: {playerId}");
                    };

                    socket.OnClose = () =>
                    {
                        if (_clients.TryGetValue(socket, out string? playerId) && playerId != null)
                        {
                            _engine.RemoveParrot(playerId);
                            _clients.Remove(socket);
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Client disconnected: {playerId}");
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
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Error processing message: {ex.Message}");
                        }
                    };
                });

                System.Threading.Thread.Sleep(500);
                SocketValidator.ValidateOnlyTargetPort(Port);

                Console.WriteLine($"\n[{DateTime.Now:HH:mm:ss}] WebSocket Server ACTIVE on:");
                Console.WriteLine($" -> ws://{Host}:{Port}");
                Console.WriteLine($" -> ws://localhost:{Port}");
                Console.WriteLine("========================================");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CRITICAL ERROR: Could not start server: {ex.Message}");
                return;
            }

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

                var state = _engine.GetState();
                var stateJson = JsonConvert.SerializeObject(state);
                
                string? leaderboardJson = null;
                if (frameCount % 30 == 0)
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

                Thread.Sleep(33);
            }
        }
    }
}
