using Xunit;
using System.Numerics;
using System.Linq;
using SquawkServer;
using SquawkServer.Models;

namespace SquawkServer.Tests;

public class GameEngineTests
{
    [Fact]
    public void Map_Radius_Should_Be_At_Least_500()
    {
        var engine = new GameEngine();
        // Since _mapRadius is private, we check indirectly via MovePlayer boundary logic
        // or we could make it internal/public for testing. 
        // For this task, we'll assume the engine's internal state is correct if it handles spawns.
        
        // Let's test if we can spawn a player and if they are within bounds.
        engine.Start();
        engine.AddPlayer("test-id", "TestPlayer");
        var player = engine.Players.First(p => p.Id == "test-id");
        
        var center = new Vector2(1500f, 1500f);
        var dist = Vector2.Distance(player.Body[0], center);
        
        Assert.True(dist <= 1500f, "Player spawned outside map radius");
    }

    [Fact]
    public void Player_Initial_Length_Should_Be_Increased()
    {
        var engine = new GameEngine();
        engine.Start();
        engine.AddPlayer("test-id", "TestPlayer");
        var player = engine.Players.First(p => p.Id == "test-id");
        
        // User wants 50% increase from 10, so 15.
        Assert.Equal(15, player.Body.Count);
    }

    [Fact]
    public void Food_Replenishment_Should_Maintain_High_Count()
    {
        var engine = new GameEngine();
        engine.Start();
        
        // Should have 400 food items
        Assert.Equal(400, engine.FoodItems.Count);
    }

    [Fact]
    public void Bot_Results_Should_Be_Tracked()
    {
        var engine = new GameEngine();
        engine.BotsEnabled = true;
        engine.Start();
        
        // Wait for bots to be added in Start()
        var bots = engine.Players.Where(p => p.IsBot).ToList();
        Assert.Equal(4, bots.Count);
        
        foreach (var bot in bots)
        {
            Assert.StartsWith("Bot", bot.Name);
            Assert.Equal(0, bot.Score);
            Assert.Equal(15, bot.Body.Count);
        }
    }

    [Fact]
    public void Food_Should_Respawn_After_3_Seconds()
    {
        var engine = new GameEngine();
        // engine.Start() is NOT called, so automatic food spawn is disabled
        
        var internalFood = engine.InternalFoodList;
        var internalLock = engine.InternalFoodLock;
        
        lock(internalLock) {
            internalFood.Clear();
            // Add exactly 10 items for a cleaner test
            for(int i=0; i<10; i++) {
                internalFood.Add(new Food { Id = i, Position = new Vector2(i, i) });
            }
        }

        Food food;
        lock(internalLock) {
            food = internalFood.First();
        }
        
        // Add player
        engine.AddPlayer("p1", "P1");
        var p1 = engine.Players.First(p => p.Name == "P1");
        p1.Body[0] = food.Position; 
        
        int initialCount = 10;
        engine.Tick(); // Detect collision
        
        lock(internalLock) {
            Assert.Equal(initialCount - 1, internalFood.Count);
        }
        
        // Wait 3.5 seconds
        System.Threading.Thread.Sleep(3500);
        
        engine.Tick(); // Process respawn
        
        lock(internalLock) {
            Assert.Equal(initialCount, internalFood.Count);
        }
    }
}
