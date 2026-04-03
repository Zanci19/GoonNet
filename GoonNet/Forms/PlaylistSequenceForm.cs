using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace GoonNet;

/// <summary>
/// Table-driven editor for managing daily/weekly playlist sequences.
/// Replaces the "coming soon" placeholder.
/// </summary>
public class PlaylistSequenceForm : Form
{
    public PlaylistDatabase PlaylistDb { get; set; } = null!;

    // We persist sequences in memory using a simple list;
    // real persistence would use a PlaylistSequenceDatabase.
    private System.Collections.Generic.List<PlaylistSequence> _sequences = new();

    private ListBox _lstSequences = null!;
    private ListView _lvItems = null!;
    private Button _btnNewSeq = null!;
    private Button _btnDeleteSeq = null!;
    private Button _btnRenameSeq = null!;
    private Button _btnAddItem = null!;
    private Button _btnRemoveItem = null!;
    private Button _btnMoveUp = null!;
    private Button _btnMoveDown = null!;
    private Button _btnSave = null!;
    private ComboBox _cboRepeatMode = null!;
    private CheckBox _chkActive = null!;
    private Label _lblSequenceName = null!;

    private PlaylistSequence? _current;
    private string _savePath = string.Empty;

    private static string AppDataPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GoonNet");

    public PlaylistSequenceForm()
    {
        _savePath = System.IO.Path.Combine(AppDataPath, "sequences.xml");
        InitializeComponent();
        Load += (s, e) => { LoadSequences(); RefreshSequenceList(); };
    }

    private void InitializeComponent()
    {
        Text = "Playlist Sequences";
        Size = new Size(900, 560);
        MinimumSize = new Size(700, 420);
        BackColor = SystemColors.Control;
        Font = new Font("Microsoft Sans Serif", 8f);

        // ── Left: sequence list ─────────────────────────────────────────────
        var leftPanel = new Panel { Dock = DockStyle.Left, Width = 220, BackColor = Color.FromArgb(245, 245, 252) };

        var lblSeq = new Label { Text = "Sequences:", Location = new Point(4, 4), Size = new Size(120, 16), Font = new Font("Microsoft Sans Serif", 8f, FontStyle.Bold) };
        _lstSequences = new ListBox { Location = new Point(4, 24), Size = new Size(210, 350), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom };
        _lstSequences.SelectedIndexChanged += LstSequences_SelectedIndexChanged;

        _btnNewSeq = new Button { Text = "New", Location = new Point(4, 382), Size = new Size(64, 24), FlatStyle = FlatStyle.System };
        _btnNewSeq.Click += BtnNewSeq_Click;

        _btnDeleteSeq = new Button { Text = "Delete", Location = new Point(72, 382), Size = new Size(64, 24), FlatStyle = FlatStyle.System };
        _btnDeleteSeq.Click += BtnDeleteSeq_Click;

        _btnRenameSeq = new Button { Text = "Rename", Location = new Point(140, 382), Size = new Size(70, 24), FlatStyle = FlatStyle.System };
        _btnRenameSeq.Click += BtnRenameSeq_Click;

        leftPanel.Controls.AddRange(new Control[] { lblSeq, _lstSequences, _btnNewSeq, _btnDeleteSeq, _btnRenameSeq });
        leftPanel.SizeChanged += (s, e) => _lstSequences.Height = leftPanel.Height - 60;

        // ── Right: sequence detail ──────────────────────────────────────────
        var rightPanel = new Panel { Dock = DockStyle.Fill };

        var propBar = new Panel { Dock = DockStyle.Top, Height = 34 };
        _lblSequenceName = new Label { Text = "(none selected)", Location = new Point(4, 8), Size = new Size(300, 20), Font = new Font("Microsoft Sans Serif", 9f, FontStyle.Bold) };

        var lblRepeat = new Label { Text = "Repeat:", Location = new Point(310, 8), Size = new Size(50, 18) };
        _cboRepeatMode = new ComboBox { Location = new Point(362, 5), Size = new Size(120, 22), DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.System };
        foreach (var v in Enum.GetValues<RepeatMode>()) _cboRepeatMode.Items.Add(v);
        _cboRepeatMode.SelectedIndex = 0;
        _cboRepeatMode.SelectedIndexChanged += (s, e) => { if (_current != null && _cboRepeatMode.SelectedItem is RepeatMode rm) _current.RepeatMode = rm; };

        _chkActive = new CheckBox { Text = "Active", Location = new Point(492, 8), Checked = true };
        _chkActive.CheckedChanged += (s, e) => { if (_current != null) _current.IsActive = _chkActive.Checked; };

        _btnSave = new Button { Text = "💾 Save", Location = new Point(600, 4), Size = new Size(80, 26), FlatStyle = FlatStyle.System };
        _btnSave.Click += (s, e) => { SaveSequences(); MessageBox.Show("Sequences saved.", "GoonNet"); };

        propBar.Controls.AddRange(new Control[] { _lblSequenceName, lblRepeat, _cboRepeatMode, _chkActive, _btnSave });

        // Playlist items list
        _lvItems = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            BorderStyle = BorderStyle.Fixed3D,
            Font = new Font("Microsoft Sans Serif", 8f)
        };
        _lvItems.Columns.Add("#", 32);
        _lvItems.Columns.Add("Playlist", 250);
        _lvItems.Columns.Add("Scheduled Date", 130);
        _lvItems.Columns.Add("Scheduled Time", 110);

