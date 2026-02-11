using System;
using System.Collections.Generic;
using System.Linq;
using Squawk.Server.Models;

namespace Squawk.Server
{
    public class GameEngine
    {
        public const float MapRadius = 7500f; // 5x larger than before
        public const int MaxFeathers = 12500; // Half of 25000
        public const int BotCount = 40; // More bots for larger map

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
            float angle = (float)(_random.NextDouble() * Math.PI * 2);
            float dist = (float)(Math.Sqrt(_random.NextDouble()) * MapRadius);
            return new Vector2(
                (float)(Math.Cos(angle) * dist) + MapRadius,
                (float)(Math.Sin(angle) * dist) + MapRadius
            );
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

                    // Circular Map boundaries
                    float distFromCenter = Vector2.Distance(parrot.Position, new Vector2(MapRadius, MapRadius));
                    if (distFromCenter > MapRadius)
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
                // Reduce segment distance for overlapping look
                float minDist = (Parrot.SegmentDistance * 0.4f) * parrot.Size;

                if (dist > minDist)
                {
                    Vector2 dir = (prev - curr).Normalized();
                    parrot.Segments[i] = prev - dir * minDist;
                }
            }
        }

        private void CheckFeatherCollisions(Parrot parrot)
        {
            float pickupRadius = 40f * parrot.Size;
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
                // Skip the first few segments to avoid immediate head-on weirdness if needed
                for (int i = 0; i < other.Segments.Count; i++)
                {
                    float collisionDist = (18f * parrot.Size) + (18f * other.Size);
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
                // We'll need a reference to the socket or a way to send this message
                // For now, the Program.cs will handle sending the death message when it sees IsAlive = false
            }

            // Spawn death feathers
            foreach (var seg in parrot.Segments)
            {
                if (_random.NextDouble() < 0.5) // Don't spawn for every segment to avoid clutter
                    SpawnFeather(FeatherType.DEATH_FEATHER, seg, 2.0f);
            }
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
                    if (dist < 150)
                    {
                        Vector2 away = bot.Position - seg;
                        Vector2 evadePos = bot.Position + away.Normalized() * 200;
                        UpdateInput(bot.Id, evadePos.X, evadePos.Y, bot.Energy > 50);
                        return; // Done for this tick
                    }
                }
            }

            // 2. MAP BOUNDARY AVOIDANCE
            float distFromCenter = Vector2.Distance(bot.Position, new Vector2(MapRadius, MapRadius));
            if (distFromCenter > MapRadius * 0.8f)
            {
                UpdateInput(bot.Id, MapRadius, MapRadius, false);
                return;
            }

            // 3. REGULAR BEHAVIOR
            if (bot.StateTimer <= 0)
            {
                var nearbyFeathers = _feathers.Where(f => Vector2.Distance(bot.Position, f.Position) < 800).ToList();
                
                if (nearbyFeathers.Any())
                {
                    bot.State = BotState.FEED;
                    bot.TargetPosition = nearbyFeathers.OrderBy(f => Vector2.Distance(bot.Position, f.Position)).First().Position;
                }
                else
                {
                    bot.State = BotState.WANDER;
                    // Wander towards center-ish
                    float angle = (float)(_random.NextDouble() * Math.PI * 2);
                    float dist = (float)(_random.NextDouble() * MapRadius * 0.5f);
                    bot.TargetPosition = new Vector2(
                        (float)(Math.Cos(angle) * dist) + MapRadius,
                        (float)(Math.Sin(angle) * dist) + MapRadius
                    );
                }
                bot.StateTimer = 0.5f + (float)_random.NextDouble() * 1.0f;
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
