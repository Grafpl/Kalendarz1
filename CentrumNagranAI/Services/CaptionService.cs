using System.Threading;
using System.Threading.Tasks;

namespace Kalendarz1.CentrumNagranAI.Services
{
    /// <summary>
    /// Generuje krótki opis (caption) klatki dla potrzeb wyszukiwania.
    /// Używa Claude Haiku — krótki prompt, max ~150 tokenów wyniku, ~$0.001 per klatka.
    ///
    /// Caption + jego embedding (OpenAI) tworzą "rdzeń" indeksu wyszukiwania.
    /// Cosine na embedingach robi prefilter (lokalnie, bezpłatnie), VLM rerank
    /// dopiero na top-K kandydatach.
    /// </summary>
    public static class CaptionService
    {
        private const string Prompt =
            "Opisz po polsku tę klatkę z kamery przemysłowej zakładu drobiarskiego (ubojnia kurczaków). " +
            "Wymień DOSŁOWNIE co widzisz: pomieszczenie, obiekty, ludzi, pojazdy, urządzenia, czynności. " +
            "Maks. 2 zdania, konkretne, słowa kluczowe. Bez ozdobników, bez 'na zdjęciu widzimy', " +
            "od razu treść. Po polsku.";

        public static async Task<(string Caption, double CostUsd)> CaptionAsync(string jpegPath, CancellationToken ct = default)
        {
            var result = await VlmClient.AnalyzeImageAsync(
                jpegPath, Prompt,
                model: VlmClient.ModelHaiku,
                maxTokens: 200,
                ct: ct);
            return (result.Text.Trim(), result.CostUsd);
        }
    }
}