        // Buttons
        var btnBar = new Panel { Dock = DockStyle.Bottom, Height = 34 };
        _btnAddItem = new Button { Text = "+ Add Playlist", Location = new Point(4, 5), Size = new Size(100, 24), FlatStyle = FlatStyle.System };
        _btnAddItem.Click += BtnAddItem_Click;

        _btnRemoveItem = new Button { Text = "Remove", Location = new Point(110, 5), Size = new Size(70, 24), FlatStyle = FlatStyle.System };
        _btnRemoveItem.Click += BtnRemoveItem_Click;

        _btnMoveUp = new Button { Text = "▲", Location = new Point(188, 5), Size = new Size(36, 24), FlatStyle = FlatStyle.System };
        _btnMoveUp.Click += BtnMoveUp_Click;

        _btnMoveDown = new Button { Text = "▼", Location = new Point(228, 5), Size = new Size(36, 24), FlatStyle = FlatStyle.System };
        _btnMoveDown.Click += BtnMoveDown_Click;

        btnBar.Controls.AddRange(new Control[] { _btnAddItem, _btnRemoveItem, _btnMoveUp, _btnMoveDown });

        rightPanel.Controls.Add(_lvItems);
        rightPanel.Controls.Add(propBar);
        rightPanel.Controls.Add(btnBar);

