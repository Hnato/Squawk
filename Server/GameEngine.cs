using System;
using System.Collections.Generic;
using System.Linq;
using Squawk.Server.Models;

namespace Squawk.Server
{
    public class GameEngine
    {
        public const float MapRadius = 7200f; 
        public const int MaxFeathers = 4000; 
        public const int BotCount = 34; 

        private Dictionary<string, Parrot> _parrots = new Dictionary<string, Parrot>();
        private List<FeatherEnergy> _feathers = new List<FeatherEnergy>();
        private Random _random = new Random();

        public GameEngine()
        {
            SpawnInitialFeathers();
            SpawnBots();
        }

        private Vector2 GetRandomMapPosition()
        {
            // Uniform distribution in a circle
            // r = R * sqrt(random)
            // theta = 2 * pi * random
            double r = MapRadius * Math.Sqrt(_random.NextDouble());
            double theta = _random.NextDouble() * 2 * Math.PI;
            
            float x = (float)(r * Math.Cos(theta)) + MapRadius;
            float y = (float)(r * Math.Sin(theta)) + MapRadius;
            
            return new Vector2(x, y);
        }

        private void SpawnInitialFeathers()
        {
            for (int i = 0; i < MaxFeathers; i++)
            {
                SpawnFeather(FeatherType.WORLD_FEATHER);
            }
        }

        private void SpawnBots()
        {
            for (int i = 0; i < BotCount; i++)
            {
                string id = "bot_" + Guid.NewGuid().ToString().Substring(0, 8);
                AddParrot(id, "Bot " + i, true);
            }
        }

        public Parrot AddParrot(string id, string name, bool isBot = false)
        {
            var pos = GetRandomMapPosition();
            Parrot parrot;
            
            lock (_parrots)
            {
                // If player already exists (re-joining), update it
                if (isBot)
                    parrot = new BotParrot(id, name, pos);
                else
                    parrot = new Parrot(id, name, pos);
                
                _parrots[id] = parrot;
            }
            return parrot;
        }

        public Parrot? GetParrot(string id)
        {
            lock (_parrots)
            {
                return _parrots.TryGetValue(id, out var parrot) ? parrot : null;
            }
        }

        public void RemoveParrot(string id)
        {
            lock (_parrots)
            {
                _parrots.Remove(id);
            }
        }

        public void UpdateInput(string id, float targetX, float targetY, bool isBoosting)
        {
            lock (_parrots)
            {
                if (_parrots.TryGetValue(id, out var parrot))
                {
                    // Calculate target direction
                    var targetPos = new Vector2(targetX, targetY);
                    var diff = targetPos - parrot.Position;
                    if (diff.Length() > 5)
                    {
                        float targetAngle = Vector2.Angle(diff);
                        
                        // Smoothly rotate towards target angle based on turn rate
                        float angleDiff = targetAngle - parrot.Direction;
                        while (angleDiff > Math.PI) angleDiff -= (float)(2 * Math.PI);
                        while (angleDiff < -Math.PI) angleDiff += (float)(2 * Math.PI);

                        float maxTurn = parrot.TurnRate * 0.033f; // 30 FPS tick
                        if (Math.Abs(angleDiff) > maxTurn)
                        {
                            parrot.Direction += Math.Sign(angleDiff) * maxTurn;
                        }
                        else
                        {
                            parrot.Direction = targetAngle;
                        }
                    }
                    parrot.IsBoosting = isBoosting;
                }
            }
        }

        public void Update(float deltaTime)
        {
            lock (_parrots)
            {
                // Update Bots AI
                foreach (var parrot in _parrots.Values.OfType<BotParrot>())
                {
                    UpdateBotAI(parrot, deltaTime);
                }

                // Update Movement
                foreach (var parrot in _parrots.Values.ToList())
                {
                    if (!parrot.IsAlive) continue;
                    UpdateParrot(parrot, deltaTime);
                }

                // Keep feathers populated
                while (_feathers.Count < MaxFeathers)
                {
                    SpawnFeather(FeatherType.WORLD_FEATHER);
                }
            }
        }

