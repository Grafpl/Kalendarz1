// ========================================================================
// PRZYKŁADY UŻYCIA - OcenaPDFGenerator.cs
// Kompletny przewodnik jak używać nowego generatora PDF
// ========================================================================

using Kalendarz1;
using System;

namespace PrzykladyUzycia
{
    public class PrzykladyGeneratoraPDF
    {
        // ====================================================================
        // PRZYKŁAD 1: Generowanie PUSTEGO formularza dla hodowcy
        // ====================================================================
        // Użyj tego gdy chcesz wydrukować formularz dla hodowcy do wypełnienia
        
        public void PrzykladPustyFormularz()
        {
            var generator = new OcenaPDFGenerator();
            
            generator.GenerujPdf(
                sciezkaDoPliku: @"C:\Formularze\OcenaDostawcy_Pusty_Kowalski.pdf",
                numerRaportu: "",              // Pusty dla formularza do wypełnienia
                dataOceny: DateTime.Now,       // Lub konkretna data
                dostawcaNazwa: "Jan Kowalski - Ferma Drobiu",
                dostawcaId: "DOW-001",
                samoocena: null,               // null = puste pola
                listaKontrolna: null,          // null = puste pola
                dokumentacja: false,           // false = puste pole
                p1_5: 0,                       // 0 dla pustego formularza
                p6_20: 0,                      // 0 dla pustego formularza
                pRazem: 0,                     // 0 dla pustego formularza
                uwagi: "",                     // Pusty string = puste linie
                czyPustyFormularz: true        // ⚠️ WAŻNE: true = formularz do wydruku!
            );
            
            Console.WriteLine("✅ Wygenerowano pusty formularz do wydruku!");
            Console.WriteLine("📄 Hodowca może teraz wypełnić go ręcznie.");
        }

        // ====================================================================
        // PRZYKŁAD 2: Generowanie WYPEŁNIONEGO raportu z systemu
        // ====================================================================
        // Użyj tego gdy dane są już wprowadzone do systemu i chcesz raport
        
        public void PrzykladWypelnionyRaport()
        {
            var generator = new OcenaPDFGenerator();
            
            // Przykładowe odpowiedzi (true = TAK, false = NIE)
            
            // Sekcja I - Samoocena (5 pytań, po 3 pkt)
            bool[] samoocena = new bool[]
            {
                true,   // Pyt. 1: Gospodarstwo w PIW - TAK (3 pkt)
                true,   // Pyt. 2: Miejsce na dezynfekcję - TAK (3 pkt)
                true,   // Pyt. 3: Wywóz obornika - TAK (3 pkt)
                true,   // Pyt. 4: Przechowywanie leków - TAK (3 pkt)
                false   // Pyt. 5: Uporządkowany teren - NIE (0 pkt)
            };
            // Suma Sekcja I: 12 punktów (4 × 3)
            
            // Sekcja II - Lista kontrolna (25 pytań, po 1 pkt)
            bool[] listaKontrolna = new bool[]
            {
                // Część A - Hodowca (pytania 6-10)
                true,   // Pyt. 6: Odzież/obuwie - TAK (1 pkt)
                true,   // Pyt. 7: Maty dezynfekcyjne - TAK (1 pkt)
                true,   // Pyt. 8: Środki dezynfekcyjne - TAK (1 pkt)
                true,   // Pyt. 9: Numer WNI - TAK (1 pkt)
                true,   // Pyt. 10: Opieka weterynaryjna - TAK (1 pkt)
                
                // Część B - Kierowca (pytania 11-15)
                true,   // Pyt. 11: Wolne od salmonelli - TAK (1 pkt)
                true,   // Pyt. 12: Mycie kurnika - TAK (1 pkt)
                true,   // Pyt. 13: Usuwanie padłych - TAK (1 pkt)
                false,  // Pyt. 14: Godzina przyjazdu - NIE (0 pkt)
                true,   // Pyt. 15: Godzina załadunku - TAK (1 pkt)
                
                // Część C - Infrastruktura (pytania 16-20)
                true,   // Pyt. 16: Wjazd utwardzony - TAK (1 pkt)
                true,   // Pyt. 17: Wjazd oświetlony - TAK (1 pkt)
                true,   // Pyt. 18: Podjazd oświetlony - TAK (1 pkt)
                true,   // Pyt. 19: Podjazd utwardzony - TAK (1 pkt)
                true,   // Pyt. 20: Dostosowanie do wózka - TAK (1 pkt)
                
                // Część D - Stan ptaków (pytania 21-25)
                true,   // Pyt. 21: Oznaczenie kurników - TAK (1 pkt)
                true,   // Pyt. 22: Niebieskie światło - TAK (1 pkt)
                true,   // Pyt. 23: Sucha ściółka - TAK (1 pkt)
                true,   // Pyt. 24: Czyste kury - TAK (1 pkt)
                true,   // Pyt. 25: Suche kury - TAK (1 pkt)
                
                // Część E - Załadunek (pytania 26-30)
                true,   // Pyt. 26: Puste kurniki - TAK (1 pkt)
                true,   // Pyt. 27: Technika łapania - TAK (1 pkt)
                true,   // Pyt. 28: Ilość osób - TAK (1 pkt)
                true,   // Pyt. 29: Warunki BHP - TAK (1 pkt)
                false   // Pyt. 30: Stan sanitarny - NIE (0 pkt)
            };
            // Suma Sekcja II: 23 punkty (23 × 1)
            
            bool dokumentacja = true;  // Świadectwo zdrowia dostarczone
            
            // Obliczenia punktów
            int punkty1_5 = 12;    // Sekcja I
            int punkty6_30 = 23;   // Sekcja II
            int punktyRazem = 35;  // SUMA: 35/40 = POZYTYWNA
            
            generator.GenerujPdf(
                sciezkaDoPliku: @"C:\Raporty\Ocena_Kowalski_2024-11-23.pdf",
                numerRaportu: "OCN/2024/123",
                dataOceny: new DateTime(2024, 11, 23),
                dostawcaNazwa: "Jan Kowalski - Ferma Drobiu",
                dostawcaId: "DOW-001",
                samoocena: samoocena,
                listaKontrolna: listaKontrolna,
                dokumentacja: dokumentacja,
                p1_5: punkty1_5,
                p6_20: punkty6_30,
                pRazem: punktyRazem,
                uwagi: "Dostawca spełnia większość wymagań. Drobne uchybienia w punktach 5, 14 i 30. Zaleca się poprawę organizacji terenu wokół fermy oraz punktualności przjazdów.",
                czyPustyFormularz: false  // ⚠️ WAŻNE: false = wypełniony raport!
            );
            
            Console.WriteLine("✅ Wygenerowano wypełniony raport oceny!");
            Console.WriteLine($"📊 Wynik: {punktyRazem}/40 punktów = OCENA POZYTYWNA");
        }

