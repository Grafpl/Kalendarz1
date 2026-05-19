using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Kalendarz1.Zamowienia.Services;

namespace Kalendarz1.Admin
{
    public partial class LimitProdukcyjnyEditDialog : Window
    {
        private const string ConnLibra =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        public LimitProdukcyjny? Result { get; private set; }
        private readonly LimitProdukcyjny _editing;
        private readonly LimityProdukcyjneService _service;
        private bool _initializing = true;
        private System.Threading.CancellationTokenSource? _previewCts;

        public LimitProdukcyjnyEditDialog(LimitProdukcyjny? existing)
        {
            InitializeComponent();
            _service = new LimityProdukcyjneService(ConnLibra);
            try { Kalendarz1.WindowIconHelper.SetIcon(this); } catch { }

            if (existing != null)
            {
                DlgHeader.Text = $"✏️ Edycja grupy '{existing.NazwaGrupy}'";
                _editing = new LimitProdukcyjny
                {
                    Id = existing.Id,
                    NazwaGrupy = existing.NazwaGrupy,
                    Wzorzec = existing.Wzorzec,
                    SposobLiczeniaPlanu = existing.SposobLiczeniaPlanu,
                    ProcentZKurczakaA = existing.ProcentZKurczakaA,
                    PlanStalyKg = existing.PlanStalyKg,
                    ProcentLimitu = existing.ProcentLimitu,
                    Aktywny = existing.Aktywny,
                    Ikona = existing.Ikona,
                    Kolejnosc = existing.Kolejnosc
                };
            }
            else
            {
                DlgHeader.Text = "➕ Nowa grupa limitu";
                _editing = new LimitProdukcyjny
                {
                    SposobLiczeniaPlanu = LimityProdukcyjneService.SposobHarmonogramTuszkaA,
                    ProcentLimitu = 92m,
                    Aktywny = true,
                    Ikona = "🍗"
                };
            }

            BindFormFromModel();
            _initializing = false;

            // Initial preview
            _ = RefreshPreviewAsync();
        }

        private void BindFormFromModel()
        {
            TxtNazwa.Text = _editing.NazwaGrupy;
            CmbWzorzec.Text = _editing.Wzorzec;
            TxtIkona.Text = _editing.Ikona;
            ChkAktywny.IsChecked = _editing.Aktywny;
            TxtKolejnosc.Text = _editing.Kolejnosc.ToString(CultureInfo.InvariantCulture);

            SliderLimit.Value = (double)_editing.ProcentLimitu;
            LblLimitValue.Text = $"{_editing.ProcentLimitu:N0}%";

            TxtProcentZKurczakaA.Text = (_editing.ProcentZKurczakaA ?? 35m).ToString("0.##", CultureInfo.InvariantCulture);
            TxtPlanStaly.Text = (_editing.PlanStalyKg ?? 500m).ToString("0.##", CultureInfo.InvariantCulture);

            switch (_editing.SposobLiczeniaPlanu)
            {
                case LimityProdukcyjneService.SposobProcentKurczakaA: RbProcent.IsChecked = true; break;
                case LimityProdukcyjneService.SposobStaly: RbStaly.IsChecked = true; break;
                default: RbHarmonogram.IsChecked = true; break;
            }
        }

        private void Sposob_Checked(object sender, RoutedEventArgs e)
        {
            if (_initializing) return;
            // Visibility nie ruszam — pola wewnątrz radio są zawsze widoczne
            // żeby user widział wszystkie opcje na raz. Saving used radio chooses which value to bind.
        }

        private void SliderLimit_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (LblLimitValue == null) return; // odpala się przed InitializeComponent skończy
            LblLimitValue.Text = $"{e.NewValue:N0}%";
        }

        private void CmbWzorzec_Loaded(object sender, RoutedEventArgs e)
        {
            // Załaduj wszystkie towary do dropdown (raz)
            if (CmbWzorzec.ItemsSource == null)
                _ = LoadAllArticlesAsync();

            // Hook Text changes (ComboBox.TextProperty nie jest TextBoxBase — DependencyPropertyDescriptor)
            var dpd = System.ComponentModel.DependencyPropertyDescriptor.FromProperty(
                System.Windows.Controls.ComboBox.TextProperty, typeof(System.Windows.Controls.ComboBox));
            dpd?.RemoveValueChanged(CmbWzorzec, OnCmbWzorzecTextChanged);
            dpd?.AddValueChanged(CmbWzorzec, OnCmbWzorzecTextChanged);
        }

        private void OnCmbWzorzecTextChanged(object? sender, EventArgs e)
        {
            if (_initializing) return;
            _ = RefreshPreviewAsync();
        }

