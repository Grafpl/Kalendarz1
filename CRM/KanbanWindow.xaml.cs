using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using System.Windows;

namespace Kalendarz1.CRM
{
    public partial class KanbanWindow : Window
    {
        string connStr, opID;

        // Klasa pomocnicza tylko dla tego okna
        public class Karta { public string Nazwa { get; set; } }

        public KanbanWindow(string c, string op)
        {
            InitializeComponent();
            connStr = c;
            opID = op;
            Wczytaj();
        }

        void Wczytaj()
        {
            var doZadzw = new List<Karta>();
            var wTrakcie = new List<Karta>();
            var oferty = new List<Karta>();
            var sukces = new List<Karta>();

            try
            {
                using (var conn = new SqlConnection(connStr))
                {
                    conn.Open();
                    var cmd = new SqlCommand("SELECT Nazwa, Status FROM OdbiorcyCRM WHERE Status IS NOT NULL AND Status != ''", conn);
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            var k = new Karta { Nazwa = r["Nazwa"].ToString() };
                            string s = r["Status"].ToString();

                            if (s.Contains("Do zadzwonienia") || s.Contains("Nowy")) doZadzw.Add(k);
                            else if (s.Contains("Próba") || s.Contains("Nawiązano")) wTrakcie.Add(k);
                            else if (s.Contains("oferta")) oferty.Add(k);
                            else if (s.Contains("Zgoda")) sukces.Add(k);
                        }
                    }
                }
                // Te listy istnieją w KanbanWindow.xaml
                listDoZadzwonienia.ItemsSource = doZadzw;
                listWTrakcie.ItemsSource = wTrakcie;
                listOferty.ItemsSource = oferty;
                listSukces.ItemsSource = sukces;
            }
            catch { }
        }
    }
}