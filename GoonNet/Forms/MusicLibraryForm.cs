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
        Size = new Size(800, 520);
        BackColor = SystemColors.Control;
        Font = new Font("Microsoft Sans Serif", 8f);

        var toolPanel = new Panel { Location = new Point(0, 0), Size = new Size(800, 30), Dock = DockStyle.Top };

        var lblSearch = new Label { Text = "Search:", Location = new Point(4, 6), Size = new Size(48, 16) };
        _txtSearch = new TextBox { Location = new Point(56, 4), Size = new Size(200, 20), BorderStyle = BorderStyle.Fixed3D };
        _txtSearch.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) RefreshList(); };

        _btnSearch = new Button { Text = "Search", Location = new Point(260, 3), Size = new Size(60, 22), FlatStyle = FlatStyle.System };
        _btnSearch.Click += (s, e) => RefreshList();

        _btnAdd = new Button { Text = "Add...", Location = new Point(328, 3), Size = new Size(60, 22), FlatStyle = FlatStyle.System };
        _btnAdd.Click += BtnAdd_Click;

        _btnEdit = new Button { Text = "Edit...", Location = new Point(392, 3), Size = new Size(60, 22), FlatStyle = FlatStyle.System };
        _btnEdit.Click += BtnEdit_Click;

        _btnDelete = new Button { Text = "Delete", Location = new Point(456, 3), Size = new Size(60, 22), FlatStyle = FlatStyle.System };
        _btnDelete.Click += BtnDelete_Click;

        _lblCount = new Label { Text = "0 tracks", Location = new Point(530, 6), Size = new Size(120, 16) };

        toolPanel.Controls.AddRange(new Control[] { lblSearch, _txtSearch, _btnSearch, _btnAdd, _btnEdit, _btnDelete, _lblCount });

        _lvTracks = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            BorderStyle = BorderStyle.Fixed3D,
            Font = new Font("Microsoft Sans Serif", 8f)
        };
        _lvTracks.Columns.Add("Artist", 160);
        _lvTracks.Columns.Add("Title", 180);
        _lvTracks.Columns.Add("Genre", 90);
        _lvTracks.Columns.Add("Duration", 70);
        _lvTracks.Columns.Add("BPM", 50);
        _lvTracks.Columns.Add("Plays", 50);
        _lvTracks.Columns.Add("Last Played", 110);
        _lvTracks.Columns.Add("File", 160);
        _lvTracks.DoubleClick += (s, e) => BtnEdit_Click(s, e);

        Controls.Add(toolPanel);
        Controls.Add(_lvTracks);
    }

    private void MusicLibraryForm_Load(object? sender, EventArgs e) => RefreshList();

    private void RefreshList()
    {
        _lvTracks.BeginUpdate();
        _lvTracks.Items.Clear();
        var term = _txtSearch.Text.Trim();
        IEnumerable<MusicTrack> tracks = MusicDb?.GetAll() ?? Enumerable.Empty<MusicTrack>();
        if (!string.IsNullOrEmpty(term))
            tracks = tracks.Where(t => t.Artist.Contains(term, StringComparison.OrdinalIgnoreCase)
                                    || t.Title.Contains(term, StringComparison.OrdinalIgnoreCase)
                                    || t.Genre.Contains(term, StringComparison.OrdinalIgnoreCase));
        int count = 0;
        foreach (var t in tracks)
        {
            var lvi = new ListViewItem(t.Artist);
            lvi.SubItems.Add(t.Title);
            lvi.SubItems.Add(t.Genre);
            lvi.SubItems.Add(t.Duration > TimeSpan.Zero ? t.Duration.ToString(@"m\:ss") : "");
            lvi.SubItems.Add(t.BPM > 0 ? t.BPM.ToString("F0") : "");
            lvi.SubItems.Add(t.PlayCount.ToString());
            lvi.SubItems.Add(t.LastPlayed?.ToString("dd/MM/yy HH:mm") ?? "Never");
            lvi.SubItems.Add(t.FileName);
            lvi.Tag = t;
            if (t.IsArchived) lvi.ForeColor = SystemColors.GrayText;
            _lvTracks.Items.Add(lvi);
            count++;
        }
        _lvTracks.EndUpdate();
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
        if (_lvTracks.SelectedItems.Count == 0) return;
        var track = (MusicTrack?)_lvTracks.SelectedItems[0].Tag;
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
        if (_lvTracks.SelectedItems.Count == 0) return;
        var track = (MusicTrack?)_lvTracks.SelectedItems[0].Tag;
        if (track == null) return;
        if (MessageBox.Show($"Delete '{track.Artist} - {track.Title}'?", "GoonNet",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            MusicDb?.Delete(track.Id);
            RefreshList();
        }
    }
}
