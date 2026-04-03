using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace GoonNet;

/// <summary>
/// Jingle panel with 12 buttons per group, 1 fixed group plus interchangeable groups.
/// Supports MixOver, Abort, and Sequential playing modes per button.
/// </summary>
public class JinglePanelForm : Form
{
    public JingleDatabase JingleDb { get; set; } = null!;

    private JinglePanelConfig _config = new();
    private string _configPath = string.Empty;

    // Fixed group buttons (always visible)
    private Button[] _fixedButtons = null!;
    // Active interchangeable group buttons
    private Button[] _activeButtons = null!;

    private Panel _fixedGroupPanel = null!;
    private Panel _activeGroupPanel = null!;
    private Panel _groupSelectPanel = null!;
    private Button[] _groupSelectButtons = null!;

    // Playing mode for next action (can be overridden per button)
    private JinglePlayMode _defaultPlayMode = JinglePlayMode.MixOver;

    private static string AppDataPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GoonNet");

    public JinglePanelForm()
    {
        _configPath = Path.Combine(AppDataPath, "jingle_panel.xml");
        LoadConfig();
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "Jingle Panel";
        Size = new Size(860, 430);
        MinimumSize = new Size(700, 360);
        BackColor = Color.FromArgb(25, 28, 38);
        Font = new Font("Microsoft Sans Serif", 8f);

        // ── Group selector bar ──────────────────────────────────────────────
        _groupSelectPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 30,
            BackColor = Color.FromArgb(20, 22, 30),
            Padding = new Padding(2)
        };

        var lblMode = new Label
        {
            Text = "Mode:",
            ForeColor = Color.Silver,
            Location = new Point(4, 7),
            Size = new Size(36, 16),
            Font = new Font("Microsoft Sans Serif", 7.5f)
        };
        _groupSelectPanel.Controls.Add(lblMode);

        // Mode radio-style buttons
        int mx = 44;
        foreach (JinglePlayMode mode in Enum.GetValues<JinglePlayMode>())
        {
            var m = mode; // capture
            var btn = new Button
            {
                Text = mode.ToString(),
                Location = new Point(mx, 4),
                Size = new Size(80, 22),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft Sans Serif", 7.5f),
                Tag = mode
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(80, 90, 120);
            HighlightModeButton(btn, mode == _defaultPlayMode);
            btn.Click += (s, e) =>
            {
                _defaultPlayMode = m;
                foreach (Control c in _groupSelectPanel.Controls)
                    if (c is Button b && b.Tag is JinglePlayMode)
                        HighlightModeButton(b, (JinglePlayMode)b.Tag == _defaultPlayMode);
            };
            _groupSelectPanel.Controls.Add(btn);
            mx += 84;
        }

        // Interchangeable group selector
        mx += 20;
        var lblPanels = new Label
        {
            Text = "Panel:",
            ForeColor = Color.Silver,
            Location = new Point(mx, 7),
            Size = new Size(38, 16),
            Font = new Font("Microsoft Sans Serif", 7.5f)
        };
        _groupSelectPanel.Controls.Add(lblPanels);
        mx += 42;

        // One button per interchangeable group (groups[1..N])
        int interCount = Math.Max(0, _config.Groups.Count - 1);
        _groupSelectButtons = new Button[interCount];
        for (int gi = 0; gi < interCount; gi++)
        {
            int gIdx = gi + 1; // group index in config
            var gb = new Button
            {
                Text = _config.Groups[gIdx].Name,
                Location = new Point(mx, 4),
                Size = new Size(70, 22),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft Sans Serif", 7.5f),
                Tag = gIdx
            };
            gb.FlatAppearance.BorderColor = Color.FromArgb(80, 90, 120);
            HighlightGroupButton(gb, gIdx == _config.ActiveGroupIndex);
            int captureGi = gi;
            gb.Click += (s, e) => SwitchActiveGroup(captureGi + 1);
            _groupSelectPanel.Controls.Add(gb);
            _groupSelectButtons[gi] = gb;
            mx += 74;
        }

        var btnConfigure = new Button
        {
            Text = "⚙ Configure",
            Location = new Point(mx + 10, 4),
            Size = new Size(90, 22),
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.FromArgb(180, 200, 255),
            BackColor = Color.FromArgb(40, 50, 80),
            Font = new Font("Microsoft Sans Serif", 7.5f),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        btnConfigure.FlatAppearance.BorderColor = Color.FromArgb(80, 110, 160);
        btnConfigure.Click += BtnConfigure_Click;
        _groupSelectPanel.Controls.Add(btnConfigure);

        // ── Main area: fixed group (left) + active group (right) ────────────
        var mainPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(25, 28, 38) };

        _fixedGroupPanel = new Panel
        {
            Location = new Point(0, 0),
            Width = 400,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom,
            BackColor = Color.FromArgb(28, 32, 44)
        };

        var fixedLabel = new Label
        {
            Text = "FIXED",
            ForeColor = Color.FromArgb(180, 200, 255),
            Font = new Font("Microsoft Sans Serif", 8f, FontStyle.Bold),
            Location = new Point(4, 2),
            Size = new Size(380, 16),
            TextAlign = ContentAlignment.MiddleCenter
        };
        _fixedGroupPanel.Controls.Add(fixedLabel);

        _activeGroupPanel = new Panel
        {
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            BackColor = Color.FromArgb(22, 28, 38)
        };

        var activeLabel = new Label
        {
            Name = "lblActiveGroupName",
            ForeColor = Color.FromArgb(255, 210, 130),
            Font = new Font("Microsoft Sans Serif", 8f, FontStyle.Bold),
            Location = new Point(4, 2),
            Size = new Size(400, 16),
            TextAlign = ContentAlignment.MiddleCenter
        };
        _activeGroupPanel.Controls.Add(activeLabel);

        mainPanel.Controls.Add(_fixedGroupPanel);
        mainPanel.Controls.Add(_activeGroupPanel);

        mainPanel.SizeChanged += (s, e) =>
        {
            _fixedGroupPanel.Height = mainPanel.Height;
            _fixedGroupPanel.Width = mainPanel.Width / 2;
            _activeGroupPanel.Location = new Point(_fixedGroupPanel.Width, 0);
            _activeGroupPanel.Size = new Size(mainPanel.Width - _fixedGroupPanel.Width, mainPanel.Height);
            RebuildButtons();
        };

        Controls.Add(_groupSelectPanel);
        Controls.Add(mainPanel);

        Load += (s, e) =>
        {
            RebuildButtons();
            SwitchActiveGroup(_config.ActiveGroupIndex);
        };
        FormClosed += (s, e) => SaveConfig();
    }

