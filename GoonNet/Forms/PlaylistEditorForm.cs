using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace GoonNet;

public class PlaylistEditorForm : Form
{
    public PlaylistDatabase PlaylistDb { get; set; } = null!;
    public MusicDatabase MusicDb { get; set; } = null!;

    private ComboBox _cmbPlaylists = null!;
    private Button _btnNewPlaylist = null!;
    private Button _btnSave = null!;
    private ListView _lvItems = null!;
    private Button _btnAddTrack = null!;
    private Button _btnRemove = null!;
    private Button _btnMoveUp = null!;
    private Button _btnMoveDown = null!;
    private Label _lblTotal = null!;

    private Playlist? _currentPlaylist;

    public PlaylistEditorForm()
    {
        InitializeComponent();
        Load += PlaylistEditorForm_Load;
    }

    private void InitializeComponent()
    {
        Text = "Playlist Editor";
        Size = new Size(700, 520);
        MinimumSize = new Size(580, 400);
        BackColor = SystemColors.Control;
        Font = new Font("Microsoft Sans Serif", 8f);

        var topPanel = new Panel { Dock = DockStyle.Top, Height = 32 };

        var lblPl = new Label { Text = "Playlist:", Location = new Point(4, 8), Size = new Size(55, 16) };
        _cmbPlaylists = new ComboBox { Location = new Point(62, 4), Size = new Size(280, 22), DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.System };
        _cmbPlaylists.SelectedIndexChanged += (s, e) => LoadSelectedPlaylist();

        _btnNewPlaylist = new Button { Text = "New", Location = new Point(346, 4), Size = new Size(60, 22), FlatStyle = FlatStyle.System };
        _btnNewPlaylist.Click += BtnNewPlaylist_Click;

        _btnSave = new Button { Text = "Save", Location = new Point(410, 4), Size = new Size(60, 22), FlatStyle = FlatStyle.System };
        _btnSave.Click += BtnSave_Click;

        _lblTotal = new Label { Text = "Total: 0:00:00", Location = new Point(480, 8), Size = new Size(200, 16) };

        topPanel.Controls.AddRange(new Control[] { lblPl, _cmbPlaylists, _btnNewPlaylist, _btnSave, _lblTotal });

        _lvItems = new ListView
        {
            Location = new Point(0, 32),
            Size = new Size(580, 440),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom | AnchorStyles.Right,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            BorderStyle = BorderStyle.Fixed3D,
            Font = new Font("Microsoft Sans Serif", 8f)
        };
        _lvItems.Columns.Add("#", 30);
        _lvItems.Columns.Add("Artist", 160);
        _lvItems.Columns.Add("Title", 180);
        _lvItems.Columns.Add("Duration", 70);
        _lvItems.Columns.Add("Notes", 120);

        var btnPanel = new Panel
        {
            Location = new Point(584, 32),
            Size = new Size(100, 440),
            Anchor = AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom
        };

        _btnAddTrack = new Button { Text = "Add Track...", Location = new Point(4, 4), Size = new Size(90, 24), FlatStyle = FlatStyle.System };
        _btnAddTrack.Click += BtnAddTrack_Click;
        _btnRemove = new Button { Text = "Remove", Location = new Point(4, 32), Size = new Size(90, 24), FlatStyle = FlatStyle.System };
        _btnRemove.Click += BtnRemove_Click;
        _btnMoveUp = new Button { Text = "▲ Move Up", Location = new Point(4, 64), Size = new Size(90, 24), FlatStyle = FlatStyle.System };
        _btnMoveUp.Click += BtnMoveUp_Click;
        _btnMoveDown = new Button { Text = "▼ Move Down", Location = new Point(4, 92), Size = new Size(90, 24), FlatStyle = FlatStyle.System };
        _btnMoveDown.Click += BtnMoveDown_Click;

        btnPanel.Controls.AddRange(new Control[] { _btnAddTrack, _btnRemove, _btnMoveUp, _btnMoveDown });

        Controls.Add(topPanel);
        Controls.Add(_lvItems);
        Controls.Add(btnPanel);
    }

    private void PlaylistEditorForm_Load(object? sender, EventArgs e)
    {
        RefreshPlaylistDropdown();
    }

    private void RefreshPlaylistDropdown()
    {
        _cmbPlaylists.Items.Clear();
        foreach (var pl in PlaylistDb?.GetAll() ?? Enumerable.Empty<Playlist>())
            _cmbPlaylists.Items.Add(pl);
        _cmbPlaylists.DisplayMember = "Name";
        if (_cmbPlaylists.Items.Count > 0)
            _cmbPlaylists.SelectedIndex = 0;
    }

    private void LoadSelectedPlaylist()
    {
        _currentPlaylist = _cmbPlaylists.SelectedItem as Playlist;
        RefreshItemList();
    }

