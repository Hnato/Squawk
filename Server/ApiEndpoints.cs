using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Server.Data;

namespace Server;

public static class ApiEndpoints
{
    public static void MapEndpoints(this WebApplication app)
    {
        app.MapGet("/api/status", () => "Serwer działa!");

        app.MapPost("/api/register", async (RegisterRequest req) =>
        {
            using var db = new SquawkDbContext();
            var usernameTrimmed = req.Username.Trim();
            if (await db.Users.AnyAsync(u => u.Username.ToLower() == usernameTrimmed.ToLower()))
            {
                return Results.BadRequest(new { message = "Konto z tym nickiem już istnieje." });
            }

            var user = new User
            {
                Username = usernameTrimmed,
                PasswordHash = HashPassword(req.Password),
                SkinColor = "#22c55e"
            };

            db.Users.Add(user);
            await db.SaveChangesAsync();
            return Results.Ok(new { user.Username, user.SkinColor, user.SkinPattern });
        });

        app.MapPost("/api/login", async (LoginRequest req) =>
        {
            using var db = new SquawkDbContext();
            var usernameTrimmed = req.Username.Trim();
            var hash = HashPassword(req.Password);
            
            var existingUser = await db.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == usernameTrimmed.ToLower());
            if (existingUser == null)
            {
                return Results.BadRequest(new { message = "Konto z tym nickiem nie istnieje. Zarejestruj się." });
            }

            if (existingUser.PasswordHash != hash)
            {
                return Results.BadRequest(new { message = "Niepoprawne hasło." });
            }

            return Results.Ok(new { Username = existingUser.Username, SkinColor = existingUser.SkinColor, SkinPattern = existingUser.SkinPattern });
        });

        app.MapPost("/api/saveskin", async (SaveSkinRequest req) =>
        {
            using var db = new SquawkDbContext();
            var user = await db.Users.FirstOrDefaultAsync(u => u.Username == req.Username);
            if (user != null)
            {
                user.SkinColor = req.Color;
                user.SkinPattern = req.Pattern;
                await db.SaveChangesAsync();
                return Results.Ok();
            }
            return Results.NotFound();
        });

        app.MapGet("/api/scores/top24h", async () =>
        {
            using var db = new SquawkDbContext();
            var yesterday = System.DateTime.UtcNow.AddDays(-1);
            var scores = await db.Scores
                .Include(s => s.User)
                .Where(s => s.Date >= yesterday)
                .OrderByDescending(s => s.Score)
                .Take(10)
                .Select(s => new { s.User.Username, s.Score, s.Date })
                .ToListAsync();
            return Results.Ok(scores);
        });

        app.MapGet("/api/scores/user/{username}", async (string username) =>
        {
            using var db = new SquawkDbContext();
            var scores = await db.Scores
                .Include(s => s.User)
                .Where(s => s.User.Username == username)
                .OrderByDescending(s => s.Score)
                .Take(10)
                .Select(s => new { s.Score, s.Date })
                .ToListAsync();
            return Results.Ok(scores);
        });

        app.MapPost("/api/scores", async (SaveScoreRequest req) =>
        {
            using var db = new SquawkDbContext();
            var user = await db.Users.FirstOrDefaultAsync(u => u.Username == req.Username);
            if (user != null)
            {
                var entry = new ScoreEntry
                {
                    UserId = user.Id,
                    Score = req.Score,
                    Date = System.DateTime.UtcNow
                };
                db.Scores.Add(entry);
                await db.SaveChangesAsync();
                return Results.Ok();
            }
            return Results.NotFound();
        });
    }

    private static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return System.Convert.ToBase64String(bytes);
    }
}

public class RegisterRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class SaveSkinRequest
{
    public string Username { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string Pattern { get; set; } = string.Empty;
}

public class SaveScoreRequest
{
    public string Username { get; set; } = string.Empty;
    public int Score { get; set; }
}
