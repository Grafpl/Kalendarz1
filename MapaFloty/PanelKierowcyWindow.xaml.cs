using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Kalendarz1.MapaFloty
{
    public partial class PanelKierowcyWindow : Window
    {
        private readonly List<MapaFlotyView.VehiclePosition> _vehicles;

        public PanelKierowcyWindow(List<MapaFlotyView.VehiclePosition> vehicles)
        {
            InitializeComponent();
            _vehicles = vehicles;
            try { WindowIconHelper.SetIcon(this); } catch { }
            Loaded += (_, _) => BuildDriverPanel();
        }

        private void BuildDriverPanel()
        {
            // Grupuj po kierowcy
            var groups = _vehicles
                .Where(v => v.Driver != "—" && !string.IsNullOrWhiteSpace(v.Driver))
                .GroupBy(v => v.Driver)
                .OrderByDescending(g => g.Any(v => v.IsMoving))
                .ThenBy(g => g.Key)
                .ToList();

            TotalDriversText.Text = groups.Count.ToString();
            ActiveDriversText.Text = groups.Count(g => g.Any(v => v.IsMoving)).ToString();
            var allSpeeds = _vehicles.Where(v => v.IsMoving).Select(v => v.Speed).ToList();
            TotalSpeedText.Text = allSpeeds.Count > 0 ? $"{(int)allSpeeds.Average()}" : "0";
            MaxSpeedText.Text = allSpeeds.Count > 0 ? allSpeeds.Max().ToString() : "0";

            DriverListPanel.Children.Clear();
            foreach (var g in groups)
                DriverListPanel.Children.Add(CreateDriverCard(g.Key, g.ToList()));
        }

        private Border CreateDriverCard(string driverName, List<MapaFlotyView.VehiclePosition> vehicles)
        {
            var isActive = vehicles.Any(v => v.IsMoving);
            var mainVehicle = vehicles.OrderByDescending(v => v.IsMoving).ThenByDescending(v => v.Speed).First();
            var statusColor = isActive ? Color.FromRgb(46, 125, 50) : Color.FromRgb(120, 144, 156);
            var statusBrush = new SolidColorBrush(statusColor);

            var card = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(0, 0, 0, 8),
                Padding = new Thickness(16, 12, 16, 12),
                BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 228)),
                BorderThickness = new Thickness(1)
            };

            var mainGrid = new Grid();
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // ── Kolumna 1: Kierowca + status ──
            var col1 = new StackPanel();
            col1.Children.Add(new TextBlock
            {
                Text = driverName, FontSize = 15, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(38, 50, 56))
            });

            var statusText = isActive ? "W trasie" : "Postój / Nieaktywny";
            var pill = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(30, statusColor.R, statusColor.G, statusColor.B)),
                CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 3, 8, 3),
                HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 0)
            };
            pill.Child = new TextBlock
            {
                Text = statusText, Foreground = statusBrush,
                FontSize = 11, FontWeight = FontWeights.SemiBold
            };
            col1.Children.Add(pill);

            // Pojazdy kierowcy
            foreach (var v in vehicles)
            {
                col1.Children.Add(new TextBlock
                {
                    Text = $"Pojazd: {v.ObjectName}" + (!string.IsNullOrEmpty(v.InternalName) ? $" ({v.InternalName})" : ""),
                    FontSize = 10.5, Foreground = new SolidColorBrush(Color.FromRgb(96, 125, 139)),
                    Margin = new Thickness(0, 3, 0, 0)
                });
            }
            Grid.SetColumn(col1, 0);
            mainGrid.Children.Add(col1);

            // ── Kolumna 2: Statystyki jazdy ──
            var col2 = new StackPanel { Margin = new Thickness(16, 0, 0, 0) };

            void AddStat(string label, string value, Color color)
            {
                var row = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
                var valBlock = new TextBlock
                {
                    Text = value, FontWeight = FontWeights.Bold, FontSize = 13,
                    Foreground = new SolidColorBrush(color),
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                DockPanel.SetDock(valBlock, Dock.Right);
                row.Children.Add(valBlock);
                row.Children.Add(new TextBlock
                {
                    Text = label, FontSize = 10.5,
                    Foreground = new SolidColorBrush(Color.FromRgb(120, 144, 156))
                });
                col2.Children.Add(row);
            }

            AddStat("Aktualna prędkość", $"{mainVehicle.Speed} km/h",
                mainVehicle.Speed > 90 ? Color.FromRgb(198, 40, 40) : Color.FromRgb(38, 50, 56));
            AddStat("Do ubojni Koziołki", $"{mainVehicle.DistToUbojnia:F1} km", Color.FromRgb(38, 50, 56));
            if (mainVehicle.EtaMinutes > 0)
                AddStat("Szac. czas dojazdu", $"ok. {mainVehicle.EtaMinutes} min", Color.FromRgb(21, 101, 192));
            AddStat("Ostatni sygnał GPS", !string.IsNullOrEmpty(mainVehicle.LastUpdate) ? mainVehicle.LastUpdate : "brak", Color.FromRgb(120, 144, 156));

            Grid.SetColumn(col2, 1);
            mainGrid.Children.Add(col2);

            // ── Kolumna 3: Lokalizacja + kurs ──
            var col3 = new StackPanel { Margin = new Thickness(16, 0, 0, 0) };

            if (!string.IsNullOrWhiteSpace(mainVehicle.Address))
            {
                col3.Children.Add(new TextBlock
                {
                    Text = mainVehicle.Address,
                    FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(38, 50, 56)),
                    TextWrapping = TextWrapping.Wrap
                });
            }

            if (mainVehicle.InGeofence)
            {
                var geoLabel = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(255, 235, 238)),
                    CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 3, 8, 3),
                    Margin = new Thickness(0, 4, 0, 0), HorizontalAlignment = HorizontalAlignment.Left
                };
                geoLabel.Child = new TextBlock
                {
                    Text = "W STREFIE ŁYSZKOWICE",
                    Foreground = new SolidColorBrush(Color.FromRgb(198, 40, 40)),
                    FontSize = 10, FontWeight = FontWeights.Bold
                };
                col3.Children.Add(geoLabel);
            }

            if (!string.IsNullOrEmpty(mainVehicle.KursTrasa))
            {
                var kursBox = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(232, 234, 246)),
                    CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 6, 10, 6),
                    Margin = new Thickness(0, 6, 0, 0)
                };
                var kursStack = new StackPanel();
                kursStack.Children.Add(new TextBlock
                {
                    Text = $"Kurs: {mainVehicle.KursTrasa}", FontSize = 11, FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(40, 53, 147))
                });
                var timeStr = "";
                if (!string.IsNullOrEmpty(mainVehicle.KursGodzWyjazdu)) timeStr = mainVehicle.KursGodzWyjazdu;
                if (!string.IsNullOrEmpty(mainVehicle.KursGodzPowrotu)) timeStr += $" — {mainVehicle.KursGodzPowrotu}";
                if (!string.IsNullOrEmpty(timeStr))
                {
                    kursStack.Children.Add(new TextBlock
                    {
                        Text = timeStr, FontSize = 10,
                        Foreground = new SolidColorBrush(Color.FromRgb(57, 73, 171))
                    });
                }
                var statusKursColor = mainVehicle.KursStatus == "Planowany" ? Color.FromRgb(230, 81, 0) :
                    mainVehicle.KursStatus == "W realizacji" ? Color.FromRgb(46, 125, 50) : Color.FromRgb(120, 144, 156);
                kursStack.Children.Add(new TextBlock
                {
                    Text = $"Status: {mainVehicle.KursStatus}", FontSize = 10, FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(statusKursColor), Margin = new Thickness(0, 2, 0, 0)
                });
                kursBox.Child = kursStack;
                col3.Children.Add(kursBox);
            }

            Grid.SetColumn(col3, 2);
            mainGrid.Children.Add(col3);

            card.Child = mainGrid;
            return card;
        }

        private void BtnExportPdf_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var pdf = RaportFlotyPDF.Generuj(_vehicles, "Raport Kierowców Floty");
                var path = Path.Combine(Path.GetTempPath(), $"RaportKierowcow_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
                File.WriteAllBytes(path, pdf);
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd generowania PDF:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