        Controls.Add(rightPanel);
        Controls.Add(leftPanel);
    }

    // ── Sequence list ───────────────────────────────────────────────────────

    private void RefreshSequenceList()
    {
        _lstSequences.Items.Clear();
        foreach (var s in _sequences)
            _lstSequences.Items.Add(s);
        _lstSequences.DisplayMember = "Name";
    }

    private void LstSequences_SelectedIndexChanged(object? sender, EventArgs e)
    {
        _current = _lstSequences.SelectedItem as PlaylistSequence;
        if (_current == null) { _lblSequenceName.Text = "(none selected)"; return; }
        _lblSequenceName.Text = _current.Name;
        _cboRepeatMode.SelectedItem = _current.RepeatMode;
        _chkActive.Checked = _current.IsActive;
        RefreshItemList();
    }

    private void BtnNewSeq_Click(object? sender, EventArgs e)
    {
        var name = InputBoxForm.Show("Sequence name:", "New Sequence", $"Sequence {_sequences.Count + 1}", this);
        if (string.IsNullOrWhiteSpace(name)) return;
        var seq = new PlaylistSequence { Name = name };
        _sequences.Add(seq);
        RefreshSequenceList();
        _lstSequences.SelectedItem = _lstSequences.Items.Cast<PlaylistSequence>().FirstOrDefault(s => s.Id == seq.Id);
    }

    private void BtnDeleteSeq_Click(object? sender, EventArgs e)
    {
        if (_current == null) return;
        if (MessageBox.Show($"Delete sequence '{_current.Name}'?", "GoonNet", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            _sequences.Remove(_current);
            _current = null;
            RefreshSequenceList();
            _lvItems.Items.Clear();
        }
    }

    private void BtnRenameSeq_Click(object? sender, EventArgs e)
    {
        if (_current == null) return;
        var name = InputBoxForm.Show("New name:", "Rename Sequence", _current.Name, this);
        if (!string.IsNullOrWhiteSpace(name))
        {
            _current.Name = name;
            RefreshSequenceList();
            _lblSequenceName.Text = name;
        }
    }

    // ── Sequence items ──────────────────────────────────────────────────────

    private void RefreshItemList()
    {
        _lvItems.BeginUpdate();
        _lvItems.Items.Clear();
        if (_current == null) { _lvItems.EndUpdate(); return; }

        for (int i = 0; i < _current.Items.Count; i++)
        {
            var item = _current.Items[i];
            var pl = PlaylistDb?.GetById(item.PlaylistId);
            var lvi = new ListViewItem((i + 1).ToString());
            lvi.SubItems.Add(pl?.Name ?? item.PlaylistId.ToString());
            lvi.SubItems.Add(item.ScheduledDate.HasValue ? item.ScheduledDate.Value.ToString("ddd dd/MM/yyyy") : "Any");
            lvi.SubItems.Add(item.ScheduledTime.HasValue ? item.ScheduledTime.Value.ToString(@"hh\:mm") : "--");
            lvi.Tag = i;
            _lvItems.Items.Add(lvi);
        }
        _lvItems.EndUpdate();
    }

    private void BtnAddItem_Click(object? sender, EventArgs e)
    {
        if (_current == null) { MessageBox.Show("Select or create a sequence first.", "GoonNet"); return; }
        if (PlaylistDb == null) return;

        // Simple picker: list all playlists
        using var picker = new PlaylistPickerDialog(PlaylistDb);
        if (picker.ShowDialog(this) == DialogResult.OK && picker.SelectedId.HasValue)
        {
            _current.Items.Add(new SequenceItem
            {
                PlaylistId = picker.SelectedId.Value,
                Order = _current.Items.Count
            });
            RefreshItemList();
        }
    }

    private void BtnRemoveItem_Click(object? sender, EventArgs e)
    {
        if (_current == null || _lvItems.SelectedItems.Count == 0) return;
        int idx = (int)(_lvItems.SelectedItems[0].Tag ?? -1);
        if (idx >= 0 && idx < _current.Items.Count)
        {
            _current.Items.RemoveAt(idx);
            RefreshItemList();
        }
    }

    private void BtnMoveUp_Click(object? sender, EventArgs e)
    {
        if (_current == null || _lvItems.SelectedItems.Count == 0) return;
        int idx = (int)(_lvItems.SelectedItems[0].Tag ?? -1);
        if (idx > 0)
        {
            (_current.Items[idx], _current.Items[idx - 1]) = (_current.Items[idx - 1], _current.Items[idx]);
            RefreshItemList();
            if (idx - 1 < _lvItems.Items.Count) _lvItems.Items[idx - 1].Selected = true;
        }
    }

    private void BtnMoveDown_Click(object? sender, EventArgs e)
    {
        if (_current == null || _lvItems.SelectedItems.Count == 0) return;
        int idx = (int)(_lvItems.SelectedItems[0].Tag ?? -1);
        if (idx >= 0 && idx < _current.Items.Count - 1)
        {
            (_current.Items[idx], _current.Items[idx + 1]) = (_current.Items[idx + 1], _current.Items[idx]);
            RefreshItemList();
            if (idx + 1 < _lvItems.Items.Count) _lvItems.Items[idx + 1].Selected = true;
        }
    }

    // ── Persistence ─────────────────────────────────────────────────────────

    private void LoadSequences()
    {
        try
        {
            if (!System.IO.File.Exists(_savePath)) return;
            var ser = new System.Xml.Serialization.XmlSerializer(
                typeof(System.Collections.Generic.List<PlaylistSequence>),
                new System.Xml.Serialization.XmlRootAttribute("Sequences"));
            using var reader = new System.IO.StreamReader(_savePath);
            if (ser.Deserialize(reader) is System.Collections.Generic.List<PlaylistSequence> list)
                _sequences = list;
        }
        catch { /* use empty */ }
    }

    private void SaveSequences()
    {
        try
        {
            System.IO.Directory.CreateDirectory(AppDataPath);
            var ser = new System.Xml.Serialization.XmlSerializer(
                typeof(System.Collections.Generic.List<PlaylistSequence>),
                new System.Xml.Serialization.XmlRootAttribute("Sequences"));
            using var writer = new System.IO.StreamWriter(_savePath);
            ser.Serialize(writer, _sequences);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not save sequences:\n{ex.Message}", "GoonNet");
        }
    }
}

// Helper dialog to pick a playlist
internal class PlaylistPickerDialog : Form
{
    public Guid? SelectedId { get; private set; }
    private ListBox _lst = null!;

    public PlaylistPickerDialog(PlaylistDatabase db)
    {
        Text = "Select Playlist";
        Size = new Size(340, 300);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;

        _lst = new ListBox { Location = new Point(8, 8), Size = new Size(308, 210), DisplayMember = "Name" };
        foreach (var pl in db.GetAll()) _lst.Items.Add(pl);
        Controls.Add(_lst);

        var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(60, 228), Size = new Size(80, 26), FlatStyle = FlatStyle.System };
        btnOk.Click += (s, e) => { SelectedId = (_lst.SelectedItem as Playlist)?.Id; };
        var btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(160, 228), Size = new Size(80, 26), FlatStyle = FlatStyle.System };
        Controls.Add(btnOk); Controls.Add(btnCancel);
        AcceptButton = btnOk; CancelButton = btnCancel;
    }
}
