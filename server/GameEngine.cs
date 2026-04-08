using System.Collections.Concurrent;
using System.Numerics;
using SquawkServer.Models;

namespace SquawkServer;

public interface IGameEngine
{
    IReadOnlyCollection<Player> Players { get; }
    List<Food> FoodItems { get; }
    bool BotsEnabled { get; set; }
    bool IsRunning { get; }
    event Action<string>? OnLog;
    event Action<Player, string>? OnPlayerDeath;
    event Action<Player>? OnPlayerSpawned;
    void Start();
    void Stop();
    void Tick();
    void AddPlayer(string id, string name, float? startX = null, float? startY = null, int initialScore = 0);
    void RemovePlayer(string id);
    void UpdatePlayerAngle(string id, float angle);
}

public class GameEngine : IGameEngine
{
    private readonly ConcurrentDictionary<string, Player> _players = [];
    private List<Food> _foodItems = [];
    private readonly System.Threading.Lock _foodLock = new();
    
    public IReadOnlyCollection<Player> Players => _players.Values.ToList().AsReadOnly();
    public ConcurrentDictionary<string, Player> InternalPlayerMap => _players; // For tests only
    public List<Food> InternalFoodList => _foodItems; // For tests only
    public System.Threading.Lock InternalFoodLock => _foodLock; // For tests only
    public ConcurrentDictionary<int, DateTime> InternalRespawnQueue => _respawnQueue; // For tests only
    public List<Food> FoodItems 
    {
        get 
        {
            lock (_foodLock) return _foodItems.ToList();
        }
    }
    
    public bool BotsEnabled { get; set; } = true;
    public bool IsRunning { get; private set; } = false;

    private readonly Random _random = new();
    private readonly float _mapRadius = 1950f;
    private readonly int _maxFood = 9000; // Fixed 9000 pieces of food
    private readonly int _maxBots = 12; // Exactly 12 bots
    private readonly ConcurrentDictionary<int, DateTime> _respawnQueue = new();

    public event Action<string>? OnLog;
    public event Action<Player, string>? OnPlayerDeath;
    public event Action<Player>? OnPlayerSpawned;

    public void Start()
    {
        if (IsRunning) return;
        
        IsRunning = true;
        InitializeFood();
        OnLog?.Invoke("Game Engine started. Map radius: " + _mapRadius);

        // Ensure exactly 4 bots with unique names at start
        for (int i = 1; i <= _maxBots; i++)
        {
            AddBot("Bot" + i);
        }
    }

    public void Stop()
    {
        if (!IsRunning) return;
        
        IsRunning = false;
        _players.Clear();
        lock (_foodLock) _foodItems.Clear();
        _respawnQueue.Clear();
        OnLog?.Invoke("Game Engine stopped.");
    }

    private void InitializeFood()
    {
        lock (_foodLock)
        {
            _foodItems.Clear();
            _respawnQueue.Clear();
            if (IsRunning)
            {
                for (int i = 0; i < _maxFood; i++)
                {
                    SpawnFood();
                }
            }
        }
    }

