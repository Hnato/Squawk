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
    event Action<Player, string>? OnPlayerDeath;
    void Start();
    void Stop();
    void Tick();
    void AddPlayer(string id, string name, float? startX = null, float? startY = null, int initialScore = 0);
    void RemovePlayer(string id);
    void UpdatePlayerAngle(string id, float angle);
}

public class GameEngine : IGameEngine
{
    private readonly ConcurrentDictionary<string, Player> _players = new();
    private List<Food> _foodItems = new();
    private readonly object _foodLock = new();
    
    public IReadOnlyCollection<Player> Players => _players.Values.ToList().AsReadOnly();
    public IReadOnlyCollection<Food> FoodItems 
    {
        get 
        {
            lock (_foodLock) return _foodItems.ToList().AsReadOnly();
        }
    }
    
    public bool BotsEnabled { get; set; } = true;
    public bool IsRunning { get; private set; } = false;

    private readonly Random _random = new();
    private readonly float _mapRadius = 1500f;
    private readonly int _maxFood = 300;
    private readonly int _maxBots = 10; // Reduced for testing as requested

    public event Action<string>? OnLog;
    public event Action<Player, string>? OnPlayerDeath;

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
        lock (_foodLock) _foodItems.Clear();
        OnLog?.Invoke("Game Engine stopped.");
    }

    private void InitializeFood()
    {
        lock (_foodLock)
        {
            _foodItems.Clear();
            for (int i = 0; i < _maxFood; i++)
            {
                SpawnFood();
            }
        }
    }

    private void SpawnFood()
    {
        // Must be called within _foodLock
        float angle = (float)(_random.NextDouble() * Math.PI * 2);
        float dist = (float)(_random.NextDouble() * (_mapRadius - 50));
        
        bool isPowerUp = _random.NextDouble() < 0.05; // 5% chance
        string type = isPowerUp ? (_random.NextDouble() < 0.5 ? "speed" : "score") : "food";
        string color = isPowerUp ? (type == "speed" ? "cyan" : "gold") : $"rgb({_random.Next(100, 255)}, {_random.Next(100, 255)}, {_random.Next(100, 255)})";
        
        _foodItems.Add(new Food
        {
            Id = _random.Next(1000000),
            Position = new Vector2(
                _mapRadius + (float)Math.Cos(angle) * dist,
                _mapRadius + (float)Math.Sin(angle) * dist
            ),
            Value = isPowerUp ? 10 : _random.Next(1, 5),
            Color = color,
            IsPowerUp = isPowerUp,
            Type = type
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
            // Advanced AI: Avoid boundaries
            var head = bot.Body[0];
            var center = new Vector2(_mapRadius, _mapRadius);
            var distToCenter = Vector2.Distance(head, center);
            
            // If near boundary (within 300 units of edge)
            if (distToCenter > _mapRadius - 300)
            {
                // Vector pointing to center
                var toCenter = center - head;
                var targetAngle = (float)Math.Atan2(toCenter.Y, toCenter.X);
                
                // Smoothly rotate towards center
                float angleDiff = targetAngle - bot.Angle;
                while (angleDiff > Math.PI) angleDiff -= (float)(Math.PI * 2);
                while (angleDiff < -Math.PI) angleDiff += (float)(Math.PI * 2);
                
                bot.Angle += angleDiff * 0.15f; // Faster turning for bots
            }
            else
            {
                // Normal random movement
                if (_random.NextDouble() < 0.05)
                {
                    bot.Angle += (float)(_random.NextDouble() * 0.5 - 0.25);
                }
            }
            
            MovePlayer(bot);
        }
    }

    private void MovePlayer(Player player)
    {
        if (player.Body.Count == 0 || player.IsDead) return;

        lock (player)
        {
            var head = player.Body[0];
            var direction = new Vector2((float)Math.Cos(player.Angle), (float)Math.Sin(player.Angle));
            var newHead = head + direction * player.Speed;

            // Boundary Check (Circle)
            var center = new Vector2(_mapRadius, _mapRadius);
            if (Vector2.Distance(newHead, center) > _mapRadius)
            {
                player.IsDead = true; 
                OnPlayerDeath?.Invoke(player, "Wyleciałeś poza mapę!");
                return;
            }

            player.Body.Insert(0, newHead);
            if (player.Body.Count > 10 + player.Score / 2)
            {
                player.Body.RemoveAt(player.Body.Count - 1);
            }
        }
    }

    private void CheckCollisions()
    {
        var playersToRemove = new List<string>();
        var playersSnapshot = _players.Values.ToList();

        foreach (var player in playersSnapshot)
        {
            if (player.Body.Count == 0 || player.IsDead) 
            {
                if (player.IsDead) playersToRemove.Add(player.Id);
                continue;
            }
            var head = player.Body[0];

            // 1. Food collision
            lock (_foodLock)
            {
                for (int i = _foodItems.Count - 1; i >= 0; i--)
                {
                    var food = _foodItems[i];
                    if (Vector2.DistanceSquared(head, food.Position) < 625) // 25^2
                    {
                        if (food.IsPowerUp)
                        {
                            if (food.Type == "speed") player.Speed += 0.2f;
                            else if (food.Type == "score") player.Score += 20;
                        }
                        
                        player.Score += food.Value;
                        _foodItems.RemoveAt(i);
                    }
                }
            }

            // 2. Player collision
            foreach (var other in playersSnapshot)
            {
                if (other.IsDead) continue;
                int startIdx = (other.Id == player.Id) ? 10 : 0; // Increased safety for self-collision
                var otherBody = other.Body.ToList();
                for (int i = startIdx; i < otherBody.Count; i++)
                {
                    if (Vector2.DistanceSquared(head, otherBody[i]) < 400) // 20^2
                    {
                        player.IsDead = true;
                        playersToRemove.Add(player.Id);
                        OnPlayerDeath?.Invoke(player, $"Zderzenie z {other.Name}!");
                        break;
                    }
                }
                if (player.IsDead) break;
            }
        }

        // Process removals
        foreach (var id in playersToRemove)
        {
            if (_players.TryRemove(id, out var deadPlayer))
            {
                lock (_foodLock)
                {
                    // Leave food behind for others to eat
                    foreach (var segment in deadPlayer.Body)
                    {
                        _foodItems.Add(new Food
                        {
                            Id = _random.Next(1000000),
                            Position = segment,
                            Value = 5,
                            Color = deadPlayer.Color,
                            IsPowerUp = false,
                            Type = "food"
                        });
                    }
                }
                OnLog?.Invoke($"Player {deadPlayer.Name} removed and converted to food.");
            }
        }
    }

    private void ReplenishFood()
    {
        lock (_foodLock)
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
            Color = GetRandomParrotColor(),
            Score = 0,
            Speed = 2.5f + (float)(_random.NextDouble() * 1.0) // Slower bots for better stability
        };
        
        Vector2 startPos = FindSafeSpawn(100); // 100 units safe distance
        
        for (int i = 0; i < 10; i++) bot.Body.Add(startPos);
        _players.TryAdd(botId, bot);
    }

    public void AddPlayer(string id, string name, float? startX = null, float? startY = null, int initialScore = 0)
    {
        if (_players.ContainsKey(id)) return;

        var player = new Player
        {
            Id = id,
            Name = name,
            IsBot = false,
            Angle = (float)(_random.NextDouble() * Math.PI * 2),
            Color = GetRandomParrotColor(),
            Score = initialScore
        };
        
        Vector2 startPos;
        if (startX.HasValue && startY.HasValue)
        {
            startPos = new Vector2(startX.Value, startY.Value);
            // Safety check: ensure loaded position is within map bounds
            if (Vector2.Distance(startPos, new Vector2(_mapRadius, _mapRadius)) > _mapRadius)
            {
                startPos = FindSafeSpawn(100f);
            }
        }
        else
        {
            startPos = FindSafeSpawn(100f);
        }
        
        // Initial body segments
        for (int i = 0; i < 10 + initialScore / 2; i++) player.Body.Add(startPos);
        
        if (_players.TryAdd(id, player))
        {
            OnLog?.Invoke($"Player {name} spawned at {startPos.X:F1}, {startPos.Y:F1} with score {initialScore}");
        }
    }

    private string GetRandomParrotColor()
    {
        string[] colors = {
            "#FF5733", "#33FF57", "#3357FF", "#FF33A1", "#A133FF",
            "#33FFF5", "#FF8C33", "#8CFF33", "#338CFF", "#FF3333",
            "#33FF33", "#3333FF", "#FFFF33", "#FF33FF", "#33FFFF"
        };
        return colors[_random.Next(colors.Length)];
    }

    private Vector2 FindSafeSpawn(float safeDist)
    {
        for (int attempt = 0; attempt < 50; attempt++)
        {
            float angle = (float)(_random.NextDouble() * Math.PI * 2);
            // Ensure spawn is WELL WITHIN the map radius (not near the boundary line)
            float dist = (float)(_random.NextDouble() * (_mapRadius * 0.8f)); 
            
            var pos = new Vector2(
                _mapRadius + (float)Math.Cos(angle) * dist,
                _mapRadius + (float)Math.Sin(angle) * dist
            );

            bool safe = true;
            foreach (var p in _players.Values)
            {
                if (p.Body.Count > 0 && Vector2.Distance(pos, p.Body[0]) < safeDist)
                {
                    safe = false;
                    break;
                }
            }

            if (safe) return pos;
        }

        // Final fallback within the circle
        return new Vector2(_mapRadius, _mapRadius); 
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