        // ====================================================================
        // PRZYKŁAD 3: Raport z oceną WARUNKOWO POZYTYWNĄ
        // ====================================================================
        
        public void PrzykladOcenaWarunkowa()
        {
            var generator = new OcenaPDFGenerator();
            
            // Przykład słabszych wyników (20-29 punktów)
            bool[] samoocena = new bool[] { true, false, true, false, true };
            // Suma: 9 pkt (3 × 3)
            
            bool[] listaKontrolna = new bool[]
            {
                // Łącznie 13 TAK z 25 pytań = 13 punktów
                true, false, true, false, true,     // Część A: 3/5
                true, true, false, true, false,     // Część B: 3/5
                false, true, false, true, true,     // Część C: 3/5
                false, true, false, false, true,    // Część D: 2/5
                false, false, true, true, false     // Część E: 2/5
            };
            // Suma: 13 pkt
            
            generator.GenerujPdf(
                sciezkaDoPliku: @"C:\Raporty\Ocena_Nowak_Warunkowa.pdf",
                numerRaportu: "OCN/2024/124",
                dataOceny: DateTime.Now,
                dostawcaNazwa: "Adam Nowak - Hodowla Kurczaków",
                dostawcaId: "DOW-002",
                samoocena: samoocena,
                listaKontrolna: listaKontrolna,
                dokumentacja: false,  // Brak dokumentacji!
                p1_5: 9,
                p6_20: 13,
                pRazem: 22,  // 22 punkty = WARUNKOWO POZYTYWNA
                uwagi: "⚠️ UWAGA: Dostawca wymaga działań korygujących! Brak dokumentacji sanitarnej. Niedostateczne środki dezynfekcyjne. Wymagana ponowna kontrola w ciągu 30 dni.",
                czyPustyFormularz: false
            );
            
            Console.WriteLine("⚠️ Wygenerowano raport z oceną WARUNKOWO POZYTYWNĄ");
            Console.WriteLine("📋 Wymagane działania korygujące!");
        }

        // ====================================================================
        // PRZYKŁAD 4: Raport z oceną NEGATYWNĄ
        // ====================================================================
        
