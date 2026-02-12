using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Kalendarz1.Quiz
{
    public partial class QuizWindow : Window
    {
        // ════════════════════════════════════════════════════════════
        // Models
        // ════════════════════════════════════════════════════════════
        private class QuizQuestion
        {
            public int Id { get; set; }
            public string Chapter { get; set; } = "";
            public string Question { get; set; } = "";
            public List<string> Answers { get; set; } = new();
            public int CorrectIndex { get; set; }
            public string Explanation { get; set; } = "";
        }

        private class QuizData
        {
            public string Version { get; set; } = "";
            public string Source { get; set; } = "";
            public List<QuizQuestion> Questions { get; set; } = new();
        }

        private class SessionResult
        {
            public string UserId { get; set; } = "";
            public DateTime Date { get; set; }
            public int TotalQuestions { get; set; }
            public int CorrectAnswers { get; set; }
            public string Categories { get; set; } = "";
        }

        private class UserHistory
        {
            public List<SessionResult> Sessions { get; set; } = new();
        }

        private class HistoryDisplayItem
        {
            public string DateText { get; set; } = "";
            public string ScoreText { get; set; } = "";
            public string Detail { get; set; } = "";
            public SolidColorBrush ScoreColor { get; set; } = Brushes.Gray;
        }

        // ════════════════════════════════════════════════════════════
        // State
        // ════════════════════════════════════════════════════════════
        private List<QuizQuestion> _allQuestions = new();
        private List<QuizQuestion> _currentQuestions = new();
        private int _currentIndex;
        private int _correctCount;
        private int _wrongCount;
        private bool _answered;
        private readonly string _userId;
        private readonly string _questionsPath;
        private readonly string _historyPath;
        private readonly Random _rng = new();

        // Track per-chapter stats for current session
        private Dictionary<string, (int correct, int total)> _chapterStats = new();

        // Selected categories (null = all)
        private HashSet<string>? _selectedCategories;

        private readonly Button[] _answerButtons;

        public QuizWindow()
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);

            _userId = App.UserID;

            // Paths
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var quizDir = Path.Combine(baseDir, "Quiz");
            if (!Directory.Exists(quizDir))
                Directory.CreateDirectory(quizDir);

            _questionsPath = Path.Combine(quizDir, "quiz_questions.json");
            _historyPath = Path.Combine(quizDir, $"quiz_history_{_userId}.json");

            // Also check source directory for questions file
            var sourceQuizDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory);
            var altPath = FindQuestionsFile();
            if (!File.Exists(_questionsPath) && altPath != null)
                _questionsPath = altPath;

            _answerButtons = new[] { btnAnswerA, btnAnswerB, btnAnswerC, btnAnswerD };

            Loaded += QuizWindow_Loaded;
        }

        private string? FindQuestionsFile()
        {
            // Search up from base directory
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "Quiz", "quiz_questions.json");
                if (File.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
            return null;
        }

        // ════════════════════════════════════════════════════════════
        // Initialization
        // ════════════════════════════════════════════════════════════
        private void QuizWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadQuestions();
            BuildCategoryFilters();
            LoadHistory();
            StartQuiz();
        }

        private void LoadQuestions()
        {
            try
            {
                if (!File.Exists(_questionsPath))
                {
                    MessageBox.Show(
                        $"Nie znaleziono pliku z pytaniami:\n{_questionsPath}\n\nUpewnij się, że plik quiz_questions.json znajduje się w folderze Quiz.",
                        "Brak pytań", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var json = File.ReadAllText(_questionsPath);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var data = JsonSerializer.Deserialize<QuizData>(json, options);

                if (data?.Questions != null)
                    _allQuestions = data.Questions;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd wczytywania pytań:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BuildCategoryFilters()
        {
            pnlCategoryFilters.Children.Clear();

            // "All" button
            var btnAll = new ToggleButton
            {
                Content = "Wszystkie",
                IsChecked = true,
                Style = (Style)FindResource("CategoryChipStyle"),
                Tag = "__ALL__"
            };
            btnAll.Click += CategoryFilter_Click;
            pnlCategoryFilters.Children.Add(btnAll);

            var chapters = _allQuestions.Select(q => q.Chapter).Distinct().OrderBy(c => c).ToList();
            foreach (var chapter in chapters)
            {
                var count = _allQuestions.Count(q => q.Chapter == chapter);
                var btn = new ToggleButton
                {
                    Content = $"{chapter} ({count})",
                    IsChecked = false,
                    Style = (Style)FindResource("CategoryChipStyle"),
                    Tag = chapter
                };
                btn.Click += CategoryFilter_Click;
                pnlCategoryFilters.Children.Add(btn);
            }
        }

        private void CategoryFilter_Click(object sender, RoutedEventArgs e)
        {
            var clicked = (ToggleButton)sender;
            var tag = clicked.Tag?.ToString() ?? "";

            if (tag == "__ALL__")
            {
                // Deselect all others, select "All"
                clicked.IsChecked = true;
                foreach (var child in pnlCategoryFilters.Children.OfType<ToggleButton>())
                {
                    if (child.Tag?.ToString() != "__ALL__")
                        child.IsChecked = false;
                }
                _selectedCategories = null;
            }
            else
            {
                // Deselect "All"
                foreach (var child in pnlCategoryFilters.Children.OfType<ToggleButton>())
                {
                    if (child.Tag?.ToString() == "__ALL__")
                        child.IsChecked = false;
                }

                // Gather selected
                var selected = pnlCategoryFilters.Children.OfType<ToggleButton>()
                    .Where(b => b.IsChecked == true && b.Tag?.ToString() != "__ALL__")
                    .Select(b => b.Tag?.ToString() ?? "")
                    .ToHashSet();

                if (selected.Count == 0)
                {
                    // Nothing selected → revert to all
                    foreach (var child in pnlCategoryFilters.Children.OfType<ToggleButton>())
                    {
                        if (child.Tag?.ToString() == "__ALL__")
                            child.IsChecked = true;
                    }
                    _selectedCategories = null;
                }
                else
                {
                    _selectedCategories = selected;
                }
            }

            StartQuiz();
        }

        // ════════════════════════════════════════════════════════════
        // Quiz Logic
        // ════════════════════════════════════════════════════════════
        private void StartQuiz()
        {
            var filtered = _selectedCategories == null
                ? _allQuestions.ToList()
                : _allQuestions.Where(q => _selectedCategories.Contains(q.Chapter)).ToList();

            // Shuffle
            _currentQuestions = filtered.OrderBy(_ => _rng.Next()).ToList();
            _currentIndex = 0;
            _correctCount = 0;
            _wrongCount = 0;
            _answered = false;
            _chapterStats.Clear();

            UpdateScoreDisplay();
            ShowQuestion();
        }

        private void ShowQuestion()
        {
            if (_currentQuestions.Count == 0)
            {
                txtQuestion.Text = "Brak pytań do wyświetlenia. Wybierz kategorię lub sprawdź plik z pytaniami.";
                txtChapter.Text = "";
                foreach (var btn in _answerButtons)
                {
                    btn.Content = "";
                    btn.IsEnabled = false;
                }
                pnlExplanation.Visibility = Visibility.Collapsed;
                btnNext.Visibility = Visibility.Collapsed;
                return;
            }

            if (_currentIndex >= _currentQuestions.Count)
            {
                FinishQuiz();
                return;
            }

            var q = _currentQuestions[_currentIndex];
            _answered = false;

            // Update UI
            txtChapter.Text = q.Chapter;
            txtQuestion.Text = q.Question;
            txtQuestionNumber.Text = $"{_currentIndex + 1} / {_currentQuestions.Count}";

            // Set answers
            var labels = new[] { "A", "B", "C", "D" };
            for (int i = 0; i < _answerButtons.Length; i++)
            {
                if (i < q.Answers.Count)
                {
                    _answerButtons[i].Content = $"  {labels[i]}.  {q.Answers[i]}";
                    _answerButtons[i].Visibility = Visibility.Visible;
                    _answerButtons[i].IsEnabled = true;
                    _answerButtons[i].Background = Brushes.White;
                    _answerButtons[i].BorderBrush = new SolidColorBrush(Color.FromRgb(189, 195, 199));
                    _answerButtons[i].Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80));
                }
                else
                {
                    _answerButtons[i].Visibility = Visibility.Collapsed;
                }
            }

            pnlExplanation.Visibility = Visibility.Collapsed;
            btnNext.Visibility = Visibility.Collapsed;

            // Progress bar
            UpdateProgressBar();
        }

        private void BtnAnswer_Click(object sender, RoutedEventArgs e)
        {
            if (_answered || _currentIndex >= _currentQuestions.Count) return;

            _answered = true;
            var btn = (Button)sender;
            var selectedIndex = int.Parse(btn.Tag.ToString()!);
            var q = _currentQuestions[_currentIndex];
            var isCorrect = selectedIndex == q.CorrectIndex;

            // Track stats
            if (!_chapterStats.ContainsKey(q.Chapter))
                _chapterStats[q.Chapter] = (0, 0);

            var stats = _chapterStats[q.Chapter];
            _chapterStats[q.Chapter] = (stats.correct + (isCorrect ? 1 : 0), stats.total + 1);

            if (isCorrect)
                _correctCount++;
            else
                _wrongCount++;

            // Disable all buttons
            foreach (var b in _answerButtons)
                b.IsEnabled = false;

            // Highlight correct answer
            var correctBtn = _answerButtons[q.CorrectIndex];
            correctBtn.Background = new SolidColorBrush(Color.FromRgb(212, 239, 223)); // green
            correctBtn.BorderBrush = new SolidColorBrush(Color.FromRgb(39, 174, 96));
            correctBtn.Foreground = new SolidColorBrush(Color.FromRgb(30, 130, 76));

            // Highlight wrong answer if selected wrong
            if (!isCorrect)
            {
                btn.Background = new SolidColorBrush(Color.FromRgb(250, 219, 216)); // red
                btn.BorderBrush = new SolidColorBrush(Color.FromRgb(231, 76, 60));
                btn.Foreground = new SolidColorBrush(Color.FromRgb(176, 58, 46));
            }

            // Show explanation
            pnlExplanation.Visibility = Visibility.Visible;
            if (isCorrect)
            {
                pnlExplanation.Background = new SolidColorBrush(Color.FromRgb(234, 250, 241));
                txtExplanationHeader.Text = "✓ Poprawna odpowiedź!";
                txtExplanationHeader.Foreground = new SolidColorBrush(Color.FromRgb(39, 174, 96));
            }
            else
            {
                pnlExplanation.Background = new SolidColorBrush(Color.FromRgb(253, 237, 236));
                txtExplanationHeader.Text = $"✗ Błędna odpowiedź. Poprawna: {(char)('A' + q.CorrectIndex)}";
                txtExplanationHeader.Foreground = new SolidColorBrush(Color.FromRgb(231, 76, 60));
            }
            txtExplanation.Text = q.Explanation;

            // Show next button
            btnNext.Visibility = Visibility.Visible;
            if (_currentIndex >= _currentQuestions.Count - 1)
                btnNext.Content = "Podsumowanie \u27A1";

            UpdateScoreDisplay();
            UpdateChapterStats();
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            _currentIndex++;
            ShowQuestion();
        }

        private void BtnRestart_Click(object sender, RoutedEventArgs e)
        {
            StartQuiz();
        }

        private void FinishQuiz()
        {
            // Save session
            SaveSession();

            // Show summary
            var total = _correctCount + _wrongCount;
            var percent = total > 0 ? (int)Math.Round(100.0 * _correctCount / total) : 0;

            txtChapter.Text = "Koniec quizu";
            txtQuestion.Text = $"Gratulacje! Ukończyłeś quiz.\n\n" +
                               $"Wynik: {_correctCount} / {total} ({percent}%)\n\n" +
                               $"Poprawne: {_correctCount}\n" +
                               $"Błędne: {_wrongCount}\n\n" +
                               "Kliknij 'Restart' aby rozpocząć nowy quiz.";

            foreach (var btn in _answerButtons)
            {
                btn.Visibility = Visibility.Collapsed;
            }

            pnlExplanation.Visibility = Visibility.Collapsed;
            btnNext.Visibility = Visibility.Collapsed;

            LoadHistory();
        }

        // ════════════════════════════════════════════════════════════
        // UI Updates
        // ════════════════════════════════════════════════════════════
        private void UpdateScoreDisplay()
        {
            txtCorrectCount.Text = _correctCount.ToString();
            txtWrongCount.Text = _wrongCount.ToString();

            var total = _correctCount + _wrongCount;
            var percent = total > 0 ? (int)Math.Round(100.0 * _correctCount / total) : 0;

            txtScorePercent.Text = $"{percent}%";
            txtScoreDetail.Text = $"{_correctCount} z {total} poprawnych";

            // Score bar color
            var scoreColor = percent >= 70
                ? Color.FromRgb(39, 174, 96)     // green
                : percent >= 40
                    ? Color.FromRgb(243, 156, 18) // yellow
                    : Color.FromRgb(231, 76, 60); // red

            scoreBar.Background = new SolidColorBrush(scoreColor);

            // Animate score bar width
            if (total > 0)
            {
                var targetWidth = scoreBar.Parent is Grid grid
                    ? grid.ActualWidth * percent / 100.0
                    : 0;
                if (targetWidth > 0)
                {
                    var anim = new DoubleAnimation(targetWidth, TimeSpan.FromMilliseconds(400))
                    {
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    };
                    scoreBar.BeginAnimation(WidthProperty, anim);
                }
            }
        }

        private void UpdateProgressBar()
        {
            if (_currentQuestions.Count == 0) return;
            var parent = progressBar.Parent as Grid;
            if (parent == null || parent.ActualWidth <= 0) return;

            var targetWidth = parent.ActualWidth * (_currentIndex + 1) / _currentQuestions.Count;
            var anim = new DoubleAnimation(targetWidth, TimeSpan.FromMilliseconds(300))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            progressBar.BeginAnimation(WidthProperty, anim);
        }

        private void UpdateChapterStats()
        {
            var items = new List<object>();
            foreach (var kv in _chapterStats.OrderBy(k => k.Key))
            {
                var pct = kv.Value.total > 0
                    ? (int)Math.Round(100.0 * kv.Value.correct / kv.Value.total)
                    : 0;

                var panel = new StackPanel { Margin = new Thickness(0, 3, 0, 3) };

                var header = new Grid();
                header.Children.Add(new TextBlock
                {
                    Text = kv.Key,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80))
                });
                header.Children.Add(new TextBlock
                {
                    Text = $"{kv.Value.correct}/{kv.Value.total} ({pct}%)",
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(127, 140, 141)),
                    HorizontalAlignment = HorizontalAlignment.Right
                });
                panel.Children.Add(header);

                // Mini progress bar
                var barGrid = new Grid { Height = 4, Margin = new Thickness(0, 3, 0, 0) };
                barGrid.Children.Add(new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(236, 240, 241)),
                    CornerRadius = new CornerRadius(2)
                });
                var barColor = pct >= 70
                    ? Color.FromRgb(39, 174, 96)
                    : pct >= 40
                        ? Color.FromRgb(243, 156, 18)
                        : Color.FromRgb(231, 76, 60);
                barGrid.Children.Add(new Border
                {
                    Background = new SolidColorBrush(barColor),
                    CornerRadius = new CornerRadius(2),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Width = 0
                });
                panel.Children.Add(barGrid);

                // We need to animate after layout
                panel.Loaded += (s, e) =>
                {
                    if (barGrid.ActualWidth > 0)
                    {
                        var fill = barGrid.Children.OfType<Border>().Last();
                        var anim = new DoubleAnimation(barGrid.ActualWidth * pct / 100.0,
                            TimeSpan.FromMilliseconds(400));
                        fill.BeginAnimation(WidthProperty, anim);
                    }
                };

                items.Add(panel);
            }

            lstChapterStats.ItemsSource = null;
            lstChapterStats.Items.Clear();
            foreach (var item in items)
                lstChapterStats.Items.Add(item);
        }

        // ════════════════════════════════════════════════════════════
        // History / Persistence
        // ════════════════════════════════════════════════════════════
        private void SaveSession()
        {
            var total = _correctCount + _wrongCount;
            if (total == 0) return;

            try
            {
                var history = LoadHistoryData();
                history.Sessions.Add(new SessionResult
                {
                    UserId = _userId,
                    Date = DateTime.Now,
                    TotalQuestions = total,
                    CorrectAnswers = _correctCount,
                    Categories = _selectedCategories != null
                        ? string.Join(", ", _selectedCategories)
                        : "Wszystkie"
                });

                // Keep last 50 sessions
                if (history.Sessions.Count > 50)
                    history.Sessions = history.Sessions.Skip(history.Sessions.Count - 50).ToList();

                var json = JsonSerializer.Serialize(history, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_historyPath, json);
            }
            catch { /* ignore save errors */ }
        }

        private UserHistory LoadHistoryData()
        {
            try
            {
                if (File.Exists(_historyPath))
                {
                    var json = File.ReadAllText(_historyPath);
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    return JsonSerializer.Deserialize<UserHistory>(json, options) ?? new UserHistory();
                }
            }
            catch { }
            return new UserHistory();
        }

        private void LoadHistory()
        {
            var history = LoadHistoryData();
            var items = history.Sessions
                .Where(s => s.UserId == _userId)
                .OrderByDescending(s => s.Date)
                .Take(20)
                .Select(s =>
                {
                    var pct = s.TotalQuestions > 0
                        ? (int)Math.Round(100.0 * s.CorrectAnswers / s.TotalQuestions)
                        : 0;
                    var color = pct >= 70
                        ? new SolidColorBrush(Color.FromRgb(39, 174, 96))
                        : pct >= 40
                            ? new SolidColorBrush(Color.FromRgb(243, 156, 18))
                            : new SolidColorBrush(Color.FromRgb(231, 76, 60));

                    return new HistoryDisplayItem
                    {
                        DateText = s.Date.ToString("dd.MM HH:mm"),
                        ScoreText = $"{pct}% ({s.CorrectAnswers}/{s.TotalQuestions})",
                        Detail = s.Categories,
                        ScoreColor = color
                    };
                })
                .ToList();

            lstHistory.ItemsSource = items;
        }
    }
}
