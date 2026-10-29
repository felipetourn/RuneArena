using RuneArena.Waves;

namespace RuneArena.Game
{
    // Survives the scene reload used to restart: what to do when the arena loads again.
    public static class GameSession
    {
        public static Difficulty Difficulty = Difficulty.Normal;
        // True to skip the start menu and play right away, as after "Retry".
        public static bool StartImmediately;
    }
}
