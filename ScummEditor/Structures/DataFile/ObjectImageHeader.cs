using System.Collections.Generic;

namespace ScummEditor.Structures.DataFile
{
    public class ObjectImageHeader : BlockBase, IImageSize
    {
        /*
        IMHD
          obj id       : 2 bytes
          num imnn     : 2 bytes
          num zpnn     : 2 bytes
          unknown      : 2 bytes (unknow BE or LE!?)
          x            : 2 bytes
          y            : 2 bytes
          width        : 2 bytes
          height       : 2 bytes
         
          //SCUMM V6 ONLY
          num hotspots : 2 bytes (usually one for each IMnn, but there is one even if no IMnn is present)
          hotspots * num hotspots
            x          : 2 bytes (signed)
            y          : 2 bytes (signed)

        */
        public override string BlockType
        {
            get { return "IMHD"; }
        }

        public ObjectImageHeader(BlockBase blockBase)
            : base(blockBase)
        {
            Hotspots = new List<Hotspot>();
        }

        public ushort Id { get; set; }
        public ushort NumImages { get; set; }
        public ushort NumZPlanes { get; set; }
        public ushort Unknown { get; set; }
        public ushort X { get; set; }
        public ushort Y { get; set; }
        public ushort Width { get; set; }
        public ushort Height { get; set; }
        public ushort NumHotspots { get; set; }
        public List<Hotspot> Hotspots { get; set; }

        public override void CalculateBlockSize()
        {
            base.CalculateBlockSize();

            uint block = 0;
            block += 2; //id
            block += 2; //NumImages
            block += 2; //NumZPlanes
            block += 2; //Unknown
            block += 2; //X
            block += 2; //Y
            block += 2; //Width
            block += 2; //Height

            if (_gameInfo.ScummVersion == 6)
            {
                block += 2; //NumHotspots
                block += (uint)(4 * Hotspots.Count);
            }

            BlockSize += block;

        }

        public override void LoadFromBinaryReader(System.IO.Stream binaryReader)
        {
            base.LoadFromBinaryReader(binaryReader);

            Id = binaryReader.ReadUint16();
            NumImages = binaryReader.ReadUint16();
            NumZPlanes = binaryReader.ReadUint16();
            Unknown = binaryReader.ReadUint16();
            X = binaryReader.ReadUint16();
            Y = binaryReader.ReadUint16();
            Width = binaryReader.ReadUint16();
            Height = binaryReader.ReadUint16();

            if (_gameInfo.ScummVersion == 6)
            {
                NumHotspots = binaryReader.ReadUint16();
                Hotspots = new List<Hotspot>();
                for (int i = 0; i < NumHotspots; i++)
                {
                    var item = new Hotspot();
                    item.X = binaryReader.ReadInt16();
                    item.Y = binaryReader.ReadInt16();

                    Hotspots.Add(item);
                }
            }
        }

        public override void SaveToBinaryWriter(System.IO.Stream binaryWriter)
        {
            base.SaveToBinaryWriter(binaryWriter);

            binaryWriter.Write(Id);
            binaryWriter.Write(NumImages);
            binaryWriter.Write(NumZPlanes);
            binaryWriter.Write(Unknown);
            binaryWriter.Write(X);
            binaryWriter.Write(Y);
            binaryWriter.Write(Width);
            binaryWriter.Write(Height);

            if (_gameInfo.ScummVersion == 6)
            {
                binaryWriter.Write(NumHotspots);
                foreach (Hotspot hotspot in Hotspots)
                {
                    binaryWriter.Write(hotspot.X);
                    binaryWriter.Write(hotspot.Y);
                }
            }
        }

    }

    public class Hotspot
    {
        public short X { get; set; }
        public short Y { get; set; }
    }
}