        public void PrzykladOcenaNegatywna()
        {
            var generator = new OcenaPDFGenerator();
            
            // Przykład złych wyników (poniżej 20 punktów)
            bool[] samoocena = new bool[] { true, false, false, false, false };
            // Suma: 3 pkt (1 × 3)
            
            bool[] listaKontrolna = new bool[]
            {
                // Łącznie 10 TAK z 25 pytań = 10 punktów
                false, false, false, true, false,
                false, true, false, false, false,
                false, false, true, true, false,
                false, false, true, false, true,
                false, true, false, true, true
            };
            // Suma: 10 pkt
            
            generator.GenerujPdf(
                sciezkaDoPliku: @"C:\Raporty\Ocena_Wisniewski_Negatywna.pdf",
                numerRaportu: "OCN/2024/125",
                dataOceny: DateTime.Now,
                dostawcaNazwa: "Piotr Wiśniewski - Ferma",
                dostawcaId: "DOW-003",
                samoocena: samoocena,
                listaKontrolna: listaKontrolna,
                dokumentacja: false,
                p1_5: 3,
                p6_20: 10,
                pRazem: 13,  // 13 punktów = NEGATYWNA
                uwagi: "❌ KRYTYCZNE: Dostawca nie spełnia podstawowych wymagań! Brak zgłoszenia w PIW, brak systemu dezynfekcji, nieodpowiednie warunki przechowywania leków, brak dokumentacji. ZAWIESZENIE DOSTAW do czasu wdrożenia działań naprawczych. Wymagany pełny audyt po 90 dniach.",
                czyPustyFormularz: false
            );
            
            Console.WriteLine("❌ Wygenerowano raport z oceną NEGATYWNĄ");
            Console.WriteLine("🚫 Rekomendacja: ZAWIESZENIE DOSTAW!");
        }

        // ====================================================================
        // PRZYKŁAD 5: Integracja z WPF Window (OcenaDostawcyWindow)
        // ====================================================================
        
        public void PrzykladIntegracjaZWPF(
            string dostawcaId,
            string dostawcaNazwa,
            bool[] odpowiedziSamoocena,
            bool[] odpowiedziKontrolna,
            bool dokumentacja,
            string uwagi)
        {
            // To byłoby wewnątrz twojego WPF okna przy kliknięciu "Generuj PDF"
            
            var generator = new OcenaPDFGenerator();
            
            // Oblicz punkty (logika z twojego systemu)
            int punkty1_5 = 0;
            for (int i = 0; i < 5 && i < odpowiedziSamoocena.Length; i++)
            {
                if (odpowiedziSamoocena[i]) punkty1_5 += 3;
            }
            
            int punkty6_30 = 0;
            for (int i = 0; i < 25 && i < odpowiedziKontrolna.Length; i++)
            {
                if (odpowiedziKontrolna[i]) punkty6_30 += 1;
            }
            
            int punktyRazem = punkty1_5 + punkty6_30;
            
            // Wygeneruj numer raportu
            string numerRaportu = $"OCN/{DateTime.Now.Year}/{DateTime.Now:MMdd}-{dostawcaId}";
            
            // Ścieżka do pliku
            string nazwaPliku = $"Ocena_{dostawcaId}_{DateTime.Now:yyyy-MM-dd}.pdf";
            string sciezka = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Raporty Dostawców",
                nazwaPliku
            );
            
            // Upewnij się że folder istnieje
            System.IO.Directory.CreateDirectory(
                System.IO.Path.GetDirectoryName(sciezka)
            );
            
            // Generuj PDF
            generator.GenerujPdf(
                sciezkaDoPliku: sciezka,
                numerRaportu: numerRaportu,
                dataOceny: DateTime.Now,
                dostawcaNazwa: dostawcaNazwa,
                dostawcaId: dostawcaId,
                samoocena: odpowiedziSamoocena,
                listaKontrolna: odpowiedziKontrolna,
                dokumentacja: dokumentacja,
                p1_5: punkty1_5,
                p6_20: punkty6_30,
                pRazem: punktyRazem,
                uwagi: uwagi,
                czyPustyFormularz: false
            );
            
            Console.WriteLine($"✅ PDF zapisany: {sciezka}");
            
