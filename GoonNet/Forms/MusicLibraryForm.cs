using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace GoonNet;

public class MusicLibraryForm : Form
{
    public MySqlMusicDatabase MusicDb { get; set; } = null!;

    private ListView _lvTracks = null!;
    private ListView _lvLibraryView = null!;
    private TextBox _txtSearch = null!;
    private ComboBox _cboPlaylistFilter = null!;
    private Button _btnSearch = null!;
    private Button _btnAdd = null!;
    private Button _btnEdit = null!;
    private Button _btnDelete = null!;
    private Button _btnImport = null!;
    private Button _btnExport = null!;
    private Label _lblCount = null!;

    public MusicLibraryForm()
    {
        InitializeComponent();
        Load += MusicLibraryForm_Load;
    }

    private void InitializeComponent()
    {
        Text = "Music Library";
        Size = new Size(1100, 660);
        MinimumSize = new Size(800, 520);
        BackColor = SystemColors.Control;
        Font = new Font("Microsoft Sans Serif", 8f);

        var toolPanel = new Panel { Dock = DockStyle.Top, Height = 34 };

        var lblSearch = new Label { Text = "Find:", Location = new Point(8, 9), Size = new Size(36, 16) };
        _txtSearch = new TextBox { Location = new Point(46, 6), Size = new Size(200, 20), BorderStyle = BorderStyle.Fixed3D };
        _txtSearch.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) RefreshList(); };

        var lblPl = new Label { Text = "Playlist:", Location = new Point(254, 9), Size = new Size(54, 16) };
        _cboPlaylistFilter = new ComboBox { Location = new Point(310, 5), Size = new Size(160, 22), DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.System };
        _cboPlaylistFilter.SelectedIndexChanged += (s, e) => RefreshList();

        _btnSearch = new Button { Text = "Search", Location = new Point(478, 5), Size = new Size(60, 22), FlatStyle = FlatStyle.System };
        _btnSearch.Click += (s, e) => RefreshList();

        _btnAdd = new Button { Text = "Add", Location = new Point(544, 5), Size = new Size(48, 22), FlatStyle = FlatStyle.System };
        _btnAdd.Click += BtnAdd_Click;

        _btnEdit = new Button { Text = "Edit", Location = new Point(598, 5), Size = new Size(48, 22), FlatStyle = FlatStyle.System };
        _btnEdit.Click += BtnEdit_Click;

        _btnDelete = new Button { Text = "Delete", Location = new Point(652, 5), Size = new Size(52, 22), FlatStyle = FlatStyle.System };
        _btnDelete.Click += BtnDelete_Click;

        _btnImport = new Button { Text = "📥 Import", Location = new Point(712, 5), Size = new Size(76, 22), FlatStyle = FlatStyle.System };
        _btnImport.Click += BtnImport_Click;

        _btnExport = new Button { Text = "📤 Export", Location = new Point(794, 5), Size = new Size(76, 22), FlatStyle = FlatStyle.System };
        _btnExport.Click += BtnExport_Click;

        _lblCount = new Label { Text = "0 tracks", Location = new Point(878, 9), Size = new Size(200, 16) };
        toolPanel.Controls.AddRange(new Control[]
        {
            lblSearch, _txtSearch, lblPl, _cboPlaylistFilter,
            _btnSearch, _btnAdd, _btnEdit, _btnDelete,
            _btnImport, _btnExport, _lblCount
        });

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterWidth = 4,
            SplitterDistance = 300,
            BorderStyle = BorderStyle.Fixed3D
        };

        _lvTracks = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            BorderStyle = BorderStyle.None,
            BackColor = Color.Black,
            ForeColor = Color.FromArgb(150, 255, 170),
            Font = new Font("Consolas", 8f)
        };
        _lvTracks.Columns.Add("#", 30);
        _lvTracks.Columns.Add("Artist", 170);
        _lvTracks.Columns.Add("Title", 200);
        _lvTracks.Columns.Add("Year", 46);
        _lvTracks.Columns.Add("Category", 90);
        _lvTracks.Columns.Add("Playlist", 100);
        _lvTracks.Columns.Add("Plays", 46);
        _lvTracks.Columns.Add("Path", 320);
        _lvTracks.DoubleClick += (s, e) => BtnEdit_Click(s, e);
        _lvTracks.ColumnClick += LvTracks_ColumnClick;

        _lvLibraryView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            BorderStyle = BorderStyle.None,
            BackColor = Color.Black,
            ForeColor = Color.FromArgb(200, 220, 255),
            Font = new Font("Consolas", 8f)
        };
        _lvLibraryView.Columns.Add("Artist", 170);
        _lvLibraryView.Columns.Add("Song", 210);
        _lvLibraryView.Columns.Add("Playlist", 110);
        _lvLibraryView.Columns.Add("Last Played", 130);
        _lvLibraryView.Columns.Add("File", 320);

        split.Panel1.BackColor = Color.Black;
        split.Panel2.BackColor = Color.Black;
        split.Panel1.Controls.Add(_lvTracks);
        split.Panel2.Controls.Add(_lvLibraryView);

        Controls.Add(split);
        Controls.Add(toolPanel);
    }

    private void MusicLibraryForm_Load(object? sender, EventArgs e)
    {
        RefreshPlaylistFilter();
        RefreshList();
    }

    private void RefreshPlaylistFilter()
    {
        _cboPlaylistFilter.Items.Clear();
        _cboPlaylistFilter.Items.Add("(All playlists)");
        if (MusicDb != null)
            foreach (var pl in MusicDb.GetAllPlaylistNames())
                _cboPlaylistFilter.Items.Add(pl);
        _cboPlaylistFilter.SelectedIndex = 0;
    }

    private int _sortColumn = -1;
    private bool _sortAsc = true;

    private void LvTracks_ColumnClick(object? sender, ColumnClickEventArgs e)
    {
        if (_sortColumn == e.Column) _sortAsc = !_sortAsc;
        else { _sortColumn = e.Column; _sortAsc = true; }
        RefreshList();
    }

    private void RefreshList()
    {
        _lvTracks.BeginUpdate();
        _lvLibraryView.BeginUpdate();
        _lvTracks.Items.Clear();
        _lvLibraryView.Items.Clear();

        var term = _txtSearch.Text.Trim();
        var playlistFilter = _cboPlaylistFilter.SelectedIndex > 0
            ? _cboPlaylistFilter.SelectedItem as string : null;

        IEnumerable<MusicTrack> tracks = MusicDb?.GetAll() ?? Enumerable.Empty<MusicTrack>();

        if (!string.IsNullOrEmpty(term))
            tracks = tracks.Where(t =>
                t.Artist.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                t.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                t.Genre.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                t.PlaylistName.Contains(term, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(playlistFilter))
            tracks = tracks.Where(t => t.PlaylistName.Equals(playlistFilter, StringComparison.OrdinalIgnoreCase));

        // Sorting
        tracks = _sortColumn switch
        {
            1 => _sortAsc ? tracks.OrderBy(t => t.Artist) : tracks.OrderByDescending(t => t.Artist),
            2 => _sortAsc ? tracks.OrderBy(t => t.Title) : tracks.OrderByDescending(t => t.Title),
            3 => _sortAsc ? tracks.OrderBy(t => t.Year) : tracks.OrderByDescending(t => t.Year),
            4 => _sortAsc ? tracks.OrderBy(t => t.Genre) : tracks.OrderByDescending(t => t.Genre),
            5 => _sortAsc ? tracks.OrderBy(t => t.PlaylistName) : tracks.OrderByDescending(t => t.PlaylistName),
            6 => _sortAsc ? tracks.OrderBy(t => t.PlayCount) : tracks.OrderByDescending(t => t.PlayCount),
            _ => tracks
        };

        int count = 0;
        foreach (var t in tracks)
        {
            count++;
            var top = new ListViewItem(count.ToString());
            top.SubItems.Add(t.Artist);
            top.SubItems.Add(t.Title);
            top.SubItems.Add(t.Year > 0 ? t.Year.ToString() : "");
            top.SubItems.Add(t.Genre);
            top.SubItems.Add(t.PlaylistName);
            top.SubItems.Add(t.PlayCount.ToString());
            top.SubItems.Add(t.FullPath.Trim('\\', '/'));
            top.Tag = t;

            var bottom = new ListViewItem(t.Artist);
            bottom.SubItems.Add(t.Title);
            bottom.SubItems.Add(t.PlaylistName);
            bottom.SubItems.Add(t.LastPlayed?.ToString("dd/MM/yy HH:mm") ?? "Never");
            bottom.SubItems.Add(t.FullPath.Trim('\\', '/'));
            bottom.Tag = t;

            _lvTracks.Items.Add(top);
            _lvLibraryView.Items.Add(bottom);
        }

        _lvTracks.EndUpdate();
        _lvLibraryView.EndUpdate();
        _lblCount.Text = $"{count} track(s)";
    }

    private void BtnAdd_Click(object? sender, EventArgs e)
    {
        using var dlg = new TrackEditorForm { MusicDb = MusicDb };
        if (dlg.ShowDialog(this) == DialogResult.OK && dlg.Track != null)
        {
            try
            {
                MusicDb?.Add(dlg.Track);
                RefreshPlaylistFilter();
                RefreshList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to add track:\n{ex.Message}", "GoonNet", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void BtnEdit_Click(object? sender, EventArgs e)
    {
        var source = _lvTracks.SelectedItems.Count > 0 ? _lvTracks : _lvLibraryView;
        if (source.SelectedItems.Count == 0) return;
        var track = (MusicTrack?)source.SelectedItems[0].Tag;
        if (track == null) return;

        using var dlg = new TrackEditorForm(track) { MusicDb = MusicDb };
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            MusicDb?.Update(track);
            RefreshPlaylistFilter();
            RefreshList();
        }
    }

    private void BtnDelete_Click(object? sender, EventArgs e)
    {
        var source = _lvTracks.SelectedItems.Count > 0 ? _lvTracks : _lvLibraryView;
        if (source.SelectedItems.Count == 0) return;
        var track = (MusicTrack?)source.SelectedItems[0].Tag;
        if (track == null) return;

        if (MessageBox.Show($"Delete '{track.Artist} – {track.Title}' from the database?", "GoonNet",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            MusicDb?.Delete(track.Id);
            RefreshPlaylistFilter();
            RefreshList();
        }
    }

    private void BtnImport_Click(object? sender, EventArgs e)
    {
        if (MusicDb == null) { MessageBox.Show("Music database not connected.", "GoonNet"); return; }
        using var dlg = new OpenFileDialog
        {
            Title = "Import Music Database",
            Filter = "CSV Files|*.csv|All Files|*.*"
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var (added, skipped) = MusicDb.ImportCsv(dlg.FileName);
            MessageBox.Show($"Import complete.\nAdded: {added}  Skipped (duplicate): {skipped}", "GoonNet Import");
            RefreshPlaylistFilter();
            RefreshList();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Import error:\n{ex.Message}", "GoonNet", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void BtnExport_Click(object? sender, EventArgs e)
    {
        using var menu = new ContextMenuStrip();
        menu.Items.Add("Export as CSV", null, (s, ev) => DoExport("csv"));
        menu.Items.Add("Export as SQL", null, (s, ev) => DoExport("sql"));
        menu.Show(_btnExport, 0, _btnExport.Height);
    }

    private void DoExport(string format)
    {
        if (MusicDb == null) { MessageBox.Show("Music database not connected.", "GoonNet"); return; }
        using var dlg = new SaveFileDialog
        {
            Title = "Export Music Database",
            Filter = format == "csv" ? "CSV Files|*.csv|All Files|*.*" : "SQL Files|*.sql|All Files|*.*",
            FileName = $"GoonNet_music_{DateTime.Now:yyyyMMdd}.{format}"
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            if (format == "csv") MusicDb.ExportCsv(dlg.FileName);
            else MusicDb.ExportSql(dlg.FileName);
            MessageBox.Show($"Exported {MusicDb.GetAll().Count} tracks to:\n{dlg.FileName}", "GoonNet Export");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Export error:\n{ex.Message}", "GoonNet", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
