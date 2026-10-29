namespace RuneArena.Strokes
{
    // Every label a stroke can have. Same order as LABELS in ml/runes/__init__.py: it is the order of the CNN outputs.
    public static class RuneLabels
    {
        public const string Noise = "noise";

        public static readonly string[] All = { "square", "circle", "lightning", "triangle", "spiral", "v", Noise };
    }
}
