using ScummEditor.Engine.Structures.DataFile;
using ScummEditor.Engine.Structures.IndexFile;

namespace ScummEditor.Engine.Structures
{
    /// <summary>
    /// Game data for the SCUMM v3 "old bundle" (GF_OLD_BUNDLE) games (Loom EGA, Indy3 EGA, Zak DOS):
    /// one NN.LFL room file per room (an untagged [size:uint16] chunk chain) and a 00.LFL magic-format
    /// index, all XOR 0xFF. The containers keep their decrypted bytes verbatim, so loading and saving
    /// an unedited game round-trips byte-for-byte; the typed room/resource readers and the directory
    /// offset remap are layered on top as the image/text editing features are added.
    /// </summary>
    public class ScummGameDataV3OldBundle : ScummGameData
    {
        protected override ScummDataFile CreateDataFile()
        {
            return new ScummV3OldBundleDataFile(null, LoadedGameInfo);
        }

        protected override ScummIndexFile CreateIndexFile()
        {
            return new ScummV3OldBundleIndexFile(LoadedGameInfo);
        }

        protected override void AfterLoad()
        {
            LoadV3Charsets();
        }

        /// <summary>
        /// No-op for now: the index and room files are kept verbatim, so an unedited game needs no
        /// linking. The (roomNumber, offset) directory entries are remapped here once editing of the
        /// resources they point at is implemented.
        /// </summary>
        protected override void LinkDataAndIndexFile()
        {
        }

        protected override void FixUpIndexOffsets()
        {
        }
    }
}
