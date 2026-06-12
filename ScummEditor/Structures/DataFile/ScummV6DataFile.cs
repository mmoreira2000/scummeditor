using System.Collections.Generic;
using System.Linq;

namespace ScummEditor.Structures.DataFile
{
    //LECF - Main Container
    //  LOFF     - Room offsets table
    //  LFLF * n - Game Rooms.
    public class ScummV6DataFile : BlockBase
    {
        public ScummV6DataFile(BlockBase blockBase, GameInfo loadedGameInfo) : base(blockBase, loadedGameInfo) { }

        public override string BlockType
        {
            get { return "LECF"; }
        }

        #region Save & Load

        public override void LoadFromBinaryReader(System.IO.Stream binaryReader)
        {
            base.LoadFromBinaryReader(binaryReader);

            var LOFF = new RoomOffsetTable(this);
            LOFF.LoadFromBinaryReader(binaryReader);
            Childrens.Add(LOFF);

            while (binaryReader.Position < binaryReader.Length)
            {
                // Anything that is not an LFLF (stray blocks in fan editions) is kept as a
                // byte-preserved generic block so the file still loads and round-trips.
                string typeRead = BinaryHelper.ConvertByteArrayToUTF8String(binaryReader.PeekBytes(4));
                if (typeRead == "LFLF")
                {
                    var LFLF = new DiskBlock(this);
                    LFLF.LoadFromBinaryReader(binaryReader);
                    Childrens.Add(LFLF);
                }
                else
                {
                    var stray = new NotImplementedDataBlock(this, typeRead);
                    stray.LoadFromBinaryReader(binaryReader);
                    Childrens.Add(stray);
                }
            }
        }

        public override void SaveToBinaryWriter(System.IO.Stream binaryWriter)
        {
            base.SaveToBinaryWriter(binaryWriter);

            foreach (var diskBlock in Childrens)
            {
                diskBlock.SaveToBinaryWriter(binaryWriter);
            }

            binaryWriter.Flush();
        }

        public RoomOffsetTable GetLOFF()
        {
            return (RoomOffsetTable)Childrens.First(x => x.GetType() == typeof(RoomOffsetTable));
        }

        public List<DiskBlock> GetLFLFs()
        {
            return Childrens.OfType<DiskBlock>().ToList();
        }

        #endregion
    }



}
