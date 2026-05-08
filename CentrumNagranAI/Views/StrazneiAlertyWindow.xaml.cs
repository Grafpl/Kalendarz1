using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Kalendarz1.CentrumNagranAI.Services;

namespace Kalendarz1.CentrumNagranAI.Views
{
    public partial class StrazneiAlertyWindow : Window
    {
        public class RuleVm
        {
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Prompt { get; set; } = string.Empty;
            public int Threshold { get; set; }
            public int CooldownMin { get; set; }
            public bool Enabled { get; set; }
            public bool NotifySms { get; set; }
            public int RequiredConfirmations { get; set; } = 2;
        }

        public class AlertVm
        {
            public long Id { get; set; }
            public DateTime TsUtc { get; set; }
            public string RuleName { get; set; } = string.Empty;
            public string CameraId { get; set; } = string.Empty;
            public int Score { get; set; }
            public string Reason { get; set; } = string.Empty;
            public string TimeLabel => TsUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
            public string CameraDisplayName => CnaConfig.DisplayName(CameraId);
        }

        public class AnomalyVm
        {
            public long FrameId { get; set; }
            public DateTime TsUtc { get; set; }
            public string CameraId { get; set; } = string.Empty;
            public double Distance { get; set; }
            public string FilePath { get; set; } = string.Empty;
            public string TimeLabel => TsUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
            public string CameraDisplayName => CnaConfig.DisplayName(CameraId);
            public string DistanceLabel => Distance.ToString("F3");
        }

        public class FeedbackQueryVm
        {
            public string Query { get; set; } = string.Empty;
            public int Total { get; set; }
            public int Correct { get; set; }
            public double Precision { get; set; }
            public string PrecisionLabel => $"{Precision:F1}%";
        }

        public class BriefVm
        {
            public long Id { get; set; }
            public string Day { get; set; } = string.Empty;
            public string Summary { get; set; } = string.Empty;
            public DateTime Created { get; set; }
            public double CostUsd { get; set; }
            public int SampleSize { get; set; }
            public string CreatedLabel => Created.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            public string CostLabel => $"${CostUsd:F4}";
            public string Preview => Summary.Length > 100 ? Summary.Substring(0, 100).Replace("\n", " ") + "..." : Summary.Replace("\n", " ");
        }

        private readonly ObservableCollection<RuleVm> _rules = new();
        private readonly ObservableCollection<AlertVm> _alerts = new();
        private readonly ObservableCollection<AnomalyVm> _anomalies = new();
        private readonly ObservableCollection<BriefVm> _briefs = new();
        private readonly ObservableCollection<FeedbackQueryVm> _feedbackQueries = new();
        private RuleVm? _editing;

        public StrazneiAlertyWindow()
        {
            InitializeComponent();
            CnaConfig.ZaladujJesliTrzeba();
            FrameIndex.Init();

            RulesGrid.ItemsSource = _rules;
            AlertsGrid.ItemsSource = _alerts;
            AnomaliesGrid.ItemsSource = _anomalies;
            BriefsGrid.ItemsSource = _briefs;
            FeedbackGrid.ItemsSource = _feedbackQueries;

            LoadRules();
            LoadAlerts();
            LoadAnomalies();
            LoadBriefs();
            LoadFeedback();
        }

        private void LoadFeedback()
        {
            _feedbackQueries.Clear();
            try
            {
                var stats = FeedbackService.GetStats(30);
                StatTotalText.Text = stats.TotalFeedbacks.ToString();
                StatCorrectText.Text = stats.Correct.ToString();
                StatIncorrectText.Text = stats.Incorrect.ToString();
                StatPrecisionText.Text = stats.TotalFeedbacks == 0 ? "—" : $"{stats.PrecisionPercent:F1}%";
                foreach (var q in stats.ByQuery)
                {
                    _feedbackQueries.Add(new FeedbackQueryVm
                    {
                        Query = q.Query, Total = q.Total, Correct = q.Correct, Precision = q.Precision
                    });
                }
            }
            catch (Exception ex)
            {
                StatTotalText.Text = "—"; StatCorrectText.Text = "—"; StatIncorrectText.Text = "—";
                StatPrecisionText.Text = ex.Message.Substring(0, Math.Min(40, ex.Message.Length));
            }
        }

