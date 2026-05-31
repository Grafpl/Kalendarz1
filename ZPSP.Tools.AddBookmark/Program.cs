using System;
using System.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

// ════════════════════════════════════════════════════════════════════════════
// ZPSP.Tools.AddBookmark — jednorazowy CLI do szablonu umowy kontraktacji.
//
//  Tryb 1 (domyślny) — wstaw bookmark:
//    dotnet run --project ZPSP.Tools.AddBookmark -- "<docx>" ["tekst nagłówka"]
//    Wstawia BMK_HARMONOGRAM wokół tabeli po nagłówku (domyślnie "HARMONOGRAM WSTAWIEŃ I ODBIORÓW").
//
//  Tryb 2 — inspekcja (read-only): pokazuje akapity + tabele + tokeny, żeby dopasować generator:
//    dotnet run --project ZPSP.Tools.AddBookmark -- --inspect "<docx>"
//
// Idempotentny: jeśli BMK_HARMONOGRAM już jest — nic nie zmienia.
// ════════════════════════════════════════════════════════════════════════════

const string DOMYSLNY_NAGLOWEK = "HARMONOGRAM WSTAWIEŃ I ODBIORÓW";
const string NAZWA_BM = "BMK_HARMONOGRAM";

if (args.Length >= 1 && args[0].Equals("--inspect", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 2) { Console.Error.WriteLine("Użycie: AddBookmark --inspect <docx>"); return 2; }
    return Inspect(args[1]);
}

if (args.Length < 1) { Console.Error.WriteLine("Użycie: AddBookmark <docx> [nagłówek]  |  AddBookmark --inspect <docx>"); return 2; }
return WstawBookmark(args[0], args.Length >= 2 ? args[1] : DOMYSLNY_NAGLOWEK);


int WstawBookmark(string sciezka, string naglowek)
{
    if (!System.IO.File.Exists(sciezka)) { Console.Error.WriteLine($"Nie znaleziono pliku: {sciezka}"); return 2; }
    try
    {
        using var doc = WordprocessingDocument.Open(sciezka, isEditable: true);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body is null) { Console.Error.WriteLine("Pusty dokument."); return 3; }

        if (body.Descendants<BookmarkStart>().Any(b => b.Name?.Value == NAZWA_BM))
        { Console.WriteLine($"ℹ️ Bookmark {NAZWA_BM} już istnieje — pominięto."); return 0; }

        var bloki = body.Elements().ToList();
        int idx = bloki.FindIndex(el => el is Paragraph && (el.InnerText ?? "").IndexOf(naglowek, StringComparison.OrdinalIgnoreCase) >= 0);
        if (idx < 0) { Console.Error.WriteLine($"Nie znaleziono nagłówka '{naglowek}'. Sprawdź --inspect i podaj dokładny tekst jako 2. arg."); return 4; }

        var tabela = bloki.Skip(idx + 1).OfType<Table>().FirstOrDefault();
        if (tabela is null) { Console.Error.WriteLine($"Po nagłówku '{naglowek}' nie ma tabeli."); return 5; }

        int nextId = 1;
        var ids = body.Descendants<BookmarkStart>().Select(b => int.TryParse(b.Id?.Value, out var n) ? n : 0).ToList();
        if (ids.Count > 0) nextId = ids.Max() + 1;
        string bmId = nextId.ToString();

        tabela.InsertBeforeSelf(new BookmarkStart { Name = NAZWA_BM, Id = bmId });
        tabela.InsertAfterSelf(new BookmarkEnd { Id = bmId });
        doc.MainDocumentPart!.Document.Save();

        Console.WriteLine($"✅ Wstawiono bookmark {NAZWA_BM} (Id={bmId}) wokół tabeli po nagłówku '{naglowek}'.");
        return 0;
    }
    catch (Exception ex) { Console.Error.WriteLine("Błąd: " + ex.Message); return 1; }
}

int Inspect(string sciezka)
{
    if (!System.IO.File.Exists(sciezka)) { Console.Error.WriteLine($"Nie znaleziono pliku: {sciezka}"); return 2; }
    try
    {
        using var doc = WordprocessingDocument.Open(sciezka, isEditable: false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body is null) { Console.Error.WriteLine("Pusty dokument."); return 3; }

        Console.WriteLine("── AKAPITY (niepuste) ──");
        foreach (var p in body.Elements<Paragraph>())
        {
            var t = (p.InnerText ?? "").Trim();
            if (t.Length > 0) Console.WriteLine("  P: " + (t.Length > 120 ? t.Substring(0, 120) + "…" : t));
        }

        int ti = 0;
        foreach (var tab in body.Descendants<Table>())
        {
            Console.WriteLine($"── TABELA #{ti++} ──");
            int ri = 0;
            foreach (var row in tab.Elements<TableRow>())
            {
                var cells = row.Elements<TableCell>().Select(c => (c.InnerText ?? "").Trim());
                Console.WriteLine($"  R{ri++}: " + string.Join(" | ", cells));
                if (ri >= 3) { Console.WriteLine("  …(więcej wierszy)"); break; }
            }
        }
        Console.WriteLine("\nWskazówka: nagłówek tabeli → 2. arg do trybu wstawiania; tokeny w wierszu (np. [Cykl_Nr]) → dopasuj do FillScheduleTable.");
        return 0;
    }
    catch (Exception ex) { Console.Error.WriteLine("Błąd: " + ex.Message); return 1; }
}
