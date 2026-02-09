using System;
using System.Collections.Generic;
using System.Numerics;

namespace Squawk.Game
{
    public class Parrot
    {
        public Vector2 Position;
        public Vector2 Direction = new Vector2(1, 0);
        public float SpeedBase = 300f;
        public float SpeedBoost = 550f;
        public float Energy = 10f;
        public bool IsBoosting;
        public bool IsAlive = true;
        public float Size => 8f + MathF.Sqrt(MathF.Max(0f, Energy)) * 0.9f;
        public float VisionRadius => 180f + Size * 3f;
        public List<Segment> Segments = new();
        public int MaxSegments = 40;
        public float SegmentSpacing => 4f;
        public float TurnRateBase => 5.0f;
        public float Hue = 180f;
        public string Name = "Gracz";

        public void InitializeSegments(int count)
        {
            Segments.Clear();
            for (int i = 0; i < count; i++)
            {
                Segments.Add(new Segment { Position = Position - Direction * i * SegmentSpacing, Radius = Size * 0.8f });
            }
        }

        public float TurnRate()
        {
            var r = TurnRateBase / (1f + Size * 0.03f);
            return MathF.Max(3.0f, r);
        }

        public float CurrentSpeed(float dt, float boostCost)
        {
            if (IsBoosting && Energy > 1f)
            {
                Energy = MathF.Max(0f, Energy - boostCost * dt);
                return SpeedBoost;
            }
            return SpeedBase;
        }

        public void Update(float dt, Vector2 desiredDir, float boostCost)
        {
            if (!IsAlive) return;
            if (desiredDir.LengthSquared() > 0.0001f)
            {
                desiredDir = Vector2.Normalize(desiredDir);
                var angle = MathF.Acos(Math.Clamp(Vector2.Dot(Direction, desiredDir), -1f, 1f));
                if (angle > 0f)
                {
                    var maxTurn = TurnRate() * dt;
                    var t = MathF.Min(1f, maxTurn / MathF.Max(0.0001f, angle));
                    Direction = Vector2.Normalize(Direction * (1f - t) + desiredDir * t);
                }
            }
            var speed = CurrentSpeed(dt, boostCost);
            Position += Direction * speed * dt;
            UpdateSegments();
        }

        void UpdateSegments()
        {
            if (Segments.Count == 0)
            {
                InitializeSegments(10);
            }
            Segments[0].Position = Position;
            Segments[0].Radius = Size * 1.05f;
            for (int i = 1; i < Segments.Count; i++)
            {
                var prev = Segments[i - 1];
                var cur = Segments[i];
                var toPrev = prev.Position - cur.Position;
                var dist = toPrev.Length();
                var target = prev.Position - Vector2.Normalize(toPrev == Vector2.Zero ? new Vector2(1, 0) : toPrev) * SegmentSpacing;
                cur.Position = Vector2.Lerp(cur.Position, target, 0.75f);
                cur.Radius = MathF.Max(3f, Size * 0.85f - i * 0.05f * Size);
                Segments[i] = cur;
            }
            var desiredCount = Math.Clamp((int)(Energy * 0.5f) + 10, 10, MaxSegments);
            if (desiredCount > Segments.Count)
            {
                var tail = Segments[^1];
                Segments.Add(new Segment { Position = tail.Position - Direction * SegmentSpacing, Radius = MathF.Max(3f, Size * 0.7f) });
            }
            else if (desiredCount < Segments.Count)
            {
                Segments.RemoveAt(Segments.Count - 1);
            }
        }
    }
}
