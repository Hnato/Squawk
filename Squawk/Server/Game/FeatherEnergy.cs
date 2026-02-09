using System.Numerics;

namespace Squawk.Game
{
    public class FeatherEnergy
    {
        public FeatherType Type;
        public Vector2 Position;
        public float Value;
        public float Radius => MathF.Max(1.5f, Value * 0.5f);
    }
}
