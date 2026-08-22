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

    public void AddPlayer(string connectionId, string username, string skinColor)
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

        // Optimized Steering AI for bots
        foreach (var bot in _players.Values.Where(p => p.IsBot && !p.IsDead))
        {
            var head = bot.Body[0];
            float moveVx = MathF.Cos(bot.Angle);
            float moveVy = MathF.Sin(bot.Angle);
            bool emergencyFlee = false;

            // 1. Wall repulsion force
            float distanceFromCenter = MathF.Sqrt(head.X * head.X + head.Y * head.Y);
            if (distanceFromCenter > MapRadius * 0.72f)
            {
                float wallFactor = (distanceFromCenter - MapRadius * 0.72f) / (MapRadius * 0.28f);
                float wallForce = wallFactor * wallFactor * 6.0f;
                moveVx -= (head.X / distanceFromCenter) * wallForce;
                moveVy -= (head.Y / distanceFromCenter) * wallForce;
            }

            // 2. Obstacle / Enemy body repulsion force
            float bodyRepulsionX = 0f;
            float bodyRepulsionY = 0f;

            foreach (var other in _players.Values)
            {
                if (other.Id == bot.Id || other.IsDead) continue;

                int step = Math.Max(1, other.Body.Count / 10);
                for (int index = 0; index < other.Body.Count; index += step)
                {
                    var seg = other.Body[index];
                    float dx = head.X - seg.X;
                    float dy = head.Y - seg.Y;
                    float distSq = dx * dx + dy * dy;

                    if (distSq < BotDangerRadius * BotDangerRadius && distSq > 1f)
                    {
                        float dist = MathF.Sqrt(distSq);
                        float force = (BotDangerRadius - dist) / BotDangerRadius;
                        force = force * force * 4.0f;

                        bodyRepulsionX += (dx / dist) * force;
                        bodyRepulsionY += (dy / dist) * force;

                        if (dist < 70f)
                        {
                            emergencyFlee = true;
                        }
                    }
                }
            }

            moveVx += bodyRepulsionX;
            moveVy += bodyRepulsionY;

            // 3. Ambition-Driven Growth & Combat AI
            Food? bestFoodCluster = null;
            float maxFoodScore = float.MinValue;

            // Search for high-value food clusters (e.g. dead snake drops)
            foreach (var food in _foods.Values)
            {
                float fdx = food.Position.X - head.X;
                float fdy = food.Position.Y - head.Y;
                float fdistSq = fdx * fdx + fdy * fdy;

                if (fdistSq > 500f * 500f) continue;

                float fdist = MathF.Sqrt(fdistSq);
                // Prioritize high-value food (dead snake drops) and nearby food
                float candidateScore = (food.Value * 150f) / MathF.Max(fdist, 15f);

                if (candidateScore > maxFoodScore)
                {
                    maxFoodScore = candidateScore;
                    bestFoodCluster = food;
                }
            }

            PlayerData? targetEnemy = null;
            float nearestEnemyDistSq = 350f * 350f;

            // Only consider combat if target is close
            foreach (var enemy in _players.Values)
            {
                if (enemy.Id == bot.Id || enemy.IsDead) continue;
                var enemyHead = enemy.Body[0];
                float edx = enemyHead.X - head.X;
                float edy = enemyHead.Y - head.Y;
                float edistSq = edx * edx + edy * edy;

                if (edistSq < nearestEnemyDistSq)
                {
                    nearestEnemyDistSq = edistSq;
                    targetEnemy = enemy;
                }
            }

            bool isTacticalBoost = false;

            // Decision Logic: Growth ambition vs Opportunistic Combat
            bool focusOnGrowth = (bot.Score < 450) || (bestFoodCluster != null && bestFoodCluster.Value >= 20);

            if (!focusOnGrowth && targetEnemy != null && bot.Score > targetEnemy.Score * 1.2f)
            {
                // Coiling / Trap maneuver when large enough
                var enemyHead = targetEnemy.Body[0];
                float distToEnemy = MathF.Sqrt(nearestEnemyDistSq);
                float angleToTarget = MathF.Atan2(enemyHead.Y - head.Y, enemyHead.X - head.X);
                float coilAngle = angleToTarget + MathF.PI * 0.52f;

                moveVx += MathF.Cos(coilAngle) * 3.8f;
                moveVy += MathF.Sin(coilAngle) * 3.8f;
                isTacticalBoost = distToEnemy < 160f;
            }
            else if (bestFoodCluster != null)
            {
                // Active Food Scavenging & Mass Accumulation
                float fdx = bestFoodCluster.Position.X - head.X;
                float fdy = bestFoodCluster.Position.Y - head.Y;
                float fdist = MathF.Sqrt(fdx * fdx + fdy * fdy);

                if (fdist > 1f)
                {
                    moveVx += (fdx / fdist) * 3.5f;
                    moveVy += (fdy / fdist) * 3.5f;

                    // Rush to high value drop clusters
                    if (bestFoodCluster.Value >= 20 && fdist < 320f && fdist > 40f && bot.Score > 60)
                    {
                        isTacticalBoost = true;
                    }
                }
            }

            float desiredAngle = MathF.Atan2(moveVy, moveVx);
            bot.Angle = RotateTowards(bot.Angle, desiredAngle, bot.BotSteerStrength);
            bot.IsBoosting = emergencyFlee || isTacticalBoost;
        }
    }

    private void SpawnBot()
    {
        string botId = "bot_" + Guid.NewGuid().ToString().Substring(0, 8);
        string[] colors = { "#ef4444", "#22c55e", "#3b82f6", "#eab308", "#ec4899", "#06b6d4", "#a855f7" };
        float dist = (float)(_rand.NextDouble() * MapRadius * 0.75f);
        float angle = (float)(_rand.NextDouble() * Math.PI * 2);
        float x = (float)(Math.Cos(angle) * dist);
        float y = (float)(Math.Sin(angle) * dist);

        int initialScore = _rand.Next(100, 200);

        var bot = new PlayerData
        {
            Id = botId,
            Username = GenerateBotName(),
            SkinColor = colors[_rand.Next(colors.Length)],
            Score = initialScore,
            IsBot = true,
            Angle = angle,
            BotSteerStrength = 0.09f + (float)_rand.NextDouble() * 0.07f
        };

        SeedBody(bot, x, y, angle, BaseBodySegments + initialScore / 18);
        
        _players.TryAdd(botId, bot);
    }

    private void ManageFood()
    {
        while (_foods.Count < MaxFoodCount)
        {
            float dist = (float)Math.Sqrt(_rand.NextDouble()) * MapRadius;
            float angle = (float)(_rand.NextDouble() * Math.PI * 2);
            var pos = new Vector2D((float)(Math.Cos(angle) * dist), (float)(Math.Sin(angle) * dist));
            
            string[] colors = { "#ef4444", "#22c55e", "#3b82f6", "#eab308", "#ec4899", "#06b6d4", "#ffffff" };
            
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
            if (p.IsDead) continue;

            float speed = p.MoveSpeed;
            if (p.IsBoosting && p.Score > 40)
            {
                speed *= 1.85f; // Faster, punchy sprint speed
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
            
            // Move head position
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

            // Balanced length growth (+25% harder score threshold per segment)
            int desiredLength = BaseBodySegments + (p.Score / 18);

            while (p.Body.Count < desiredLength)
            {
                var last = p.Body.Last();
                p.Body.Add(new Vector2D(last.X, last.Y));
            }
            while (p.Body.Count > desiredLength)
            {
                p.Body.RemoveAt(p.Body.Count - 1);
            }

            // Propagate distance constraint across body segments (Inverse Kinematics joint anchoring)
            for (int i = 1; i < p.Body.Count; i++)
            {
                var prev = p.Body[i - 1];
                var curr = p.Body[i];
                float dx = curr.X - prev.X;
                float dy = curr.Y - prev.Y;
                float dist = MathF.Sqrt(dx * dx + dy * dy);

                if (dist > 0.001f)
                {
                    float factor = SegmentSpacing / dist;
                    curr.X = prev.X + dx * factor;
                    curr.Y = prev.Y + dy * factor;
                }
            }
        }
    }

    private void DropFood(Vector2D pos, string color, int value = 8)
    {
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
        var players = _players.Values.Where(p => !p.IsDead).ToList();
        
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
                            if (fdx * fdx + fdy * fdy < 400f) // 20 radius squared
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
                if (p.Id == other.Id) continue;
                
                // Skip head, start from index 2 to give leeway
                for (int i = 2; i < other.Body.Count; i++)
                {
                    var seg = other.Body[i];
                    float dx = seg.X - head.X;
                    float dy = seg.Y - head.Y;
                    if (dx*dx + dy*dy < 225f) // 15 radius squared
                    {
                        p.IsDead = true;
                        TurnIntoFood(p);
                        break;
                    }
                }
                if (p.IsDead) break;
            }
        }

        // Clean up dead humans from dictionary, keep bots so ManageBots can clean them (or just clean here)
        var deadHumans = players.Where(p => p.IsDead && !p.IsBot).ToList();
        foreach (var dead in deadHumans)
        {
            _hubContext?.Clients.Client(dead.Id).SendAsync("GameOver", dead.Score);
            _players.TryRemove(dead.Id, out _);
        }
    }

    private async void BroadcastState()
    {
        if (_hubContext == null) return;

        var state = new GameStateDto
        {
            Players = _players.Values.ToList(),
            Food = _foods.Values.ToList()
        };

        await _hubContext.Clients.All.SendAsync("UpdateState", state);
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
