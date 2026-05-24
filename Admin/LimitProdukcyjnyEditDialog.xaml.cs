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
        private const string ConnHandel =
            "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

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

            // Skróty klawiszowe: Esc = anuluj, Ctrl+Enter = zapisz
            Loaded += (s, e) =>
            {
                if (string.IsNullOrEmpty(TxtNazwa.Text)) TxtNazwa.Focus();
            };
            KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Escape)
                {
                    DialogResult = false;
                    Close();
                    e.Handled = true;
                }
                else if (e.Key == System.Windows.Input.Key.Enter
                         && System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control)
                {
                    BtnSave_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                }
            };
        }

        private void BindFormFromModel()
        {
            TxtNazwa.Text = _editing.NazwaGrupy;
            CmbWzorzec.Text = _editing.Wzorzec;
            TxtIkona.Text = _editing.Ikona;
            ChkAktywny.IsChecked = _editing.Aktywny;

            SliderLimit.Value = (double)_editing.ProcentLimitu;
            LblLimitValue.Text = $"{_editing.ProcentLimitu:N0}%";

            TxtProcentZKurczakaA.Text = (_editing.ProcentZKurczakaA ?? 35m).ToString("0.##", CultureInfo.InvariantCulture);
            TxtPlanStaly.Text = (_editing.PlanStalyKg ?? 500m).ToString("0.##", CultureInfo.InvariantCulture);

            switch (_editing.SposobLiczeniaPlanu)
            {
                case LimityProdukcyjneService.SposobKonfiguracjaProduktow: RbKonfiguracja.IsChecked = true; break;
                case LimityProdukcyjneService.SposobProcentKurczakaA: RbProcent.IsChecked = true; break;
                case LimityProdukcyjneService.SposobStaly: RbStaly.IsChecked = true; break;
                default: RbHarmonogram.IsChecked = true; break;
            }
        }

        // Quick preset chips — wypełniają formularz dla typowych grup
        // Tag format: "Nazwa|ikona|wzorzec|sposob(H/P/S)|[procent]|[limit]"
        private void BtnPreset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button btn) return;
            var parts = (btn.Tag?.ToString() ?? "").Split('|');
            if (parts.Length < 4) return;
            TxtNazwa.Text = parts[0];
            TxtIkona.Text = parts[1];
            CmbWzorzec.Text = parts[2];

            switch (parts[3])
            {
                case "H":
                    RbHarmonogram.IsChecked = true;
                    if (parts.Length >= 5 && double.TryParse(parts[4], NumberStyles.Any, CultureInfo.InvariantCulture, out var hLim))
                        SliderLimit.Value = hLim;
                    break;
                case "K":
                    RbKonfiguracja.IsChecked = true;
                    if (parts.Length >= 5 && double.TryParse(parts[4], NumberStyles.Any, CultureInfo.InvariantCulture, out var kLim))
                        SliderLimit.Value = kLim;
                    break;
                case "P":
                    RbProcent.IsChecked = true;
                    if (parts.Length >= 5) TxtProcentZKurczakaA.Text = parts[4];
                    if (parts.Length >= 6 && double.TryParse(parts[5], NumberStyles.Any, CultureInfo.InvariantCulture, out var pLim))
                        SliderLimit.Value = pLim;
                    break;
                case "S":
                    RbStaly.IsChecked = true;
                    if (parts.Length >= 5) TxtPlanStaly.Text = parts[4];
                    if (parts.Length >= 6 && double.TryParse(parts[5], NumberStyles.Any, CultureInfo.InvariantCulture, out var sLim))
                        SliderLimit.Value = sLim;
                    break;
            }
            _ = RefreshLivePreviewAsync();
        }

        // Live preview limit w kg — pokazuje aktualnie ile to limit dla dziś
        private async Task RefreshLivePreviewAsync()
        {
            if (_initializing) return;
            try
            {
                string wzorzec = (CmbWzorzec.Text ?? "").Trim();
                if (string.IsNullOrEmpty(wzorzec)) { LivePreviewBox.Visibility = Visibility.Collapsed; return; }

                // Stwórz tymczasowy obiekt z bieżącymi wartościami formularza
                decimal procent = (decimal)SliderLimit.Value;
                string sposob = RbKonfiguracja.IsChecked == true ? LimityProdukcyjneService.SposobKonfiguracjaProduktow
                              : RbProcent.IsChecked == true ? LimityProdukcyjneService.SposobProcentKurczakaA
                              : RbStaly.IsChecked == true ? LimityProdukcyjneService.SposobStaly
                              : LimityProdukcyjneService.SposobHarmonogramTuszkaA;

                decimal? procentZKurA = null;
                decimal? planStaly = null;
                if (sposob == LimityProdukcyjneService.SposobProcentKurczakaA &&
                    decimal.TryParse((TxtProcentZKurczakaA.Text ?? "").Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var p))
                    procentZKurA = p;
                if (sposob == LimityProdukcyjneService.SposobStaly &&
                    decimal.TryParse((TxtPlanStaly.Text ?? "").Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var s))
                    planStaly = s;

                var temp = new LimitProdukcyjny
                {
                    Id = -1,
                    NazwaGrupy = "temp",
                    Wzorzec = wzorzec,
                    SposobLiczeniaPlanu = sposob,
                    ProcentZKurczakaA = procentZKurA,
                    PlanStalyKg = planStaly,
                    ProcentLimitu = procent,
                    Aktywny = true
                };

                decimal limitKg = await CalculateLimitForTodayAsync(temp);
                LblLivePreviewKg.Text = $"{limitKg:N0} kg";
                LivePreviewBox.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LivePreview] {ex.Message}");
                LivePreviewBox.Visibility = Visibility.Collapsed;
            }
        }

        // Wylicza Plan+Stan dla TYLKO TEJ definicji (pomija inne aktywne grupy żeby nie zaśmiecać DB)
        private async Task<decimal> CalculateLimitForTodayAsync(LimitProdukcyjny m)
        {
            await using var cn = new Microsoft.Data.SqlClient.SqlConnection(ConnLibra);
            await cn.OpenAsync();

            decimal wspTuszki = 78m, procentA = 85m, procentB = 15m;
            try
            {
                await using var cmd = new Microsoft.Data.SqlClient.SqlCommand(
                    "SELECT TOP 1 WspolczynnikTuszki, ProcentTuszkaA, ProcentTuszkaB FROM KonfiguracjaWydajnosci WHERE DataOd <= @D AND Aktywny = 1 ORDER BY DataOd DESC", cn);
                cmd.Parameters.AddWithValue("@D", DateTime.Today);
                await using var rd = await cmd.ExecuteReaderAsync();
                if (await rd.ReadAsync())
                { wspTuszki = Convert.ToDecimal(rd[0]); procentA = Convert.ToDecimal(rd[1]); procentB = Convert.ToDecimal(rd[2]); }
            }
            catch { }

            decimal totalZywiec = 0m;
            try
            {
                await using var cmd = new Microsoft.Data.SqlClient.SqlCommand(
                    "SELECT WagaDek, SztukiDek FROM dbo.HarmonogramDostaw WHERE DataOdbioru = @D AND Bufor IN ('B.Wolny','B.Kontr.','Potwierdzony')", cn);
                cmd.Parameters.AddWithValue("@D", DateTime.Today);
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    var w = rd.IsDBNull(0) ? 0m : Convert.ToDecimal(rd.GetValue(0));
                    var s = rd.IsDBNull(1) ? 0m : Convert.ToDecimal(rd.GetValue(1));
                    totalZywiec += w * s;
                }
            }
            catch { }

            decimal planKurA = totalZywiec * (wspTuszki / 100m) * (procentA / 100m);
            decimal pulaB = totalZywiec * (wspTuszki / 100m) * (procentB / 100m);

            decimal planKg;
            if (m.SposobLiczeniaPlanu == LimityProdukcyjneService.SposobKonfiguracjaProduktow)
            {
                // Pobierz IDs z HM.TW + ProcentUdzialu z KonfiguracjaProduktow
                decimal sumaProcent = 0m;
                try
                {
                    await using var cnH = new Microsoft.Data.SqlClient.SqlConnection(ConnHandel);
                    await cnH.OpenAsync();
                    var ids = await LimityProdukcyjneService_MatchingIds(cnH, m.Wzorzec);
                    if (ids.Count > 0)
                    {
                        string idsCsv = string.Join(",", ids);
                        await using var cmdK = new Microsoft.Data.SqlClient.SqlCommand(
                            $@"SELECT ISNULL(SUM(kp.ProcentUdzialu),0)
                               FROM KonfiguracjaProduktow kp
                               INNER JOIN (SELECT MAX(DataOd) MaxData FROM KonfiguracjaProduktow WHERE DataOd <= @D AND Aktywny = 1) sub ON kp.DataOd = sub.MaxData
                               WHERE kp.Aktywny = 1 AND kp.TowarID IN ({idsCsv})", cn);
                        cmdK.Parameters.AddWithValue("@D", DateTime.Today);
                        var r = await cmdK.ExecuteScalarAsync();
                        if (r != null && r != DBNull.Value) sumaProcent = Convert.ToDecimal(r);
                    }
                }
                catch { }
                planKg = pulaB * (sumaProcent / 100m);
            }
            else
            {
                planKg = m.SposobLiczeniaPlanu switch
                {
                    LimityProdukcyjneService.SposobProcentKurczakaA => planKurA * ((m.ProcentZKurczakaA ?? 0m) / 100m),
                    LimityProdukcyjneService.SposobStaly => m.PlanStalyKg ?? 0m,
                    _ => planKurA
                };
            }

            // Stan z poprzedniego dnia — matching IDs musi być z HANDEL HM.TW (StanyMagazynowe.ProduktId = HM.TW.id)
            decimal stanKg = 0m;
            try
            {
                await using var cnH = new Microsoft.Data.SqlClient.SqlConnection(ConnHandel);
                await cnH.OpenAsync();
                var ids = await LimityProdukcyjneService_MatchingIds(cnH, m.Wzorzec);
                if (ids.Count > 0)
                {
                    string idsCsv = string.Join(",", ids);
                    await using var cmd = new Microsoft.Data.SqlClient.SqlCommand(
                        $"SELECT ISNULL(SUM(Stan),0) FROM dbo.StanyMagazynowe WHERE Data = @D AND ProduktId IN ({idsCsv})", cn);
                    cmd.Parameters.AddWithValue("@D", DateTime.Today);
                    var r = await cmd.ExecuteScalarAsync();
                    if (r != null && r != DBNull.Value) stanKg = Convert.ToDecimal(r);
                }
            }
            catch { }

            return Math.Round((planKg + stanKg) * (m.ProcentLimitu / 100m), 0);
        }

        // UWAGA: connection musi być do HANDEL (HM.TW) — to ID jest spójne z ZamowieniaMiesoTowar.KodTowaru i StanyMagazynowe.ProduktId.
        private static async Task<List<int>> LimityProdukcyjneService_MatchingIds(Microsoft.Data.SqlClient.SqlConnection cnHandel, string wzorzec)
        {
            var ids = new List<int>();
            await using var cmd = new Microsoft.Data.SqlClient.SqlCommand(
                "SELECT id FROM [HANDEL].[HM].[TW] WHERE kod LIKE @p", cnHandel);
            cmd.Parameters.AddWithValue("@p", "%" + wzorzec + "%");
            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync()) ids.Add(rd.GetInt32(0));
            return ids;
        }

        private void Sposob_Checked(object sender, RoutedEventArgs e)
        {
            if (_initializing) return;
            _ = RefreshLivePreviewAsync();
        }

        private void SliderLimit_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (LblLimitValue == null) return; // odpala się przed InitializeComponent skończy
            LblLimitValue.Text = $"{e.NewValue:N0}%";
            _ = RefreshLivePreviewAsync();
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
            _ = RefreshLivePreviewAsync();
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
                // HANDEL HM.TW — identyczne źródło z runtime matching (KodTowaru = HM.TW.id)
                await using var cn = new Microsoft.Data.SqlClient.SqlConnection(ConnHandel);
                await cn.OpenAsync();
                await using var cmd = new Microsoft.Data.SqlClient.SqlCommand(
                    "SELECT TOP 1000 id, ISNULL(kod,'') AS Nazwa, CAST(katalog AS NVARCHAR(32)) AS Katalog " +
                    "FROM [HANDEL].[HM].[TW] WHERE katalog IN ('67095','67153') AND kod IS NOT NULL AND LTRIM(kod) <> '' ORDER BY kod", cn);
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    list.Add(new LimityProdukcyjneService.PreviewProdukt
                    {
                        Id = rd.GetInt32(0),
                        Kod = rd.IsDBNull(2) ? "" : rd.GetString(2),
                        Nazwa = rd.GetString(1)
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

            if (RbKonfiguracja.IsChecked == true)
            {
                sposob = LimityProdukcyjneService.SposobKonfiguracjaProduktow;
            }
            else if (RbProcent.IsChecked == true)
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

            _editing.NazwaGrupy = nazwa;
            _editing.Wzorzec = wzorzec;
            _editing.Ikona = ikona;
            _editing.SposobLiczeniaPlanu = sposob;
            _editing.ProcentZKurczakaA = procentZKurA;
            _editing.PlanStalyKg = planStaly;
            _editing.ProcentLimitu = Math.Round((decimal)SliderLimit.Value, 2);
            _editing.Aktywny = ChkAktywny.IsChecked == true;
            // Kolejnosc: zachowaj istniejącą wartość (z _editing, ustawioną w ctor)

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
