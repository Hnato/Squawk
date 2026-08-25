using System.Collections.Generic;

namespace Server.Game;

public class Vector2D
{
    public float X { get; set; }
    public float Y { get; set; }

    public Vector2D(float x, float y)
    {
        X = x;
        Y = y;
    }
}

public class PlayerData
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string SkinColor { get; set; } = "#22c55e";
    public string SkinPattern { get; set; } = "ara";
    public bool IsBot { get; set; }
    public List<Vector2D> Body { get; set; } = new();
    public float Angle { get; set; } // Radian direction
    public bool IsBoosting { get; set; }
    public int Score { get; set; }
    public bool IsDead { get; set; }
    
    // Server-side only states
    public float MoveSpeed { get; set; } = 3.0f;
    public int BoostCostTicks { get; set; } = 0;
    public int BotTargetFoodId { get; set; } = -1;
    public int BotDecisionTicks { get; set; } = 0;
    public float BotSteerStrength { get; set; } = 0.08f;
}

public class Food
{
    public int Id { get; set; }
    public Vector2D Position { get; set; } = new(0, 0);
    public string Color { get; set; } = "#ff0000";
    public int Value { get; set; } = 10;
}

public class GameStateDto
{
    public List<PlayerData> Players { get; set; } = new();
    public List<Food> Food { get; set; } = new();
}
