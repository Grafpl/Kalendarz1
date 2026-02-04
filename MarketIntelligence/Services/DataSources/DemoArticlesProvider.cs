using System;
using System.Collections.Generic;

namespace Kalendarz1.MarketIntelligence.Services.DataSources
{
    /// <summary>
    /// Provider testowych artykułów do demonstracji systemu Poranny Briefing.
    /// Artykuły są realistyczne i dotyczą polskiego rynku drobiarskiego.
    /// </summary>
    public static class DemoArticlesProvider
    {
        /// <summary>
        /// Zwraca 5 testowych artykułów z realistyczną treścią
        /// </summary>
        public static List<PerplexityArticle> GetDemoArticles()
        {
            return new List<PerplexityArticle>
            {
                // Artykuł 1: HPAI - Krytyczny
                new PerplexityArticle
                {
                    Title = "Nowe ognisko ptasiej grypy H5N1 wykryte w województwie łódzkim - GIW wprowadza strefy ochronne",
                    Snippet = @"Główny Inspektorat Weterynarii potwierdził wykrycie wysoce zjadliwej grypy ptaków (HPAI) podtypu H5N1 w gospodarstwie hodowlanym w powiecie brzezińskim, województwo łódzkie. To już trzecie ognisko w tym regionie w ciągu ostatnich dwóch tygodni.

W związku z wykryciem choroby, wojewódzki lekarz weterynarii wprowadził strefę ochronną o promieniu 3 km oraz strefę nadzoru o promieniu 10 km wokół ogniska. W strefach tych obowiązuje zakaz przemieszczania drobiu i jaj wylęgowych bez zgody powiatowego lekarza weterynarii.

Według danych GIW, w gospodarstwie znajdowało się około 45 000 sztuk drobiu rzeźnego (kurczaki brojlery), które zostaną poddane ubojowi sanitarnemu. Właściciel gospodarstwa będzie mógł ubiegać się o odszkodowanie zgodnie z przepisami ustawy o ochronie zdrowia zwierząt.

Dr Jan Kowalski, główny lekarz weterynarii, apeluje do hodowców o wzmożoną czujność i stosowanie rygorystycznych zasad bioasekuracji. Szczególnie istotne jest ograniczenie kontaktu drobiu z dzikimi ptakami oraz dezynfekcja środków transportu wjeżdżających do gospodarstw.

Eksperci zwracają uwagę, że obecna fala zachorowań może być związana z jesienną migracją ptaków wodnych, które są naturalnym rezerwuarem wirusa. Zaleca się powstrzymanie od wypuszczania drobiu na wybieg oraz zabezpieczenie paszy i wody przed kontaktem z dzikimi ptakami.

W całej Europie od początku sezonu 2024/2025 potwierdzono już ponad 200 ognisk HPAI w gospodarstwach komercyjnych, co stanowi wzrost o 35% w porównaniu z analogicznym okresem roku ubiegłego.",
                    Source = "wetgiw.gov.pl",
                    Url = "https://www.wetgiw.gov.pl/nadzor-weterynaryjny/hpai-lodzkie-2025"
                },

                // Artykuł 2: Ceny - Pozytywny
                new PerplexityArticle
                {
                    Title = "Ceny żywca drobiowego rosną - producenci notują najlepsze wyniki od dwóch lat",
                    Snippet = @"Hurtowe ceny żywca drobiowego w Polsce osiągnęły najwyższy poziom od 24 miesięcy. Według danych Zintegrowanego Systemu Rolniczej Informacji Rynkowej (ZSRIR), średnia cena kurczaka brojlera wynosi obecnie 6,85 zł/kg, co stanowi wzrost o 18% w porównaniu z analogicznym okresem roku ubiegłego.

Analitycy Krajowej Izby Drobiowej wskazują na kilka czynników napędzających wzrost cen. Po pierwsze, rosnący popyt wewnętrzny - Polacy spożywają coraz więcej mięsa drobiowego, które postrzegane jest jako zdrowsza i tańsza alternatywa dla wołowiny i wieprzowiny. Po drugie, silny eksport - polskie mięso drobiowe cieszy się rosnącym zainteresowaniem na rynkach Unii Europejskiej oraz krajów trzecich.

Jednocześnie koszty produkcji ustabilizowały się po okresie gwałtownych wzrostów związanych z kryzysem energetycznym i inflacją. Ceny pasz, które stanowią około 65-70% kosztów produkcji drobiu, spadły o 12% w ciągu ostatnich sześciu miesięcy dzięki dobrym zbiorum zbóż w Polsce i Europie.

Marek Sawicki, prezes Krajowej Rady Drobiarstwa, prognozuje, że korzystna koniunktura utrzyma się przynajmniej do końca pierwszego kwartału 2025 roku. 'Widzimy wyraźne sygnały ze strony sieci handlowych, które zabezpieczają wolumeny na święta wielkanocne z wyprzedzeniem' - powiedział w rozmowie z PAP.

Dla ubojni drobiu oznacza to możliwość poprawy marż, które w poprzednich latach były pod presją wysokich kosztów energii i surowców. Wiele zakładów planuje inwestycje w modernizację linii produkcyjnych i zwiększenie mocy przerobowych.",
                    Source = "portalspozywczy.pl",
                    Url = "https://www.portalspozywczy.pl/drob/ceny-zywca-2025-rekord"
                },

                // Artykuł 3: Eksport - Informacyjny
                new PerplexityArticle
                {
                    Title = "Polska umacnia pozycję lidera eksportu drobiu w UE - nowe rynki zbytu w Azji",
                    Snippet = @"Polska pozostaje największym eksporterem mięsa drobiowego w Unii Europejskiej. Według najnowszych danych Krajowego Ośrodka Wsparcia Rolnictwa (KOWR), w ciągu pierwszych dziewięciu miesięcy 2024 roku wyeksportowaliśmy 1,42 mln ton mięsa drobiowego o wartości 4,8 mld euro.

Szczególnie dynamiczny wzrost odnotowano w eksporcie do krajów azjatyckich. Po otwarciu rynku japońskiego dla polskiego drobiu w zeszłym roku, eksport do tego kraju wzrósł o 340% r/r. Japonia ceni polskie mięso za wysoką jakość i konkurencyjną cenę. Trwają także negocjacje w sprawie otwarcia rynku chińskiego, co mogłoby znacząco zwiększyć wolumen eksportu.

Głównymi odbiorcami polskiego drobiu w UE pozostają Niemcy (23% eksportu), Wielka Brytania (15%), Holandia (12%) i Francja (9%). Na rynkach pozaunijnych, oprócz Japonii, znaczący wzrost odnotowano w eksporcie do Arabii Saudyjskiej, ZEA i Filipin.

Anna Kowalczyk, dyrektor departamentu promocji w KOWR, podkreśla znaczenie certyfikacji i standardów jakości. 'Polscy producenci intensywnie inwestują w certyfikaty halal i standardy wymagane przez importerów azjatyckich. To otwiera drzwi do najbardziej lukratywnych rynków' - wyjaśnia.

Branża wskazuje jednak na wyzwania związane z logistyką i kosztami transportu. Wzrost cen frachtu morskiego oraz wydłużone czasy dostaw (m.in. z powodu sytuacji w rejonie Morza Czerwonego) wpływają na konkurencyjność cenową polskiego eksportu na rynkach odległych geograficznie.",
                    Source = "farmer.pl",
                    Url = "https://www.farmer.pl/produkcja-zwierzeca/drob/eksport-drobiu-polska-2025,142587.html"
                },

                // Artykuł 4: Konkurencja/Inwestycje - Ostrzegawczy
                new PerplexityArticle
                {
                    Title = "Cedrob ogłasza budowę nowej ubojni za 450 mln zł - zwiększenie mocy produkcyjnych o 40%",
                    Snippet = @"Grupa Cedrob, największy producent drobiu w Polsce, ogłosiła rozpoczęcie budowy nowego zakładu ubojowego w województwie warmińsko-mazurskim. Inwestycja o wartości 450 mln zł ma zostać ukończona do końca 2026 roku i zwiększy moce produkcyjne grupy o 40%.

Nowa ubojnia będzie jednym z najnowocześniejszych zakładów tego typu w Europie. Planowana wydajność to 16 000 sztuk drobiu na godzinę, z możliwością rozbudowy do 20 000 sztuk. Zakład będzie wyposażony w pełną automatyzację procesów, roboty do pakowania oraz zaawansowane systemy kontroli jakości oparte na sztucznej inteligencji.

Prezes Cedrob, Andrzej Goździkowski, uzasadnia inwestycję rosnącym popytem zarówno na rynku krajowym, jak i eksportowym. 'Widzimy ogromny potencjał wzrostu, szczególnie na rynkach azjatyckich. Musimy zwiększać moce, aby nie stracić udziału w rynku' - powiedział podczas konferencji prasowej.

Inwestycja Cedrobu jest częścią szerszego trendu konsolidacji w polskim sektorze drobiarskim. W ciągu ostatnich trzech lat trzej najwięksi producenci (Cedrob, Indykpol, SuperDrob) zwiększyli swój łączny udział w rynku z 35% do 45%. Analitycy przewidują, że ta tendencja będzie się pogłębiać.

Dla mniejszych i średnich ubojni oznacza to rosnącą presję konkurencyjną. Eksperci wskazują, że zakłady, które nie zainwestują w automatyzację i modernizację, mogą mieć problemy z utrzymaniem rentowności w perspektywie 3-5 lat. Kluczowe staje się znalezienie niszy rynkowej lub specjalizacja w produktach premium.",
                    Source = "wiadomoscihandlowe.pl",
                    Url = "https://www.wiadomoscihandlowe.pl/artykul/cedrob-nowa-ubojnia-450-mln-inwestycja"
                },

                // Artykuł 5: Regulacje - Informacyjny
                new PerplexityArticle
                {
                    Title = "Nowe przepisy UE o dobrostanie drobiu - co czeka polskich producentów od 2026 roku",
                    Snippet = @"Komisja Europejska przyjęła nowe rozporządzenie dotyczące dobrostanu drobiu, które wejdzie w życie od 1 stycznia 2026 roku. Przepisy wprowadzają znaczące zmiany w wymaganiach dotyczących hodowli i transportu ptaków, co będzie wymagało dostosowania praktyk przez polskich producentów.

Kluczowe zmiany obejmują: obniżenie maksymalnej obsady w kurniku z 42 kg/m² do 36 kg/m², obowiązkowe wzbogacenie środowiska (grzędy, materiał do dziobania), skrócenie maksymalnego czasu transportu z 12 do 8 godzin oraz nowe standardy ogłuszania przed ubojem.

Polska, jako największy producent drobiu w UE, będzie szczególnie dotknięta nowymi przepisami. Według szacunków Krajowej Izby Drobiowej, dostosowanie się do nowych wymogów może kosztować polską branżę od 2 do 3 mld zł w ciągu najbliższych dwóch lat. Największe inwestycje będą dotyczyły modernizacji ferm i zakładów ubojowych.

Minister rolnictwa Czesław Siekierski zadeklarował wsparcie dla producentów w procesie adaptacji. 'Planujemy uruchomienie programu dotacji pokrywającego do 50% kosztów inwestycji dostosowawczych. Szczegóły zostaną ogłoszone w pierwszym kwartale 2025 roku' - powiedział minister.

Organizacje branżowe wyrażają obawy o konkurencyjność polskiego drobiu wobec importu z krajów trzecich, gdzie takie standardy nie obowiązują. KRD apeluje o wprowadzenie klauzul lustrzanych w umowach handlowych UE, które wymuszałyby podobne standardy na importerach.

Eksperci podkreślają jednak, że wyższe standardy dobrostanu mogą stać się atutem marketingowym, szczególnie w eksporcie do świadomych ekologicznie rynków zachodnioeuropejskich i skandynawskich. Konsumenci są coraz bardziej skłonni płacić więcej za produkty z certyfikatem wysokiego dobrostanu.",
                    Source = "agroinfo.pl",
                    Url = "https://www.agroinfo.pl/regulacje/dobrostan-drobiu-ue-2026-nowe-przepisy"
                }
            };
        }

        /// <summary>
        /// Zwraca losowy pojedynczy artykuł testowy
        /// </summary>
        public static PerplexityArticle GetRandomDemoArticle()
        {
            var articles = GetDemoArticles();
            var random = new Random();
            return articles[random.Next(articles.Count)];
        }
    }
}
