namespace ScummEditor.Engine.Structures
{
    public enum ScummGame
    {
        None = 0,
        SamAndMax = 1,
        DayOfTheTentacle = 2,
        FateOfAtlantis = 3,
        MonkeyIsland1VGA = 4,
        MonkeyIsland2 = 5,
        MonkeyIsland1VGASpeech = 6,
        MonkeyIsland1Floppy = 7,   // SCUMM v4 (000.LFL + DISKnn.LEC)
        Loom = 8,                  // SCUMM v4 (Loom CD); also the SCUMM v3 Loom EGA (00.LFL + NN.LFL)
        IndianaJones3 = 9,         // SCUMM v3 - Indiana Jones and the Last Crusade
        ZakMcKracken = 10,         // SCUMM v3 - Zak McKracken (FM Towns / enhanced); also v1/v2 Zak (00.LFL + NN.LFL)
        ManiacMansion = 11,        // SCUMM v1/v2 - Maniac Mansion (00.LFL + NN.LFL)
        TheDig = 12,               // SCUMM v7 - The Dig (GAME.LA0 + GAME.LA1)
        FullThrottle = 13,         // SCUMM v7 - Full Throttle (GAME.LA0 + GAME.LA1)
        CurseOfMonkeyIsland = 14   // SCUMM v8 - The Curse of Monkey Island (COMI.LA0 + COMI.LA1 + COMI.LA2)
    }
}
