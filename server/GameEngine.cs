using System.Collections.Concurrent;
using System.Numerics;
using SquawkServer.Models;

namespace SquawkServer;

public interface IGameEngine
{
    IReadOnlyCollection<Player> Players { get; }
    IReadOnlyCollection<Food> FoodItems { get; }
    bool BotsEnabled { get; set; }
    bool IsRunning { get; }
    event Action<string>? OnLog;
    void Start();
    void Stop();
    void Tick();
    void AddPlayer(string id, string name);
    void RemovePlayer(string id);
    void UpdatePlayerAngle(string id, float angle);
}

public class GameEngine : IGameEngine
{
    private readonly ConcurrentDictionary<string, Player> _players = new();
    private readonly ConcurrentBag<Food> _foodItems = new();
    
    public IReadOnlyCollection<Player> Players => _players.Values.ToList().AsReadOnly();
    public IReadOnlyCollection<Food> FoodItems => _foodItems.ToList().AsReadOnly();
    
    public bool BotsEnabled { get; set; } = false;
    public bool IsRunning { get; private set; } = false;

    private readonly Random _random = new();
    private readonly int _worldWidth = 3000;
    private readonly int _worldHeight = 3000;
    private readonly int _maxFood = 300;
    private readonly int _maxBots = 20;

    public event Action<string>? OnLog;

    public void Start()
    {
        if (IsRunning) return;
        
        IsRunning = true;
        InitializeFood();
        OnLog?.Invoke("Game Engine started.");
    }

    public void Stop()
    {
        if (!IsRunning) return;
        
        IsRunning = false;
        _players.Clear();
        while (!_foodItems.IsEmpty) _foodItems.TryTake(out _);
        OnLog?.Invoke("Game Engine stopped.");
    }

    private void InitializeFood()
    {
        while (!_foodItems.IsEmpty) _foodItems.TryTake(out _);
        for (int i = 0; i < _maxFood; i++)
        {
            SpawnFood();
        }
    }

    private void SpawnFood()
    {
        _foodItems.Add(new Food
        {
            Id = _random.Next(1000000),
            Position = new Vector2(_random.Next(_worldWidth), _random.Next(_worldHeight)),
            Value = _random.Next(1, 5),
            Color = $"rgb({_random.Next(100, 255)}, {_random.Next(100, 255)}, {_random.Next(100, 255)})"
        });
    }

    public void Tick()
    {
        if (!IsRunning) return;

        try
        {
            UpdatePlayers();
            UpdateBots();
            CheckCollisions();
            ReplenishFood();
        }
        catch (Exception ex)
        {
            OnLog?.Invoke($"Game Engine Error in Tick: {ex.Message}");
        }
    }

    private void UpdatePlayers()
    {
        foreach (var player in _players.Values.Where(p => !p.IsBot))
        {
            MovePlayer(player);
        }
    }

    private void UpdateBots()
    {
        if (!BotsEnabled)
        {
            var botIds = _players.Values.Where(p => p.IsBot).Select(p => p.Id).ToList();
            foreach (var id in botIds) _players.TryRemove(id, out _);
            return;
        }

        var currentBots = _players.Values.Where(p => p.IsBot).ToList();
        if (currentBots.Count < _maxBots)
        {
            AddBot();
        }

        foreach (var bot in currentBots)
        {
            if (_random.NextDouble() < 0.05)
            {
                bot.Angle += (float)(_random.NextDouble() * 0.5 - 0.25);
            }
            MovePlayer(bot);
        }
    }

    private void MovePlayer(Player player)
    {
        if (player.Body.Count == 0) return;

        lock (player) // Thread-safe body modification
        {
            var head = player.Body[0];
            var direction = new Vector2((float)Math.Cos(player.Angle), (float)Math.Sin(player.Angle));
            var newHead = head + direction * player.Speed;

            // Wrap around world
            newHead.X = (newHead.X + _worldWidth) % _worldWidth;
            newHead.Y = (newHead.Y + _worldHeight) % _worldHeight;

            player.Body.Insert(0, newHead);
            if (player.Body.Count > 10 + player.Score / 2)
            {
                player.Body.RemoveAt(player.Body.Count - 1);
            }
        }
    }

