using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace GoonNet;

public class MusicLibraryForm : Form
{
    public MusicDatabase MusicDb { get; set; } = null!;

    private ListView _lvTracks = null!;
    private ListView _lvLibraryView = null!;
    private TextBox _txtSearch = null!;
    private Button _btnSearch = null!;
    private Button _btnAdd = null!;
    private Button _btnEdit = null!;
    private Button _btnDelete = null!;
    private Label _lblCount = null!;

    public MusicLibraryForm()
    {
        InitializeComponent();
        Load += MusicLibraryForm_Load;
    }

    private void InitializeComponent()
    {
        Text = "Music Library";
        Size = new Size(1020, 640);
        MinimumSize = new Size(760, 520);
        BackColor = SystemColors.Control;
        Font = new Font("Microsoft Sans Serif", 8f);

        var toolPanel = new Panel { Dock = DockStyle.Top, Height = 32 };

        var lblSearch = new Label { Text = "Find:", Location = new Point(8, 8), Size = new Size(40, 16) };
        _txtSearch = new TextBox { Location = new Point(48, 5), Size = new Size(250, 20), BorderStyle = BorderStyle.Fixed3D };
        _txtSearch.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) RefreshList(); };

        _btnSearch = new Button { Text = "Search", Location = new Point(304, 4), Size = new Size(64, 22), FlatStyle = FlatStyle.System };
        _btnSearch.Click += (s, e) => RefreshList();

        _btnAdd = new Button { Text = "Add", Location = new Point(378, 4), Size = new Size(54, 22), FlatStyle = FlatStyle.System };
        _btnAdd.Click += BtnAdd_Click;

        _btnEdit = new Button { Text = "Edit", Location = new Point(438, 4), Size = new Size(54, 22), FlatStyle = FlatStyle.System };
        _btnEdit.Click += BtnEdit_Click;

        _btnDelete = new Button { Text = "Delete", Location = new Point(498, 4), Size = new Size(56, 22), FlatStyle = FlatStyle.System };
        _btnDelete.Click += BtnDelete_Click;

        _lblCount = new Label { Text = "0 tracks", Location = new Point(568, 8), Size = new Size(220, 16) };
        toolPanel.Controls.AddRange(new Control[] { lblSearch, _txtSearch, _btnSearch, _btnAdd, _btnEdit, _btnDelete, _lblCount });

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
        _lvTracks.Columns.Add("Title", 210);
        _lvTracks.Columns.Add("Genre", 90);
        _lvTracks.Columns.Add("Time", 60);
        _lvTracks.Columns.Add("BPM", 50);
        _lvTracks.Columns.Add("Plays", 50);
        _lvTracks.Columns.Add("Path", 280);
        _lvTracks.DoubleClick += (s, e) => BtnEdit_Click(s, e);

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
        _lvLibraryView.Columns.Add("Song", 220);
        _lvLibraryView.Columns.Add("Category", 100);
        _lvLibraryView.Columns.Add("Last Played", 130);
        _lvLibraryView.Columns.Add("File", 340);

        split.Panel1.BackColor = Color.Black;
        split.Panel2.BackColor = Color.Black;
        split.Panel1.Controls.Add(_lvTracks);
        split.Panel2.Controls.Add(_lvLibraryView);

        Controls.Add(split);
        Controls.Add(toolPanel);
    }

    private void MusicLibraryForm_Load(object? sender, EventArgs e) => RefreshList();

    private void RefreshList()
    {
        _lvTracks.BeginUpdate();
        _lvLibraryView.BeginUpdate();
        _lvTracks.Items.Clear();
        _lvLibraryView.Items.Clear();

        var term = _txtSearch.Text.Trim();
        IEnumerable<MusicTrack> tracks = MusicDb?.GetAll() ?? Enumerable.Empty<MusicTrack>();
        if (!string.IsNullOrEmpty(term))
            tracks = tracks.Where(t => t.Artist.Contains(term, StringComparison.OrdinalIgnoreCase)
                                    || t.Title.Contains(term, StringComparison.OrdinalIgnoreCase)
                                    || t.Genre.Contains(term, StringComparison.OrdinalIgnoreCase));

        int count = 0;
        foreach (var t in tracks)
        {
            count++;
            var top = new ListViewItem(count.ToString());
            top.SubItems.Add(t.Artist);
            top.SubItems.Add(t.Title);
            top.SubItems.Add(t.Genre);
            top.SubItems.Add(t.Duration > TimeSpan.Zero ? t.Duration.ToString(@"m\:ss") : "");
            top.SubItems.Add(t.BPM > 0 ? t.BPM.ToString("F0") : "");
            top.SubItems.Add(t.PlayCount.ToString());
            top.SubItems.Add($"{t.Location}\\{t.FileName}".Trim('\\'));
            top.Tag = t;

            var bottom = new ListViewItem(t.Artist);
            bottom.SubItems.Add(t.Title);
            bottom.SubItems.Add(t.Genre);
            bottom.SubItems.Add(t.LastPlayed?.ToString("dd/MM/yy HH:mm") ?? "Never");
            bottom.SubItems.Add($"{t.Location}\\{t.FileName}".Trim('\\'));
            bottom.Tag = t;

            if (t.IsArchived)
            {
                top.ForeColor = Color.Gray;
                bottom.ForeColor = Color.Gray;
            }

            _lvTracks.Items.Add(top);
            _lvLibraryView.Items.Add(bottom);
        }

        _lvTracks.EndUpdate();
        _lvLibraryView.EndUpdate();
        _lblCount.Text = $"{count} track(s)";
    }

    private void BtnAdd_Click(object? sender, EventArgs e)
    {
        using var dlg = new TrackEditorForm();
        if (dlg.ShowDialog(this) == DialogResult.OK && dlg.Track != null)
        {
            MusicDb?.Add(dlg.Track);
            RefreshList();
        }
    }

    private void BtnEdit_Click(object? sender, EventArgs e)
    {
        var source = _lvTracks.SelectedItems.Count > 0 ? _lvTracks : _lvLibraryView;
        if (source.SelectedItems.Count == 0) return;

        var track = (MusicTrack?)source.SelectedItems[0].Tag;
        if (track == null) return;

        using var dlg = new TrackEditorForm(track);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            MusicDb?.Update(track);
            RefreshList();
        }
    }

    private void BtnDelete_Click(object? sender, EventArgs e)
    {
        var source = _lvTracks.SelectedItems.Count > 0 ? _lvTracks : _lvLibraryView;
        if (source.SelectedItems.Count == 0) return;

        var track = (MusicTrack?)source.SelectedItems[0].Tag;
        if (track == null) return;

        if (MessageBox.Show($"Delete '{track.Artist} - {track.Title}'?", "GoonNet",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            MusicDb?.Delete(track.Id);
            RefreshList();
        }
    }
}
