using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Kalendarz1.MarketIntelligence.Models;
using Kalendarz1.MarketIntelligence.ViewModels;

namespace Kalendarz1.MarketIntelligence.Views
{
    public partial class PorannyBriefingWindow : Window
    {
        private PorannyBriefingViewModel _viewModel;

        public PorannyBriefingWindow()
        {
            InitializeComponent();
            _viewModel = DataContext as PorannyBriefingViewModel;
            Loaded += PorannyBriefingWindow_Loaded;
        }

        private void PorannyBriefingWindow_Loaded(object sender, RoutedEventArgs e)
        {
            BuildSummaryText();
            DrawPriceChart();
        }

        #region Summary Builder

        private void BuildSummaryText()
        {
            if (_viewModel == null || txtSummary == null) return;

            txtSummary.Inlines.Clear();

            foreach (var segment in _viewModel.SummarySegments)
            {
                var run = new Run(segment.Text);

                if (segment.Color != "default")
                {
                    try
                    {
                        run.Foreground = new SolidColorBrush(
                            (Color)ColorConverter.ConvertFromString(segment.Color));
                    }
                    catch
                    {
                        run.Foreground = new SolidColorBrush(Color.FromRgb(213, 204, 192));
                    }
                }
                else
                {
                    run.Foreground = new SolidColorBrush(Color.FromRgb(213, 204, 192));
                }

                if (segment.IsBold)
                {
                    run.FontWeight = FontWeights.SemiBold;
                }

                txtSummary.Inlines.Add(run);
            }
        }

        #endregion

        #region Chart Drawing

        private void DrawPriceChart()
        {
            if (canvasChart == null || _viewModel == null) return;

            canvasChart.Children.Clear();

            var skupIndicator = _viewModel.Indicators.Count > 0 ? _viewModel.Indicators[0] : null;
            if (skupIndicator?.SparkData == null || skupIndicator.SparkData.Length < 2) return;

            var data = skupIndicator.SparkData;
            double width = 320;
            double height = 80;
            double padding = 10;

            double minVal = double.MaxValue;
            double maxVal = double.MinValue;
            foreach (var val in data)
            {
                if (val < minVal) minVal = val;
                if (val > maxVal) maxVal = val;
            }
            double range = maxVal - minVal;
            if (range < 0.01) range = 0.1;

            minVal -= range * 0.1;
            maxVal += range * 0.1;
            range = maxVal - minVal;

            var points = new PointCollection();
            double stepX = (width - padding * 2) / (data.Length - 1);

            for (int i = 0; i < data.Length; i++)
            {
                double x = padding + i * stepX;
                double y = height - padding - ((data[i] - minVal) / range) * (height - padding * 2);
                points.Add(new Point(x, y));
            }

            var fillPoints = new PointCollection(points);
            fillPoints.Add(new Point(padding + (data.Length - 1) * stepX, height - padding));
            fillPoints.Add(new Point(padding, height - padding));

            var fillPolygon = new Polygon
            {
                Points = fillPoints,
                Fill = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(0, 1),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(Color.FromArgb(60, 201, 169, 110), 0),
                        new GradientStop(Color.FromArgb(0, 201, 169, 110), 1)
                    }
                }
            };
            canvasChart.Children.Add(fillPolygon);

            var polyline = new Polyline
            {
                Points = points,
                Stroke = new SolidColorBrush(Color.FromRgb(201, 169, 110)),
                StrokeThickness = 2,
                StrokeLineJoin = PenLineJoin.Round
            };
            canvasChart.Children.Add(polyline);

            if (points.Count > 0)
            {
                var lastPoint = points[points.Count - 1];
                var dot = new Ellipse
                {
                    Width = 8,
                    Height = 8,
                    Fill = new SolidColorBrush(Color.FromRgb(201, 169, 110)),
                    Stroke = new SolidColorBrush(Color.FromRgb(21, 18, 13)),
                    StrokeThickness = 2
                };
                Canvas.SetLeft(dot, lastPoint.X - 4);
                Canvas.SetTop(dot, lastPoint.Y - 4);
                canvasChart.Children.Add(dot);
            }

