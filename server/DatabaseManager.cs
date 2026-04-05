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
                PasswordHash TEXT NOT NULL
            )");
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
