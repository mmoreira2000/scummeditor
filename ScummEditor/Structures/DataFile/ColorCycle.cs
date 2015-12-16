using System;
using System.Collections.Generic;
using System.IO;

namespace ScummEditor.Structures.DataFile
{
    /*  
     * Defines the various parts of the palette that can be cycled. 
     * The only known flag is the second bit, which means the cycling should go backwards.
    
    CYCL - Color Cycle (vlc stop on 0x00) (vlc = Variable Length Code)
    cycles  : variable length
        idx   : 1 byte (valid range is [1-16])
        unk   : 2 bytes
        freq  : 2 bytes BE (delay = 16384/freq)
        flags : 2 bytes BE
        start : 1 byte (start/end entries in the palette)
        end   : 1 byte
    close   : 1 byte (must be set to 0 to end the block)  -- BUT on SOME Sam & Max ROOMs, the Cycles ends with an extra 0x00 
    */
    public class ColorCycles : BlockBase
    {
        public ColorCycles(BlockBase blockBase) : base(blockBase) { }

        public List<ColorCycle> Cycles { get; set; }
        public List<byte> Close { get; set; }

        public override string BlockType
        {
            get { return "CYCL"; }
        }

        public override void CalculateBlockSize()
        {
            base.CalculateBlockSize();

            uint block = (uint)Close.Count; //1 byte per item;

            foreach (ColorCycle colorCycle in Cycles)
            {
                if (colorCycle.Index > 0 && colorCycle.Index <= 16)
                {
                    block += 9;
                }
                else
                {
                    block += 1;
                }
            }

            BlockSize += block;
        }

        public override void LoadFromBinaryReader(Stream binaryReader)
        {
            base.LoadFromBinaryReader(binaryReader);

            Close = new List<byte>();
            Cycles = new List<ColorCycle>();

            byte byteCheck;
            do
            {
                var cycle = new ColorCycle();
                cycle.Index = binaryReader.ReadByte1();
                if (cycle.Index > 0 && cycle.Index <= 16)
                {
                    cycle.Unkown = binaryReader.ReadUint16();
                    cycle.Freq = binaryReader.ReadUint16(true);
                    cycle.Flags = binaryReader.ReadUint16(true);
                    cycle.Start = binaryReader.ReadByte1();
                    cycle.End = binaryReader.ReadByte1();
                }
                Cycles.Add(cycle);

                byteCheck = binaryReader.PeekByte();

            } while (byteCheck != 0);

            Close.Add(binaryReader.ReadByte1());

            //ScummVM documentation says that Close is one 0 byte, but on some ROOMs at Sam & Max
            //the cycles has one extra 0x00 before TRNS.
            if (binaryReader.PeekByte() == 0)
            {
                Close.Add(binaryReader.ReadByte1());
            }
        }

        public override void SaveToBinaryWriter(Stream binaryWriter)
        {
            base.SaveToBinaryWriter(binaryWriter);

            foreach (ColorCycle t in Cycles)
            {
                binaryWriter.WriteByte(t.Index);
                if (t.Index > 0 && t.Index <= 16)
                {
                    binaryWriter.Write(t.Unkown);
                    binaryWriter.Write(t.Freq, true);
                    binaryWriter.Write(t.Flags, true);
                    binaryWriter.Write(t.Start);
                    binaryWriter.Write(t.End);
                }
            }

            foreach (byte b in Close)
            {
                binaryWriter.WriteByte(b);
            }
        }
    }


    public class ColorCycle
    {
        public byte Index { get; set; }
        public ushort Unkown { get; set; }
        public ushort Freq { get; set; }
        public ushort Flags { get; set; }
        public byte Start { get; set; }
        public byte End { get; set; }
    }

}