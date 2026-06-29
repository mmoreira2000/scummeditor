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
    /// "Room#i Obj#j Img#0.png", "Room#i ZP#z.png" for a background z-plane, "Room#i Obj#j Img#0 ZP#z.png"
    /// for an object z-plane, "Room#i Akos#j Cel#k.png" for a costume cel) where i is the room's game-wide
    /// order, so export and import map 1:1; an edit is written back by the normal save (the v8 index
    /// relocation handles the size change). SMAP backgrounds/objects, BOMP-coded objects, AKOS cels and
    /// z-planes (occlusion masks) all round-trip.
    /// </summary>
    public static class ScummV8GraphicsBatch
    {
        public class ExportOptions
        {
            public bool Backgrounds = true;
            public bool Objects = true;
            public bool Costumes = true;
            public bool BackgroundZPlanes = true;
            public bool ObjectZPlanes = true;
        }

        /// <summary>The room (LFLF) blocks in a stable, game-wide order (both data files, file order).</summary>
        public static List<DiskBlock> EnumerateRooms(ScummGameData game)
        {
            var rooms = new List<DiskBlock>();
            if (game.DataDisks == null) return rooms;
            foreach (DataDisk disk in game.DataDisks)
            {
                rooms.AddRange(disk.Tree.GetLFLFs());
            }
            return rooms;
        }

        public static int Export(ScummGameData game, string folder, ExportOptions options, Action<int, int> onProgress, Func<bool> shouldCancel = null)
        {
            var decoder = new ScummV8ImageDecoder();
            List<DiskBlock> rooms = EnumerateRooms(game);
            int count = 0;

            for (int i = 0; i < rooms.Count; i++)
            {
                if (shouldCancel != null && shouldCancel()) break;
                RoomBlock room = rooms[i].GetROOM();

                if (options.Backgrounds)
                {
                    Bitmap bg = decoder.DecodeBackground(room);
                    if (bg != null) { Save(bg, folder, string.Format("Room#{0}.png", i)); count++; }
                }

                if (options.BackgroundZPlanes)
                {
                    int zCount = decoder.CountBackgroundZPlanes(room);
                    for (int z = 0; z < zCount; z++)
                    {
                        Bitmap zp = decoder.DecodeBackgroundZPlane(room, z);
                        if (zp != null) { Save(zp, folder, string.Format("Room#{0} ZP#{1}.png", i, z)); count++; }
                    }
                }

                if (options.Objects || options.ObjectZPlanes)
                {
                    int objects = ScummV8ImageDecoder.ObjectCount(room);
                    for (int j = 0; j < objects; j++)
                    {
                        if (options.Objects)
                        {
                            Bitmap obj = decoder.DecodeObject(room, j);
                            if (obj != null) { Save(obj, folder, string.Format("Room#{0} Obj#{1} Img#0.png", i, j)); count++; }
                        }
                        if (options.ObjectZPlanes)
                        {
                            int zCount = decoder.CountObjectZPlanes(room, j);
                            for (int z = 0; z < zCount; z++)
                            {
                                Bitmap zp = decoder.DecodeObjectZPlane(room, j, z);
                                if (zp != null) { Save(zp, folder, string.Format("Room#{0} Obj#{1} Img#0 ZP#{2}.png", i, j, z)); count++; }
                            }
                        }
                    }
                }

                if (options.Costumes)
                {
                    List<BlockBase> akosList = rooms[i].Childrens.Where(c => c.BlockType == "AKOS").ToList();
                    for (int j = 0; j < akosList.Count; j++)
                    {
                        int cels = AkosImageDecoder.GetCelCount(akosList[j]);
                        for (int k = 0; k < cels; k++)
                        {
                            System.Drawing.Size sz = AkosImageDecoder.GetCelSize(akosList[j], k);
                            if (sz.Width * sz.Height <= 4) continue; // placeholder slot
                            Bitmap cel = AkosImageDecoder.DecodeCel(akosList[j], k);
                            if (cel != null) { Save(cel, folder, string.Format("Room#{0} Akos#{1} Cel#{2}.png", i, j, k)); count++; }
                        }
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
            List<DiskBlock> rooms = EnumerateRooms(game);
            string[] files = Directory.GetFiles(folder, "Room#*.png");

            for (int f = 0; f < files.Length; f++)
            {
                if (shouldCancel != null && shouldCancel()) break;
                string name = Path.GetFileNameWithoutExtension(files[f]);
                int roomIndex, objectIndex, akosIndex, celIndex, zPlaneIndex;
                if (!TryParseName(name, out roomIndex, out objectIndex, out akosIndex, out celIndex, out zPlaneIndex)) continue;
                if (roomIndex < 0 || roomIndex >= rooms.Count) continue;
                report.Found++;

                try
                {
                    using (var bmp = new Bitmap(files[f]))
                    {
                        if (akosIndex >= 0)
                        {
                            List<BlockBase> akosList = rooms[roomIndex].Childrens.Where(c => c.BlockType == "AKOS").ToList();
                            if (akosIndex >= akosList.Count) { report.Errors.Add(name + ": AKOS index out of range"); continue; }
                            if (!IndexedImageHelper.IsIndexed(bmp)) { report.Errors.Add(name + ": cel must be an indexed PNG"); continue; }
                            AkosImageEncoder.ReplaceCel(akosList[akosIndex], celIndex, IndexedImageHelper.GetIndexMatrix(bmp));
                        }
                        else if (zPlaneIndex >= 0 && objectIndex < 0)
                        {
                            encoder.EncodeBackgroundZPlane(rooms[roomIndex].GetROOM(), zPlaneIndex, bmp);
                        }
                        else if (zPlaneIndex >= 0)
                        {
                            encoder.EncodeObjectZPlane(rooms[roomIndex].GetROOM(), objectIndex, zPlaneIndex, bmp);
                        }
                        else if (objectIndex < 0)
                        {
                            encoder.EncodeBackground(rooms[roomIndex].GetROOM(), bmp);
                        }
                        else
                        {
                            encoder.EncodeObject(rooms[roomIndex].GetROOM(), objectIndex, bmp);
                        }
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

        /// <summary>Parses "Room#i" (background), "Room#i ZP#z" (bg z-plane), "Room#i Obj#j Img#0" (object),
        /// "Room#i Obj#j Img#0 ZP#z" (object z-plane) or "Room#i Akos#j Cel#k" (costume cel). Unused indices
        /// come back as -1.</summary>
        private static bool TryParseName(string name, out int roomIndex, out int objectIndex, out int akosIndex, out int celIndex, out int zPlaneIndex)
        {
            roomIndex = -1; objectIndex = -1; akosIndex = -1; celIndex = -1; zPlaneIndex = -1;
            string[] parts = name.Split(' ');
            if (parts.Length == 0 || !parts[0].StartsWith("Room#")) return false;
            if (!int.TryParse(parts[0].Substring(5), out roomIndex)) return false;
            if (parts.Length == 1) return true; // background

            string akos = parts.FirstOrDefault(p => p.StartsWith("Akos#"));
            if (akos != null)
            {
                string cel = parts.FirstOrDefault(p => p.StartsWith("Cel#"));
                return int.TryParse(akos.Substring(5), out akosIndex)
                    && cel != null && int.TryParse(cel.Substring(4), out celIndex);
            }

            string zp = parts.FirstOrDefault(p => p.StartsWith("ZP#"));
            if (zp != null && !int.TryParse(zp.Substring(3), out zPlaneIndex)) return false;

            string obj = parts.FirstOrDefault(p => p.StartsWith("Obj#"));
            if (obj != null) return int.TryParse(obj.Substring(4), out objectIndex); // object image or object z-plane
            return zp != null; // a bare "Room#i ZP#z" is a background z-plane; anything else here is unknown
        }

        private static void Save(Bitmap bitmap, string folder, string fileName)
        {
            Directory.CreateDirectory(folder);
            bitmap.Save(Path.Combine(folder, fileName), ImageFormat.Png);
            bitmap.Dispose();
        }
    }
}
