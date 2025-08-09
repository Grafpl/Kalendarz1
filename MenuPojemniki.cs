using System;
using System.Windows.Forms;


namespace Kalendarz1
{
    public partial class MenuPojemniki : Form
    {
        public MenuPojemniki()
        {
            InitializeComponent();
            this.StartPosition = FormStartPosition.CenterScreen;
        }
        private void MENU_Load(object sender, EventArgs e)
        {

        }

        private void buttonPojemnikiZestawienie_Click(object sender, EventArgs e)
        {
            WidokPojemnikiZestawienie widokPojemnikiZestawienie = new WidokPojemnikiZestawienie();
            widokPojemnikiZestawienie.Show();

        }

        private void UzgodnienieSaldButton_Click_1(object sender, EventArgs e)
        {
            WidokPojemniki widokPojemniki = new WidokPojemniki();
            widokPojemniki.Show();
        }
    }
}
