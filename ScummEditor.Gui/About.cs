using System;
using System.Windows.Forms;
using ScummEditor.Properties;

namespace ScummEditor
{
    public partial class About : Form
    {
        public About()
        {
            InitializeComponent();
            ScummEditorTitle.Text = string.Format(ScummEditorTitle.Text, Application.ProductVersion);

            History.Text = Resources.History;
        }

        private void OK_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void ShowHistory_Click(object sender, EventArgs e)
        {
            if (TextAbout.Visible)
            {
                History.Show();
                TextAbout.Hide();
                ShowHistory.Text = "&Hide History";
            }
            else
            {
                History.Hide();
                TextAbout.Show();
                ShowHistory.Text = "&Show History";
            }
        }
    }
}
