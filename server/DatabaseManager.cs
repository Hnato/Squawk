using Microsoft.Data.Sqlite;
using Dapper;
using SquawkServer.Models;
using BCrypt.Net;
namespace SquawkServer;

public class DatabaseManager
{
    private readonly string _connectionString;

    public DatabaseManager(string dbPath = "squawk.db")
    {
        _connectionString = $"Data Source={dbPath};";
        Initialize();
    }

    private void Initialize()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        connection.Execute(@"
            CREATE TABLE IF NOT EXISTS Users (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Username TEXT UNIQUE NOT NULL,
                PasswordHash TEXT NOT NULL,
                LastPosX REAL DEFAULT 1500,
                LastPosY REAL DEFAULT 1500,
                LastScore INTEGER DEFAULT 0
            )");
    }

    public void SavePlayerState(string username, float x, float y, int score)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Execute(@"
            UPDATE Users 
            SET LastPosX = @x, LastPosY = @y, LastScore = @score 
            WHERE Username = @username",
            new { username, x, y, score });
    }

    public (float x, float y, int score) GetPlayerState(string username)
    {
        using var connection = new SqliteConnection(_connectionString);
        var state = connection.QueryFirstOrDefault(@"
            SELECT LastPosX as x, LastPosY as y, LastScore as score 
            FROM Users WHERE Username = @username",
            new { username });
        
        if (state != null)
        {
            return ( (float)state.x, (float)state.y, (int)state.score );
        }
        return (1500f, 1500f, 0);
    }

    public bool RegisterUser(string username, string password)
    {
        using var connection = new SqliteConnection(_connectionString);
        try
        {
            var hash = BCrypt.Net.BCrypt.HashPassword(password);
            connection.Execute("INSERT INTO Users (Username, PasswordHash) VALUES (@Username, @PasswordHash)",
                new { Username = username, PasswordHash = hash });
            return true;
        }
        catch
        {
            return false;
        }
    }

    public User? AuthenticateUser(string username, string password)
    {
        using var connection = new SqliteConnection(_connectionString);
        var user = connection.QueryFirstOrDefault<User>("SELECT * FROM Users WHERE Username = @Username",
            new { Username = username });

        if (user != null && BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            return user;
        }
        return null;
    }

    public void ResetDatabase()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        connection.Execute("DELETE FROM Users");
        connection.Execute("DELETE FROM sqlite_sequence WHERE name='Users'");
    }
}
