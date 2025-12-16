// =====================================================
// KONFIGURACJA WAGI ELEKTRONICZNEJ
// Plik: ScaleConfig.cs
// =====================================================
// 
// TWÓJ SPRZĘT:
// ============
// Waga: RHEWA 82c-1 (waga samochodowa 60t)
// Drukarka: PICCO-2SU (termiczna 58mm)
//
// PARAMETRY POŁĄCZENIA RHEWA 82c:
// ===============================
// - BaudRate: 9600 (lub 2400, 4800, 19200 - sprawdź w menu wagi)
// - DataBits: 8
// - Parity: None (lub Even - zależnie od ustawień)
// - StopBits: 1
// - Handshake: None
//
// KOMENDY RHEWA:
// ==============
// "S" lub "s" - odczyt stabilnej wagi
// "W" - odczyt wagi (weight)
// "G" - odczyt brutto (gross)
// "N" - odczyt netto
// "T" - odczyt tary
// CR (Enter) - niektóre modele
//
// FORMAT ODPOWIEDZI RHEWA:
// ========================
// "G      12345 kg" - brutto
// "N      12345 kg" - netto
// "T       1234 kg" - tara
// "US" - waga niestabilna (unstable)
// "OL" - przeciążenie (overload)
//
// JAK SPRAWDZIĆ USTAWIENIA W WADZE RHEWA:
// =======================================
// 1. Wejdź w menu serwisowe wagi (zazwyczaj przytrzymaj przycisk "i")
// 2. Znajdź ustawienia "Interface" lub "RS232"
// 3. Sprawdź BaudRate, Parity, Format danych
// 4. Zapisz te ustawienia i użyj w programie
//
// KONTAKT SERWISOWY:
// ==================
// MULTIWAG - www.multiwag.pl
// Tel: 504 335 604
// =====================================================

namespace Kalendarz1
{
    /// <summary>
    /// Konfiguracja dla wagi elektronicznej RHEWA 82c-1
    /// </summary>
    public static class ScaleConfig
    {
        // === USTAWIENIA DLA RHEWA 82c ===
        
        /// <summary>
        /// Domyślna prędkość transmisji dla RHEWA
        /// Sprawdź w menu wagi - może być 2400, 4800, 9600, 19200
        /// </summary>
        public static int DefaultBaudRate = 9600;
        
        /// <summary>
        /// Komenda wysyłana do wagi w celu odczytu
        /// Dla RHEWA: "S" (stable), "W" (weight), "G" (gross)
        /// </summary>
        public static string ReadCommand = "S";
        
        /// <summary>
        /// Alternatywne komendy do wypróbowania
        /// </summary>
        public static string[] AlternativeCommands = { "S", "W", "G", "\r", "?" };
        
        /// <summary>
        /// Czy waga wymaga komendy, czy wysyła dane ciągle
        /// RHEWA zazwyczaj wymaga komendy
        /// </summary>
        public static bool RequiresCommand = true;
        
        /// <summary>
        /// Czas oczekiwania na odpowiedź wagi (ms)
        /// </summary>
        public static int ReadTimeout = 1000;
        
        /// <summary>
        /// Jednostka wagi
        /// </summary>
        public static string WeightUnit = "kg";
        
        /// <summary>
        /// Działka elementarna wagi (e)
        /// Dla RHEWA 82c-1: 20 kg
        /// </summary>
        public static int ScaleDivision = 20;
        
        /// <summary>
        /// Maksymalna waga (Max)
        /// Dla RHEWA 82c-1: 60000 kg
        /// </summary>
        public static int MaxWeight = 60000;
        
        /// <summary>
        /// Minimalna waga (Min)
        /// Dla RHEWA 82c-1: 400 kg
        /// </summary>
        public static int MinWeight = 400;
    }
    
    /// <summary>
    /// Konfiguracja drukarki termicznej PICCO-2SU
    /// </summary>
    public static class PrinterConfig
    {
        /// <summary>
        /// Szerokość papieru w mm
        /// </summary>
        public static int PaperWidth = 58;
        
        /// <summary>
        /// Szerokość druku w mm (papier - marginesy)
        /// </summary>
        public static int PrintWidth = 48;
        
        /// <summary>
        /// Maksymalna liczba znaków w linii (font 9pt)
        /// </summary>
        public static int CharsPerLine = 32;
        
        /// <summary>
        /// Interfejs drukarki
        /// </summary>
        public static string Interface = "USB"; // lub "RS232"
    }
}
