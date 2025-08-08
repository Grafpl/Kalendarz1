using System;
using System.Windows.Forms;


namespace Kalendarz1
{
    public partial class MenuSprzedaz : Form
    {
        public MenuSprzedaz()
        {
            InitializeComponent();
            this.StartPosition = FormStartPosition.CenterScreen;
        }
        private void kalendarzButton_Click(object sender, EventArgs e)
        {
            Platnosci platnosci = new Platnosci();
            platnosci.Show();
        }
        private void terminyButton_Click(object sender, EventArgs e)
        {
            WidokKalendarza widokKalendarza = new WidokKalendarza();
            widokKalendarza.UserID = App.UserID;
            widokKalendarza.WindowState = FormWindowState.Maximized;
            widokKalendarza.Show();
        }
        private void MENU_Load(object sender, EventArgs e)
        {

        }
        private void buttonWstawienia_Click(object sender, EventArgs e)
        {
            WidokWstawienia widokWstawienia = new WidokWstawienia();
            widokWstawienia.Show();
        }
        private void buttonKontrahenci_Click(object sender, EventArgs e)
        {
            WidokKontrahenci widokKontrahenci = new WidokKontrahenci();
            widokKontrahenci.Show();
        }
        private void mrozniaButton_Click(object sender, EventArgs e)
        {
            Mroznia mroznia = new Mroznia();
            mroznia.Show();
        }
        private void button1_Click(object sender, EventArgs e)
        {
            WidokSpecyfikacje widokSpecyfikacje = new WidokSpecyfikacje();
            widokSpecyfikacje.Show();
        }
        private void sprzedazZakupButton_Click(object sender, EventArgs e)
        {
            WidokSprzeZakup widokSprzeZakup = new WidokSprzeZakup();
            widokSprzeZakup.Show();
        }
        private void button2_Click(object sender, EventArgs e)
        {
            WidokMatryca Widokmatryca = new WidokMatryca();
            Widokmatryca.Show();
        }
        private void button3_Click(object sender, EventArgs e)
        {
        }
        private void krojenieButton_Click(object sender, EventArgs e)
        {
            PokazKrojenieMrozenie platPokazKrojenieMrozenienosci = new PokazKrojenieMrozenie();
            platPokazKrojenieMrozenienosci.WindowState = FormWindowState.Maximized;
            platPokazKrojenieMrozenienosci.Show();
        }
        private void button4_Click(object sender, EventArgs e)
        {
            SprawdzalkaUmow SprawdzalkaUmow = new SprawdzalkaUmow();
            SprawdzalkaUmow.Show();
        }
        private void odbiorcaButton_Click(object sender, EventArgs e)
        {
            WidokZamowieniaPodsumowanie widokZamowieniaPodsumowanie = new WidokZamowieniaPodsumowanie();
            widokZamowieniaPodsumowanie.UserID = App.UserID;
            widokZamowieniaPodsumowanie.Show();
        }

        private void buttonPojemnikiZestawienie_Click(object sender, EventArgs e)
        {
            WidokPojemnikiZestawienie widokPojemnikiZestawienie = new WidokPojemnikiZestawienie();
            widokPojemnikiZestawienie.Show();
        }

        private void UzgodnienieSaldButton_Click(object sender, EventArgs e)
        {
            WidokPojemniki widokPojemniki = new WidokPojemniki();
            widokPojemniki.Show();
        }

        private void mrozniaButton_Click_1(object sender, EventArgs e)
        {
            Mroznia mroznia = new Mroznia();
            mroznia.Show();
        }

        private void krojenieButton_Click_1(object sender, EventArgs e)
        {

            PokazKrojenieMrozenie platPokazKrojenieMrozenienosci = new PokazKrojenieMrozenie();
            platPokazKrojenieMrozenienosci.WindowState = FormWindowState.Maximized;
            platPokazKrojenieMrozenienosci.Show();

        }

        private void CRM_Click(object sender, EventArgs e)
        {
            CRM crm = new CRM();
            crm.UserID = App.UserID;
            crm.Show();
        }

        private void buttonFakturySprzedazy_Click(object sender, EventArgs e)
        {
            WidokFakturSprzedazy widokFakturSprzedazy = new WidokFakturSprzedazy();
            widokFakturSprzedazy.UserID = App.UserID;
            widokFakturSprzedazy.Show();
        }

        private void label8_Click(object sender, EventArgs e)
        {

        }
    }
}