    private void RefreshItemList()
    {
        _lvItems.BeginUpdate();
        _lvItems.Items.Clear();
        if (_currentPlaylist == null) { _lvItems.EndUpdate(); return; }

        for (int i = 0; i < _currentPlaylist.Items.Count; i++)
        {
            var item = _currentPlaylist.Items[i];
            EnsureDisplayFields(item);
            var lvi = new ListViewItem((i + 1).ToString());
            lvi.SubItems.Add(item.Artist);
            lvi.SubItems.Add(item.Title);
            lvi.SubItems.Add(item.Duration.HasValue ? item.Duration.Value.ToString(@"m\:ss") : "");
            lvi.SubItems.Add(item.Notes);
            lvi.Tag = i;
            _lvItems.Items.Add(lvi);
        }
        _lvItems.EndUpdate();
        _lblTotal.Text = $"Total: {_currentPlaylist.TotalDuration:h\\:mm\\:ss}";
    }

    private void EnsureDisplayFields(PlaylistItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.Artist) && !string.IsNullOrWhiteSpace(item.Title))
            return;
        var track = MusicDb?.GetById(item.TrackId);
        if (track == null) return;
        item.Artist = track.Artist;
        item.Title = track.Title;
        if (!item.Duration.HasValue || item.Duration.Value <= TimeSpan.Zero)
            item.Duration = track.Duration > TimeSpan.Zero ? track.Duration : null;
    }

    private void BtnNewPlaylist_Click(object? sender, EventArgs e)
    {
        var name = InputBoxForm.Show("Playlist name:", "New Playlist", $"Playlist {DateTime.Now:yyyy-MM-dd}", this);
        if (string.IsNullOrWhiteSpace(name)) return;
        var pl = new Playlist { Name = name };
        PlaylistDb?.Add(pl);
        RefreshPlaylistDropdown();
        _cmbPlaylists.SelectedItem = _cmbPlaylists.Items.Cast<Playlist>().FirstOrDefault(p => p.Id == pl.Id);
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        if (_currentPlaylist == null) return;
        PlaylistDb?.Update(_currentPlaylist);
        MessageBox.Show("Playlist saved.", "GoonNet", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void BtnAddTrack_Click(object? sender, EventArgs e)
    {
        if (_currentPlaylist == null) { MessageBox.Show("Select or create a playlist first.", "GoonNet"); return; }
        using var dlg = new TrackPickerForm { MusicDb = MusicDb };
        if (dlg.ShowDialog(this) == DialogResult.OK && dlg.SelectedTrack != null)
        {
            var t = dlg.SelectedTrack;
            _currentPlaylist.Items.Add(new PlaylistItem
            {
                TrackId = t.Id,
                TrackType = TrackType.Music,
                Order = _currentPlaylist.Items.Count,
                Artist = t.Artist,
                Title = t.Title,
                Duration = t.Duration > TimeSpan.Zero ? t.Duration : null
            });
            RefreshItemList();
        }
    }

    private void BtnRemove_Click(object? sender, EventArgs e)
    {
        if (_currentPlaylist == null || _lvItems.SelectedItems.Count == 0) return;
        var idx = (int)(_lvItems.SelectedItems[0].Tag ?? -1);
        if (idx >= 0 && idx < _currentPlaylist.Items.Count)
        {
            _currentPlaylist.Items.RemoveAt(idx);
            RefreshItemList();
        }
    }

    private void BtnMoveUp_Click(object? sender, EventArgs e)
    {
        if (_currentPlaylist == null || _lvItems.SelectedItems.Count == 0) return;
        var idx = (int)(_lvItems.SelectedItems[0].Tag ?? -1);
        if (idx > 0)
        {
            (_currentPlaylist.Items[idx], _currentPlaylist.Items[idx - 1]) = (_currentPlaylist.Items[idx - 1], _currentPlaylist.Items[idx]);
            RefreshItemList();
            if (idx - 1 < _lvItems.Items.Count) _lvItems.Items[idx - 1].Selected = true;
        }
    }

    private void BtnMoveDown_Click(object? sender, EventArgs e)
    {
        if (_currentPlaylist == null || _lvItems.SelectedItems.Count == 0) return;
        var idx = (int)(_lvItems.SelectedItems[0].Tag ?? -1);
        if (idx >= 0 && idx < _currentPlaylist.Items.Count - 1)
        {
            (_currentPlaylist.Items[idx], _currentPlaylist.Items[idx + 1]) = (_currentPlaylist.Items[idx + 1], _currentPlaylist.Items[idx]);
            RefreshItemList();
            if (idx + 1 < _lvItems.Items.Count) _lvItems.Items[idx + 1].Selected = true;
        }
    }
}
