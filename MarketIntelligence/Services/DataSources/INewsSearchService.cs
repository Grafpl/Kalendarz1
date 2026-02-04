using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Kalendarz1.MarketIntelligence.Services.DataSources
{
    /// <summary>
    /// Interfejs dla serwisów wyszukiwania wiadomości.
    /// Umożliwia łatwą wymianę providera (Bing, Perplexity, Google News itp.)
    /// </summary>
    public interface INewsSearchService
    {
        /// <summary>
        /// Czy serwis jest poprawnie skonfigurowany (ma klucz API)
        /// </summary>
        bool IsConfigured { get; }

        /// <summary>
        /// Podgląd klucza API (pierwsze znaki + ...)
        /// </summary>
        string ApiKeyPreview { get; }

        /// <summary>
        /// Testuje połączenie z API
        /// </summary>
        Task<(bool Success, string Message)> TestConnectionAsync(CancellationToken ct = default);

        /// <summary>
        /// Wyszukuje artykuły dla podanego zapytania
        /// </summary>
        Task<List<NewsArticle>> SearchAsync(string query, CancellationToken ct = default);

        /// <summary>
        /// Wyszukuje z informacjami debugowymi
        /// </summary>
        Task<(List<NewsArticle> Articles, string DebugInfo)> SearchWithDebugAsync(string query, CancellationToken ct = default);

        /// <summary>
        /// Pobiera wszystkie wiadomości dla predefiniowanych zapytań branżowych
        /// </summary>
        Task<List<NewsArticle>> FetchAllNewsAsync(
            IProgress<(int completed, int total, string query)> progress = null,
            bool quickMode = false,
            CancellationToken ct = default);

        /// <summary>
        /// Zwraca listę predefiniowanych zapytań (pełny tryb)
        /// </summary>
        List<string> GetAllQueries();

        /// <summary>
        /// Zwraca listę zapytań dla trybu szybkiego
        /// </summary>
        List<string> GetQuickQueries();

        /// <summary>
        /// Szacowany koszt zapytania (USD)
        /// </summary>
        decimal EstimateCost(int queryCount);
    }

    /// <summary>
    /// Uniwersalny model artykułu z wyszukiwarki
    /// </summary>
    public class NewsArticle
    {
        public string Title { get; set; }
        public string Snippet { get; set; }
        public string Source { get; set; }
        public string Url { get; set; }
        public DateTime? PublishedDate { get; set; }
        public string ThumbnailUrl { get; set; }
        public string Category { get; set; }
    }
}
