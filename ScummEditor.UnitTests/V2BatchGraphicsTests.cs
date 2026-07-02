using System.IO;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Structures;
using Xunit;

namespace ScummEditor.UnitTests
{
    /// <summary>
    /// v2 BATCH graphics export/import (ScummV2Graphics) including the newly-added object walk-behind
    /// (z-plane) masks. Confirms object z-planes are exported and that re-importing the unmodified export
    /// is a clean no-op (the per-object image+mask merge produces no spurious conflicts).
    /// </summary>
    public class V2BatchGraphicsTests
    {
        [SkippableTheory]
        [InlineData(GameLibrary.ManiacV2)]
        [InlineData(GameLibrary.ZakV2)]
        public void V2BatchExportsObjectZPlanesAndReimportsCleanly(string relativePath)
        {
            ScummGameData game = SkipOrLoad(relativePath);
            string dir = Path.Combine(Path.GetTempPath(), "v2batch_zp");
            try
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
                Directory.CreateDirectory(dir);

                int exported = ScummV2Graphics.Export(game, dir, new ScummV4GraphicsBatch.ExportOptions(), null, null);
                Assert.True(exported > 0, "nothing exported");

                string[] objZ = Directory.GetFiles(dir, "*Obj#* ZP#000.png");
                Assert.True(objZ.Length > 0, "no object z-plane PNGs were exported");

                ScummV4GraphicsBatch.ImportReport report = ScummV2Graphics.Import(game, dir, null);
                Assert.Empty(report.Errors);
                Assert.Equal(0, report.Imported); // unmodified export -> nothing re-imported
            }
            finally { try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { } }
        }

        private static ScummGameData SkipOrLoad(string relativePath)
        {
            Skip.If(GameLibrary.Folder(relativePath) == null, "GameData folder not present: " + relativePath);
            ScummGameData game = GameLibrary.Load(relativePath);
            Skip.If(game == null, "could not load: " + relativePath);
            return game;
        }
    }
}