        private void RefreshFeedback_Click(object sender, RoutedEventArgs e) => LoadFeedback();

        // ───── Reguły ─────
        private void LoadRules()
        {
            _rules.Clear();
            foreach (var r in GuardService.GetAllRules())
            {
                _rules.Add(new RuleVm
                {
                    Id = r.Id, Name = r.Name, Prompt = r.Prompt,
                    Threshold = r.Threshold, CooldownMin = r.CooldownMin,
                    Enabled = r.Enabled, NotifySms = r.NotifySms,
                    RequiredConfirmations = r.RequiredConfirmations
                });
            }
        }

        private void RulesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _editing = RulesGrid.SelectedItem as RuleVm;
            if (_editing == null) { EditorPanel.IsEnabled = false; return; }
            EditorPanel.IsEnabled = true;
            RuleNameBox.Text = _editing.Name;
            RulePromptBox.Text = _editing.Prompt;
            RuleThresholdBox.Text = _editing.Threshold.ToString();
            RuleCooldownBox.Text = _editing.CooldownMin.ToString();
            RuleConfirmsBox.Text = _editing.RequiredConfirmations.ToString();
            RuleEnabledChk.IsChecked = _editing.Enabled;
            RuleSmsChk.IsChecked = _editing.NotifySms;
            TestStatus.Text = string.Empty;
        }

        private void NewRule_Click(object sender, RoutedEventArgs e)
        {
            _editing = new RuleVm { Threshold = 70, CooldownMin = 10, Enabled = true };
            EditorPanel.IsEnabled = true;
            RuleNameBox.Text = "Nowa reguła";
            RulePromptBox.Text = "Czy na zdjęciu widać...";
            RuleThresholdBox.Text = "70";
            RuleCooldownBox.Text = "10";
            RuleConfirmsBox.Text = "2";
            RuleEnabledChk.IsChecked = true;
            RuleSmsChk.IsChecked = false;
        }

