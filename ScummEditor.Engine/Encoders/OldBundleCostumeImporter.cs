using System;
using System.Collections.Generic;
using System.Drawing;
using ScummEditor.Engine.Structures.DataFile;
using ScummEditor.Engine.Structures.IndexFile;

namespace ScummEditor.Engine.Encoders
{
    /// <summary>
    /// Imports ONE edited frame PNG back into a v2 / v3-old costume (classic CEL format 0x58, shared by
    /// both families), the per-node counterpart of the batch costume import in ScummV2Graphics /
    /// ScummV3OldGraphics. It re-encodes the frame with CostumeImageEncoderV4, rebuilds the whole costume
    /// resource via CostumeV3Old.BuildWithReplacedFrames, and splices it with the v2 / v3-old writer (whose
    /// ApplyEdit re-points the index + room offsets the size change shifts; the costume chunk carries its
    /// own size word, so sizeWordOffset is the costume offset). RawContent is updated in memory; the user
    /// persists via the normal "Save changes".
    ///
    /// Returns false (with a message) instead of throwing, so the GUI can report a wrong-size / non-indexed
    /// PNG or a v2 edit that overflows a 1-byte offset.
    /// </summary>
    public static class OldBundleCostumeImporter
    {
        public static bool ImportFrame(ScummV3OldBundleDataFile df, ScummV3OldBundleIndexFile index, int roomNo,
            bool isV2, int costumeOffset, int frameIndex, Bitmap png, out string error)
        {
            error = null;
            if (df == null || df.RawContent == null) { error = "The costume has no data to import into."; return false; }
            if (png == null) { error = "No image was loaded."; return false; }

            try
            {
                var costume = new CostumeV3Old(df.RawContent, costumeOffset);
                if (frameIndex < 0 || frameIndex >= costume.Frames.Count)
                {
                    error = "Frame index out of range.";
                    return false;
                }

                var frame = costume.Frames[frameIndex];
                // 16-colour EGA; the 4-arg overload checks the size and that the PNG is indexed (throws).
                byte[] encoded = new CostumeImageEncoderV4().Encode(png, 16, frame.Width, frame.Height);

                byte[] rebuilt = costume.BuildWithReplacedFrames(new Dictionary<int, byte[]> { { frameIndex, encoded } });

                if (isV2)
                    ScummV2Writer.ApplyEdit(df, index, roomNo, costumeOffset, costume.ResourceSize, rebuilt, costumeOffset);
                else
                    ScummV3OldWriter.ApplyEdit(df, index, roomNo, costumeOffset, costume.ResourceSize, rebuilt, costumeOffset);
                return true;
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }
    }
}
