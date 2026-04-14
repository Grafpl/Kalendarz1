using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Kalendarz1.Opakowania.Models;
using Kalendarz1.Opakowania.Services;

namespace Kalendarz1.Opakowania.ViewModels
{
    public class DodajPotwierdzenieViewModel : ViewModelBase
    {
        private readonly OpakowaniaDataService _dataService = new();
        private readonly ExportService _exportService = new();
        private readonly int _kontrahentId;
        private readonly string _kontrahentShortcut;
        private readonly string _userId;

        private string _kontrahentNazwa;
        private DateTime _dataPotwierdzenia = DateTime.Today;
        private string _uwagi;
        private string _skanSciezka;
        private int _sysE2, _sysH1, _sysEURO, _sysPCV, _sysDREW;
        private int _potwE2, _potwH1, _potwEURO, _potwPCV, _potwDREW;

        public DodajPotwierdzenieViewModel(int kontrahentId, string kontrahentNazwa, string kontrahentShortcut,
            string userId, int sysE2, int sysH1, int sysEURO, int sysPCV, int sysDREW)
        {
            _kontrahentId = kontrahentId;
            _kontrahentNazwa = kontrahentNazwa;
            _kontrahentShortcut = kontrahentShortcut;
            _userId = userId;
            _sysE2 = sysE2; _potwE2 = sysE2;
            _sysH1 = sysH1; _potwH1 = sysH1;
            _sysEURO = sysEURO; _potwEURO = sysEURO;
            _sysPCV = sysPCV; _potwPCV = sysPCV;
            _sysDREW = sysDREW; _potwDREW = sysDREW;
        }

        public event Action<bool?> RequestClose;

        public string KontrahentNazwa => _kontrahentNazwa;
        public DateTime DataPotwierdzenia { get => _dataPotwierdzenia; set => SetProperty(ref _dataPotwierdzenia, value); }
        public string Uwagi { get => _uwagi; set => SetProperty(ref _uwagi, value); }

        public string SkanSciezka
        {
            get => _skanSciezka;
            set { SetProperty(ref _skanSciezka, value); OnPropertyChanged(nameof(SkanNazwa)); }
        }
        public string SkanNazwa => string.IsNullOrEmpty(_skanSciezka) ? "(brak)" : Path.GetFileName(_skanSciezka);

        public int SysE2 => _sysE2; public int SysH1 => _sysH1; public int SysEURO => _sysEURO; public int SysPCV => _sysPCV; public int SysDREW => _sysDREW;

        public int PotwE2 { get => _potwE2; set { if (SetProperty(ref _potwE2, value)) Roz("E2"); } }
        public int PotwH1 { get => _potwH1; set { if (SetProperty(ref _potwH1, value)) Roz("H1"); } }
        public int PotwEURO { get => _potwEURO; set { if (SetProperty(ref _potwEURO, value)) Roz("EURO"); } }
        public int PotwPCV { get => _potwPCV; set { if (SetProperty(ref _potwPCV, value)) Roz("PCV"); } }
        public int PotwDREW { get => _potwDREW; set { if (SetProperty(ref _potwDREW, value)) Roz("DREW"); } }

        public string RozE2Txt => Fmt(_potwE2 - _sysE2); public string RozH1Txt => Fmt(_potwH1 - _sysH1);
        public string RozEUROTxt => Fmt(_potwEURO - _sysEURO); public string RozPCVTxt => Fmt(_potwPCV - _sysPCV);
        public string RozDREWTxt => Fmt(_potwDREW - _sysDREW);

        public Brush RozE2Color => Clr(_potwE2 - _sysE2); public Brush RozH1Color => Clr(_potwH1 - _sysH1);
        public Brush RozEUROColor => Clr(_potwEURO - _sysEURO); public Brush RozPCVColor => Clr(_potwPCV - _sysPCV);
        public Brush RozDREWColor => Clr(_potwDREW - _sysDREW);

        public string Info
        {
            get
            {
                int n = 0;
                if (_potwE2 != _sysE2) n++; if (_potwH1 != _sysH1) n++;
                if (_potwEURO != _sysEURO) n++; if (_potwPCV != _sysPCV) n++;
                if (_potwDREW != _sysDREW) n++;
                return n == 0 ? "Wszystko zgodne" : $"{n} rozbieżności";
            }
        }

        void Roz(string k) { OnPropertyChanged($"Roz{k}Txt"); OnPropertyChanged($"Roz{k}Color"); OnPropertyChanged(nameof(Info)); }
        static string Fmt(int r) => r == 0 ? "0" : r > 0 ? $"+{r}" : r.ToString();
        static Brush Clr(int r) => r == 0 ? new SolidColorBrush(Color.FromRgb(156, 163, 175)) : new SolidColorBrush(Color.FromRgb(220, 38, 38));

        /// <summary>Kopiuje skan na serwer do folderu kontrahenta/Potwierdzenia</summary>
        string KopiujSkanNaSerwer()
        {
            if (string.IsNullOrEmpty(_skanSciezka) || !File.Exists(_skanSciezka)) return null;
            try
            {
                string bazowa = _exportService.GetSciezkaZapisu();
                string bezp = string.Join("_", _kontrahentNazwa.Trim().Split(Path.GetInvalidFileNameChars()));
                string folder = Path.Combine(bazowa, bezp, "Potwierdzenia");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                string ext = Path.GetExtension(_skanSciezka);
                string nazwa = $"Potw_{DataPotwierdzenia:yyyy-MM-dd}_{DateTime.Now:HHmmss}{ext}";
                string cel = Path.Combine(folder, nazwa);
                File.Copy(_skanSciezka, cel, true);
                return cel;
            }
            catch { return _skanSciezka; } // fallback — oryginalna ścieżka
        }

        public async Task<bool> ZapiszAsync()
        {
            if (DataPotwierdzenia > DateTime.Today)
            {
                MessageBox.Show("Data nie może być z przyszłości.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            try
            {
                // Skopiuj skan na serwer (raz, wspólna ścieżka dla wszystkich typów)
                string skanSerwer = KopiujSkanNaSerwer();

                var typy = TypOpakowania.WszystkieTypy;
                (string kod, int potw, int sys)[] items =
                {
                    ("E2", _potwE2, _sysE2), ("H1", _potwH1, _sysH1), ("EURO", _potwEURO, _sysEURO),
                    ("PCV", _potwPCV, _sysPCV), ("DREW", _potwDREW, _sysDREW)
                };

                foreach (var (kod, potw, sys) in items)
                {
                    if (sys == 0 && potw == 0) continue;
                    var typ = Array.Find(typy, t => t.Kod == kod)
                        ?? new TypOpakowania { Kod = kod, Nazwa = kod, NazwaSystemowa = kod };

                    await _dataService.DodajPotwierdzenie(new PotwierdzenieSalda
                    {
                        KontrahentId = _kontrahentId,
                        KontrahentNazwa = _kontrahentNazwa,
                        KontrahentShortcut = _kontrahentShortcut,
                        TypOpakowania = typ.NazwaSystemowa,
                        KodOpakowania = kod,
                        DataPotwierdzenia = DataPotwierdzenia,
                        IloscPotwierdzona = potw,
                        SaldoSystemowe = sys,
                        StatusPotwierdzenia = potw != sys ? "Rozbieżność" : "Potwierdzone",
                        SciezkaZalacznika = skanSerwer,
                        Uwagi = Uwagi,
                        UzytkownikId = _userId,
                        UzytkownikNazwa = _userId
                    });
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Błąd zapisu", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
    }
}
