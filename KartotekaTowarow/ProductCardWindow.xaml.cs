using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Kalendarz1.KartotekaTowarow
{
    public partial class ProductCardWindow : Window
    {
        private readonly ArticleModel _article;

        public ProductCardWindow(ArticleModel article)
        {
            InitializeComponent();
            _article = article;
            PopulateCard();
        }

        private void PopulateCard()
        {
            var a = _article;

            // Title
            TxtCardName.Text = a.Name ?? "(brak nazwy)";
            TxtCardId.Text = $"ID: {a.ID}   |   Skrot: {a.ShortName ?? "-"}   |   GUID: {a.GUID}";

            // Status badges
            BadgeCardHalt.Visibility = a.Halt == 1 ? Visibility.Visible : Visibility.Collapsed;
            if (a.isStandard == 1)
            {
                BadgeCardStandard.Visibility = Visibility.Visible;
                TxtCardStandard.Text = "STANDARD";
            }

            // Dane podstawowe
            TxtCardJM.Text = a.JM ?? "-";
            TxtCardGrupa.Text = a.Grupa?.ToString() ?? "-";
            TxtCardRodzaj.Text = a.Rodzaj switch
            {
                0 => "Mieso",
                1 => "Podroby",
                2 => "Odpady",
                _ => a.Rodzaj?.ToString() ?? "-"
            };
            TxtCardWRC.Text = a.WRC?.ToString("N2") ?? "-";
            TxtCardWydajnosc.Text = a.Wydajnosc?.ToString("N1") ?? "-";
            TxtCardPrzelicznik.Text = a.Przelicznik?.ToString("N2") ?? "-";

            // Ceny
            TxtCardCena1.Text = a.Cena1?.ToString("N2") ?? "-";
            TxtCardCena2.Text = a.Cena2?.ToString("N2") ?? "-";
            TxtCardCena3.Text = a.Cena3?.ToString("N2") ?? "-";

            // Etykieta
            TxtCardDuration.Text = a.Duration != null ? $"{a.Duration} dni" : "-";
            TxtCardTemp.Text = a.TempOfStorage ?? "-";

            var ingredients = new[] { a.Ingredients1, a.Ingredients2, a.Ingredients3, a.Ingredients4,
                                      a.Ingredients5, a.Ingredients6, a.Ingredients7, a.Ingredients8 }
                .Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            TxtCardSklad.Text = ingredients.Count > 0 ? string.Join(", ", ingredients) : "-";

            // Standard
            if (a.isStandard == 1)
            {
                PanelCardStandard.Visibility = Visibility.Visible;
                TxtCardStdWeight.Text = $"{a.StandardWeight?.ToString("N2") ?? "-"} kg";
                TxtCardTolPlus.Text = $"+{a.StandardTol?.ToString("N2") ?? "0"} kg";
                TxtCardTolMinus.Text = $"-{a.StandardTolMinus?.ToString("N2") ?? "0"} kg";
            }

            // Powiazania
            var related = new[] { a.RELATED_ID1, a.RELATED_ID2, a.RELATED_ID3 }
                .Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            if (related.Count > 0)
            {
                PanelCardRelated.Visibility = Visibility.Visible;
                TxtCardRelated.Text = string.Join(", ", related);
            }

            // Footer
            TxtCardCreated.Text = a.CreateData != null
                ? $"Utworzony: {a.CreateData} {a.CreateGodzina}  |  Zmodyfikowany: {a.ModificationData} {a.ModificationGodzina}"
                : "";
            TxtCardPrintDate.Text = $"Wydrukowano: {DateTime.Now:yyyy-MM-dd HH:mm}";
        }

        private void BtnPrint_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new System.Windows.Controls.PrintDialog();
                if (dlg.ShowDialog() == true)
                {
                    dlg.PrintVisual(PrintArea, $"Karta produktu - {_article.ID}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad drukowania:\n{ex.Message}", "Blad",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
