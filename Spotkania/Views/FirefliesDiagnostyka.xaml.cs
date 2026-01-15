using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Kalendarz1.Spotkania.Views
{
    public partial class FirefliesDiagnostyka : Window
    {
        private readonly string _apiKey;
        private readonly HttpClient _httpClient;
        private const string API_ENDPOINT = "https://api.fireflies.ai/graphql";

        public FirefliesDiagnostyka(string apiKey)
        {
            InitializeComponent();
            _apiKey = apiKey;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

            cmbQuery.SelectionChanged += CmbQuery_SelectionChanged;
        }

        private void CmbQuery_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool showTranscriptId = cmbQuery.SelectedIndex == 3;
            bool showCustomQuery = cmbQuery.SelectedIndex == 4;

            txtTranscriptId.Visibility = showTranscriptId ? Visibility.Visible : Visibility.Collapsed;
            txtCustomQuery.Visibility = showCustomQuery ? Visibility.Visible : Visibility.Collapsed;
        }

        private void TxtTranscriptId_GotFocus(object sender, RoutedEventArgs e)
        {
            if (txtTranscriptId.Text == "Podaj ID transkrypcji...")
            {
                txtTranscriptId.Text = "";
                txtTranscriptId.Foreground = System.Windows.Media.Brushes.White;
            }
        }

        private async void BtnExecute_Click(object sender, RoutedEventArgs e)
        {
            btnExecute.IsEnabled = false;
            txtStatus.Text = "Wykonywanie zapytania...";
            txtStatus.Foreground = System.Windows.Media.Brushes.Yellow;
            txtResponse.Text = "";

            try
            {
                string query = GetSelectedQuery();
                if (string.IsNullOrEmpty(query))
                {
                    txtResponse.Text = "Błąd: Nie wybrano zapytania";
                    return;
                }

                var stopwatch = Stopwatch.StartNew();
                var (success, response, error) = await ExecuteGraphQLQuery(query);
                stopwatch.Stop();

                txtResponseTime.Text = $"Czas: {stopwatch.ElapsedMilliseconds}ms";
                txtResponseSize.Text = $"Rozmiar: {response?.Length ?? 0} znaków";

                if (success)
                {
                    txtStatus.Text = "✅ Sukces";
                    txtStatus.Foreground = System.Windows.Media.Brushes.LightGreen;

                    // Format JSON for display
                    try
                    {
                        var jsonDoc = JsonDocument.Parse(response!);
                        txtResponse.Text = JsonSerializer.Serialize(jsonDoc, new JsonSerializerOptions
                        {
                            WriteIndented = true
                        });
                    }
                    catch
                    {
                        txtResponse.Text = response;
                    }
                }
                else
                {
                    txtStatus.Text = "❌ Błąd";
                    txtStatus.Foreground = System.Windows.Media.Brushes.Red;
                    txtResponse.Text = $"BŁĄD:\n{error}\n\n--- Surowa odpowiedź ---\n{response}";
                }
            }
            catch (Exception ex)
            {
                txtStatus.Text = "❌ Wyjątek";
                txtStatus.Foreground = System.Windows.Media.Brushes.Red;
                txtResponse.Text = $"WYJĄTEK:\n{ex.GetType().Name}: {ex.Message}\n\nStack trace:\n{ex.StackTrace}";
            }
            finally
            {
                btnExecute.IsEnabled = true;
            }
        }

        private string GetSelectedQuery()
        {
            return cmbQuery.SelectedIndex switch
            {
                0 => @"query { user { user_id email name minutes_consumed is_admin } }",

                1 => @"query { transcripts { id title date duration organizer_email participants } }",

                2 => @"query {
                    transcripts {
                        id
                        title
                        date
                        duration
                        organizer_email
                        host_email
                        participants
                        transcript_url
                        audio_url
                        video_url
                    }
                }",

                3 => $@"query {{
                    transcript(id: ""{txtTranscriptId.Text}"") {{
                        id
                        title
                        date
                        duration
                        organizer_email
                        host_email
                        participants
                        transcript_url
                        sentences {{
                            index
                            text
                            speaker_id
                            speaker_name
                            start_time
                            end_time
                        }}
                        summary {{
                            keywords
                            action_items
                            overview
                            shorthand_bullet
                            outline
                        }}
                    }}
                }}",

                4 => txtCustomQuery.Text,

                _ => ""
            };
        }

        private async Task<(bool success, string? response, string? error)> ExecuteGraphQLQuery(string query)
        {
            try
            {
                var requestBody = new { query };
                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(API_ENDPOINT, content);
                var responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return (false, responseString, $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");
                }

                // Check for GraphQL errors in response
                try
                {
                    var jsonDoc = JsonDocument.Parse(responseString);
                    if (jsonDoc.RootElement.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0)
                    {
                        var firstError = errors[0];
                        var errorMessage = firstError.GetProperty("message").GetString();
                        return (false, responseString, $"GraphQL Error: {errorMessage}");
                    }
                }
                catch { }

                return (true, responseString, null);
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        private void BtnCopyResponse_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(txtResponse.Text))
            {
                Clipboard.SetText(txtResponse.Text);
                MessageBox.Show("Skopiowano do schowka!", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
