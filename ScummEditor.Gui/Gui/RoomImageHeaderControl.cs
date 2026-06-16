using ScummEditor.Engine.Structures.DataFile;

using ScummEditor.Engine.Structures;
namespace ScummEditor.Gui
{
    public partial class RoomImageHeaderControl : BlockBaseControl
    {
        private RoomImageHeader _roomImageHeader;
        public RoomImageHeaderControl()
        {
            InitializeComponent();
        }
        public override void SetAndRefreshData(BlockBase blockBase)
        {
            base.SetAndRefreshData(blockBase);

            _roomImageHeader = (RoomImageHeader) blockBase;

            ZBuffers.Text = _roomImageHeader.NumberOfZBuffers.ToString();
        }
    }
}
