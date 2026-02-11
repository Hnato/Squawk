using System;
using System.Collections.Generic;
using System.Linq;
using Squawk.Server.Models;

namespace Squawk.Server
{
    public class GameEngine
    {
        public const float MapWidth = 3000f;
        public const float MapHeight = 3000f;
        public const int MaxFeathers = 200;
        public const int BotCount = 10;

        private Dictionary<string, Parrot> _parrots = new Dictionary<string, Parrot>();
        private List<FeatherEnergy> _feathers = new List<FeatherEnergy>();
        private Random _random = new Random();

        public GameEngine()
        {
            SpawnInitialFeathers();
            SpawnBots();
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
            var pos = new Vector2((float)_random.NextDouble() * MapWidth, (float)_random.NextDouble() * MapHeight);
            Parrot parrot;
            if (isBot)
                parrot = new BotParrot(id, name, pos);
            else
                parrot = new Parrot(id, name, pos);
            
            lock (_parrots)
            {
                _parrots[id] = parrot;
            }
            return parrot;
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

                        float maxTurn = parrot.TurnRate * 0.016f; // approx 60fps
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

                    // Boost logic
                    if (parrot.IsBoosting && parrot.Energy > Parrot.MinEnergyForBoost)
                    {
                        parrot.Energy -= Parrot.BoostCost * deltaTime;
                        // Spawn boost feathers occasionally
                        if (_random.NextDouble() < 0.1)
                        {
                            SpawnFeather(FeatherType.BOOST_FEATHER, parrot.Segments.Last(), 0.5f);
                        }
                    }

                    // Move head
                    Vector2 move = Vector2.FromAngle(parrot.Direction) * parrot.CurrentSpeed * deltaTime;
                    parrot.Position += move;

                    // Update segments (follow leader)
                    UpdateSegments(parrot);

                    // Map boundaries
                    if (parrot.Position.X < 0 || parrot.Position.X > MapWidth || parrot.Position.Y < 0 || parrot.Position.Y > MapHeight)
                    {
                        KillParrot(parrot);
                        continue;
                    }

                    // Collisions with feathers
                    CheckFeatherCollisions(parrot);

                    // Collisions with other parrots
                    CheckParrotCollisions(parrot);
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
            // First segment is the head position
            parrot.Segments[0] = parrot.Position;

            // Target number of segments based on energy
            int targetSegmentCount = (int)(parrot.Energy / 5f) + 3;
            while (parrot.Segments.Count < targetSegmentCount)
            {
                parrot.Segments.Add(parrot.Segments.Last());
            }

            // Move segments to maintain distance
            for (int i = 1; i < parrot.Segments.Count; i++)
            {
                Vector2 prev = parrot.Segments[i - 1];
                Vector2 curr = parrot.Segments[i];
                float dist = Vector2.Distance(prev, curr);
                float minDist = Parrot.SegmentDistance * parrot.Size;

                if (dist > minDist)
                {
                    Vector2 dir = (prev - curr).Normalized();
                    parrot.Segments[i] = prev - dir * minDist;
                }
            }
        }

        private void CheckFeatherCollisions(Parrot parrot)
        {
            float pickupRadius = 30f * parrot.Size;
            var caught = _feathers.Where(f => Vector2.Distance(parrot.Position, f.Position) < pickupRadius).ToList();
            foreach (var f in caught)
            {
                parrot.Energy += f.Value;
                _feathers.Remove(f);
            }
        }

        private void CheckParrotCollisions(Parrot parrot)
        {
            foreach (var other in _parrots.Values)
            {
                if (other.Id == parrot.Id) continue;
                if (!other.IsAlive) continue;

                // Check head of 'parrot' against all segments of 'other'
                for (int i = 0; i < other.Segments.Count; i++)
                {
                    float collisionDist = (15f * parrot.Size) + (15f * other.Size);
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
            // Spawn death feathers
            foreach (var seg in parrot.Segments)
            {
                SpawnFeather(FeatherType.DEATH_FEATHER, seg, 2.0f);
            }
            
            // If it's a bot, respawn it after a delay or immediately
            if (parrot.IsBot)
            {
                // We'll just remove and re-add in next cycle or something
                // For now, just remove.
            }
        }

        private void SpawnFeather(FeatherType type, Vector2? pos = null, float? value = null)
        {
            var feather = new FeatherEnergy
            {
                Type = type,
                Position = pos ?? new Vector2((float)_random.NextDouble() * MapWidth, (float)_random.NextDouble() * MapHeight),
                Value = value ?? (type == FeatherType.WORLD_FEATHER ? 1.0f : 0.5f)
            };
            _feathers.Add(feather);
        }

        private void UpdateBotAI(BotParrot bot, float deltaTime)
        {
            bot.StateTimer -= deltaTime;
            if (bot.StateTimer <= 0)
            {
                // Decision logic
                // Scan nearby
                var nearbyFeathers = _feathers.Where(f => Vector2.Distance(bot.Position, f.Position) < 500).ToList();
                var nearbyParrots = _parrots.Values.Where(p => p.Id != bot.Id && p.IsAlive && Vector2.Distance(bot.Position, p.Position) < 500).ToList();

                if (nearbyParrots.Any())
                {
                    var closest = nearbyParrots.OrderBy(p => Vector2.Distance(bot.Position, p.Position)).First();
                    if (closest.Energy < bot.Energy) bot.State = BotState.ATTACK;
                    else bot.State = BotState.EVADE;
                    bot.TargetEntityId = closest.Id;
                }
                else if (nearbyFeathers.Any())
                {
                    bot.State = BotState.FEED;
                    bot.TargetPosition = nearbyFeathers.OrderBy(f => Vector2.Distance(bot.Position, f.Position)).First().Position;
                }
                else
                {
                    bot.State = BotState.WANDER;
                    bot.TargetPosition = new Vector2((float)_random.NextDouble() * MapWidth, (float)_random.NextDouble() * MapHeight);
                }
                bot.StateTimer = 1.0f + (float)_random.NextDouble() * 2.0f;
            }

            // Behavior in states
            if (bot.State == BotState.WANDER || bot.State == BotState.FEED)
            {
                if (bot.TargetPosition.HasValue)
                {
                    UpdateInput(bot.Id, bot.TargetPosition.Value.X, bot.TargetPosition.Value.Y, false);
                }
            }
            else if (bot.State == BotState.ATTACK)
            {
                if (_parrots.TryGetValue(bot.TargetEntityId, out var target))
                {
                    // Try to cut off
                    Vector2 targetFuture = target.Position + Vector2.FromAngle(target.Direction) * target.CurrentSpeed * 0.5f;
                    UpdateInput(bot.Id, targetFuture.X, targetFuture.Y, true);
                }
            }
            else if (bot.State == BotState.EVADE)
            {
                 if (_parrots.TryGetValue(bot.TargetEntityId, out var target))
                {
                    Vector2 away = bot.Position - target.Position;
                    Vector2 targetPos = bot.Position + away.Normalized() * 100;
                    UpdateInput(bot.Id, targetPos.X, targetPos.Y, true);
                }
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
