using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Server.Hubs;

namespace Server.Game;

public class GameEngine
{
    public static GameEngine Instance { get; } = new GameEngine();
    private const int BaseBodySegments = 12;
    private const float SegmentSpacing = 12f;
    private const float BotDangerRadius = 160f;
    private const float SpatialCellSize = 150f;

    private static readonly string[] BotNameParts =
    {
        "bowieknife",
        "venomtail",
        "acidfang",
        "skullcoil",
        "nightviper",
        "razorbeak",
        "stormbite",
        "toxicloop",
        "shadowfang",
        "warpscale",
        "ironcoil",
        "fangblast",
        "cryptskin",
        "darkslither",
        "voidhiss"
    };

    private readonly ConcurrentDictionary<string, PlayerData> _players = new();
    private readonly ConcurrentDictionary<int, Food> _foods = new();
    private int _foodCounter = 0;
    
    // Map bounds
    public const float MapRadius = 2000f;
    private const int MaxFoodCount = 550;
    private const int TargetBotCount = 10;
    private const int MaxPlayersForBots = 15;
    
    private Random _rand = new Random();
    private System.Threading.Timer? _gameTimer;
    private IHubContext<GameHub>? _hubContext;
    private bool _botsEnabled = true;

    public void Initialize(IHubContext<GameHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public void SetBotsEnabled(bool enabled)
    {
        _botsEnabled = enabled;

        if (!enabled)
        {
            var botIds = _players.Values.Where(player => player.IsBot).Select(player => player.Id).ToList();
            foreach (var botId in botIds)
            {
                _players.TryRemove(botId, out _);
            }
        }
    }

    public void Start()
    {
        // 30 ticks per second (approx 33ms)
        _gameTimer = new System.Threading.Timer(GameLoop, null, 0, 33);
        Form1.Instance?.Log("Silnik gry wystartował.");
    }

    public void Stop()
    {
        _gameTimer?.Dispose();
        _players.Clear();
        _foods.Clear();
        Form1.Instance?.Log("Silnik gry zatrzymany.");
    }

    public void AddPlayer(string connectionId, string username, string skinColor, string skinPattern = "ara")
    {
        float spawnDistance = (float)(_rand.NextDouble() * MapRadius * 0.45f);
        float spawnAngle = (float)(_rand.NextDouble() * Math.PI * 2);
        float spawnX = (float)(Math.Cos(spawnAngle) * spawnDistance);
        float spawnY = (float)(Math.Sin(spawnAngle) * spawnDistance);
        float travelAngle = (float)(_rand.NextDouble() * Math.PI * 2);

        var player = new PlayerData
        {
            Id = connectionId,
            Username = username,
            SkinColor = skinColor,
            SkinPattern = string.IsNullOrEmpty(skinPattern) ? "ara" : skinPattern,
            Score = 100,
            Angle = travelAngle,
            IsBot = false
        };

        SeedBody(player, spawnX, spawnY, travelAngle, BaseBodySegments);

        _players.TryAdd(connectionId, player);
    }

    public void RemovePlayer(string connectionId)
    {
        _players.TryRemove(connectionId, out _);
    }

    public void UpdatePlayerInput(string connectionId, float angle, bool isBoosting)
    {
        if (_players.TryGetValue(connectionId, out var player))
        {
            player.Angle = angle;
            player.IsBoosting = isBoosting;
        }
    }

    private void GameLoop(object? state)
    {
        ManageBots();
        ManageFood();
        UpdatePositions();
        CheckCollisions();
        BroadcastState();
    }

    private void ManageBots()
    {
        if (!_botsEnabled)
        {
            var botIds = _players.Values.Where(player => player.IsBot).Select(player => player.Id).ToList();
            foreach (var botId in botIds)
            {
                _players.TryRemove(botId, out _);
            }
            return;
        }

        // Remove dead bots
        var deadBots = _players.Values.Where(p => p.IsBot && p.IsDead).Select(p => p.Id).ToList();
        foreach (var botId in deadBots) _players.TryRemove(botId, out _);

        int humanCount = _players.Values.Count(p => !p.IsBot);
        int botCount = _players.Values.Count(p => p.IsBot);

        if (humanCount <= MaxPlayersForBots && botCount < TargetBotCount)
        {
            SpawnBot();
        }

        // Hyper-Greedy, Aggressive Predator AI for bots
        foreach (var bot in _players.Values.Where(p => p.IsBot && !p.IsDead))
        {
            if (bot.Body.Count == 0) continue;
            var head = bot.Body[0];
            float distFromCenter = MathF.Sqrt(head.X * head.X + head.Y * head.Y);

            // 1. HARD BORDER SAFETY: When near border, steer decisively straight towards center
            if (distFromCenter > MapRadius * 0.82f)
            {
                float angleToCenter = MathF.Atan2(-head.Y, -head.X);
                bot.Angle = RotateTowards(bot.Angle, angleToCenter, 0.35f);
                bot.IsBoosting = bot.Score > 50;
                continue;
            }

            // 2. TARGET SELECTION (Food Hunger & Killer Instinct)
            float bestTargetX = head.X + MathF.Cos(bot.Angle) * 100f;
            float bestTargetY = head.Y + MathF.Sin(bot.Angle) * 100f;
            float highestDesireScore = -1f;
            bool shouldBoost = false;

            // A. Search for High-Value Food and nearby pellets
            foreach (var food in _foods.Values)
            {
                float dx = food.Position.X - head.X;
                float dy = food.Position.Y - head.Y;
                float distSq = dx * dx + dy * dy;
                if (distSq > 550f * 550f) continue;

                float dist = MathF.Sqrt(distSq);
                // Big drops (dead snakes) get immense desire score
                float desire = (food.Value >= 18 ? 3200f : food.Value * 120f) / MathF.Max(dist, 15f);

                if (desire > highestDesireScore)
                {
                    highestDesireScore = desire;
                    bestTargetX = food.Position.X;
                    bestTargetY = food.Position.Y;
                    if (food.Value >= 18 && dist < 320f && bot.Score > 50)
                    {
                        shouldBoost = true;
                    }
                }
            }

            // B. Hunt and Kill Nearby Snakes (Predator Mode)
            foreach (var enemy in _players.Values)
            {
                if (enemy.Id == bot.Id || enemy.IsDead || enemy.Body.Count == 0) continue;
                var enemyHead = enemy.Body[0];
                float edx = enemyHead.X - head.X;
                float edy = enemyHead.Y - head.Y;
                float distSq = edx * edx + edy * edy;

                if (distSq > 420f * 420f) continue;
                float dist = MathF.Sqrt(distSq);

                // If bot is bigger or equal, attack aggressively to cut them off
                if (bot.Score >= enemy.Score * 0.85f)
                {
                    float enemySpeed = enemy.IsBoosting ? 5.5f : 3.0f;
                    // Predict future head position (intercept lead)
                    float interceptLead = MathF.Min(dist / 10f, 25f);
                    float predX = enemyHead.X + MathF.Cos(enemy.Angle) * (enemySpeed * interceptLead + 30f);
                    float predY = enemyHead.Y + MathF.Sin(enemy.Angle) * (enemySpeed * interceptLead + 30f);

                    float huntDesire = 2800f / MathF.Max(dist, 20f);
                    if (huntDesire > highestDesireScore)
                    {
                        highestDesireScore = huntDesire;
                        bestTargetX = predX;
                        bestTargetY = predY;

                        // Boost to cut off enemy head!
                        if (dist < 220f && dist > 30f && bot.Score > 45)
                        {
                            shouldBoost = true;
                        }
                    }
                }
                else if (enemy.Score > bot.Score * 1.4f && dist < 120f)
                {
                    // Evade much larger predator when dangerously close
                    float fleeX = head.X - edx;
                    float fleeY = head.Y - edy;
                    bestTargetX = fleeX;
                    bestTargetY = fleeY;
                    shouldBoost = bot.Score > 45;
                }
            }

            // 3. TARGET ANGLE
            float desiredAngle = MathF.Atan2(bestTargetY - head.Y, bestTargetX - head.X);

            // 4. IMMINENT COLLISION DODGE (Only dodge when actual body is directly in front within 40px)
            foreach (var other in _players.Values)
            {
                if (other.Id == bot.Id || other.IsDead || other.Body.Count == 0) continue;

                for (int i = 2; i < other.Body.Count; i += 2)
                {
                    var seg = other.Body[i];
                    float sdx = seg.X - head.X;
                    float sdy = seg.Y - head.Y;
                    float sdistSq = sdx * sdx + sdy * sdy;

                    if (sdistSq < 42f * 42f)
                    {
                        // Direct obstacle in front! Steer perpendicular away
                        float obstacleAngle = MathF.Atan2(sdy, sdx);
                        float angleDiff = NormalizeAngle(desiredAngle - obstacleAngle);
                        float dodgeSign = angleDiff >= 0 ? 1f : -1f;
                        desiredAngle = obstacleAngle + dodgeSign * (MathF.PI * 0.55f);
                        break;
                    }
                }
            }

            bot.Angle = RotateTowards(bot.Angle, desiredAngle, 0.28f);
            bot.IsBoosting = shouldBoost;
        }
    }

    private void SpawnBot()
    {
        string botId = "bot_" + Guid.NewGuid().ToString().Substring(0, 8);
        string[] patterns = { "ara", "ararauna", "nimfa", "zako", "kakadu", "falista", "lorysa", "amazonka", "cyber", "sloneczna" };
        string[] colors = { "#ef4444", "#3b82f6", "#f59e0b", "#94a3b8", "#f8fafc", "#10b981", "#8b5cf6", "#06b6d4", "#ec4899", "#f97316" };
        
        int patternIdx = _rand.Next(patterns.Length);
        float dist = (float)(_rand.NextDouble() * MapRadius * 0.70f);
        float angle = (float)(_rand.NextDouble() * Math.PI * 2);
        float x = (float)(Math.Cos(angle) * dist);
        float y = (float)(Math.Sin(angle) * dist);

        int initialScore = _rand.Next(100, 220);

        var bot = new PlayerData
        {
            Id = botId,
            Username = GenerateBotName(),
            SkinColor = colors[patternIdx % colors.Length],
            SkinPattern = patterns[patternIdx],
            Score = initialScore,
            IsBot = true,
            Angle = angle,
            BotSteerStrength = 0.25f + (float)_rand.NextDouble() * 0.08f
        };

        SeedBody(bot, x, y, angle, BaseBodySegments + initialScore / 18);
        
        _players.TryAdd(botId, bot);
    }

    private void ManageFood()
    {
        while (_foods.Count < MaxFoodCount)
        {
            float dist = (float)Math.Sqrt(_rand.NextDouble()) * (MapRadius * 0.94f);
            float angle = (float)(_rand.NextDouble() * Math.PI * 2);
            var pos = new Vector2D((float)(Math.Cos(angle) * dist), (float)(Math.Sin(angle) * dist));
            
            string[] colors = { "#ef4444", "#22c55e", "#3b82f6", "#eab308", "#ec4899", "#06b6d4", "#f97316", "#a855f7", "#ffffff" };
            
            var food = new Food
            {
                Id = Interlocked.Increment(ref _foodCounter),
                Position = pos,
                Color = colors[_rand.Next(colors.Length)],
                Value = 12
            };
            _foods.TryAdd(food.Id, food);
        }
    }

    private void UpdatePositions()
    {
        foreach (var p in _players.Values)
        {
            if (p.IsDead || p.Body.Count == 0) continue;

            float speed = p.MoveSpeed;
            if (p.IsBoosting && p.Score > 40)
            {
                speed *= 1.85f;
                p.BoostCostTicks++;
                if (p.BoostCostTicks > 5)
                {
                    p.Score -= 3;
                    p.BoostCostTicks = 0;
                    if (p.Body.Count > 1)
                    {
                        DropFood(p.Body.Last(), p.SkinColor, 8);
                    }
                }
            }
            
            // 1. Advance head position forward in angle direction
            var head = p.Body[0];
            float newX = head.X + MathF.Cos(p.Angle) * speed;
            float newY = head.Y + MathF.Sin(p.Angle) * speed;

            // Check Map boundary (circle)
            if (newX * newX + newY * newY > MapRadius * MapRadius)
            {
                p.IsDead = true;
                TurnIntoFood(p);
                continue;
            }

            head.X = newX;
            head.Y = newY;

            // 2. Segment length growth & tail trimming
            int desiredLength = BaseBodySegments + (p.Score / 18);

            while (p.Body.Count < desiredLength)
            {
                var last = p.Body.Last();
                p.Body.Add(new Vector2D(last.X, last.Y));
            }
            while (p.Body.Count > desiredLength && p.Body.Count > 2)
            {
                p.Body.RemoveAt(p.Body.Count - 1);
            }

            // 3. True Snake Trail Pulling Physics:
            // When segment i-1 moves, segment i is pulled towards segment i-1 ONLY by the excess distance.
            // This ensures segments follow the head's exact path without freezing, bunching or pushing away.
            for (int i = 1; i < p.Body.Count; i++)
            {
                var prev = p.Body[i - 1];
                var curr = p.Body[i];
                float dx = prev.X - curr.X;
                float dy = prev.Y - curr.Y;
                float dist = MathF.Sqrt(dx * dx + dy * dy);

                if (dist > SegmentSpacing)
                {
                    float excess = dist - SegmentSpacing;
                    curr.X += (dx / dist) * excess;
                    curr.Y += (dy / dist) * excess;
                }
            }
        }
    }

    private void DropFood(Vector2D pos, string color, int value = 8)
    {
        if (_foods.Count >= MaxFoodCount + 100) return;

        var food = new Food
        {
            Id = Interlocked.Increment(ref _foodCounter),
            Position = new Vector2D(pos.X, pos.Y),
            Color = color,
            Value = value
        };
        _foods.TryAdd(food.Id, food);
    }

    private void TurnIntoFood(PlayerData player)
    {
        for (int i = 0; i < player.Body.Count; i += 2)
        {
            float jitterX = (float)(_rand.NextDouble() * 16 - 8);
            float jitterY = (float)(_rand.NextDouble() * 16 - 8);
            var pos = new Vector2D(player.Body[i].X + jitterX, player.Body[i].Y + jitterY);
            DropFood(pos, player.SkinColor, 20);
        }
    }

    private void CheckCollisions()
    {
        var players = _players.Values.Where(p => !p.IsDead && p.Body.Count > 0).ToList();
        
        // Fast Spatial Grid Partitioning for food collisions
        var foodGrid = new Dictionary<(int x, int y), List<Food>>();
        foreach (var food in _foods.Values)
        {
            int gx = (int)MathF.Floor(food.Position.X / SpatialCellSize);
            int gy = (int)MathF.Floor(food.Position.Y / SpatialCellSize);
            var key = (gx, gy);
            if (!foodGrid.TryGetValue(key, out var list))
            {
                list = new List<Food>();
                foodGrid[key] = list;
            }
            list.Add(food);
        }

        foreach (var p in players)
        {
            var head = p.Body[0];
            int headGx = (int)MathF.Floor(head.X / SpatialCellSize);
            int headGy = (int)MathF.Floor(head.Y / SpatialCellSize);

            var foodsEaten = new List<int>();

            // Query only 3x3 neighboring cells
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (foodGrid.TryGetValue((headGx + dx, headGy + dy), out var cellFoods))
                    {
                        foreach (var food in cellFoods)
                        {
                            float fdx = food.Position.X - head.X;
                            float fdy = food.Position.Y - head.Y;
                            if (fdx * fdx + fdy * fdy < 450f) // ~21 radius squared
                            {
                                p.Score += food.Value;
                                foodsEaten.Add(food.Id);
                            }
                        }
                    }
                }
            }

            foreach (var fId in foodsEaten) _foods.TryRemove(fId, out _);

            // Check player collision (head hits other player's body)
            foreach (var other in players)
            {
                if (p.Id == other.Id || other.IsDead) continue;
                
                // Skip head, start from index 2 to give leeway
                for (int i = 2; i < other.Body.Count; i++)
                {
                    var seg = other.Body[i];
                    float dx = seg.X - head.X;
                    float dy = seg.Y - head.Y;
                    if (dx*dx + dy*dy < 240f) // ~15.5 radius squared
                    {
                        p.IsDead = true;
                        TurnIntoFood(p);
                        break;
                    }
                }
                if (p.IsDead) break;
            }
        }

