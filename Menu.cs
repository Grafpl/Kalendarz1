using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Kalendarz1
{
    public partial class MENU : Form
    {
        public MENU()
        {
            InitializeComponent();
        }

        private void kalendarzButton_Click(object sender, EventArgs e)
        {
            // Tworzenie nowej instancji Form1
            Platnosci platnosci = new Platnosci();

            // Wyświetlanie Form1
            platnosci.Show();
        }

        private void terminyButton_Click(object sender, EventArgs e)
        {
            // Tworzenie nowej instancji WidokKalendarza
            WidokKalendarza widokKalendarza = new WidokKalendarza();

            // Przypisanie wartości UserID
            widokKalendarza.UserID = App.UserID;

            // Wyświetlanie formularza
            widokKalendarza.Show();
        }

        private void MENU_Load(object sender, EventArgs e)
        {

        }

        private void buttonWstawienia_Click(object sender, EventArgs e)
        {
            // Tworzenie nowej instancji Form1
            WidokWstawienia widokWstawienia = new WidokWstawienia();

            // Wyświetlanie Form1
            widokWstawienia.Show();
        }

        private void buttonKontrahenci_Click(object sender, EventArgs e)
        {
            // Tworzenie nowej instancji Form1
            WidokKontrahenci widokKontrahenci = new WidokKontrahenci();

            // Wyświetlanie Form1
            widokKontrahenci.Show();

        }

        private void mrozniaButton_Click(object sender, EventArgs e)
        {
            // Tworzenie nowej instancji Form1
            Mroznia mroznia = new Mroznia();

            // Wyświetlanie Form1
            mroznia.Show();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            // Tworzenie nowej instancji Form1
            WidokAvilog widokAvilog = new WidokAvilog();

            // Wyświetlanie Form1
            widokAvilog.Show();
        }

        private void sprzedazZakupButton_Click(object sender, EventArgs e)
        {
            // Tworzenie nowej instancji Form1
            WidokSprzeZakup widokSprzeZakup = new WidokSprzeZakup();

            // Wyświetlanie Form1
            widokSprzeZakup.Show();
        }
    }
}
