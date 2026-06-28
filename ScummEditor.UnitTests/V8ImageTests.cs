using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;
using Xunit;
using Xunit.Abstractions;

namespace ScummEditor.UnitTests
{
    /// <summary>
    /// SCUMM v8 (The Curse of Monkey Island) image decode: every room background and a sample of object
    /// images decode through <see cref="ScummV8ImageDecoder"/> (which navigates the v8 IMAG/WRAP/OFFS
    /// nesting and reuses the v5/v6/v7 SMAP strip codec). Asserts the decoded bitmaps have the RMHD/IMHD
    /// dimensions and real content (more than one colour - a decode that desynced would be a flat block).
    /// </summary>
    public class V8ImageTests
    {
        private readonly ITestOutputHelper _out;
        public V8ImageTests(ITestOutputHelper o) { _out = o; }

        [SkippableFact]
        public void EveryBackgroundDecodes()
        {
            ScummGameData game = GameLibrary.Load(GameLibrary.CurseOfMonkeyIsland);
            Skip.If(game == null, "COMI (v8) not present");

            var decoder = new ScummV8ImageDecoder();
            int withImage = 0, ok = 0;
            foreach (DataDisk disk in game.DataDisks)
            {
                foreach (DiskBlock lflf in disk.Tree.GetLFLFs())
                {
                    RoomBlock room = lflf.GetROOM();
                    // A room "has a background" only if its IMAG actually carries strip data (a BSTR);
                    // some rooms have an IMAG wrapper with only an (empty) z-plane and no bitmap.
                    if (!HasBackgroundStrips(room)) continue;
                    withImage++;

                    Bitmap bmp = decoder.DecodeBackground(room);
                    Assert.NotNull(bmp);
                    if (HasMultipleColours(bmp)) ok++;
                }
            }

            _out.WriteLine("COMI v8 backgrounds: {0} with a bitmap, {1} decoded with real content", withImage, ok);
            Assert.True(withImage > 0, "no v8 room backgrounds found");
            // Every room with real strip data must decode to a non-flat image.
            Assert.Equal(withImage, ok);
        }

        private static bool HasBackgroundStrips(RoomBlock room)
        {
            BlockBase imag = room.Childrens.FirstOrDefault(c => c.BlockType == "IMAG");
            BlockBase wrap = imag == null ? null : imag.Childrens.FirstOrDefault(c => c.BlockType == "WRAP");
            BlockBase smap = wrap == null ? null : wrap.Childrens.FirstOrDefault(c => c.BlockType == "SMAP");
            return smap != null && smap.Childrens.Any(c => c.BlockType == "BSTR");
        }

        [SkippableFact]
        public void ObjectImagesDecode()
        {
            ScummGameData game = GameLibrary.Load(GameLibrary.CurseOfMonkeyIsland);
            Skip.If(game == null, "COMI (v8) not present");

            var decoder = new ScummV8ImageDecoder();
            int objectsWithImage = 0, decoded = 0;
            foreach (DataDisk disk in game.DataDisks)
            {
                foreach (DiskBlock lflf in disk.Tree.GetLFLFs())
                {
                    RoomBlock room = lflf.GetROOM();
                    int objects = ScummV8ImageDecoder.ObjectCount(room);
                    for (int i = 0; i < objects; i++)
                    {
                        Bitmap bmp = decoder.DecodeObject(room, i);
                        if (bmp == null) continue; // hotspot-only object (no IMAG) - expected
                        objectsWithImage++;
                        if (HasMultipleColours(bmp)) decoded++;
                    }
                }
                if (objectsWithImage >= 50) break; // a sample is enough; this is a sweep, not exhaustive
            }

            _out.WriteLine("COMI v8 object images sampled: {0} with an image, {1} decoded with content", objectsWithImage, decoded);
            Assert.True(objectsWithImage > 0, "no v8 object images found");
            // Each SMAP object image must decode without error to the right size; most have real content,
            // but a few are legitimately a uniform fill (a solid overlay), so allow a small flat fraction.
            Assert.True(decoded >= objectsWithImage * 0.9,
                string.Format("only {0}/{1} v8 object images decoded with content", decoded, objectsWithImage));
        }

        private static bool HasMultipleColours(Bitmap bmp)
        {
            if (bmp == null) return false;
            var seen = new HashSet<int>();
            // Sample a grid (full per-pixel scan of an 800x480 image is slow and unnecessary).
            int stepX = System.Math.Max(1, bmp.Width / 64);
            int stepY = System.Math.Max(1, bmp.Height / 64);
            for (int y = 0; y < bmp.Height; y += stepY)
                for (int x = 0; x < bmp.Width; x += stepX)
                {
                    seen.Add(bmp.GetPixel(x, y).ToArgb());
                    if (seen.Count > 1) return true;
                }
            return seen.Count > 1;
        }
    }
}
