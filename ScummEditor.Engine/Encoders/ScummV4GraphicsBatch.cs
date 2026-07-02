using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Exceptions;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;

namespace ScummEditor.Engine.Encoders
{
    /// <summary>
    /// Batch PNG export/import of every SCUMM v4 image - room backgrounds, object images, their
    /// z-plane masks and costume frames - across ALL of a game's DISKnn.LEC containers. The v5/v6
    /// batch (ExportResources/ImportResources) walks LFLF blocks, which v4 has no equivalent of (its
    /// rooms are spread over LE/FO/LF disks), so v4 uses this dedicated path. Filenames match the
    /// v5/v6 scheme (Room#i, "Room#i ZP#j", "Room#i Obj#j Img#0", "Room#i Obj#j Img#0 ZP#l",
    /// "Room#i Costume#j FrameIndex#k") where i is the room's position in the game-wide room order, so
    /// export and import map 1:1. Decoding/encoding reuse the same per-image classes the v4 viewers
    /// use, so a no-op round-trip is pixel-identical and the change is written back by the v4 save
    /// fix-up.
    /// </summary>
    public static class ScummV4GraphicsBatch
    {
        public class ExportOptions
        {
            public bool Backgrounds = true;
            public bool Objects = true;
            public bool Costumes = true;
            public bool BackgroundZPlanes = true;
            public bool ObjectZPlanes = true;
            public bool Transparency;
        }

        public class ImportReport
        {
            public int Found;
            public int Imported;
            public List<string> Errors = new List<string>();
        }

        /// <summary>
        /// The room containers in a stable, game-wide order: v4 LF disk blocks (one room each, spread
        /// over the DISKnn.LEC files) or v3 "GF_OLD256" room files (one NN.LFL each). Both expose the
        /// same v4 room/costume blocks, so the batch walks them uniformly.
        /// </summary>
        public static List<IScummRoomContainer> EnumerateRooms(ScummGameData game)
        {
            var rooms = new List<IScummRoomContainer>();
            if (game.DataDisks == null) return rooms;
            foreach (DataDisk disk in game.DataDisks)
            {
                CollectDiskBlocks(disk.Tree, rooms);
            }
            return rooms;
        }

        private static void CollectDiskBlocks(BlockBase block, List<IScummRoomContainer> acc)
        {
            var container = block as IScummRoomContainer;
            if (container != null) acc.Add(container);
            foreach (BlockBase child in block.Childrens) CollectDiskBlocks(child, acc);
        }

        public static int Export(ScummGameData game, string folder, ExportOptions options, Action<int, int> onProgress, Func<bool> shouldCancel = null)
        {
            var decoder = new ScummV4ImageDecoder();
            var costumeDecoder = new CostumeImageDecoderV4();
            List<IScummRoomContainer> rooms = EnumerateRooms(game);
            int count = 0;

            for (int i = 0; i < rooms.Count; i++)
            {
                if (shouldCancel != null && shouldCancel()) break;

                ScummV4RoomBlock room = rooms[i].GetRoom();
                if (room != null && room.GetBM() != null)
                {
                    if (options.Backgrounds)
                    {
                        Bitmap background = decoder.DecodeBackground(room);
                        if (background != null)
                        {
                            Save(background, folder, string.Format("Room#{0:D3}.png", i));
                            count++;
                        }
                    }

                    if (options.BackgroundZPlanes)
                    {
                        int zCount = decoder.CountBackgroundZPlanes(room);
                        for (int z = 0; z < zCount; z++)
                        {
                            Bitmap zplane = decoder.DecodeBackgroundZPlane(room, z);
                            if (zplane != null)
                            {
                                Save(zplane, folder, string.Format("Room#{0:D3} ZP#{1:D3}.png", i, z));
                                count++;
                            }
                        }
                    }
                }

                if (room != null && (options.Objects || options.ObjectZPlanes))
                {
                    List<ObjectCode> codes = room.GetObjectCodes();
                    List<ScummV4ImageBlock> images = room.GetObjectImages();
                    for (int j = 0; j < images.Count; j++)
                    {
                        ScummV4ImageBlock objectImage = images[j];
                        ObjectCode code = codes.Find(c => c.ObjectId == objectImage.ObjectId);
                        if (code == null || code.Width == 0 || code.Height == 0)
                        {
                            continue; // hotspot-only object: no pixels to export
                        }

                        if (options.Objects)
                        {
                            Bitmap obj = decoder.DecodeObject(room, objectImage, code);
                            if (obj != null)
                            {
                                Save(obj, folder, string.Format("Room#{0:D3} Obj#{1:D3} Img#000.png", i, j));
                                count++;
                            }
                        }

                        if (options.ObjectZPlanes)
                        {
                            int zCount = decoder.CountObjectZPlanes(room, objectImage, code);
                            for (int z = 0; z < zCount; z++)
                            {
                                Bitmap zplane = decoder.DecodeObjectZPlane(room, objectImage, code, z);
                                if (zplane != null)
                                {
                                    Save(zplane, folder, string.Format("Room#{0:D3} Obj#{1:D3} Img#000 ZP#{2:D3}.png", i, j, z));
                                    count++;
                                }
                            }
                        }
                    }
                }

                if (options.Costumes)
                {
                    List<CostumeV4> costumes = rooms[i].GetCostumes();
                    for (int j = 0; j < costumes.Count; j++)
                    {
                        CostumeV4 costume = costumes[j];
                        Color[] palette = CostumeV4PaletteResolver.Resolve(costume);
                        for (int k = 0; k < costume.Frames.Count; k++)
                        {
                            Bitmap frame = costumeDecoder.Decode(costume.Frames[k], costume.PaletteSize, palette, options.Transparency);
                            if (frame != null)
                            {
                                Save(frame, folder, string.Format("Room#{0:D3} Costume#{1:D3} FrameIndex#{2:D3}.png", i, j, k));
                                count++;
                            }
                        }
                    }
                }

                if (onProgress != null) onProgress(i + 1, rooms.Count);
            }

            return count;
        }

