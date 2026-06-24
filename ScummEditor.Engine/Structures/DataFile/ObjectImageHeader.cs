using System.Collections.Generic;

namespace ScummEditor.Engine.Structures.DataFile
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

        /// <summary>v7-only leading version word (0 on v5/v6).</summary>
        public uint Version { get; set; }
        /// <summary>v7-only 3 reserved bytes between height and actor direction (null on v5/v6).</summary>
        public byte[] Unknown2 { get; set; }
        /// <summary>v7-only actor direction byte (0 on v5/v6).</summary>
        public byte ActorDirection { get; set; }

        private bool IsV7
        {
            get { return _gameInfo != null && _gameInfo.ScummVersion == 7; }
        }

        public override void CalculateBlockSize()
        {
            base.CalculateBlockSize();

            if (IsV7)
            {
                // version:4 + obj_id:2 + image_count:2 + x:2 + y:2 + width:2 + height:2 + unk:3 +
                // actordir:1 + hotspot_num:2 + hotspots:4*n
                BlockSize += (uint)(20 + 2 + 4 * Hotspots.Count);
                return;
            }

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

            if (IsV7)
            {
                LoadScummV7(binaryReader);
                return;
            }

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

        /// <summary>
        /// v7 IMHD: version:32le, obj_id:16le, image_count:16le, x:16le, y:16le, width:16le, height:16le,
        /// 3 reserved bytes, actor dir:8, then (when present) hotspot_num:16le + that many int16 x/y
        /// hotspots. The decoder only needs image_count (how many IMnn follow) and width/height.
        /// </summary>
        private void LoadScummV7(System.IO.Stream binaryReader)
        {
            long bodyEnd = BlockOffSet + BlockSize;

            Version = binaryReader.ReadUint32();
            Id = binaryReader.ReadUint16();
            NumImages = binaryReader.ReadUint16();
            X = binaryReader.ReadUint16();
            Y = binaryReader.ReadUint16();
            Width = binaryReader.ReadUint16();
            Height = binaryReader.ReadUint16();
            Unknown2 = binaryReader.ReadBytes(3);
            ActorDirection = binaryReader.ReadByte1();

            Hotspots = new List<Hotspot>();
            if (binaryReader.Position + 2 <= bodyEnd)
            {
                NumHotspots = binaryReader.ReadUint16();
                for (int i = 0; i < NumHotspots && binaryReader.Position + 4 <= bodyEnd; i++)
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

            if (IsV7)
            {
                binaryWriter.Write(Version);
                binaryWriter.Write(Id);
                binaryWriter.Write(NumImages);
                binaryWriter.Write(X);
                binaryWriter.Write(Y);
                binaryWriter.Write(Width);
                binaryWriter.Write(Height);
                binaryWriter.WriteBytes(Unknown2);
                binaryWriter.WriteByte(ActorDirection);
                binaryWriter.Write(NumHotspots);
                foreach (Hotspot hotspot in Hotspots)
                {
                    binaryWriter.Write(hotspot.X);
                    binaryWriter.Write(hotspot.Y);
                }
                return;
            }

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
}