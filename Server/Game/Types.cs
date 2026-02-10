namespace Squawk.Game
{
    public enum FeatherType
    {
        WorldFeather = 0,
        BoostFeather = 1,
        DeathFeather = 2
    }
    public enum BotState
    {
        Wander,
        Feed,
        Attack,
        Evade,
        Trapped
    }

    public class InputState
    {
        public float x { get; set; }
        public float y { get; set; }
        public bool down { get; set; }
        public bool space { get; set; }
        public float w { get; set; }
        public float h { get; set; }
    }
}

