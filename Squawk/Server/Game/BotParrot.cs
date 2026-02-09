using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Squawk.Game
{
    public class BotParrot : Parrot
    {
        public BotState State = BotState.Wander;
        public Vector2 TargetPos;
        public float BoostPulse;
        public BotParrot()
        {
            MaxSegments = 24;
            Name = "Bot";
        }

        public void TickAI(GameWorld world, float dt, float boostCost)
        {
            var nearbyFeathers = world.Feathers.Where(f => Vector2.Distance(f.Position, Position) <= VisionRadius).ToList();
            var nearbyEnemies = world.Parrots.Where(p => p != this && p.IsAlive && Vector2.Distance(p.Position, Position) <= VisionRadius).ToList();
            var imminent = nearbyEnemies.Any(p => Vector2.Distance(p.Position, Position) < Size * 1.4f);
            if (imminent) State = BotState.Evade;
            else if (nearbyEnemies.Any() && nearbyEnemies.Min(p => p.Energy) < Energy && Vector2.Distance(Nearest(nearbyEnemies).Position, Position) < VisionRadius * 0.6f) State = BotState.Attack;
            else if (nearbyFeathers.Any()) State = BotState.Feed;
            else State = BotState.Wander;

            Vector2 desired = Direction;
            IsBoosting = false;
            if (State == BotState.Wander)
            {
                var jitter = new Vector2((float)(world.Rng.NextDouble() - 0.5), (float)(world.Rng.NextDouble() - 0.5));
                desired = Vector2.Normalize(Direction + jitter * 0.6f);
            }
            else if (State == BotState.Feed)
            {
                var target = nearestFeather(nearbyFeathers)?.Position ?? Position + Direction;
                desired = Vector2.Normalize(target - Position);
                IsBoosting = Energy > 2f && boostCost > 0f && world.Rng.NextDouble() < 0.2;
            }
            else if (State == BotState.Attack)
            {
                var enemy = Nearest(nearbyEnemies);
                var ahead = enemy.Position + enemy.Direction * 20f;
                desired = Vector2.Normalize(ahead - Position);
                BoostPulse += dt;
                IsBoosting = Energy > 2f && (BoostPulse % 1.2f) < 0.3f;
            }
            else if (State == BotState.Evade)
            {
                var enemy = Nearest(nearbyEnemies);
                desired = Vector2.Normalize(Position - enemy.Position);
                IsBoosting = Energy > 1f;
            }
            else
            {
                desired = Vector2.Normalize(Direction);
            }
            Update(dt, desired, boostCost);
        }

        Parrot Nearest(List<Parrot> list)
        {
            return list.OrderBy(p => Vector2.DistanceSquared(p.Position, Position)).First();
        }
        FeatherEnergy? nearestFeather(List<FeatherEnergy> list)
        {
            return list.OrderBy(f => f.Value).OrderBy(f => f.Position.LengthSquared()).FirstOrDefault();
        }
    }
}
