using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;

namespace ScummEditor.Engine.Encoders
{
    /// <summary>
    /// Batch PNG export/import of every SCUMM v8 (The Curse of Monkey Island) room background and object
    /// image, across BOTH data files. v8's room/object images use the IMAG/WRAP/OFFS nesting (not the v5/v6
    /// RMIM/OBIM blocks), so it needs this dedicated path over <see cref="ScummV8ImageDecoder"/> /
    /// <see cref="ScummV8ImageEncoder"/>. Filenames follow the v4-v6 scheme ("Room#i.png",
    /// "Room#i Obj#j Img#0.png") where i is the room's game-wide order, so export and import map 1:1; an
    /// edit is written back by the normal save (the v8 index relocation handles the size change). z-plane
    /// and BOMP-coded object images are not yet covered (see the v8 build notes).
    /// </summary>
    public static class ScummV8GraphicsBatch
    {
        public class ExportOptions
        {
            public bool Backgrounds = true;
            public bool Objects = true;
        }

        /// <summary>The ROOM blocks in a stable, game-wide order (both data files, file order).</summary>
        public static List<RoomBlock> EnumerateRooms(ScummGameData game)
        {
            var rooms = new List<RoomBlock>();
            if (game.DataDisks == null) return rooms;
            foreach (DataDisk disk in game.DataDisks)
            {
                foreach (DiskBlock lflf in disk.Tree.GetLFLFs())
                {
                    rooms.Add(lflf.GetROOM());
                }
            }
            return rooms;
        }

        public static int Export(ScummGameData game, string folder, ExportOptions options, Action<int, int> onProgress, Func<bool> shouldCancel = null)
        {
            var decoder = new ScummV8ImageDecoder();
            List<RoomBlock> rooms = EnumerateRooms(game);
            int count = 0;

            for (int i = 0; i < rooms.Count; i++)
            {
                if (shouldCancel != null && shouldCancel()) break;
                RoomBlock room = rooms[i];

                if (options.Backgrounds)
                {
                    Bitmap bg = decoder.DecodeBackground(room);
                    if (bg != null) { Save(bg, folder, string.Format("Room#{0}.png", i)); count++; }
                }

                if (options.Objects)
                {
                    int objects = ScummV8ImageDecoder.ObjectCount(room);
                    for (int j = 0; j < objects; j++)
                    {
                        Bitmap obj = decoder.DecodeObject(room, j);
                        if (obj != null) { Save(obj, folder, string.Format("Room#{0} Obj#{1} Img#0.png", i, j)); count++; }
                    }
                }

                if (onProgress != null) onProgress(i + 1, rooms.Count);
            }
            return count;
        }

        public static ScummV4GraphicsBatch.ImportReport Import(ScummGameData game, string folder, Action<int, int> onProgress, Func<bool> shouldCancel = null)
        {
            var report = new ScummV4GraphicsBatch.ImportReport();
            var encoder = new ScummV8ImageEncoder();
            List<RoomBlock> rooms = EnumerateRooms(game);
            string[] files = Directory.GetFiles(folder, "Room#*.png");

            for (int f = 0; f < files.Length; f++)
            {
                if (shouldCancel != null && shouldCancel()) break;
                string name = Path.GetFileNameWithoutExtension(files[f]);
                int roomIndex, objectIndex;
                if (!TryParseName(name, out roomIndex, out objectIndex)) continue;
                if (roomIndex < 0 || roomIndex >= rooms.Count) continue;
                report.Found++;

                try
                {
                    using (var bmp = new Bitmap(files[f]))
                    {
                        if (objectIndex < 0) encoder.EncodeBackground(rooms[roomIndex], bmp);
                        else encoder.EncodeObject(rooms[roomIndex], objectIndex, bmp);
                    }
                    report.Imported++;
                }
                catch (Exception ex)
                {
                    report.Errors.Add(name + ": " + ex.Message);
                }
                if (onProgress != null) onProgress(f + 1, files.Length);
            }
            return report;
        }

        /// <summary>Parses "Room#i" (background) or "Room#i Obj#j Img#0" (object); objectIndex = -1 for bg.</summary>
        private static bool TryParseName(string name, out int roomIndex, out int objectIndex)
        {
            roomIndex = -1;
            objectIndex = -1;
            string[] parts = name.Split(' ');
            if (parts.Length == 0 || !parts[0].StartsWith("Room#")) return false;
            if (!int.TryParse(parts[0].Substring(5), out roomIndex)) return false;
            if (parts.Length == 1) return true; // background
            // object: "Room#i Obj#j Img#0"
            string obj = parts.FirstOrDefault(p => p.StartsWith("Obj#"));
            if (obj == null) return false;
            return int.TryParse(obj.Substring(4), out objectIndex);
        }

        private static void Save(Bitmap bitmap, string folder, string fileName)
        {
            Directory.CreateDirectory(folder);
            bitmap.Save(Path.Combine(folder, fileName), ImageFormat.Png);
            bitmap.Dispose();
        }
    }
}