        public static ImportReport Import(ScummGameData game, string folder, Action<int, int> onProgress)
        {
            var report = new ImportReport();
            // v3 "GF_OLD256" rooms reuse the v4 block layout but store FM-Towns codecs, so they need
            // the raw256 re-encoder; v4 uses the standard VGA/EGA codec picker.
            ScummV4ImageEncoder encoder = game.LoadedGameInfo != null && game.LoadedGameInfo.ScummVersion == 3
                ? new ScummV3ImageEncoder()
                : new ScummV4ImageEncoder();
            var costumeEncoder = new CostumeImageEncoderV4();
            List<IScummRoomContainer> rooms = EnumerateRooms(game);

            List<ImageInfo> files = Directory.GetFiles(folder, "*.png").Select(f => new ImageInfo(f)).ToList();
            report.Found = files.Count;

            for (int i = 0; i < files.Count; i++)
            {
                ImageInfo file = files[i];
                string name = Path.GetFileName(file.Filename);

                if (file.ImageType == ImageType.Unknown || file.RoomIndex < 0 || file.RoomIndex >= rooms.Count)
                {
                    report.Errors.Add(name + ": unrecognized file name (skipped)");
                    if (onProgress != null) onProgress(i + 1, files.Count);
                    continue;
                }

                IScummRoomContainer lf = rooms[file.RoomIndex];
                ScummV4RoomBlock room = lf.GetRoom();

                try
                {
                    using (var bitmap = (Bitmap)Image.FromFile(file.Filename))
                    {
                        ImportOne(file, room, lf, bitmap, encoder, costumeEncoder, report, name);
                    }
                }
                catch (ImageEncodeException ex)
                {
                    report.Errors.Add(name + ": " + ex.Message);
                }

                if (onProgress != null) onProgress(i + 1, files.Count);
            }

            return report;
        }

        private static void ImportOne(ImageInfo file, ScummV4RoomBlock room, IScummRoomContainer lf, Bitmap bitmap,
            ScummV4ImageEncoder encoder, CostumeImageEncoderV4 costumeEncoder, ImportReport report, string name)
        {
            if (file.ImageType == ImageType.Costume)
            {
                List<CostumeV4> costumes = lf.GetCostumes();
                if (file.CostumeIndex < 0 || file.CostumeIndex >= costumes.Count)
                {
                    report.Errors.Add(name + ": costume not found");
                    return;
                }
                CostumeV4 costume = costumes[file.CostumeIndex];
                if (file.FrameIndex < 0 || file.FrameIndex >= costume.Frames.Count)
                {
                    report.Errors.Add(name + ": costume frame not found");
                    return;
                }
                CostumeImageData frame = costume.Frames[file.FrameIndex];
                if (bitmap.Width != frame.Width || bitmap.Height != frame.Height)
                {
                    report.Errors.Add(string.Format("{0}: the frame must be {1}x{2} (the original size), but it is {3}x{4}.",
                        name, frame.Width, frame.Height, bitmap.Width, bitmap.Height));
                    return;
                }
                byte[] rle = costumeEncoder.Encode(bitmap, costume.PaletteSize);
                costume.ReplaceFrameImage(file.FrameIndex, rle);
                report.Imported++;
                return;
            }

            if (room == null)
            {
                report.Errors.Add(name + ": room has no image data");
                return;
            }

            switch (file.ImageType)
            {
                case ImageType.Background:
                    encoder.EncodeBackground(room, bitmap);
                    report.Imported++;
                    break;

                case ImageType.ZPlane:
                    encoder.EncodeBackgroundZPlane(room, file.ZPlaneIndex, bitmap);
                    report.Imported++;
                    break;

                case ImageType.Object:
                case ImageType.ObjectsZPlane:
                    ScummV4ImageBlock objectImage;
                    ObjectCode code;
                    if (!TryResolveObject(room, file.ObjectIndex, out objectImage, out code))
                    {
                        report.Errors.Add(name + ": object image not found");
                        return;
                    }
                    if (file.ImageType == ImageType.Object)
                    {
                        encoder.EncodeObject(room, objectImage, code, bitmap);
                    }
                    else
                    {
                        encoder.EncodeObjectZPlane(room, objectImage, code, file.ZPlaneIndex, bitmap);
                    }
                    report.Imported++;
                    break;
            }
        }

        private static bool TryResolveObject(ScummV4RoomBlock room, int objectIndex, out ScummV4ImageBlock image, out ObjectCode code)
        {
            image = null;
            code = null;
            List<ScummV4ImageBlock> images = room.GetObjectImages();
            if (objectIndex < 0 || objectIndex >= images.Count) return false;

            ScummV4ImageBlock found = images[objectIndex];
            image = found;
            code = room.GetObjectCodes().Find(c => c.ObjectId == found.ObjectId);
            return code != null && code.Width > 0 && code.Height > 0;
        }

        private static void Save(Bitmap bitmap, string folder, string fileName)
        {
            bitmap.Save(Path.Combine(folder, fileName), ImageFormat.Png);
        }
    }
}