        private void UpdateSegments(Parrot parrot)
        {
            if (parrot.Segments.Count == 0) return;

            // Target number of segments based on energy
            int targetSegments = parrot.MaxSegments;

            // Add segments if needed
            if (parrot.Segments.Count < targetSegments)
            {
                parrot.Segments.Add(parrot.Segments.Last());
            }

            // Move segments
            for (int i = parrot.Segments.Count - 1; i > 0; i--)
            {
                Vector2 prev = parrot.Segments[i - 1];
                Vector2 curr = parrot.Segments[i];
                
                float dist = Vector2.Distance(curr, prev);
                float minDist = Parrot.SegmentDistance * 0.4f; // Overlapping segments

                if (dist > minDist)
                {
                    Vector2 diff = prev - curr;
                    parrot.Segments[i] = prev - diff.Normalized() * minDist;
                }
            }
            
            // Head position is already updated in Update loop
            parrot.Segments[0] = parrot.Position;
        }

        private void UpdateParrot(Parrot parrot, float deltaTime)
        {
            if (!parrot.IsAlive) return;

            // V12: Level System & Difficulty Scaling
            float levelProgress = Math.Min(1.0f, parrot.Energy / 1000f);
            // Higher energy = slightly more speed but much harder turning
            float speedMultiplier = 1.0f + (levelProgress * 0.5f);
            float currentSpeed = parrot.CurrentSpeed * speedMultiplier;

            // Handle Input/Movement (Direction updated in UpdateInput)
            // Update Position
            parrot.Position += Vector2.FromAngle(parrot.Direction) * currentSpeed * deltaTime;

            // Energy decay based on size/level
            float decayRate = 1.0f + (levelProgress * 5.0f);
            if (parrot.IsBoosting) decayRate *= 3.0f;
            parrot.Energy = Math.Max(10, parrot.Energy - decayRate * deltaTime);

            UpdateSegments(parrot);
            CheckCollisions(parrot);
        }

        private void CheckCollisions(Parrot parrot)
        {
            // Boundary check
            float distFromCenter = Vector2.Distance(parrot.Position, new Vector2(MapRadius, MapRadius));
            if (distFromCenter > MapRadius)
            {
                KillParrot(parrot);
                return;
            }

            // Feather collection
            var collected = _feathers.Where(f => Vector2.Distance(parrot.Position, f.Position) < 30 * parrot.Size).ToList();
            foreach (var f in collected)
            {
                _feathers.Remove(f);
                // V12: Scaling Rewards - Higher level parrots get slightly more energy per feather
                float rewardBonus = 1.0f + (parrot.Energy / 1000f);
                parrot.Energy += (f.Value * 2.0f) * rewardBonus;
            }

            // Player-Player collisions (Head to body)
            foreach (var other in _parrots.Values)
            {
                if (other.Id == parrot.Id || !other.IsAlive) continue;

                for (int i = 0; i < other.Segments.Count; i++)
                {
                    // Head of 'parrot' hits 'other' body segment
                    float collisionDist = 20 * (parrot.Size + other.Size) * 0.5f;
                    if (Vector2.Distance(parrot.Position, other.Segments[i]) < collisionDist)
                    {
                        KillParrot(parrot);
                        return;
                    }
                }
            }
        }

        private void KillParrot(Parrot parrot)
        {
            parrot.IsAlive = false;
            
            // Notify player of death if it's a real player
            if (!parrot.IsBot)
            {
                // Notification handled in Program.cs
            }

            // In V9, we remove the death feathers or keep them minimal to ensure total cleanup
            // The user requested NO trace after death
        }

        private void SpawnFeather(FeatherType type, Vector2? pos = null, float? value = null)
        {
            var feather = new FeatherEnergy
            {
                Type = type,
                Position = pos ?? GetRandomMapPosition(),
                Value = value ?? (type == FeatherType.WORLD_FEATHER ? 1.0f : 0.5f)
            };
            _feathers.Add(feather);
        }

