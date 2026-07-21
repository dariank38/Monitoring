using System.Drawing;
using System.Windows.Forms;

namespace Monitoring
{
    public class SettingsForm : Form
    {
        private readonly TextBox _urlBox;
        private readonly Button _saveBtn;
        private readonly Button _cancelBtn;
        private readonly Label _statusLabel;
        private readonly ServerClient _serverClient;

        public SettingsForm(ServerClient serverClient)
        {
            _serverClient = serverClient;

            Text = "Server Settings";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            Width = 420;
            Height = 200;
            BackColor = Color.White;

            var lblUrl = new Label
            {
                Text = "Server Address:",
                Location = new Point(20, 20),
                Width = 100,
                Font = new Font("Segoe UI", 9F)
            };
            Controls.Add(lblUrl);

            _urlBox = new TextBox
            {
                Location = new Point(20, 45),
                Width = 360,
                Font = new Font("Segoe UI", 10F),
                Text = serverClient.GetCurrentServerUrl()
            };
            Controls.Add(_urlBox);

            _statusLabel = new Label
            {
                Location = new Point(20, 75),
                Width = 360,
                Height = 20,
                ForeColor = Color.SlateGray,
                Font = new Font("Segoe UI", 8F),
                Text = "Enter the server URL (e.g. http://192.168.1.100:3000)"
            };
            Controls.Add(_statusLabel);

            _saveBtn = new Button
            {
                Text = "Save",
                Location = new Point(220, 115),
                Width = 75,
                Font = new Font("Segoe UI", 9F)
            };
            _saveBtn.Click += SaveBtn_Click;
            Controls.Add(_saveBtn);

            _cancelBtn = new Button
            {
                Text = "Cancel",
                Location = new Point(305, 115),
                Width = 75,
                Font = new Font("Segoe UI", 9F),
                DialogResult = DialogResult.Cancel
            };
            Controls.Add(_cancelBtn);

            AcceptButton = _saveBtn;
            CancelButton = _cancelBtn;
        }

        private void SaveBtn_Click(object? sender, EventArgs e)
        {
            var url = _urlBox.Text.Trim();
            if (string.IsNullOrEmpty(url))
            {
                _statusLabel.Text = "URL cannot be empty.";
                _statusLabel.ForeColor = Color.Red;
                return;
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "http" && uri.Scheme != "https"))
            {
                _statusLabel.Text = "Invalid URL. Must start with http:// or https://";
                _statusLabel.ForeColor = Color.Red;
                return;
            }

            _serverClient.UpdateServerUrl(url);
            _statusLabel.Text = "Saved! Reconnecting to server...";
            _statusLabel.ForeColor = Color.Green;

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
