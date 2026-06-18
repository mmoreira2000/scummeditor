using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;
using ScummEditor.Engine.Structures.IndexFile;

namespace ScummEditor.Engine.Encoders
{
    /// <summary>
    /// Batch graphics export for SCUMM v2 games (Maniac Mansion, Zak McKracken): room backgrounds and
    /// object images via the GdiV2 codec (ScummV2ImageDecoder), and costume frames - which are the classic
    /// format 0x58, byte-identical to v3old, so they reuse CostumeV3Old + CostumeImageDecoderV4 + the fixed
    /// 16-colour EGA palette. Mirrors ScummV3OldGraphics.
    /// </summary>
    public static class ScummV2Graphics
    {
        public static int Export(ScummGameData game, string folder, ScummV4GraphicsBatch.ExportOptions options,
            Action<int, int> onProgress, Func<bool> shouldCancel = null)
        {
            var decoder = new ScummV2ImageDecoder();
            var rooms = RoomsByNumber(game);
            var roomNumbers = new List<int>(rooms.Keys);
            roomNumbers.Sort();
            int count = 0;

            for (int idx = 0; idx < roomNumbers.Count; idx++)
            {
                if (shouldCancel != null && shouldCancel()) break;
                int roomNo = roomNumbers[idx];
                var room = new ScummV2Room(rooms[roomNo].RawContent);
                if (room.Width <= 0 || room.Height <= 0) { if (onProgress != null) onProgress(idx + 1, roomNumbers.Count); continue; }

                if (options.Backgrounds)
                {
                    Bitmap background = decoder.DecodeBackground(room);
                    if (background != null) { Save(background, folder, string.Format("Room#{0}.png", roomNo)); count++; }
                }
                if (options.Objects)
                {
                    for (int j = 0; j < room.NumObjects; j++)
                    {
                        Bitmap obj = decoder.DecodeObject(room, j);
                        if (obj != null) { Save(obj, folder, string.Format("Room#{0} Obj#{1} Img#0.png", roomNo, j)); count++; }
                    }
                }
                if (onProgress != null) onProgress(idx + 1, roomNumbers.Count);
            }

            if (options.Costumes) count += ExportCostumes(game, folder);
            return count;
        }

        /// <summary>Exports every costume frame (format 0x58 = classic, same as v3old), via the index COSTUME directory.</summary>
        private static int ExportCostumes(ScummGameData game, string folder)
        {
            var index = game.IndexFile as ScummV3OldBundleIndexFile;
            if (index == null || index.CostumeDirectory == null) return 0;

            var byRoom = RoomsByNumber(game);
            var costumeDecoder = new CostumeImageDecoderV4();
            var ega = new Color[16];
            Array.Copy(EgaColorTable.Colors256, ega, 16);

            int count = 0;
            V3OldResourceDirectory dir = index.CostumeDirectory;
            for (int c = 0; c < dir.Count; c++)
            {
                int offset = dir.Offsets[c];
                if (offset == 0xFFFF || offset == 0) continue;
                ScummV3OldBundleDataFile df;
                if (!byRoom.TryGetValue(dir.RoomNumbers[c], out df)) continue;

                CostumeV3Old costume;
                try { costume = new CostumeV3Old(df.RawContent, offset); }
                catch { continue; }
                for (int k = 0; k < costume.Frames.Count; k++)
                {
                    Bitmap frame;
                    try { frame = costumeDecoder.Decode(costume.Frames[k], 16, ega, false); }
                    catch { continue; }
                    if (frame != null) { Save(frame, folder, string.Format("Costume#{0} FrameIndex#{1}.png", c, k)); count++; }
                }
            }
            return count;
        }

        private static Dictionary<int, ScummV3OldBundleDataFile> RoomsByNumber(ScummGameData game)
        {
            var map = new Dictionary<int, ScummV3OldBundleDataFile>();
            foreach (DataDisk disk in game.DataDisks)
            {
                var df = disk.Tree as ScummV3OldBundleDataFile;
                int n;
                if (df != null && int.TryParse(Path.GetFileNameWithoutExtension(disk.FilePath), out n)) map[n] = df;
            }
            return map;
        }

        private static void Save(Bitmap bitmap, string folder, string fileName)
        {
            bitmap.Save(Path.Combine(folder, fileName), ImageFormat.Png);
        }
    }
}
