using System.Drawing;
using System.Windows.Forms;

namespace Kalendarz1
{
    public class InputBoxDialog : Form
    {
        public string Value { get; private set; } = "";
        private readonly TextBox _textBox;

        public InputBoxDialog(string title, string prompt, string defaultValue = "")
        {
            Text = title;
            Width = 440;
            Height = 180;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.FromArgb(30, 30, 46);
            ForeColor = Color.FromArgb(205, 214, 244);

            WindowIconHelper.SetIcon(this);

            var lbl = new Label
            {
                Text = prompt,
                Location = new Point(14, 14),
                Size = new Size(400, 22),
                ForeColor = Color.FromArgb(186, 194, 222)
            };

            _textBox = new TextBox
            {
                Text = defaultValue,
                Location = new Point(14, 40),
                Size = new Size(396, 26),
                BackColor = Color.FromArgb(49, 50, 68),
                ForeColor = Color.FromArgb(205, 214, 244),
                BorderStyle = BorderStyle.FixedSingle
            };

            var btnOk = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Location = new Point(246, 80),
                Size = new Size(80, 32),
                BackColor = Color.FromArgb(137, 180, 250),
                ForeColor = Color.FromArgb(30, 30, 46),
                FlatStyle = FlatStyle.Flat
            };
            btnOk.FlatAppearance.BorderSize = 0;

            var btnCancel = new Button
            {
                Text = "Anuluj",
                DialogResult = DialogResult.Cancel,
                Location = new Point(332, 80),
                Size = new Size(80, 32),
                BackColor = Color.FromArgb(69, 71, 90),
                ForeColor = Color.FromArgb(205, 214, 244),
                FlatStyle = FlatStyle.Flat
            };
            btnCancel.FlatAppearance.BorderSize = 0;

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            Controls.AddRange(new Control[] { lbl, _textBox, btnOk, btnCancel });

            FormClosing += (_, _) => Value = _textBox.Text?.Trim() ?? "";
        }
    }
}