        private void DeleteRule_Click(object sender, RoutedEventArgs e)
        {
            if (RulesGrid.SelectedItem is not RuleVm vm) return;
            if (MessageBox.Show($"Skasować regułę '{vm.Name}'?", "Potwierdź", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            GuardService.DeleteRule(vm.Id);
            LoadRules();
        }

        private void SaveRule_Click(object sender, RoutedEventArgs e)
        {
            if (_editing == null) return;

            string name = (RuleNameBox.Text ?? "").Trim();
            string prompt = (RulePromptBox.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(name)) { MessageBox.Show("Nazwa reguły nie może być pusta"); return; }
            if (string.IsNullOrWhiteSpace(prompt)) { MessageBox.Show("Pytanie (prompt) nie może być puste"); return; }
            if (prompt.Length < 10) { MessageBox.Show("Pytanie za krótkie - opisz co AI ma sprawdzać (min 10 znaków)"); return; }
            if (!int.TryParse(RuleThresholdBox.Text, out int th) || th < 0 || th > 100)
            { MessageBox.Show("Próg musi być liczbą 0-100"); return; }
            if (!int.TryParse(RuleCooldownBox.Text, out int cd) || cd < 1 || cd > 1440)
            { MessageBox.Show("Cooldown musi być liczbą 1-1440 (minut)"); return; }
            if (!int.TryParse(RuleConfirmsBox.Text, out int rc) || rc < 1 || rc > 10)
            { MessageBox.Show("Wymagane potwierdzenia: liczba 1-10"); return; }
            bool sms = RuleSmsChk.IsChecked == true;
            if (sms && !NotifyService.IsConfigured)
            {
                if (MessageBox.Show("Twilio nie jest skonfigurowany w secrets.json. SMS nie będzie wysłany. Zapisać mimo to?",
                    "Ostrzeżenie", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            }

            var rule = new GuardRule
            {
                Id = _editing.Id,
                Name = name,
                Prompt = prompt,
                Threshold = th,
                CooldownMin = cd,
                Enabled = RuleEnabledChk.IsChecked == true,
                NotifySms = sms,
                RequiredConfirmations = rc
            };
            GuardService.UpsertRule(rule);
            LoadRules();
        }

        private async void TestRule_Click(object sender, RoutedEventArgs e)
        {
            if (_editing == null) { TestStatus.Text = "Nic nie zaznaczone"; return; }

            string name = (RuleNameBox.Text ?? "").Trim();
            string prompt = (RulePromptBox.Text ?? "").Trim();
            if (!int.TryParse(RuleThresholdBox.Text, out int th)) th = 70;
            if (string.IsNullOrWhiteSpace(prompt) || prompt.Length < 5)
            { TestStatus.Text = "❌ Najpierw uzupełnij prompt"; return; }

            TestRuleBtn.IsEnabled = false;
            TestStatus.Text = "🧪 Sprawdzam... (może potrwać 30-90s)";
            try
            {
                var rule = new GuardRule { Id = _editing.Id, Name = name, Prompt = prompt, Threshold = th };
                var result = await GuardService.TestRuleAsync(rule, sampleSize: 100);
                TestStatus.Text =
                    $"✓ {result.Matches} matchy z {result.FramesChecked} klatek " +
                    $"({100.0 * result.Matches / Math.Max(1, result.FramesChecked):F1}%) " +
                    $"• VLM calls: {result.VlmCalls} (po prefiltrze) " +
                    $"• koszt: ${result.TotalCostUsd:F4}";
                if (result.Matches > 30)
                    TestStatus.Text += "\n⚠ Dużo trafień - rozważ surowszy prompt albo wyższy próg, albo wymagane potwierdzenia 3+";
                if (result.Matches == 0)
                    TestStatus.Text += "\n⚠ Zero trafień - prompt może być za precyzyjny albo CLIP prefilter za ostry";
            }
            catch (Exception ex) { TestStatus.Text = $"❌ {ex.Message}"; }
            finally { TestRuleBtn.IsEnabled = true; }
        }

        // ───── Alerty ─────
        private void LoadAlerts()
        {
            _alerts.Clear();
            foreach (var a in GuardService.GetRecentAlerts(100))
            {
                _alerts.Add(new AlertVm
                {
                    Id = a.Id, TsUtc = a.TsUtc, RuleName = a.RuleName,
                    CameraId = a.CameraId, Score = a.Score, Reason = a.Reason
                });
            }
        }

        private void RefreshAlerts_Click(object sender, RoutedEventArgs e) => LoadAlerts();

        // ───── Anomalie ─────
        private void LoadAnomalies()
        {
            _anomalies.Clear();
            foreach (var a in AnomalyService.GetRecentAnomalies(100))
            {
                _anomalies.Add(new AnomalyVm
                {
                    FrameId = a.FrameId, TsUtc = a.Ts, CameraId = a.CameraId,
                    Distance = a.Distance, FilePath = a.FilePath
                });
            }
        }

        private void RefreshAnomalies_Click(object sender, RoutedEventArgs e) => LoadAnomalies();

        private void RebuildBaseline_Click(object sender, RoutedEventArgs e)
        {
            RebuildBtn.IsEnabled = false;
            try
            {
                AnomalyService.RebuildBaseline(7);
                MessageBox.Show("Baseline przebudowany.", "OK", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally { RebuildBtn.IsEnabled = true; }
        }

        // ───── Briefy ─────
        private void LoadBriefs()
        {
            _briefs.Clear();
            foreach (var b in DailyBriefService.GetRecent(30))
            {
                _briefs.Add(new BriefVm
                {
                    Id = b.Id, Day = b.Day, Summary = b.Summary,
                    Created = b.Created, CostUsd = b.CostUsd,
                    SampleSize = b.SampleFrameIds.Count
                });
            }
        }

        private void RefreshBriefs_Click(object sender, RoutedEventArgs e) => LoadBriefs();

        private void BriefsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (BriefsGrid.SelectedItem is BriefVm vm)
                BriefDetailText.Text = vm.Summary;
        }

        private async void GenerateBrief_Click(object sender, RoutedEventArgs e)
        {
            GenerateBriefBtn.IsEnabled = false;
            BriefStatus.Text = "Generuję brief... (10-20 sek)";
            try
            {
                var brief = await DailyBriefService.GenerateAsync();
                BriefStatus.Text = $"✓ Gotowy: {brief.Day} (${brief.CostUsd:F4})";
                LoadBriefs();
                BriefDetailText.Text = brief.Summary;
            }
            catch (Exception ex)
            {
                BriefStatus.Text = $"Błąd: {ex.Message}";
            }
            finally { GenerateBriefBtn.IsEnabled = true; }
        }
    }
}
