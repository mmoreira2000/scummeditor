using System.IO;
using ScummEditor.Engine.Structures.IndexFile;

namespace ScummEditor.Engine.Structures.DataFile
{
    /// <summary>Which image of a v2 / v3-old room a block refers to.</summary>
    public enum OldBundleImageKind { Background, Object, BackgroundZPlane, ObjectZPlane }

    /// <summary>What a v2 / v3-old view block represents (picks the GUI viewer).</summary>
    public enum OldBundleNodeKind { Room, Header, Image, Object, Script, Sound, Costume, Directory }

    /// <summary>
    /// A synthetic, VIEW-ONLY block for the v2 / v3-old (GF_OLD_BUNDLE) games. These games store each
    /// NN.LFL room as a raw byte image with no tagged blocks, so the editor cannot show them in the
    /// BlockBase tree the way v4-v6 do. This block reifies one navigable resource (room header, image,
    /// object, script, sound, costume, index directory) as a BlockBase node so the SAME tree walker and
    /// grouping the other engines use renders v2/v3 too - giving "OC 000", "OI 000", "LS 000", "SC 000"
    /// exactly like v4.
    ///
    /// It carries only positions/refs into the verbatim RawContent; it is never serialized. The owning
    /// ScummV3OldBundleDataFile/IndexFile write their bytes verbatim and ignore Childrens, so these blocks
    /// cannot affect a save. All persistence overrides are deliberate no-ops.
    /// </summary>
    public class OldBundleBlock : BlockBase
    {
        private readonly string _blockType;

        public OldBundleBlock(BlockBase parent, GameInfo gameInfo, string blockType, OldBundleNodeKind kind)
            : base(parent, gameInfo)
        {
            _blockType = blockType;
            Kind = kind;
        }

        public override string BlockType { get { return _blockType; } }

        public OldBundleNodeKind Kind { get; private set; }

        // --- payload (only the fields relevant to Kind are set) ----------------

        /// <summary>The room file this resource lives in (its RawContent holds the bytes).</summary>
        public ScummV3OldBundleDataFile DataFile;

        public int RoomNo;
        public bool IsV2;
        public bool IsIndy3;

        // Image
        public OldBundleImageKind ImageKind;
        public int ObjectIndex;

        // Object (code + metadata + verb ranges)
        public OldBundleObjectInfo ObjectInfo;

        // Script (bytecode window into DataFile.RawContent) + a display title / local-script id
        public int Start;
        public int End;
        public int ScriptId = -1;
        public string Title;

        // Sound / Costume (offset into DataFile.RawContent)
        public int Offset;
        public int ResourceIndex; // sound or costume id from the index directory

        // Directory (index resource directory listing)
        public V3OldResourceDirectory Directory;

        // --- view-only: never persisted ---------------------------------------
        public override void LoadFromBinaryReader(Stream binaryReader) { }
        public override void SaveToBinaryWriter(Stream binaryWriter) { }
        public override void CalculateBlockSize() { BlockSize = 0; }
        public override void CalculateOffsets() { }
    }
}
