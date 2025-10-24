using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;

namespace Kalendarz1
{
    public partial class UserHandlowcyDialog : Form
    {
        private string libraConnectionString;
        private string handelConnectionString;
        private string userId;
        private string userName;

        private ListBox availableListBox;
        private ListBox assignedListBox;
        private Button addButton;
        private Button removeButton;
        private Button addAllButton;
        private Button removeAllButton;
        private Button saveButton;
        private Button cancelButton;
        private TextBox searchTextBox;
        private Label availableCountLabel;
        private Label assignedCountLabel;

        private List<string> allHandlowcy = new List<string>();
        private List<string> assignedHandlowcy = new List<string>();

        public event EventHandler HandlowcyZapisani;

        public UserHandlowcyDialog(string libraConnString, string handelConnString, string uid, string uname)
        {
            libraConnectionString = libraConnString;
            handelConnectionString = handelConnString;
            userId = uid;
            userName = uname;

            InitializeComponents();
            LoadData();
        }

        private void InitializeComponents()
        {
            // Form settings
            this.Text = $"Zarządzanie handlowcami - {userName}";
            this.Size = new Size(900, 650);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimizeBox = true;
            this.BackColor = ColorTranslator.FromHtml("#F5F7FA");

            // ========== HEADER ==========
            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = ColorTranslator.FromHtml("#9B59B6")
            };

            var titleLabel = new Label
            {
                Text = "👔 PRZYPISYWANIE HANDLOWCÓW",
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(25, 15)
            };
            headerPanel.Controls.Add(titleLabel);

            var subtitleLabel = new Label
            {
                Text = $"Użytkownik: {userName} (ID: {userId})",
                Font = new Font("Segoe UI", 10),
                ForeColor = ColorTranslator.FromHtml("#E8DAEF"),
                AutoSize = true,
                Location = new Point(28, 48)
            };
            headerPanel.Controls.Add(subtitleLabel);

            this.Controls.Add(headerPanel);

            // ========== MAIN CONTENT ==========
            var contentPanel = new Panel
            {
                Location = new Point(20, 100),
                Size = new Size(840, 450),
                BackColor = Color.White
            };

            // Left panel - Available handlowcy
            var leftLabel = new Label
            {
                Text = "📋 DOSTĘPNI HANDLOWCY",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#2C3E50"),
                Location = new Point(20, 15),
                AutoSize = true
            };
            contentPanel.Controls.Add(leftLabel);

            availableCountLabel = new Label
            {
                Text = "0 handlowców",
                Font = new Font("Segoe UI", 9),
                ForeColor = ColorTranslator.FromHtml("#7F8C8D"),
                Location = new Point(23, 40),
                AutoSize = true
            };
            contentPanel.Controls.Add(availableCountLabel);

            searchTextBox = new TextBox
            {
                Location = new Point(20, 65),
                Size = new Size(330, 30),
                Font = new Font("Segoe UI", 10),
                PlaceholderText = "🔍 Szukaj handlowca..."
            };
            searchTextBox.TextChanged += SearchTextBox_TextChanged;
            contentPanel.Controls.Add(searchTextBox);

            availableListBox = new ListBox
            {
                Location = new Point(20, 105),
                Size = new Size(330, 310),
                Font = new Font("Segoe UI", 10),
                SelectionMode = SelectionMode.MultiExtended,
                BorderStyle = BorderStyle.FixedSingle
            };
            availableListBox.DoubleClick += (s, e) => AddSelectedHandlowcy();
            contentPanel.Controls.Add(availableListBox);

            // Middle panel - Buttons
            var buttonX = 370;
            var buttonStartY = 180;

            addButton = CreateStyledButton("➤ Dodaj", new Point(buttonX, buttonStartY), new Size(100, 40));
            addButton.Click += (s, e) => AddSelectedHandlowcy();
            contentPanel.Controls.Add(addButton);

            addAllButton = CreateStyledButton("⏩ Dodaj wszystkich", new Point(buttonX, buttonStartY + 50), new Size(100, 40));
            addAllButton.Click += (s, e) => AddAllHandlowcy();
            contentPanel.Controls.Add(addAllButton);

            removeButton = CreateStyledButton("◄ Usuń", new Point(buttonX, buttonStartY + 110), new Size(100, 40));
            removeButton.Click += (s, e) => RemoveSelectedHandlowcy();
            contentPanel.Controls.Add(removeButton);

            removeAllButton = CreateStyledButton("⏪ Usuń wszystkich", new Point(buttonX, buttonStartY + 160), new Size(100, 40));
            removeAllButton.Click += (s, e) => RemoveAllHandlowcy();
            contentPanel.Controls.Add(removeAllButton);

            // Right panel - Assigned handlowcy
            var rightLabel = new Label
            {
                Text = "✓ PRZYPISANI HANDLOWCY",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = ColorTranslator.FromHtml("#27AE60"),
                Location = new Point(490, 15),
                AutoSize = true
            };
            contentPanel.Controls.Add(rightLabel);

            assignedCountLabel = new Label
            {
                Text = "0 przypisanych",
                Font = new Font("Segoe UI", 9),
                ForeColor = ColorTranslator.FromHtml("#7F8C8D"),
                Location = new Point(493, 40),
                AutoSize = true
            };
            contentPanel.Controls.Add(assignedCountLabel);

            assignedListBox = new ListBox
            {
                Location = new Point(490, 65),
                Size = new Size(330, 350),
                Font = new Font("Segoe UI", 10),
                SelectionMode = SelectionMode.MultiExtended,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = ColorTranslator.FromHtml("#E8F8F5")
            };
            assignedListBox.DoubleClick += (s, e) => RemoveSelectedHandlowcy();
            contentPanel.Controls.Add(assignedListBox);

