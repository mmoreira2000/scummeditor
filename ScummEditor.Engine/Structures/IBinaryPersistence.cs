using System.IO;

namespace ScummEditor.Engine.Structures
{
    public interface IBinaryPersistence
    {
        void LoadFromBinaryReader(Stream binaryReader);
        void SaveToBinaryWriter(Stream binaryWriter);
    }
}