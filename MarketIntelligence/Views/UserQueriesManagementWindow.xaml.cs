using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Kalendarz1.MarketIntelligence.Services;

namespace Kalendarz1.MarketIntelligence.Views
{
    /// <summary>
    /// Okno zarządzania własnymi tematami (intel_UserQueries) — zapytania do Perplexity dla daily fetch.
    /// User definiuje co AI ma śledzić: HPAI łódzkie, ceny żywca, Cedrob, Biedronka itd.
    /// </summary>
    public partial class UserQueriesManagementWindow : Window
    {
        private readonly UserQueriesService _service = new();
        private List<UserQuery> _queries = new();
        private UserQuery _editing;

        public UserQueriesManagementWindow()
        {
            InitializeComponent();
            Loaded += async (_, __) => await LoadAsync();
        }

        private async Task LoadAsync()
        {
            try
            {
                _queries = await _service.GetAllAsync();
                dgQueries.ItemsSource = null;
                dgQueries.ItemsSource = _queries;
                txtStats.Text = $"Łącznie: {_queries.Count}  ·  Aktywne: {_queries.Count(q => q.Enabled)}  ·  +16 hardcoded";
                ClearEditor();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd ładowania: " + ex.Message);
            }
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e) => await LoadAsync();

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            _editing = new UserQuery
            {
                Enabled = true,
                Priority = 5,
                Category = "Custom",
                RecencyFilter = "week"
            };
            BindToEditor(_editing);
            txtQuery.Focus();
        }