        private void UpdateBotAI(BotParrot bot, float deltaTime)
        {
            bot.StateTimer -= deltaTime;
            
            // 1. DANGER AVOIDANCE (Highest priority)
            foreach (var other in _parrots.Values)
            {
                if (other.Id == bot.Id || !other.IsAlive) continue;
                
                // If head is close to any other parrot segment, steer away
                foreach (var seg in other.Segments)
                {
                    float dist = Vector2.Distance(bot.Position, seg);
                    if (dist < 180) // Increased danger zone
                    {
                        Vector2 away = bot.Position - seg;
                        Vector2 evadePos = bot.Position + away.Normalized() * 250;
                        UpdateInput(bot.Id, evadePos.X, evadePos.Y, bot.Energy > 30); // More likely to boost when in danger
                        return; 
                    }
                }
            }

            // 2. AGGRESSIVE ATTACK (New priority)
            // Look for nearby players/bots to attack
            var target = _parrots.Values
                .Where(p => p.Id != bot.Id && p.IsAlive)
                .OrderBy(p => Vector2.Distance(bot.Position, p.Position))
                .FirstOrDefault();

            if (target != null && Vector2.Distance(bot.Position, target.Position) < 1000)
            {
                // Try to intercept
                Vector2 interceptPos = target.Position + Vector2.FromAngle(target.Direction) * 150;
                bool shouldBoost = Vector2.Distance(bot.Position, target.Position) < 400 && bot.Energy > 20;
                UpdateInput(bot.Id, interceptPos.X, interceptPos.Y, shouldBoost);
                return;
            }

            // 3. MAP BOUNDARY AVOIDANCE
            float distFromCenter = Vector2.Distance(bot.Position, new Vector2(MapRadius, MapRadius));
            if (distFromCenter > MapRadius * 0.85f)
            {
                UpdateInput(bot.Id, MapRadius, MapRadius, false);
                return;
            }

            // 4. REGULAR BEHAVIOR
            if (bot.StateTimer <= 0)
            {
                var nearbyFeathers = _feathers.Where(f => Vector2.Distance(bot.Position, f.Position) < 1200).ToList();
                
                if (nearbyFeathers.Any())
                {
                    bot.State = BotState.FEED;
                    bot.TargetPosition = nearbyFeathers.OrderBy(f => Vector2.Distance(bot.Position, f.Position)).First().Position;
                }
                else
                {
                    bot.State = BotState.WANDER;
                    float angle = (float)(_random.NextDouble() * Math.PI * 2);
                    float dist = (float)(_random.NextDouble() * MapRadius * 0.6f);
                    bot.TargetPosition = new Vector2(
                        (float)(Math.Cos(angle) * dist) + MapRadius,
                        (float)(Math.Sin(angle) * dist) + MapRadius
                    );
                }
                bot.StateTimer = 0.3f + (float)_random.NextDouble() * 0.5f; // Faster decision making
            }

            if (bot.TargetPosition.HasValue)
            {
                UpdateInput(bot.Id, bot.TargetPosition.Value.X, bot.TargetPosition.Value.Y, false);
            }
        }

        public GameUpdateMessage GetState()
        {
            lock (_parrots)
            {
                return new GameUpdateMessage
                {
                    Parrots = _parrots.Values.Where(p => p.IsAlive).Select(p => new ParrotData
                    {
                        Id = p.Id,
                        Name = p.Name,
                        X = p.Position.X,
                        Y = p.Position.Y,
                        Dir = p.Direction,
                        Energy = p.Energy,
                        Size = p.Size,
                        Segments = p.Segments.ToList(),
                        IsBoosting = p.IsBoosting
                    }).ToList(),
                    Feathers = _feathers.Select(f => new FeatherData
                    {
                        Id = f.Id,
                        X = f.Position.X,
                        Y = f.Position.Y,
                        Value = f.Value,
                        Type = f.Type
                    }).ToList()
                };
            }
        }

        public LeaderboardMessage GetLeaderboard()
        {
            lock (_parrots)
            {
                return new LeaderboardMessage
                {
                    Entries = _parrots.Values.Where(p => p.IsAlive)
                        .OrderByDescending(p => p.Energy)
                        .Take(10)
                        .Select(p => new LeaderboardEntry { Name = p.Name, Score = p.Energy })
                        .ToList()
                };
            }
        }

        public void CleanupDead()
        {
            lock (_parrots)
            {
                var deadBots = _parrots.Values.Where(p => p.IsBot && !p.IsAlive).ToList();
                foreach (var bot in deadBots)
                {
                    _parrots.Remove(bot.Id);
                    AddParrot("bot_" + Guid.NewGuid().ToString().Substring(0, 8), bot.Name, true);
                }
            }
        }
    }
}
