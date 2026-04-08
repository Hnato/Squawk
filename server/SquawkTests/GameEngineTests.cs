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
        
        Assert.Equal(15, player.Body.Count);
    }

    [Fact]
    public void Food_Replenishment_Should_Maintain_High_Count()
    {
        var engine = new GameEngine();
        engine.Start();
        
        Assert.Equal(400, engine.FoodItems.Count);
    }

    [Fact]
    public void Bot_Results_Should_Be_Tracked()
    {
        var engine = new GameEngine();
        engine.BotsEnabled = true;
        engine.Start(); 
        
        engine.Tick();
        
        var bots = engine.Players.Where(p => p.IsBot).ToList();
        Assert.Equal(4, bots.Count);
        
        foreach (var bot in bots)
        {
            Assert.StartsWith("Bot", bot.Name);
            Assert.Equal(15, bot.Body.Count);
        }
    }

    [Fact]
    public void Food_Should_Respawn_After_3_Seconds()
    {
        var engine = new GameEngine();
        engine.BotsEnabled = false;
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
        
        var p1 = new Player { 
            Id = "p1", 
            Name = "P1", 
            Body = [new Vector2(food.Position.X, food.Position.Y)],
            IsDead = false,
            Speed = 0f,
            Score = 0,
            Angle = 0
        };
        engine.InternalPlayerMap.TryAdd("p1", p1);
        
        engine.Tick(); 
        
        bool inQueue = engine.InternalRespawnQueue.ContainsKey(food.Id);
        Assert.True(inQueue, $"Food {food.Id} should be in respawn queue after collision.");
        
        System.Threading.Thread.Sleep(3100);
        engine.Tick(); 
        
        lock(internalLock) {
            Assert.Contains(food.Id, internalFood.Select(f => f.Id));
            Assert.Equal(400, internalFood.Count);
        }
    }
}
