using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using Kalendarz1.MarketIntelligence.Models;
using Kalendarz1.MarketIntelligence.Services;

namespace Kalendarz1.MarketIntelligence.Views
{
    /// <summary>Szczegóły wątku (Faza A): digest + business impact + artykuły źródłowe.</summary>
    public partial class StoryDetailWindow : Window
    {
        private readonly IntelStory _story;

        public StoryDetailWindow(IntelStory story)
        {
            InitializeComponent();
            _story = story;
            Loaded += async (_, __) => await LoadAsync();
        }

        private async System.Threading.Tasks.Task LoadAsync()
        {
            txtTitle.Text = $"{_story.TypeEmoji} {_story.Title}";
            txtMeta.Text = $"{_story.SeverityIcon} {_story.AgeDisplay} · 📎 {_story.ArticleCount} art. · ostatnia aktualizacja: {_story.LastUpdatedDisplay}";
            txtDigest.Text = string.IsNullOrWhiteSpace(_story.LastDigest)
                ? "(Digest jeszcze nie wygenerowany. Pojawi się po pełnym pobraniu, gdy wątek dojrzeje.)"
                : _story.LastDigest;

            if (_story.HasBusinessImpact)
            {
                impactBox.Visibility = Visibility.Visible;
                txtImpact.Text = _story.BusinessImpact;
            }

            try
            {
                var svc = new StoriesService();
                var articles = await svc.GetStoryArticlesAsync(_story.Id);
                lstArticles.ItemsSource = articles;
            }
            catch (Exception ex) { Debug.WriteLine($"[StoryDetail] {ex.Message}"); }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try { Process.Start(new ProcessStartInfo { FileName = e.Uri.AbsoluteUri, UseShellExecute = true }); }
            catch (Exception ex) { Debug.WriteLine($"[StoryDetail] link: {ex.Message}"); }
            e.Handled = true;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
