using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Fleck;
using Newtonsoft.Json;
using Squawk.Server.Models;

namespace Squawk.Server
{
    class Program
    {
        private static GameEngine _engine = new GameEngine();
        private static Dictionary<IWebSocketConnection, string> _clients = new Dictionary<IWebSocketConnection, string>();

        static void Main(string[] args)
        {
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
                        MapWidth = GameEngine.MapWidth,
                        MapHeight = GameEngine.MapHeight
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
                        if (_clients.TryGetValue(socket, out string playerId))
                        {
                            switch (baseMsg.Type)
                            {
                                case MessageType.Join:
                                    var joinMsg = JsonConvert.DeserializeObject<JoinMessage>(message);
                                    _engine.AddParrot(playerId, joinMsg.Name);
                                    break;
                                case MessageType.Input:
                                    var inputMsg = JsonConvert.DeserializeObject<InputMessage>(message);
                                    _engine.UpdateInput(playerId, inputMsg.TargetX, inputMsg.TargetY, inputMsg.IsBoosting);
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

            Console.WriteLine("Server started on ws://0.0.0.0:5004");

            // Game Loop
            DateTime lastTick = DateTime.Now;
            while (true)
            {
                DateTime now = DateTime.Now;
                float deltaTime = (float)(now - lastTick).TotalSeconds;
                lastTick = now;

                _engine.Update(deltaTime);
                _engine.CleanupDead();

                // Broadcast state
                var state = _engine.GetState();
                var stateJson = JsonConvert.SerializeObject(state);
                var leaderboard = _engine.GetLeaderboard();
                var leaderboardJson = JsonConvert.SerializeObject(leaderboard);

                foreach (var client in _clients.Keys.ToList())
                {
                    if (client.IsAvailable)
                    {
                        client.Send(stateJson);
                        client.Send(leaderboardJson);
                    }
                }

                Thread.Sleep(33); // ~30 FPS
            }
        }
    }
}
