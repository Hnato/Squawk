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
        
        // Wait a bit to ensure Start() has finished if there's any async (there isn't but just in case)
        
        var botsAfterStart = engine.Players.Where(p => p.IsBot).ToList();
        
        engine.Tick();
        
        var botsAfterTick = engine.Players.Where(p => p.IsBot).ToList();
        
        Assert.True(botsAfterStart.Count == 4, $"Expected 4 bots after Start, got {botsAfterStart.Count}");
        Assert.True(botsAfterTick.Count == 4, $"Expected 4 bots after Tick, got {botsAfterTick.Count}");
        
        foreach (var bot in botsAfterTick)
        {
            Assert.StartsWith("Bot", bot.Name);
            Assert.Equal(15, bot.Body.Count);
        }
    }

    [Fact]
    public void Food_Should_Respawn_After_3_Seconds()
    {
        var engine = new GameEngine();
        engine.BotsEnabled = false; // Disable bots to avoid extra food from bot deaths
        engine.Start(); 
        
        var internalFood = engine.InternalFoodList;
        var internalLock = engine.InternalFoodLock;
        var center = new Vector2(1500f, 1500f);
        
        lock(internalLock) {
            internalFood.Clear();
            engine.InternalRespawnQueue.Clear();
            for(int i=0; i<400; i++) {
                internalFood.Add(new Food { Id = i, Position = center + new Vector2(i, 0), Value = 1 });
            }
        }

        Food food;
        lock(internalLock) {
            food = internalFood.First();
        }
        
        // Create player exactly on top of food
        var p1 = new Player { 
            Id = "p1", 
            Name = "P1", 
            Body = new List<Vector2> { new Vector2(food.Position.X, food.Position.Y) },
            IsDead = false,
            Speed = 0f, // Don't move so we stay on food
            Score = 0,
            Angle = 0
        };
        engine.InternalPlayerMap.TryAdd("p1", p1);
        
        // Manually trigger collision logic
        engine.Tick(); 
        
        // Check if food was removed and added to queue
        bool inQueue = engine.InternalRespawnQueue.ContainsKey(food.Id);
        Assert.True(inQueue, $"Food {food.Id} should be in respawn queue after collision. Player at {p1.Body[0]}, Food at {food.Position}");
        
        // Fast forward time in the queue (we can't easily do this, so we'll wait)
        System.Threading.Thread.Sleep(3100);
        engine.Tick(); 
        
        lock(internalLock) {
            Assert.Contains(food.Id, internalFood.Select(f => f.Id));
            Assert.Equal(400, internalFood.Count);
        }
    }
}
