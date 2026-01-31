using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using ClosedXML.Excel;
using Microsoft.Win32;
using Kalendarz1.Kartoteka.Models;
using Kalendarz1.Kartoteka.Services;

namespace Kalendarz1.Kartoteka.Views
{
    public partial class KartotekaOdbiorcowWindow : Window
    {
        private KartotekaService _service;
        private string _userId;
        private string _userName;
        private List<FakturaOdbiorcy> _currentFaktury = new();

        private readonly string _libraNetConn = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private readonly string _handelConn = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

        // Accordion state
        private List<OdbiorcaHandlowca> _allOdbiorcy = new();
        private List<OdbiorcaHandlowca> _displayedOdbiorcy = new();
        private List<TowarKatalog> _towary = new();
        private Dictionary<int, HashSet<string>> _odbiorcyTowary = new();
        private OdbiorcaHandlowca _selectedOdbiorca;
        private Border _expandedCard;
        private bool _isAdmin;

        // Sorting state
        private string _sortColumn = "OstFaktura";
        private bool _sortAscending = false;

        public string UserID
        {
            get => _userId;
            set => _userId = value;
        }

        public KartotekaOdbiorcowWindow()
        {
            InitializeComponent();
            _userId = App.UserID ?? "11111";
            _userName = App.UserFullName ?? "Administrator";
        }

        public KartotekaOdbiorcowWindow(string userId, string userName) : this()
        {
            _userId = userId;
            _userName = userName;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _service = new KartotekaService(_libraNetConn, _handelConn);

            _isAdmin = _userId == "11111";
            if (_isAdmin)
            {
                ComboBoxHandlowiec.Visibility = Visibility.Visible;
                TextBlockHandlowiec.Text = "Administrator - wszystkie dane";
            }
            else
            {
                TextBlockHandlowiec.Text = $"Handlowiec: {_userName}";
            }

            await LoadData();
        }

        private async System.Threading.Tasks.Task LoadData()
        {
            try
            {
                LoadingOverlay.Visibility = Visibility.Visible;

                string handlowiec = null;
                bool pokazWszystkich = false;

                if (_userId == "11111")
                {
                    var selected = ComboBoxHandlowiec.SelectedItem as ComboBoxItem;
                    var selectedText = selected?.Content?.ToString();
                    if (string.IsNullOrEmpty(selectedText) || selectedText == "Wszyscy")
                    {
                        pokazWszystkich = true;
                    }
                    else
                    {
                        handlowiec = selectedText;
                    }
                }
                else
                {
                    handlowiec = _userName;
                }

                await _service.EnsureTablesExistAsync();

                var odbiorcy = await _service.PobierzOdbiorcowAsync(handlowiec, pokazWszystkich);
                await _service.WczytajDaneWlasneAsync(odbiorcy);

                _allOdbiorcy = odbiorcy;
                _displayedOdbiorcy = odbiorcy;

                ApplySortAndRegenerate();

                // Załaduj handlowców dla admina
                if (_userId == "11111" && ComboBoxHandlowiec.Items.Count <= 1)
                {
                    ComboBoxHandlowiec.Items.Clear();
                    ComboBoxHandlowiec.Items.Add(new ComboBoxItem { Content = "Wszyscy", IsSelected = true });
                    var handlowcy = await _service.PobierzHandlowcowAsync();
                    foreach (var h in handlowcy)
                    {
                        ComboBoxHandlowiec.Items.Add(new ComboBoxItem { Content = h });
                    }
                }

                UpdateStatystyki();
                UpdateLicznik();
                UpdatePowiadomienia();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania danych:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }

            _ = LoadAsortymentInBackground();
            _ = LoadTowaryAsync();
        }

        private async System.Threading.Tasks.Task LoadAsortymentInBackground()
        {
            try
            {
                if (_allOdbiorcy == null || _allOdbiorcy.Count == 0) return;

                var khids = _allOdbiorcy.Select(o => o.IdSymfonia).ToList();
                var asortyment = await _service.PobierzAsortymentAsync(khids, 6);

                foreach (var odbiorca in _allOdbiorcy)
                {
                    if (asortyment.TryGetValue(odbiorca.IdSymfonia, out var produkty))
                    {
                        if (string.IsNullOrEmpty(odbiorca.Asortyment))
                            odbiorca.Asortyment = produkty;
                    }
                }

                // Refresh cards to show updated asortyment
                ApplySortAndRegenerate();
            }
            catch { }
        }

        private async System.Threading.Tasks.Task LoadTowaryAsync()
        {
            try
            {
                _towary = await _service.PobierzTowaryKatalogAsync();

                ComboBoxTowar.Items.Clear();
                ComboBoxTowar.Items.Add(new ComboBoxItem { Content = "Towar (wszystkie)", IsSelected = true });

                var swieze = _towary.Where(t => t.Katalog == "Świeże").ToList();
                var mrozonki = _towary.Where(t => t.Katalog == "Mrożonki").ToList();

                ComboBoxTowar.Items.Add(new ComboBoxItem { Content = "── Świeże ──", IsEnabled = false, FontWeight = FontWeights.Bold });
                foreach (var t in swieze)
                    ComboBoxTowar.Items.Add(new ComboBoxItem { Content = $"{t.Kod} - {t.Nazwa}", Tag = t.Kod });

                ComboBoxTowar.Items.Add(new ComboBoxItem { Content = "── Mrożonki ──", IsEnabled = false, FontWeight = FontWeights.Bold });
                foreach (var t in mrozonki)
                    ComboBoxTowar.Items.Add(new ComboBoxItem { Content = $"{t.Kod} - {t.Nazwa}", Tag = t.Kod });

                ComboBoxTowar.SelectedIndex = 0;
            }
            catch { }
        }

        // ═══════════════════════════════════════════
        // ACCORDION - Generowanie kart
        // ═══════════════════════════════════════════

        private void GenerateCards(List<OdbiorcaHandlowca> odbiorcy)
        {
            // Remember expanded state
            var expandedId = _selectedOdbiorca?.IdSymfonia;

            // Remove DetailPanel from AccordionPanel
            AccordionPanel.Children.Remove(DetailPanel);
            AccordionPanel.Children.Clear();

            _expandedCard = null;
            _selectedOdbiorca = null;

            // ── Sticky column headers ──
            var header = CreateHeaderRow();
            HeaderRowContainer.Child = header;
            HeaderRowContainer.Visibility = Visibility.Visible;

            foreach (var o in odbiorcy)
            {
                var card = CreateCustomerCard(o);
                AccordionPanel.Children.Add(card);
            }

            // Add DetailPanel back at the end (collapsed)
            AccordionPanel.Children.Add(DetailPanel);
            DetailPanel.Visibility = Visibility.Collapsed;

            // Re-expand previously expanded card if still in list
            if (expandedId.HasValue)
            {
                foreach (Border card in AccordionPanel.Children.OfType<Border>().Where(b => b != DetailPanel))
                {
                    if (card.Tag is OdbiorcaHandlowca o && o.IdSymfonia == expandedId.Value)
                    {
                        ExpandCard(card, o);
                        break;
                    }
                }
            }
        }

        // Column sort keys matching header order (index → sort key)
        private static readonly string[] _columnSortKeys = new[]
        {
            "",           // 0: dot
            "Firma",      // 1
            "NIP",        // 2
            "Kontakt",    // 3
            "Telefon",    // 4
            "Forma",      // 5
            "Termin",     // 6
            "Naleznosci", // 7
            "Limit",      // 8
            "Bilans",     // 9
            "Przeter",    // 10
            "Procent",    // 11
            "OstFaktura", // 12
            "Kategoria",  // 13
            "Handlowiec", // 14 (admin only)
            ""            // arrow
        };

