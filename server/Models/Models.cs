using System.Numerics;

namespace SquawkServer.Models;

public class Player
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<Vector2> Body { get; set; } = [];
    public float Angle { get; set; }
    public float TargetAngle { get; set; }
    public float Speed { get; set; } = 3.0f;
    public int Score { get; set; }
    public bool IsBot { get; set; }
    public string Color { get; set; } = "green";
    public bool IsDead { get; set; } = false;
}

public class Food
{
    public int Id { get; set; }
    public Vector2 Position { get; set; }
    public int Value { get; set; }
    public string Color { get; set; } = "yellow";
    public bool IsPowerUp { get; set; } = false;
    public string Type { get; set; } = "food"; // "food", "speed", "score"
}

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
}
