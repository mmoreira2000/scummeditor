using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;

namespace ScummEditor.Engine.Encoders
{
    /// <summary>
    /// Batch PNG export of the EGA images of a SCUMM v3 "old bundle" game (Loom EGA, Indy3 EGA). These
    /// games are not the v4/v3small block-tree the ScummV4GraphicsBatch walks - each NN.LFL is a raw
    /// room whose background/object strip tables are reached by file-relative offsets - so they get this
    /// dedicated path. Decoding reuses the validated ScummV3OldImageDecoder; filenames match the v4/v5
    /// scheme ("Room#i", "Room#i Obj#j Img#0") so export/import map 1:1 if import is added later.
    /// </summary>
    public static class ScummV3OldGraphics
    {
        /// <summary>The v3 old-bundle room files in a stable, game-wide order (the DataDisk order).</summary>
        public static List<ScummV3OldBundleDataFile> EnumerateRooms(ScummGameData game)
        {
            var rooms = new List<ScummV3OldBundleDataFile>();
            if (game.DataDisks == null) return rooms;
            foreach (DataDisk disk in game.DataDisks)
            {
                var df = disk.Tree as ScummV3OldBundleDataFile;
                if (df != null) rooms.Add(df);
            }
            return rooms;
        }

        public static int Export(ScummGameData game, string folder, ScummV4GraphicsBatch.ExportOptions options,
            Action<int, int> onProgress, Func<bool> shouldCancel = null)
        {
            var decoder = new ScummV3OldImageDecoder();
            List<ScummV3OldBundleDataFile> rooms = EnumerateRooms(game);
            int count = 0;

            for (int i = 0; i < rooms.Count; i++)
            {
                if (shouldCancel != null && shouldCancel()) break;

                var room = new ScummV3OldRoom(rooms[i].RawContent);

                if (options.Backgrounds)
                {
                    Bitmap background = decoder.DecodeBackground(room);
                    if (background != null)
                    {
                        Save(background, folder, string.Format("Room#{0}.png", i));
                        count++;
                    }
                }

                if (options.Objects)
                {
                    for (int j = 0; j < room.NumObjects; j++)
                    {
                        Bitmap obj = decoder.DecodeObject(room, j);
                        if (obj != null)
                        {
                            Save(obj, folder, string.Format("Room#{0} Obj#{1} Img#0.png", i, j));
                            count++;
                        }
                    }
                }

                if (onProgress != null) onProgress(i + 1, rooms.Count);
            }

            return count;
        }

        private static void Save(Bitmap bitmap, string folder, string fileName)
        {
            bitmap.Save(Path.Combine(folder, fileName), ImageFormat.Png);
        }
    }
}
