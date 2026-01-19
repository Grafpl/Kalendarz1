using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace Kalendarz1
{
    /// <summary>
    /// Statyczna klasa pomocnicza do ustawiania ikon okien WPF i WinForms
    /// </summary>
    public static class WindowIconHelper
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        /// <summary>
        /// Konfiguracja ikony dla okna
        /// </summary>
        public class IconConfig
        {
            public string Emoji { get; set; }
            public Color Color { get; set; }

            public IconConfig(string emoji, Color color)
            {
                Emoji = emoji;
                Color = color;
            }
        }

        /// <summary>
        /// Mapa ikon dla różnych typów okien - klucz to nazwa typu okna (bez namespace)
        /// </summary>
        private static readonly Dictionary<string, IconConfig> IconMap = new Dictionary<string, IconConfig>(StringComparer.OrdinalIgnoreCase)
        {
            // ========== ZAOPATRZENIE I ZAKUPY (Zielone) ==========
            { "WidokKontrahenci", new IconConfig("🧑‍🌾", Color.FromArgb(165, 214, 167)) },
            { "HodowcaForm", new IconConfig("🧑‍🌾", Color.FromArgb(165, 214, 167)) },
            { "NewHodowcaForm", new IconConfig("🧑‍🌾", Color.FromArgb(165, 214, 167)) },
            { "ChangeRequestForm", new IconConfig("🧑‍🌾", Color.FromArgb(165, 214, 167)) },

            { "WidokWstawienia", new IconConfig("🐣", Color.FromArgb(129, 199, 132)) },
            { "WstawienieWindow", new IconConfig("🐣", Color.FromArgb(129, 199, 132)) },
            { "Wstawienie", new IconConfig("🐣", Color.FromArgb(129, 199, 132)) },
            { "StatystykiPracownikow", new IconConfig("🐣", Color.FromArgb(129, 199, 132)) },
            { "SzczegółyPracownika", new IconConfig("🐣", Color.FromArgb(129, 199, 132)) },

            { "WidokKalendarzaWPF", new IconConfig("📅", Color.FromArgb(102, 187, 106)) },
            { "WidokKalendarza", new IconConfig("📅", Color.FromArgb(102, 187, 106)) },
            { "HistoriaZmianWindow", new IconConfig("📅", Color.FromArgb(102, 187, 106)) },

            { "WidokMatrycaWPF", new IconConfig("🚛", Color.FromArgb(76, 175, 80)) },
            { "WidokMatrycaNowy", new IconConfig("🚛", Color.FromArgb(76, 175, 80)) },
            { "ImportAvilogWindow", new IconConfig("🚛", Color.FromArgb(76, 175, 80)) },
            { "ImportExcelWindow", new IconConfig("🚛", Color.FromArgb(76, 175, 80)) },
            { "StatystykiPracownikowWindow", new IconConfig("🚛", Color.FromArgb(76, 175, 80)) },

            { "PanelPortiera", new IconConfig("⚖️", Color.FromArgb(85, 139, 47)) },
            { "PanelLekarza", new IconConfig("🩺", Color.FromArgb(51, 105, 30)) },

            { "WidokSpecyfikacje", new IconConfig("📋", Color.FromArgb(67, 160, 71)) },
            { "NowaSpecyfikacjaWindow", new IconConfig("📋", Color.FromArgb(67, 160, 71)) },
            { "HistoriaZmianSpecyfikacjeWindow", new IconConfig("📋", Color.FromArgb(67, 160, 71)) },
            { "EmailSpecyfikacjaWindow", new IconConfig("📋", Color.FromArgb(67, 160, 71)) },
            { "ImportSpecyfikacjeWizard", new IconConfig("📋", Color.FromArgb(67, 160, 71)) },
            { "IRZplusExportDialog", new IconConfig("📋", Color.FromArgb(67, 160, 71)) },
            { "IRZplusHistoryWindow", new IconConfig("📋", Color.FromArgb(67, 160, 71)) },
            { "IRZplusOdpadyWindow", new IconConfig("📋", Color.FromArgb(67, 160, 71)) },
            { "IRZplusPreviewWindow", new IconConfig("📋", Color.FromArgb(67, 160, 71)) },
            { "IRZplusSettingsWindow", new IconConfig("📋", Color.FromArgb(67, 160, 71)) },
            { "PartiaSelectWindow", new IconConfig("📋", Color.FromArgb(67, 160, 71)) },
            { "PhotoCompareWindow", new IconConfig("📋", Color.FromArgb(67, 160, 71)) },
            { "PhotoViewerWindow", new IconConfig("📋", Color.FromArgb(67, 160, 71)) },

            { "SprawdzalkaUmow", new IconConfig("📑", Color.FromArgb(56, 142, 60)) },
            { "UmowyForm", new IconConfig("📑", Color.FromArgb(56, 142, 60)) },

            { "Platnosci", new IconConfig("💵", Color.FromArgb(46, 125, 50)) },

            { "WidokPaszaPisklak", new IconConfig("🌾", Color.FromArgb(27, 94, 32)) },

            { "RaportyStatystykiWindow", new IconConfig("📊", Color.FromArgb(27, 94, 32)) },

            { "OcenaDostawcyWindow", new IconConfig("⭐", Color.FromArgb(76, 175, 80)) },
            { "OcenaDostawcyForm", new IconConfig("⭐", Color.FromArgb(76, 175, 80)) },
            { "HistoriaOcenWindow", new IconConfig("⭐", Color.FromArgb(76, 175, 80)) },
            { "HistoriaOcenForm", new IconConfig("⭐", Color.FromArgb(76, 175, 80)) },

            // ========== PRODUKCJA I MAGAZYN (Pomarańczowe) ==========
            { "ProdukcjaPanel", new IconConfig("🏭", Color.FromArgb(255, 204, 128)) },
            { "WidokPanelProdukcjaNowy", new IconConfig("🏭", Color.FromArgb(255, 204, 128)) },
            { "LivePrzychodyWindow", new IconConfig("🏭", Color.FromArgb(255, 204, 128)) },
            { "LivePrzychodyForm", new IconConfig("🏭", Color.FromArgb(255, 204, 128)) },
            { "ShipmentDetailsWindow", new IconConfig("🏭", Color.FromArgb(255, 204, 128)) },
            { "ShipmentDetailsForm", new IconConfig("🏭", Color.FromArgb(255, 204, 128)) },

            { "PokazKrojenieMrozenie", new IconConfig("✂️", Color.FromArgb(255, 183, 77)) },

            { "Mroznia", new IconConfig("❄️", Color.FromArgb(255, 152, 0)) },
            { "RezerwacjaInputDialog", new IconConfig("❄️", Color.FromArgb(255, 152, 0)) },
            { "MrozniaZewnetrznaDialog", new IconConfig("❄️", Color.FromArgb(255, 152, 0)) },
            { "WydanieZewnetrzneDialog", new IconConfig("❄️", Color.FromArgb(255, 152, 0)) },

            { "LiczenieStanuWindow", new IconConfig("📝", Color.FromArgb(251, 140, 0)) },

            { "MagazynPanel", new IconConfig("📦", Color.FromArgb(245, 124, 0)) },
            { "WydanieDialog", new IconConfig("📦", Color.FromArgb(245, 124, 0)) },

            { "AnalizaPrzychoduWindow", new IconConfig("⏱️", Color.FromArgb(239, 108, 0)) },

            { "AnalizaWydajnosci", new IconConfig("📈", Color.FromArgb(230, 81, 0)) },
            { "AnalizaWydajnosciKrojenia", new IconConfig("📈", Color.FromArgb(230, 81, 0)) },
            { "KonfiguracjaPodrobow", new IconConfig("📈", Color.FromArgb(230, 81, 0)) },
            { "KonfiguracjaWydajnosci", new IconConfig("📈", Color.FromArgb(230, 81, 0)) },
            { "KonfiguracjaProduktow", new IconConfig("📈", Color.FromArgb(230, 81, 0)) },

            // ========== ADMINISTRACJA (Czerwone) ==========
            { "AdminChangeRequestsForm", new IconConfig("📝", Color.FromArgb(239, 154, 154)) },
            { "MultiChangeRequestForm", new IconConfig("📝", Color.FromArgb(239, 154, 154)) },

            { "AdminPermissionsForm", new IconConfig("🔐", Color.FromArgb(183, 28, 28)) },
            { "PanelAdministracyjny", new IconConfig("🔐", Color.FromArgb(183, 28, 28)) },
            { "AddUserDialog", new IconConfig("🔐", Color.FromArgb(183, 28, 28)) },
            { "EditOperatorContactDialog", new IconConfig("🔐", Color.FromArgb(183, 28, 28)) },

            // ========== CRM I SPRZEDAŻ (Niebieskie) ==========
            { "CRMWindow", new IconConfig("🤝", Color.FromArgb(144, 202, 249)) },
            { "HistoriaHandlowcaWindow", new IconConfig("🤝", Color.FromArgb(144, 202, 249)) },
            { "KanbanWindow", new IconConfig("🤝", Color.FromArgb(144, 202, 249)) },
            { "DashboardCRMWindow", new IconConfig("🤝", Color.FromArgb(144, 202, 249)) },
            { "PanelManageraWindow", new IconConfig("🤝", Color.FromArgb(144, 202, 249)) },
            { "MapaCRMWindow", new IconConfig("🤝", Color.FromArgb(144, 202, 249)) },
            { "UstawDateKontaktuDialog", new IconConfig("🤝", Color.FromArgb(144, 202, 249)) },
            { "WyborBranzyDialog", new IconConfig("🤝", Color.FromArgb(144, 202, 249)) },
            { "EdycjaKontaktuWindow", new IconConfig("🤝", Color.FromArgb(144, 202, 249)) },

            { "KartotekaOdbiorcowWindow", new IconConfig("👥", Color.FromArgb(100, 181, 246)) },
            { "FormDodajOdbiorce", new IconConfig("👥", Color.FromArgb(100, 181, 246)) },
            { "PotencjalniOdbiorcy", new IconConfig("👥", Color.FromArgb(100, 181, 246)) },
            { "MapaOdbiorcowForm", new IconConfig("👥", Color.FromArgb(100, 181, 246)) },
            { "FormMapaWojewodztwa", new IconConfig("👥", Color.FromArgb(100, 181, 246)) },

            { "WidokZamowienia", new IconConfig("🛒", Color.FromArgb(66, 165, 245)) },
            { "WidokZamowieniaPodsumowanie", new IconConfig("🛒", Color.FromArgb(66, 165, 245)) },
            { "DuplicateOrderDialog", new IconConfig("🛒", Color.FromArgb(66, 165, 245)) },
            { "AfterSaveDialog", new IconConfig("🛒", Color.FromArgb(66, 165, 245)) },
            { "MultipleDatePickerDialog", new IconConfig("🛒", Color.FromArgb(66, 165, 245)) },
            { "CykliczneZamowieniaDialog", new IconConfig("🛒", Color.FromArgb(66, 165, 245)) },
            { "NotatkiDialog", new IconConfig("🛒", Color.FromArgb(66, 165, 245)) },
            { "KlasyWagoweDialog", new IconConfig("🛒", Color.FromArgb(66, 165, 245)) },
            { "ScalowanieTowarowDialog", new IconConfig("🛒", Color.FromArgb(66, 165, 245)) },

            { "HandlowiecDashboardWindow", new IconConfig("📊", Color.FromArgb(41, 121, 255)) },
            { "AnalizaCenHandlowcaWindow", new IconConfig("📊", Color.FromArgb(41, 121, 255)) },
            { "KontrahentOpakowaniaWindow", new IconConfig("📊", Color.FromArgb(41, 121, 255)) },
            { "KontrahentPlatnosciWindow", new IconConfig("📊", Color.FromArgb(41, 121, 255)) },

            { "MainWindow", new IconConfig("🛒", Color.FromArgb(30, 136, 229)) },
            { "DashboardWindow", new IconConfig("🛒", Color.FromArgb(30, 136, 229)) },
            { "StatystykiWindow", new IconConfig("🛒", Color.FromArgb(30, 136, 229)) },
            { "NoteWindow", new IconConfig("🛒", Color.FromArgb(30, 136, 229)) },
            { "InstrukcjaWindow", new IconConfig("🛒", Color.FromArgb(30, 136, 229)) },
            { "MultipleDatePickerWindow", new IconConfig("🛒", Color.FromArgb(30, 136, 229)) },
            { "CyclicOrdersWindow", new IconConfig("🛒", Color.FromArgb(30, 136, 229)) },
            { "PrintOptionsWindow", new IconConfig("🛒", Color.FromArgb(30, 136, 229)) },
            { "PrintPreviewByClientWindow", new IconConfig("🛒", Color.FromArgb(30, 136, 229)) },
            { "PrintPreviewByProductWindow", new IconConfig("🛒", Color.FromArgb(30, 136, 229)) },
            { "PrzyczynaAnulowaniaDialog", new IconConfig("🛒", Color.FromArgb(30, 136, 229)) },

            { "WidokFakturSprzedazy", new IconConfig("🧾", Color.FromArgb(33, 150, 243)) },
            { "FormDokumentyTowarowDnia", new IconConfig("🧾", Color.FromArgb(33, 150, 243)) },
            { "FormHistoriaCen", new IconConfig("🧾", Color.FromArgb(33, 150, 243)) },
            { "FormSzczegolyPlatnosci", new IconConfig("🧾", Color.FromArgb(33, 150, 243)) },

            { "PanelFakturWindow", new IconConfig("🧾", Color.FromArgb(30, 136, 229)) },

            // ========== OFERTY (Niebieskie) ==========
            { "OfertaHandlowaWindow", new IconConfig("💰", Color.FromArgb(30, 136, 229)) },
            { "DodajOdbiorceWindow", new IconConfig("💰", Color.FromArgb(30, 136, 229)) },
            { "EdycjaKontaktuWindowOferty", new IconConfig("💰", Color.FromArgb(30, 136, 229)) },
            { "MapowanieSwiezyMrozonyWindow", new IconConfig("💰", Color.FromArgb(30, 136, 229)) },
            { "MarzeWindow", new IconConfig("💰", Color.FromArgb(30, 136, 229)) },
            { "OfertaSzczegolyWindow", new IconConfig("💰", Color.FromArgb(30, 136, 229)) },
            { "SzablonOdbiorcowWindow", new IconConfig("💰", Color.FromArgb(30, 136, 229)) },
            { "SzablonParametrowWindow", new IconConfig("💰", Color.FromArgb(30, 136, 229)) },
            { "SzablonTowarowWindow", new IconConfig("💰", Color.FromArgb(30, 136, 229)) },
            { "TlumaczeniaProduktowWindow", new IconConfig("💰", Color.FromArgb(30, 136, 229)) },
            { "WprowadzOdbiorceRecznieWindow", new IconConfig("💰", Color.FromArgb(30, 136, 229)) },
            { "WyborSzablonuTowarowWindow", new IconConfig("💰", Color.FromArgb(30, 136, 229)) },
            { "WyborSzablonuWindow", new IconConfig("💰", Color.FromArgb(30, 136, 229)) },
            { "WyborTowarowWindow", new IconConfig("💰", Color.FromArgb(30, 136, 229)) },

            { "OfertyListaWindow", new IconConfig("📋", Color.FromArgb(25, 118, 210)) },
            { "OfertyDashboardWindow", new IconConfig("📊", Color.FromArgb(21, 101, 192)) },

            { "DashboardKlasWagowychWindow", new IconConfig("⚖️", Color.FromArgb(13, 71, 161)) },
            { "WidokKlasWagowychDnia", new IconConfig("⚖️", Color.FromArgb(13, 71, 161)) },
            { "WidokKlasWagowychDniaWPF", new IconConfig("⚖️", Color.FromArgb(13, 71, 161)) },

            { "FormPanelReklamacjiWindow", new IconConfig("⚠️", Color.FromArgb(21, 101, 192)) },
            { "FormPanelReklamacji", new IconConfig("⚠️", Color.FromArgb(21, 101, 192)) },
            { "FormReklamacjaWindow", new IconConfig("⚠️", Color.FromArgb(21, 101, 192)) },
            { "FormReklamacja", new IconConfig("⚠️", Color.FromArgb(21, 101, 192)) },
            { "FormSzczegolyReklamacji", new IconConfig("⚠️", Color.FromArgb(21, 101, 192)) },
            { "FormZmianaStatusu", new IconConfig("⚠️", Color.FromArgb(21, 101, 192)) },
            { "FormStatystykiReklamacji", new IconConfig("⚠️", Color.FromArgb(21, 101, 192)) },

            // ========== PLANOWANIE I ANALIZY (Fioletowe) ==========
            { "PrognozyUbojuWindow", new IconConfig("🔮", Color.FromArgb(206, 147, 216)) },
            { "FormWyborHandlowcow", new IconConfig("🔮", Color.FromArgb(206, 147, 216)) },
            { "FormWyborKontrahentow", new IconConfig("🔮", Color.FromArgb(206, 147, 216)) },

            { "TygodniowyPlan", new IconConfig("🗓️", Color.FromArgb(171, 71, 188)) },
            { "SzczegolyDnia", new IconConfig("🗓️", Color.FromArgb(171, 71, 188)) },
            { "SzczegolyDokumentuWindow", new IconConfig("🗓️", Color.FromArgb(171, 71, 188)) },

            { "AnalizaTygodniowaWindow", new IconConfig("📉", Color.FromArgb(74, 20, 140)) },
            { "AnalizaTygodniowaForm", new IconConfig("📉", Color.FromArgb(74, 20, 140)) },
            { "WeekDetailsForm", new IconConfig("📉", Color.FromArgb(74, 20, 140)) },
            { "ChartViewerForm", new IconConfig("📉", Color.FromArgb(74, 20, 140)) },

            // ========== OPAKOWANIA I TRANSPORT (Turkusowe) ==========
            { "ZestawienieOpakowanWindow", new IconConfig("📦", Color.FromArgb(128, 222, 234)) },
            { "OpakowaniaMainDashboard", new IconConfig("📦", Color.FromArgb(128, 222, 234)) },
            { "ListaOpakowanWindow", new IconConfig("📦", Color.FromArgb(128, 222, 234)) },
            { "DashboardPotwierdzeniWindow", new IconConfig("📦", Color.FromArgb(128, 222, 234)) },
            { "DashboardZarzadczyWindow", new IconConfig("📦", Color.FromArgb(128, 222, 234)) },
            { "DiagnostykaWindow", new IconConfig("📦", Color.FromArgb(128, 222, 234)) },
            { "RaportZarzaduWindow", new IconConfig("📦", Color.FromArgb(128, 222, 234)) },
            { "ZestawieniePorownanczeWindow", new IconConfig("📦", Color.FromArgb(128, 222, 234)) },
            { "DodajPotwierdzenieWindow", new IconConfig("📦", Color.FromArgb(128, 222, 234)) },

            { "SaldaWszystkichOpakowanWindow", new IconConfig("🏷️", Color.FromArgb(0, 172, 193)) },
            { "SaldaMainWindow", new IconConfig("🏷️", Color.FromArgb(0, 172, 193)) },
            { "SaldaSzczegolyWindow", new IconConfig("🏷️", Color.FromArgb(0, 172, 193)) },
            { "SaldoOdbiorcyWindow", new IconConfig("🏷️", Color.FromArgb(0, 172, 193)) },
            { "SzczegolyKontrahentaWindow", new IconConfig("🏷️", Color.FromArgb(0, 172, 193)) },

            { "TransportMainFormImproved", new IconConfig("🚚", Color.FromArgb(0, 96, 100)) },
            { "TransportWindow", new IconConfig("🚚", Color.FromArgb(0, 96, 100)) },
            { "TransportMapWindow", new IconConfig("🚚", Color.FromArgb(0, 96, 100)) },
            { "TransportStatystykiForm", new IconConfig("🚚", Color.FromArgb(0, 96, 100)) },
            { "TransportRaportForm", new IconConfig("🚚", Color.FromArgb(0, 96, 100)) },
            { "EdytorKursuWithPalety", new IconConfig("🚚", Color.FromArgb(0, 96, 100)) },
            { "SzybkiePrzypisanieDialog", new IconConfig("🚚", Color.FromArgb(0, 96, 100)) },
            { "PodgladLadunkowDialog", new IconConfig("🚚", Color.FromArgb(0, 96, 100)) },
            { "DodajKierowceDialog", new IconConfig("🚚", Color.FromArgb(0, 96, 100)) },
            { "DodajPojazdDialog", new IconConfig("🚚", Color.FromArgb(0, 96, 100)) },
            { "KolejnoscLadunkuDialog", new IconConfig("🚚", Color.FromArgb(0, 96, 100)) },
            { "PojazdyForm", new IconConfig("🚚", Color.FromArgb(0, 96, 100)) },
            { "KierowcyForm", new IconConfig("🚚", Color.FromArgb(0, 96, 100)) },

            // ========== FINANSE I PŁATNOŚCI (Szaroniebieski) ==========
            { "WidokSprzeZakup", new IconConfig("💼", Color.FromArgb(176, 190, 197)) },

            { "SzczegolyPlatnosciWindow", new IconConfig("💳", Color.FromArgb(100, 181, 246)) },
            { "WykresAnalizaPlatnosciWindow", new IconConfig("💳", Color.FromArgb(100, 181, 246)) },
            { "PlanSplatyWindow", new IconConfig("💳", Color.FromArgb(100, 181, 246)) },
            { "WyborWersjiPrzypomnienieWindow", new IconConfig("💳", Color.FromArgb(100, 181, 246)) },
            { "EmailPrzypomnienieWindow", new IconConfig("💳", Color.FromArgb(100, 181, 246)) },

            // ========== SPOTKANIA I NOTATKI (Niebieski/Szary) ==========
            { "SpotkaniaGlowneWindow", new IconConfig("📅", Color.FromArgb(25, 118, 210)) },
            { "EdytorSpotkania", new IconConfig("📅", Color.FromArgb(25, 118, 210)) },
            { "PodgladSpotkania", new IconConfig("📅", Color.FromArgb(25, 118, 210)) },
            { "NotyfikacjeWindow", new IconConfig("📅", Color.FromArgb(25, 118, 210)) },
            { "PodgladTranskrypcji", new IconConfig("📅", Color.FromArgb(25, 118, 210)) },
            { "TranskrypcjaSzczegolyWindow", new IconConfig("📅", Color.FromArgb(25, 118, 210)) },
            { "FirefliesDiagnostyka", new IconConfig("📅", Color.FromArgb(25, 118, 210)) },
            { "FirefliesKonfiguracjaWindow", new IconConfig("📅", Color.FromArgb(25, 118, 210)) },

            { "NotatkirGlownyWindow", new IconConfig("📝", Color.FromArgb(38, 50, 56)) },
            { "EdytorNotatki", new IconConfig("📝", Color.FromArgb(38, 50, 56)) },
            { "PodgladNotatki", new IconConfig("📝", Color.FromArgb(38, 50, 56)) },
            { "WyborTypuSpotkania", new IconConfig("📝", Color.FromArgb(38, 50, 56)) },

            // ========== KONTROLA GODZIN (Indygo) ==========
            { "KontrolaGodzinWindow", new IconConfig("⏱️", Color.FromArgb(126, 87, 194)) },
            { "SzczegolyDniaWindow", new IconConfig("⏱️", Color.FromArgb(126, 87, 194)) },
            { "TimelineWindow", new IconConfig("⏱️", Color.FromArgb(126, 87, 194)) },
            { "KartaRCPWindow", new IconConfig("⏱️", Color.FromArgb(126, 87, 194)) },
            { "ZarzadzanieKartamiWindow", new IconConfig("⏱️", Color.FromArgb(126, 87, 194)) },
            { "UstawieniaStawekWindow", new IconConfig("⏱️", Color.FromArgb(126, 87, 194)) },
            { "EdycjaStawkiWindow", new IconConfig("⏱️", Color.FromArgb(126, 87, 194)) },
            { "PrzypisKartyWindow", new IconConfig("⏱️", Color.FromArgb(126, 87, 194)) },
            { "DodajNieobecnoscDialog", new IconConfig("⏱️", Color.FromArgb(126, 87, 194)) },
            { "DodajNadgodzinyDialog", new IconConfig("⏱️", Color.FromArgb(126, 87, 194)) },
            { "DodajPrzerweDialog", new IconConfig("⏱️", Color.FromArgb(126, 87, 194)) },
            { "DodajPrzesuniecieDialog", new IconConfig("⏱️", Color.FromArgb(126, 87, 194)) },

            // ========== ZADANIA I POWIADOMIENIA (Zielony/Niebieski) ==========
            { "ZadaniaWindow", new IconConfig("✅", Color.FromArgb(76, 175, 80)) },
            { "ZadanieDialog", new IconConfig("✅", Color.FromArgb(76, 175, 80)) },
            { "FormZadania", new IconConfig("✅", Color.FromArgb(76, 175, 80)) },
            { "NotificationWindow", new IconConfig("🔔", Color.FromArgb(255, 193, 7)) },
            { "MeetingChangePopup", new IconConfig("🔔", Color.FromArgb(255, 193, 7)) },

            // ========== KOMUNIKATOR FIRMOWY (Fioletowy) ==========
            { "ChatMainWindow", new IconConfig("💬", Color.FromArgb(124, 58, 237)) },
            { "ChatNotificationPopup", new IconConfig("💬", Color.FromArgb(124, 58, 237)) },

            // ========== WYKRESY I ANALIZY ==========
            { "EurChartWindow", new IconConfig("💶", Color.FromArgb(76, 175, 80)) },
            { "WeatherChartWindow", new IconConfig("🌤️", Color.FromArgb(33, 150, 243)) },

            // ========== POZOSTAŁE ==========
            { "WidokWaga", new IconConfig("⚖️", Color.FromArgb(158, 158, 158)) },
            { "QuickReportDialog", new IconConfig("⚖️", Color.FromArgb(158, 158, 158)) },
            { "BatchAnalysisForm", new IconConfig("⚖️", Color.FromArgb(158, 158, 158)) },
            { "SupplierComparisonForm", new IconConfig("⚖️", Color.FromArgb(158, 158, 158)) },

            { "WidokCena", new IconConfig("💲", Color.FromArgb(76, 175, 80)) },
            { "WidokCenWszystkich", new IconConfig("💲", Color.FromArgb(76, 175, 80)) },
            { "PokazCeneTuszki", new IconConfig("💲", Color.FromArgb(76, 175, 80)) },

            { "Dostawa", new IconConfig("📦", Color.FromArgb(102, 187, 106)) },
            { "WidokWszystkichDostaw", new IconConfig("📦", Color.FromArgb(102, 187, 106)) },

            { "WidokAvilog", new IconConfig("📊", Color.FromArgb(76, 175, 80)) },
            { "WidokAvilogPlan", new IconConfig("📊", Color.FromArgb(76, 175, 80)) },
            { "WidokSprzedazPlan", new IconConfig("📊", Color.FromArgb(33, 150, 243)) },

            { "ObliczenieAut", new IconConfig("🚗", Color.FromArgb(0, 96, 100)) },

            { "ZarzadzanieHandlowcamiForm", new IconConfig("👔", Color.FromArgb(41, 121, 255)) },
            { "EdycjaHandlowcaForm", new IconConfig("👔", Color.FromArgb(41, 121, 255)) },
            { "UserHandlowcyDialog", new IconConfig("👔", Color.FromArgb(41, 121, 255)) },

            { "AnkietaPotwierdzoneForm", new IconConfig("📋", Color.FromArgb(76, 175, 80)) },
            { "HistoriaHodowcyWindowPremium_FINAL", new IconConfig("📊", Color.FromArgb(76, 175, 80)) },
            { "Top20ReportWindowEnhanced_FINAL", new IconConfig("🏆", Color.FromArgb(255, 193, 7)) },

            { "SzczegolyDrukowaniaSpecki", new IconConfig("🖨️", Color.FromArgb(158, 158, 158)) },

            { "LoginForm", new IconConfig("🔑", Color.FromArgb(33, 150, 243)) },
            { "WelcomeScreen", new IconConfig("👋", Color.FromArgb(76, 175, 80)) },
            { "MENU", new IconConfig("🏠", Color.FromArgb(38, 50, 56)) },
            { "Menu1", new IconConfig("🏠", Color.FromArgb(38, 50, 56)) },
        };

        /// <summary>
        /// Pobiera konfigurację ikony dla danego typu okna
        /// </summary>
        public static IconConfig GetIconConfig(string windowTypeName)
        {
            if (IconMap.TryGetValue(windowTypeName, out var config))
            {
                return config;
            }
            // Domyślna ikona jeśli nie znaleziono
            return new IconConfig("📄", Color.FromArgb(158, 158, 158));
        }

        /// <summary>
        /// Pobiera konfigurację ikony dla danego typu
        /// </summary>
        public static IconConfig GetIconConfig(Type windowType)
        {
            return GetIconConfig(windowType.Name);
        }

        /// <summary>
        /// Tworzy ikonę Windows Forms z emoji i kolorowym tłem (48x48 dla paska zadań)
        /// </summary>
        public static Icon CreateWinFormsIcon(string emoji, Color accentColor)
        {
            try
            {
                int size = 48;
                using (Bitmap bmp = new Bitmap(size, size))
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

                    using (SolidBrush bgBrush = new SolidBrush(accentColor))
                    {
                        g.FillEllipse(bgBrush, 1, 1, size - 2, size - 2);
                    }

                    if (!string.IsNullOrEmpty(emoji))
                    {
                        using (Font emojiFont = new Font("Segoe UI Emoji", 28, FontStyle.Regular, GraphicsUnit.Pixel))
                        {
                            var textSize = g.MeasureString(emoji, emojiFont);
                            float x = (size - textSize.Width) / 2;
                            float y = (size - textSize.Height) / 2;
                            g.DrawString(emoji, emojiFont, Brushes.White, x, y);
                        }
                    }

                    IntPtr hIcon = bmp.GetHicon();
                    using (Icon tempIcon = Icon.FromHandle(hIcon))
                    {
                        Icon clonedIcon = (Icon)tempIcon.Clone();
                        DestroyIcon(hIcon);
                        return clonedIcon;
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Tworzy ikonę WPF (BitmapSource) z emoji i kolorowym tłem (48x48 dla paska zadań)
        /// </summary>
        public static BitmapSource CreateWpfIcon(string emoji, Color accentColor)
        {
            try
            {
                int size = 48;
                using (Bitmap bmp = new Bitmap(size, size))
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

                    using (SolidBrush bgBrush = new SolidBrush(accentColor))
                    {
                        g.FillEllipse(bgBrush, 1, 1, size - 2, size - 2);
                    }

                    if (!string.IsNullOrEmpty(emoji))
                    {
                        using (Font emojiFont = new Font("Segoe UI Emoji", 28, FontStyle.Regular, GraphicsUnit.Pixel))
                        {
                            var textSize = g.MeasureString(emoji, emojiFont);
                            float x = (size - textSize.Width) / 2;
                            float y = (size - textSize.Height) / 2;
                            g.DrawString(emoji, emojiFont, Brushes.White, x, y);
                        }
                    }

                    IntPtr hBitmap = bmp.GetHbitmap();
                    try
                    {
                        return Imaging.CreateBitmapSourceFromHBitmap(
                            hBitmap,
                            IntPtr.Zero,
                            System.Windows.Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());
                    }
                    finally
                    {
                        DeleteObject(hBitmap);
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Ustawia ikonę dla okna WPF na podstawie jego typu
        /// </summary>
        public static void SetIcon(System.Windows.Window window)
        {
            if (window == null) return;

            var config = GetIconConfig(window.GetType().Name);
            var icon = CreateWpfIcon(config.Emoji, config.Color);
            if (icon != null)
            {
                window.Icon = icon;
            }
        }

        /// <summary>
        /// Ustawia ikonę dla okna WPF z określonym emoji i kolorem
        /// </summary>
        public static void SetIcon(System.Windows.Window window, string emoji, Color color)
        {
            if (window == null) return;

            var icon = CreateWpfIcon(emoji, color);
            if (icon != null)
            {
                window.Icon = icon;
            }
        }

        /// <summary>
        /// Ustawia ikonę dla formularza WinForms na podstawie jego typu
        /// </summary>
        public static void SetIcon(System.Windows.Forms.Form form)
        {
            if (form == null) return;

            var config = GetIconConfig(form.GetType().Name);
            var icon = CreateWinFormsIcon(config.Emoji, config.Color);
            if (icon != null)
            {
                form.Icon = icon;
            }
        }

        /// <summary>
        /// Ustawia ikonę dla formularza WinForms z określonym emoji i kolorem
        /// </summary>
        public static void SetIcon(System.Windows.Forms.Form form, string emoji, Color color)
        {
            if (form == null) return;

            var icon = CreateWinFormsIcon(emoji, color);
            if (icon != null)
            {
                form.Icon = icon;
            }
        }
    }
}
