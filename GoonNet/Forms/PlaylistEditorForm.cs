using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace GoonNet;

public class PlaylistEditorForm : Form
{
    public PlaylistDatabase PlaylistDb { get; set; } = null!;
    public MySqlMusicDatabase MusicDb { get; set; } = null!;

    private ComboBox _cmbPlaylists = null!;
    private Button _btnNewPlaylist = null!;
    private Button _btnSave = null!;
    private ListView _lvItems = null!;
    private Button _btnAddTrack = null!;
    private Button _btnRemove = null!;
    private Button _btnMoveUp = null!;
    private Button _btnMoveDown = null!;
    private Label _lblTotal = null!;
    private Label _lblSource = null!;

    // The playlist name currently selected from the MySQL "playlist" column
    private string? _currentMySqlPlaylist;
    // The XML-based playlist (kept for the Studio scheduler)
    private Playlist? _currentPlaylist;
    // Mode: true = MySQL-playlist-column based, false = XML Playlist
    private bool _mySqlMode;

    public PlaylistEditorForm()
    {
        InitializeComponent();
        Load += PlaylistEditorForm_Load;
    }

    private void InitializeComponent()
    {
        Text = "Playlist Editor";
        Size = new Size(760, 540);
        MinimumSize = new Size(580, 400);
        BackColor = SystemColors.Control;
        Font = new Font("Microsoft Sans Serif", 8f);

        var topPanel = new Panel { Dock = DockStyle.Top, Height = 58 };

        // Row 1: source selector
        var lblSource = new Label { Text = "Source:", Location = new Point(4, 6), Size = new Size(52, 16) };
        var cboSource = new ComboBox
        {
            Location = new Point(58, 3), Size = new Size(160, 22),
            DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.System
        };
        cboSource.Items.Add("🎵 Music DB Playlists");
        cboSource.Items.Add("📋 Broadcast Playlists");
        cboSource.SelectedIndex = 0;
        _lblSource = new Label { Text = "", Location = new Point(226, 6), Size = new Size(460, 16), ForeColor = Color.Gray };
        cboSource.SelectedIndexChanged += (s, e) =>
        {
            _mySqlMode = cboSource.SelectedIndex == 0;
            RefreshPlaylistDropdown();
        };
        topPanel.Controls.AddRange(new Control[] { lblSource, cboSource, _lblSource });

        // Row 2: playlist selector
        var lblPl = new Label { Text = "Playlist:", Location = new Point(4, 32), Size = new Size(52, 16) };
        _cmbPlaylists = new ComboBox { Location = new Point(58, 28), Size = new Size(280, 22), DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.System };
        _cmbPlaylists.SelectedIndexChanged += (s, e) => LoadSelectedPlaylist();

        _btnNewPlaylist = new Button { Text = "New", Location = new Point(342, 28), Size = new Size(60, 22), FlatStyle = FlatStyle.System };
        _btnNewPlaylist.Click += BtnNewPlaylist_Click;

        _btnSave = new Button { Text = "Save", Location = new Point(406, 28), Size = new Size(60, 22), FlatStyle = FlatStyle.System };
        _btnSave.Click += BtnSave_Click;

        _lblTotal = new Label { Text = "Total: 0:00:00", Location = new Point(476, 32), Size = new Size(220, 16) };

        topPanel.Controls.AddRange(new Control[] { lblPl, _cmbPlaylists, _btnNewPlaylist, _btnSave, _lblTotal });

        _lvItems = new ListView
        {
            Location = new Point(0, 58),
            Size = new Size(650, 460),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom | AnchorStyles.Right,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            BorderStyle = BorderStyle.Fixed3D,
            Font = new Font("Microsoft Sans Serif", 8f)
        };
        _lvItems.Columns.Add("#", 30);
        _lvItems.Columns.Add("Artist", 180);
        _lvItems.Columns.Add("Title", 200);
        _lvItems.Columns.Add("Year", 46);
        _lvItems.Columns.Add("Category", 90);
        _lvItems.Columns.Add("File", 200);

        var btnPanel = new Panel
        {
            Location = new Point(654, 58),
            Size = new Size(100, 460),
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
        _mySqlMode = true;
        RefreshPlaylistDropdown();
    }

    private void RefreshPlaylistDropdown()
    {
        _cmbPlaylists.Items.Clear();
        _currentMySqlPlaylist = null;
        _currentPlaylist = null;

        if (_mySqlMode)
        {
            _lblSource.Text = "Playlists defined by the music DB (playlist column)";
            foreach (var name in MusicDb?.GetAllPlaylistNames() ?? Enumerable.Empty<string>())
                _cmbPlaylists.Items.Add(name);
        }
        else
        {
            _lblSource.Text = "Broadcast playlists (used by the Studio scheduler)";
            foreach (var pl in PlaylistDb?.GetAll() ?? Enumerable.Empty<Playlist>())
                _cmbPlaylists.Items.Add(pl);
            _cmbPlaylists.DisplayMember = "Name";
        }

        _lvItems.Items.Clear();
        if (_cmbPlaylists.Items.Count > 0)
            _cmbPlaylists.SelectedIndex = 0;
    }

    private void LoadSelectedPlaylist()
    {
        _lvItems.Items.Clear();
        _currentMySqlPlaylist = null;
        _currentPlaylist = null;

        if (_mySqlMode)
        {
            _currentMySqlPlaylist = _cmbPlaylists.SelectedItem as string;
            RefreshMySqlPlaylistItems();
        }
        else
        {
            _currentPlaylist = _cmbPlaylists.SelectedItem as Playlist;
            RefreshXmlPlaylistItems();
        }
    }

    private void RefreshMySqlPlaylistItems()
    {
        _lvItems.BeginUpdate();
        _lvItems.Items.Clear();
        if (_currentMySqlPlaylist == null || MusicDb == null) { _lvItems.EndUpdate(); return; }

        var tracks = MusicDb.GetByPlaylist(_currentMySqlPlaylist).ToList();
        int i = 0;
        foreach (var t in tracks)
        {
            i++;
            var lvi = new ListViewItem(i.ToString());
            lvi.SubItems.Add(t.Artist);
            lvi.SubItems.Add(t.Title);
            lvi.SubItems.Add(t.Year > 0 ? t.Year.ToString() : "");
            lvi.SubItems.Add(t.Genre);
            lvi.SubItems.Add(t.FullPath.Trim('/', '\\'));
            lvi.Tag = t;
            _lvItems.Items.Add(lvi);
        }
        _lvItems.EndUpdate();
        _lblTotal.Text = $"{tracks.Count} track(s)";
    }

    private void RefreshXmlPlaylistItems()
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
            lvi.SubItems.Add("");
            lvi.SubItems.Add("");
            lvi.SubItems.Add(item.Duration.HasValue ? item.Duration.Value.ToString(@"m\:ss") : "");
            lvi.Tag = i;
            _lvItems.Items.Add(lvi);
        }
        _lvItems.EndUpdate();
        _lblTotal.Text = $"Total: {_currentPlaylist.TotalDuration:h\\:mm\\:ss}";
    }

    private void EnsureDisplayFields(PlaylistItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.Artist) && !string.IsNullOrWhiteSpace(item.Title)) return;
        var track = MusicDb?.GetById(item.TrackId);
        if (track == null) return;
        item.Artist = track.Artist;
        item.Title = track.Title;
        if (!item.Duration.HasValue || item.Duration.Value <= TimeSpan.Zero)
            item.Duration = track.Duration > TimeSpan.Zero ? track.Duration : null;
    }

    private void BtnNewPlaylist_Click(object? sender, EventArgs e)
    {
        if (_mySqlMode)
        {
            var name = InputBoxForm.Show("New playlist name (will be set on tracks):", "New DB Playlist", "", this);
            if (string.IsNullOrWhiteSpace(name)) return;
            if (!_cmbPlaylists.Items.Contains(name))
            {
                _cmbPlaylists.Items.Add(name);
                _cmbPlaylists.SelectedItem = name;
            }
        }
        else
        {
            var name = InputBoxForm.Show("Playlist name:", "New Playlist", $"Playlist {DateTime.Now:yyyy-MM-dd}", this);
            if (string.IsNullOrWhiteSpace(name)) return;
            var pl = new Playlist { Name = name };
            PlaylistDb?.Add(pl);
            RefreshPlaylistDropdown();
            _cmbPlaylists.SelectedItem = _cmbPlaylists.Items.Cast<Playlist>().FirstOrDefault(p => p.Id == pl.Id);
        }
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        if (!_mySqlMode && _currentPlaylist != null)
        {
            PlaylistDb?.Update(_currentPlaylist);
            MessageBox.Show("Broadcast playlist saved.", "GoonNet", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else
        {
            MessageBox.Show("Music DB playlist tracks are saved automatically when added/removed.", "GoonNet", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void BtnAddTrack_Click(object? sender, EventArgs e)
    {
        if (_mySqlMode)
        {
            if (string.IsNullOrEmpty(_currentMySqlPlaylist)) { MessageBox.Show("Select or create a playlist first.", "GoonNet"); return; }
            using var dlg = new TrackPickerForm { MusicDb = MusicDb };
            if (dlg.ShowDialog(this) == DialogResult.OK && dlg.SelectedTrack != null)
            {
                var t = dlg.SelectedTrack;
                t.PlaylistName = _currentMySqlPlaylist;
                MusicDb.Update(t);
                RefreshMySqlPlaylistItems();
            }
        }
        else
        {
            if (_currentPlaylist == null) { MessageBox.Show("Select or create a playlist first.", "GoonNet"); return; }
            using var dlg = new TrackPickerForm { MusicDb = MusicDb };
            if (dlg.ShowDialog(this) == DialogResult.OK && dlg.SelectedTrack != null)
            {
                var t = dlg.SelectedTrack;
                _currentPlaylist.Items.Add(new PlaylistItem
                {
                    TrackId = t.Id, TrackType = TrackType.Music,
                    Order = _currentPlaylist.Items.Count,
                    Artist = t.Artist, Title = t.Title,
                    Duration = t.Duration > TimeSpan.Zero ? t.Duration : null
                });
                RefreshXmlPlaylistItems();
            }
        }
    }

    private void BtnRemove_Click(object? sender, EventArgs e)
    {
        if (_lvItems.SelectedItems.Count == 0) return;

        if (_mySqlMode)
        {
            var track = _lvItems.SelectedItems[0].Tag as MusicTrack;
            if (track == null) return;
            if (MessageBox.Show($"Remove '{track.Artist} – {track.Title}' from playlist '{_currentMySqlPlaylist}'?",
                "GoonNet", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            track.PlaylistName = string.Empty;
            MusicDb.Update(track);
            RefreshMySqlPlaylistItems();
        }
        else
        {
            if (_currentPlaylist == null) return;
            var idx = (int)(_lvItems.SelectedItems[0].Tag ?? -1);
            if (idx >= 0 && idx < _currentPlaylist.Items.Count)
            {
                _currentPlaylist.Items.RemoveAt(idx);
                RefreshXmlPlaylistItems();
            }
        }
    }

    private void BtnMoveUp_Click(object? sender, EventArgs e)
    {
        if (_mySqlMode || _currentPlaylist == null || _lvItems.SelectedItems.Count == 0) return;
        var idx = (int)(_lvItems.SelectedItems[0].Tag ?? -1);
        if (idx > 0)
        {
            (_currentPlaylist.Items[idx], _currentPlaylist.Items[idx - 1]) = (_currentPlaylist.Items[idx - 1], _currentPlaylist.Items[idx]);
            RefreshXmlPlaylistItems();
            if (idx - 1 < _lvItems.Items.Count) _lvItems.Items[idx - 1].Selected = true;
        }
    }

    private void BtnMoveDown_Click(object? sender, EventArgs e)
    {
        if (_mySqlMode || _currentPlaylist == null || _lvItems.SelectedItems.Count == 0) return;
        var idx = (int)(_lvItems.SelectedItems[0].Tag ?? -1);
        if (idx >= 0 && idx < _currentPlaylist.Items.Count - 1)
        {
            (_currentPlaylist.Items[idx], _currentPlaylist.Items[idx + 1]) = (_currentPlaylist.Items[idx + 1], _currentPlaylist.Items[idx]);
            RefreshXmlPlaylistItems();
            if (idx + 1 < _lvItems.Items.Count) _lvItems.Items[idx + 1].Selected = true;
        }
    }
}
