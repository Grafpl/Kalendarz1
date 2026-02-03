using System.Windows;
using Kalendarz1.MarketIntelligence.Models;

namespace Kalendarz1.MarketIntelligence.Views
{
    public partial class ArticleDetailWindow : Window
    {
        public ArticleDetailWindow(IntelArticle article)
        {
            InitializeComponent();
            LoadArticle(article);
        }

        private void LoadArticle(IntelArticle article)
        {
            Title = article.Title;
            txtSeverityIcon.Text = article.SeverityIcon;
            txtCategory.Text = article.CategoryBadge;
            txtTitle.Text = article.Title;
            txtSource.Text = article.Source ?? "Źródło nieznane";
            txtDate.Text = article.FormattedDate;
            txtBody.Text = article.Body ?? "Brak treści artykułu.";

            if (!string.IsNullOrEmpty(article.AiAnalysis))
            {
                txtAiAnalysis.Text = article.AiAnalysis;
                pnlAiAnalysis.Visibility = Visibility.Visible;
            }
            else
            {
                pnlAiAnalysis.Visibility = Visibility.Collapsed;
            }

            if (!string.IsNullOrEmpty(article.Tags))
            {
                txtTags.Text = $"Tagi: {article.Tags}";
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
