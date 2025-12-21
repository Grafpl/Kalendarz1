using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Kalendarz1.KontrolaGodzin
{
    public partial class SzczegolyDniaWindow : Window
    {
        public SzczegolyDniaWindow(string pracownik, string dzial, DateTime data, List<RejestracjaModel> rejestracje)
        {
            InitializeComponent();
            
            // Nagłówek
            txtPracownik.Text = pracownik;
            txtDzial.Text = dzial;
            txtData.Text = $"{data:dd.MM.yyyy} ({data:dddd})";
            
            // Sortuj rejestracje chronologicznie
            var posortowane = rejestracje.OrderBy(r => r.DataCzas).ToList();
            
            // Znajdź wejścia i wyjścia
            var wejscia = posortowane.Where(r => r.TypInt == 1).ToList();
            var wyjscia = posortowane.Where(r => r.TypInt == 0).ToList();
            
            var pierwszeWejscie = wejscia.FirstOrDefault()?.DataCzas;
            var ostatnieWyjscie = wyjscia.LastOrDefault()?.DataCzas;
            
            // Podsumowanie
            txtWejscie.Text = pierwszeWejscie?.ToString("HH:mm") ?? "-";
            txtWyjscie.Text = ostatnieWyjscie?.ToString("HH:mm") ?? "-";
            
            // Oblicz czas pracy
            TimeSpan czasPracy = TimeSpan.Zero;
            TimeSpan czasPrzerw = TimeSpan.Zero;
            
            if (pierwszeWejscie.HasValue && ostatnieWyjscie.HasValue && ostatnieWyjscie > pierwszeWejscie)
            {
                czasPracy = ostatnieWyjscie.Value - pierwszeWejscie.Value;
                
                // Oblicz przerwy (wyjście -> następne wejście)
                for (int i = 0; i < wyjscia.Count; i++)
                {
                    var nastepneWejscie = wejscia.FirstOrDefault(w => w.DataCzas > wyjscia[i].DataCzas);
                    if (nastepneWejscie != null)
                    {
                        var przerwa = nastepneWejscie.DataCzas - wyjscia[i].DataCzas;
                        if (przerwa.TotalMinutes > 2 && przerwa.TotalHours < 4)
                        {
                            czasPrzerw += przerwa;
                        }
                    }
                }
            }
            
            txtCzasPracy.Text = FormatTimeSpan(czasPracy);
            txtPrzerwy.Text = FormatTimeSpan(czasPrzerw);
            
            // Lista rejestracji
            var listaRej = posortowane.Select((r, index) =>
            {
                string uwagi = "";
                
                // Sprawdź czy to przerwa
                if (r.TypInt == 0 && index < posortowane.Count - 1)
                {
                    var nastepna = posortowane.Skip(index + 1).FirstOrDefault(x => x.TypInt == 1);
                    if (nastepna != null)
                    {
                        var roznica = nastepna.DataCzas - r.DataCzas;
                        if (roznica.TotalMinutes > 2 && roznica.TotalHours < 4)
                        {
                            uwagi = $"Przerwa: {FormatTimeSpan(roznica)}";
                        }
                    }
                }
                
                // Sprawdź spóźnienie
                if (r.TypInt == 1 && r == wejscia.FirstOrDefault())
                {
                    if (r.DataCzas.Hour >= 6 && r.DataCzas.Minute > 10)
                    {
                        uwagi = "⏰ Spóźnienie";
                    }
                }
                
                return new
                {
                    Godzina = r.DataCzas.ToString("HH:mm:ss"),
                    Typ = r.Typ,
                    PunktDostepu = r.PunktDostepu,
                    Urzadzenie = r.Urzadzenie,
                    Uwagi = uwagi
                };
            }).ToList();
            
            gridRejestracje.ItemsSource = listaRej;
            txtPodsumowanie.Text = $"Łącznie: {rejestracje.Count} rejestracji  •  Wejścia: {wejscia.Count}  •  Wyjścia: {wyjscia.Count}";
        }
        
        private string FormatTimeSpan(TimeSpan ts)
        {
            if (ts.TotalHours < 0) return "-";
            return $"{(int)ts.TotalHours}:{ts.Minutes:D2}";
        }
        
        private void BtnZamknij_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