        private void CmbWzorzec_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is LimityProdukcyjneService.PreviewProdukt pp)
            {
                CmbWzorzec.Text = pp.Nazwa;
            }
        }

        private void BtnPickTowar_Click(object sender, RoutedEventArgs e)
        {
            CmbWzorzec.Focus();
            CmbWzorzec.IsDropDownOpen = true;
        }

        private async Task LoadAllArticlesAsync()
        {
            try
            {
                // "" wzorzec → wszystkie towary (zmodyfikuj service jeśli early return)
                var all = await _service.PreviewMatchingArticlesAsync(""); // pusta lista
                if (all.Count == 0)
                {
                    // Fallback — bezpośrednie zapytanie do bazy
                    all = await LoadAllArticlesDirectAsync();
                }
                await Kalendarz1.AnalitykaPelna.Services.TowaryZdjeciaService.LoadAsync(ConnLibra);
                foreach (var a in all)
                    a.Image = Kalendarz1.AnalitykaPelna.Services.TowaryZdjeciaService.Get(a.Id);
                CmbWzorzec.ItemsSource = all;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[LoadAllArticles] {ex.Message}"); }
        }

        private async Task<List<LimityProdukcyjneService.PreviewProdukt>> LoadAllArticlesDirectAsync()
        {
            var list = new List<LimityProdukcyjneService.PreviewProdukt>();
            try
            {
                await using var cn = new Microsoft.Data.SqlClient.SqlConnection(ConnLibra);
                await cn.OpenAsync();
                await using var cmd = new Microsoft.Data.SqlClient.SqlCommand(
                    "SELECT TOP 1000 ID, ISNULL(ShortName,'') AS Kod, ISNULL(Name,'') AS Nazwa FROM dbo.Article WHERE Name IS NOT NULL AND LTRIM(Name) <> '' ORDER BY Name", cn);
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    list.Add(new LimityProdukcyjneService.PreviewProdukt
                    {
                        Id = rd.GetInt32(0),
                        Kod = rd.GetString(1),
                        Nazwa = rd.GetString(2)
                    });
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[LoadAllArticlesDirect] {ex.Message}"); }
            return list;
        }

        private async System.Threading.Tasks.Task RefreshPreviewAsync()
        {
            try
            {
                _previewCts?.Cancel();
                _previewCts = new System.Threading.CancellationTokenSource();
                var token = _previewCts.Token;
                // Debounce 300ms
                await System.Threading.Tasks.Task.Delay(300, token);
                if (token.IsCancellationRequested) return;

                var wzorzec = (CmbWzorzec.Text ?? "").Trim();
                if (string.IsNullOrEmpty(wzorzec))
                {
                    PreviewArticles.ItemsSource = null;
                    PreviewCount.Text = "Wpisz wzorzec aby zobaczyć podgląd.";
                    return;
                }
                var articles = await _service.PreviewMatchingArticlesAsync(wzorzec);
                if (token.IsCancellationRequested) return;
                await Kalendarz1.AnalitykaPelna.Services.TowaryZdjeciaService.LoadAsync(ConnLibra);
                foreach (var a in articles)
                    a.Image = Kalendarz1.AnalitykaPelna.Services.TowaryZdjeciaService.Get(a.Id);
                PreviewArticles.ItemsSource = articles;
                PreviewCount.Text = articles.Count == 0
                    ? "(brak dopasowań)"
                    : $"Znaleziono {articles.Count} towar(ów)";
            }
            catch (TaskCanceledException) { }
            catch (OperationCanceledException) { }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[EditDlg preview] {ex.Message}"); }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            LblValidation.Text = "";

            string nazwa = (TxtNazwa.Text ?? "").Trim();
            string wzorzec = (CmbWzorzec.Text ?? "").Trim();
            string ikona = string.IsNullOrWhiteSpace(TxtIkona.Text) ? "🍗" : TxtIkona.Text!.Trim();

            if (string.IsNullOrWhiteSpace(nazwa)) { LblValidation.Text = "❌ Podaj nazwę grupy."; return; }
            if (string.IsNullOrWhiteSpace(wzorzec)) { LblValidation.Text = "❌ Podaj wzorzec dopasowania."; return; }

            string sposob;
            decimal? procentZKurA = null;
            decimal? planStaly = null;

            if (RbProcent.IsChecked == true)
            {
                sposob = LimityProdukcyjneService.SposobProcentKurczakaA;
                if (!TryParse(TxtProcentZKurczakaA.Text, out var p) || p < 0 || p > 200)
                { LblValidation.Text = "❌ % planu Kurczaka A: podaj liczbę 0-200."; return; }
                procentZKurA = p;
            }
            else if (RbStaly.IsChecked == true)
            {
                sposob = LimityProdukcyjneService.SposobStaly;
                if (!TryParse(TxtPlanStaly.Text, out var s) || s < 0)
                { LblValidation.Text = "❌ Stały plan kg/dzień: podaj liczbę ≥ 0."; return; }
                planStaly = s;
            }
            else
            {
                sposob = LimityProdukcyjneService.SposobHarmonogramTuszkaA;
            }

            int kolejnosc = 0;
            int.TryParse((TxtKolejnosc.Text ?? "0").Trim(), out kolejnosc);

            _editing.NazwaGrupy = nazwa;
            _editing.Wzorzec = wzorzec;
            _editing.Ikona = ikona;
            _editing.SposobLiczeniaPlanu = sposob;
            _editing.ProcentZKurczakaA = procentZKurA;
            _editing.PlanStalyKg = planStaly;
            _editing.ProcentLimitu = Math.Round((decimal)SliderLimit.Value, 2);
            _editing.Aktywny = ChkAktywny.IsChecked == true;
            _editing.Kolejnosc = kolejnosc;

            Result = _editing;
            DialogResult = true;
            Close();
        }

        private static bool TryParse(string? s, out decimal value)
        {
            s = (s ?? "").Replace(",", ".").Trim();
            return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
        }
    }
}
