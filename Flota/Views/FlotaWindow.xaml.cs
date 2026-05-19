using System.Windows;

namespace Kalendarz1.Flota.Views
{
    public partial class FlotaWindow : Window
    {
        public const int TabKierowcy = 0;
        public const int TabPojazdy = 1;
        public const int TabPrzypisania = 2;

        public FlotaWindow()
        {
            InitializeComponent();
            try { WindowIconHelper.SetIcon(this); } catch { }
        }

        /// <summary>Faza 8-B — otwiera okno z określoną zakładką startową.</summary>
        public FlotaWindow(int startTab) : this()
        {
            // SetStartTab po Loaded — WidokFlota.MainTabs musi być zainicjalizowany.
            Loaded += (_, _) => FlotaContent?.SetStartTab(startTab);
        }
    }
}