        // Clean up dead humans from dictionary
        var deadHumans = players.Where(p => p.IsDead && !p.IsBot).ToList();
        foreach (var dead in deadHumans)
        {
            _hubContext?.Clients.Client(dead.Id).SendAsync("GameOver", dead.Score);
            _players.TryRemove(dead.Id, out _);
        }
    }

    private int _isBroadcasting = 0;

    private async void BroadcastState()
    {
        if (_hubContext == null) return;

        // Prevent overlapping asynchronous broadcasts from queueing up and saturating bandwidth / CPU
        if (Interlocked.CompareExchange(ref _isBroadcasting, 1, 0) != 0)
        {
            return;
        }

        try
        {
            var livingPlayers = _players.Values.Where(p => !p.IsDead).ToList();
            var foods = _foods.Values.ToList();

            var state = new GameStateDto
            {
                Players = livingPlayers,
                Food = foods
            };

            await _hubContext.Clients.All.SendAsync("UpdateState", state);
        }
        catch
        {
            // Ignore broadcast exceptions during disconnects
        }
        finally
        {
            Interlocked.Exchange(ref _isBroadcasting, 0);
        }
    }

    private void SeedBody(PlayerData player, float x, float y, float angle, int segmentCount)
    {
        player.Body.Clear();

        for (int index = 0; index < segmentCount; index++)
        {
            float offset = index * SegmentSpacing;
            player.Body.Add(
                new Vector2D(
                    x - (float)Math.Cos(angle) * offset,
                    y - (float)Math.Sin(angle) * offset
                )
            );
        }
    }

    private static float NormalizeAngle(float angle)
    {
        while (angle > MathF.PI)
        {
            angle -= MathF.PI * 2f;
        }

        while (angle < -MathF.PI)
        {
            angle += MathF.PI * 2f;
        }

        return angle;
    }

    private static float RotateTowards(float currentAngle, float targetAngle, float maxStep)
    {
        float diff = NormalizeAngle(targetAngle - currentAngle);

        if (MathF.Abs(diff) <= maxStep)
        {
            return targetAngle;
        }

        return currentAngle + MathF.Sign(diff) * maxStep;
    }

    private string GenerateBotName()
    {
        string baseName = BotNameParts[_rand.Next(BotNameParts.Length)];
        int suffix = _rand.Next(10, 100);
        return $"{baseName}{suffix}";
    }
}

