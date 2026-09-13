namespace NowPlayingToast;

internal sealed class SettingsForm : Form
{
    private readonly AppSettings _settings;
    private readonly NumericUpDown _hold;
    private readonly ComboBox _corner;
    private readonly ComboBox _size;
    private readonly CheckBox _showOnActivate;
    private readonly CheckBox _playSound;
    private readonly ComboBox _theme;
    private readonly ComboBox _monitor;
    private readonly CheckBox _checkUpdates;

    public SettingsForm(AppSettings settings)
    {
        _settings = settings;
        Text = "Now Playing Toast - Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(420, 430);
        Font = new Font("Segoe UI", 9f);

        var y = 16;
        void AddLabel(string text)
        {
            Controls.Add(new Label { Text = text, Left = 16, Top = y, AutoSize = true });
            y += 22;
        }

        AddLabel("Hold duration (seconds)");
        _hold = new NumericUpDown
        {
            Left = 16, Top = y, Width = 120,
            DecimalPlaces = 1, Minimum = 1, Maximum = 30,
            Increment = 0.1M, Value = (decimal)Math.Clamp(settings.HoldDurationSeconds, 1, 30)
        };
        Controls.Add(_hold);
        y += 36;

        AddLabel("Corner");
        _corner = new ComboBox { Left = 16, Top = y, Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
        _corner.Items.AddRange(new object[] { "BottomRight", "BottomLeft", "TopRight", "TopLeft" });
        _corner.SelectedItem = settings.Corner.ToString();
        Controls.Add(_corner);
        y += 36;

        AddLabel("Size");
        _size = new ComboBox { Left = 16, Top = y, Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
        _size.Items.AddRange(new object[] { "Small", "Normal", "Large" });
        _size.SelectedItem = settings.Size.ToString();
        Controls.Add(_size);
        y += 36;

        AddLabel("Theme");
        _theme = new ComboBox { Left = 16, Top = y, Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
        _theme.Items.AddRange(new object[] { "Dark", "Light" });
        _theme.SelectedItem = settings.Theme.ToString();
        Controls.Add(_theme);
        y += 36;

        AddLabel("Monitor");
        _monitor = new ComboBox { Left = 16, Top = y, Width = 380, DropDownStyle = ComboBoxStyle.DropDownList };
        _monitor.Items.Add("Primary");
        for (int i = 0; i < Screen.AllScreens.Length; i++)
        {
            var s = Screen.AllScreens[i];
            var label = $"Screen {i}: {s.Bounds.Width}x{s.Bounds.Height}" + (s.Primary ? " (Primary)" : "");
            _monitor.Items.Add(label);
        }
        _monitor.SelectedIndex = settings.MonitorIndex < 0
            ? 0
            : Math.Min(settings.MonitorIndex + 1, _monitor.Items.Count - 1);
        Controls.Add(_monitor);
        y += 36;

        _showOnActivate = new CheckBox
        {
            Left = 16, Top = y, Width = 380, AutoSize = true,
            Text = "Show toast when Apple Music activates",
            Checked = settings.ShowOnAppleMusicActivate
        };
        Controls.Add(_showOnActivate);
        y += 28;

        _playSound = new CheckBox
        {
            Left = 16, Top = y, Width = 380, AutoSize = true,
            Text = "Play sound on toast",
            Checked = settings.PlaySoundOnToast
        };
        Controls.Add(_playSound);
        y += 28;

        _checkUpdates = new CheckBox
        {
            Left = 16, Top = y, Width = 380, AutoSize = true,
            Text = "Check for updates on startup",
            Checked = settings.CheckForUpdatesOnStartup
        };
        Controls.Add(_checkUpdates);
        y += 40;

        var save = new Button { Text = "Save", Left = 220, Top = y, Width = 90, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancel", Left = 320, Top = y, Width = 90, DialogResult = DialogResult.Cancel };
        save.Click += (_, _) => ApplyToSettings();
        Controls.Add(save);
        Controls.Add(cancel);
        AcceptButton = save;
        CancelButton = cancel;
    }

    private void ApplyToSettings()
    {
        _settings.HoldDurationSeconds = (double)_hold.Value;
        _settings.Corner = Enum.Parse<ToastCorner>(_corner.SelectedItem!.ToString()!);
        _settings.Size = Enum.Parse<ToastSize>(_size.SelectedItem!.ToString()!);
        _settings.Theme = Enum.Parse<ToastTheme>(_theme.SelectedItem!.ToString()!);
        _settings.MonitorIndex = _monitor.SelectedIndex <= 0 ? -1 : _monitor.SelectedIndex - 1;
        _settings.ShowOnAppleMusicActivate = _showOnActivate.Checked;
        _settings.PlaySoundOnToast = _playSound.Checked;
        _settings.CheckForUpdatesOnStartup = _checkUpdates.Checked;
        _settings.Save();
    }
}

