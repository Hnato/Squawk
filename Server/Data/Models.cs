using System;
using System.Collections.Generic;

namespace Server.Data
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string SkinColor { get; set; } = "#22c55e"; // default green
        public string SkinPattern { get; set; } = "solid";
    }

    public class ScoreEntry
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int Score { get; set; }
        public DateTime Date { get; set; }
        public User User { get; set; } = null!;
    }
}
