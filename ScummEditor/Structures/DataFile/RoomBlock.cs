using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ScummEditor.Structures.DataFile
{
    //ROOM - Room Block
    //  RMHD - Room Header
    //  CYCL - Color Cycle (vlc stop on 0x00) (vlc = Variable Length Code)
    //  TRNS - Transparent Color
    //  PALS - Palette Data
    //  RMIM - Room Image
    //  OBIM - *Object Images
    //  OBCD - *Object Scripts
    //  EXCD - Exit Script (script run when leaving the room)
    //  ENCD - Entry Script (script run when entering the room)
    //  NLSC - Number of Local Scripts
    //  LSCR - Local Script * NLSC
    //  BOXD - Box Data (box 0 seems to be crap with all the points set to -32000,-32000)
    //  BOXM - Box Matrix
    //  SCAL - Box Scale
    public class RoomBlock : BlockBase
    {
        public RoomBlock(BlockBase blockBase) : base(blockBase) { }

        public override string BlockType
        {
            get { return "ROOM"; }
        }

        public override void LoadFromBinaryReader(Stream binaryReader)
        {
            base.LoadFromBinaryReader(binaryReader);

            switch (_gameInfo.ScummVersion)
            {
                case 5:
                    LoadScummV5(binaryReader);
                    break;
                case 6:
                    LoadScummV6(binaryReader);
                    break;
            }
        }

        private void LoadScummV5(Stream binaryReader)
        {
            //Leitura Simplificada dos Blocos, le todos e ignora os desconhecidos. Assim a leitura fica mais generica.
            //Isso vai possibilitar ler arquivos como o primeiro monkey island com full speech, que teve sua estrutura não padronizada.

            //Adicionado CDHD e VERB e OBNA, nenhum jogo testado por mim antes tinha esse bloco - apareceu apenas no Monkey Island 2 - Talkie.
            //Esses tres verbos deveriam ficar sempre dentro do OBCD...
            var blocosPossiveis = new [] { "RMHD", "CYCL", "TRNS", "EPAL", "BOXD", "BOXM", 
                                                 "CLUT", "SCAL", "RMIM", "OBIM", "OBCD", "EXCD",
                                                 "ENCD", "NLSC", "LSCR", "CDHD", "VERB", "OBNA"};

            string typeRead = BinaryHelper.ConvertByteArrayToUTF8String(binaryReader.PeekBytes(4));
            while (blocosPossiveis.Any(x => x.Equals(typeRead)))
            {
                switch (typeRead)
                {
                    case "RMHD":
                        var RMHD = new RoomHeader(this);
                        RMHD.LoadFromBinaryReader(binaryReader);
                        Childrens.Add(RMHD);
                        break;

                    case "CYCL":
                        var CYCL = new ColorCycles(this);
                        CYCL.LoadFromBinaryReader(binaryReader);
                        Childrens.Add(CYCL);
                        break;

                    case "TRNS":
                        var TRNS = new ValuePaddingBlock(this, "TRNS");
                        TRNS.LoadFromBinaryReader(binaryReader);
                        Childrens.Add(TRNS);
                        break;

                    case "CLUT":
                        var CLUT = new PaletteData(this, "CLUT");
                        CLUT.LoadFromBinaryReader(binaryReader);
                        Childrens.Add(CLUT);
                        break;

                    case "RMIM":
                        var RMIM = new RoomImage(this, this);
                        RMIM.LoadFromBinaryReader(binaryReader);
                        Childrens.Add(RMIM);
                        break;

                    case "OBIM":
                        var OBIM = new ObjectImage(this);
                        OBIM.LoadFromBinaryReader(binaryReader);
                        Childrens.Add(OBIM);
                        break;

                    case "NLSC":
                        var NLSC = new ValuePaddingBlock(this, "NLSC");
                        NLSC.LoadFromBinaryReader(binaryReader);
                        Childrens.Add(NLSC);
                        break;

                    default:
                        var Default = new NotImplementedDataBlock(this, typeRead);
                        Default.LoadFromBinaryReader(binaryReader);
                        Childrens.Add(Default);
                        break;
                }

                typeRead = BinaryHelper.ConvertByteArrayToUTF8String(binaryReader.PeekBytes(4));
            }


            /*
            var RMHD = new RoomHeader(this);
            RMHD.LoadFromBinaryReader(binaryReader);
            Childrens.Add(RMHD);

            var CYCL = new ColorCycles(this);
            CYCL.LoadFromBinaryReader(binaryReader);
            Childrens.Add(CYCL);

            var TRNS = new ValuePaddingBlock(this, "TRNS");
            TRNS.LoadFromBinaryReader(binaryReader);
            Childrens.Add(TRNS);

            var EPAL = new NotImplementedDataBlock(this, "EPAL");
            EPAL.LoadFromBinaryReader(binaryReader);
            Childrens.Add(EPAL);

            var BOXD = new NotImplementedDataBlock(this, "BOXD");
            BOXD.LoadFromBinaryReader(binaryReader);
            Childrens.Add(BOXD);

            var BOXM = new NotImplementedDataBlock(this, "BOXM");
            BOXM.LoadFromBinaryReader(binaryReader);
            Childrens.Add(BOXM);

            var CLUT = new PaletteData(this, "CLUT");
            CLUT.LoadFromBinaryReader(binaryReader);
            Childrens.Add(CLUT);

            var SCAL = new NotImplementedDataBlock(this, "SCAL");
            SCAL.LoadFromBinaryReader(binaryReader);
            Childrens.Add(SCAL);

            var RMIM = new RoomImage(this, this);
            RMIM.LoadFromBinaryReader(binaryReader);
            Childrens.Add(RMIM);

            while (BinaryHelper.ConvertByteArrayToUTF8String(binaryReader.PeekBytes(4)) == "OBIM")
            {
                var OBIM = new ObjectImage(this);
                OBIM.LoadFromBinaryReader(binaryReader);
                Childrens.Add(OBIM);
            }

            while (BinaryHelper.ConvertByteArrayToUTF8String(binaryReader.PeekBytes(4)) == "OBCD")
            {
                var OBCD = new NotImplementedDataBlock(this, "OBCD");
                OBCD.LoadFromBinaryReader(binaryReader);
                Childrens.Add(OBCD);
            }

            var EXCD = new NotImplementedDataBlock(this, "EXCD");
            EXCD.LoadFromBinaryReader(binaryReader);
            Childrens.Add(EXCD);

            var ENCD = new NotImplementedDataBlock(this, "ENCD");
            ENCD.LoadFromBinaryReader(binaryReader);
            Childrens.Add(ENCD);

            var NLSC = new ValuePaddingBlock(this, "NLSC");
            NLSC.LoadFromBinaryReader(binaryReader);
            Childrens.Add(NLSC);

            for (int i = 0; i < NLSC.Value; i++)
            {
                var LSCR = new NotImplementedDataBlock(this, "LSCR");
                LSCR.LoadFromBinaryReader(binaryReader);
                Childrens.Add(LSCR);
            }
             
             */
        }

        private void LoadScummV6(Stream binaryReader)
        {
            var RMHD = new RoomHeader(this);
            RMHD.LoadFromBinaryReader(binaryReader);
            Childrens.Add(RMHD);

            var CYCL = new ColorCycles(this);
            CYCL.LoadFromBinaryReader(binaryReader);
            Childrens.Add(CYCL);

            var TRNS = new ValuePaddingBlock(this, "TRNS");
            TRNS.LoadFromBinaryReader(binaryReader);
            Childrens.Add(TRNS);

            var PALS = new PalettesData(this);
            PALS.LoadFromBinaryReader(binaryReader);
            Childrens.Add(PALS);

            var RMIM = new RoomImage(this, this);
            RMIM.LoadFromBinaryReader(binaryReader);
            Childrens.Add(RMIM);

            while (BinaryHelper.ConvertByteArrayToUTF8String(binaryReader.PeekBytes(4)) == "OBIM")
            {
                var OBIM = new ObjectImage(this);
                OBIM.LoadFromBinaryReader(binaryReader);
                Childrens.Add(OBIM);
            }

            while (BinaryHelper.ConvertByteArrayToUTF8String(binaryReader.PeekBytes(4)) == "OBCD")
            {
                var OBCD = new NotImplementedDataBlock(this, "OBCD");
                OBCD.LoadFromBinaryReader(binaryReader);
                Childrens.Add(OBCD);
            }

            var EXCD = new NotImplementedDataBlock(this, "EXCD");
            EXCD.LoadFromBinaryReader(binaryReader);
            Childrens.Add(EXCD);

            var ENCD = new NotImplementedDataBlock(this, "ENCD");
            ENCD.LoadFromBinaryReader(binaryReader);
            Childrens.Add(ENCD);

            var NLSC = new ValuePaddingBlock(this, "NLSC");
            NLSC.LoadFromBinaryReader(binaryReader);
            Childrens.Add(NLSC);

            for (int i = 0; i < NLSC.Value; i++)
            {
                var LSCR = new NotImplementedDataBlock(this, "LSCR");
                LSCR.LoadFromBinaryReader(binaryReader);
                Childrens.Add(LSCR);
            }

            var BOXD = new NotImplementedDataBlock(this, "BOXD");
            BOXD.LoadFromBinaryReader(binaryReader);
            Childrens.Add(BOXD);

            var BOXM = new NotImplementedDataBlock(this, "BOXM");
            BOXM.LoadFromBinaryReader(binaryReader);
            Childrens.Add(BOXM);

            var SCAL = new NotImplementedDataBlock(this, "SCAL");
            SCAL.LoadFromBinaryReader(binaryReader);
            Childrens.Add(SCAL);
        }

        public override void SaveToBinaryWriter(Stream binaryWriter)
        {
            base.SaveToBinaryWriter(binaryWriter);

            foreach (BlockBase blockBase in Childrens)
            {
                blockBase.SaveToBinaryWriter(binaryWriter);
            }
        }

        public PaletteData GetDefaultPalette()
        {
            if (_gameInfo.ScummVersion == 5)
            {
                return (PaletteData)Childrens.Single(p => p.BlockType == "CLUT");
            }
            else
            {
                return GetPALS().GetWRAP().GetAPALs()[0];
            }
        }
        public PalettesData GetPALS()
        {
            return (PalettesData)Childrens.SingleOrDefault(x => x.GetType() == typeof(PalettesData));
        }

        public List<ObjectImage> GetOBIMs()
        {
            return Childrens.OfType<ObjectImage>().ToList();
        }

        public RoomHeader GetRMHD()
        {
            return (RoomHeader)Childrens.Single(x => x.GetType() == typeof(RoomHeader));
        }

        public RoomImage GetRMIM()
        {
            return (RoomImage)Childrens.Single(x => x.GetType() == typeof(RoomImage));
        }

        public ValuePaddingBlock GetTRNS()
        {
            return (ValuePaddingBlock)Childrens.Single(x => x.BlockType == "TRNS");
        }
    }
}