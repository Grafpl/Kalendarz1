using System;
using System.Windows.Forms;


namespace Kalendarz1
{
    public partial class MenuTransport : Form
    {
        public MenuTransport()
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

        private void button6_Click(object sender, EventArgs e)
        {
            try
            {
                // Uruchom moduł transportu z raportem
                var connString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
                var connTransport = "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";

                var repo = new Kalendarz1.Transport.Repozytorium.TransportRepozytorium(connTransport, connString);
                var frm = new Kalendarz1.Transport.Formularze.TransportMainFormImproved(repo, App.UserID);
                frm.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas uruchamiania modułu transportu: {ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void odbiorcaButton_Click(object sender, EventArgs e)
        {
            WidokZamowieniaPodsumowanie widokZamowieniaPodsumowanie = new WidokZamowieniaPodsumowanie();
            widokZamowieniaPodsumowanie.UserID = App.UserID;
            widokZamowieniaPodsumowanie.Show();
        }
    }
}