            var priceLabel = new TextBlock
            {
                Text = $"{data[data.Length - 1]:N2} zl",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(245, 237, 224))
            };
            Canvas.SetRight(priceLabel, 10);
            Canvas.SetTop(priceLabel, 10);
            canvasChart.Children.Add(priceLabel);
        }

        #endregion

        #region Event Handlers

        private void RoleButton_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag is string role)
            {
                _viewModel?.ChangeRoleCommand.Execute(role);
                // Force refresh of articles to update AI analysis
                icArticles?.Items.Refresh();
            }
        }

        private void BtnAdminPanel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var adminPanel = new AdminPanelWindow();
                adminPanel.Owner = this;
                var result = adminPanel.ShowDialog();

                // Jesli zapisano zmiany, odswiez konfiguracje
                if (result == true)
                {
                    MarketIntelligence.Config.ConfigService.Instance.Load();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad otwierania Panelu Administracyjnego:\n{ex.Message}", "Blad",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TasksButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel?.ToggleTasksPanelCommand.Execute(null);
        }

        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton btn && btn.Tag is string category)
            {
                var parent = btn.Parent as Panel;
                if (parent != null)
                {
                    foreach (var child in parent.Children)
                    {
                        if (child is ToggleButton otherBtn && otherBtn != btn)
                        {
                            otherBtn.IsChecked = false;
                        }
                    }
                }
                btn.IsChecked = true;

                _viewModel?.FilterByCategoryCommand.Execute(category);
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tb && _viewModel != null)
            {
                _viewModel.SearchText = tb.Text;
            }
        }

        private void Article_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is BriefingArticle article)
            {
                _viewModel?.ToggleArticleCommand.Execute(article);
            }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = e.Uri.AbsoluteUri,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error opening link: {ex.Message}");
            }
            e.Handled = true;
        }

        private void OpenUrl_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is string url && !string.IsNullOrEmpty(url))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error opening URL: {ex.Message}");
                }
            }
        }

        #endregion
    }

    #region Value Converters

    public class SeverityToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SeverityLevel severity)
            {
                return severity switch
                {
                    SeverityLevel.Critical => new SolidColorBrush(Color.FromRgb(192, 80, 80)),
                    SeverityLevel.Warning => new SolidColorBrush(Color.FromRgb(212, 160, 53)),
                    SeverityLevel.Positive => new SolidColorBrush(Color.FromRgb(109, 175, 109)),
                    SeverityLevel.Info => new SolidColorBrush(Color.FromRgb(90, 143, 192)),
                    _ => new SolidColorBrush(Color.FromRgb(90, 143, 192))
                };
            }
            return new SolidColorBrush(Color.FromRgb(90, 143, 192));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class SeverityToBgConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SeverityLevel severity)
            {
                return severity switch
                {
                    SeverityLevel.Critical => new SolidColorBrush(Color.FromRgb(28, 18, 16)),
                    SeverityLevel.Warning => new SolidColorBrush(Color.FromRgb(28, 24, 8)),
                    SeverityLevel.Positive => new SolidColorBrush(Color.FromRgb(16, 28, 16)),
                    SeverityLevel.Info => new SolidColorBrush(Color.FromRgb(16, 21, 32)),
                    _ => new SolidColorBrush(Color.FromRgb(16, 21, 32))
                };
            }
            return new SolidColorBrush(Color.FromRgb(16, 21, 32));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class DirectionToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is PriceDirection direction)
            {
                return direction switch
                {
                    PriceDirection.Up => new SolidColorBrush(Color.FromRgb(109, 175, 109)),
                    PriceDirection.Down => new SolidColorBrush(Color.FromRgb(192, 80, 80)),
                    PriceDirection.Stable => new SolidColorBrush(Color.FromRgb(122, 111, 99)),
                    _ => new SolidColorBrush(Color.FromRgb(122, 111, 99))
                };
            }
            return new SolidColorBrush(Color.FromRgb(122, 111, 99));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                return b ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Multi-value converter for role-based AI analysis.
    /// Values[0] = BriefingArticle, Values[1] = UserRole
    /// Parameter = "Analysis" or "Actions" or "Label"
    /// </summary>
    public class RoleBasedAnalysisConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2) return string.Empty;

            var article = values[0] as BriefingArticle;
            var role = values[1] is UserRole r ? r : UserRole.CEO;
            var type = parameter as string ?? "Analysis";

            if (article == null) return string.Empty;

            return type switch
            {
                "Analysis" => role switch
                {
                    UserRole.CEO => article.AiAnalysisCeo ?? "",
                    UserRole.Sales => article.AiAnalysisSales ?? "",
                    UserRole.Buyer => article.AiAnalysisBuyer ?? "",
                    _ => article.AiAnalysisCeo ?? ""
                },
                "Actions" => role switch
                {
                    UserRole.CEO => article.RecommendedActionsCeo ?? "",
                    UserRole.Sales => article.RecommendedActionsSales ?? "",
                    UserRole.Buyer => article.RecommendedActionsBuyer ?? "",
                    _ => article.RecommendedActionsCeo ?? ""
                },
                "Label" => role switch
                {
                    UserRole.CEO => " — CEO / Strategia",
                    UserRole.Sales => " — Handlowiec",
                    UserRole.Buyer => " — Zakupowiec",
                    _ => " — CEO"
                },
                _ => ""
            };
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Converts boolean to green (true) or red (false) color
    /// </summary>
    public class BoolToGreenRedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b)
            {
                return new SolidColorBrush(Color.FromRgb(109, 175, 109)); // Green
            }
            return new SolidColorBrush(Color.FromRgb(192, 80, 80)); // Red
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    #endregion
}
