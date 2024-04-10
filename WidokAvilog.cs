using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Kalendarz1
{
    public partial class WidokAvilog : Form
    {
        public WidokAvilog()
        {
            InitializeComponent();
            // Tablica zawierająca wszystkie kontrolki
            DateTimePicker[] dateTimePickers = { dateTimePicker1, dateTimePicker2, dateTimePicker3, dateTimePicker4, dateTimePicker5, dateTimePicker6, dateTimePicker7, dateTimePicker8 };
            // Pętla ustawiająca właściwości dla każdej kontrolki w tablicy
            foreach (DateTimePicker dateTimePicker in dateTimePickers)
            {
                dateTimePicker.Format = DateTimePickerFormat.Custom;
                dateTimePicker.CustomFormat = "HH:mm";
                dateTimePicker.ShowUpDown = true;
                dateTimePicker.Value = DateTime.Today.Date;
            }

        }





        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            // Pobierz wybrany czas z DateTimePicker
            DateTime selectedTime = dateTimePicker1.Value;

            // Wyświetl wybrany czas w kontrolce TextBox
            textBox1.Text = selectedTime.ToString("HH:mm");
        }

        private void WidokAvilog_Load(object sender, EventArgs e)
        {

        }
    }
}