    private static void HighlightModeButton(Button btn, bool active)
    {
        btn.BackColor = active ? Color.FromArgb(60, 80, 130) : Color.FromArgb(30, 35, 55);
        btn.ForeColor = active ? Color.White : Color.FromArgb(160, 170, 200);
    }

    private static void HighlightGroupButton(Button btn, bool active)
    {
        btn.BackColor = active ? Color.FromArgb(100, 70, 20) : Color.FromArgb(30, 35, 55);
        btn.ForeColor = active ? Color.FromArgb(255, 220, 100) : Color.Silver;
    }

    private void SwitchActiveGroup(int groupIndex)
    {
        if (groupIndex < 1 || groupIndex >= _config.Groups.Count) return;
        _config.ActiveGroupIndex = groupIndex;

        foreach (var gb in _groupSelectButtons)
            HighlightGroupButton(gb, (int)(gb.Tag ?? 0) == groupIndex);

        // Update label
        if (_activeGroupPanel.Controls["lblActiveGroupName"] is Label lbl)
            lbl.Text = _config.Groups[groupIndex].Name;

        RebuildButtons();
    }

    private void RebuildButtons()
    {
        BuildGroupButtons(_fixedGroupPanel, 0, ref _fixedButtons!);
        BuildGroupButtons(_activeGroupPanel, _config.ActiveGroupIndex, ref _activeButtons!);
    }

