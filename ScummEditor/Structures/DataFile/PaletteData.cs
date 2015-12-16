using System.Drawing;

namespace ScummEditor.Structures.DataFile
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

        public override string BlockType
        {
            get { return _blockType; }
        }

        public override void CalculateBlockSize()
        {
            base.CalculateBlockSize();
            BlockSize += 3 * (uint)Colors.Length;
        }

        public override void LoadFromBinaryReader(System.IO.Stream binaryReader)
        {
            base.LoadFromBinaryReader(binaryReader);

            Colors = new Color[256];
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

            foreach (Color color in Colors)
            {
                binaryWriter.WriteByte(color.R);
                binaryWriter.WriteByte(color.G);
                binaryWriter.WriteByte(color.B);
            }
        }
    }
}