using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Data.SqlClient;

namespace Kalendarz1.Zadania
{
    public partial class ZadanieDialog : Window
    {
        private readonly string connectionString;
        private readonly string operatorId;
        private readonly ZadanieViewModel existingTask;
        private readonly bool isEditMode;
        private List<PracownikItem> wszyscyPracownicy = new List<PracownikItem>();
        private List<PracownikItem> wybraniPracownicy = new List<PracownikItem>();

        public ZadanieDialog(string connString, string opId, ZadanieViewModel task = null)
        {
            InitializeComponent();
            connectionString = connString;
            operatorId = opId;
            existingTask = task;
            isEditMode = task != null;

            if (isEditMode)
            {
                txtHeader.Text = "Edytuj Zadanie";
                btnZapisz.Content = "Aktualizuj";
            }

            txtSzukajPracownika.Text = "Szukaj...";
            txtSzukajPracownika.Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66));

            LoadPracownicy();
            InitializeForm();
            UpdateWybraniPanel();
        }

        private void LoadPracownicy()
        {
            wszyscyPracownicy.Clear();

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    var cmd = new SqlCommand("SELECT ID, Name FROM operators WHERE Name IS NOT NULL ORDER BY Name", conn);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            wszyscyPracownicy.Add(new PracownikItem
                            {
                                Id = reader.GetString(0),
                                Nazwa = reader.GetString(1)
                            });
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Ignore errors
            }

            // Domyślnie zaznacz bieżącego użytkownika
            var current = wszyscyPracownicy.FirstOrDefault(p => p.Id == operatorId);
            if (current != null && !isEditMode)
            {
                wybraniPracownicy.Add(current);
            }

            RefreshPracownicyList();
        }

        private void RefreshPracownicyList(string filter = null)
        {
            pnlPracownicy.Children.Clear();

            var lista = wszyscyPracownicy.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(filter) && filter != "Szukaj...")
            {
                lista = lista.Where(p => p.Nazwa.ToLower().Contains(filter.ToLower()));
            }

            foreach (var pracownik in lista)
            {
                var isSelected = wybraniPracownicy.Any(w => w.Id == pracownik.Id);
                var item = CreatePracownikItem(pracownik, isSelected);
                pnlPracownicy.Children.Add(item);
            }
        }

        private Border CreatePracownikItem(PracownikItem pracownik, bool isSelected)
        {
            var border = new Border
            {
                Background = isSelected
                    ? new SolidColorBrush(Color.FromRgb(0x2d, 0x4a, 0x2d))
                    : new SolidColorBrush(Colors.Transparent),
                BorderBrush = isSelected
                    ? new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50))
                    : new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(2),
                Padding = new Thickness(8, 6, 8, 6),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = pracownik
            };

            var stack = new StackPanel { Orientation = Orientation.Horizontal };

            // Avatar
            var avatar = CreateAvatar(pracownik.Id, pracownik.Nazwa, 28);
            if (avatar is FrameworkElement fe)
            {
                fe.Margin = new Thickness(0, 0, 8, 0);
            }
            stack.Children.Add(avatar);

            // Nazwa
            var nazwa = new TextBlock
            {
                Text = pracownik.Nazwa,
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };
            stack.Children.Add(nazwa);

            // Checkbox icon
            if (isSelected)
            {
                var check = new TextBlock
                {
                    Text = "✓",
                    Foreground = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)),
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(8, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                stack.Children.Add(check);
            }

            border.Child = stack;

            border.MouseLeftButtonUp += (s, e) =>
            {
                var p = (PracownikItem)border.Tag;
                if (wybraniPracownicy.Any(w => w.Id == p.Id))
                {
                    wybraniPracownicy.RemoveAll(w => w.Id == p.Id);
                }
                else
                {
                    wybraniPracownicy.Add(p);
                }
                RefreshPracownicyList(txtSzukajPracownika.Text);
                UpdateWybraniPanel();
            };

            border.MouseEnter += (s, e) =>
            {
                if (!isSelected)
                    border.Background = new SolidColorBrush(Color.FromRgb(0x35, 0x35, 0x55));
            };

            border.MouseLeave += (s, e) =>
            {
                if (!isSelected)
                    border.Background = new SolidColorBrush(Colors.Transparent);
            };

            return border;
        }

        private UIElement CreateAvatar(string id, string name, int size)
        {
            var avatarPath = GetAvatarPath(id);

            if (File.Exists(avatarPath))
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(avatarPath, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();

                    var brush = new ImageBrush(bitmap) { Stretch = Stretch.UniformToFill };
                    var ellipse = new Ellipse
                    {
                        Width = size,
                        Height = size,
                        Fill = brush
                    };
                    return ellipse;
                }
                catch { }
            }

            // Domyślny avatar z inicjałami
            var grid = new Grid { Width = size, Height = size };

            var bgColor = GetColorFromId(id);
            var circle = new Ellipse
            {
                Fill = new SolidColorBrush(bgColor)
            };
            grid.Children.Add(circle);

            var initials = GetInitials(name);
            var text = new TextBlock
            {
                Text = initials,
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = size / 2.5,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            grid.Children.Add(text);

            return grid;
        }

        private string GetAvatarPath(string userId)
        {
            return System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ZPSP", "Avatars", $"{userId}.png");
        }

        private string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                return (parts[0][0].ToString() + parts[1][0].ToString()).ToUpper();
            return name.Length >= 2 ? name.Substring(0, 2).ToUpper() : name.ToUpper();
        }

        private Color GetColorFromId(string id)
        {
            int hash = id?.GetHashCode() ?? 0;
            Color[] colors = {
                Color.FromRgb(46, 125, 50),
                Color.FromRgb(25, 118, 210),
                Color.FromRgb(156, 39, 176),
                Color.FromRgb(230, 81, 0),
                Color.FromRgb(0, 137, 123),
                Color.FromRgb(194, 24, 91),
                Color.FromRgb(69, 90, 100),
                Color.FromRgb(121, 85, 72)
            };
            return colors[Math.Abs(hash) % colors.Length];
        }

        private void UpdateWybraniPanel()
        {
            pnlWybrani.Children.Clear();

            if (wybraniPracownicy.Count == 0)
            {
                var info = new TextBlock
                {
                    Text = "Wybierz pracowników →",
                    Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center
                };
                pnlWybrani.Children.Add(info);
                return;
            }

            // Pokaż avatary wybranych (max 5)
            var toShow = wybraniPracownicy.Take(5).ToList();
            foreach (var p in toShow)
            {
                var avatar = CreateAvatar(p.Id, p.Nazwa, 32);
                if (avatar is FrameworkElement fe)
                {
                    fe.Margin = new Thickness(0, 0, -8, 0);
                    fe.ToolTip = p.Nazwa;
                }
                pnlWybrani.Children.Add(avatar);
            }

            if (wybraniPracownicy.Count > 5)
            {
                var more = new Border
                {
                    Width = 32,
                    Height = 32,
                    CornerRadius = new CornerRadius(16),
                    Background = new SolidColorBrush(Color.FromRgb(0x3a, 0x3a, 0x5c)),
                    Margin = new Thickness(4, 0, 0, 0)
                };
                more.Child = new TextBlock
                {
                    Text = $"+{wybraniPracownicy.Count - 5}",
                    Foreground = new SolidColorBrush(Colors.White),
                    FontSize = 11,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                pnlWybrani.Children.Add(more);
            }

            var countText = new TextBlock
            {
                Text = $"  ({wybraniPracownicy.Count} osób)",
                Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            };
            pnlWybrani.Children.Add(countText);
        }

        private void TxtSzukaj_GotFocus(object sender, RoutedEventArgs e)
        {
            if (txtSzukajPracownika.Text == "Szukaj...")
            {
                txtSzukajPracownika.Text = "";
                txtSzukajPracownika.Foreground = new SolidColorBrush(Colors.White);
            }
        }

        private void TxtSzukaj_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtSzukajPracownika.Text))
            {
                txtSzukajPracownika.Text = "Szukaj...";
                txtSzukajPracownika.Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66));
            }
        }

        private void TxtSzukaj_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (txtSzukajPracownika.Text != "Szukaj...")
            {
                RefreshPracownicyList(txtSzukajPracownika.Text);
            }
        }

        private void InitializeForm()
        {
            dpTermin.SelectedDate = DateTime.Today;

            if (isEditMode && existingTask != null)
            {
                txtTypZadania.Text = existingTask.TypZadania;
                txtOpis.Text = existingTask.Opis;
                dpTermin.SelectedDate = existingTask.TerminWykonania.Date;
                txtGodzina.Text = existingTask.TerminWykonania.ToString("HH:mm");

                rbNiski.IsChecked = existingTask.Priorytet == 1;
                rbSredni.IsChecked = existingTask.Priorytet == 2;
                rbWysoki.IsChecked = existingTask.Priorytet == 3;

                // Wczytaj przypisanych pracowników
                LoadPrzypisaniPracownicy(existingTask.Id);
            }
        }

        private void LoadPrzypisaniPracownicy(int zadanieId)
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    var cmd = new SqlCommand(@"
                        SELECT zp.OperatorID, o.Name
                        FROM ZadaniaPrzypisani zp
                        LEFT JOIN operators o ON zp.OperatorID = o.ID
                        WHERE zp.ZadanieID = @id", conn);
                    cmd.Parameters.AddWithValue("@id", zadanieId);

                    wybraniPracownicy.Clear();
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            wybraniPracownicy.Add(new PracownikItem
                            {
                                Id = reader.GetString(0),
                                Nazwa = reader.IsDBNull(1) ? reader.GetString(0) : reader.GetString(1)
                            });
                        }
                    }
                }
                RefreshPracownicyList();
                UpdateWybraniPanel();
            }
            catch { }
        }

        private void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtTypZadania.Text))
            {
                MessageBox.Show("Wprowadź typ zadania.", "Walidacja", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtTypZadania.Focus();
                return;
            }

            if (!dpTermin.SelectedDate.HasValue)
            {
                MessageBox.Show("Wybierz termin wykonania.", "Walidacja", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (wybraniPracownicy.Count == 0)
            {
                MessageBox.Show("Wybierz przynajmniej jednego pracownika.", "Walidacja", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DateTime termin = dpTermin.SelectedDate.Value;
            if (TimeSpan.TryParse(txtGodzina.Text, out TimeSpan time))
            {
                termin = termin.Add(time);
            }
            else
            {
                termin = termin.AddHours(12);
            }

            int priorytet = rbWysoki.IsChecked == true ? 3 :
                           rbSredni.IsChecked == true ? 2 : 1;

            bool zespolowe = rbZespolowe.IsChecked == true;

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    int zadanieId;

                    if (isEditMode)
                    {
                        var cmd = new SqlCommand(@"
                            UPDATE Zadania SET
                                TypZadania = @typ,
                                Opis = @opis,
                                TerminWykonania = @termin,
                                Priorytet = @priorytet,
                                Zespolowe = @zespolowe
                            WHERE ID = @id", conn);

                        cmd.Parameters.AddWithValue("@typ", txtTypZadania.Text);
                        cmd.Parameters.AddWithValue("@opis", (object)txtOpis.Text ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@termin", termin);
                        cmd.Parameters.AddWithValue("@priorytet", priorytet);
                        cmd.Parameters.AddWithValue("@zespolowe", zespolowe);
                        cmd.Parameters.AddWithValue("@id", existingTask.Id);
                        cmd.ExecuteNonQuery();
                        zadanieId = existingTask.Id;

                        // Usuń stare przypisania
                        var delCmd = new SqlCommand("DELETE FROM ZadaniaPrzypisani WHERE ZadanieID = @id", conn);
                        delCmd.Parameters.AddWithValue("@id", zadanieId);
                        delCmd.ExecuteNonQuery();
                    }
                    else
                    {
                        var cmd = new SqlCommand(@"
                            INSERT INTO Zadania (OperatorID, TypZadania, Opis, TerminWykonania, Priorytet, Wykonane, Zespolowe)
                            OUTPUT INSERTED.ID
                            VALUES (@operator, @typ, @opis, @termin, @priorytet, 0, @zespolowe)", conn);

                        cmd.Parameters.AddWithValue("@operator", operatorId);
                        cmd.Parameters.AddWithValue("@typ", txtTypZadania.Text);
                        cmd.Parameters.AddWithValue("@opis", (object)txtOpis.Text ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@termin", termin);
                        cmd.Parameters.AddWithValue("@priorytet", priorytet);
                        cmd.Parameters.AddWithValue("@zespolowe", zespolowe);
                        zadanieId = (int)cmd.ExecuteScalar();
                    }

                    // Dodaj nowe przypisania
                    foreach (var pracownik in wybraniPracownicy)
                    {
                        var assignCmd = new SqlCommand(@"
                            INSERT INTO ZadaniaPrzypisani (ZadanieID, OperatorID, OperatorNazwa)
                            VALUES (@zadanieId, @opId, @opNazwa)", conn);
                        assignCmd.Parameters.AddWithValue("@zadanieId", zadanieId);
                        assignCmd.Parameters.AddWithValue("@opId", pracownik.Id);
                        assignCmd.Parameters.AddWithValue("@opNazwa", pracownik.Nazwa);
                        assignCmd.ExecuteNonQuery();
                    }
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas zapisywania: {ex.Message}\n\nUpewnij się, że tabela ZadaniaPrzypisani istnieje w bazie danych.", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    public class PracownikItem
    {
        public string Id { get; set; }
        public string Nazwa { get; set; }
    }
}
