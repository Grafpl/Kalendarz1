using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Kalendarz1.Reklamacje
{
    public partial class FormRozpatrzenieWindow : Window
    {
        private readonly string connectionString;
        private readonly int idReklamacji;
        private readonly string userId;
        private readonly string aktualnyStatus;

        public bool Zapisano { get; private set; }

        // Wybrany docelowy status (ustawiany po kliknieciu karty)
        private string wybranyStatus;

        // Referencje do pol formularza (tworzone dynamicznie)
        private TextBox txtPrzyczyna;
        private TextBox txtAkcje;
        private TextBox txtPowod;

        // ========================================
        // DEFINICJE STATUSOW
        // ========================================

        internal static readonly Dictionary<string, List<string>> dozwolonePrzejscia = new Dictionary<string, List<string>>
        {
            { "Nowa",            new List<string> { "Przyjeta" } },
            { "Przyjeta",       new List<string> { "Zaakceptowana", "Odrzucona", "Nowa" } },
            { "Zaakceptowana",  new List<string> { "Nowa" } },
            { "Odrzucona",      new List<string> { "Nowa" } },
            { "Zamknieta",      new List<string>() },
            { "Zamknięta",      new List<string>() },
            { "W trakcie",      new List<string> { "Przyjeta", "Zaakceptowana", "Odrzucona" } },
            { "W analizie",     new List<string> { "Przyjeta", "Zaakceptowana", "Odrzucona", "Nowa" } },
            { "W trakcie realizacji", new List<string> { "Zaakceptowana", "Odrzucona", "Nowa" } },
            { "Oczekuje na dostawce", new List<string> { "Przyjeta", "Zaakceptowana", "Nowa" } }
        };

        internal static readonly string[] statusPipeline = {
            "Nowa", "Przyjeta", "Zaakceptowana", "Odrzucona"
        };

        internal static readonly Dictionary<string, string> statusKolory = new Dictionary<string, string>
        {
            { "Nowa",            "#3498DB" },
            { "Przyjeta",       "#F39C12" },
            { "Zaakceptowana",  "#27AE60" },
            { "Odrzucona",      "#E74C3C" },
            { "Zamknieta",      "#95A5A6" },
            { "Zamknięta",      "#95A5A6" },
            { "W trakcie",             "#F39C12" },
            { "W analizie",            "#F39C12" },
            { "W trakcie realizacji",  "#E67E22" },
            { "Oczekuje na dostawce",  "#9B59B6" }
        };

        public FormRozpatrzenieWindow(string connString, int reklamacjaId, string aktualnyStatus, string user)
        {
            connectionString = connString;
            idReklamacji = reklamacjaId;
            this.aktualnyStatus = aktualnyStatus;
            userId = user;

            InitializeComponent();
            WindowIconHelper.SetIcon(this);

            txtHeader.Text = $"ROZPATRZENIE #{idReklamacji}";
            txtSubheader.Text = $"Aktualny status: {aktualnyStatus}";

            string userName = PobierzNazweUzytkownika(userId);
            txtRozpatrujacyNazwa.Text = userName;
            txtAvatarRozpatrujacy.Text = GetInitials(userName);
            avatarRozpatrujacy.Background = GetAvatarBrush(userName);

            var avatarSource = LoadWpfAvatar(userId, userName, 80);
            if (avatarSource != null)
            {
                imgBrushAvatarRozpatrujacy.ImageSource = avatarSource;
                ellipseAvatarRozpatrujacy.Visibility = Visibility.Visible;
            }

            BudujPipeline();
            BudujKartyAkcji();
            DodajHistorie();
        }

        // ========================================
        // DYNAMICZNA TRESC — karty akcji
        // ========================================

        private void BudujKartyAkcji()
        {
            panelContent.Children.Clear();
            wybranyStatus = null;
            btnZapisz.Visibility = Visibility.Collapsed;

            // Pobierz dozwolone przejscia
            var dozwolone = new List<string>();
            bool isAdmin = userId == "11111";
            if (isAdmin)
            {
                foreach (var s in statusPipeline)
                    if (s != aktualnyStatus) dozwolone.Add(s);
            }
            else if (dozwolonePrzejscia.ContainsKey(aktualnyStatus))
            {
                dozwolone = dozwolonePrzejscia[aktualnyStatus];
            }

            if (dozwolone.Count == 0)
            {
                // Status finalny — brak akcji
                var info = UtworzKarteInfo(
                    "Reklamacja zamknieta",
                    "Ta reklamacja ma status koncowy i nie mozna juz zmienic jej statusu.",
                    "#95A5A6", "#ECEFF1");
                panelContent.Children.Add(info);
                return;
            }

            // Naglowek
            panelContent.Children.Add(new TextBlock
            {
                Text = "WYBIERZ AKCJE",
                FontSize = 11, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7F8C8D")),
                Margin = new Thickness(2, 0, 0, 12)
            });

            // === PRZYJETA (jednoklick, bez pol) ===
            if (dozwolone.Contains("Przyjeta"))
            {
                var karta = UtworzKarteAkcji(
                    "PRZYJETA",
                    "Przyjmij reklamacje do rozpatrzenia. Nie wymaga komentarza — po prostu kliknij.",
                    "#F39C12", "#FFF8E1", "#F39C12",
                    "Przyjeta", natychmiastowyZapis: true);
                panelContent.Children.Add(karta);
            }

            // === ZAAKCEPTOWANA (z polami) ===
            if (dozwolone.Contains("Zaakceptowana"))
            {
                var karta = UtworzKarteAkcji(
                    "ZAAKCEPTOWANA",
                    "Reklamacja jest zasadna — podaj przyczyne i akcje naprawcze.",
                    "#27AE60", "#E8F5E9", "#27AE60",
                    "Zaakceptowana", natychmiastowyZapis: false);
                panelContent.Children.Add(karta);
            }

            // === ODRZUCONA (z powodem) ===
            if (dozwolone.Contains("Odrzucona"))
            {
                var karta = UtworzKarteAkcji(
                    "ODRZUCONA",
                    "Reklamacja niezasadna — podaj powod odrzucenia.",
                    "#E74C3C", "#FFEBEE", "#E74C3C",
                    "Odrzucona", natychmiastowyZapis: false);
                panelContent.Children.Add(karta);
            }

            // === COFNIJ DO NOWEJ (jednoklick) ===
            if (dozwolone.Contains("Nowa") && aktualnyStatus != "Nowa")
            {
                // Separator
                panelContent.Children.Add(new Border
                {
                    Height = 1, Margin = new Thickness(0, 6, 0, 6),
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0E0E0"))
                });

                var karta = UtworzKarteAkcji(
                    "COFNIJ",
                    "Cofnij reklamacje do statusu 'Nowa' — umozliwia ponowne rozpatrzenie.",
                    "#95A5A6", "#F5F5F5", "#95A5A6",
                    "Nowa", natychmiastowyZapis: true);
                panelContent.Children.Add(karta);
            }
        }

        private Border UtworzKarteAkcji(string tytul, string opis, string kolorHex,
            string bgHex, string borderHex, string docelowyStatus, bool natychmiastowyZapis)
        {
            var kolor = (Color)ColorConverter.ConvertFromString(kolorHex);
            var bg = (Color)ColorConverter.ConvertFromString(bgHex);
            var brdr = (Color)ColorConverter.ConvertFromString(borderHex);

            var card = new Border
            {
                Background = new SolidColorBrush(bg),
                BorderBrush = new SolidColorBrush(Color.FromArgb(80, brdr.R, brdr.G, brdr.B)),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(20, 16, 20, 16),
                Margin = new Thickness(0, 0, 0, 12),
                Cursor = Cursors.Hand
            };

            var root = new StackPanel();

            // Naglowek karty z ikona
            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Kolorowa kropka
            var dot = new Border
            {
                Width = 14, Height = 14,
                CornerRadius = new CornerRadius(7),
                Background = new SolidColorBrush(kolor),
                Margin = new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(dot, 0);
            header.Children.Add(dot);

            var headerText = new StackPanel();
            headerText.Children.Add(new TextBlock
            {
                Text = tytul,
                FontSize = 16, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(kolor)
            });
            headerText.Children.Add(new TextBlock
            {
                Text = opis,
                FontSize = 11, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7F8C8D")),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 3, 0, 0)
            });
            Grid.SetColumn(headerText, 1);
            header.Children.Add(headerText);

            // Strzalka / ikona akcji
            var arrow = new TextBlock
            {
                Text = natychmiastowyZapis ? "➜" : "▼",
                FontSize = 18, Foreground = new SolidColorBrush(Color.FromArgb(120, kolor.R, kolor.G, kolor.B)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0)
            };
            Grid.SetColumn(arrow, 2);
            header.Children.Add(arrow);

            root.Children.Add(header);

            // Panel na pola (ukryty, pokazywany po kliknieciu)
            StackPanel polaPanel = null;
            if (!natychmiastowyZapis)
            {
                polaPanel = new StackPanel { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 16, 0, 0) };

                // Separator
                polaPanel.Children.Add(new Border
                {
                    Height = 1,
                    Background = new SolidColorBrush(Color.FromArgb(60, brdr.R, brdr.G, brdr.B)),
                    Margin = new Thickness(0, 0, 0, 14)
                });

                if (docelowyStatus == "Zaakceptowana")
                {
                    // Przyczyna glowna
                    polaPanel.Children.Add(UtworzEtykiete("PRZYCZYNA GLOWNA *", kolorHex));
                    txtPrzyczyna = UtworzPoleTextowe("Co bylo przyczyna problemu?", "#A5D6A7", "#F8FFF8");
                    polaPanel.Children.Add(txtPrzyczyna);

                    // Akcje naprawcze
                    polaPanel.Children.Add(UtworzEtykiete("AKCJE NAPRAWCZE *", kolorHex));
                    txtAkcje = UtworzPoleTextowe("Co robimy aby naprawic sytuacje?", "#A5D6A7", "#F8FFF8");
                    polaPanel.Children.Add(txtAkcje);
                }
                else if (docelowyStatus == "Odrzucona")
                {
                    // Powod odrzucenia
                    polaPanel.Children.Add(UtworzEtykiete("POWOD ODRZUCENIA *", kolorHex));
                    txtPowod = UtworzPoleTextowe("Dlaczego reklamacja jest odrzucana?", "#F5B7B1", "#FFF8F7");
                    txtPowod.MinHeight = 100;
                    polaPanel.Children.Add(txtPowod);
                }

                root.Children.Add(polaPanel);
            }

            card.Child = root;

            // Klikniecie na karte
            card.MouseLeftButtonDown += (s, e) =>
            {
                if (natychmiastowyZapis)
                {
                    // PRZYJETA — natychmiastowy zapis bez pol
                    wybranyStatus = docelowyStatus;
                    ZapiszIZamknij();
                }
                else
                {
                    // Rozwin formularz
                    if (polaPanel.Visibility == Visibility.Collapsed)
                    {
                        // Schowaj inne panele
                        ZwinWszystkieKarty();
                        polaPanel.Visibility = Visibility.Visible;
                        arrow.Text = "▲";
                        card.BorderBrush = new SolidColorBrush(kolor);
                        card.BorderThickness = new Thickness(2.5);
                        wybranyStatus = docelowyStatus;

                        // Pokaz przycisk Zapisz
                        btnZapisz.Content = docelowyStatus == "Zaakceptowana" ? "Zaakceptuj" : "Odrzuc";
                        btnZapisz.Background = new SolidColorBrush(kolor);
                        btnZapisz.Visibility = Visibility.Visible;

                        // Focus na pierwsze pole
                        if (docelowyStatus == "Zaakceptowana" && txtPrzyczyna != null)
                            txtPrzyczyna.Focus();
                        else if (docelowyStatus == "Odrzucona" && txtPowod != null)
                            txtPowod.Focus();
                    }
                    else
                    {
                        polaPanel.Visibility = Visibility.Collapsed;
                        arrow.Text = "▼";
                        card.BorderBrush = new SolidColorBrush(Color.FromArgb(80, brdr.R, brdr.G, brdr.B));
                        card.BorderThickness = new Thickness(1.5);
                        wybranyStatus = null;
                        btnZapisz.Visibility = Visibility.Collapsed;
                    }
                }
            };

            // Hover effect
            var originalBg = card.Background;
            card.MouseEnter += (s, e) =>
            {
                if (polaPanel == null || polaPanel.Visibility == Visibility.Collapsed)
                    card.Background = new SolidColorBrush(Color.FromArgb(
                        (byte)Math.Min(255, bg.A + 30), bg.R,
                        (byte)Math.Max(0, bg.G - 10),
                        (byte)Math.Max(0, bg.B - 10)));
            };
            card.MouseLeave += (s, e) =>
            {
                if (polaPanel == null || polaPanel.Visibility == Visibility.Collapsed)
                    card.Background = originalBg;
            };

            return card;
        }

        private void ZwinWszystkieKarty()
        {
            foreach (var child in panelContent.Children)
            {
                if (child is Border b && b.Child is StackPanel sp)
                {
                    foreach (var inner in sp.Children)
                    {
                        if (inner is StackPanel pola && pola.Margin.Top > 10)
                        {
                            pola.Visibility = Visibility.Collapsed;
                        }
                    }
                    // Reset border
                    if (b.Tag is string hex)
                    {
                        var c = (Color)ColorConverter.ConvertFromString(hex);
                        b.BorderBrush = new SolidColorBrush(Color.FromArgb(80, c.R, c.G, c.B));
                        b.BorderThickness = new Thickness(1.5);
                    }
                }
            }
        }

        private TextBlock UtworzEtykiete(string text, string kolorHex)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 10.5, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(kolorHex)),
                Margin = new Thickness(0, 0, 0, 6)
            };
        }

        private TextBox UtworzPoleTextowe(string placeholder, string borderHex, string bgHex)
        {
            var tb = new TextBox
            {
                FontSize = 13,
                Padding = new Thickness(12, 10, 12, 10),
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MinHeight = 70,
                MaxHeight = 140,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(borderHex)),
                BorderThickness = new Thickness(1.5),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bgHex)),
                Margin = new Thickness(0, 0, 0, 14)
            };
            // Placeholder via GotFocus/LostFocus
            tb.Tag = placeholder;
            tb.Text = placeholder;
            tb.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BDC3C7"));
            tb.GotFocus += (s, e) =>
            {
                if (tb.Text == (string)tb.Tag)
                {
                    tb.Text = "";
                    tb.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2C3E50"));
                }
            };
            tb.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(tb.Text))
                {
                    tb.Text = (string)tb.Tag;
                    tb.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BDC3C7"));
                }
            };
            return tb;
        }

        private string PobierzTekst(TextBox tb)
        {
            if (tb == null) return "";
            string text = tb.Text?.Trim() ?? "";
            if (text == (string)tb.Tag) return ""; // placeholder
            return text;
        }

        private Border UtworzKarteInfo(string tytul, string opis, string kolorHex, string bgHex)
        {
            var card = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bgHex)),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(24, 20, 24, 20),
                Margin = new Thickness(0, 0, 0, 12)
            };
            var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            sp.Children.Add(new TextBlock
            {
                Text = tytul,
                FontSize = 16, FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(kolorHex)),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            sp.Children.Add(new TextBlock
            {
                Text = opis,
                FontSize = 12,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95A5A6")),
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 6, 0, 0)
            });
            card.Child = sp;
            return card;
        }

        // ========================================
        // HISTORIA (na dole)
        // ========================================

        private void DodajHistorie()
        {
            try
            {
                var items = new List<HistoriaMiniItem>();
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(@"
                        SELECT TOP 5 h.DataZmiany, h.PoprzedniStatus, h.StatusNowy,
                               ISNULL(o.Name, h.UserID) AS Uzytkownik
                        FROM [dbo].[ReklamacjeHistoria] h
                        LEFT JOIN [dbo].[operators] o ON h.UserID = o.ID
                        WHERE h.IdReklamacji = @Id
                        ORDER BY h.DataZmiany DESC", conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", idReklamacji);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string statusNowy = reader["StatusNowy"] != DBNull.Value ? reader["StatusNowy"].ToString() : "";
                                string hexColor = statusKolory.ContainsKey(statusNowy) ? statusKolory[statusNowy] : "#BDC3C7";

                                items.Add(new HistoriaMiniItem
                                {
                                    PoprzedniStatus = reader["PoprzedniStatus"] != DBNull.Value ? reader["PoprzedniStatus"].ToString() : "",
                                    StatusNowy = statusNowy,
                                    Uzytkownik = reader["Uzytkownik"] != DBNull.Value ? reader["Uzytkownik"].ToString() : "",
                                    Data = reader["DataZmiany"] != DBNull.Value ? Convert.ToDateTime(reader["DataZmiany"]).ToString("dd.MM HH:mm") : "",
                                    Kolor = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hexColor))
                                });
                            }
                        }
                    }
                }

                if (items.Count == 0) return;

                panelContent.Children.Add(new TextBlock
                {
                    Text = "OSTATNIE ZMIANY",
                    FontSize = 10.5, FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95A5A6")),
                    Margin = new Thickness(2, 8, 0, 8)
                });

                foreach (var item in items)
                {
                    var row = new Border
                    {
                        Padding = new Thickness(10, 6, 10, 6),
                        Margin = new Thickness(0, 0, 0, 3),
                        Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FAFAFA")),
                        CornerRadius = new CornerRadius(6)
                    };
                    var g = new Grid();
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    var dot = new Ellipse { Width = 7, Height = 7, Fill = item.Kolor, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
                    Grid.SetColumn(dot, 0);
                    g.Children.Add(dot);

                    var sp = new StackPanel();
                    var statusLine = new TextBlock { FontSize = 10.5 };
                    statusLine.Inlines.Add(new System.Windows.Documents.Run(item.PoprzedniStatus) { Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95A5A6")) });
                    statusLine.Inlines.Add(new System.Windows.Documents.Run(" \u25B6 ") { Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BDC3C7")) });
                    statusLine.Inlines.Add(new System.Windows.Documents.Run(item.StatusNowy) { FontWeight = FontWeights.SemiBold, Foreground = item.Kolor });
                    sp.Children.Add(statusLine);
                    sp.Children.Add(new TextBlock { Text = item.Uzytkownik, FontSize = 9, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BDC3C7")) });
                    Grid.SetColumn(sp, 1);
                    g.Children.Add(sp);

                    var dt = new TextBlock { Text = item.Data, FontSize = 9, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BDC3C7")), VerticalAlignment = VerticalAlignment.Center };
                    Grid.SetColumn(dt, 2);
                    g.Children.Add(dt);

                    row.Child = g;
                    panelContent.Children.Add(row);
                }
            }
            catch { }
        }

        // ========================================
        // ZAPIS
        // ========================================

        private void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(wybranyStatus)) return;

            // Walidacja pol
            if (wybranyStatus == "Zaakceptowana")
            {
                string przyczyna = PobierzTekst(txtPrzyczyna);
                string akcje = PobierzTekst(txtAkcje);
                if (string.IsNullOrEmpty(przyczyna))
                {
                    PokazBlad("Podaj przyczyne glowna.");
                    txtPrzyczyna?.Focus();
                    return;
                }
                if (string.IsNullOrEmpty(akcje))
                {
                    PokazBlad("Podaj akcje naprawcze.");
                    txtAkcje?.Focus();
                    return;
                }
            }
            else if (wybranyStatus == "Odrzucona")
            {
                string powod = PobierzTekst(txtPowod);
                if (string.IsNullOrEmpty(powod))
                {
                    PokazBlad("Podaj powod odrzucenia.");
                    txtPowod?.Focus();
                    return;
                }
            }

            ZapiszIZamknij();
        }

        private void ZapiszIZamknij()
        {
            if (string.IsNullOrEmpty(wybranyStatus)) return;

            try
            {
                btnZapisz.IsEnabled = false;
                txtError.Visibility = Visibility.Collapsed;

                string przyczyna = PobierzTekst(txtPrzyczyna);
                string akcje = PobierzTekst(txtAkcje);
                string powod = PobierzTekst(txtPowod);

                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Mapowanie Status -> StatusV2
                    string nowyStatusV2 = wybranyStatus switch
                    {
                        "Przyjeta" => StatusyV2.W_ANALIZIE,
                        "Zaakceptowana" => StatusyV2.ZASADNA,
                        "Odrzucona" => StatusyV2.ODRZUCONA,
                        _ => StatusyV2.ZGLOSZONA
                    };

                    bool cofanie = wybranyStatus == "Nowa";

                    var setClauses = new List<string>
                    {
                        "Status = @NowyStatus",
                        "StatusV2 = @StatusV2",
                        "OsobaRozpatrujaca = @UserID",
                        "DataModyfikacji = GETDATE()"
                    };

                    if (cofanie)
                    {
                        // Cofniecie — wyzeruj analize, przywroc WymagaUzupelnienia dla korekt
                        setClauses.Add("DataAnalizy = NULL");
                        setClauses.Add("UserAnalizy = NULL");
                        setClauses.Add("DecyzjaJakosci = NULL");
                        setClauses.Add("WymagaUzupelnienia = CASE WHEN TypReklamacji = 'Faktura korygujaca' AND (PowiazanaReklamacjaId IS NULL OR PowiazanaReklamacjaId = 0) THEN 1 ELSE 0 END");
                    }
                    else
                    {
                        setClauses.Add("WymagaUzupelnienia = CASE WHEN @StatusV2 IN ('ZASADNA','ODRZUCONA','ZAMKNIETA') THEN 0 ELSE WymagaUzupelnienia END");
                        setClauses.Add("DataAnalizy = CASE WHEN DataAnalizy IS NULL THEN GETDATE() ELSE DataAnalizy END");
                        setClauses.Add("UserAnalizy = CASE WHEN UserAnalizy IS NULL THEN @UserID ELSE UserAnalizy END");
                    }

                    if (!string.IsNullOrEmpty(przyczyna))
                        setClauses.Add("PrzyczynaGlowna = @PrzyczynaGlowna");
                    if (!string.IsNullOrEmpty(akcje))
                        setClauses.Add("AkcjeNaprawcze = @AkcjeNaprawcze");
                    if (!string.IsNullOrEmpty(powod))
                        setClauses.Add("NotatkaJakosci = ISNULL(NotatkaJakosci, '') + CHAR(13) + CHAR(10) + @NowyKomentarz");

                    string updateSql = $"UPDATE [dbo].[Reklamacje] SET {string.Join(", ", setClauses)} WHERE Id = @Id";

                    using (var cmd = new SqlCommand(updateSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", idReklamacji);
                        cmd.Parameters.AddWithValue("@NowyStatus", wybranyStatus);
                        cmd.Parameters.AddWithValue("@StatusV2", nowyStatusV2);
                        cmd.Parameters.AddWithValue("@UserID", userId);
                        if (!string.IsNullOrEmpty(przyczyna))
                            cmd.Parameters.AddWithValue("@PrzyczynaGlowna", przyczyna);
                        if (!string.IsNullOrEmpty(akcje))
                            cmd.Parameters.AddWithValue("@AkcjeNaprawcze", akcje);
                        if (!string.IsNullOrEmpty(powod))
                            cmd.Parameters.AddWithValue("@NowyKomentarz", $"[{DateTime.Now:yyyy-MM-dd HH:mm}] Powod odrzucenia: {powod}");
                        cmd.ExecuteNonQuery();
                    }

                    // Historia
                    string histKomentarz = wybranyStatus switch
                    {
                        "Przyjeta" => "Reklamacja przyjeta do rozpatrzenia",
                        "Zaakceptowana" => $"[Przyczyna] {przyczyna}\n[Akcje] {akcje}",
                        "Odrzucona" => $"Powod odrzucenia: {powod}",
                        "Nowa" => $"Cofnieto ze statusu '{aktualnyStatus}' do ponownego rozpatrzenia",
                        _ => $"Zmiana statusu: {aktualnyStatus} -> {wybranyStatus}"
                    };

                    using (var cmd = new SqlCommand(@"
                        INSERT INTO [dbo].[ReklamacjeHistoria]
                            (IdReklamacji, UserID, PoprzedniStatus, StatusNowy, Komentarz, TypAkcji)
                        VALUES
                            (@IdReklamacji, @UserID, @PoprzedniStatus, @StatusNowy, @Komentarz, 'Rozpatrzenie')", conn))
                    {
                        cmd.Parameters.AddWithValue("@IdReklamacji", idReklamacji);
                        cmd.Parameters.AddWithValue("@UserID", userId);
                        cmd.Parameters.AddWithValue("@PoprzedniStatus", aktualnyStatus);
                        cmd.Parameters.AddWithValue("@StatusNowy", wybranyStatus);
                        cmd.Parameters.AddWithValue("@Komentarz", histKomentarz);
                        cmd.ExecuteNonQuery();
                    }
                }

                Zapisano = true;
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                PokazBlad($"Blad zapisu: {ex.Message}");
                btnZapisz.IsEnabled = true;
            }
        }

        private void PokazBlad(string msg)
        {
            txtError.Text = msg;
            txtError.Visibility = Visibility.Visible;
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        // ========================================
        // PIPELINE VISUAL
        // ========================================

        private void BudujPipeline()
        {
            pipelinePanel.Children.Clear();
            int currentIdx = Array.IndexOf(statusPipeline, aktualnyStatus);
            if (currentIdx < 0)
            {
                if (aktualnyStatus == "Zamknięta" || aktualnyStatus == "Zamknieta") currentIdx = 2;
                else if (aktualnyStatus == "W trakcie" || aktualnyStatus == "W analizie" ||
                         aktualnyStatus == "W trakcie realizacji" || aktualnyStatus == "Oczekuje na dostawce")
                    currentIdx = 1;
            }

            for (int i = 0; i < statusPipeline.Length; i++)
            {
                string s = statusPipeline[i];
                string hexColor = statusKolory.ContainsKey(s) ? statusKolory[s] : "#BDC3C7";
                var color = (Color)ColorConverter.ConvertFromString(hexColor);

                bool isCurrent = (i == currentIdx);
                bool isPast = (i < currentIdx);

                if (i >= 2 && currentIdx >= 2 && i != currentIdx)
                {
                    isCurrent = false;
                    isPast = false;
                }

                var border = new Border
                {
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(12, 6, 12, 6),
                    Margin = new Thickness(0, 0, 2, 0),
                    Background = isCurrent ? new SolidColorBrush(color) :
                                 isPast ? new SolidColorBrush(Color.FromArgb(40, color.R, color.G, color.B)) :
                                 new SolidColorBrush(Color.FromRgb(240, 240, 240)),
                    BorderThickness = isCurrent ? new Thickness(2) : new Thickness(1),
                    BorderBrush = isCurrent ? new SolidColorBrush(color) :
                                  isPast ? new SolidColorBrush(Color.FromArgb(80, color.R, color.G, color.B)) :
                                  new SolidColorBrush(Color.FromRgb(220, 220, 220))
                };

                border.Child = new TextBlock
                {
                    Text = s,
                    FontSize = isCurrent ? 11.5 : 10.5,
                    FontWeight = isCurrent ? FontWeights.Bold : FontWeights.Normal,
                    Foreground = isCurrent ? Brushes.White :
                                 isPast ? new SolidColorBrush(color) :
                                 new SolidColorBrush(Color.FromRgb(180, 180, 180))
                };

                pipelinePanel.Children.Add(border);

                if (i < statusPipeline.Length - 1)
                {
                    string separator = (i == 2) ? "/" : "\u25B8";
                    pipelinePanel.Children.Add(new TextBlock
                    {
                        Text = separator,
                        Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                        FontSize = 12,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(4, 0, 4, 0)
                    });
                }
            }
        }

        // ========================================
        // AVATAR HELPERS
        // ========================================

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        internal static ImageSource LoadWpfAvatar(string odbiorcaId, string name, int size)
        {
            if (string.IsNullOrEmpty(odbiorcaId)) return null;
            try
            {
                // Sprawdz czy avatar istnieje na udziale sieciowym
                System.Drawing.Image img = null;
                if (UserAvatarManager.HasAvatar(odbiorcaId))
                {
                    img = UserAvatarManager.GetAvatarRounded(odbiorcaId, size);
                }

                // Fallback: wygeneruj domyslny avatar z inicjalami
                if (img == null)
                {
                    img = UserAvatarManager.GenerateDefaultAvatar(name ?? odbiorcaId, odbiorcaId, size);
                }

                if (img == null) return null;

                using (img)
                using (var bmp = new System.Drawing.Bitmap(img))
                {
                    var hBitmap = bmp.GetHbitmap();
                    try
                    {
                        var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                            hBitmap, IntPtr.Zero, Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());
                        source.Freeze();
                        return source;
                    }
                    finally { DeleteObject(hBitmap); }
                }
            }
            catch { return null; }
        }

        // ========================================
        // HELPERS
        // ========================================

        private string PobierzNazweUzytkownika(string id)
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand("SELECT Name FROM operators WHERE ID = @id", conn))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        var result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                            return result.ToString();
                    }
                }
            }
            catch { }
            return id;
        }

        internal static string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                return $"{char.ToUpper(parts[0][0])}{char.ToUpper(parts[parts.Length - 1][0])}";
            return char.ToUpper(parts[0][0]).ToString();
        }

        internal static SolidColorBrush GetAvatarBrush(string name)
        {
            string[] palette = {
                "#1ABC9C", "#2ECC71", "#3498DB", "#9B59B6", "#E67E22",
                "#E74C3C", "#16A085", "#27AE60", "#2980B9", "#8E44AD"
            };
            int hash = 0;
            if (!string.IsNullOrEmpty(name))
                foreach (char c in name) hash = hash * 31 + c;
            int idx = Math.Abs(hash) % palette.Length;
            var color = (Color)ColorConverter.ConvertFromString(palette[idx]);
            return new SolidColorBrush(color);
        }

        internal static string GetStatusColor(string status)
        {
            return statusKolory.ContainsKey(status) ? statusKolory[status] : "#BDC3C7";
        }
    }

    public class HistoriaMiniItem
    {
        public string PoprzedniStatus { get; set; }
        public string StatusNowy { get; set; }
        public string Uzytkownik { get; set; }
        public string Data { get; set; }
        public SolidColorBrush Kolor { get; set; }
    }
}
