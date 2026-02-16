using System;
using System.Collections.Generic;

namespace Squawk.Server.Models
{
    public enum FeatherType
    {
        WORLD_FEATHER,
        BOOST_FEATHER,
        DEATH_FEATHER
    }

    public class FeatherEnergy
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public Vector2 Position { get; set; }
        public float Value { get; set; }
        public FeatherType Type { get; set; }
    }

    public class Parrot
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = "Papuga";
        public Vector2 Position { get; set; }
        public float Direction { get; set; } // Angle in radians
        public float Energy { get; set; } = 10f;
        public List<Vector2> Segments { get; set; } = new List<Vector2>();
        public bool IsBoosting { get; set; }
        public bool IsAlive { get; set; } = true;
        public bool IsBot { get; set; }

        // Constants from spec
        public const float BaseSpeed = 150f;
        public const float BoostSpeed = 300f;
        public const float MinEnergyForBoost = 5f;
        public const float BoostCost = 5f; 
        public const float SegmentDistance = 30f;

        public float CurrentSpeed => IsBoosting && Energy > MinEnergyForBoost ? BoostSpeed : BaseSpeed;
        public float Size => Energy < 40f ? 1f + (Energy / 100f) : 1.5f + (Energy / 120f);
        public float TurnRate => Math.Max(0.8f, 5.0f / Size); 
        public int MaxSegments => 5 + (int)(Energy * 0.8f); 
        public Parrot(string id, string name, Vector2 startPos)
        {
            Id = id;
            Name = name;
            Position = startPos;
            Direction = 0;
            Segments.Add(startPos); 
        }
    }

    public enum BotState
    {
        WANDER,
        FEED,
        ATTACK,
        EVADE,
        TRAPPED
    }

    public class BotParrot : Parrot
    {
        public BotState State { get; set; } = BotState.WANDER;
        public Vector2? TargetPosition { get; set; }
        public string? TargetEntityId { get; set; }
        public float StateTimer { get; set; }

        public BotParrot(string id, string name, Vector2 startPos) : base(id, name, startPos)
        {
            IsBot = true;
        }
    }
}
