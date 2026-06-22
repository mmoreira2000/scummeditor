using System.IO;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;
using Xunit;

namespace ScummEditor.UnitTests
{
    /// <summary>
    /// Real-data tests for the v2 / v3-old GUI navigation overlay (OldBundleNavigator): every room model
    /// builds without throwing, the object/script ranges are valid windows into the room bytes, and the
    /// disassembler produces a real listing for the vast majority of them. Skips when the GameData library
    /// is absent. (Display formatting now lives in the GUI viewers, so it is not unit-tested here.)
    /// </summary>
    public class OldBundleNavigatorTests
    {
        [SkippableTheory]
        [InlineData(GameLibrary.LoomEga)]
        [InlineData(GameLibrary.Indy3Ega)]
        [InlineData(GameLibrary.ManiacV2)]
        [InlineData(GameLibrary.ZakV2)]
        public void EveryRoomModelBuildsAndDisassembles(string rel)
        {
            Skip.IfNot(GameLibrary.Available, "GameData library not present");
            ScummGameData game = GameLibrary.Load(rel);
            Skip.If(game == null, "game folder missing");

            int totalObjects = 0, totalScripts = 0, codeTried = 0, codeWithListing = 0;

            foreach (DataDisk disk in game.DataDisks)
            {
                var df = disk.Tree as ScummV3OldBundleDataFile;
                if (df == null) continue;
                int roomNo;
                int.TryParse(Path.GetFileNameWithoutExtension(disk.FilePath), out roomNo);

                OldBundleRoomModel model = OldBundleNavigator.BuildRoomModel(game, df, roomNo);
                // Only valid objects are listed; phantom objects (no real OBCD offset) are skipped (S1).
                Assert.True(model.Objects.Count <= model.NumObjects,
                    rel + " room " + roomNo + ": listed more objects than the room declares");

                foreach (OldBundleObjectInfo obj in model.Objects)
                {
                    totalObjects++;
                    foreach (OldBundleCodeRange v in obj.VerbCode)
                    {
                        Assert.True(v.Start >= 0 && v.End >= v.Start && v.End <= df.RawContent.Length,
                            rel + " room " + roomNo + " obj " + obj.Index + " verb '" + v.Label + "' window invalid");
                        if (v.End <= v.Start) continue; // shared/empty body
                        codeTried++;
                        var vr = OldBundleNavigator.DisassembleRange(df.RawContent, v.Start, v.End, model.IsV2, model.IsIndy3, model.IsV1);
                        if (vr != null && !string.IsNullOrEmpty(vr.Listing)) codeWithListing++;
                    }
                }

                foreach (OldBundleCodeRange r in model.Scripts)
                {
                    totalScripts++;
                    Assert.True(r.Start >= 0 && r.End > r.Start && r.End <= df.RawContent.Length,
                        rel + " room " + roomNo + " script '" + r.Label + "' window invalid");
                    var result = OldBundleNavigator.DisassembleRange(df.RawContent, r.Start, r.End, model.IsV2, model.IsIndy3, model.IsV1);
                    Assert.NotNull(result);
                    codeTried++;
                    if (!string.IsNullOrEmpty(result.Listing)) codeWithListing++;
                }
            }

            Assert.True(totalObjects > 0, rel + ": expected objects across rooms");
            Assert.True(totalScripts > 0, rel + ": expected scripts across rooms");
            // The vast majority of code ranges must disassemble to a real (non-empty) listing.
            Assert.True(codeWithListing >= codeTried * 0.9, rel + ": too many empty disassemblies");
        }

        [SkippableFact]
        public void DisassembleRange_InvalidWindow_ReturnsNull()
        {
            Skip.IfNot(GameLibrary.Available, "GameData library not present");
            ScummGameData game = GameLibrary.Load(GameLibrary.LoomEga);
            Skip.If(game == null, "game folder missing");
            var df = game.DataDisks[0].Tree as ScummV3OldBundleDataFile;
            Assert.NotNull(df);

            Assert.Null(OldBundleNavigator.DisassembleRange(df.RawContent, 10, 10, false, false, false));   // empty
            Assert.Null(OldBundleNavigator.DisassembleRange(df.RawContent, -1, 5, false, false, false));     // negative start
            Assert.Null(OldBundleNavigator.DisassembleRange(df.RawContent, 0, df.RawContent.Length + 1, false, false, false)); // past end
        }
    }
}
