using System;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Kalendarz1
{
    // ============================================================================
    // UI/UX Polish — status bar, dynamiczny tytuł, empty state, hover effect
    // ============================================================================

    public partial class WidokWstawienia
    {
        // Wywoływane po każdej operacji która zmienia liczbę wierszy
        internal void OdswiezStatusBar()
        {
            try
            {
                int liczbaWstawien = 0;
                int liczbaPrzypomnien = 0;
                int liczbaNadchodzacych = 0;
                int liczbaHistoria = 0;

                if (dataGridWstawienia?.ItemsSource is DataView dvW)
                    liczbaWstawien = dvW.Count;

                if (dataGridPrzypomnienia?.ItemsSource is DataView dvP)
                    liczbaPrzypomnien = dvP.Count;

                if (dataGridDoPotwierdzenia?.ItemsSource is DataView dvN)
                    liczbaNadchodzacych = dvN.Count;

                if (dataGridHistoria?.ItemsSource is DataView dvH)
                    liczbaHistoria = dvH.Count;

                int liczbaStalych = _staliKlienci?.Count ?? 0;

                if (statusWstawienia != null)
                    statusWstawienia.Text = $"Wstawień: {liczbaWstawien:N0}";
                if (statusPrzypomnienia != null)
                    statusPrzypomnienia.Text = $"Przypomnień: {liczbaPrzypomnien:N0}";
                if (statusNadchodzace != null)
                    statusNadchodzace.Text = $"Nadchodzących: {liczbaNadchodzacych:N0}";
                if (statusHistoria != null)
                    statusHistoria.Text = $"Historia (90d): {liczbaHistoria:N0}";
                if (statusStali != null)
                    statusStali.Text = $"Stałych: {liczbaStalych:N0}";

                if (statusUser != null)
                {
                    string user = App.UserFullName ?? App.UserID ?? "";
                    statusUser.Text = $"Zalogowany: {user}   •   {DateTime.Now:HH:mm}";
                }

                // Dynamiczny tytuł okna — pokazuje liczniki w pasku zadań/Alt+Tab
                this.Title = $"🐔 Wstawienia Kurczaków — ⏰ {liczbaPrzypomnien} przypomnień, 📞 {liczbaNadchodzacych} nadchodzących";

                // Empty state — jeśli grid pusty, pokaż placeholder
                AktualizujEmptyState(dataGridWstawienia, liczbaWstawien, "📭 Brak wstawień. Naciśnij Ctrl+N żeby dodać.");
                AktualizujEmptyState(dataGridPrzypomnienia, liczbaPrzypomnien, "✅ Brak przypomnień — wszystko pod kontrolą!");
                AktualizujEmptyState(dataGridDoPotwierdzenia, liczbaNadchodzacych, "📭 Brak nadchodzących wstawień do potwierdzenia w najbliższych dniach.");
                AktualizujEmptyState(dataGridHistoria, liczbaHistoria, "📭 Brak historii kontaktów w ostatnich 90 dniach.");
            }
            catch { /* polish nie może blokować */ }
        }

        // Empty state - pokazuje wycentrowany komunikat w pustym DataGrid
        private void AktualizujEmptyState(DataGrid grid, int rowCount, string komunikat)
        {
            if (grid == null) return;

            // Szukamy nadrzędnego Border (zawiera grid)
            if (grid.Parent is not Border parentBorder) return;

            // Sprawdzamy czy istnieje już overlay placeholder
            if (parentBorder.Parent is Grid hostGrid)
            {
                TextBlock? overlay = null;
                string overlayName = "emptyOverlay_" + grid.Name;

                foreach (var child in hostGrid.Children)
                {
                    if (child is TextBlock tb && tb.Name == overlayName)
                    {
                        overlay = tb;
                        break;
                    }
                }

                if (rowCount == 0)
                {
                    if (overlay == null)
                    {
                        overlay = new TextBlock
                        {
                            Name = overlayName,
                            Text = komunikat,
                            FontSize = 11,
                            FontStyle = FontStyles.Italic,
                            Foreground = new SolidColorBrush(Color.FromRgb(0x95, 0xA5, 0xA6)),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center,
                            TextWrapping = TextWrapping.Wrap,
                            MaxWidth = 280,
                            TextAlignment = TextAlignment.Center,
                            IsHitTestVisible = false
                        };
                        Grid.SetRow(overlay, Grid.GetRow(parentBorder));
                        Grid.SetColumn(overlay, Grid.GetColumn(parentBorder));
                        Grid.SetRowSpan(overlay, Math.Max(1, Grid.GetRowSpan(parentBorder)));
                        Grid.SetColumnSpan(overlay, Math.Max(1, Grid.GetColumnSpan(parentBorder)));
                        Panel.SetZIndex(overlay, 50);
                        hostGrid.Children.Add(overlay);
                    }
                    else
                    {
                        overlay.Text = komunikat;
                        overlay.Visibility = Visibility.Visible;
                    }
                }
                else if (overlay != null)
                {
                    overlay.Visibility = Visibility.Collapsed;
                }
            }
        }
    }
}
