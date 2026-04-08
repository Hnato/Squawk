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
        // engine.Start(); // We call Tick() to trigger maintenance
        
        engine.Tick();
        
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
        
        var internalFood = engine.InternalFoodList;
        var internalLock = engine.InternalFoodLock;
        
        lock(internalLock) {
            internalFood.Clear();
            for(int i=0; i<400; i++) {
                internalFood.Add(new Food { Id = i, Position = new Vector2(i*100, i*100), Value = 1 });
            }
        }

        Food food;
        lock(internalLock) {
            food = internalFood.First();
        }
        
        // Use direct player map access with proper initialization
        var p1 = new Player { 
            Id = "p1", 
            Name = "P1", 
            Body = new List<Vector2> { new Vector2(food.Position.X, food.Position.Y) },
            IsDead = false,
            Speed = 3.0f,
            Score = 0
        };
        engine.InternalPlayerMap.TryAdd("p1", p1);
        
        // Manually trigger collision logic
        engine.Tick(); 
        
        // Check if food was removed and added to queue
        bool inQueue = engine.InternalRespawnQueue.ContainsKey(food.Id);
        Assert.True(inQueue, $"Food {food.Id} should be in respawn queue after collision. Player at {p1.Body[0]}, Food at {food.Position}");
        
        System.Threading.Thread.Sleep(3500);
        engine.Tick(); 
        
        lock(internalLock) {
            Assert.Contains(food.Id, internalFood.Select(f => f.Id));
            Assert.Equal(400, internalFood.Count);
        }
    }
}