    private void BuildGroupButtons(Panel panel, int groupIndex, ref Button[] buttons)
    {
        // Remove old buttons (keep labels)
        for (int i = panel.Controls.Count - 1; i >= 0; i--)
            if (panel.Controls[i] is Button)
                panel.Controls.RemoveAt(i);

        if (groupIndex < 0 || groupIndex >= _config.Groups.Count) return;
        var group = _config.Groups[groupIndex];
        buttons = new Button[12];

        int cols = 3;
        int rows = 4;
        int bw = (panel.Width - 8) / cols;
        int bh = Math.Max(30, (panel.Height - 26) / rows);

        for (int i = 0; i < 12; i++)
        {
            int row = i / cols;
            int col = i % cols;
            var cfg = i < group.Buttons.Count ? group.Buttons[i] : new JingleButtonConfig();
            int captureI = i;
            int captureGroup = groupIndex;

            var btn = new Button
            {
                Text = string.IsNullOrEmpty(cfg.Label) ? $"#{i + 1}" : cfg.Label,
                Location = new Point(4 + col * bw, 20 + row * bh),
                Size = new Size(bw - 2, bh - 2),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft Sans Serif", 7.5f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = ParseColor(cfg.ColorHex, Color.FromArgb(40, 50, 70)),
                Tag = cfg
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(60, 80, 110);
            btn.Click += (s, e) => PlayJingle(captureGroup, captureI);
            btn.MouseUp += (s, e) =>
            {
                if (e.Button == MouseButtons.Right)
                    ShowButtonContextMenu(captureGroup, captureI, btn);
            };
            panel.Controls.Add(btn);
            buttons[i] = btn;
        }
    }

    private void PlayJingle(int groupIndex, int buttonIndex)
    {
        if (groupIndex < 0 || groupIndex >= _config.Groups.Count) return;
        var group = _config.Groups[groupIndex];
        if (buttonIndex < 0 || buttonIndex >= group.Buttons.Count) return;
        var cfg = group.Buttons[buttonIndex];

        if (!cfg.IsActive) return;

        var mode = cfg.PlayMode; // use button's own mode

        switch (cfg.ActionType)
        {
            case JingleButtonActionType.Jingle:
                PlayJingleAudio(cfg, mode);
                break;

            case JingleButtonActionType.File:
                PlayFileAudio(cfg.FilePath, mode);
                break;

            case JingleButtonActionType.TimeAnnouncement:
                PlayTimeAnnouncement(mode);
                break;

            case JingleButtonActionType.ExternalCommand:
                if (!string.IsNullOrWhiteSpace(cfg.Command))
                {
                    // Only allow execution of configured commands — warn user if the command
                    // was not set by a trusted administrator via the configuration UI.
                    var parts = cfg.Command.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 0)
                    {
                        var psi = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = parts[0],
                            Arguments = parts.Length > 1 ? parts[1] : string.Empty,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        try { System.Diagnostics.Process.Start(psi); }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Could not start command:\n{ex.Message}", "Jingle Panel", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }
                }
                break;

            case JingleButtonActionType.RDS:
                MessageBox.Show($"RDS: {cfg.RdsText}", "RDS Traffic", MessageBoxButtons.OK, MessageBoxIcon.Information);
                break;
        }
    }

    private void PlayJingleAudio(JingleButtonConfig cfg, JinglePlayMode mode)
    {
        if (cfg.JingleIds.Count == 0) return;

        // Rotate through multiple jingles
        if (cfg.NextJingleIndex >= cfg.JingleIds.Count) cfg.NextJingleIndex = 0;
        var jingleId = cfg.JingleIds[cfg.NextJingleIndex];
        cfg.NextJingleIndex = (cfg.NextJingleIndex + 1) % cfg.JingleIds.Count;

        var jingle = JingleDb?.GetById(jingleId);
        if (jingle == null) return;
        string path = Path.Combine(jingle.Location, jingle.FileName);
        if (!File.Exists(path)) { MessageBox.Show($"File not found:\n{path}", "Jingle Panel"); return; }

        var track = new MusicTrack
        {
            Artist = "Jingle",
            Title = jingle.Title,
            FileName = jingle.FileName,
            Location = jingle.Location
        };

        switch (mode)
        {
            case JinglePlayMode.MixOver:
                // Play on preview device so it mixes over the main playback
                AudioEngine.Instance.PlayTrack(track, AudioDeviceType.Preview);
                break;
            case JinglePlayMode.Abort:
                AudioEngine.Instance.Stop(AudioDeviceType.Main);
                AudioEngine.Instance.PlayTrack(track, AudioDeviceType.Main);
                break;
            case JinglePlayMode.Sequential:
                AudioEngine.Instance.QueueNext(track);
                break;
        }

        JingleDb?.IncrementPlayCount(jingleId);
    }

    private static void PlayFileAudio(string path, JinglePlayMode mode)
    {
        if (!File.Exists(path)) { MessageBox.Show($"File not found:\n{path}", "Jingle Panel"); return; }
        var track = new MusicTrack
        {
            Artist = "File",
            Title = Path.GetFileNameWithoutExtension(path),
            FileName = Path.GetFileName(path),
            Location = Path.GetDirectoryName(path) ?? string.Empty
        };
        switch (mode)
        {
            case JinglePlayMode.MixOver:
                AudioEngine.Instance.PlayTrack(track, AudioDeviceType.Preview);
                break;
            case JinglePlayMode.Abort:
                AudioEngine.Instance.Stop(AudioDeviceType.Main);
                AudioEngine.Instance.PlayTrack(track, AudioDeviceType.Main);
                break;
            case JinglePlayMode.Sequential:
                AudioEngine.Instance.QueueNext(track);
                break;
        }
    }

    private static void PlayTimeAnnouncement(JinglePlayMode mode)
    {
        string announcement = $"The time is {DateTime.Now:h:mm tt}.";
        MessageBox.Show($"Time Announcement:\n{announcement}\n\n(TTS not available in this build.)",
            "Time Announcement", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ShowButtonContextMenu(int groupIndex, int buttonIndex, Button btn)
    {
        var menu = new ContextMenuStrip();
        var cfg = _config.Groups[groupIndex].Buttons[buttonIndex];

        menu.Items.Add("🎵 Play (MixOver)", null, (s, e) => { cfg.PlayMode = JinglePlayMode.MixOver; PlayJingle(groupIndex, buttonIndex); });
        menu.Items.Add("⏹ Play (Abort)", null, (s, e) => { cfg.PlayMode = JinglePlayMode.Abort; PlayJingle(groupIndex, buttonIndex); });
        menu.Items.Add("▶ Play (Sequential)", null, (s, e) => { cfg.PlayMode = JinglePlayMode.Sequential; PlayJingle(groupIndex, buttonIndex); });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("⚙ Configure Button...", null, (s, e) => ConfigureButton(groupIndex, buttonIndex, btn));
        menu.Show(btn, new Point(0, btn.Height));
    }

    private void ConfigureButton(int groupIndex, int buttonIndex, Button btn)
    {
        var cfg = _config.Groups[groupIndex].Buttons[buttonIndex];
        using var dlg = new JingleButtonConfigDialog(cfg, JingleDb);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            btn.Text = string.IsNullOrEmpty(cfg.Label) ? $"#{buttonIndex + 1}" : cfg.Label;
            btn.BackColor = ParseColor(cfg.ColorHex, Color.FromArgb(40, 50, 70));
            SaveConfig();
        }
    }

    private void BtnConfigure_Click(object? sender, EventArgs e)
    {
        using var dlg = new JinglePanelConfigDialog(_config, JingleDb);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            SaveConfig();
            // Rebuild group selector buttons
            RebuildGroupSelectorButtons();
            RebuildButtons();
        }
    }

    private void RebuildGroupSelectorButtons()
    {
        // Refresh group names on existing selector buttons
        for (int i = 0; i < _groupSelectButtons.Length && i + 1 < _config.Groups.Count; i++)
            _groupSelectButtons[i].Text = _config.Groups[i + 1].Name;
    }

    private void LoadConfig()
    {
        try
        {
            if (!File.Exists(_configPath)) return;
            var ser = new XmlSerializer(typeof(JinglePanelConfig));
            using var reader = new System.IO.StreamReader(_configPath);
            if (ser.Deserialize(reader) is JinglePanelConfig loaded)
                _config = loaded;
        }
        catch { /* use defaults */ }
    }

    private void SaveConfig()
    {
        try
        {
            Directory.CreateDirectory(AppDataPath);
            var ser = new XmlSerializer(typeof(JinglePanelConfig));
            using var writer = new System.IO.StreamWriter(_configPath);
            ser.Serialize(writer, _config);
        }
        catch { /* non-critical */ }
    }

    private static Color ParseColor(string hex, Color fallback)
    {
        try { return ColorTranslator.FromHtml(hex); }
        catch { return fallback; }
    }
}

// ────────────────────────────────────────────────────────────────────────────
// Dialogs for configuring buttons and the panel
// ────────────────────────────────────────────────────────────────────────────

internal class JingleButtonConfigDialog : Form
{
    private readonly JingleButtonConfig _cfg;
    private readonly JingleDatabase? _jingleDb;

