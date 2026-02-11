using System;

namespace Squawk.Server.Models
{
    public struct Vector2
    {
        public float X;
        public float Y;

        public Vector2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.X + b.X, a.Y + b.Y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.X - b.X, a.Y - b.Y);
        public static Vector2 operator *(Vector2 a, float b) => new Vector2(a.X * b, a.Y * b);
        public static Vector2 operator /(Vector2 a, float b) => new Vector2(a.X / b, a.Y / b);

        public float Length() => (float)Math.Sqrt(X * X + Y * Y);
        public Vector2 Normalized()
        {
            float len = Length();
            return len > 0 ? this / len : new Vector2(0, 0);
        }

        public static float Distance(Vector2 a, Vector2 b) => (a - b).Length();
        
        public static Vector2 Lerp(Vector2 a, Vector2 b, float t)
        {
            return a + (b - a) * t;
        }

        public static float Angle(Vector2 v) => (float)Math.Atan2(v.Y, v.X);
        public static Vector2 FromAngle(float angle) => new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
    }
}
