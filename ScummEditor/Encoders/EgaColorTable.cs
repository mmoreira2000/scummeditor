using System.Drawing;

namespace ScummEditor.Encoders
{
    /// <summary>
    /// The fixed 16-color EGA hardware palette used by SCUMM v4 EGA rooms (which store no palette
    /// of their own). The 16 RGB triples are taken verbatim from ScummVM's tableEGAPalette
    /// (palette.cpp); note index 6 is an irregular brown (0xAA,0x55,0x00).
    /// </summary>
    public static class EgaColorTable
    {
        private static readonly byte[,] Triples =
        {
            { 0x00, 0x00, 0x00 }, // 0  black
            { 0x00, 0x00, 0xAA }, // 1  blue
            { 0x00, 0xAA, 0x00 }, // 2  green
            { 0x00, 0xAA, 0xAA }, // 3  cyan
            { 0xAA, 0x00, 0x00 }, // 4  red
            { 0xAA, 0x00, 0xAA }, // 5  magenta
            { 0xAA, 0x55, 0x00 }, // 6  brown
            { 0xAA, 0xAA, 0xAA }, // 7  light gray
            { 0x55, 0x55, 0x55 }, // 8  dark gray
            { 0x55, 0x55, 0xFF }, // 9  bright blue
            { 0x55, 0xFF, 0x55 }, // 10 bright green
            { 0x55, 0xFF, 0xFF }, // 11 bright cyan
            { 0xFF, 0x55, 0x55 }, // 12 bright red
            { 0xFF, 0x55, 0xFF }, // 13 bright magenta
            { 0xFF, 0xFF, 0x55 }, // 14 yellow
            { 0xFF, 0xFF, 0xFF }, // 15 white
        };

        /// <summary>
        /// The 16 EGA colors padded to 256 entries (16..255 = black), so it plugs straight into
        /// <see cref="IndexedImageHelper.FromIndexMatrix"/> like a normal 256-color palette.
        /// </summary>
        public static Color[] Colors256
        {
            get
            {
                var colors = new Color[256];
                for (int i = 0; i < 16; i++)
                {
                    colors[i] = Color.FromArgb(Triples[i, 0], Triples[i, 1], Triples[i, 2]);
                }
                for (int i = 16; i < colors.Length; i++)
                {
                    colors[i] = Color.Black;
                }
                return colors;
            }
        }
    }
}