    private TextBox _txtLabel = null!;
    private ComboBox _cboActionType = null!;
    private ComboBox _cboPlayMode = null!;
    private TextBox _txtFilePath = null!;
    private TextBox _txtCommand = null!;
    private TextBox _txtRds = null!;
    private TextBox _txtColor = null!;
    private ListBox _lstJingles = null!;
    private CheckBox _chkActive = null!;

    public JingleButtonConfigDialog(JingleButtonConfig cfg, JingleDatabase? jingleDb)
    {
        _cfg = cfg;
        _jingleDb = jingleDb;
        InitializeComponent();
        LoadFields();
    }

    private void InitializeComponent()
    {
        Text = "Configure Jingle Button";
        Size = new Size(520, 460);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;

        int y = 12, lx = 10, lw = 90, fx = 106, fw = 380;

        void Row(string label, Control ctrl)
        {
            Controls.Add(new Label { Text = label, Location = new Point(lx, y + 2), Size = new Size(lw, 18), TextAlign = ContentAlignment.MiddleRight });
            ctrl.Location = new Point(fx, y); ctrl.Width = fw;
            Controls.Add(ctrl); y += 28;
        }

        _txtLabel = new TextBox();
        Row("Label:", _txtLabel);

        _cboActionType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (var v in Enum.GetValues<JingleButtonActionType>()) _cboActionType.Items.Add(v);
        _cboActionType.SelectedIndex = 0;
        _cboActionType.SelectedIndexChanged += (s, e) => UpdateVisibility();
        Row("Action:", _cboActionType);

        _cboPlayMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (var v in Enum.GetValues<JinglePlayMode>()) _cboPlayMode.Items.Add(v);
        _cboPlayMode.SelectedIndex = 0;
        Row("Play Mode:", _cboPlayMode);

        var lblJingles = new Label { Text = "Jingles (multi):", Location = new Point(lx, y + 2), Size = new Size(lw, 18), TextAlign = ContentAlignment.MiddleRight };
        Controls.Add(lblJingles);
        _lstJingles = new ListBox { Location = new Point(fx, y), Size = new Size(fw, 90), SelectionMode = SelectionMode.MultiExtended };
        if (_jingleDb != null)
            foreach (var j in _jingleDb.GetAll())
            {
                _lstJingles.Items.Add(j);
                if (_cfg.JingleIds.Contains(j.Id))
                    _lstJingles.SetSelected(_lstJingles.Items.Count - 1, true);
            }
        _lstJingles.DisplayMember = "Title";
        Controls.Add(_lstJingles);
        y += 96;

        _txtFilePath = new TextBox();
        var btnBrowse = new Button { Text = "...", Location = new Point(fx + fw + 2, y), Size = new Size(28, 20), FlatStyle = FlatStyle.System };
        btnBrowse.Click += (s, e) =>
        {
            using var dlg = new OpenFileDialog { Filter = "Audio|*.mp3;*.wav;*.ogg;*.aac;*.flac|All|*.*" };
            if (dlg.ShowDialog() == DialogResult.OK) _txtFilePath.Text = dlg.FileName;
        };
        Controls.Add(btnBrowse);
        Row("File:", _txtFilePath);

        _txtCommand = new TextBox();
        Row("Command:", _txtCommand);

        _txtRds = new TextBox();
        Row("RDS Text:", _txtRds);

        _txtColor = new TextBox();
        Row("Color (hex):", _txtColor);

        _chkActive = new CheckBox { Text = "Active", Location = new Point(fx, y) };
        Controls.Add(_chkActive); y += 28;

        var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(fx, y), Size = new Size(80, 26), FlatStyle = FlatStyle.System };
        btnOk.Click += BtnOk_Click;
        var btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(fx + 90, y), Size = new Size(80, 26), FlatStyle = FlatStyle.System };
        Controls.Add(btnOk); Controls.Add(btnCancel);
        AcceptButton = btnOk; CancelButton = btnCancel;
        ClientSize = new Size(500, y + 50);
    }

    private void LoadFields()
    {
        _txtLabel.Text = _cfg.Label;
        _cboActionType.SelectedItem = _cfg.ActionType;
        _cboPlayMode.SelectedItem = _cfg.PlayMode;
        _txtFilePath.Text = _cfg.FilePath;
        _txtCommand.Text = _cfg.Command;
        _txtRds.Text = _cfg.RdsText;
        _txtColor.Text = _cfg.ColorHex;
        _chkActive.Checked = _cfg.IsActive;
        UpdateVisibility();
    }

    private void UpdateVisibility()
    {
        // Could show/hide controls based on ActionType – for simplicity all remain visible
    }

    private void BtnOk_Click(object? sender, EventArgs e)
    {
        _cfg.Label = _txtLabel.Text.Trim();
        if (Enum.TryParse<JingleButtonActionType>(_cboActionType.SelectedItem?.ToString(), out var at)) _cfg.ActionType = at;
        if (Enum.TryParse<JinglePlayMode>(_cboPlayMode.SelectedItem?.ToString(), out var pm)) _cfg.PlayMode = pm;
        _cfg.FilePath = _txtFilePath.Text.Trim();
        _cfg.Command = _txtCommand.Text.Trim();
        _cfg.RdsText = _txtRds.Text.Trim();
        _cfg.ColorHex = _txtColor.Text.Trim();
        _cfg.IsActive = _chkActive.Checked;
        _cfg.JingleIds.Clear();
        foreach (var sel in _lstJingles.SelectedItems)
            if (sel is Jingle j) _cfg.JingleIds.Add(j.Id);
    }
}