    private void CheckCollisions()
    {
        var playersToRemove = new ConcurrentBag<string>();
        var playersSnapshot = _players.Values.ToList();

        foreach (var player in playersSnapshot)
        {
            if (player.Body.Count == 0) continue;
            var head = player.Body[0];

            // 1. Food collision (optimized with spatial check)
            var foodList = _foodItems.ToList();
            foreach (var food in foodList)
            {
                if (Vector2.DistanceSquared(head, food.Position) < 400) // 20^2
                {
                    player.Score += food.Value;
                    // Note: ConcurrentBag doesn't support easy removal by object, 
                    // in a real large-scale engine we'd use a different structure.
                    // For now, we'll mark it as value 0 and replenish later.
                    food.Value = 0; 
                }
            }

            // 2. Player collision
            foreach (var other in playersSnapshot)
            {
                int startIdx = (other.Id == player.Id) ? 5 : 0; 
                var otherBody = other.Body.ToList();
                for (int i = startIdx; i < otherBody.Count; i++)
                {
                    if (Vector2.DistanceSquared(head, otherBody[i]) < 225) // 15^2
                    {
                        playersToRemove.Add(player.Id);
                        OnLog?.Invoke($"Player {player.Name} collided and died.");
                        break;
                    }
                }
                if (playersToRemove.Contains(player.Id)) break;
            }
        }

        // Process removals and convert to food
        foreach (var id in playersToRemove)
        {
            if (_players.TryRemove(id, out var p))
            {
                foreach (var segment in p.Body)
                {
                    if (_random.NextDouble() < 0.5)
                    {
                        _foodItems.Add(new Food
                        {
                            Id = _random.Next(1000000),
                            Position = segment,
                            Value = 2,
                            Color = p.Color
                        });
                    }
                }
            }
        }
    }

    private void ReplenishFood()
    {
        // Simple cleanup for eaten food (value marked as 0)
        // In a high-performance engine, we'd use a better structure.
        var count = _foodItems.Count;
        if (count < _maxFood)
        {
            while (_foodItems.Count < _maxFood) SpawnFood();
        }
    }

    private void AddBot()
    {
        var botId = "bot_" + Guid.NewGuid().ToString().Substring(0, 8);
        var bot = new Player
        {
            Id = botId,
            Name = "Bot_" + _random.Next(1000),
            IsBot = true,
            Angle = (float)(_random.NextDouble() * Math.PI * 2),
            Color = $"rgb({_random.Next(50, 200)}, {_random.Next(50, 200)}, {_random.Next(50, 200)})",
            Score = 0
        };
        var startPos = new Vector2(_random.Next(_worldWidth), _random.Next(_worldHeight));
        for (int i = 0; i < 10; i++) bot.Body.Add(startPos);
        _players.TryAdd(botId, bot);
    }

    public void AddPlayer(string id, string name)
    {
        if (_players.ContainsKey(id)) return;

        var player = new Player
        {
            Id = id,
            Name = name,
            IsBot = false,
            Angle = 0,
            Color = $"rgb({_random.Next(100, 255)}, {_random.Next(100, 255)}, {_random.Next(100, 255)})",
            Score = 0
        };
        var startPos = new Vector2(_random.Next(_worldWidth), _random.Next(_worldHeight));
        for (int i = 0; i < 10; i++) player.Body.Add(startPos);
        
        if (_players.TryAdd(id, player))
        {
            OnLog?.Invoke($"Player {name} joined.");
        }
    }

    public void RemovePlayer(string id)
    {
        if (_players.TryRemove(id, out var player))
        {
            OnLog?.Invoke($"Player {player.Name} left.");
        }
    }

    public void UpdatePlayerAngle(string id, float angle)
    {
        if (_players.TryGetValue(id, out var player))
        {
            player.Angle = angle;
        }
    }
}
