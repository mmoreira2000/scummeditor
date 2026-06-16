using ScummEditor.Structures.DataFile;

namespace ScummEditor.Gui
{
    public partial class RoomImageHeaderControl : BlockBaseControl
    {
        private RoomImageHeader _roomImageHeader;
        public RoomImageHeaderControl()
        {
            InitializeComponent();
        }
        public override void SetAndRefreshData(Structures.BlockBase blockBase)
        {
            base.SetAndRefreshData(blockBase);

            _roomImageHeader = (RoomImageHeader) blockBase;

            ZBuffers.Text = _roomImageHeader.NumberOfZBuffers.ToString();
        }
    }
}