    private void SpawnFood(Vector2? nearPos = null, float radius = 0, bool force = false, int? fixedId = null)
    {
        // Must be called within _foodLock
        if (!force && !IsRunning && nearPos == null) return; 
        if (!force && _foodItems.Count + _respawnQueue.Count >= _maxFood) return; 
        
        Vector2 position = Vector2.Zero;
        bool positionFound = false;
        int attempts = 0;

        while (!positionFound && attempts < 100) // Increased attempts from 50 to 100
        {
            attempts++;
            float angle = (float)(_random.NextDouble() * Math.PI * 2);
            float dist;

            if (nearPos.HasValue)
            {
                dist = (float)(_random.NextDouble() * radius);
                position = nearPos.Value + new Vector2(
                    (float)Math.Cos(angle) * dist,
                    (float)Math.Sin(angle) * dist
                );
            }
            else
            {
                // Ensure spawn is within the circle map
                dist = (float)(_random.NextDouble() * (_mapRadius - 50));
                position = new Vector2(
                    _mapRadius + (float)Math.Cos(angle) * dist,
                    _mapRadius + (float)Math.Sin(angle) * dist
                );
            }

            // Boundary check - ensure it's inside the map
            var center = new Vector2(_mapRadius, _mapRadius);
            if (Vector2.Distance(position, center) < _mapRadius - 20)
            {
                // Spacing check
                bool tooClose = false;
                foreach (var f in _foodItems)
                {                    if (Vector2.DistanceSquared(position, f.Position) < 225) // Reduced from 400 to 225 (15 units)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose) positionFound = true;
            }
        }

        if (!positionFound) 
        {
            // If still not found, just pick a random spot inside the circle without checking proximity
            float angle = (float)(_random.NextDouble() * Math.PI * 2);
            float dist = (float)(_random.NextDouble() * (_mapRadius - 50));
            position = new Vector2(
                _mapRadius + (float)Math.Cos(angle) * dist,
                _mapRadius + (float)Math.Sin(angle) * dist
            );
        }
        
        bool isPowerUp = _random.NextDouble() < 0.05; // 5% chance
        string type = isPowerUp ? (_random.NextDouble() < 0.5 ? "speed" : "score") : "food";
        string color = isPowerUp ? (type == "speed" ? "cyan" : "gold") : $"rgb({_random.Next(100, 255)}, {_random.Next(100, 255)}, {_random.Next(100, 255)})";
        
        _foodItems.Add(new Food
        {
            Id = fixedId ?? _random.Next(1000000),
            Position = position,
            Value = isPowerUp ? 10 : _random.Next(1, 5),
            Color = color,
            IsPowerUp = isPowerUp,
            Type = type
        });
    }

    public void Tick()
    {
        if (!IsRunning) return;

        // Maintenance: ensure exactly bots with unique names
        for (int i = 1; i <= _maxBots; i++)
        {
            string botName = "Bot" + i;
            if (!_players.Values.Any(b => b.Name == botName))
            {
                AddBot(botName);
            }
        }

        try
        {
            UpdatePlayers();
            UpdateBots();
            CheckCollisions();
            ProcessRespawnQueue();
            if (IsRunning) ReplenishFood();
        }
        catch (Exception ex)
        {
            OnLog?.Invoke($"Game Engine Error in Tick: {ex.Message}");
        }
    }

    private void ProcessRespawnQueue()
    {
        var now = DateTime.Now;
        var toRespawn = _respawnQueue.Where(x => x.Value <= now).Select(x => x.Key).ToList();
        
        foreach (var id in toRespawn)
        {
            _respawnQueue.TryRemove(id, out _);
            lock (_foodLock)
            {
                SpawnFood(force: true, fixedId: id);
            }
        }
    }

    private void UpdatePlayers()
    {
        foreach (var player in _players.Values.Where(p => !p.IsBot))
        {
            // Smoothly turn towards target angle
            player.Angle = SmoothTurn(player.Angle, player.TargetAngle, 0.1f);
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
        var allPlayers = _players.Values.ToList();

        foreach (var bot in currentBots)
        {
            var head = bot.Body[0];
            var center = new Vector2(_mapRadius, _mapRadius);
            var distToCenter = Vector2.Distance(head, center);
            
            // 1. Avoid boundaries (Highest priority)
            if (distToCenter > _mapRadius - 200)
            {
                var toCenter = center - head;
                bot.Angle = SmoothTurn(bot.Angle, (float)Math.Atan2(toCenter.Y, toCenter.X), 0.2f);
            }
            else
            {
                // 2. Collision Avoidance (High priority)
            bool avoiding = false;
            foreach (var other in allPlayers)
            {
                if (other.Id == bot.Id && other.Body.Count < 5) continue;
                
                List<Vector2> otherBody;
                lock (other)
                {
                    otherBody = other.Body.ToList();
                }
                int startIdx = (other.Id == bot.Id) ? 5 : 0;
                    
                    for (int i = startIdx; i < otherBody.Count; i += 2)
                    {
                        var distSq = Vector2.DistanceSquared(head, otherBody[i]);
                        if (distSq < 10000) // 100 units
                        {
                            // Turn away from the segment
                            var away = head - otherBody[i];
                            bot.Angle = SmoothTurn(bot.Angle, (float)Math.Atan2(away.Y, away.X), 0.3f);
                            avoiding = true;
                            break;
                        }
                    }
                    if (avoiding) break;
                }

                if (!avoiding)
                {
                    // 3. Hunt Players or Food (Simple AI)
                    Food? nearestFood = null;
                    float minDistSq = float.MaxValue;
                    lock (_foodLock)
                    {
                        foreach (var f in _foodItems)
                        {
                            var dSq = Vector2.DistanceSquared(head, f.Position);
                            if (dSq < minDistSq)
                            {
                                minDistSq = dSq;
                                nearestFood = f;
                            }
                        }
                    }

                    if (nearestFood != null && minDistSq < 40000) // 200 units
                    {
                        var toFood = nearestFood.Position - head;
                        bot.Angle = SmoothTurn(bot.Angle, (float)Math.Atan2(toFood.Y, toFood.X), 0.1f);
                    }
                    else if (_random.NextDouble() < 0.02)
                    {
                        // Random roam
                        bot.Angle += (float)(_random.NextDouble() * 0.4 - 0.2);
                    }
                }
            }
            
            MovePlayer(bot);
        }
    }

    private float SmoothTurn(float currentAngle, float targetAngle, float speed)
    {
        float diff = targetAngle - currentAngle;
        while (diff > Math.PI) diff -= (float)(Math.PI * 2);
        while (diff < -Math.PI) diff += (float)(Math.PI * 2);
        return currentAngle + diff * speed;
    }

    private void MovePlayer(Player player)
    {
        if (player.Body.Count == 0 || player.IsDead) return;

        lock (player)
        {
            // Smooth Rotation (90-120 degrees per second)
            // Tick is 16ms (~60 FPS), so speed is ~2 degrees per tick
            float turnSpeed = 0.12f; 
            player.Angle = SmoothTurn(player.Angle, player.TargetAngle, turnSpeed);

            var head = player.Body[0];
            var direction = new Vector2((float)Math.Cos(player.Angle), (float)Math.Sin(player.Angle));
            
            // Speed can vary based on score or powerups
            float currentSpeed = player.Speed;
            var newHead = head + direction * currentSpeed;

            // Boundary Check (Circle)
            var center = new Vector2(_mapRadius, _mapRadius);
            if (Vector2.Distance(newHead, center) > _mapRadius)
            {
                player.IsDead = true; 
                OnLog?.Invoke($"[MONITOR] Player {player.Name} died: Out of bounds at {newHead}");
                OnPlayerDeath?.Invoke(player, "Wyleciałeś poza mapę!");
                return;
            }

            // Predictive Collision Check (Self and others)
            // (Handled in CheckCollisions for simplicity, but could be added here)

            player.Body.Insert(0, newHead);
            
            // Dynamic length based on score
            int targetLength = 15 + player.Score / 2;
            while (player.Body.Count > targetLength)
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
            if (player.Body == null || player.Body.Count == 0 || player.IsDead) 
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
                    var distSq = Vector2.DistanceSquared(head, food.Position);
                    // DEBUG: Log if we are close but not colliding
                    // if (distSq < 1000) OnLog?.Invoke($"DEBUG: Head at {head}, Food at {food.Position}, DistSq: {distSq}");
                    
                    if (distSq < 625) // 25^2
                    {
                        if (food.IsPowerUp)
                        {
                            if (food.Type == "speed") player.Speed += 0.2f;
                            else if (food.Type == "score") player.Score += 20;
                        }
                        
                        player.Score += food.Value;
                        
                        // Queue for respawn after 3 seconds
                        _respawnQueue[food.Id] = DateTime.Now.AddSeconds(3);
                        _foodItems.RemoveAt(i);
                    }
                }
            }

            // 2. Player collision
            foreach (var other in playersSnapshot)
            {
                if (other.IsDead) continue;
                int startIdx = (other.Id == player.Id) ? 20 : 0; // Increased safety for self-collision (initial length is 15)
                List<Vector2> otherBody;
                lock (other)
                {
                    otherBody = other.Body.ToList();
                }
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
                    List<Vector2> deadBody;
                    lock (deadPlayer)
                    {
                        deadBody = deadPlayer.Body.ToList();
                    }
                    foreach (var segment in deadBody)
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
            // Only replenish if not waiting for respawn and below target
            if (IsRunning && _maxFood > 0) 
            {
                while (_foodItems.Count + _respawnQueue.Count < _maxFood) SpawnFood();
            }
        }
    }

    private void AddBot(string? fixedName = null)
    {
        var botId = "bot_" + Guid.NewGuid().ToString().Substring(0, 8);
        var bot = new Player
        {
            Id = botId,
            Name = fixedName ?? ("Bot_" + _random.Next(1000)),
            IsBot = true,
            Angle = (float)(_random.NextDouble() * Math.PI * 2),
            Color = GetRandomParrotColor(),
            Score = 0,
            Speed = 2.5f + (float)(_random.NextDouble() * 1.0) // Slower bots for better stability
        };
        
        Vector2 startPos = FindSafeSpawn(100); // 100 units safe distance
        
        for (int i = 0; i < 15; i++) bot.Body.Add(startPos);
        _players.TryAdd(botId, bot);
    }

    public void AddPlayer(string id, string name, float? startX = null, float? startY = null, int initialScore = 0)
    {
        OnLog?.Invoke($"[INIT] Attempting to spawn player: {name} (ID: {id})");
        
        if (_players.ContainsKey(id)) {
            OnLog?.Invoke($"[MONITOR] ERROR: Duplicate join for ID {id}. Cleaning up old session.");
            _players.TryRemove(id, out _);
        }

        var player = new Player
        {
            Id = id,
            Name = name,
            IsBot = false,
            Angle = (float)(_random.NextDouble() * Math.PI * 2),
            TargetAngle = 0,
            Color = GetRandomParrotColor(),
            Score = initialScore,
            Speed = 3.2f, // Slightly increased base speed
            IsDead = false
        };
        player.TargetAngle = player.Angle;
        
        Vector2 startPos;
        try {
            // Priority: provided coord -> safe spawn -> center
            if (startX.HasValue && startY.HasValue && startX.Value > 0 && startY.Value > 0)
            {
                startPos = new Vector2(startX.Value, startY.Value);
            }
            else
            {
                startPos = FindSafeSpawn(200f);
            }
            
            player.Body.Clear();
            int startSegments = 15 + initialScore / 2;
            for (int i = 0; i < startSegments; i++) player.Body.Add(startPos);
            
            if (_players.TryAdd(id, player))
            {
                OnLog?.Invoke($"[MONITOR] SUCCESS: Player {name} spawned at {startPos.X:F0},{startPos.Y:F0}. Segments: {startSegments}");
                OnPlayerSpawned?.Invoke(player);
            } else {
                OnLog?.Invoke($"[MONITOR] ERROR: Dictionary conflict for {name} ({id}).");
            }
        } catch (Exception ex) {
            OnLog?.Invoke($"[CRITICAL] Spawn Pipeline failed for {name}: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private string GetRandomParrotColor()
    {
        string[] colors = [
            "#FF5733", "#33FF57", "#3357FF", "#FF33A1", "#A133FF",
            "#33FFF5", "#FF8C33", "#8CFF33", "#338CFF", "#FF3333",
            "#33FF33", "#3333FF", "#FFFF33", "#FF33FF", "#33FFFF"
        ];
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
                Vector2 headPos;
                lock (p)
                {
                    if (p.Body.Count == 0) continue;
                    headPos = p.Body[0];
                }
                
                if (Vector2.Distance(pos, headPos) < safeDist)
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
            player.TargetAngle = angle;
        }
    }
}
