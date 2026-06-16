using ScummEditor.Encoders;
using ScummEditor.Structures.DataFile;

namespace ScummEditor.Gui
{
    public struct ClassCreateInfo
    {
        public string ControlId { get; set; }
        public Costume Costume { get; set; }
        public ImageType ImageType { get; set; }
        public int ImageIndex { get; set; }
        public int ObjectIndex { get; set; }
        public int ZPlaneIndex { get; set; }
    }
}
