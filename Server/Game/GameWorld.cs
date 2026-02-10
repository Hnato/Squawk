using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace Squawk.Game
{
    public struct Bounds
    {
        public float X;
        public float Y;
        public float W;
        public float H;
        public bool Outside(Vector2 p) => p.X < X || p.Y < Y || p.X > X + W || p.Y > Y + H;
    }

    public class GameWorld
    {
        public class LeaderboardEntry
        {
            public required string Name { get; set; }
            public float Energy { get; set; }
        }
        public Parrot? Player;
        public List<Parrot> Parrots = new();
        public List<FeatherEnergy> Feathers = new();
        public float MapRadius;
        public Random Rng = new Random();
        public float BoostCost = 6f;
        public float WorldFeatherRate = 1f;
        float spawnAccum;
        public int MaxFeathers = 500;
        public int MaxPlayers = 20;
        public int BotWorkers = 4;
        public bool ParallelBots = false;

        public void Initialize(int botCount)
        {
            MapRadius = 2500f;
            Parrots.Clear();
            // Temporarily disabled bots
            /*
            for (int i = 0; i < botCount; i++)
            {
                var b = new BotParrot
                {
                    Position = SafeRandomPosition(100f),
                    Direction = Vector2.Normalize(new Vector2((float)(Rng.NextDouble() - 0.5), (float)(Rng.NextDouble() - 0.5))),
                    Energy = 8f + (float)Rng.NextDouble() * 6f,
                    Hue = (float)Rng.Next(0, 360)
                };
                b.InitializeSegments(10);
                Parrots.Add(b);
            }
            */
            Feathers.Clear();
            for (int i = 0; i < 500; i++)
            {
                Feathers.Add(new FeatherEnergy { 
                    Type = FeatherType.WorldFeather, 
                    Position = SafeRandomPosition(50f), 
                    Value = 1f + (float)Rng.NextDouble() * 2f,
                    Hue = (float)Rng.Next(0, 360)
                });
            }
        }

        Vector2 RandInside()
        {
            var angle = Rng.NextDouble() * Math.PI * 2;
            var r = Math.Sqrt(Rng.NextDouble()) * MapRadius;
            return new Vector2((float)(Math.Cos(angle) * r), (float)(Math.Sin(angle) * r));
        }

        public void ApplyInput(string playerId, InputState input)
        {
            _playerInputs[playerId] = input;
        }
        
        private ConcurrentDictionary<string, InputState> _playerInputs = new();

        public void Update(float dt)
        {
            // Update Players
            foreach (var kvp in _playerInputs)
            {
                var p = GetPlayer(kvp.Key);
                if (p != null && p.IsAlive)
                {
                    var input = kvp.Value;
                    float screenCX = input.w / 2;
                    float screenCY = input.h / 2;
                    float dx = input.x - screenCX;
                    float dy = input.y - screenCY;
                    Vector2 desired = new Vector2(dx, dy);
                    
                    bool boosting = input.space; // Space is boost
                    
                    UpdateParrot(p, dt, desired, boosting);
                }
            }

            // TickBots(dt);
            TickBots(dt);

            if (Feathers.Count < MaxFeathers)
            {
                spawnAccum += dt;
                while (spawnAccum > 1f / WorldFeatherRate)
                {
                    spawnAccum -= 1f / WorldFeatherRate;
                    Feathers.Add(new FeatherEnergy { 
                        Type = FeatherType.WorldFeather, 
                        Position = SafeRandomPosition(50f), 
                        Value = 1.2f,
                        Hue = (float)Rng.Next(0, 360)
                    });
                }
            }
            HandleFeatherPickup();
            HandleCollisions();
            Parrots = Parrots.Where(p => p.IsAlive).ToList();
        }

        public void AddPlayer(string playerId, string name)
        {
            if (Parrots.Count >= MaxPlayers) return;
            // Check if player already exists
            if (Parrots.Any(p => p.Name == playerId)) return;

            var p = new Parrot
            {
                Position = SafeRandomPosition(100f),
                Hue = 190f,
                Name = playerId, // Using ID as Name for internal logic for now
                DisplayName = string.IsNullOrWhiteSpace(name) ? "Gracz" : name.Trim(),
                Energy = 10f
            };
            p.InitializeSegments(10);
            Parrots.Insert(0, p);
        }

        Vector2 SafeRandomPosition(float minDistance)
        {
            for (int attempts = 0; attempts < 1000; attempts++)
            {
                var pos = RandInside();
                if (Parrots.All(p => Vector2.Distance(p.Position, pos) >= minDistance))
                    return pos;
            }
            return RandInside();
        }

        public Parrot? GetPlayer(string name)
        {
            return Parrots.FirstOrDefault(p => p.Name == name);
        }

        // Helper to update a specific parrot (player or bot)
        public void UpdateParrot(Parrot p, float dt, Vector2 desired, bool boosting)
        {
             p.IsBoosting = boosting;
             p.Update(dt, desired, BoostCost);
             if (p.IsBoosting) SpawnBoostFeather(p);
        }

        void TickBots(float dt)
        {
            var bots = Parrots.Where(p => p is BotParrot).Cast<BotParrot>().Where(b => b.IsAlive).ToList();
            foreach (var bot in bots)
            {
                bot.TickAI(this, dt, BoostCost);
                if (bot.IsBoosting) SpawnBoostFeather(bot);
            }
        }


        void SpawnBoostFeather(Parrot p)
        {
            var tail = p.Segments.Count > 0 ? p.Segments[^1].Position : p.Position - p.Direction * 10f;
            Feathers.Add(new FeatherEnergy { Type = FeatherType.BoostFeather, Position = tail, Value = 0.6f });
        }

        void HandleFeatherPickup()
        {
            var toRemove = new List<FeatherEnergy>();
            foreach (var p in Parrots)
            {
                if (!p.IsAlive) continue;
                foreach (var f in Feathers)
                {
                    if (Vector2.Distance(p.Position, f.Position) <= p.Size + f.Radius)
                    {
                        p.Energy += f.Value;
                        toRemove.Add(f);
                    }
                }
            }
            foreach (var f in toRemove.Distinct()) Feathers.Remove(f);
        }

        void HandleCollisions()
        {
            foreach (var p in Parrots.ToList())
            {
                if (!p.IsAlive) continue;
                if (p.Position.LengthSquared() > MapRadius * MapRadius)
                {
                    KillParrot(p);
                    continue;
                }
                foreach (var other in Parrots.ToList())
                {
                    if (other == p || !other.IsAlive) continue;
                    for (int i = 1; i < other.Segments.Count; i++)
                    {
                        var s = other.Segments[i];
                        if (Vector2.Distance(p.Position, s.Position) <= p.Size + s.Radius)
                        {
                            KillParrot(p);
                            goto nextParrot;
                        }
                    }
                }
            nextParrot: ;
            }
        }

        void KillParrot(Parrot p)
        {
            p.IsAlive = false;
            foreach (var s in p.Segments)
            {
                Feathers.Add(new FeatherEnergy { Type = FeatherType.DeathFeather, Position = s.Position, Value = 1f });
            }
            if (p is BotParrot)
            {
                /* Temporarily disabled bot respawn
                var b = new BotParrot
                {
                    Position = SafeRandomPosition(100f),
                    Direction = Vector2.Normalize(new Vector2((float)(Rng.NextDouble() - 0.5), (float)(Rng.NextDouble() - 0.5))),
                    Energy = 8f + (float)Rng.NextDouble() * 6f,
                    Hue = (float)Rng.Next(0, 360)
                };
                b.InitializeSegments(10);
                Parrots.Add(b);
                */
            }
        }

        public List<LeaderboardEntry> Leaderboard()
        {
            return Parrots
                .Select((p, i) => new LeaderboardEntry { Name = p.Name, Energy = p.Energy })
                .OrderByDescending(x => x.Energy)
                .ToList();
        }
    }
}
