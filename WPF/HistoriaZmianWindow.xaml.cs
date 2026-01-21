using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.WPF
{
    public partial class HistoriaZmianWindow : Window
    {
        private readonly string _connLibra;
        private readonly string _connHandel;
        private readonly string _userId;
        private readonly DataTable _dtHistoria = new();
        private bool _isLoading;
        private Dictionary<int, string> _productNames = new();

        public HistoriaZmianWindow(string connLibra, string connHandel, string userId = "")
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            _connLibra = connLibra;
            _connHandel = connHandel;
            _userId = userId;

            InitializeDataTable();
            _ = LoadDataAsync();
        }

        private void InitializeDataTable()
        {
            _dtHistoria.Columns.Add("Id", typeof(int));
            _dtHistoria.Columns.Add("ZamowienieId", typeof(int));
            _dtHistoria.Columns.Add("DataZmiany", typeof(DateTime));
            _dtHistoria.Columns.Add("TypZmiany", typeof(string));
            _dtHistoria.Columns.Add("Handlowiec", typeof(string));
            _dtHistoria.Columns.Add("Odbiorca", typeof(string));
            _dtHistoria.Columns.Add("UzytkownikNazwa", typeof(string));
            _dtHistoria.Columns.Add("UzytkownikId", typeof(string));
            _dtHistoria.Columns.Add("Towar", typeof(string));
            _dtHistoria.Columns.Add("KodTowaru", typeof(int));
            _dtHistoria.Columns.Add("OpisZmiany", typeof(string));
            _dtHistoria.Columns.Add("DataUboju", typeof(DateTime));

            dgHistoria.ItemsSource = _dtHistoria.DefaultView;
            SetupDataGrid();
        }

        private void SetupDataGrid()
        {
            dgHistoria.Columns.Clear();

            dgHistoria.Columns.Add(new DataGridTextColumn
            {
                Header = "Data zmiany",
                Binding = new Binding("DataZmiany") { StringFormat = "yyyy-MM-dd HH:mm" },
                Width = new DataGridLength(130)
            });

            dgHistoria.Columns.Add(new DataGridTextColumn
            {
                Header = "Typ",
                Binding = new Binding("TypZmiany"),
                Width = new DataGridLength(100)
            });

            dgHistoria.Columns.Add(new DataGridTextColumn
            {
                Header = "Odbiorca",
                Binding = new Binding("Odbiorca"),
                Width = new DataGridLength(180)
            });

            dgHistoria.Columns.Add(new DataGridTextColumn
            {
                Header = "Handlowiec",
                Binding = new Binding("Handlowiec"),
                Width = new DataGridLength(100)
            });

            // Kolumna Użytkownik z avatarem
            var userColumn = new DataGridTemplateColumn
            {
                Header = "Użytkownik",
                Width = new DataGridLength(140)
            };

            var cellTemplate = new DataTemplate();
            var stackPanelFactory = new FrameworkElementFactory(typeof(StackPanel));
            stackPanelFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            stackPanelFactory.SetValue(StackPanel.VerticalAlignmentProperty, VerticalAlignment.Center);

            // Grid dla avatara
            var gridFactory = new FrameworkElementFactory(typeof(Grid));
            gridFactory.SetValue(Grid.WidthProperty, 26.0);
            gridFactory.SetValue(Grid.HeightProperty, 26.0);
            gridFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 8, 0));

            // Border z inicjałami (domyślny avatar)
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.WidthProperty, 26.0);
            borderFactory.SetValue(Border.HeightProperty, 26.0);
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(13));
            borderFactory.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(142, 68, 173)));
            borderFactory.SetValue(FrameworkElement.NameProperty, "avatarBorder");

            var initialsFactory = new FrameworkElementFactory(typeof(TextBlock));
            initialsFactory.SetBinding(TextBlock.TextProperty, new Binding("UzytkownikNazwa") { Converter = new InitialsConverter() });
            initialsFactory.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            initialsFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            initialsFactory.SetValue(TextBlock.ForegroundProperty, Brushes.White);
            initialsFactory.SetValue(TextBlock.FontSizeProperty, 10.0);
            initialsFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            borderFactory.AppendChild(initialsFactory);

            // Ellipse dla obrazka avatara
            var ellipseFactory = new FrameworkElementFactory(typeof(Ellipse));
            ellipseFactory.SetValue(Ellipse.WidthProperty, 26.0);
            ellipseFactory.SetValue(Ellipse.HeightProperty, 26.0);
            ellipseFactory.SetValue(UIElement.VisibilityProperty, Visibility.Collapsed);
            ellipseFactory.SetValue(FrameworkElement.NameProperty, "avatarImage");

            gridFactory.AppendChild(borderFactory);
            gridFactory.AppendChild(ellipseFactory);

            // TextBlock z nazwą użytkownika
            var nameFactory = new FrameworkElementFactory(typeof(TextBlock));
            nameFactory.SetBinding(TextBlock.TextProperty, new Binding("UzytkownikNazwa"));
            nameFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            nameFactory.SetValue(TextBlock.FontSizeProperty, 11.0);

            stackPanelFactory.AppendChild(gridFactory);
            stackPanelFactory.AppendChild(nameFactory);

            cellTemplate.VisualTree = stackPanelFactory;
            userColumn.CellTemplate = cellTemplate;

            dgHistoria.Columns.Add(userColumn);

            // Dodaj LoadingRow event handler dla avatarów
            dgHistoria.LoadingRow += DgHistoria_LoadingRow;

            dgHistoria.Columns.Add(new DataGridTextColumn
            {
                Header = "Towar",
                Binding = new Binding("Towar"),
                Width = new DataGridLength(150)
            });

            dgHistoria.Columns.Add(new DataGridTextColumn
            {
                Header = "Opis zmiany",
                Binding = new Binding("OpisZmiany"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            });

            dgHistoria.Columns.Add(new DataGridTextColumn
            {
                Header = "Data uboju",
                Binding = new Binding("DataUboju") { StringFormat = "yyyy-MM-dd" },
                Width = new DataGridLength(100)
            });
        }

        private async System.Threading.Tasks.Task LoadDataAsync()
        {
            if (_isLoading) return;

            _isLoading = true;
            _dtHistoria.Rows.Clear();

            try
            {
                // Pobierz kontrahentów
                var contractors = new Dictionary<int, (string Name, string Salesman)>();
                await using (var cnHandel = new SqlConnection(_connHandel))
                {
                    await cnHandel.OpenAsync();
                    const string sqlContr = @"SELECT c.Id, c.Shortcut, wym.CDim_Handlowiec_Val
                                    FROM [HANDEL].[SSCommon].[STContractors] c
                                    LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] wym
                                    ON c.Id = wym.ElementId";
                    await using var cmdContr = new SqlCommand(sqlContr, cnHandel);
                    await using var rd = await cmdContr.ExecuteReaderAsync();

                    while (await rd.ReadAsync())
                    {
                        int id = rd.GetInt32(0);
                        string shortcut = rd.IsDBNull(1) ? "" : rd.GetString(1);
                        string salesman = rd.IsDBNull(2) ? "" : rd.GetString(2);
                        contractors[id] = (string.IsNullOrWhiteSpace(shortcut) ? $"KH {id}" : shortcut, salesman);
                    }
                }

                // Pobierz produkty z katalogu TW (tylko Świeże 67095 i Mrożone 67153)
                _productNames.Clear();
                try
                {
                    await using var cnHandel = new SqlConnection(_connHandel);
                    await cnHandel.OpenAsync();
                    const string sqlProducts = "SELECT ID, kod FROM [HANDEL].[HM].[TW] WHERE katalog IN (67095, 67153) ORDER BY kod";
                    await using var cmdProducts = new SqlCommand(sqlProducts, cnHandel);
                    await using var rdrProducts = await cmdProducts.ExecuteReaderAsync();
                    while (await rdrProducts.ReadAsync())
                    {
                        if (!rdrProducts.IsDBNull(0))
                        {
                            int id = rdrProducts.GetInt32(0);
                            string kod = rdrProducts.IsDBNull(1) ? $"ID:{id}" : rdrProducts.GetString(1);
                            _productNames[id] = kod;
                        }
                    }
                }
                catch { /* Ignoruj błędy ładowania produktów */ }

                // Pobierz wszystkie zamówienia
                var orderToClient = new Dictionary<int, int>();
                var orderToDataUboju = new Dictionary<int, DateTime>();
                await using (var cnLibra = new SqlConnection(_connLibra))
                {
                    await cnLibra.OpenAsync();

                    // Sprawdź czy tabela istnieje
                    string checkSql = @"SELECT COUNT(*) FROM sys.objects
                                       WHERE object_id = OBJECT_ID(N'[dbo].[HistoriaZmianZamowien]') AND type in (N'U')";
                    using var checkCmd = new SqlCommand(checkSql, cnLibra);
                    var tableExists = (int)await checkCmd.ExecuteScalarAsync() > 0;

                    if (!tableExists)
                    {
                        MessageBox.Show("Tabela historii zmian nie istnieje w bazie danych.", "Informacja",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    // Sprawdź czy kolumna DataUboju istnieje
                    bool hasDataUboju = false;
                    const string checkDataUbojuSql = @"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                                                      WHERE TABLE_NAME = 'ZamowieniaMieso' AND COLUMN_NAME = 'DataUboju'";
                    await using (var checkDuCmd = new SqlCommand(checkDataUbojuSql, cnLibra))
                    {
                        hasDataUboju = (int)await checkDuCmd.ExecuteScalarAsync() > 0;
                    }

                    // Pobierz wszystkie zamówienia
                    string sqlOrders = hasDataUboju
                        ? @"SELECT Id, KlientId, DataUboju FROM dbo.ZamowieniaMieso"
                        : @"SELECT Id, KlientId FROM dbo.ZamowieniaMieso";
                    await using var cmdOrders = new SqlCommand(sqlOrders, cnLibra);
                    await using var rdrOrders = await cmdOrders.ExecuteReaderAsync();

                    while (await rdrOrders.ReadAsync())
                    {
                        int orderId = rdrOrders.GetInt32(0);
                        int clientId = rdrOrders.IsDBNull(1) ? 0 : rdrOrders.GetInt32(1);
                        orderToClient[orderId] = clientId;

                        if (hasDataUboju && !rdrOrders.IsDBNull(2))
                        {
                            orderToDataUboju[orderId] = rdrOrders.GetDateTime(2);
                        }
                    }

                    await rdrOrders.CloseAsync();

                    // Sprawdź czy tabela ma kolumnę KodTowaru
                    bool hasKodTowaru = false;
                    const string checkColSql = @"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                                                WHERE TABLE_NAME = 'HistoriaZmianZamowien' AND COLUMN_NAME = 'KodTowaru'";
                    await using (var checkColCmd = new SqlCommand(checkColSql, cnLibra))
                    {
                        hasKodTowaru = (int)await checkColCmd.ExecuteScalarAsync() > 0;
                    }

                    // Pobierz CAŁĄ historię - posortowaną od najnowszej do najstarszej
                    string sqlHistory = hasKodTowaru
                        ? @"SELECT Id, ZamowienieId, DataZmiany, TypZmiany, UzytkownikNazwa, OpisZmiany, KodTowaru, Uzytkownik
                            FROM HistoriaZmianZamowien
                            ORDER BY DataZmiany DESC"
                        : @"SELECT Id, ZamowienieId, DataZmiany, TypZmiany, UzytkownikNazwa, OpisZmiany, Uzytkownik
                            FROM HistoriaZmianZamowien
                            ORDER BY DataZmiany DESC";

                    await using var cmdHistory = new SqlCommand(sqlHistory, cnLibra);
                    await using var rdrHistory = await cmdHistory.ExecuteReaderAsync();

                    while (await rdrHistory.ReadAsync())
                    {
                        int id = rdrHistory.GetInt32(0);
                        int zamowienieId = rdrHistory.GetInt32(1);
                        DateTime dataZmiany = rdrHistory.GetDateTime(2);
                        string typZmiany = rdrHistory.IsDBNull(3) ? "" : rdrHistory.GetString(3);
                        string uzytkownikNazwa = rdrHistory.IsDBNull(4) ? "" : rdrHistory.GetString(4);
                        string opisZmiany = rdrHistory.IsDBNull(5) ? "" : rdrHistory.GetString(5);

                        // Pobierz towar jeśli kolumna istnieje
                        string towar = "";
                        int kodTowaru = 0;
                        string uzytkownikId = "";

                        if (hasKodTowaru)
                        {
                            if (!rdrHistory.IsDBNull(6))
                            {
                                kodTowaru = rdrHistory.GetInt32(6);
                                towar = _productNames.TryGetValue(kodTowaru, out var name) ? name : $"ID:{kodTowaru}";
                            }
                            // Uzytkownik jest na pozycji 7 gdy jest KodTowaru
                            uzytkownikId = rdrHistory.IsDBNull(7) ? "" : rdrHistory.GetString(7);
                        }
                        else
                        {
                            // Spróbuj wyciągnąć nazwę towaru z opisu zmiany
                            towar = ExtractProductFromDescription(opisZmiany);
                            // Uzytkownik jest na pozycji 6 gdy nie ma KodTowaru
                            uzytkownikId = rdrHistory.IsDBNull(6) ? "" : rdrHistory.GetString(6);
                        }

                        string handlowiec = "";
                        string odbiorca = "";
                        if (orderToClient.TryGetValue(zamowienieId, out int clientId) &&
                            contractors.TryGetValue(clientId, out var contr))
                        {
                            handlowiec = contr.Salesman;
                            odbiorca = contr.Name;
                        }

                        // Pobierz DataUboju dla zamówienia
                        DateTime? dataUboju = orderToDataUboju.TryGetValue(zamowienieId, out var du) ? du : null;

                        _dtHistoria.Rows.Add(id, zamowienieId, dataZmiany, typZmiany,
                            handlowiec, odbiorca, uzytkownikNazwa, uzytkownikId, towar, kodTowaru, opisZmiany,
                            dataUboju.HasValue ? (object)dataUboju.Value : DBNull.Value);
                    }
                }

                txtTotalChanges.Text = _dtHistoria.Rows.Count.ToString("N0");
                txtDateRange.Text = $"Łącznie: {_dtHistoria.Rows.Count:N0} zmian";
                PopulateFilterComboBoxes();
                UpdateDisplayedCount();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania danych: {ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isLoading = false;
            }
        }

        private string ExtractProductFromDescription(string opis)
        {
            if (string.IsNullOrEmpty(opis)) return "";

            // Wzorce do wyciągnięcia nazwy produktu z opisu
            var patterns = new[]
            {
                @"Zmiana ilości:\s*(.+?)\s+z\s+\d",
                @"Zmiana ceny:\s*(.+?)\s+z\s+\d",
                @"Produkt:\s*(.+?)(?:\s*[-,]|$)",
                @"Towar:\s*(.+?)(?:\s*[-,]|$)",
                @"^(.+?)\s*[-:]\s*(?:ilość|cena|zmiana)",
            };

            foreach (var pattern in patterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(opis, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success && match.Groups.Count > 1)
                {
                    var product = match.Groups[1].Value.Trim();
                    if (!string.IsNullOrEmpty(product) && product.Length > 2)
                        return product;
                }
            }

            return "";
        }

        private void PopulateFilterComboBoxes()
        {
            // ComboBox Towar - produkty z katalogu TW
            var towary = new List<string> { "(Wszystkie)" };
            towary.AddRange(_productNames.Values.OrderBy(x => x));
            cmbTowar.ItemsSource = towary;
            cmbTowar.SelectedIndex = 0;

            var users = new List<string> { "(Wszystkie)" };
            var odbiorcy = new List<string> { "(Wszystkie)" };
            var typy = new List<string> { "(Wszystkie)" };
            var handlowcy = new List<string> { "(Wszystkie)" };
            var datyUboju = new List<string> { "(Wszystkie)" };

            foreach (DataRow row in _dtHistoria.Rows)
            {
                var user = row["UzytkownikNazwa"]?.ToString();
                var odbiorca = row["Odbiorca"]?.ToString();
                var typ = row["TypZmiany"]?.ToString();
                var handlowiec = row["Handlowiec"]?.ToString();
                var dataUboju = row["DataUboju"] as DateTime?;
                string dataUbojuStr = dataUboju.HasValue && dataUboju.Value > DateTime.MinValue
                    ? dataUboju.Value.ToString("yyyy-MM-dd") : "";

                if (!string.IsNullOrEmpty(user) && !users.Contains(user)) users.Add(user);
                if (!string.IsNullOrEmpty(odbiorca) && !odbiorcy.Contains(odbiorca)) odbiorcy.Add(odbiorca);
                if (!string.IsNullOrEmpty(typ) && !typy.Contains(typ)) typy.Add(typ);
                if (!string.IsNullOrEmpty(handlowiec) && !handlowcy.Contains(handlowiec)) handlowcy.Add(handlowiec);
                if (!string.IsNullOrEmpty(dataUbojuStr) && !datyUboju.Contains(dataUbojuStr)) datyUboju.Add(dataUbojuStr);
            }

            cmbKtoEdytowal.ItemsSource = users.OrderBy(x => x).ToList();
            cmbOdbiorca.ItemsSource = odbiorcy.OrderBy(x => x).ToList();
            cmbTyp.ItemsSource = typy.OrderBy(x => x).ToList();
            cmbHandlowiec.ItemsSource = handlowcy.OrderBy(x => x).ToList();
            // Data uboju - sortuj od najnowszej
            cmbDataUboju.ItemsSource = datyUboju.Take(1).Concat(datyUboju.Skip(1).OrderByDescending(x => x)).ToList();

            cmbKtoEdytowal.SelectedIndex = 0;
            cmbOdbiorca.SelectedIndex = 0;
            cmbTyp.SelectedIndex = 0;
            cmbHandlowiec.SelectedIndex = 0;
            cmbDataUboju.SelectedIndex = 0;
        }

        private void ApplyFilters()
        {
            var dv = _dtHistoria.DefaultView;
            var filters = new List<string>();

            // Filtr produktu z ComboBox - szukaj w Towar LUB w OpisZmiany
            if (cmbTowar.SelectedItem?.ToString() is string towar && towar != "(Wszystkie)")
            {
                string escapedTowar = towar.Replace("'", "''").Replace("[", "[[]").Replace("*", "[*]").Replace("%", "[%]");
                filters.Add($"(Towar = '{escapedTowar}' OR OpisZmiany LIKE '*{escapedTowar}*')");
                txtHistoriaTitle.Text = $"HISTORIA ZMIAN - {towar.ToUpper()}";
            }
            else
            {
                txtHistoriaTitle.Text = "HISTORIA ZMIAN";
            }

            if (cmbKtoEdytowal.SelectedItem?.ToString() is string user && user != "(Wszystkie)")
                filters.Add($"UzytkownikNazwa = '{user.Replace("'", "''")}'");

            if (cmbOdbiorca.SelectedItem?.ToString() is string odbiorca && odbiorca != "(Wszystkie)")
                filters.Add($"Odbiorca = '{odbiorca.Replace("'", "''")}'");

            if (cmbTyp.SelectedItem?.ToString() is string typ && typ != "(Wszystkie)")
                filters.Add($"TypZmiany = '{typ.Replace("'", "''")}'");

            if (cmbHandlowiec.SelectedItem?.ToString() is string handlowiec && handlowiec != "(Wszystkie)")
                filters.Add($"Handlowiec = '{handlowiec.Replace("'", "''")}'");

            if (cmbDataUboju.SelectedItem?.ToString() is string dataUboju && dataUboju != "(Wszystkie)")
            {
                if (DateTime.TryParse(dataUboju, out var dt))
                    filters.Add($"DataUboju = '{dt:yyyy-MM-dd}'");
            }

            dv.RowFilter = filters.Count > 0 ? string.Join(" AND ", filters) : "";
            UpdateDisplayedCount();
        }

        private void UpdateDisplayedCount()
        {
            var dv = _dtHistoria.DefaultView;
            txtDisplayedCount.Text = dv.Count.ToString("N0");
        }

        private void Filter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            ApplyFilters();
        }

        private void BtnClearFilters_Click(object sender, RoutedEventArgs e)
        {
            cmbTowar.SelectedIndex = 0;
            cmbKtoEdytowal.SelectedIndex = 0;
            cmbOdbiorca.SelectedIndex = 0;
            cmbTyp.SelectedIndex = 0;
            cmbHandlowiec.SelectedIndex = 0;
            cmbDataUboju.SelectedIndex = 0;
            txtHistoriaTitle.Text = "HISTORIA ZMIAN";
            _dtHistoria.DefaultView.RowFilter = "";
            UpdateDisplayedCount();
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            _ = LoadDataAsync();
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Eksport do Excel - funkcja do zaimplementowania", "Informacja",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void DgHistoria_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Znajdź kliknięty wiersz
            var dep = (DependencyObject)e.OriginalSource;
            while (dep != null && !(dep is DataGridRow))
            {
                dep = System.Windows.Media.VisualTreeHelper.GetParent(dep);
            }

            if (dep is DataGridRow row && row.Item is DataRowView rowView)
            {
                // Pobierz ID zamówienia z wiersza
                var zamowienieId = rowView.Row.Field<int>("ZamowienieId");
                if (zamowienieId > 0)
                {
                    // Otwórz okno edycji zamówienia
                    var widokZamowienia = new Kalendarz1.WidokZamowienia(_userId, zamowienieId);
                    if (widokZamowienia.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        // Odśwież dane po edycji
                        _ = LoadDataAsync();
                    }
                }
            }
        }

        // Event handler dla ładowania avatarów użytkowników
        private void DgHistoria_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.DataContext is DataRowView rowView)
            {
                var uzytkownikId = rowView.Row.Field<string>("UzytkownikId");
                if (!string.IsNullOrEmpty(uzytkownikId))
                {
                    e.Row.Loaded += (s, args) =>
                    {
                        try
                        {
                            var presenter = FindVisualChild<DataGridCellsPresenter>(e.Row);
                            if (presenter == null) return;

                            var avatarImage = FindVisualChild<Ellipse>(e.Row, "avatarImage");
                            var avatarBorder = FindVisualChild<Border>(e.Row, "avatarBorder");

                            if (avatarImage != null && avatarBorder != null && UserAvatarManager.HasAvatar(uzytkownikId))
                            {
                                using (var avatar = UserAvatarManager.GetAvatarRounded(uzytkownikId, 32))
                                {
                                    if (avatar != null)
                                    {
                                        var brush = new ImageBrush(ConvertToImageSource(avatar));
                                        brush.Stretch = Stretch.UniformToFill;
                                        avatarImage.Fill = brush;
                                        avatarImage.Visibility = Visibility.Visible;
                                        avatarBorder.Visibility = Visibility.Collapsed;
                                    }
                                }
                            }
                        }
                        catch { }
                    };
                }
            }
        }

        private T FindVisualChild<T>(DependencyObject parent, string name = null) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                {
                    if (name == null || (child is FrameworkElement fe && fe.Name == name))
                        return typedChild;
                }
                var result = FindVisualChild<T>(child, name);
                if (result != null) return result;
            }
            return null;
        }

        private ImageSource ConvertToImageSource(System.Drawing.Image image)
        {
            using (var ms = new System.IO.MemoryStream())
            {
                image.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                ms.Position = 0;

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = ms;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
        }
    }

    /// <summary>
    /// Konwerter do wyświetlania inicjałów z nazwy użytkownika
    /// </summary>
    public class InitialsConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is string name && !string.IsNullOrWhiteSpace(name))
            {
                var parts = name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                    return (parts[0][0].ToString() + parts[1][0].ToString()).ToUpper();
                return name.Length >= 2 ? name.Substring(0, 2).ToUpper() : name.ToUpper();
            }
            return "?";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
