using Kalendarz1.Transport;
using Kalendarz1.Transport.Formularze;
using Kalendarz1.Transport.Repozytorium;
using System;
using System.Windows.Forms;
namespace Kalendarz1

{
    public partial class MENU : Form
    {
        public MENU()
        {
            InitializeComponent();
            StartPosition = FormStartPosition.CenterScreen;
        }

        private void kalendarzButton_Click(object sender, EventArgs e)
        {
            var platnosci = new Platnosci();
            platnosci.Show();
        }

        private void terminyButton_Click(object sender, EventArgs e)
        {
            var widokKalendarza = new WidokKalendarza
            {
                UserID = App.UserID,
                WindowState = FormWindowState.Maximized
            };
            widokKalendarza.Show();
        }

        private void MENU_Load(object sender, EventArgs e) { }

        private void buttonWstawienia_Click(object sender, EventArgs e)
        {
            new WidokWstawienia().Show();
        }

        private void buttonKontrahenci_Click(object sender, EventArgs e)
        {
            new WidokKontrahenci().Show();
        }

        private void mrozniaButton_Click(object sender, EventArgs e)
        {
            new Mroznia().Show();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            new WidokSpecyfikacje().Show();
        }

        private void sprzedazZakupButton_Click(object sender, EventArgs e)
        {
            new WidokSprzeZakup().Show();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            new WidokMatryca().Show();
        }

        private void button3_Click(object sender, EventArgs e) { }

        private void krojenieButton_Click(object sender, EventArgs e)
        {
            var pk = new PokazKrojenieMrozenie { WindowState = FormWindowState.Maximized };
            pk.Show();
        }

        private void button4_Click(object sender, EventArgs e)
        {
            new SprawdzalkaUmow { UserID = App.UserID }.Show();
        }

        private void odbiorcaButton_Click(object sender, EventArgs e)
        {
            WidokZamowieniaPodsumowanie widokZamowieniaPodsumowanie = new WidokZamowieniaPodsumowanie();
            widokZamowieniaPodsumowanie.UserID = App.UserID;
            widokZamowieniaPodsumowanie.Show();
        }

        private void UzgodnienieSaldButton_Click(object sender, EventArgs e)
        {
            new WidokPojemniki().Show();
        }

        private void buttonPojemnikiZestawienie_Click(object sender, EventArgs e)
        {
            new WidokPojemnikiZestawienie().Show();
        }

        private void CRM_Click(object sender, EventArgs e)
        {
            new CRM { UserID = App.UserID }.Show();
        }

        private void buttonFakturySprzedazy_Click(object sender, EventArgs e)
        {
            new WidokFakturSprzedazy { UserID = App.UserID }.Show();
        }

        private void buttonAkceptacja_Click(object sender, EventArgs e)
        {
            var connString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
            var appUser = string.IsNullOrWhiteSpace(App.UserID) ? Environment.UserName : App.UserID;
            using var f = new AdminChangeRequestsForm(connString, appUser);
            f.ShowDialog(this);
        }

        private void button5_Click(object sender, EventArgs e)
        {
            new WidokPanelProdukcja { UserID = App.UserID }.Show();
        }

        private void button6_Click(object sender, EventArgs e)
        {
            try
            {
                // Uruchom moduł transportu
                var connString = "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";
                var libraConnString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

                var repo = new TransportRepozytorium(connString, libraConnString);
                var frm = new TransportMainFormImproved(repo, App.UserID);
                frm.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas uruchamiania modułu transportu:\n{ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
