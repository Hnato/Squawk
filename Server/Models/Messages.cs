using System.Collections.Generic;

namespace Squawk.Server.Models
{
    public static class MessageType
    {
        public const string Welcome = "welcome";
        public const string Join = "join";
        public const string Update = "update";
        public const string Input = "input";
        public const string Death = "death";
        public const string Leaderboard = "leaderboard";
    }

    public class BaseMessage
    {
        public string Type { get; set; } = string.Empty;
    }

    public class WelcomeMessage : BaseMessage
    {
        public string PlayerId { get; set; } = string.Empty;
        public float MapRadius { get; set; }
        public WelcomeMessage() => Type = MessageType.Welcome;
    }

    public class JoinMessage : BaseMessage
    {
        public string Name { get; set; } = "Papuga";
        public JoinMessage() => Type = MessageType.Join;
    }

    public class InputMessage : BaseMessage
    {
        public float TargetX { get; set; }
        public float TargetY { get; set; }
        public bool IsBoosting { get; set; }
        public InputMessage() => Type = MessageType.Input;
    }

    public class GameUpdateMessage : BaseMessage
    {
        public List<ParrotData> Parrots { get; set; } = new List<ParrotData>();
        public List<FeatherData> Feathers { get; set; } = new List<FeatherData>();
        public GameUpdateMessage() => Type = MessageType.Update;
    }

    public class ParrotData
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public float X { get; set; }
        public float Y { get; set; }
        public float Dir { get; set; }
        public float Energy { get; set; }
        public float Size { get; set; }
        public List<Vector2> Segments { get; set; } = new List<Vector2>();
        public bool IsBoosting { get; set; }
    }

    public class FeatherData
    {
        public string Id { get; set; } = string.Empty;
        public float X { get; set; }
        public float Y { get; set; }
        public float Value { get; set; }
        public FeatherType Type { get; set; }
    }

    public class LeaderboardEntry
    {
        public string Name { get; set; } = string.Empty;
        public float Score { get; set; }
    }

    public class LeaderboardMessage : BaseMessage
    {
        public List<LeaderboardEntry> Entries { get; set; } = new List<LeaderboardEntry>();
        public LeaderboardMessage() => Type = MessageType.Leaderboard;
    }
}
