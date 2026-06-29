using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;
using Xunit;
using Xunit.Abstractions;

namespace ScummEditor.UnitTests
{
    /// <summary>
    /// SCUMM v8 (The Curse of Monkey Island) z-plane (occlusion mask) decode/encode. v8 nests them
    /// ROOM/OBIM -> IMAG -> WRAP -> SMAP -> ZPLN -> WRAP -> {OFFS, ZSTR x numZBuffer}; each ZSTR -> WRAP ->
    /// leaf is the SMAP strip leaf layout but with 1-bit mask strips (shared ZPlaneDecoder/Encoder codec).
    /// These verify the masks decode with content, re-encode losslessly, and survive a size-changing edit
    /// + save/reload with the ZPLN and IMAG OFFS tables kept correct.
    /// </summary>
    public class V8ZPlaneTests
    {
        private readonly ITestOutputHelper _out;
        public V8ZPlaneTests(ITestOutputHelper o) { _out = o; }

        [SkippableFact]
        public void ZPlanesDecodeWithContent()
        {
            ScummGameData game = GameLibrary.Load(GameLibrary.CurseOfMonkeyIsland);
            Skip.If(game == null, "COMI (v8) not present");

            var dec = new ScummV8ImageDecoder();
            int checkedZ = 0, withContent = 0, roomsWithZ = 0;
            foreach (DataDisk disk in game.DataDisks)
            {
                foreach (DiskBlock lflf in disk.Tree.GetLFLFs())
                {
                    RoomBlock room = lflf.GetROOM();
                    int n = dec.CountBackgroundZPlanes(room);
                    if (n > 0) roomsWithZ++;
                    for (int z = 0; z < n; z++)
                    {
                        using (Bitmap m = dec.DecodeBackgroundZPlane(room, z))
                        {
                            if (m == null) continue;
                            checkedZ++;
                            if (HasMasked(m)) withContent++;
                        }
                    }
                    if (checkedZ >= 40) break;
                }
                if (checkedZ >= 40) break;
            }
            _out.WriteLine("v8 bg z-planes: {0} rooms with z-planes, {1} decoded, {2} have masked content", roomsWithZ, checkedZ, withContent);
            Assert.True(checkedZ > 0, "no v8 background z-planes decoded");
            Assert.True(withContent > 0, "every decoded z-plane was empty - the mask codec is likely wrong");
        }

        [SkippableFact]
        public void ZPlaneReEncodeIsLossless()
        {
            ScummGameData game = GameLibrary.Load(GameLibrary.CurseOfMonkeyIsland);
            Skip.If(game == null, "COMI (v8) not present");

            var dec = new ScummV8ImageDecoder();
            var enc = new ScummV8ImageEncoder();
            int done = 0;
            foreach (DiskBlock lflf in game.DataFile.GetLFLFs())
            {
                RoomBlock room = lflf.GetROOM();
                int n = dec.CountBackgroundZPlanes(room);
                for (int z = 0; z < n && done < 10; z++)
                {
                    bool[,] before;
                    using (Bitmap a = dec.DecodeBackgroundZPlane(room, z))
                    {
                        if (a == null || !HasMasked(a)) continue; // only verify ones with real mask content
                        before = ToMask(a);
                        enc.EncodeBackgroundZPlane(room, z, a);
                    }
                    using (Bitmap b = dec.DecodeBackgroundZPlane(room, z))
                    {
                        Assert.NotNull(b);
                        Assert.True(MaskEquals(before, ToMask(b)), "z-plane re-encode was not lossless");
                    }
                    done++;
                }
                if (done >= 10) break;
            }
            _out.WriteLine("v8 z-planes re-encoded losslessly: {0}", done);
            Assert.True(done > 0, "no v8 z-plane with content to re-encode");
        }

        [SkippableFact]
        public void ZPlaneEditSurvivesSaveReload()
        {
            ScummGameData game = GameLibrary.Load(GameLibrary.CurseOfMonkeyIsland);
            Skip.If(game == null, "COMI (v8) not present");

            var dec = new ScummV8ImageDecoder();
            var enc = new ScummV8ImageEncoder();
            List<DiskBlock> lflfs = game.DataFile.GetLFLFs();

            int roomIndex = -1;
            bool[,] edited = null;
            for (int i = 0; i < lflfs.Count && roomIndex < 0; i++)
            {
                RoomBlock room = lflfs[i].GetROOM();
                if (dec.CountBackgroundZPlanes(room) <= 0) continue;
                using (Bitmap a = dec.DecodeBackgroundZPlane(room, 0))
                {
                    if (a == null) continue;
                    // Paint a solid masked (black) band across the top -> a real, size-changing edit.
                    for (int y = 0; y < System.Math.Min(24, a.Height); y++)
                        for (int x = 0; x < a.Width; x++)
                            a.SetPixel(x, y, Color.Black);
                    edited = ToMask(a);
                    enc.EncodeBackgroundZPlane(room, 0, a);
                    roomIndex = i;
                }
            }
            Skip.If(roomIndex < 0, "no disk-0 room with a background z-plane");

            game.PostProcessChanges();
            using (var idxMs = new MemoryStream()) game.IndexFile.SaveToBinaryWriter(idxMs); // must not throw

            ScummDataFile reparsed;
            using (var ms = new MemoryStream())
            {
                game.DataFile.SaveToBinaryWriter(ms);
                ms.Position = 0;
                reparsed = new ScummDataFile(null, game.LoadedGameInfo);
                reparsed.LoadFromBinaryReader(ms);
            }

            RoomBlock reloaded = reparsed.GetLFLFs()[roomIndex].GetROOM();
            using (Bitmap rb = dec.DecodeBackgroundZPlane(reloaded, 0))
            {
                Assert.NotNull(rb);
                Assert.True(MaskEquals(edited, ToMask(rb)), "the edited z-plane did not survive save+reload");
            }
        }

        private static bool HasMasked(Bitmap b)
        {
            int stepX = System.Math.Max(1, b.Width / 64), stepY = System.Math.Max(1, b.Height / 64);
            for (int y = 0; y < b.Height; y += stepY)
                for (int x = 0; x < b.Width; x += stepX)
                    if (b.GetPixel(x, y).R < 128) return true; // black = masked
            return false;
        }

        private static bool[,] ToMask(Bitmap b)
        {
            var m = new bool[b.Width, b.Height];
            for (int y = 0; y < b.Height; y++)
                for (int x = 0; x < b.Width; x++)
                    m[x, y] = b.GetPixel(x, y).R < 128;
            return m;
        }

        private static bool MaskEquals(bool[,] a, bool[,] b)
        {
            if (a.GetLength(0) != b.GetLength(0) || a.GetLength(1) != b.GetLength(1)) return false;
            for (int y = 0; y < a.GetLength(1); y++)
                for (int x = 0; x < a.GetLength(0); x++)
                    if (a[x, y] != b[x, y]) return false;
            return true;
        }
    }
}
