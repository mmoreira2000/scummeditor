using System;
using System.IO;

namespace ScummEditor.Structures.IndexFile
{
    public class MaximumValues : BlockBase
    {
        public MaximumValues(BlockBase blockBase, GameInfo gameInfo) : base(blockBase, gameInfo) { }

        /*
        MAXS
        ----
        SCUMM VERSION 5
        Block Name	   (4 bytes)
        Block Size	   (4 bytes BE)
        Variables	   (2 bytes)
        Unknown	   (2 bytes)
        Bit Variables	   (2 bytes)
        Local Objects	   (2 bytes)
        New Names?	   (2 bytes)
        Character Sets	   (2 bytes)
        Verbs?		   (2 bytes)
        Array?		   (2 bytes)
        Inventory Objects (2 bytes)
        
        SCUMM VERSION 6
        Block Name	  (4 bytes)
        Block Size	  (4 bytes BE)
        Variables	  (2 bytes)
        Unknown		  (2 bytes)
        Bit Variables	  (2 bytes)
        Local Objects	  (2 bytes)
        Arrays		  (2 bytes)
        Unknown		  (2 bytes)
        Verbs		  (2 bytes)
        Floating Objects  (2 bytes)
        Inventory Objects (2 bytes)
        Rooms		  (2 bytes)
        Scripts		  (2 bytes)
        Sounds  	  (2 bytes)
        Character Sets	  (2 bytes)
        Costumes	  (2 bytes)
        Global Objects	  (2 bytes)
        */

        public ushort Variables { get; set; }
        public ushort Unknown0 { get; set; }
        public ushort BitVariables { get; set; }
        public ushort LocalObjects { get; set; }
        public ushort Arrays { get; set; }
        public ushort Unknown1 { get; set; }
        public ushort Verbs { get; set; }
        public ushort FloatingObjects { get; set; }
        public ushort InventoryObjects { get; set; }
        public ushort Rooms { get; set; }
        public ushort Scripts { get; set; }
        public ushort Sounds { get; set; }
        public ushort CharacterSets { get; set; }
        public ushort Costumes { get; set; }
        public ushort GlobalObjects { get; set; }
        public ushort NewNames { get; set; }

        public override string BlockType
        {
            get { return "MAXS"; }
        }

        public override void LoadFromBinaryReader(Stream binaryReader)
        {
            base.LoadFromBinaryReader(binaryReader);

            if (_gameInfo.ScummVersion == 5)
            {
                LoadScummV5(binaryReader);
            }
            else if (_gameInfo.ScummVersion == 6)
            {
                LoadScummV6(binaryReader);
            }
        }

        private void LoadScummV5(Stream binaryReader)
        {
            Variables = binaryReader.ReadUint16();
            Unknown0 = binaryReader.ReadUint16();
            BitVariables = binaryReader.ReadUint16();
            LocalObjects = binaryReader.ReadUint16();
            NewNames = binaryReader.ReadUint16();
            CharacterSets = binaryReader.ReadUint16();
            Verbs = binaryReader.ReadUint16();
            Arrays = binaryReader.ReadUint16();
            InventoryObjects = binaryReader.ReadUint16();
        }


        private void LoadScummV6(Stream binaryReader)
        {
            Variables = binaryReader.ReadUint16();
            Unknown0 = binaryReader.ReadUint16();
            BitVariables = binaryReader.ReadUint16();
            LocalObjects = binaryReader.ReadUint16();
            Arrays = binaryReader.ReadUint16();
            Unknown1 = binaryReader.ReadUint16();
            Verbs = binaryReader.ReadUint16();
            FloatingObjects = binaryReader.ReadUint16();
            InventoryObjects = binaryReader.ReadUint16();
            Rooms = binaryReader.ReadUint16();
            Scripts = binaryReader.ReadUint16();
            Sounds = binaryReader.ReadUint16();
            CharacterSets = binaryReader.ReadUint16();
            Costumes = binaryReader.ReadUint16();
            GlobalObjects = binaryReader.ReadUint16();
        }

        public override void SaveToBinaryWriter(Stream binaryWriter)
        {
            base.SaveToBinaryWriter(binaryWriter);

            if (_gameInfo.ScummVersion == 5)
            {
                SaveScummV5(binaryWriter);
            }
            else if (_gameInfo.ScummVersion == 6)
            {
                SaveScummV6(binaryWriter);
            }

        }

        private void SaveScummV5(Stream binaryWriter)
        {
            binaryWriter.Write(Variables);
            binaryWriter.Write(Unknown0);
            binaryWriter.Write(BitVariables);
            binaryWriter.Write(LocalObjects);
            binaryWriter.Write(NewNames);
            binaryWriter.Write(CharacterSets);
            binaryWriter.Write(Verbs);
            binaryWriter.Write(Arrays);
            binaryWriter.Write(InventoryObjects);
        }

        private void SaveScummV6(Stream binaryWriter)
        {
            binaryWriter.Write(Variables);
            binaryWriter.Write(Unknown0);
            binaryWriter.Write(BitVariables);
            binaryWriter.Write(LocalObjects);
            binaryWriter.Write(Arrays);
            binaryWriter.Write(Unknown1);
            binaryWriter.Write(Verbs);
            binaryWriter.Write(FloatingObjects);
            binaryWriter.Write(InventoryObjects);
            binaryWriter.Write(Rooms);
            binaryWriter.Write(Scripts);
            binaryWriter.Write(Sounds);
            binaryWriter.Write(CharacterSets);
            binaryWriter.Write(Costumes);
            binaryWriter.Write(GlobalObjects);
        }
    }
}