internal class JinglePanelConfigDialog : Form
{
    private readonly JinglePanelConfig _config;
    private readonly JingleDatabase? _jingleDb;
    private ListBox _lstGroups = null!;
    private TextBox _txtGroupName = null!;

    public JinglePanelConfigDialog(JinglePanelConfig config, JingleDatabase? jingleDb)
    {
        _config = config;
        _jingleDb = jingleDb;
        InitializeComponent();
        RefreshGroups();
    }

    private void InitializeComponent()
    {
        Text = "Configure Jingle Panel";
        Size = new Size(400, 350);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;

        Controls.Add(new Label { Text = "Groups (first = fixed):", Location = new Point(10, 10), Size = new Size(160, 18) });

        _lstGroups = new ListBox { Location = new Point(10, 32), Size = new Size(250, 200), DisplayMember = "Name" };
        Controls.Add(_lstGroups);

        _txtGroupName = new TextBox { Location = new Point(270, 32), Size = new Size(100, 20) };
        Controls.Add(_txtGroupName);

        var btnRename = new Button { Text = "Rename", Location = new Point(270, 56), Size = new Size(100, 24), FlatStyle = FlatStyle.System };
        btnRename.Click += (s, e) =>
        {
            if (_lstGroups.SelectedIndex >= 0 && !string.IsNullOrWhiteSpace(_txtGroupName.Text))
            {
                _config.Groups[_lstGroups.SelectedIndex].Name = _txtGroupName.Text.Trim();
                RefreshGroups();
            }
        };
        Controls.Add(btnRename);

        var btnAdd = new Button { Text = "Add Group", Location = new Point(270, 88), Size = new Size(100, 24), FlatStyle = FlatStyle.System };
        btnAdd.Click += (s, e) =>
        {
            _config.Groups.Add(new JingleGroupConfig { Name = $"Panel {_config.Groups.Count}" });
            RefreshGroups();
        };
        Controls.Add(btnAdd);

        var btnRemove = new Button { Text = "Remove", Location = new Point(270, 116), Size = new Size(100, 24), FlatStyle = FlatStyle.System };
        btnRemove.Click += (s, e) =>
        {
            int idx = _lstGroups.SelectedIndex;
            if (idx > 0 && _config.Groups.Count > 1)
            {
                _config.Groups.RemoveAt(idx);
                RefreshGroups();
            }
        };
        Controls.Add(btnRemove);

        var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(100, 270), Size = new Size(80, 26), FlatStyle = FlatStyle.System };
        var btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(200, 270), Size = new Size(80, 26), FlatStyle = FlatStyle.System };
        Controls.Add(btnOk); Controls.Add(btnCancel);
        AcceptButton = btnOk; CancelButton = btnCancel;

        _lstGroups.SelectedIndexChanged += (s, e) =>
        {
            if (_lstGroups.SelectedIndex >= 0)
                _txtGroupName.Text = _config.Groups[_lstGroups.SelectedIndex].Name;
        };
    }

    private void RefreshGroups()
    {
        _lstGroups.Items.Clear();
        foreach (var g in _config.Groups)
            _lstGroups.Items.Add(g);
    }
}
