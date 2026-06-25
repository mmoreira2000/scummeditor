using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using ScummEditor.Engine.Exceptions;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;

namespace ScummEditor.Engine.Encoders
{
    /// <summary>
    /// Batch PNG export/import of every image in a SCUMM v5/v6 game (room backgrounds, object images,
    /// their z-plane masks and costume frames), the counterpart of ScummV4GraphicsBatch. This is the
    /// logic the Export/Import "Game Graphics" dialogs used to run inline; it now lives in the engine
    /// so any GUI (or a headless tool) can drive it. Filenames: Room#i, "Room#i ZP#j",
    /// "Room#i Obj#j Img#k", "Room#i Obj#j Img#k ZP#l", "Room#i Costume#j FrameIndex#k", where i is
    /// the LFLF disk index.
    /// </summary>
    public static class ScummV5V6GraphicsBatch
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

        public static int Export(ScummV5V6DataFile dataFile, string folder, ExportOptions options, Action<int, int> onProgress, Func<bool> shouldCancel = null)
        {
            List<DiskBlock> diskBlocks = dataFile.GetLFLFs();
            var convert = new ImageDepthConversor();
            int count = 0;

            for (int i = 0; i < diskBlocks.Count; i++)
            {
                if (shouldCancel != null && shouldCancel()) break;

                RoomBlock room = diskBlocks[i].GetROOM();

                if (options.Backgrounds)
                {
                    Bitmap bg = ImageResourceCodec.Decode(room, null, ImageType.Background, 0, 0, 0, 0, options.Transparency);
                    if (bg != null) { Save(bg, folder, string.Format("Room#{0}.png", i)); count++; }
                }

                if (options.BackgroundZPlanes)
                {
                    List<ZPlane> zPlanes = room.GetRMIM().GetIM00().GetZPlanes();
                    for (int j = 0; j < zPlanes.Count; j++)
                    {
                        Bitmap zp = ImageResourceCodec.Decode(room, null, ImageType.ZPlane, 0, 0, j, 0, false);
                        if (zp != null)
                        {
                            zp = convert.CopyToBpp(zp, 1, new[] { Color.Black, Color.White });
                            Save(zp, folder, string.Format("Room#{0} ZP#{1}.png", i, j));
                            count++;
                        }
                    }
                }

                if (options.Objects || options.ObjectZPlanes)
                {
                    List<ObjectImage> obims = room.GetOBIMs();
                    for (int j = 0; j < obims.Count; j++)
                    {
                        List<ImageData> images = obims[j].GetIMxx();
                        for (int k = 0; k < images.Count; k++)
                        {
                            if (options.Objects)
                            {
                                Bitmap img = ImageResourceCodec.Decode(room, null, ImageType.Object, j, k, 0, 0, options.Transparency);
                                if (img != null) { Save(img, folder, string.Format("Room#{0} Obj#{1} Img#{2}.png", i, j, k)); count++; }
                            }

                            if (options.ObjectZPlanes)
                            {
                                List<ZPlane> zplanes = images[k].GetZPlanes();
                                for (int l = 0; l < zplanes.Count; l++)
                                {
                                    Bitmap zp = ImageResourceCodec.Decode(room, null, ImageType.ObjectsZPlane, j, k, l, 0, false);
                                    if (zp != null)
                                    {
                                        zp = convert.CopyToBpp(zp, 1, new[] { Color.Black, Color.White });
                                        Save(zp, folder, string.Format("Room#{0} Obj#{1} Img#{2} ZP#{3}.png", i, j, k, l));
                                        count++;
                                    }
                                }
                            }
                        }
                    }
                }

                if (options.Costumes)
                {
                    List<Costume> costumes = diskBlocks[i].GetCostumes();
                    for (int j = 0; j < costumes.Count; j++)
                    {
                        Costume costume = costumes[j];
                        for (int k = 0; k < costume.Pictures.Count; k++)
                        {
                            byte[] data = costume.Pictures[k].ImageData;
                            if (data.Length == 0 || (data.Length == 1 && data[0] == 0)) continue; // empty frame

                            Bitmap img = ImageResourceCodec.Decode(room, costume, ImageType.Costume, 0, k, 0, 0, options.Transparency);
                            if (img != null) { Save(img, folder, string.Format("Room#{0} Costume#{1} FrameIndex#{2}.png", i, j, k)); count++; }
                        }
                    }

                    // v7 AKOS costumes (the COST list above is empty for v7; AKOS is empty for v5/v6). Cels are
                    // saved as indexed PNGs carrying the raw costume-colour indexes, so re-import is exact.
                    List<BlockBase> akosList = diskBlocks[i].Childrens.Where(c => c.BlockType == "AKOS").ToList();
                    for (int j = 0; j < akosList.Count; j++)
                    {
                        BlockBase akos = akosList[j];
                        int cels = AkosImageDecoder.GetCelCount(akos);
                        for (int k = 0; k < cels; k++)
                        {
                            Size sz = AkosImageDecoder.GetCelSize(akos, k);
                            if (sz.Width * sz.Height <= 4) continue; // tiny placeholder cel (unused animation slot)

                            Bitmap cel = AkosImageDecoder.DecodeCel(akos, k);
                            if (cel != null)
                            {
                                Save(cel, folder, string.Format("Room#{0} Akos#{1} Cel#{2}.png", i, j, k));
                                cel.Dispose();
                                count++;
                            }
                        }
                    }
                }

                if (onProgress != null) onProgress(i + 1, diskBlocks.Count);
            }

            return count;
        }

