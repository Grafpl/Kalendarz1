using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Kalendarz1.HDI
{
    /// <summary>
    /// Live podgląd HdiDiag — subskrybuje OnLog i pokazuje wpisy w ItemsControl.
    /// Filtruje (SQL only / errors only). Kopiuj wszystko do schowka.
    /// </summary>
    public partial class HdiDiagWindow : Window
    {
        public class LogVm
        {
            public string TimeStr { get; set; } = "";
            public string Level { get; set; } = "";
            public string Source { get; set; } = "";
            public string Message { get; set; } = "";
            public string ElapsedStr { get; set; } = "";
            public Brush LevelBrush { get; set; } = Brushes.Gray;
            public Brush RowBg { get; set; } = Brushes.Transparent;
            public HdiDiag.Entry Raw { get; set; } = new();
        }

        private static readonly Brush BgErr   = new SolidColorBrush(Color.FromArgb(0x40, 0xDC, 0x26, 0x26));
        private static readonly Brush BgWarn  = new SolidColorBrush(Color.FromArgb(0x30, 0xF9, 0x73, 0x16));
        private static readonly Brush BgSql   = new SolidColorBrush(Color.FromArgb(0x18, 0x2D, 0xD4, 0xBF));
        private static readonly Brush BgTime  = new SolidColorBrush(Color.FromArgb(0x18, 0xFB, 0xBF, 0x24));
        private static readonly Brush BgTrans = Brushes.Transparent;
        private static readonly Brush FgErr   = new SolidColorBrush(Color.FromRgb(0xF8, 0x71, 0x71));
        private static readonly Brush FgWarn  = new SolidColorBrush(Color.FromRgb(0xFB, 0xBF, 0x24));
        private static readonly Brush FgSql   = new SolidColorBrush(Color.FromRgb(0x2D, 0xD4, 0xBF));
        private static readonly Brush FgTime  = new SolidColorBrush(Color.FromRgb(0xFB, 0xBF, 0x24));
        private static readonly Brush FgInfo  = new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1));

        public ObservableCollection<LogVm> AllLogs { get; } = new();
        public ObservableCollection<LogVm> Visible { get; } = new();

        static HdiDiagWindow() { BgErr.Freeze(); BgWarn.Freeze(); BgSql.Freeze(); BgTime.Freeze(); FgErr.Freeze(); FgWarn.Freeze(); FgSql.Freeze(); FgTime.Freeze(); FgInfo.Freeze(); }

        public HdiDiagWindow()
        {
            InitializeComponent();
            try { Kalendarz1.WindowIconHelper.SetIcon(this); } catch { }
            LogList.ItemsSource = Visible;

            // Pozycja: prawy-dolny róg ekranu (żeby nie zasłaniać HDI)
            var sw = SystemParameters.WorkArea.Width;
            var sh = SystemParameters.WorkArea.Height;
            Width = Math.Min(1100, sw - 100);
            Height = Math.Min(700, sh - 100);
            Left = sw - Width - 20;
            Top = sh - Height - 20;

            // Historia (jeśli ktoś logował przed otwarciem okna)
            foreach (var e in HdiDiag.Snapshot()) AddEntry(e);
            HdiDiag.OnLog += OnNewLog;
            Closed += (s, e) => HdiDiag.OnLog -= OnNewLog;
            UpdateCount();
        }

        private void OnNewLog(HdiDiag.Entry e)
        {
            // Marshal do UI thread
            Dispatcher.BeginInvoke(new Action(() => { AddEntry(e); UpdateCount(); ScrollToEnd(); }));
        }

        private void AddEntry(HdiDiag.Entry e)
        {
            var vm = new LogVm
            {
                Raw = e,
                TimeStr = e.When.ToString("HH:mm:ss.fff"),
                Level = e.Level,
                Source = e.Source,
                Message = e.Message,
                ElapsedStr = e.ElapsedMs.HasValue ? $"{e.ElapsedMs}ms" : "",
                LevelBrush = e.Level switch { "ERR" => FgErr, "WARN" => FgWarn, "SQL" => FgSql, "TIME" => FgTime, _ => FgInfo },
                RowBg = e.Level switch { "ERR" => BgErr, "WARN" => BgWarn, "SQL" => BgSql, "TIME" => BgTime, _ => BgTrans }
            };
            AllLogs.Add(vm);
            if (PassesFilter(vm)) Visible.Add(vm);
        }

        private bool PassesFilter(LogVm vm)
        {
            if (ChkErrOnly.IsChecked == true) return vm.Level == "ERR" || vm.Level == "WARN";
            if (ChkSqlOnly.IsChecked == true) return vm.Level == "SQL";
            return true;
        }

        private void Filter_Changed(object sender, RoutedEventArgs e)
        {
            Visible.Clear();
            foreach (var vm in AllLogs.Where(PassesFilter)) Visible.Add(vm);
            UpdateCount();
            ScrollToEnd();
        }

        private void ScrollToEnd()
        {
            if (ChkAutoScroll.IsChecked == true) ScrollView.ScrollToEnd();
        }

        private void UpdateCount()
        {
            LblCount.Text = $"{Visible.Count} / {AllLogs.Count} wpisów";
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== HDI DIAG — {DateTime.Now:dd.MM.yyyy HH:mm:ss} === ({Visible.Count} wpisów)");
            foreach (var vm in Visible)
                sb.AppendLine(vm.Raw.ToString());
            try
            {
                Clipboard.SetText(sb.ToString());
                LblStatus.Text = $"✓ Skopiowano {Visible.Count} wpisów do schowka — wklej w czacie";
            }
            catch (Exception ex) { LblStatus.Text = $"⚠ Błąd kopiowania: {ex.Message}"; }
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            AllLogs.Clear();
            Visible.Clear();
            HdiDiag.Clear();
            UpdateCount();
            LblStatus.Text = "🗑 Wyczyszczono — gotowe do nowego testu";
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