        private void DgQueries_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgQueries.SelectedItem is UserQuery q)
            {
                _editing = q;
                BindToEditor(q);
            }
        }

        private void BindToEditor(UserQuery q)
        {
            txtQuery.Text = q.QueryText;
            cbCategory.Text = q.Category;
            txtNotes.Text = q.Notes;
            chkEnabled.IsChecked = q.Enabled;

            // Priority: mapuje stary zakres 1-9 na 3 widoczne kubełki (1=Krytyczne / 5=Normalne / 9=Niskie)
            var bucket = q.Priority <= 3 ? 1 : (q.Priority <= 6 ? 5 : 9);
            for (int i = 0; i < cbPriority.Items.Count; i++)
            {
                if (cbPriority.Items[i] is ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out var p) && p == bucket)
                {
                    cbPriority.SelectedIndex = i;
                    break;
                }
            }
            // Recency
            for (int i = 0; i < cbRecency.Items.Count; i++)
            {
                if (cbRecency.Items[i] is ComboBoxItem item && item.Tag?.ToString() == q.RecencyFilter)
                {
                    cbRecency.SelectedIndex = i;
                    break;
                }
            }
        }

        private void ClearEditor()
        {
            _editing = null;
            txtQuery.Text = "";
            cbCategory.Text = "Custom";
            txtNotes.Text = "";
            chkEnabled.IsChecked = true;
            cbPriority.SelectedIndex = 1; // Normalne (5)
            cbRecency.SelectedIndex = 1;  // week
        }

        private void BtnToggleHelp_Click(object sender, RoutedEventArgs e)
        {
            helpBanner.Visibility = helpBanner.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void BtnTmpl_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string tag) return;
            var tmpl = GetTemplate(tag);
            if (tmpl == null) return;

            _editing = tmpl;
            BindToEditor(_editing);
            txtQuery.Focus();
        }

        private UserQuery GetTemplate(string tag) => tag switch
        {
            "HPAI_łódzkie" => new UserQuery
            {
                QueryText = "HPAI ptasia grypa łódzkie 2026 - ogniska w powiatach (zgierski, brzeziński, łódzki-wschodni), strefy zapowietrzone, decyzje GLW i wojewody. Wpływ na ubojnie regionalne.",
                Category = "HPAI",
                Priority = 1,
                RecencyFilter = "week",
                Enabled = true,
                Notes = "Krytyczne dla Piórkowscy — Brzeziny, łódzkie. Sprawdza co dzień."
            },
            "Ceny_żywca" => new UserQuery
            {
                QueryText = "Ceny skupu żywca drobiowego kurczak brojler Polska 2026. Ile ubojnie płacą hodowcom za kilogram? Notowania KIPD, Krajowa Izba Producentów Drobiu, MRiRW.",
                Category = "Ceny",
                Priority = 1,
                RecencyFilter = "week",
                Enabled = true,
                Notes = "Punkt odniesienia do naszych cen skupu (obecnie 4.72 zł/kg)."
            },
            "Cedrob_ADQ" => new UserQuery
            {
                QueryText = "Cedrob ADQ Abu Dhabi przejęcie 2026 - status negocjacji, due diligence, cena transakcji, termin finalizacji, opinia UOKiK. Wpływ na rynek polskiego drobiu.",
                Category = "Konkurencja",
                Priority = 1,
                RecencyFilter = "week",
                Enabled = true,
                Notes = "Krytyczne zagrożenie — Cedrob = 40% rynku, jeśli ADQ przejmie + Drosed = monopol."
            },
            "Biedronka" => new UserQuery
            {
                QueryText = "Biedronka Jeronimo Martins mięso drobiowe 2026 - promocje, ceny detaliczne kurczak, dostawcy, kontrakty, marża, ekspansja.",
                Category = "Klienci",
                Priority = 1,
                RecencyFilter = "week",
                Enabled = true,
                Notes = "Biedronka DC = 380 palet/mies, NIEPRZYPISANY handlowiec, potencjał ogromny."
            },
            "Pasze_MATIF" => new UserQuery
            {
                QueryText = "Ceny pasz drobiowych Polska 2026 - kukurydza, śruta sojowa, pszenica. Notowania MATIF Euronext, CBOT. Prognozy. Wpływ na koszty hodowli brojlerów.",
                Category = "Pasze",
                Priority = 5,
                RecencyFilter = "week",
                Enabled = true,
                Notes = "Pasza = 65-70% kosztu hodowcy. Relacja żywiec/pasza wpływa na nasze ceny skupu."
            },
            "KSeF_Sage" => new UserQuery
            {
                QueryText = "KSeF faktury elektroniczne 2026 - integracja z Sage Symfonia HANDEL. Wdrożenia w branży mięsnej. Problemy, koszty, terminy.",
                Category = "Regulacje",
                Priority = 1,
                RecencyFilter = "week",
                Enabled = true,
                Notes = "Używamy Sage Symfonia. KSeF obowiązuje od 01.04.2026."
            },
            "Brazylia" => new UserQuery
            {
                QueryText = "Import drobiu z Brazylii do Polski 2026 - BRF, JBS, Seara. Wolumeny, ceny fileta mrożonego w Makro/Selgros. Wpływ Salmonelli na zakaz UE od 03.09.2026.",
                Category = "Import",
                Priority = 1,
                RecencyFilter = "week",
                Enabled = true,
                Notes = "Filet brazylijski 13 zł vs nasze 15-17 zł — kluczowe zagrożenie cenowe."
            },
            "MHP_Ukraina" => new UserQuery
            {
                QueryText = "MHP Myronivsky Hliboproduct Ukraina drób 2026 - wolumeny, ceny, eksport do UE i Polski. Przeniesienie produkcji do UE. Wpływ na polski rynek.",
                Category = "Import",
                Priority = 1,
                RecencyFilter = "week",
                Enabled = true,
                Notes = "MHP rośnie 8% rocznie, większość przez UE division — bezpośrednia konkurencja."
            },
            _ => null
        };

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (_editing == null) _editing = new UserQuery();

            var query = (txtQuery.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(query))
            {
                MessageBox.Show("Wpisz treść zapytania.");
                txtQuery.Focus();
                return;
            }

            _editing.QueryText = query;
            _editing.Category = (cbCategory.Text ?? "Custom").Trim();
            _editing.Notes = (txtNotes.Text ?? "").Trim();
            _editing.Enabled = chkEnabled.IsChecked == true;

            if (cbPriority.SelectedItem is ComboBoxItem priItem && int.TryParse(priItem.Tag?.ToString(), out var pri))
                _editing.Priority = pri;
            if (cbRecency.SelectedItem is ComboBoxItem recItem)
                _editing.RecencyFilter = recItem.Tag?.ToString() ?? "week";

            try
            {
                if (_editing.Id == 0)
                {
                    var id = await _service.InsertAsync(_editing);
                    _editing.Id = id;
                }
                else
                {
                    await _service.UpdateAsync(_editing);
                }
                await LoadAsync();
                MessageBox.Show($"✅ Zapisano temat:\n{query.Substring(0, Math.Min(80, query.Length))}...\n\nUżyty przy następnym pełnym pobraniu.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd zapisu: " + ex.Message);
            }
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_editing == null || _editing.Id == 0)
            {
                MessageBox.Show("Brak tematu do usunięcia.");
                return;
            }
            var result = MessageBox.Show(
                $"Usunąć temat?\n\n{_editing.QueryText.Substring(0, Math.Min(100, _editing.QueryText.Length))}...",
                "Potwierdzenie", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                await _service.DeleteAsync(_editing.Id);
                await LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd usunięcia: " + ex.Message);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => ClearEditor();
        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
