namespace ScummEditor.Engine.Structures
{
    /// <summary>
    /// Canonical human-readable names for the detected games. Engine-side (not GUI) so the name is the
    /// single source of truth and is unit-testable: every ScummGame value must map to a real name, and a
    /// value that is somehow unmapped falls back to its enum name rather than silently showing "None"
    /// (the bug that made Indiana Jones 3 / Zak / Maniac appear as "None" in the status bar).
    /// </summary>
    public static class ScummGameNames
    {
        public static string DisplayName(ScummGame game)
        {
            switch (game)
            {
                case ScummGame.ManiacMansion: return "Maniac Mansion";
                case ScummGame.ZakMcKracken: return "Zak McKracken and the Alien Mindbenders";
                case ScummGame.IndianaJones3: return "Indiana Jones and the Last Crusade";
                case ScummGame.Loom: return "Loom";
                case ScummGame.MonkeyIsland1Floppy: return "The Secret of Monkey Island";
                case ScummGame.MonkeyIsland1VGA:
                case ScummGame.MonkeyIsland1VGASpeech: return "The Secret of Monkey Island (CD)";
                case ScummGame.MonkeyIsland2: return "Monkey Island 2: LeChuck's Revenge";
                case ScummGame.FateOfAtlantis: return "Indiana Jones and the Fate of Atlantis";
                case ScummGame.DayOfTheTentacle: return "Day of the Tentacle";
                case ScummGame.SamAndMax: return "Sam & Max Hit the Road";
                case ScummGame.TheDig: return "The Dig";
                case ScummGame.FullThrottle: return "Full Throttle";
                case ScummGame.None: return "None";
                default: return game.ToString(); // never silently "None" for a real, newly-added game
            }
        }
    }
}
