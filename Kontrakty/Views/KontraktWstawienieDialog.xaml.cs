using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Kalendarz1.Kontrakty.Models;

namespace Kalendarz1.Kontrakty.Views
{
    /// <summary>
    /// Dialog dodawania/edycji wstawienia: data + sztuki → daty ubiórki/uboju liczone automatycznie.
    /// W trybie NOWE: pole „Powtórz co N dni przez M wstawień" generuje wiele cykli za jednym razem.
    /// </summary>
    public partial class KontraktWstawienieDialog : Window
    {
        private static readonly CultureInfo Pl = new("pl-PL");
        private const int UbiorKaDni = 33;
        private const int PelnyUbojDni = 42;

        private readonly int _nrPoczatkowy;
        private readonly bool _edycja;
        private bool _ready;

        /// <summary>Wynik: lista cykli (zawsze ≥1). W edycji zawsze 1.</summary>
        public List<HarmonogramCykl> Wynik { get; private set; } = new();

        public KontraktWstawienieDialog(HarmonogramCykl? edytowany, int nrPoczatkowy, DateTime? domyslnaData)
        {
            InitializeComponent();
            _nrPoczatkowy = edytowany?.NrCyklu ?? nrPoczatkowy;
            _edycja = edytowany != null;
            txtTytul.Text = _edycja ? $"✏ Edycja — Cykl {_nrPoczatkowy}" : "🐔 Nowe wstawienie";

            if (_edycja)
            {
                dpWst.SelectedDate = edytowany!.DataWstawienia;
                txtSzt.Text = edytowany.IloscWstawiona?.ToString() ?? "";
                panelPowtorz.Visibility = Visibility.Collapsed;   // edycja = pojedynczy cykl
            }
            else
            {
                dpWst.SelectedDate = domyslnaData ?? DateTime.Today;
            }

            Loaded += (_, _) => { _ready = true; Przelicz(); };
        }

        private void Pole_Changed(object sender, RoutedEventArgs e) { if (_ready) Przelicz(); }

        private void Przelicz()
        {
            if (dpWst.SelectedDate is { } wst)
            {
                txtAutoUbiorka.Text = wst.AddDays(UbiorKaDni).ToString("dd.MM.yyyy");
                txtAutoUboj.Text    = wst.AddDays(PelnyUbojDni).ToString("dd.MM.yyyy");
            }
            else { txtAutoUbiorka.Text = "—"; txtAutoUboj.Text = "—"; }

            if (!_edycja)
            {
                int n = ParseInt(txtCoIle.Text) ?? 0;
                int ile = ParseInt(txtIle.Text) ?? 1;
                if (ile <= 1) txtPodpowiedzPowtorz.Text = "Tylko to jedno wstawienie.";
                else if (n <= 0) txtPodpowiedzPowtorz.Text = "Podaj 'co ile dni', by powtórzyć.";
                else if (dpWst.SelectedDate is { } w2)
                {
                    var ostatnia = w2.AddDays((ile - 1) * n);
                    txtPodpowiedzPowtorz.Text =
                        $"→ {ile} wstawień co {n} dni: od {w2:dd.MM.yyyy} do {ostatnia:dd.MM.yyyy}.";
                }
                else txtPodpowiedzPowtorz.Text = "";
            }
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }

        private void PokazBlad(string msg) { txtBlad.Text = msg; boxBlad.Visibility = Visibility.Visible; }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (dpWst.SelectedDate is null) { PokazBlad("Ustaw datę wstawienia."); return; }
            int? szt = ParseInt(txtSzt.Text);
            if (szt is null || szt <= 0) { PokazBlad("Podaj liczbę piskląt."); return; }

            var pierwsza = dpWst.SelectedDate.Value;
            int ile = _edycja ? 1 : Math.Max(1, ParseInt(txtIle.Text) ?? 1);
            int n   = _edycja ? 0 : Math.Max(0, ParseInt(txtCoIle.Text) ?? 0);
            if (!_edycja && ile > 1 && n <= 0) { PokazBlad("Przy powtarzaniu podaj 'co ile dni'."); return; }
            if (ile > 24) { PokazBlad("Maks. 24 wstawień za jednym razem."); return; }

            Wynik = new List<HarmonogramCykl>(ile);
            for (int i = 0; i < ile; i++)
            {
                var data = pierwsza.AddDays(i * n);
                Wynik.Add(new HarmonogramCykl
                {
                    NrCyklu = _nrPoczatkowy + i,
                    DataWstawienia = data,
                    IloscWstawiona = szt,
                    DzienUbiorki = UbiorKaDni,
                    DataUbojuKoncowego = data.AddDays(PelnyUbojDni),
                    Status = "PLANOWANY"
                });
            }
            DialogResult = true;
            Close();
        }

        private static int? ParseInt(string? s)
        {
            s = (s ?? "").Trim().Replace(" ", "").Replace(".", "");
            return s.Length == 0 ? null : (int.TryParse(s, NumberStyles.Any, Pl, out var i) ? i : null);
        }
    }
}