        private Grid CreateCardColumnDefinitions()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });   // 0: Status dot
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 140 }); // 1: Firma + miasto
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(95) });   // 2: NIP
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });   // 3: Kontakt
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(85) });   // 4: Telefon
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });   // 5: Forma płatn.
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(45) });   // 6: Termin
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });   // 7: Należności
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });   // 8: Limit
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });   // 9: Bilans
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });   // 10: Przetermin.
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });   // 11: % limitu
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(65) });   // 12: Ost. faktura
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });   // 13: Kategoria
            if (_isAdmin)
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(75) }); // 14: Handlowiec
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });   // Arrow
            return grid;
        }

        private Border CreateHeaderRow()
        {
            var header = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(249, 250, 251)), // #F9FAFB
                BorderBrush = new SolidColorBrush(Color.FromRgb(209, 213, 219)), // #D1D5DB
                BorderThickness = new Thickness(0, 0, 0, 2),
                Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(0, 0, 0, 1)
            };

            var grid = CreateCardColumnDefinitions();
            var grayBrush = new SolidColorBrush(Color.FromRgb(107, 114, 128)); // #6B7280
            var greenBrush = new SolidColorBrush(Color.FromRgb(22, 101, 52));  // #166534

            string[] headers = _isAdmin
                ? new[] { "", "Firma", "NIP", "Kontakt", "Telefon", "Forma", "Termin", "Należności", "Limit", "Bilans", "Przeter.", "%", "Ost.fakt.", "Kat.", "Handl.", "" }
                : new[] { "", "Firma", "NIP", "Kontakt", "Telefon", "Forma", "Termin", "Należności", "Limit", "Bilans", "Przeter.", "%", "Ost.fakt.", "Kat.", "" };

            // Determine sort key indices considering admin
            int totalCols = headers.Length;

            for (int i = 0; i < totalCols; i++)
            {
                if (string.IsNullOrEmpty(headers[i]))
                {
                    // Empty column (dot or arrow) - no header
                    Grid.SetColumn(new Border(), i); // placeholder
                    continue;
                }

                // Determine the sort key for this column index
                string sortKey = GetSortKeyForColumnIndex(i);

                var sortIndicator = "";
                if (_sortColumn == sortKey && !string.IsNullOrEmpty(sortKey))
                    sortIndicator = _sortAscending ? " ▲" : " ▼";

                var tb = new TextBlock
                {
                    Text = headers[i] + sortIndicator,
                    FontSize = 10,
                    FontWeight = (_sortColumn == sortKey && !string.IsNullOrEmpty(sortKey)) ? FontWeights.Bold : FontWeights.SemiBold,
                    Foreground = (_sortColumn == sortKey && !string.IsNullOrEmpty(sortKey)) ? greenBrush : grayBrush,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Cursor = string.IsNullOrEmpty(sortKey) ? Cursors.Arrow : Cursors.Hand
                };

                // Right-align numeric columns (Należności=7, Limit=8, Bilans=9, Przeter=10, %=11)
                int baseIdx = _isAdmin ? i : i; // same since admin column is near end
                // For non-admin: indices 7-11 are numeric; for admin same
                if (i >= 7 && i <= 11)
                {
                    tb.HorizontalAlignment = HorizontalAlignment.Right;
                    tb.Margin = new Thickness(0, 0, 8, 0);
                }
                int katIdx = _isAdmin ? 13 : 13;
                int ostFaktIdx = _isAdmin ? 12 : 12;
                if (i == ostFaktIdx)
                    tb.HorizontalAlignment = HorizontalAlignment.Center;
                if (i == katIdx)
                    tb.HorizontalAlignment = HorizontalAlignment.Center;

                // Click handler for sorting
                if (!string.IsNullOrEmpty(sortKey))
                {
                    var key = sortKey; // capture
                    var border = new Border
                    {
                        Background = Brushes.Transparent,
                        Child = tb,
                        Cursor = Cursors.Hand
                    };
                    border.MouseLeftButtonDown += (s, e) =>
                    {
                        if (_sortColumn == key)
                            _sortAscending = !_sortAscending;
                        else
                        {
                            _sortColumn = key;
                            _sortAscending = true;
                        }
                        ApplySortAndRegenerate();
                    };
                    border.MouseEnter += (s, e) => tb.TextDecorations = TextDecorations.Underline;
                    border.MouseLeave += (s, e) => tb.TextDecorations = null;
                    Grid.SetColumn(border, i);
                    grid.Children.Add(border);
                }
                else
                {
                    Grid.SetColumn(tb, i);
                    grid.Children.Add(tb);
                }
            }

            header.Child = grid;
            return header;
        }

        private string GetSortKeyForColumnIndex(int colIndex)
        {
            // Map visual column index to sort key
            // Non-admin: 0=dot,1=Firma,2=NIP,3=Kontakt,4=Telefon,5=Forma,6=Termin,7=Naleznosci,8=Limit,9=Bilans,10=Przeter,11=Procent,12=OstFaktura,13=Kategoria,14=arrow
            // Admin: same but 14=Handlowiec,15=arrow
            if (colIndex <= 0) return "";
            if (colIndex < _columnSortKeys.Length)
                return _columnSortKeys[colIndex];
            return "";
        }

        private void ApplySortAndRegenerate()
        {
            if (!string.IsNullOrEmpty(_sortColumn))
            {
                var sorted = _sortColumn switch
                {
                    "Firma" => _sortAscending
                        ? _displayedOdbiorcy.OrderBy(o => o.Skrot ?? o.NazwaFirmy ?? "").ToList()
                        : _displayedOdbiorcy.OrderByDescending(o => o.Skrot ?? o.NazwaFirmy ?? "").ToList(),
                    "NIP" => _sortAscending
                        ? _displayedOdbiorcy.OrderBy(o => o.NIP ?? "").ToList()
                        : _displayedOdbiorcy.OrderByDescending(o => o.NIP ?? "").ToList(),
                    "Kontakt" => _sortAscending
                        ? _displayedOdbiorcy.OrderBy(o => o.OsobaKontaktowa ?? "").ToList()
                        : _displayedOdbiorcy.OrderByDescending(o => o.OsobaKontaktowa ?? "").ToList(),
                    "Telefon" => _sortAscending
                        ? _displayedOdbiorcy.OrderBy(o => o.TelefonKontakt ?? "").ToList()
                        : _displayedOdbiorcy.OrderByDescending(o => o.TelefonKontakt ?? "").ToList(),
                    "Forma" => _sortAscending
                        ? _displayedOdbiorcy.OrderBy(o => o.FormaPlatnosci ?? "").ToList()
                        : _displayedOdbiorcy.OrderByDescending(o => o.FormaPlatnosci ?? "").ToList(),
                    "Termin" => _sortAscending
                        ? _displayedOdbiorcy.OrderBy(o => o.TerminPlatnosci).ToList()
                        : _displayedOdbiorcy.OrderByDescending(o => o.TerminPlatnosci).ToList(),
                    "Naleznosci" => _sortAscending
                        ? _displayedOdbiorcy.OrderBy(o => o.WykorzystanoLimit).ToList()
                        : _displayedOdbiorcy.OrderByDescending(o => o.WykorzystanoLimit).ToList(),
                    "Limit" => _sortAscending
                        ? _displayedOdbiorcy.OrderBy(o => o.LimitKupiecki).ToList()
                        : _displayedOdbiorcy.OrderByDescending(o => o.LimitKupiecki).ToList(),
                    "Bilans" => _sortAscending
                        ? _displayedOdbiorcy.OrderBy(o => o.Bilans).ToList()
                        : _displayedOdbiorcy.OrderByDescending(o => o.Bilans).ToList(),
                    "Przeter" => _sortAscending
                        ? _displayedOdbiorcy.OrderBy(o => o.KwotaPrzeterminowana).ToList()
                        : _displayedOdbiorcy.OrderByDescending(o => o.KwotaPrzeterminowana).ToList(),
                    "Procent" => _sortAscending
                        ? _displayedOdbiorcy.OrderBy(o => o.ProcentWykorzystania).ToList()
                        : _displayedOdbiorcy.OrderByDescending(o => o.ProcentWykorzystania).ToList(),
                    "OstFaktura" => _sortAscending
                        ? _displayedOdbiorcy.OrderBy(o => o.OstatniaFakturaData ?? DateTime.MinValue).ToList()
                        : _displayedOdbiorcy.OrderByDescending(o => o.OstatniaFakturaData ?? DateTime.MinValue).ToList(),
                    "Kategoria" => _sortAscending
                        ? _displayedOdbiorcy.OrderBy(o => o.KategoriaHandlowca ?? "C").ToList()
                        : _displayedOdbiorcy.OrderByDescending(o => o.KategoriaHandlowca ?? "C").ToList(),
                    "Handlowiec" => _sortAscending
                        ? _displayedOdbiorcy.OrderBy(o => o.Handlowiec ?? "").ToList()
                        : _displayedOdbiorcy.OrderByDescending(o => o.Handlowiec ?? "").ToList(),
                    _ => _displayedOdbiorcy
                };
                _displayedOdbiorcy = sorted;
            }
            GenerateCards(_displayedOdbiorcy);
        }

        private Border CreateCustomerCard(OdbiorcaHandlowca odbiorca)
        {
            var card = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(229, 231, 235)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(0, 0, 0, 1),
                Cursor = Cursors.Hand,
                Tag = odbiorca
            };
            card.Effect = new DropShadowEffect
            {
                ShadowDepth = 0, Color = Colors.Black, Opacity = 0.04, BlurRadius = 4
            };

            // Alert row background
            switch (odbiorca.AlertType)
            {
                case "LimitExceeded":
                    card.Background = new SolidColorBrush(Color.FromRgb(254, 242, 242)); // #FEF2F2
                    card.BorderBrush = new SolidColorBrush(Color.FromRgb(248, 113, 113)); // #F87171
                    break;
                case "Overdue":
                    card.Background = new SolidColorBrush(Color.FromRgb(254, 249, 195)); // #FEF9C3
                    card.BorderBrush = new SolidColorBrush(Color.FromRgb(250, 204, 21)); // #FACC15
                    break;
                case "Inactive":
                    card.Background = new SolidColorBrush(Color.FromRgb(255, 237, 213)); // #FFEDD5
                    card.BorderBrush = new SolidColorBrush(Color.FromRgb(251, 146, 60)); // #FB923C
                    break;
                case "NewClient":
                    card.Background = new SolidColorBrush(Color.FromRgb(219, 234, 254)); // #DBEAFE
                    card.BorderBrush = new SolidColorBrush(Color.FromRgb(96, 165, 250)); // #60A5FA
                    break;
            }

            var grid = CreateCardColumnDefinitions();

            int col = 0;

            // Status dot
            var statusColor = GetAlertColor(odbiorca.AlertType);
            var dot = new Border
            {
                Width = 8, Height = 8,
                CornerRadius = new CornerRadius(4),
                Background = statusColor,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                ToolTip = GetAlertTooltip(odbiorca.AlertType)
            };
            Grid.SetColumn(dot, col++);
            grid.Children.Add(dot);

            // Company name + city
            var nameStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            nameStack.Children.Add(new TextBlock
            {
                Text = string.IsNullOrEmpty(odbiorca.Skrot) ? odbiorca.NazwaFirmy : odbiorca.Skrot,
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 180,
                ToolTip = odbiorca.NazwaFirmy
            });
            var addressParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(odbiorca.KodPocztowy)) addressParts.Add(odbiorca.KodPocztowy);
            if (!string.IsNullOrWhiteSpace(odbiorca.Miasto)) addressParts.Add(odbiorca.Miasto);
            if (!string.IsNullOrWhiteSpace(odbiorca.Ulica)) addressParts.Add(odbiorca.Ulica);
            var addressText = addressParts.Count > 0 ? "  " + string.Join(" ", addressParts) : "";
            nameStack.Children.Add(new TextBlock
            {
                Text = addressText,
                Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128)),
                FontSize = 10,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            Grid.SetColumn(nameStack, col++);
            grid.Children.Add(nameStack);

            // NIP
            var nipText = new TextBlock
            {
                Text = odbiorca.NIP ?? "",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128)),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(nipText, col++);
            grid.Children.Add(nipText);

            // Kontakt
            var kontaktText = new TextBlock
            {
                Text = odbiorca.OsobaKontaktowa ?? "",
                Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128)),
                FontSize = 10,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(kontaktText, col++);
            grid.Children.Add(kontaktText);

            // Telefon
            var telText = new TextBlock
            {
                Text = odbiorca.TelefonKontakt ?? "",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128)),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(telText, col++);
            grid.Children.Add(telText);

            // Forma płatności
            var formaText = new TextBlock
            {
                Text = odbiorca.FormaPlatnosci ?? "",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128)),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(formaText, col++);
            grid.Children.Add(formaText);

            // Termin płatności
            var terminText = new TextBlock
            {
                Text = odbiorca.TerminPlatnosci > 0 ? $"{odbiorca.TerminPlatnosci}d" : "",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128)),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetColumn(terminText, col++);
            grid.Children.Add(terminText);

            // Należności (WykorzystanoLimit)
            var nalezText = new TextBlock
            {
                Text = odbiorca.WykorzystanoLimit > 0 ? $"{odbiorca.WykorzystanoLimit:N0} zł" : "-",
                FontSize = 10,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 8, 0),
                Foreground = odbiorca.WykorzystanoLimit > 0
                    ? new SolidColorBrush(Color.FromRgb(180, 83, 9))     // amber-700
                    : new SolidColorBrush(Color.FromRgb(156, 163, 175))  // gray
            };
            Grid.SetColumn(nalezText, col++);
            grid.Children.Add(nalezText);

            // Limit
            var limitText = new TextBlock
            {
                Text = odbiorca.LimitKupiecki > 0 ? $"{odbiorca.LimitKupiecki:N0} zł" : "-",
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 8, 0)
            };
            Grid.SetColumn(limitText, col++);
            grid.Children.Add(limitText);

            // Bilans
            var bilansText = new TextBlock
            {
                Text = $"{odbiorca.Bilans:N0} zł",
                FontWeight = FontWeights.SemiBold,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 8, 0),
                Foreground = odbiorca.Bilans >= 0
                    ? new SolidColorBrush(Color.FromRgb(22, 163, 74))    // green
                    : new SolidColorBrush(Color.FromRgb(220, 38, 38))    // red
            };
            Grid.SetColumn(bilansText, col++);
            grid.Children.Add(bilansText);

            // Przeterminowane
            var przeterText = new TextBlock
            {
                Text = odbiorca.KwotaPrzeterminowana > 0 ? $"{odbiorca.KwotaPrzeterminowana:N0} zł" : "-",
                FontSize = 10,
                FontWeight = odbiorca.KwotaPrzeterminowana > 0 ? FontWeights.SemiBold : FontWeights.Normal,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 8, 0),
                Foreground = odbiorca.KwotaPrzeterminowana > 0
                    ? new SolidColorBrush(Color.FromRgb(220, 38, 38))    // red
                    : new SolidColorBrush(Color.FromRgb(156, 163, 175))  // gray
            };
            Grid.SetColumn(przeterText, col++);
            grid.Children.Add(przeterText);

            // % limitu
            var procentText = new TextBlock
            {
                Text = odbiorca.LimitKupiecki > 0 ? $"{odbiorca.ProcentWykorzystania:N0}%" : "",
                FontSize = 10,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 8, 0),
                Foreground = odbiorca.ProcentWykorzystania > 100
                    ? new SolidColorBrush(Color.FromRgb(220, 38, 38))
                    : odbiorca.ProcentWykorzystania > 80
                        ? new SolidColorBrush(Color.FromRgb(234, 179, 8))
                        : new SolidColorBrush(Color.FromRgb(107, 114, 128))
            };
            Grid.SetColumn(procentText, col++);
            grid.Children.Add(procentText);

            // Last invoice date
            var fakturaText = new TextBlock
            {
                Text = odbiorca.OstatniaFakturaData.HasValue ? odbiorca.OstatniaFakturaData.Value.ToString("dd.MM.yy") : "",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128)),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetColumn(fakturaText, col++);
            grid.Children.Add(fakturaText);

            // Kategoria badge
            var katBorder = new Border
            {
                Width = 24, Height = 24, CornerRadius = new CornerRadius(12),
                Background = GetKategoriaBackground(odbiorca.KategoriaHandlowca),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            katBorder.Child = new TextBlock
            {
                Text = odbiorca.KategoriaHandlowca ?? "C",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Bold,
                FontSize = 10,
                Foreground = GetKategoriaForeground(odbiorca.KategoriaHandlowca)
            };
            Grid.SetColumn(katBorder, col++);
            grid.Children.Add(katBorder);

            // Handlowiec (admin only)
            if (_isAdmin)
            {
                var handlText = new TextBlock
                {
                    Text = odbiorca.Handlowiec ?? "",
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128)),
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                Grid.SetColumn(handlText, col++);
                grid.Children.Add(handlText);
            }

            // Arrow
            var arrow = new TextBlock
            {
                Text = "▼",
                FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175)),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetColumn(arrow, col);
            grid.Children.Add(arrow);

            card.Child = grid;

            // Click to expand/collapse
            card.MouseLeftButtonDown += Card_Click;

            // Hover effect
            var defaultBg = card.Background;
            card.MouseEnter += (s, ev) =>
            {
                if (card != _expandedCard)
                    card.Background = new SolidColorBrush(Color.FromRgb(240, 253, 244)); // #F0FDF4
            };
            card.MouseLeave += (s, ev) =>
            {
                if (card != _expandedCard)
                    card.Background = defaultBg;
            };

            // Context menu
            card.ContextMenu = new ContextMenu();
            var editItem = new MenuItem { Header = "📝 Edytuj dane", InputGestureText = "DblClick" };
            editItem.Click += (s, ev) => { _selectedOdbiorca = odbiorca; OtworzEdycje(); };
            var callItem = new MenuItem { Header = "📞 Zadzwoń" };
            callItem.Click += (s, ev) =>
            {
                if (!string.IsNullOrEmpty(odbiorca.TelefonKontakt))
                    try { Process.Start(new ProcessStartInfo { FileName = $"tel:{odbiorca.TelefonKontakt}", UseShellExecute = true }); } catch { }
            };
            var emailItem = new MenuItem { Header = "📧 Wyślij email" };
            emailItem.Click += (s, ev) =>
            {
                if (!string.IsNullOrEmpty(odbiorca.EmailKontakt))
                    try { Process.Start(new ProcessStartInfo { FileName = $"mailto:{odbiorca.EmailKontakt}", UseShellExecute = true }); } catch { }
            };
            var copyItem = new MenuItem { Header = "📋 Kopiuj dane", InputGestureText = "Ctrl+C" };
            copyItem.Click += (s, ev) =>
            {
                var text = $"{odbiorca.NazwaFirmy}\n{odbiorca.Ulica}\n{odbiorca.KodPocztowy} {odbiorca.Miasto}\nNIP: {odbiorca.NIP}\nTel: {odbiorca.TelefonKontakt}\nEmail: {odbiorca.EmailKontakt}";
                Clipboard.SetText(text);
            };
            card.ContextMenu.Items.Add(editItem);
            card.ContextMenu.Items.Add(callItem);
            card.ContextMenu.Items.Add(emailItem);
            card.ContextMenu.Items.Add(new Separator());
            card.ContextMenu.Items.Add(copyItem);

            return card;
        }

        private async void Card_Click(object sender, MouseButtonEventArgs e)
        {
            var card = sender as Border;
            if (card == null) return;
            var odbiorca = card.Tag as OdbiorcaHandlowca;
            if (odbiorca == null) return;

            // Double-click = edit
            if (e.ClickCount == 2)
            {
                _selectedOdbiorca = odbiorca;
                OtworzEdycje();
                return;
            }

            // If clicking same card, collapse
            if (_expandedCard == card)
            {
                CollapseCard();
                return;
            }

            ExpandCard(card, odbiorca);
            await LoadSzczegoly(odbiorca);
        }

        private void ExpandCard(Border card, OdbiorcaHandlowca odbiorca)
        {
            // Collapse previous
            if (_expandedCard != null)
                UpdateCardVisual(_expandedCard, false);

            // Move DetailPanel after this card
            AccordionPanel.Children.Remove(DetailPanel);
            int idx = AccordionPanel.Children.IndexOf(card);
            if (idx >= 0)
                AccordionPanel.Children.Insert(idx + 1, DetailPanel);
            else
                AccordionPanel.Children.Add(DetailPanel);

            DetailPanel.Visibility = Visibility.Visible;

            UpdateCardVisual(card, true);
            _expandedCard = card;
            _selectedOdbiorca = odbiorca;
        }

        private void CollapseCard()
        {
            DetailPanel.Visibility = Visibility.Collapsed;
            if (_expandedCard != null)
                UpdateCardVisual(_expandedCard, false);
            _expandedCard = null;
            _selectedOdbiorca = null;
        }

        private void UpdateCardVisual(Border card, bool expanded)
        {
            // Find arrow TextBlock in the card
            if (card.Child is Grid grid)
            {
                foreach (var child in grid.Children)
                {
                    if (child is TextBlock tb && (tb.Text == "▼" || tb.Text == "▲"))
                    {
                        tb.Text = expanded ? "▲" : "▼";
                        tb.Foreground = expanded
                            ? new SolidColorBrush(Color.FromRgb(22, 163, 74))
                            : new SolidColorBrush(Color.FromRgb(156, 163, 175));
                    }
                }
            }

            if (expanded)
            {
                card.BorderBrush = new SolidColorBrush(Color.FromRgb(22, 163, 74)); // #16A34A
                card.BorderThickness = new Thickness(2);
            }
            else
            {
                // Restore default border based on alert
                var odbiorca = card.Tag as OdbiorcaHandlowca;
                if (odbiorca != null)
                {
                    switch (odbiorca.AlertType)
                    {
                        case "LimitExceeded":
                            card.BorderBrush = new SolidColorBrush(Color.FromRgb(248, 113, 113));
                            break;
                        case "Overdue":
                            card.BorderBrush = new SolidColorBrush(Color.FromRgb(250, 204, 21));
                            break;
                        case "Inactive":
                            card.BorderBrush = new SolidColorBrush(Color.FromRgb(251, 146, 60));
                            break;
                        case "NewClient":
                            card.BorderBrush = new SolidColorBrush(Color.FromRgb(96, 165, 250));
                            break;
                        default:
                            card.BorderBrush = new SolidColorBrush(Color.FromRgb(229, 231, 235));
                            break;
                    }
                }
                card.BorderThickness = new Thickness(1);
            }
        }

        // ═══════════════════════════════════════════
        // Helper methods for card styling
        // ═══════════════════════════════════════════

        private SolidColorBrush GetAlertColor(string alertType)
        {
            return alertType switch
            {
                "LimitExceeded" => new SolidColorBrush(Color.FromRgb(220, 38, 38)),   // red
                "Overdue" => new SolidColorBrush(Color.FromRgb(245, 158, 11)),         // amber
                "Inactive" => new SolidColorBrush(Color.FromRgb(251, 146, 60)),        // orange
                "NewClient" => new SolidColorBrush(Color.FromRgb(59, 130, 246)),       // blue
                _ => new SolidColorBrush(Color.FromRgb(34, 197, 94))                   // green (OK)
            };
        }

        private string GetAlertTooltip(string alertType)
        {
            return alertType switch
            {
                "LimitExceeded" => "Przekroczony limit kupiecki",
                "Overdue" => "Przeterminowane płatności",
                "Inactive" => "Nieaktywny ponad 30 dni",
                "NewClient" => "Nowy klient",
                _ => "OK"
            };
        }

        private SolidColorBrush GetKategoriaBackground(string kat)
        {
            return kat switch
            {
                "A" => new SolidColorBrush(Color.FromRgb(220, 252, 231)), // green-100
                "B" => new SolidColorBrush(Color.FromRgb(219, 234, 254)), // blue-100
                "D" => new SolidColorBrush(Color.FromRgb(254, 226, 226)), // red-100
                _ => new SolidColorBrush(Color.FromRgb(243, 244, 246))    // gray-100 (C)
            };
        }

        private SolidColorBrush GetKategoriaForeground(string kat)
        {
            return kat switch
            {
                "A" => new SolidColorBrush(Color.FromRgb(22, 101, 52)),   // green-800
                "B" => new SolidColorBrush(Color.FromRgb(30, 64, 175)),   // blue-800
                "D" => new SolidColorBrush(Color.FromRgb(153, 27, 27)),   // red-800
                _ => new SolidColorBrush(Color.FromRgb(55, 65, 81))       // gray-700 (C)
            };
        }

        // ═══════════════════════════════════════════
        // FILTRY
        // ═══════════════════════════════════════════

        private void ComboBoxTowar_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded) ApplyFilters();
        }

        private void ApplyFilters()
        {
            if (_allOdbiorcy == null) return;

            var filtered = _allOdbiorcy.AsEnumerable();

            // Filtr tekstu
            var szukaj = TextBoxSzukaj.Text?.ToLower()?.Trim();
            if (!string.IsNullOrEmpty(szukaj))
            {
                filtered = filtered.Where(o =>
                    (o.NazwaFirmy?.ToLower().Contains(szukaj) ?? false) ||
                    (o.Skrot?.ToLower().Contains(szukaj) ?? false) ||
                    (o.Miasto?.ToLower().Contains(szukaj) ?? false) ||
                    (o.NIP?.Contains(szukaj) ?? false) ||
                    (o.OsobaKontaktowa?.ToLower().Contains(szukaj) ?? false) ||
                    (o.TelefonKontakt?.Contains(szukaj) ?? false));
            }

            // Filtr kategorii
            if (ComboBoxKategoria.SelectedIndex > 0)
            {
                var kat = (ComboBoxKategoria.SelectedItem as ComboBoxItem)?.Content?.ToString();
                if (!string.IsNullOrEmpty(kat))
                    filtered = filtered.Where(o => o.KategoriaHandlowca == kat);
            }

            // Filtr towaru
            var selectedTowar = ComboBoxTowar.SelectedItem as ComboBoxItem;
            var towarKod = selectedTowar?.Tag?.ToString();
            if (!string.IsNullOrEmpty(towarKod))
            {
                filtered = filtered.Where(o =>
                    !string.IsNullOrEmpty(o.Asortyment) && o.Asortyment.Contains(towarKod));
            }

            // Filtr alertów
            if (CheckBoxAlerty.IsChecked == true)
            {
                filtered = filtered.Where(o => o.AlertType != "None");
            }

            _displayedOdbiorcy = filtered.ToList();
            ApplySortAndRegenerate();
            UpdateLicznik();
            UpdateStatystyki();
        }

        private void UpdateStatystyki()
        {
            var list = _displayedOdbiorcy;
            if (list == null) return;

            TextStopkaSumaLimitow.Text = list.Sum(o => o.LimitKupiecki).ToString("N0");
            TextStopkaWykorzystano.Text = list.Sum(o => o.WykorzystanoLimit).ToString("N0");
            TextStopkaWolne.Text = list.Sum(o => o.WolnyLimit).ToString("N0");
            TextStopkaPrzeterminowane.Text = list.Sum(o => o.KwotaPrzeterminowana).ToString("N0");
        }

        private void UpdateLicznik()
        {
            var count = _displayedOdbiorcy?.Count ?? 0;
            TextBlockLicznik.Text = $"Wyświetlono: {count} z {_allOdbiorcy?.Count ?? 0} odbiorców";
        }

        // ═══════════════════════════════════════════
        // POWIADOMIENIA - braki danych
        // ═══════════════════════════════════════════

        private void UpdatePowiadomienia()
        {
            var list = _allOdbiorcy;
            if (list == null || list.Count == 0)
            {
                TextPowiadomieniaCount.Text = "";
                ButtonPowiadomienia.Background = new SolidColorBrush(Color.FromRgb(107, 114, 128));
                return;
            }

            int totalIssues = 0;
            foreach (var o in list)
            {
                if (string.IsNullOrWhiteSpace(o.OsobaKontaktowa)) totalIssues++;
                if (string.IsNullOrWhiteSpace(o.EmailKontakt)) totalIssues++;
            }

            TextPowiadomieniaCount.Text = totalIssues > 0 ? totalIssues.ToString() : "";
            ButtonPowiadomienia.Background = totalIssues > 0
                ? new SolidColorBrush(Color.FromRgb(245, 158, 11)) // amber
                : new SolidColorBrush(Color.FromRgb(34, 197, 94)); // green - all good
        }

        private void ButtonPowiadomienia_Click(object sender, RoutedEventArgs e)
        {
            BuildPowiadomieniaPanel();
            PanelPowiadomienia.Visibility = Visibility.Visible;
        }

        private void ButtonZamknijPowiadomienia_Click(object sender, RoutedEventArgs e)
        {
            PanelPowiadomienia.Visibility = Visibility.Collapsed;
        }

        private void PanelPowiadomienia_BackgroundClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.OriginalSource == PanelPowiadomienia)
                PanelPowiadomienia.Visibility = Visibility.Collapsed;
        }

        private void BuildPowiadomieniaPanel()
        {
            PowiadomieniaList.Children.Clear();
            var list = _allOdbiorcy;
            if (list == null) return;

            var brakKontaktu = list.Where(o => string.IsNullOrWhiteSpace(o.OsobaKontaktowa)).ToList();
            var brakEmaila = list.Where(o => string.IsNullOrWhiteSpace(o.EmailKontakt)).ToList();

            AddPowiadomienieGroup("👤 Brak osoby kontaktowej", "OsobaKontaktowa", brakKontaktu, "#DC2626", "#FEF2F2");
            AddPowiadomienieGroup("📧 Brak adresu email", "Email", brakEmaila, "#EA580C", "#FFF7ED");

            if (PowiadomieniaList.Children.Count == 0)
            {
                PowiadomieniaList.Children.Add(new TextBlock
                {
                    Text = "✅ Wszystkie dane uzupełnione!",
                    FontSize = 14,
                    Foreground = new SolidColorBrush(Color.FromRgb(22, 163, 74)),
                    FontWeight = FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 20, 0, 20)
                });
            }
        }

        private void AddPowiadomienieGroup(string title, string fieldKey, List<OdbiorcaHandlowca> items, string colorHex, string bgHex)
        {
            if (items.Count == 0) return;

            var color = (Color)ColorConverter.ConvertFromString(colorHex);
            var bgColor = (Color)ColorConverter.ConvertFromString(bgHex);

            var group = new Border
            {
                Background = new SolidColorBrush(bgColor),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 0, 0, 8),
                Padding = new Thickness(12, 8, 12, 8)
            };

            var stack = new StackPanel();

            // Header with count
            var headerPanel = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
            headerPanel.Children.Add(new TextBlock
            {
                Text = title,
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                Foreground = new SolidColorBrush(color)
            });
            var badge = new Border
            {
                Background = new SolidColorBrush(color),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(7, 1, 7, 1),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            badge.Child = new TextBlock
            {
                Text = items.Count.ToString(),
                Foreground = Brushes.White,
                FontSize = 10,
                FontWeight = FontWeights.Bold
            };
            DockPanel.SetDock(badge, Dock.Right);
            headerPanel.Children.Insert(0, badge);
            stack.Children.Add(headerPanel);

            // List of customers (max 8, then "i X więcej...")
            var shown = items.Take(8).ToList();
            foreach (var o in shown)
            {
                var nameText = string.IsNullOrEmpty(o.Skrot) ? o.NazwaFirmy : o.Skrot;
                var link = new TextBlock
                {
                    Text = $"  • {nameText}",
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(55, 65, 81)),
                    Margin = new Thickness(0, 1, 0, 1),
                    Cursor = Cursors.Hand,
                    TextDecorations = null
                };
                var capturedOdbiorca = o;
                var capturedFieldKey = fieldKey;
                link.MouseEnter += (s, ev) => link.TextDecorations = TextDecorations.Underline;
                link.MouseLeave += (s, ev) => link.TextDecorations = null;
                link.MouseLeftButtonDown += (s, ev) =>
                {
                    PanelPowiadomienia.Visibility = Visibility.Collapsed;
                    OpenEdycjaWithHighlight(capturedOdbiorca, capturedFieldKey);
                };
                stack.Children.Add(link);
            }

            if (items.Count > 8)
            {
                stack.Children.Add(new TextBlock
                {
                    Text = $"  ... i {items.Count - 8} więcej",
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175)),
                    FontStyle = FontStyles.Italic,
                    Margin = new Thickness(0, 2, 0, 0)
                });
            }

            group.Child = stack;
            PowiadomieniaList.Children.Add(group);
        }

        private void OpenEdycjaWithHighlight(OdbiorcaHandlowca odbiorca, string clickedFieldKey)
        {
            // Determine all missing fields for this customer
            var missingFields = new List<string>();
            if (string.IsNullOrWhiteSpace(odbiorca.OsobaKontaktowa)) missingFields.Add("OsobaKontaktowa");
            if (string.IsNullOrWhiteSpace(odbiorca.EmailKontakt)) missingFields.Add("Email");

            var edycja = new OdbiorcaEdycjaWindow(odbiorca, _service, _userName, missingFields);
            edycja.Owner = this;
            if (edycja.ShowDialog() == true)
            {
                UpdatePowiadomienia();
                BuildPowiadomieniaPanel();
                _ = LoadData();
            }
        }

        private async void ButtonExportArkusz_Click(object sender, RoutedEventArgs e)
        {
            await ExportArkuszDoUzupelnienia();
        }

        private async System.Threading.Tasks.Task ExportArkuszDoUzupelnienia()
        {
            var items = _allOdbiorcy;
            if (items == null || !items.Any())
            {
                MessageBox.Show("Brak danych do eksportu.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var saveDialog = new SaveFileDialog
            {
                Filter = "Excel (*.xlsx)|*.xlsx",
                FileName = $"Braki_danych_odbiorcy_{DateTime.Now:yyyy-MM-dd}.xlsx"
            };

            if (saveDialog.ShowDialog() != true) return;

            try
            {
                // Load all contacts in one batch
                var allKontakty = await _service.PobierzWszystkieKontaktyAsync();

                var contactTypes = new[] { "Zakupowiec", "Księgowość", "Opakowania" };

                using var workbook = new XLWorkbook();
                var ws = workbook.Worksheets.Add("Do uzupełnienia");

                // ── Page setup for printing ──
                ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
                ws.PageSetup.PaperSize = XLPaperSize.A4Paper;
                ws.PageSetup.Margins.Left = 0.4;
                ws.PageSetup.Margins.Right = 0.4;
                ws.PageSetup.Margins.Top = 0.5;
                ws.PageSetup.Margins.Bottom = 0.3;
                ws.PageSetup.FitToPages(1, 0); // fit width to 1 page

                // ── Column widths ──
                ws.Column(1).Width = 5;    // Lp.
                ws.Column(2).Width = 28;   // Firma
                ws.Column(3).Width = 14;   // NIP
                ws.Column(4).Width = 14;   // Typ kontaktu
                ws.Column(5).Width = 22;   // Imię i Nazwisko
                ws.Column(6).Width = 16;   // Telefon
                ws.Column(7).Width = 28;   // Email
                ws.Column(8).Width = 24;   // Email firmowy (główny)

                // ── Title ──
                ws.Cell(1, 1).Value = $"BRAKI DANYCH DO UZUPEŁNIENIA — {_userName} — {DateTime.Now:dd.MM.yyyy}";
                ws.Range(1, 1, 1, 8).Merge();
                ws.Cell(1, 1).Style.Font.Bold = true;
                ws.Cell(1, 1).Style.Font.FontSize = 14;
                ws.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                ws.Cell(2, 1).Value = "Uzupełnij puste pola. Już wypełnione dane zostaw bez zmian.";
                ws.Range(2, 1, 2, 8).Merge();
                ws.Cell(2, 1).Style.Font.Italic = true;
                ws.Cell(2, 1).Style.Font.FontColor = XLColor.FromHtml("#6B7280");
                ws.Cell(2, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // ── Headers ──
                int row = 4;
                var headers = new[] { "Lp.", "Firma", "NIP", "Typ kontaktu", "Imię i Nazwisko", "Telefon", "Email kontaktu", "Email firmowy" };
                for (int i = 0; i < headers.Length; i++)
                    ws.Cell(row, i + 1).Value = headers[i];

                var headerRange = ws.Range(row, 1, row, headers.Length);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Font.FontColor = XLColor.White;
                headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#166534");
                headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                row++;

                // ── Data rows ──
                int lp = 0;
                foreach (var o in items.OrderBy(x => x.Skrot ?? x.NazwaFirmy))
                {
                    // Check what's missing
                    bool brakEmail = string.IsNullOrWhiteSpace(o.EmailKontakt);
                    bool brakOsoby = string.IsNullOrWhiteSpace(o.OsobaKontaktowa);

                    // Get existing contacts
                    allKontakty.TryGetValue(o.IdSymfonia, out var kontakty);
                    kontakty ??= new List<KontaktOdbiorcy>();

                    // Check which contact types are missing
                    var missingTypes = new List<string>();
                    foreach (var ct in contactTypes)
                    {
                        var matchType = ct == "Zakupowiec" ? "Główny" : ct;
                        if (!kontakty.Any(k => k.TypKontaktu == matchType && !string.IsNullOrWhiteSpace(k.PelneNazwisko)))
                            missingTypes.Add(ct);
                    }

                    // Skip if everything is filled
                    if (!brakEmail && !brakOsoby && missingTypes.Count == 0) continue;

                    lp++;
                    int startRow = row;
                    string firma = string.IsNullOrEmpty(o.Skrot) ? o.NazwaFirmy : o.Skrot;

                    // One row per contact type
                    foreach (var ct in contactTypes)
                    {
                        var matchType = ct == "Zakupowiec" ? "Główny" : ct;
                        var existing = kontakty.FirstOrDefault(k => k.TypKontaktu == matchType);

                        ws.Cell(row, 4).Value = ct;
                        ws.Cell(row, 4).Style.Font.Bold = true;
                        ws.Cell(row, 4).Style.Font.FontSize = 9;

                        if (existing != null && !string.IsNullOrWhiteSpace(existing.PelneNazwisko))
                        {
                            // Already filled — show in gray
                            ws.Cell(row, 5).Value = existing.PelneNazwisko;
                            ws.Cell(row, 5).Style.Font.FontColor = XLColor.FromHtml("#9CA3AF");
                            ws.Cell(row, 6).Value = existing.Telefon;
                            ws.Cell(row, 6).Style.Font.FontColor = XLColor.FromHtml("#9CA3AF");
                            ws.Cell(row, 7).Value = existing.Email;
                            ws.Cell(row, 7).Style.Font.FontColor = XLColor.FromHtml("#9CA3AF");
                        }
                        else
                        {
                            // Empty — highlight for filling
                            var emptyRange = ws.Range(row, 5, row, 7);
                            emptyRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#FEF2F2");
                            emptyRange.Style.Border.BottomBorder = XLBorderStyleValues.Dotted;
                            emptyRange.Style.Border.BottomBorderColor = XLColor.FromHtml("#FCA5A5");
                        }
                        row++;
                    }

                    // Merge Lp, Firma, NIP, Email firmowy across the 3 rows
                    int endRow = row - 1;

                    ws.Cell(startRow, 1).Value = lp;
                    ws.Range(startRow, 1, endRow, 1).Merge();
                    ws.Range(startRow, 1, endRow, 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    ws.Range(startRow, 1, endRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    ws.Cell(startRow, 2).Value = firma;
                    ws.Range(startRow, 2, endRow, 2).Merge();
                    ws.Range(startRow, 2, endRow, 2).Style.Font.Bold = true;
                    ws.Range(startRow, 2, endRow, 2).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                    ws.Cell(startRow, 3).Value = o.NIP;
                    ws.Range(startRow, 3, endRow, 3).Merge();
                    ws.Range(startRow, 3, endRow, 3).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    ws.Range(startRow, 3, endRow, 3).Style.Font.FontSize = 9;

                    // Email firmowy column
                    ws.Range(startRow, 8, endRow, 8).Merge();
                    ws.Range(startRow, 8, endRow, 8).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    if (brakEmail)
                    {
                        ws.Range(startRow, 8, endRow, 8).Style.Fill.BackgroundColor = XLColor.FromHtml("#FEF2F2");
                        ws.Range(startRow, 8, endRow, 8).Style.Border.BottomBorder = XLBorderStyleValues.Dotted;
                        ws.Range(startRow, 8, endRow, 8).Style.Border.BottomBorderColor = XLColor.FromHtml("#FCA5A5");
                    }
                    else
                    {
                        ws.Cell(startRow, 8).Value = o.EmailKontakt;
                        ws.Cell(startRow, 8).Style.Font.FontColor = XLColor.FromHtml("#9CA3AF");
                    }

                    // Borders around the customer block
                    var blockRange = ws.Range(startRow, 1, endRow, 8);
                    blockRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    blockRange.Style.Border.OutsideBorderColor = XLColor.FromHtml("#D1D5DB");
                    blockRange.Style.Border.InsideBorder = XLBorderStyleValues.Hair;
                    blockRange.Style.Border.InsideBorderColor = XLColor.FromHtml("#E5E7EB");

                    // Alternate row shading per customer
                    if (lp % 2 == 0)
                    {
                        for (int r = startRow; r <= endRow; r++)
                        {
                            for (int c = 1; c <= 4; c++)
                            {
                                var bgColor = ws.Cell(r, c).Style.Fill.BackgroundColor;
                                if (bgColor == XLColor.NoColor || bgColor.ToString() == "Color Theme: None, Tint: 0")
                                    ws.Cell(r, c).Style.Fill.BackgroundColor = XLColor.FromHtml("#F9FAFB");
                            }
                        }
                    }
                }

                // ── Summary at bottom ──
                row += 1;
                ws.Cell(row, 1).Value = $"Łącznie: {lp} odbiorców z brakami danych";
                ws.Range(row, 1, row, 8).Merge();
                ws.Cell(row, 1).Style.Font.Bold = true;
                ws.Cell(row, 1).Style.Font.FontColor = XLColor.FromHtml("#92400E");

                // ── Print settings ──
                ws.PageSetup.PrintAreas.Clear();
                ws.PageSetup.PrintAreas.Add(1, 1, row, 8);
                ws.SheetView.FreezeRows(4);

                workbook.SaveAs(saveDialog.FileName);

                MessageBox.Show($"Wyeksportowano arkusz z {lp} odbiorcami do uzupełnienia.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);

                // Open the file
                try { Process.Start(new ProcessStartInfo { FileName = saveDialog.FileName, UseShellExecute = true }); } catch { }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd eksportu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ═══════════════════════════════════════════
        // SZCZEGÓŁY - aktualizacja paneli
        // ═══════════════════════════════════════════

        private async System.Threading.Tasks.Task LoadSzczegoly(OdbiorcaHandlowca odbiorca)
        {
            // Kontakty
            try
            {
                var kontakty = await _service.PobierzKontaktyAsync(odbiorca.IdSymfonia);
                WypelnijKontakty(kontakty, odbiorca.IdSymfonia);
            }
            catch { }

            // Finanse + Historia
            try
            {
                var faktury = await _service.PobierzFakturyAsync(odbiorca.IdSymfonia);
                _currentFaktury = faktury;
                WypelnijFinanse(odbiorca, faktury);
                DataGridFaktury.ItemsSource = faktury.Take(10).ToList();
                ApplyHistoriaFilters();
                WypelnijPlatnosci(faktury);
            }
            catch { }

            // Asortyment - preferencje
            TextAsortymentPakowanie.Text = string.IsNullOrEmpty(odbiorca.PreferencjePakowania) ? "-" : odbiorca.PreferencjePakowania;
            TextAsortymentJakosc.Text = string.IsNullOrEmpty(odbiorca.PreferencjeJakosci) ? "-" : odbiorca.PreferencjeJakosci;
            TextAsortymentLista.Text = string.IsNullOrEmpty(odbiorca.Asortyment) ? "-" : odbiorca.Asortyment;
            TextAsortymentDni.Text = string.IsNullOrEmpty(odbiorca.PreferowanyDzienDostawy) ? "-" : odbiorca.PreferowanyDzienDostawy;
            TextAsortymentGodzina.Text = string.IsNullOrEmpty(odbiorca.PreferowanaGodzinaDostawy) ? "-" : odbiorca.PreferowanaGodzinaDostawy;
            TextAsortymentAdres.Text = string.IsNullOrEmpty(odbiorca.AdresDostawyInny) ? "= adres główny" : odbiorca.AdresDostawyInny;
            TextAsortymentTrasa.Text = string.IsNullOrEmpty(odbiorca.Trasa) ? "-" : odbiorca.Trasa;

            // Asortyment - szczegóły z historii sprzedaży
            try
            {
                var asortymentSzczegoly = await _service.PobierzAsortymentSzczegolyAsync(odbiorca.IdSymfonia, 12);
                DataGridAsortyment.ItemsSource = asortymentSzczegoly;

                var sumaKg = asortymentSzczegoly.Sum(a => a.SumaKg);
                var sumaWartosc = asortymentSzczegoly.Sum(a => a.SumaWartosc);
                TextAsortymentIloscProduktow.Text = $"{asortymentSzczegoly.Count} produktów";
                TextAsortymentSumaKg.Text = $"{sumaKg:N0} kg";
                TextAsortymentSumaWartosc.Text = $"{sumaWartosc:N0} zł";
                TextAsortymentSredniaCena.Text = sumaKg > 0
                    ? $"{(sumaWartosc / sumaKg):N2} zł/kg"
                    : "-";

                BuildStrukturaZakupow(asortymentSzczegoly);
            }
            catch
            {
                DataGridAsortyment.ItemsSource = null;
                PanelStrukturaZakupow.Children.Clear();
                TextAsortymentIloscProduktow.Text = "0 produktów";
                TextAsortymentSumaKg.Text = "0 kg";
                TextAsortymentSumaWartosc.Text = "0 zł";
                TextAsortymentSredniaCena.Text = "-";
            }

            // Notatki
            TextBoxNotatki.Text = "";
            TextNotatkaAutor.Text = $"Autor: {_userName}";
            TextNotatkaInfo.Text = odbiorca.DataModyfikacji.HasValue
                ? $"Ost. zmiana: {odbiorca.DataModyfikacji:dd.MM.yyyy}"
                : "";
            await LoadNotatki(odbiorca.IdSymfonia);

            // Dostawy - dane adresowe
            TextDostawyDni.Text = $"Dni: {(string.IsNullOrEmpty(odbiorca.PreferowanyDzienDostawy) ? "-" : odbiorca.PreferowanyDzienDostawy)}";
            TextDostawyGodzina.Text = $"Godzina: {(string.IsNullOrEmpty(odbiorca.PreferowanaGodzinaDostawy) ? "-" : odbiorca.PreferowanaGodzinaDostawy)}";
            TextDostawyAdres.Text = $"Adres: {(string.IsNullOrEmpty(odbiorca.AdresDostawyInny) ? "= adres główny" : odbiorca.AdresDostawyInny)}";
            TextDostawyTrasa.Text = $"Trasa: {(string.IsNullOrEmpty(odbiorca.Trasa) ? "-" : odbiorca.Trasa)}";
            TextDostawyFirma.Text = odbiorca.NazwaFirmy;
            TextDostawyUlica.Text = odbiorca.Ulica ?? "-";
            TextDostawyMiasto.Text = $"{odbiorca.KodPocztowy} {odbiorca.Miasto}";
            TextDostawyNIP.Text = $"NIP: {odbiorca.NIP ?? "-"}";

            // Dostawy - analityka z zamówień
            try
            {
                await LoadDostawyAnalityka(odbiorca);
            }
            catch
            {
                TextDostawyLiczba.Text = "-";
                TextDostawySumaKg.Text = "-";
                TextDostawySredniCzas.Text = "-";
                TextDostawyCzestotliwosc.Text = "-";
                TextDostawyAnulowane.Text = "-";
                PanelDostawyDni.Children.Clear();
                PanelDostawyCzas.Children.Clear();
                PanelDostawyGodziny.Children.Clear();
                PanelDostawyTransport.Children.Clear();
                PanelDostawyWspol.Children.Clear();
                DataGridDostawyZamowienia.ItemsSource = null;
            }
        }

        private async System.Threading.Tasks.Task LoadDostawyAnalityka(OdbiorcaHandlowca odbiorca)
        {
            var zamowienia = await _service.PobierzZamowieniaDostawAsync(odbiorca.IdSymfonia, 12);

            // DataGrid
            DataGridDostawyZamowienia.ItemsSource = zamowienia;

            if (zamowienia.Count == 0)
            {
                TextDostawyLiczba.Text = "0";
                TextDostawyLiczbaInfo.Text = "brak zamówień";
                TextDostawySumaKg.Text = "0 kg";
                TextDostawySumaKgInfo.Text = "";
                TextDostawySredniCzas.Text = "-";
                TextDostawySredniCzasInfo.Text = "";
                TextDostawyCzestotliwosc.Text = "-";
                TextDostawyCzestliwoscInfo.Text = "";
                TextDostawyAnulowane.Text = "0%";
                TextDostawyAnulowaneInfo.Text = "";
                PanelDostawyDni.Children.Clear();
                PanelDostawyCzas.Children.Clear();
                PanelDostawyGodziny.Children.Clear();
                PanelDostawyTransport.Children.Clear();
                PanelDostawyWspol.Children.Clear();
                TextAsortymentDostawaAnaliza.Text = "Brak danych z zamówień";
                return;
            }

            var zrealizowane = zamowienia.Where(z => z.Status == "Zrealizowane").ToList();
            var anulowane = zamowienia.Where(z => z.Status == "Anulowane").ToList();
            var nieAnulowane = zamowienia.Where(z => z.Status != "Anulowane").ToList();

            // ═══ KPI CARDS ═══
            TextDostawyLiczba.Text = zamowienia.Count.ToString();
            TextDostawyLiczbaInfo.Text = $"{zrealizowane.Count} zrealiz. / {anulowane.Count} anul.";

            var sumaKg = nieAnulowane.Sum(z => z.IloscKg);
            var sumaPalet = nieAnulowane.Sum(z => z.LiczbaPalet);
            var sumaPojemnikow = nieAnulowane.Sum(z => z.LiczbaPojemnikow);
            TextDostawySumaKg.Text = $"{sumaKg:N0} kg";
            TextDostawySumaKgInfo.Text = $"{sumaPalet:N0} palet / {sumaPojemnikow} pojemn.";

            // Czas produkcja → dostawa
            var zDniami = nieAnulowane.Where(z => z.DniDoDostawy.HasValue && z.DniDoDostawy >= 0).ToList();
            if (zDniami.Count > 0)
            {
                var sredniDni = zDniami.Average(z => z.DniDoDostawy!.Value);
                var minDni = zDniami.Min(z => z.DniDoDostawy!.Value);
                var maxDni = zDniami.Max(z => z.DniDoDostawy!.Value);
                TextDostawySredniCzas.Text = $"{sredniDni:N1} dni";
                TextDostawySredniCzasInfo.Text = $"min {minDni}d / max {maxDni}d";
            }
            else
            {
                TextDostawySredniCzas.Text = "-";
                TextDostawySredniCzasInfo.Text = "brak danych awizacji";
            }

            // Częstotliwość
            var daty = nieAnulowane.Select(z => z.DataUboju.Date).Distinct().OrderBy(d => d).ToList();
            if (daty.Count >= 2)
            {
                var odstepy = new List<int>();
                for (int i = 1; i < daty.Count; i++)
                    odstepy.Add((int)(daty[i] - daty[i - 1]).TotalDays);
                var sredniOdstep = odstepy.Average();
                TextDostawyCzestotliwosc.Text = $"co {sredniOdstep:N0} dni";
                var zamNaMiesiac = 30.0 / Math.Max(sredniOdstep, 1);
                TextDostawyCzestliwoscInfo.Text = $"~{zamNaMiesiac:N1} zam./msc";
            }
            else
            {
                TextDostawyCzestotliwosc.Text = daty.Count == 1 ? "1 zam." : "-";
                TextDostawyCzestliwoscInfo.Text = "";
            }

            // % anulowanych
            var procentAnul = zamowienia.Count > 0 ? (double)anulowane.Count / zamowienia.Count * 100 : 0;
            TextDostawyAnulowane.Text = $"{procentAnul:N0}%";
            TextDostawyAnulowaneInfo.Text = $"{anulowane.Count} z {zamowienia.Count}";

            // ═══ PANEL: Preferowane dni (% z zamówień z awizacją) ═══
            PanelDostawyDni.Children.Clear();
            var zAwizacja = nieAnulowane.Where(z => z.DataPrzyjazdu.HasValue).ToList();
            if (zAwizacja.Count > 0)
            {
                var dnMap = new[] {
                    ("Pon", DayOfWeek.Monday), ("Wto", DayOfWeek.Tuesday), ("Śro", DayOfWeek.Wednesday),
                    ("Czw", DayOfWeek.Thursday), ("Pią", DayOfWeek.Friday), ("Sob", DayOfWeek.Saturday)
                };
                int maxCount = 1;
                var countPerDay = new Dictionary<DayOfWeek, int>();
                foreach (var d in dnMap)
                {
                    var cnt = zAwizacja.Count(z => z.DzienDostawy == d.Item2);
                    countPerDay[d.Item2] = cnt;
                    if (cnt > maxCount) maxCount = cnt;
                }

                foreach (var d in dnMap)
                {
                    var cnt = countPerDay[d.Item2];
                    var pct = (double)cnt / zAwizacja.Count * 100;
                    var barWidth = Math.Max((double)cnt / maxCount * 100, 0);

                    var row = new Grid { Margin = new Thickness(0, 0, 0, 3) };
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(55) });

                    var label = new TextBlock { Text = d.Item1, FontSize = 10, VerticalAlignment = VerticalAlignment.Center, Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128)) };
                    Grid.SetColumn(label, 0);

                    var barBorder = new Border
                    {
                        Height = 14, CornerRadius = new CornerRadius(3),
                        Background = new SolidColorBrush(Color.FromRgb(220, 252, 231)),
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Width = barWidth > 0 ? barWidth * 1.2 : 0,
                        Margin = new Thickness(0, 0, 4, 0)
                    };
                    if (cnt > 0)
                        barBorder.Child = new TextBlock
                        {
                            Text = cnt.ToString(), FontSize = 8, FontWeight = FontWeights.Bold,
                            Foreground = new SolidColorBrush(Color.FromRgb(22, 101, 52)),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                    Grid.SetColumn(barBorder, 1);

                    var pctText = new TextBlock
                    {
                        Text = cnt > 0 ? $"{pct:N0}%" : "",
                        FontSize = 10, FontWeight = cnt > 0 ? FontWeights.SemiBold : FontWeights.Normal,
                        Foreground = new SolidColorBrush(Color.FromRgb(22, 163, 74)),
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetColumn(pctText, 2);

                    row.Children.Add(label);
                    row.Children.Add(barBorder);
                    row.Children.Add(pctText);
                    PanelDostawyDni.Children.Add(row);
                }
            }
            else
            {
                PanelDostawyDni.Children.Add(new TextBlock { Text = "Brak danych awizacji", Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175)), FontSize = 10 });
            }

            // ═══ PANEL: Czas produkcja → dostawa (histogram) ═══
            PanelDostawyCzas.Children.Clear();
            if (zDniami.Count > 0)
            {
                var grupy = new[] { ("0 dni (D+0)", 0, 0), ("1 dzień (D+1)", 1, 1), ("2 dni (D+2)", 2, 2), ("3+ dni", 3, 99) };
                int maxGrp = 1;
                var countPerGrp = new List<int>();
                foreach (var g in grupy)
                {
                    var cnt = zDniami.Count(z => z.DniDoDostawy >= g.Item2 && z.DniDoDostawy <= g.Item3);
                    countPerGrp.Add(cnt);
                    if (cnt > maxGrp) maxGrp = cnt;
                }

                for (int i = 0; i < grupy.Length; i++)
                {
                    var cnt = countPerGrp[i];
                    var pct = (double)cnt / zDniami.Count * 100;
                    var barWidth = Math.Max((double)cnt / maxGrp * 100, 0);

                    var row = new Grid { Margin = new Thickness(0, 0, 0, 3) };
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(55) });

                    var label = new TextBlock { Text = grupy[i].Item1, FontSize = 10, VerticalAlignment = VerticalAlignment.Center, Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128)) };
                    Grid.SetColumn(label, 0);

                    var barColor = i == 0 ? Color.FromRgb(187, 247, 208)   // green - same day
                                : i == 1 ? Color.FromRgb(191, 219, 254)    // blue - D+1
                                : i == 2 ? Color.FromRgb(254, 249, 195)    // yellow - D+2
                                : Color.FromRgb(254, 202, 202);            // red - D+3+
                    var barBorder = new Border
                    {
                        Height = 14, CornerRadius = new CornerRadius(3),
                        Background = new SolidColorBrush(barColor),
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Width = barWidth > 0 ? barWidth * 0.8 : 0,
                        Margin = new Thickness(0, 0, 4, 0)
                    };
                    if (cnt > 0)
                        barBorder.Child = new TextBlock
                        {
                            Text = cnt.ToString(), FontSize = 8, FontWeight = FontWeights.Bold,
                            Foreground = new SolidColorBrush(Color.FromRgb(55, 65, 81)),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                    Grid.SetColumn(barBorder, 1);

                    var pctText = new TextBlock
                    {
                        Text = cnt > 0 ? $"{pct:N0}%" : "",
                        FontSize = 10, FontWeight = cnt > 0 ? FontWeights.SemiBold : FontWeights.Normal,
                        Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128)),
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetColumn(pctText, 2);

                    row.Children.Add(label);
                    row.Children.Add(barBorder);
                    row.Children.Add(pctText);
                    PanelDostawyCzas.Children.Add(row);
                }
            }
            else
            {
                PanelDostawyCzas.Children.Add(new TextBlock { Text = "Brak danych awizacji", Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175)), FontSize = 10 });
            }

            // ═══ ASORTYMENT TAB: podsumowanie dostawy ═══
            if (zAwizacja.Count > 0 && zDniami.Count > 0)
            {
                var topDni = zAwizacja
                    .GroupBy(z => z.DzienDostawy)
                    .OrderByDescending(g => g.Count())
                    .Take(2)
                    .Select(g => {
                        var dayNames = new Dictionary<DayOfWeek, string> {
                            {DayOfWeek.Monday,"Pon"},{DayOfWeek.Tuesday,"Wto"},{DayOfWeek.Wednesday,"Śro"},
                            {DayOfWeek.Thursday,"Czw"},{DayOfWeek.Friday,"Pią"},{DayOfWeek.Saturday,"Sob"},{DayOfWeek.Sunday,"Nie"}
                        };
                        var pct = (double)g.Count() / zAwizacja.Count * 100;
                        return $"{dayNames[g.Key!.Value]} ({pct:N0}%)";
                    });
                var sredni = zDniami.Average(z => z.DniDoDostawy!.Value);
                var d0pct = zDniami.Count > 0 ? (double)zDniami.Count(z => z.DniDoDostawy == 0) / zDniami.Count * 100 : 0;
                var d1pct = zDniami.Count > 0 ? (double)zDniami.Count(z => z.DniDoDostawy == 1) / zDniami.Count * 100 : 0;

                TextAsortymentDostawaAnaliza.Text = $"Dni: {string.Join(", ", topDni)}\n" +
                    $"Śr. czas: D+{sredni:N1} | D+0: {d0pct:N0}% | D+1: {d1pct:N0}%\n" +
                    $"Zam: {nieAnulowane.Count} ({sumaKg:N0} kg / 12m)";
            }
            else
            {
                TextAsortymentDostawaAnaliza.Text = nieAnulowane.Count > 0
                    ? $"Zamówień: {nieAnulowane.Count} (brak awizacji)"
                    : "Brak danych z zamówień";
            }

            // ═══ PANEL: Godziny awizacji ═══
            PanelDostawyGodziny.Children.Clear();
            var zGodzina = zAwizacja.Where(z => z.GodzinaAwizacji != null).ToList();
            if (zGodzina.Count > 0)
            {
                // Group by hour ranges
                var godzinoweGrupy = new[]
                {
                    ("Noc (0-5)", 0, 5), ("Rano (5-8)", 5, 8), ("Przedpołudnie (8-11)", 8, 11),
                    ("Południe (11-14)", 11, 14), ("Popołudnie (14-17)", 14, 17), ("Wieczór (17-22)", 17, 22)
                };

                int maxGodz = 1;
                var countPerGodz = new List<int>();
                foreach (var g in godzinoweGrupy)
                {
                    var cnt = zGodzina.Count(z => z.DataPrzyjazdu!.Value.Hour >= g.Item2 && z.DataPrzyjazdu!.Value.Hour < g.Item3);
                    countPerGodz.Add(cnt);
                    if (cnt > maxGodz) maxGodz = cnt;
                }

                for (int i = 0; i < godzinoweGrupy.Length; i++)
                {
                    var cnt = countPerGodz[i];
                    if (cnt == 0) continue;
                    var pct = (double)cnt / zGodzina.Count * 100;

                    var row = new Grid { Margin = new Thickness(0, 0, 0, 2) };
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(45) });

                    var label = new TextBlock { Text = godzinoweGrupy[i].Item1, FontSize = 9, VerticalAlignment = VerticalAlignment.Center, Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128)) };
                    Grid.SetColumn(label, 0);

                    var barBorder = new Border
                    {
                        Height = 12, CornerRadius = new CornerRadius(2),
                        Background = new SolidColorBrush(Color.FromRgb(191, 219, 254)),
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Width = (double)cnt / maxGodz * 80,
                        Margin = new Thickness(0, 0, 4, 0)
                    };
                    if (cnt > 0)
                        barBorder.Child = new TextBlock { Text = cnt.ToString(), FontSize = 7, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(30, 64, 175)), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                    Grid.SetColumn(barBorder, 1);

                    var pctText = new TextBlock { Text = $"{pct:N0}%", FontSize = 9, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(37, 99, 235)), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
                    Grid.SetColumn(pctText, 2);

                    row.Children.Add(label);
                    row.Children.Add(barBorder);
                    row.Children.Add(pctText);
                    PanelDostawyGodziny.Children.Add(row);
                }

                // Show most common exact hours
                var topGodziny = zGodzina
                    .GroupBy(z => z.DataPrzyjazdu!.Value.Hour)
                    .OrderByDescending(g => g.Count())
                    .Take(3)
                    .Select(g => $"{g.Key}:00 ({g.Count()}x)");
                PanelDostawyGodziny.Children.Add(new TextBlock
                {
                    Text = $"Najczęściej: {string.Join(", ", topGodziny)}",
                    FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
                    FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 6, 0, 0)
                });
            }
            else
            {
                PanelDostawyGodziny.Children.Add(new TextBlock { Text = "Brak danych godzinowych", Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175)), FontSize = 10 });
            }

            // ═══ PANEL: Transport (kierowcy, pojazdy, współklienci) ═══
            PanelDostawyTransport.Children.Clear();
            PanelDostawyWspol.Children.Clear();
            try
            {
                var transport = await _service.PobierzTransportAnalizaAsync(odbiorca.IdSymfonia, 12);
                LoadTransportPanel(transport);
            }
            catch
            {
                PanelDostawyTransport.Children.Add(new TextBlock { Text = "Brak danych transportowych", Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175)), FontSize = 10 });
                PanelDostawyWspol.Children.Add(new TextBlock { Text = "Brak danych", Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175)), FontSize = 10 });
            }
        }

        private void LoadTransportPanel(TransportAnaliza transport)
        {
            PanelDostawyTransport.Children.Clear();
            PanelDostawyWspol.Children.Clear();

            if (transport.LiczbaKursow == 0)
            {
                PanelDostawyTransport.Children.Add(new TextBlock { Text = "Brak przypisanych kursów", Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175)), FontSize = 10 });
                PanelDostawyWspol.Children.Add(new TextBlock { Text = "Brak danych", Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175)), FontSize = 10 });
                return;
            }

            PanelDostawyTransport.Children.Add(new TextBlock
            {
                Text = $"Łącznie {transport.LiczbaKursow} kursów (12m)",
                FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128)),
                Margin = new Thickness(0, 0, 0, 6)
            });

            // Kierowcy
            if (transport.Kierowcy.Count > 0)
            {
                PanelDostawyTransport.Children.Add(new TextBlock { Text = "Kierowcy:", FontSize = 9, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128)), Margin = new Thickness(0, 0, 0, 2) });
                foreach (var k in transport.Kierowcy)
                {
                    var pct = (double)k.LiczbaKursow / transport.LiczbaKursow * 100;
                    var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 1) };
                    row.Children.Add(new TextBlock { Text = $"👤 {k.Nazwa}", FontSize = 10, FontWeight = FontWeights.Medium });
                    row.Children.Add(new TextBlock { Text = $" — {k.LiczbaKursow}x ({pct:N0}%)", FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(22, 163, 74)), VerticalAlignment = VerticalAlignment.Center });
                    if (!string.IsNullOrEmpty(k.Telefon))
                        row.Children.Add(new TextBlock { Text = $" ☎ {k.Telefon}", FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175)), VerticalAlignment = VerticalAlignment.Center });
                    PanelDostawyTransport.Children.Add(row);
                }
            }

            // Pojazdy
            if (transport.Pojazdy.Count > 0)
            {
                PanelDostawyTransport.Children.Add(new TextBlock { Text = "Pojazdy:", FontSize = 9, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128)), Margin = new Thickness(0, 6, 0, 2) });
                foreach (var p in transport.Pojazdy)
                {
                    var pct = (double)p.LiczbaKursow / transport.LiczbaKursow * 100;
                    var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 1) };
                    row.Children.Add(new TextBlock { Text = $"🚛 {p.Nazwa}", FontSize = 10, FontWeight = FontWeights.Medium });
                    row.Children.Add(new TextBlock { Text = $" — {p.LiczbaKursow}x ({pct:N0}%)", FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(37, 99, 235)), VerticalAlignment = VerticalAlignment.Center });
                    PanelDostawyTransport.Children.Add(row);
                }
            }

            // Trasy
            if (transport.Trasy.Count > 0)
            {
                PanelDostawyTransport.Children.Add(new TextBlock { Text = "Trasy:", FontSize = 9, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128)), Margin = new Thickness(0, 6, 0, 2) });
                foreach (var t in transport.Trasy)
                {
                    var pct = (double)t.LiczbaKursow / transport.LiczbaKursow * 100;
                    PanelDostawyTransport.Children.Add(new TextBlock { Text = $"📍 {t.Nazwa} — {t.LiczbaKursow}x ({pct:N0}%)", FontSize = 10, Margin = new Thickness(0, 0, 0, 1) });
                }
            }

            // Współtransportowani klienci
            if (transport.WspolKlienci.Count > 0)
            {
                foreach (var wk in transport.WspolKlienci)
                {
                    var pct = (double)wk.LiczbaWspolnychKursow / transport.LiczbaKursow * 100;
                    var row = new Grid { Margin = new Thickness(0, 0, 0, 2) };
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });

                    var nameText = new TextBlock { Text = wk.Nazwa, FontSize = 10, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, ToolTip = wk.Nazwa };
                    Grid.SetColumn(nameText, 0);

                    var countText = new TextBlock { Text = $"{wk.LiczbaWspolnychKursow}x ({pct:N0}%)", FontSize = 9, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(124, 58, 237)), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
                    Grid.SetColumn(countText, 1);

                    row.Children.Add(nameText);
                    row.Children.Add(countText);
                    PanelDostawyWspol.Children.Add(row);
                }
            }
            else
            {
                PanelDostawyWspol.Children.Add(new TextBlock { Text = "Brak współtransportowanych", Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175)), FontSize = 10 });
            }
        }

        private void BuildStrukturaZakupow(List<AsortymentPozycja> pozycje)
        {
            PanelStrukturaZakupow.Children.Clear();
            if (pozycje == null || pozycje.Count == 0) return;

            var totalKg = pozycje.Sum(p => p.SumaKg);
            if (totalKg <= 0) return;

            var top = pozycje.OrderByDescending(p => p.SumaKg).Take(8).ToList();

            foreach (var p in top)
            {
                var procentVal = (double)(p.SumaKg / totalKg * 100);
                var barWidth = Math.Max(procentVal, 2);

                var item = new StackPanel { Margin = new Thickness(0, 0, 0, 6) };

                var header = new DockPanel();
                var nameText = new TextBlock
                {
                    Text = string.IsNullOrEmpty(p.ProduktNazwa) ? p.ProduktKod : p.ProduktNazwa,
                    FontSize = 11,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = 150
                };
                var pctText = new TextBlock
                {
                    Text = $"{procentVal:F0}%",
                    HorizontalAlignment = HorizontalAlignment.Right,
                    FontWeight = FontWeights.Medium,
                    FontSize = 11
                };
                DockPanel.SetDock(pctText, Dock.Right);
                header.Children.Add(pctText);
                header.Children.Add(nameText);
                item.Children.Add(header);

                var barGrid = new Grid { Height = 6, Margin = new Thickness(0, 2, 0, 0) };
                barGrid.Children.Add(new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(229, 231, 235)),
                    CornerRadius = new CornerRadius(3)
                });
                barGrid.Children.Add(new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(34, 197, 94)),
                    CornerRadius = new CornerRadius(3),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Width = barWidth * 1.5
                });
                item.Children.Add(barGrid);

                PanelStrukturaZakupow.Children.Add(item);
            }
        }

        // ═══════════════════════════════════════════
        // HISTORIA - Filtry
        // ═══════════════════════════════════════════

        private void ComboHistoriaOkres_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded) ApplyHistoriaFilters();
        }

        private void ComboHistoriaTyp_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded) ApplyHistoriaFilters();
        }

        private void ApplyHistoriaFilters()
        {
            if (_currentFaktury == null || DataGridHistoria == null) return;

            var filtered = _currentFaktury.Where(f => !f.Anulowany).AsEnumerable();

            if (ComboHistoriaOkres?.SelectedIndex >= 0)
            {
                var teraz = DateTime.Now;
                switch (ComboHistoriaOkres.SelectedIndex)
                {
                    case 0: filtered = filtered.Where(f => f.DataFaktury >= teraz.AddDays(-30)); break;
                    case 1: filtered = filtered.Where(f => f.DataFaktury >= teraz.AddDays(-90)); break;
                    case 2: filtered = filtered.Where(f => f.DataFaktury >= teraz.AddYears(-1)); break;
                }
            }

            if (ComboHistoriaTyp?.SelectedIndex > 0)
            {
                var typText = (ComboHistoriaTyp.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
                var typCode = typText.Split(' ')[0];
                filtered = filtered.Where(f => f.Typ == typCode);
            }

            var result = filtered.ToList();
            DataGridHistoria.ItemsSource = result;

            if (TextHistoriaLiczba != null)
                TextHistoriaLiczba.Text = result.Count.ToString();
            if (TextHistoriaSuma != null)
                TextHistoriaSuma.Text = $"{result.Sum(f => f.Brutto):N0} zł";
        }

        // ═══════════════════════════════════════════
        // PŁATNOŚCI - tab
        // ═══════════════════════════════════════════

        private void WypelnijPlatnosci(List<FakturaOdbiorcy> faktury)
        {
            var nieoplacone = faktury.Where(f => !f.Anulowany && f.DoZaplaty > 0).ToList();
            TextPlatnosciDoZaplaty.Text = $"{nieoplacone.Sum(f => f.DoZaplaty):N0} zł";
            TextPlatnosciDoZaplatyCount.Text = $"{nieoplacone.Count} faktur";

            var przeterminowane = faktury.Where(f => !f.Anulowany && f.Przeterminowana).ToList();
            TextPlatnosciPrzeterminowane.Text = $"{przeterminowane.Sum(f => f.DoZaplaty):N0} zł";
            TextPlatnosciPrzeterminowaneCount.Text = $"{przeterminowane.Count} faktur";

            var zaplacone12m = faktury.Where(f => !f.Anulowany && f.DoZaplaty <= 0 && f.DataFaktury >= DateTime.Now.AddYears(-1)).ToList();
            TextPlatnosciZaplacono.Text = $"{zaplacone12m.Sum(f => f.Brutto):N0} zł";
            TextPlatnosciZaplaconoCount.Text = $"{zaplacone12m.Count} faktur";

            var zTerminem = zaplacone12m.Where(f => f.TerminPlatnosci > DateTime.MinValue).ToList();
            if (zTerminem.Count > 0)
            {
                var srednie = zTerminem.Average(f => (f.TerminPlatnosci - f.DataFaktury).TotalDays);
                TextPlatnosciSrednieDni.Text = $"{srednie:F0} dni";
            }
            else
            {
                TextPlatnosciSrednieDni.Text = "-";
            }

            ApplyPlatnosciFilters();
        }

        private void ComboPlatnosciOkres_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded) ApplyPlatnosciFilters();
        }

        private void ComboPlatnosciStatus_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded) ApplyPlatnosciFilters();
        }

        private void ApplyPlatnosciFilters()
        {
            if (_currentFaktury == null || DataGridPlatnosci == null) return;

            var filtered = _currentFaktury.AsEnumerable();

            if (ComboPlatnosciOkres?.SelectedIndex >= 0)
            {
                var teraz = DateTime.Now;
                switch (ComboPlatnosciOkres.SelectedIndex)
                {
                    case 0: filtered = filtered.Where(f => f.DataFaktury >= teraz.AddDays(-30)); break;
                    case 1: filtered = filtered.Where(f => f.DataFaktury >= teraz.AddDays(-90)); break;
                    case 2: filtered = filtered.Where(f => f.DataFaktury >= teraz.AddYears(-1)); break;
                }
            }

            if (ComboPlatnosciStatus?.SelectedIndex > 0)
            {
                switch (ComboPlatnosciStatus.SelectedIndex)
                {
                    case 1: filtered = filtered.Where(f => f.Status == "Zapłacona"); break;
                    case 2: filtered = filtered.Where(f => f.Status == "Nieopłacona"); break;
                    case 3: filtered = filtered.Where(f => f.Przeterminowana); break;
                }
            }

            var result = filtered.ToList();
            DataGridPlatnosci.ItemsSource = result;

            if (TextPlatnosciLiczba != null)
                TextPlatnosciLiczba.Text = result.Count.ToString();
            if (TextPlatnosciSuma != null)
                TextPlatnosciSuma.Text = $"{result.Where(f => !f.Anulowany).Sum(f => f.Brutto):N0} zł";
        }

        // ═══════════════════════════════════════════
        // KONTAKTY
        // ═══════════════════════════════════════════

        private void WypelnijKontakty(List<KontaktOdbiorcy> kontakty, int idSymfonia)
        {
            WrapPanelKontakty.Children.Clear();

            foreach (var k in kontakty)
            {
                var card = CreateKontaktCard(k);
                WrapPanelKontakty.Children.Add(card);
            }

            // Przycisk "Dodaj kontakt"
            var addBtn = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(134, 239, 172)),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12),
                Margin = new Thickness(4),
                Width = 180,
                Cursor = Cursors.Hand
            };
            var addText = new TextBlock
            {
                Text = "+ Dodaj kontakt",
                Foreground = new SolidColorBrush(Color.FromRgb(22, 163, 74)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.SemiBold
            };
            addBtn.Child = addText;
            addBtn.MouseLeftButtonDown += (s, e) =>
            {
                var dialog = new KontaktEdycjaWindow(new KontaktOdbiorcy { IdSymfonia = idSymfonia });
                if (dialog.ShowDialog() == true)
                {
                    _ = ReloadKontakty(idSymfonia);
                }
            };
            WrapPanelKontakty.Children.Add(addBtn);
        }

        private Border CreateKontaktCard(KontaktOdbiorcy kontakt)
        {
            var card = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(220, 252, 231)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12),
                Margin = new Thickness(4),
                Width = 180,
                Cursor = Cursors.Hand
            };
            card.Effect = new DropShadowEffect
            {
                ShadowDepth = 0, Color = Colors.Black, Opacity = 0.05, BlurRadius = 8
            };

            var stack = new StackPanel();

            var headerStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            headerStack.Children.Add(new TextBlock { Text = kontakt.IkonaTypu, FontSize = 16 });
            headerStack.Children.Add(new TextBlock
            {
                Text = kontakt.TypKontaktu,
                Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128)),
                FontSize = 11,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            stack.Children.Add(headerStack);

            stack.Children.Add(new TextBlock { Text = kontakt.PelneNazwisko, FontWeight = FontWeights.Bold });

            if (!string.IsNullOrEmpty(kontakt.Stanowisko))
            {
                stack.Children.Add(new TextBlock
                {
                    Text = kontakt.Stanowisko,
                    Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128)),
                    FontSize = 11
                });
            }

            if (!string.IsNullOrEmpty(kontakt.Telefon))
            {
                var telBlock = new TextBlock { Margin = new Thickness(0, 8, 0, 0), FontSize = 12 };
                var telLink = new Hyperlink();
                telLink.Inlines.Add($"📞 {kontakt.Telefon}");
                telLink.Foreground = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                telLink.TextDecorations = null;
                telLink.Click += (s, e) =>
                {
                    try { Process.Start(new ProcessStartInfo { FileName = $"tel:{kontakt.Telefon}", UseShellExecute = true }); } catch { }
                };
                telBlock.Inlines.Add(telLink);
                stack.Children.Add(telBlock);
            }

            if (!string.IsNullOrEmpty(kontakt.Email))
            {
                var emailBlock = new TextBlock { FontSize = 12 };
                var emailLink = new Hyperlink();
                emailLink.Inlines.Add($"📧 {kontakt.Email}");
                emailLink.Foreground = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                emailLink.TextDecorations = null;
                emailLink.Click += (s, e) =>
                {
                    try { Process.Start(new ProcessStartInfo { FileName = $"mailto:{kontakt.Email}", UseShellExecute = true }); } catch { }
                };
                emailBlock.Inlines.Add(emailLink);
                stack.Children.Add(emailBlock);
            }

            card.Child = stack;

            card.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2)
                {
                    var dialog = new KontaktEdycjaWindow(kontakt);
                    if (dialog.ShowDialog() == true)
                    {
                        _ = ReloadKontakty(kontakt.IdSymfonia);
                    }
                }
            };

            card.ContextMenu = new ContextMenu();
            var editItem = new MenuItem { Header = "📝 Edytuj" };
            editItem.Click += (s, e) =>
            {
                var dialog = new KontaktEdycjaWindow(kontakt);
                if (dialog.ShowDialog() == true)
                    _ = ReloadKontakty(kontakt.IdSymfonia);
            };
            var deleteItem = new MenuItem { Header = "🗑️ Usuń" };
            deleteItem.Click += async (s, e) =>
            {
                if (MessageBox.Show($"Usunąć kontakt {kontakt.PelneNazwisko}?", "Potwierdź", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    await _service.UsunKontaktAsync(kontakt.Id);
                    await ReloadKontakty(kontakt.IdSymfonia);
                }
            };
            card.ContextMenu.Items.Add(editItem);
            card.ContextMenu.Items.Add(deleteItem);

            return card;
        }

        private async System.Threading.Tasks.Task ReloadKontakty(int idSymfonia)
        {
            var kontakty = await _service.PobierzKontaktyAsync(idSymfonia);
            WypelnijKontakty(kontakty, idSymfonia);
        }

        // ═══════════════════════════════════════════
        // FINANSE
        // ═══════════════════════════════════════════

        private void WypelnijFinanse(OdbiorcaHandlowca odbiorca, List<FakturaOdbiorcy> faktury)
        {
            TextFinanseLimitKwota.Text = $"{odbiorca.LimitKupiecki:N0} zł";
            var procent = odbiorca.ProcentWykorzystania;
            ProgressFinanseLimit.Value = Math.Min(procent, 100);
            TextFinanseProcentLabel.Text = $"{procent:F0}%";
            TextFinanseWolne.Text = $"{odbiorca.WolnyLimit:N0} zł";
            TextFinanseZajete.Text = $"{odbiorca.WykorzystanoLimit:N0} zł";

            TextFinansePlatnosc.Text = $"{odbiorca.TerminPlatnosci} dni";
            TextFinanseFormaPlatnosci.Text = string.IsNullOrEmpty(odbiorca.FormaPlatnosci) ? "-" : odbiorca.FormaPlatnosci;

            var doZaplaty = faktury.Where(f => !f.Anulowany && f.DoZaplaty > 0).Sum(f => f.DoZaplaty);
            TextFinanseDoZaplaty.Text = $"{doZaplaty:N0} zł";

            TextFinansePrzeteminowane.Text = $"{odbiorca.KwotaPrzeterminowana:N0} zł";

            var aktywne = faktury.Where(f => !f.Anulowany).ToList();
            var obrot = aktywne.Sum(f => f.Brutto);
            var fakturCount = aktywne.Count;
            TextFinanseObrot.Text = $"{obrot:N0} zł";
            TextFinanseFakturCount.Text = $"Obrót 12m - {fakturCount} faktur";

            BuildObrotyWykres(aktywne);

            var nieoplacone = faktury.Count(f => !f.Anulowany && f.DoZaplaty > 0);
            TextFinanseStatusPlatnosci.Text = nieoplacone == 0
                ? "Wszystkie faktury opłacone ✓"
                : $"{nieoplacone} nieopłaconych faktur";
            TextFinanseStatusPlatnosci.Foreground = nieoplacone == 0
                ? new SolidColorBrush(Color.FromRgb(22, 163, 74))
                : new SolidColorBrush(Color.FromRgb(220, 38, 38));
        }

        private void BuildObrotyWykres(List<FakturaOdbiorcy> faktury)
        {
            GridObrotyWykres.Children.Clear();
            GridObrotyWykres.ColumnDefinitions.Clear();
            GridObrotyLabels.Children.Clear();
            GridObrotyLabels.ColumnDefinitions.Clear();
            GridObrotyValues.Children.Clear();
            GridObrotyValues.ColumnDefinitions.Clear();

            var teraz = DateTime.Now;
            var miesieczne = new decimal[12];
            for (int i = 0; i < 12; i++)
            {
                var miesiac = teraz.AddMonths(-11 + i);
                miesieczne[i] = faktury
                    .Where(f => f.DataFaktury.Year == miesiac.Year && f.DataFaktury.Month == miesiac.Month)
                    .Sum(f => f.Brutto);
            }

            var maxVal = miesieczne.Max();
            if (maxVal <= 0) maxVal = 1;

            string[] nazwyMiesiecy = { "Sty", "Lut", "Mar", "Kwi", "Maj", "Cze", "Lip", "Sie", "Wrz", "Paź", "Lis", "Gru" };

            for (int i = 0; i < 12; i++)
            {
                GridObrotyWykres.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                GridObrotyLabels.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                GridObrotyValues.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var miesiac = teraz.AddMonths(-11 + i);
                var height = (double)(miesieczne[i] / maxVal) * 80;
                if (height < 2 && miesieczne[i] > 0) height = 2;

                var rect = new Rectangle
                {
                    Fill = new SolidColorBrush(Color.FromRgb(187, 247, 208)),
                    Margin = new Thickness(1, 0, 1, 0),
                    Height = height,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    ToolTip = $"{nazwyMiesiecy[miesiac.Month - 1]} {miesiac.Year}: {miesieczne[i]:N0} zł",
                    RadiusX = 2,
                    RadiusY = 2
                };
                rect.MouseEnter += (s, e) => ((Rectangle)s).Fill = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                rect.MouseLeave += (s, e) => ((Rectangle)s).Fill = new SolidColorBrush(Color.FromRgb(187, 247, 208));

                Grid.SetColumn(rect, i);
                GridObrotyWykres.Children.Add(rect);

                var labelMiesiac = new TextBlock
                {
                    Text = nazwyMiesiecy[miesiac.Month - 1],
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextAlignment = TextAlignment.Center
                };
                Grid.SetColumn(labelMiesiac, i);
                GridObrotyLabels.Children.Add(labelMiesiac);

                var valText = miesieczne[i] >= 1000 ? $"{miesieczne[i] / 1000:N0}k" : $"{miesieczne[i]:N0}";
                var labelWartosc = new TextBlock
                {
                    Text = miesieczne[i] > 0 ? valText : "",
                    FontSize = 9,
                    Foreground = new SolidColorBrush(Color.FromRgb(22, 163, 74)),
                    FontWeight = FontWeights.Medium,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextAlignment = TextAlignment.Center
                };
                Grid.SetColumn(labelWartosc, i);
                GridObrotyValues.Children.Add(labelWartosc);
            }
        }

        // ═══════════════════════════════════════════
        // NOTATKI
        // ═══════════════════════════════════════════

        private async void ButtonZapiszNotatki_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedOdbiorca == null) return;

            var tresc = TextBoxNotatki.Text?.Trim();
            if (string.IsNullOrEmpty(tresc))
            {
                MessageBox.Show("Wpisz treść notatki.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                await _service.DodajNotatkeAsync(_selectedOdbiorca.IdSymfonia, tresc, _userName);
                TextBoxNotatki.Text = "";
                await LoadNotatki(_selectedOdbiorca.IdSymfonia);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisu notatki: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async System.Threading.Tasks.Task LoadNotatki(int idSymfonia)
        {
            try
            {
                var notatki = await _service.PobierzNotatkiAsync(idSymfonia);
                WypelnijHistorieNotatek(notatki, idSymfonia);
            }
            catch { }
        }

        private void WypelnijHistorieNotatek(List<NotatkaPozycja> notatki, int idSymfonia)
        {
            PanelHistoriaNotatek.Children.Clear();
            TextNotatekCount.Text = $"{notatki.Count} notatek";

            foreach (var n in notatki)
            {
                var card = new Border
                {
                    Padding = new Thickness(10),
                    Margin = new Thickness(0, 0, 0, 6),
                    CornerRadius = new CornerRadius(6),
                    BorderThickness = new Thickness(3, 0, 0, 0),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(34, 197, 94)),
                    Background = new SolidColorBrush(Color.FromRgb(240, 253, 244))
                };
                card.Effect = new DropShadowEffect
                {
                    ShadowDepth = 0, Color = Colors.Black, Opacity = 0.04, BlurRadius = 4
                };

                var stack = new StackPanel();

                var header = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
                header.Children.Add(new TextBlock
                {
                    Text = n.Autor,
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(22, 101, 52))
                });
                var dataBlock = new TextBlock
                {
                    Text = n.DataUtworzenia.ToString("dd.MM.yyyy HH:mm"),
                    Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128)),
                    FontSize = 10,
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                DockPanel.SetDock(dataBlock, Dock.Right);
                header.Children.Insert(0, dataBlock);
                stack.Children.Add(header);

                stack.Children.Add(new TextBlock
                {
                    Text = n.Tresc,
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(55, 65, 81))
                });

                card.Child = stack;

                card.ContextMenu = new ContextMenu();
                var deleteItem = new MenuItem { Header = "Usuń notatkę" };
                var noteId = n.Id;
                deleteItem.Click += async (s, ev) =>
                {
                    if (MessageBox.Show("Usunąć tę notatkę?", "Potwierdź", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        await _service.UsunNotatkeAsync(noteId);
                        await LoadNotatki(idSymfonia);
                    }
                };
                card.ContextMenu.Items.Add(deleteItem);

                PanelHistoriaNotatek.Children.Add(card);
            }

            if (notatki.Count == 0)
            {
                PanelHistoriaNotatek.Children.Add(new TextBlock
                {
                    Text = "Brak notatek dla tego odbiorcy.\nDodaj pierwszą notatkę po lewej stronie.",
                    Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175)),
                    FontSize = 11,
                    TextWrapping = TextWrapping.Wrap,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 30, 0, 0)
                });
            }
        }

        // ═══════════════════════════════════════════
        // EVENTS - Filtry
        // ═══════════════════════════════════════════

        private void TextBoxSzukaj_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void ComboBoxKategoria_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded) ApplyFilters();
        }

        private async void ComboBoxHandlowiec_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded) await LoadData();
        }

        private void CheckBoxAlerty_Changed(object sender, RoutedEventArgs e)
        {
            if (IsLoaded) ApplyFilters();
        }

        // ═══════════════════════════════════════════
        // EVENTS - Przyciski
        // ═══════════════════════════════════════════

        private async void ButtonOdswiez_Click(object sender, RoutedEventArgs e)
        {
            await LoadData();
        }

        private void ButtonExcel_Click(object sender, RoutedEventArgs e)
        {
            ExportToExcel();
        }

        // ═══════════════════════════════════════════
        // EVENTS - Edycja
        // ═══════════════════════════════════════════

        private void OtworzEdycje()
        {
            if (_selectedOdbiorca == null) return;

            var edycja = new OdbiorcaEdycjaWindow(_selectedOdbiorca, _service, _userName);
            if (edycja.ShowDialog() == true)
            {
                _ = LoadData();
            }
        }

        // ═══════════════════════════════════════════
        // EVENTS - Hyperlinks (kept for XAML compatibility)
        // ═══════════════════════════════════════════

        private void Hyperlink_Telefon_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Hyperlink hyperlink && hyperlink.NavigateUri != null)
            {
                try { Process.Start(new ProcessStartInfo { FileName = hyperlink.NavigateUri.ToString(), UseShellExecute = true }); } catch { }
            }
        }

        private void Hyperlink_Email_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Hyperlink hyperlink && hyperlink.NavigateUri != null)
            {
                try { Process.Start(new ProcessStartInfo { FileName = hyperlink.NavigateUri.ToString(), UseShellExecute = true }); } catch { }
            }
        }

        // ═══════════════════════════════════════════
        // EVENTS - Skróty klawiszowe
        // ═══════════════════════════════════════════

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5)
            {
                _ = LoadData();
                e.Handled = true;
            }
            else if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
            {
                TextBoxSzukaj.Focus();
                TextBoxSzukaj.SelectAll();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && _selectedOdbiorca != null)
            {
                OtworzEdycje();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape && _expandedCard != null)
            {
                CollapseCard();
                e.Handled = true;
            }
        }

        // ═══════════════════════════════════════════
        // EVENTS - Zakładki
        // ═══════════════════════════════════════════

        private void TabControlSzczegoly_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Placeholder for lazy-loading tab content
        }

        // ═══════════════════════════════════════════
        // Export Excel
        // ═══════════════════════════════════════════

        private void ExportToExcel()
        {
            var items = _displayedOdbiorcy;
            if (items == null || !items.Any())
            {
                MessageBox.Show("Brak danych do eksportu.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var saveDialog = new SaveFileDialog
            {
                Filter = "Excel (*.xlsx)|*.xlsx",
                FileName = $"Kartoteka_Odbiorcow_{DateTime.Now:yyyy-MM-dd}.xlsx"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    using var workbook = new XLWorkbook();
                    var ws = workbook.Worksheets.Add("Odbiorcy");

                    var headers = new[] { "Firma", "Miasto", "NIP", "Kontakt", "Telefon", "Email",
                        "Limit kupiecki", "Wykorzystano", "%", "Bilans", "Kategoria", "Handlowiec",
                        "Forma płatności", "Termin płatności", "Asortyment", "Trasa" };
                    for (int i = 0; i < headers.Length; i++)
                        ws.Cell(1, i + 1).Value = headers[i];

                    int row = 2;
                    foreach (var o in items)
                    {
                        ws.Cell(row, 1).Value = o.NazwaFirmy;
                        ws.Cell(row, 2).Value = o.Miasto;
                        ws.Cell(row, 3).Value = o.NIP;
                        ws.Cell(row, 4).Value = o.OsobaKontaktowa;
                        ws.Cell(row, 5).Value = o.TelefonKontakt;
                        ws.Cell(row, 6).Value = o.EmailKontakt;
                        ws.Cell(row, 7).Value = (double)o.LimitKupiecki;
                        ws.Cell(row, 8).Value = (double)o.WykorzystanoLimit;
                        ws.Cell(row, 9).Value = o.ProcentWykorzystania;
                        ws.Cell(row, 10).Value = (double)o.Bilans;
                        ws.Cell(row, 11).Value = o.KategoriaHandlowca;
                        ws.Cell(row, 12).Value = o.Handlowiec;
                        ws.Cell(row, 13).Value = o.FormaPlatnosci;
                        ws.Cell(row, 14).Value = o.TerminPlatnosci;
                        ws.Cell(row, 15).Value = o.Asortyment;
                        ws.Cell(row, 16).Value = o.Trasa;
                        row++;
                    }

                    ws.Row(1).Style.Font.Bold = true;
                    ws.Row(1).Style.Fill.BackgroundColor = XLColor.FromHtml("#DCFCE7");
                    ws.Columns().AdjustToContents();

                    ws.Column(7).Style.NumberFormat.Format = "#,##0";
                    ws.Column(8).Style.NumberFormat.Format = "#,##0";
                    ws.Column(9).Style.NumberFormat.Format = "0%";
                    ws.Column(10).Style.NumberFormat.Format = "#,##0";

                    workbook.SaveAs(saveDialog.FileName);

                    MessageBox.Show("Wyeksportowano pomyślnie!", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd eksportu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // ═══════════════════════════════════════════
        // DEBUG / PROFILER
        // ═══════════════════════════════════════════

        private async void ButtonDebug_Click(object sender, RoutedEventArgs e)
        {
            ButtonDebug.IsEnabled = false;
            ButtonDebug.Content = "⏳ Test...";

            try
            {
                var sw = new Stopwatch();
                var log = new System.Text.StringBuilder();
                log.AppendLine("══════════════════════════════════════════");
                log.AppendLine("  KARTOTEKA ODBIORCÓW - PROFILER DEBUG");
                log.AppendLine($"  Data: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                log.AppendLine($"  User: {_userName} (ID: {_userId})");
                log.AppendLine("══════════════════════════════════════════");
                log.AppendLine();

                var totalSw = Stopwatch.StartNew();

                sw.Restart();
                await _service.EnsureTablesExistAsync();
                sw.Stop();
                log.AppendLine($"[1] EnsureTablesExist:     {sw.ElapsedMilliseconds,6} ms");

                sw.Restart();
                var odbiorcy = await _service.PobierzOdbiorcowAsync(null, true);
                sw.Stop();
                log.AppendLine($"[2] PobierzOdbiorcow:      {sw.ElapsedMilliseconds,6} ms  ({odbiorcy.Count} rekordów)");

                sw.Restart();
                await _service.WczytajDaneWlasneAsync(odbiorcy);
                sw.Stop();
                log.AppendLine($"[3] WczytajDaneWlasne:     {sw.ElapsedMilliseconds,6} ms");

                sw.Restart();
                var khids = odbiorcy.Select(o => o.IdSymfonia).ToList();
                var asortyment = await _service.PobierzAsortymentAsync(khids, 6);
                sw.Stop();
                log.AppendLine($"[4] PobierzAsortyment:     {sw.ElapsedMilliseconds,6} ms  ({asortyment.Count} z asortymentem)");

                sw.Restart();
                var handlowcy = await _service.PobierzHandlowcowAsync();
                sw.Stop();
                log.AppendLine($"[5] PobierzHandlowcow:     {sw.ElapsedMilliseconds,6} ms  ({handlowcy.Count} handlowców)");

                if (odbiorcy.Count > 0)
                {
                    var testId = odbiorcy[0].IdSymfonia;
                    sw.Restart();
                    var kontakty = await _service.PobierzKontaktyAsync(testId);
                    sw.Stop();
                    log.AppendLine($"[6] PobierzKontakty:       {sw.ElapsedMilliseconds,6} ms  (test: {odbiorcy[0].NazwaFirmy}, {kontakty.Count} kontaktów)");

                    sw.Restart();
                    var faktury = await _service.PobierzFakturyAsync(testId);
                    sw.Stop();
                    log.AppendLine($"[7] PobierzFaktury:        {sw.ElapsedMilliseconds,6} ms  ({faktury.Count} faktur)");

                    sw.Restart();
                    var asSzczeg = await _service.PobierzAsortymentSzczegolyAsync(testId, 12);
                    sw.Stop();
                    log.AppendLine($"[8] AsortymentSzczegoly:   {sw.ElapsedMilliseconds,6} ms  ({asSzczeg.Count} produktów)");
                }

                totalSw.Stop();
                log.AppendLine();
                log.AppendLine($"══════════════════════════════════════════");
                log.AppendLine($"  TOTAL:                   {totalSw.ElapsedMilliseconds,6} ms");
                log.AppendLine($"══════════════════════════════════════════");

                var result = log.ToString();
                Clipboard.SetText(result);
                MessageBox.Show(result + "\n\n(Skopiowano do schowka)", "Debug Profiler",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd debuggera:\n{ex.Message}\n\n{ex.StackTrace}", "Debug Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ButtonDebug.IsEnabled = true;
                ButtonDebug.Content = "🐛 Debug";
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // No column widths to save in accordion mode
        }
    }
}