            // Otwórz PDF automatycznie
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = sciezka,
                UseShellExecute = true
            });
        }

        // ====================================================================
        // PRZYKŁAD 6: Masowe generowanie pustych formularzy dla wszystkich dostawców
        // ====================================================================
        
        public void PrzykladMasoweFormularze()
        {
            // Lista dostawców z bazy danych (przykład)
            var dostawcy = new[]
            {
                new { Id = "DOW-001", Nazwa = "Jan Kowalski - Ferma Drobiu" },
                new { Id = "DOW-002", Nazwa = "Adam Nowak - Hodowla Kurczaków" },
                new { Id = "DOW-003", Nazwa = "Piotr Wiśniewski - Ferma" },
                new { Id = "DOW-004", Nazwa = "Maria Kowalczyk - Gospodarstwo" }
            };
            
            var generator = new OcenaPDFGenerator();
            string folderWyjsciowy = @"C:\Formularze\DoWydruku";
            
            // Utwórz folder jeśli nie istnieje
            System.IO.Directory.CreateDirectory(folderWyjsciowy);
            
            foreach (var dostawca in dostawcy)
            {
                string nazwaPliku = $"Formularz_{dostawca.Id}.pdf";
                string sciezka = System.IO.Path.Combine(folderWyjsciowy, nazwaPliku);
                
                generator.GenerujPdf(
                    sciezkaDoPliku: sciezka,
                    numerRaportu: "",
                    dataOceny: DateTime.Now,
                    dostawcaNazwa: dostawca.Nazwa,
                    dostawcaId: dostawca.Id,
                    samoocena: null,
                    listaKontrolna: null,
                    dokumentacja: false,
                    p1_5: 0,
                    p6_20: 0,
                    pRazem: 0,
                    uwagi: "",
                    czyPustyFormularz: true
                );
                
                Console.WriteLine($"✅ Wygenerowano: {nazwaPliku}");
            }
            
            Console.WriteLine($"\n📁 Wszystkie formularze w: {folderWyjsciowy}");
            Console.WriteLine("📄 Gotowe do wydruku i rozdania hodowcom!");
        }

        // ====================================================================
        // PRZYKŁAD 7: Eksport z Try-Catch (zalecane w produkcji)
        // ====================================================================
        
        public bool PrzykladBezpieczneGenerowanie(
            string sciezka,
            string dostawcaId,
            string dostawcaNazwa,
            bool[] samoocena,
            bool[] listaKontrolna,
            bool dokumentacja,
            int punkty1_5,
            int punkty6_30,
            int punktyRazem,
            string uwagi,
            bool czyPusty)
        {
            try
            {
                var generator = new OcenaPDFGenerator();
                
                generator.GenerujPdf(
                    sciezkaDoPliku: sciezka,
                    numerRaportu: czyPusty ? "" : $"OCN/{DateTime.Now.Year}/{DateTime.Now:MMdd}",
                    dataOceny: DateTime.Now,
                    dostawcaNazwa: dostawcaNazwa,
                    dostawcaId: dostawcaId,
                    samoocena: samoocena,
                    listaKontrolna: listaKontrolna,
                    dokumentacja: dokumentacja,
                    p1_5: punkty1_5,
                    p6_20: punkty6_30,
                    pRazem: punktyRazem,
                    uwagi: uwagi,
                    czyPustyFormularz: czyPusty
                );
                
                Console.WriteLine("✅ PDF wygenerowany pomyślnie!");
                return true;
            }
            catch (System.IO.IOException ex)
            {
                Console.WriteLine($"❌ Błąd zapisu pliku: {ex.Message}");
                Console.WriteLine("💡 Sprawdź czy plik nie jest otwarty w innym programie.");
                return false;
            }
            catch (System.UnauthorizedAccessException ex)
            {
                Console.WriteLine($"❌ Brak uprawnień: {ex.Message}");
                Console.WriteLine("💡 Uruchom program jako administrator lub wybierz inną lokalizację.");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Nieoczekiwany błąd: {ex.Message}");
                Console.WriteLine($"📋 Stack trace: {ex.StackTrace}");
                return false;
            }
        }
    }
}

// ========================================================================
// CHECKLISTY - CO SPRAWDZIĆ PRZED UŻYCIEM
// ========================================================================

/*
✅ PRZED GENEROWANIEM SPRAWDŹ:

1. Czy plik Logo.png istnieje w katalogu aplikacji?
   - Jeśli nie, zostanie wyświetlony placeholder "LOGO"

2. Czy masz zainstalowany QuestPDF?
   - Install-Package QuestPDF
   
3. Czy folder docelowy istnieje?
   - Jeśli nie, użyj Directory.CreateDirectory()

4. Czy plik docelowy nie jest otwarty?
   - Zamknij PDF przed ponownym generowaniem

5. Czy tablice odpowiedzi mają poprawną długość?
   - samoocena: 5 elementów (pytania 1-5)
   - listaKontrolna: 25 elementów (pytania 6-30)

6. Czy punktacja się zgadza?
   - punkty1_5: suma z tablicy samoocena (max 15)
   - punkty6_30: suma z tablicy listaKontrolna (max 25)
   - punktyRazem: punkty1_5 + punkty6_30 (max 40)
*/