            this.Controls.Add(contentPanel);

            // ========== BOTTOM BUTTONS ==========
            var bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 70,
                BackColor = ColorTranslator.FromHtml("#ECF0F1")
            };

            saveButton = new Button
            {
                Text = "💾 Zapisz zmiany",
                Location = new Point(540, 15),
                Size = new Size(150, 45),
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                BackColor = ColorTranslator.FromHtml("#27AE60"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            saveButton.FlatAppearance.BorderSize = 0;
            saveButton.Click += SaveButton_Click;
            bottomPanel.Controls.Add(saveButton);

            cancelButton = new Button
            {
                Text = "✕ Anuluj",
                Location = new Point(700, 15),
                Size = new Size(130, 45),
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                BackColor = ColorTranslator.FromHtml("#95A5A6"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.Cancel
            };
            cancelButton.FlatAppearance.BorderSize = 0;
            bottomPanel.Controls.Add(cancelButton);

            var infoLabel = new Label
            {
                Text = "💡 Podwójne kliknięcie dodaje/usuwa handlowca",
                Font = new Font("Segoe UI", 9, FontStyle.Italic),
                ForeColor = ColorTranslator.FromHtml("#7F8C8D"),
                Location = new Point(30, 25),
                AutoSize = true
            };
            bottomPanel.Controls.Add(infoLabel);

            this.Controls.Add(bottomPanel);
        }

        private Button CreateStyledButton(string text, Point location, Size size)
        {
            var button = new Button
            {
                Text = text,
                Location = location,
                Size = size,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                BackColor = ColorTranslator.FromHtml("#3498DB"),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderSize = 0;
            return button;
        }

        private void LoadData()
        {
            try
            {
                // Pobierz wszystkich dostępnych handlowców
                allHandlowcy = UserHandlowcyManager.GetAvailableHandlowcy();

                // Pobierz już przypisanych handlowców
                assignedHandlowcy = UserHandlowcyManager.GetUserHandlowcy(userId);

                RefreshLists();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania danych:\n{ex.Message}",
                    "Błąd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshLists()
        {
            // Odśwież listę dostępnych (bez przypisanych)
            availableListBox.Items.Clear();
            var available = allHandlowcy.Where(h => !assignedHandlowcy.Contains(h)).ToList();

            // Filtruj według wyszukiwania
            string searchText = searchTextBox.Text.Trim().ToLower();
            if (!string.IsNullOrEmpty(searchText))
            {
                available = available.Where(h => h.ToLower().Contains(searchText)).ToList();
            }

            foreach (var h in available.OrderBy(x => x))
            {
                availableListBox.Items.Add(h);
            }
            availableCountLabel.Text = $"{available.Count} handlowców dostępnych";

            // Odśwież listę przypisanych
            assignedListBox.Items.Clear();
            foreach (var h in assignedHandlowcy.OrderBy(x => x))
            {
                assignedListBox.Items.Add(h);
            }
            assignedCountLabel.Text = $"{assignedHandlowcy.Count} przypisanych";
            assignedCountLabel.ForeColor = assignedHandlowcy.Count > 0
                ? ColorTranslator.FromHtml("#27AE60")
                : ColorTranslator.FromHtml("#E74C3C");
        }

        private void SearchTextBox_TextChanged(object sender, EventArgs e)
        {
            RefreshLists();
        }

        private void AddSelectedHandlowcy()
        {
            if (availableListBox.SelectedItems.Count == 0)
            {
                MessageBox.Show("Wybierz handlowców do dodania.", "Informacja",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var selected = availableListBox.SelectedItems.Cast<string>().ToList();
            foreach (var handlowiec in selected)
            {
                if (!assignedHandlowcy.Contains(handlowiec))
                {
                    assignedHandlowcy.Add(handlowiec);
                }
            }

            RefreshLists();
        }

        private void AddAllHandlowcy()
        {
            var result = MessageBox.Show(
                $"Czy na pewno chcesz przypisać wszystkich {allHandlowcy.Count} handlowców do użytkownika {userName}?",
                "Potwierdzenie",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                assignedHandlowcy = new List<string>(allHandlowcy);
                RefreshLists();
            }
        }

        private void RemoveSelectedHandlowcy()
        {
            if (assignedListBox.SelectedItems.Count == 0)
            {
                MessageBox.Show("Wybierz handlowców do usunięcia.", "Informacja",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var selected = assignedListBox.SelectedItems.Cast<string>().ToList();
            foreach (var handlowiec in selected)
            {
                assignedHandlowcy.Remove(handlowiec);
            }

            RefreshLists();
        }

        private void RemoveAllHandlowcy()
        {
            if (assignedHandlowcy.Count == 0)
            {
                MessageBox.Show("Brak przypisanych handlowców do usunięcia.", "Informacja",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Czy na pewno chcesz usunąć wszystkich {assignedHandlowcy.Count} przypisanych handlowców?",
                "Potwierdzenie",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                assignedHandlowcy.Clear();
                RefreshLists();
            }
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            try
            {
                // Usuń wszystkie obecne przypisania
                UserHandlowcyManager.RemoveAllHandlowcyFromUser(userId);

                // Dodaj nowe przypisania
                int added = 0;
                foreach (var handlowiec in assignedHandlowcy)
                {
                    if (UserHandlowcyManager.AddHandlowiecToUser(userId, handlowiec, Environment.UserName))
                    {
                        added++;
                    }
                }

                MessageBox.Show(
                    $"✓ Zapisano pomyślnie!\n\n" +
                    $"Przypisano {added} handlowców do użytkownika {userName}.",
                    "Sukces",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                HandlowcyZapisani?.Invoke(this, EventArgs.Empty);
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Błąd podczas zapisywania:\n{ex.Message}",
                    "Błąd",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}