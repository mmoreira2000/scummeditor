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
        ManiacMansion = 11         // SCUMM v1/v2 - Maniac Mansion (00.LFL + NN.LFL)
    }
}
