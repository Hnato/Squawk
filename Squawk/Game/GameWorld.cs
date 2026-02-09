using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

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
            public string Name { get; set; }
            public float Energy { get; set; }
        }
        public Parrot? Player;
        public List<Parrot> Parrots = new();
        public List<FeatherEnergy> Feathers = new();
        public Bounds MapBounds;
        public Random Rng = new Random();
        public float BoostCost = 6f;
        public float WorldFeatherRate = 4f;
        float spawnAccum;
        public int MaxPlayers = 20;
        public int BotWorkers = 4;
        public bool ParallelBots = false;

        public void Initialize(int botCount)
        {
            MapBounds = new Bounds { X = -1500, Y = -1500, W = 3000, H = 3000 };
            Parrots.Clear();
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
            Feathers.Clear();
            for (int i = 0; i < 120; i++)
            {
                Feathers.Add(new FeatherEnergy { Type = FeatherType.WorldFeather, Position = SafeRandomPosition(50f), Value = 1f + (float)Rng.NextDouble() * 2f });
            }
        }

        Vector2 RandInside()
        {
            var x = MapBounds.X + (float)Rng.NextDouble() * MapBounds.W;
            var y = MapBounds.Y + (float)Rng.NextDouble() * MapBounds.H;
            return new Vector2(x, y);
        }

        public async Task StepAsync(float dt, Vector2 playerDesired, bool playerBoosting)
        {
            if (Player != null && Player.IsAlive)
            {
                Player.IsBoosting = playerBoosting;
                Player.Update(dt, playerDesired, BoostCost);
                if (Player.IsBoosting) SpawnBoostFeather(Player);
            }
            await TickBotsAsync(dt);
            spawnAccum += dt;
            while (spawnAccum > 1f / WorldFeatherRate)
            {
                spawnAccum -= 1f / WorldFeatherRate;
                Feathers.Add(new FeatherEnergy { Type = FeatherType.WorldFeather, Position = SafeRandomPosition(50f), Value = 1.2f });
            }
            HandleFeatherPickup();
            HandleCollisions();
            Parrots = Parrots.Where(p => p.IsAlive).ToList();
        }

        public void AddPlayer(string name)
        {
            if (Parrots.Count >= MaxPlayers) return;
            var p = new Parrot
            {
                Position = SafeRandomPosition(100f),
                Hue = 190f,
                Name = string.IsNullOrWhiteSpace(name) ? "Gracz" : name.Trim(),
                Energy = 10f
            };
            p.InitializeSegments(10);
            Player = p;
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

        async Task TickBotsAsync(float dt)
        {
            var bots = Parrots.Where(p => p is BotParrot).Cast<BotParrot>().Where(b => b.IsAlive).ToList();
            var newFeathers = new List<FeatherEnergy>();
            if (ParallelBots && BotWorkers > 1)
            {
                var workers = Math.Max(1, Math.Min(BotWorkers, bots.Count));
                var tasks = new List<Task>();
                for (int w = 0; w < workers; w++)
                {
                    var start = w * bots.Count / workers;
                    var end = (w + 1) * bots.Count / workers;
                    tasks.Add(Task.Run(() =>
                    {
                        for (int i = start; i < end; i++)
                        {
                            var bot = bots[i];
                            bot.TickAI(this, dt, BoostCost);
                            if (bot.IsBoosting)
                            {
                                var tail = bot.Segments.Count > 0 ? bot.Segments[^1].Position : bot.Position - bot.Direction * 10f;
                                var feather = new FeatherEnergy { Type = FeatherType.BoostFeather, Position = tail, Value = 0.6f };
                                lock (newFeathers) newFeathers.Add(feather);
                            }
                        }
                    }));
                }
                await Task.WhenAll(tasks);
            }
            else
            {
                foreach (var bot in bots)
                {
                    bot.TickAI(this, dt, BoostCost);
                    if (bot.IsBoosting)
                    {
                        var tail = bot.Segments.Count > 0 ? bot.Segments[^1].Position : bot.Position - bot.Direction * 10f;
                        newFeathers.Add(new FeatherEnergy { Type = FeatherType.BoostFeather, Position = tail, Value = 0.6f });
                    }
                }
            }
            if (newFeathers.Count > 0) Feathers.AddRange(newFeathers);
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
            foreach (var p in Parrots)
            {
                if (!p.IsAlive) continue;
                if (MapBounds.Outside(p.Position))
                {
                    KillParrot(p);
                    continue;
                }
                foreach (var other in Parrots)
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
