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
    public partial class WidokZamowienia : Form
    {
        private string GID;
        private string lpDostawa;
        private DateTime dzienUbojowy;
        public string UserID { get; set; }
        static string connectionPermission = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private MojeObliczenia obliczenia = new MojeObliczenia();
        private NazwaZiD nazwaZiD = new NazwaZiD();
        private RozwijanieComboBox RozwijanieComboBox = new RozwijanieComboBox();
        public WidokZamowienia()
        {
            InitializeComponent();
            RozwijanieComboBox.RozwijanieOdbiorcowSymfonia(comboBoxOdbiorca);
        }

        private void comboBoxOdbiorca_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBoxOdbiorca.SelectedItem is KeyValuePair<string, string> selectedItem)
            {
                string selectedKod = selectedItem.Value; // Pobiera kod
                string selectedId = selectedItem.Key;   // Pobiera id

                // Przypisz ID do TextBox
                textBoxIdOdbiorca.Text = selectedId;

                // Przypisz Nazwe do TextBox
                textBoxNazwaOdbiorca.Text = selectedKod;
            }
        }

    }
}
