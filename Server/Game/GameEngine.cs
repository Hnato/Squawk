using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Squawk.Game
{
    public class GameEngine : BackgroundService
    {
        private readonly GameWorld _world;
        private const double TickRate = 1.0 / 30.0; // 30 Hz
        public event Action<GameWorld>? OnStateUpdated;
        
        // Input queue: (PlayerId, InputData)
        private readonly ConcurrentQueue<(string, InputState)> _inputQueue = new();
        private long _ticks = 0;

        public GameEngine(GameWorld world)
        {
            _world = world;
        }

        public void EnqueueInput(string playerId, InputState input)
        {
            _inputQueue.Enqueue((playerId, input));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _world.Initialize(0); // Initialize world
            
            double accumulator = 0;
            var stopwatch = Stopwatch.StartNew();

            while (!stoppingToken.IsCancellationRequested)
            {
                double frameTime = stopwatch.Elapsed.TotalSeconds;
                stopwatch.Restart();
                
                // Cap frame time to avoid spiral of death
                if (frameTime > 0.25) frameTime = 0.25;

                accumulator += frameTime;

                bool updated = false;
                while (accumulator >= TickRate)
                {
                    ProcessInputs();
                    _world.Update((float)TickRate);
                    accumulator -= TickRate;
                    updated = true;
                }

                if (updated)
                {
                    OnStateUpdated?.Invoke(_world);
                }

                // Log metrics every ~100 frames
                if (stopwatch.ElapsedMilliseconds > 3000) // Just a periodic check example, but stopwatch resets
                { 
                   // Not good place
                }

                // Sleep to prevent 100% CPU usage if we are ahead
                var delay = (TickRate - accumulator) * 1000;
                
                // Logging
                if (_ticks % 100 == 0)
                {
                    var tickTimeMs = frameTime * 1000;
                    Console.WriteLine($"[Tick #{_ticks}] Duration: {tickTimeMs:F2}ms, Inputs: {_inputQueue.Count}, Parrots: {_world.Parrots.Count}");
                }
                _ticks++;

                if (delay > 1)
                {
                    await Task.Delay((int)delay, stoppingToken);
                }
                else
                {
                    await Task.Yield();
                }
            }
        }

        private void ProcessInputs()
        {
            while (_inputQueue.TryDequeue(out var item))
            {
                var (playerId, input) = item;
                _world.ApplyInput(playerId, input);
            }
        }
    }
}