        public static ImportReport Import(ScummV5V6DataFile dataFile, string folder, Action<int, int> onProgress)
        {
            var report = new ImportReport();
            List<DiskBlock> diskBlocks = dataFile.GetLFLFs();
            List<ImageInfo> files = Directory.GetFiles(folder, "*.png").Select(f => new ImageInfo(f)).ToList();
            report.Found = files.Count;

            for (int i = 0; i < files.Count; i++)
            {
                ImageInfo file = files[i];
                string name = Path.GetFileName(file.Filename);

                if (file.ImageType == ImageType.Unknown || file.RoomIndex < 0 || file.RoomIndex >= diskBlocks.Count)
                {
                    report.Errors.Add(name + ": unrecognized file name (skipped)");
                    if (onProgress != null) onProgress(i + 1, files.Count);
                    continue;
                }

                RoomBlock room = diskBlocks[file.RoomIndex].GetROOM();
                try
                {
                    using (var bitmap = (Bitmap)Image.FromFile(file.Filename))
                    {
                        if (file.ImageType == ImageType.AkosCostume)
                        {
                            // v7 AKOS costume cel: re-encode the indexed PNG's raw indexes into the cel.
                            List<BlockBase> akosList = diskBlocks[file.RoomIndex].Childrens.Where(c => c.BlockType == "AKOS").ToList();
                            if (file.AkosIndex < 0 || file.AkosIndex >= akosList.Count)
                            {
                                throw new ImageEncodeException("no AKOS costume #" + file.AkosIndex + " in room " + file.RoomIndex);
                            }
                            if (!IndexedImageHelper.IsIndexed(bitmap))
                            {
                                throw new ImageEncodeException("AKOS cel must be an indexed PNG (re-export it and edit without converting to RGB)");
                            }
                            AkosImageEncoder.ReplaceCel(akosList[file.AkosIndex], file.CelIndex, IndexedImageHelper.GetIndexMatrix(bitmap));
                        }
                        else if (file.ImageType == ImageType.Costume)
                        {
                            // v7 costumes are AKOS (not the v5/v6 COST format), so GetCostumes() is empty
                            // for a v7 game; guard the index so a stray costume PNG is reported, not a crash.
                            List<Costume> costumes = diskBlocks[file.RoomIndex].GetCostumes();
                            if (file.CostumeIndex < 0 || file.CostumeIndex >= costumes.Count)
                            {
                                throw new ImageEncodeException("no costume #" + file.CostumeIndex + " in room " + file.RoomIndex);
                            }
                            ImageResourceCodec.Encode(room, costumes[file.CostumeIndex], ImageType.Costume, 0, file.FrameIndex, 0, bitmap, ImageEncoder.EncodeTypeSettings.AutoDetect);
                        }
                        else
                        {
                            // Guard the object/image/z-plane indexes so a stray or out-of-range PNG is reported
                            // (ImageEncodeException, caught below) instead of throwing IndexOutOfRange.
                            ValidateRoomImageIndices(room, file);
                            ImageResourceCodec.Encode(room, null, file.ImageType, file.ObjectIndex, file.ImageIndex, file.ZPlaneIndex, bitmap, ImageEncoder.EncodeTypeSettings.AutoDetect);
                        }
                    }
                    report.Imported++;
                }
                catch (ImageEncodeException ex)
                {
                    report.Errors.Add(name + ": " + ex.Message);
                }

                if (onProgress != null) onProgress(i + 1, files.Count);
            }

            return report;
        }

        /// <summary>Throws ImageEncodeException when a background-z-plane / object / object-z-plane file's
        /// indexes do not exist in the room, so a stray PNG is reported instead of crashing the import.</summary>
        private static void ValidateRoomImageIndices(RoomBlock room, ImageInfo file)
        {
            if (file.ImageType == ImageType.Object || file.ImageType == ImageType.ObjectsZPlane)
            {
                List<ObjectImage> obims = room.GetOBIMs();
                if (file.ObjectIndex < 0 || file.ObjectIndex >= obims.Count)
                {
                    throw new ImageEncodeException("no object #" + file.ObjectIndex + " in room " + file.RoomIndex);
                }
                List<ImageData> imgs = obims[file.ObjectIndex].GetIMxx();
                if (file.ImageIndex < 0 || file.ImageIndex >= imgs.Count)
                {
                    throw new ImageEncodeException("no image #" + file.ImageIndex + " in object #" + file.ObjectIndex);
                }
                if (file.ImageType == ImageType.ObjectsZPlane)
                {
                    List<ZPlane> zps = imgs[file.ImageIndex].GetZPlanes();
                    if (file.ZPlaneIndex < 0 || file.ZPlaneIndex >= zps.Count)
                    {
                        throw new ImageEncodeException("no z-plane #" + file.ZPlaneIndex + " in object #" + file.ObjectIndex);
                    }
                }
            }
            else if (file.ImageType == ImageType.ZPlane)
            {
                List<ZPlane> zps = room.GetRMIM().GetIM00().GetZPlanes();
                if (file.ZPlaneIndex < 0 || file.ZPlaneIndex >= zps.Count)
                {
                    throw new ImageEncodeException("no background z-plane #" + file.ZPlaneIndex + " in room " + file.RoomIndex);
                }
            }
        }

        private static void Save(Bitmap bitmap, string folder, string fileName)
        {
            bitmap.Save(Path.Combine(folder, fileName), ImageFormat.Png);
        }
    }
}
