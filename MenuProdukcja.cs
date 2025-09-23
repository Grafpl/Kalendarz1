using System;
using System.Windows.Forms;


namespace Kalendarz1
{
    public partial class MenuProdukcja : Form
    {
        public MenuProdukcja()
        {
            InitializeComponent();
            this.StartPosition = FormStartPosition.CenterScreen;
        }
        private void MENU_Load(object sender, EventArgs e)
        {

        }

        private void button5_Click(object sender, EventArgs e)
        {
            new WidokPanelProdukcjaNowy { UserID = App.UserID }.Show();
        }
    }
}
