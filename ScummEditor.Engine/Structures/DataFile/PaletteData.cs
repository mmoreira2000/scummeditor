using System.Drawing;

namespace ScummEditor.Engine.Structures.DataFile
{
    /*
    colors : 256 times
    r    : 1 byte
    g    : 1 byte
    b    : 1 byte
    APAL   : others APAL blocks may follow
    */
    public class PaletteData : BlockBase
    {

        public PaletteData(BlockBase blockBase, string blockType)
            : base(blockBase)
        {
            _blockType = blockType;
        }

        public Color[] Colors { get; set; }
        private readonly string _blockType;

        /// <summary>
        /// The v4 PA block has a 2-byte prefix (a small index/count marker) before the 256 RGB
        /// triples. v5/v6 CLUT/APAL have no such prefix. Kept verbatim for byte-exact save.
        /// </summary>
        public byte[] V4Prefix { get; set; }

        public override string BlockType
        {
            get { return _blockType; }
        }

        public override void CalculateBlockSize()
        {
            base.CalculateBlockSize();
            BlockSize += 3 * (uint)Colors.Length;
            if (V4Prefix != null)
            {
                BlockSize += (uint)V4Prefix.Length;
            }
        }

        public override void LoadFromBinaryReader(System.IO.Stream binaryReader)
        {
            base.LoadFromBinaryReader(binaryReader);

            if (IsSmallHeader)
            {
                V4Prefix = binaryReader.ReadBytes(2);
            }

            // The colour count comes from the block size, not a fixed 256: most rooms hold a full
            // 256-colour palette, but some (e.g. Loom's FM-Towns rooms) store a 16-colour one
            // (block size 56 = 6 header + 2 prefix + 16*3). Deriving it keeps the block byte-exact and
            // lets CalculateBlockSize agree with the loaded size.
            int paletteBytes = (int)BlockSize - HeaderLength - (V4Prefix != null ? V4Prefix.Length : 0);
            int colorCount = paletteBytes > 0 ? paletteBytes / 3 : 0;

            Colors = new Color[colorCount];
            for (int i = 0; i < Colors.Length; i++)
            {
                var r = binaryReader.ReadByte();
                var g = binaryReader.ReadByte();
                var b = binaryReader.ReadByte();

                Colors[i] = Color.FromArgb(r, g, b);
            }
        }

        public override void SaveToBinaryWriter(System.IO.Stream binaryWriter)
        {
            base.SaveToBinaryWriter(binaryWriter);

            if (V4Prefix != null)
            {
                binaryWriter.WriteBytes(V4Prefix);
            }

            foreach (Color color in Colors)
            {
                binaryWriter.WriteByte(color.R);
                binaryWriter.WriteByte(color.G);
                binaryWriter.WriteByte(color.B);
            }
        }
    }
}