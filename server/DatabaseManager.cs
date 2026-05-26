using Microsoft.Data.Sqlite;
using Dapper;
using SquawkServer.Models;
using BCrypt.Net;

namespace SquawkServer;

public class DatabaseManager
{
    private readonly string _connectionString;
    private readonly object _initLock = new();
    private bool _initialized;

    public DatabaseManager(string dbPath = "squawk.db")
    {
        _connectionString = $"Data Source={dbPath};";
    }

    private void EnsureInitialized()
    {
        if (_initialized) return;

        lock (_initLock)
        {
            if (_initialized) return;
            Initialize();
            _initialized = true;
        }
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
            );
            CREATE TABLE IF NOT EXISTS Scores (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Username TEXT NOT NULL,
                Score INTEGER NOT NULL,
                Timestamp DATETIME DEFAULT CURRENT_TIMESTAMP
            );
            CREATE INDEX IF NOT EXISTS idx_scores_timestamp ON Scores(Timestamp);
            CREATE INDEX IF NOT EXISTS idx_scores_score ON Scores(Score);
        ");
    }

    public void AddScore(string username, int score)
    {
        EnsureInitialized();
        using var connection = new SqliteConnection(_connectionString);
        connection.Execute("INSERT INTO Scores (Username, Score) VALUES (@username, @score)", 
            new { username = username.Trim(), score });
    }

    public IEnumerable<dynamic> GetTop10()
    {
        EnsureInitialized();
        using var connection = new SqliteConnection(_connectionString);
        return connection.Query(@"
            SELECT Username as Name, MAX(Score) as Score 
            FROM Scores 
            GROUP BY Username 
            ORDER BY Score DESC 
            LIMIT 10");
    }

    public IEnumerable<dynamic> GetTop24h()
    {
        EnsureInitialized();
        using var connection = new SqliteConnection(_connectionString);
        return connection.Query(@"
            SELECT Username as Name, MAX(Score) as Score 
            FROM Scores 
            WHERE Timestamp >= datetime('now', '-1 day')
            GROUP BY Username 
            ORDER BY Score DESC 
            LIMIT 10");
    }

    public void SavePlayerState(string username, float x, float y, int score)
    {
        EnsureInitialized();
        using var connection = new SqliteConnection(_connectionString);
        connection.Execute(@"
            UPDATE Users 
            SET LastPosX = @x, LastPosY = @y, LastScore = @score 
            WHERE Username = @username",
            new { username = username.Trim(), x, y, score });
        
        if (score > 0) AddScore(username, score);
    }

    public (float x, float y, int score) GetPlayerState(string username)
    {
        EnsureInitialized();
        using var connection = new SqliteConnection(_connectionString);
        var state = connection.QueryFirstOrDefault(@"
            SELECT LastPosX as x, LastPosY as y, LastScore as score 
            FROM Users WHERE Username = @username",
            new { username = username.Trim() });
        
        if (state != null)
        {
            return ( (float)state.x, (float)state.y, (int)state.score );
        }
        return (1500f, 1500f, 0);
    }

    public bool RegisterUser(string username, string password, out string errorMessage)
    {
        EnsureInitialized();
        errorMessage = string.Empty;
        using var connection = new SqliteConnection(_connectionString);
        try
        {
            username = username.Trim();

            if (string.IsNullOrWhiteSpace(username) || username.Length < 3)
            {
                errorMessage = "Nazwa użytkownika musi mieć min. 3 znaki";
                return false;
            }

            if (string.IsNullOrWhiteSpace(password) || password.Length < 4)
            {
                errorMessage = "Hasło musi mieć min. 4 znaki";
                return false;
            }

            var hash = BCrypt.Net.BCrypt.HashPassword(password);
            connection.Execute("INSERT INTO Users (Username, PasswordHash) VALUES (@Username, @PasswordHash)",
                new { Username = username, PasswordHash = hash });
            return true;
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            errorMessage = "Użytkownik o tej nazwie już istnieje";
            return false;
        }
        catch
        {
            errorMessage = "Wystąpił błąd podczas tworzenia konta";
            return false;
        }
    }

    public User? AuthenticateUser(string username, string password)
    {
        EnsureInitialized();
        username = username.Trim();
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
        EnsureInitialized();
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        connection.Execute("DELETE FROM Users");
        connection.Execute("DELETE FROM sqlite_sequence WHERE name='Users'");
    }
}
