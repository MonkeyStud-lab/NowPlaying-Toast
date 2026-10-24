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
    private readonly Label _saveError;

    public SettingsForm(AppSettings settings)
    {
        // Edits stay local until the complete settings file has been saved successfully.
        _settings = settings.Clone();
        _settings.Clamp();
        Text = "Now Playing Toast - Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(16);
        Font = new Font("Segoe UI", 9f);

        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Margin = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        Controls.Add(layout);

        void AddField(string label, Control editor)
        {
            var row = layout.RowCount++;
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(new Label
            {
                Text = label,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 0, 16, 10)
            }, 0, row);
            editor.Margin = new Padding(0, 0, 0, 10);
            editor.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            editor.AccessibleName = label;
            layout.Controls.Add(editor, 1, row);
        }

        void AddWideControl(Control control)
        {
            var row = layout.RowCount++;
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(control, 0, row);
            layout.SetColumnSpan(control, 2);
        }

        ComboBox CreateChoice(string[] choices, string selection)
        {
            var combo = new ComboBox { Width = 240, DropDownStyle = ComboBoxStyle.DropDownList };
            combo.Items.AddRange(choices);
            combo.SelectedItem = selection;
            return combo;
        }

        _hold = new NumericUpDown
        {
            Width = 240,
            DecimalPlaces = 1,
            Minimum = 1,
            Maximum = 30,
            Increment = 0.1M,
            Value = (decimal)_settings.HoldDurationSeconds
        };
        AddField("Hold duration (seconds)", _hold);

        _corner = CreateChoice(Enum.GetNames<ToastCorner>(), _settings.Corner.ToString());
        AddField("Corner", _corner);
        _size = CreateChoice(Enum.GetNames<ToastSize>(), _settings.Size.ToString());
        AddField("Size", _size);
        _theme = CreateChoice(Enum.GetNames<ToastTheme>(), _settings.Theme.ToString());
        AddField("Theme", _theme);

        _monitor = new ComboBox { Width = 240, DropDownStyle = ComboBoxStyle.DropDownList };
        _monitor.Items.Add("Primary");
        var screens = Screen.AllScreens;
        for (int i = 0; i < screens.Length; i++)
        {
            var screen = screens[i];
            _monitor.Items.Add($"Screen {i}: {screen.Bounds.Width}x{screen.Bounds.Height}" +
                (screen.Primary ? " (Primary)" : ""));
        }
        _monitor.SelectedIndex = _settings.MonitorIndex >= 0 && _settings.MonitorIndex < screens.Length
            ? _settings.MonitorIndex + 1
            : 0;
        AddField("Monitor", _monitor);

        _showOnActivate = new CheckBox
        {
            AutoSize = true,
            Text = "Show toast when Apple Music activates",
            Checked = _settings.ShowOnAppleMusicActivate,
            Margin = new Padding(0, 4, 0, 8)
        };
        AddWideControl(_showOnActivate);
        _playSound = new CheckBox
        {
            AutoSize = true,
            Text = "Play sound on toast",
            Checked = _settings.PlaySoundOnToast,
            Margin = new Padding(0, 0, 0, 8)
        };
        AddWideControl(_playSound);
        _checkUpdates = new CheckBox
        {
            AutoSize = true,
            Text = "Check for updates on startup",
            Checked = _settings.CheckForUpdatesOnStartup,
            Margin = new Padding(0, 0, 0, 12)
        };
        AddWideControl(_checkUpdates);

        _saveError = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(410, 0),
            ForeColor = Color.Firebrick,
            Margin = new Padding(0, 0, 0, 12),
            Visible = false
        };
        AddWideControl(_saveError);

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Margin = Padding.Empty
        };
        var cancel = new Button { Text = "Cancel", AutoSize = true, MinimumSize = new Size(90, 0), DialogResult = DialogResult.Cancel };
        var save = new Button { Text = "Save", AutoSize = true, MinimumSize = new Size(90, 0) };
        save.Click += (_, _) => SaveSettings();
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(save);
        AddWideControl(buttons);
        AcceptButton = save;
        CancelButton = cancel;
    }

    private void SaveSettings()
    {
        _settings.HoldDurationSeconds = (double)_hold.Value;
        _settings.Corner = Enum.Parse<ToastCorner>(_corner.SelectedItem!.ToString()!);
        _settings.Size = Enum.Parse<ToastSize>(_size.SelectedItem!.ToString()!);
        _settings.Theme = Enum.Parse<ToastTheme>(_theme.SelectedItem!.ToString()!);
        _settings.MonitorIndex = _monitor.SelectedIndex <= 0 ? -1 : _monitor.SelectedIndex - 1;
        _settings.ShowOnAppleMusicActivate = _showOnActivate.Checked;
        _settings.PlaySoundOnToast = _playSound.Checked;
        _settings.CheckForUpdatesOnStartup = _checkUpdates.Checked;

        try
        {
            _settings.Save();
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            _saveError.Text = "Settings could not be saved. Your previous settings are unchanged.\n" + ex.Message;
            _saveError.Visible = true;
        }
    }
}
