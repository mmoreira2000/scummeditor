using System.IO;

namespace ScummEditor.Structures
{
    public interface IBinaryPersistence
    {
        void LoadFromBinaryReader(Stream binaryReader);
        void SaveToBinaryWriter(Stream binaryWriter);
    }
}