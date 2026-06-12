using System.Windows.Forms;
using ScummEditor.Structures;
using ScummEditor.Structures.IndexFile;

namespace ScummEditor.Gui
{
    /// <summary>
    /// Read-only viewer for the decoded index-file blocks that are not resource directories:
    /// MAXS (maximum values), DOBJ (object owners/states/classes), AARY (array definitions)
    /// and RNAM (room names - only populated on SCUMM v5 games).
    /// </summary>
    public partial class IndexDetailsControl : BlockBaseControl
    {
        public IndexDetailsControl()
        {
            InitializeComponent();
        }

        public override void SetAndRefreshData(BlockBase blockBase)
        {
            base.SetAndRefreshData(blockBase);

            grid.Columns.Clear();
            grid.Rows.Clear();

            if (blockBase is MaximumValues)
            {
                ShowMaximumValues((MaximumValues)blockBase);
            }
            else if (blockBase is DirectoryOfObjects)
            {
                ShowDirectoryOfObjects((DirectoryOfObjects)blockBase);
            }
            else if (blockBase is DirectoryOfArrays)
            {
                ShowDirectoryOfArrays((DirectoryOfArrays)blockBase);
            }
            else if (blockBase is RoomNamesV6)
            {
                ShowRoomNames((RoomNamesV6)blockBase);
            }
            else
            {
                summary.Text = string.Empty;
            }
        }

        private void AddColumns(params string[] headers)
        {
            foreach (string header in headers)
            {
                var column = new DataGridViewTextBoxColumn
                {
                    HeaderText = header,
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
                    SortMode = DataGridViewColumnSortMode.NotSortable,
                    ReadOnly = true
                };
                grid.Columns.Add(column);
            }
        }

        private void ShowMaximumValues(MaximumValues block)
        {
            summary.Text = "Engine limits declared by the game";
            AddColumns("Field", "Value");

            grid.Rows.Add("Variables", block.Variables);
            grid.Rows.Add("Bit variables", block.BitVariables);
            grid.Rows.Add("Local objects", block.LocalObjects);
            grid.Rows.Add("Arrays", block.Arrays);
            grid.Rows.Add("Verbs", block.Verbs);
            grid.Rows.Add("Floating objects", block.FloatingObjects);
            grid.Rows.Add("Inventory objects", block.InventoryObjects);
            grid.Rows.Add("Rooms", block.Rooms);
            grid.Rows.Add("Scripts", block.Scripts);
            grid.Rows.Add("Sounds", block.Sounds);
            grid.Rows.Add("Character sets", block.CharacterSets);
            grid.Rows.Add("Costumes", block.Costumes);
            grid.Rows.Add("Global objects", block.GlobalObjects);
            grid.Rows.Add("New names (v5)", block.NewNames);
        }

        private void ShowDirectoryOfObjects(DirectoryOfObjects block)
        {
            summary.Text = string.Format("{0} object(s) - initial owner/state and class data", block.NumOfItems);
            AddColumns("Object", "Owner", "State", "Class data");

            for (int i = 0; i < block.Owners.Count; i++)
            {
                DirectoryObject item = block.Owners[i];
                grid.Rows.Add(i, item.Owner, item.State, "0x" + item.ClassData.ToString("X8"));
            }
        }

        private void ShowDirectoryOfArrays(DirectoryOfArrays block)
        {
            summary.Text = string.Format("{0} predefined array(s)", block.Items.Count);
            AddColumns("Variable", "X size", "Y size", "Type");

            foreach (DirectoryArray item in block.Items)
            {
                grid.Rows.Add(item.VariableNumber, item.XSize, item.YSize,
                    item.Type == 1 ? "1 (bytes)" : item.Type == 0 ? "0 (words)" : item.Type.ToString());
            }
        }

        private void ShowRoomNames(RoomNamesV6 block)
        {
            if (block.Rooms.Count == 0)
            {
                summary.Text = "No room names (SCUMM v6 games leave this block empty)";
                return;
            }

            summary.Text = string.Format("{0} room name(s)", block.Rooms.Count);
            AddColumns("Room", "Name");
            foreach (RoomName room in block.Rooms)
            {
                grid.Rows.Add(room.RoomNumber, room.Name);
            }
        }
    }
}
