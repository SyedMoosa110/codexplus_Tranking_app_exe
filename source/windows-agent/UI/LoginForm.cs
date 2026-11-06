using System;
using System.Drawing;
using System.Windows.Forms;
using WorkAgent.Config;
using WorkAgent.Core;
using WorkAgent.Storage;

namespace WorkAgent.UI;

public sealed class LoginForm : Form
{
    private readonly ApiClient _api;
    private readonly TextBox _emailBox;
    private readonly TextBox _passwordBox;
    private readonly TextBox _serverUrlBox;
    private readonly CheckBox _consentCheckbox;
    private readonly Button _loginButton;
    private readonly Label _errorLabel;

    public bool IsAuthenticated { get; private set; } = false;

    public LoginForm(ApiClient api)
    {
        _api = api;

        Text = "Enterprise Work Agent - Employee Sign In";
        Width = 460;
        Height = 490;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        TopMost = true;
        ShowInTaskbar = true;
        Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        BackColor = Color.FromArgb(248, 250, 252);

        var titleLabel = new Label
        {
            Text = "Enterprise Time Tracking",
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Top = 15,
            Left = 30,
            Width = 380,
            Height = 30
        };

        var subtitleLabel = new Label
        {
            Text = "Please sign in with your corporate employee credentials.",
            ForeColor = Color.FromArgb(100, 116, 139),
            Top = 48,
            Left = 30,
            Width = 380,
            Height = 22
        };

        var emailLabel = new Label { Text = "Corporate Email", Top = 80, Left = 30, Width = 380, Height = 18 };
        _emailBox = new TextBox { Top = 100, Left = 30, Width = 380, Height = 28 };

        var passLabel = new Label { Text = "Password", Top = 135, Left = 30, Width = 380, Height = 18 };
        _passwordBox = new TextBox { Top = 155, Left = 30, Width = 380, Height = 28, UseSystemPasswordChar = true };

        var serverLabel = new Label { Text = "Server Address (Cloud or LAN IP)", Top = 190, Left = 30, Width = 380, Height = 18 };
        _serverUrlBox = new TextBox
        {
            Top = 210,
            Left = 30,
            Width = 380,
            Height = 28,
            Text = AgentConfig.GetServerUrl()
        };

        // Privacy Policy & Consent Notice Checkbox
        _consentCheckbox = new CheckBox
        {
            Top = 250,
            Left = 30,
            Width = 380,
            Height = 55,
            Text = "I acknowledge that this work device tracks active/idle work hours and application categories during shift time for attendance and payroll purposes.",
            ForeColor = Color.FromArgb(71, 85, 105),
            Checked = true
        };

        _errorLabel = new Label
        {
            Text = "",
            ForeColor = Color.FromArgb(220, 38, 38),
            Top = 315,
            Left = 30,
            Width = 380,
            Height = 22
        };

        _loginButton = new Button
        {
            Text = "Sign In & Start Tracking",
            Top = 350,
            Left = 30,
            Width = 380,
            Height = 42,
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _loginButton.FlatAppearance.BorderSize = 0;
        _loginButton.Click += async (s, e) => await OnLoginClicked();

        Controls.AddRange(new Control[]
        {
            titleLabel, subtitleLabel,
            emailLabel, _emailBox,
            passLabel, _passwordBox,
            serverLabel, _serverUrlBox,
            _consentCheckbox,
            _errorLabel,
            _loginButton
        });
    }

    private async Task OnLoginClicked()
    {
        _errorLabel.Text = "";

        if (string.IsNullOrWhiteSpace(_emailBox.Text) || string.IsNullOrWhiteSpace(_passwordBox.Text))
        {
            _errorLabel.Text = "Please enter both email and password.";
            return;
        }

        if (!_consentCheckbox.Checked)
        {
            _errorLabel.Text = "You must acknowledge the work tracking policy to proceed.";
            return;
        }

        var serverUrl = _serverUrlBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(serverUrl))
        {
            if (!serverUrl.EndsWith("/")) serverUrl += "/";
            AgentConfig.SaveServerUrl(serverUrl);
            try { _api.UpdateBaseUrl(serverUrl); } catch { }
        }

        _loginButton.Enabled = false;
        _loginButton.Text = "Signing in...";

        try
        {
            var deviceUuid = CredentialStore.GetOrCreateDeviceUuid();
            var result = await _api.LoginAsync(_emailBox.Text.Trim(), _passwordBox.Text, deviceUuid);

            if (result != null)
            {
                CredentialStore.SaveTokens(result.refresh);
                IsAuthenticated = true;
                DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                _errorLabel.Text = "Invalid email, password, or server unreachable.";
            }
        }
        catch (Exception ex)
        {
            _errorLabel.Text = $"Connection error: {ex.Message}";
        }
        finally
        {
            _loginButton.Enabled = true;
            _loginButton.Text = "Sign In & Start Tracking";
        }
    }
}
