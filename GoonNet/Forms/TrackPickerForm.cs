using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace GoonNet;

/// <summary>
/// Simple dialog to pick a track from the music library.
/// </summary>
public class TrackPickerForm : Form
{
    public MySqlMusicDatabase MusicDb { get; set; } = null!;
    public MusicTrack? SelectedTrack { get; private set; }

    private ListView _lvTracks = null!;
    private TextBox _txtSearch = null!;
    private Button _btnOK = null!;
    private Button _btnCancel = null!;

    public TrackPickerForm()
    {
        InitializeComponent();
        Load += TrackPickerForm_Load;
    }

    private void InitializeComponent()
    {
        Text = "Select Track";
        Size = new Size(600, 420);
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = SystemColors.Control;
        Font = new Font("Microsoft Sans Serif", 8f);

        var topPanel = new Panel { Dock = DockStyle.Top, Height = 30 };
        var lblSearch = new Label { Text = "Search:", Location = new Point(4, 7), Size = new Size(48, 16) };
        _txtSearch = new TextBox { Location = new Point(56, 4), Size = new Size(200, 20), BorderStyle = BorderStyle.Fixed3D };
        _txtSearch.TextChanged += (s, e) => RefreshList();
        topPanel.Controls.AddRange(new Control[] { lblSearch, _txtSearch });

        _lvTracks = new ListView
        {
            Location = new Point(0, 30),
            Size = new Size(594, 340),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom | AnchorStyles.Right,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            BorderStyle = BorderStyle.Fixed3D,
            Font = new Font("Microsoft Sans Serif", 8f)
        };
        _lvTracks.Columns.Add("Artist", 160);
        _lvTracks.Columns.Add("Title", 180);
        _lvTracks.Columns.Add("Genre", 80);
        _lvTracks.Columns.Add("Duration", 70);
        _lvTracks.DoubleClick += (s, e) => SelectAndClose();

        var btnPanel = new Panel { Dock = DockStyle.Bottom, Height = 34 };
        _btnOK = new Button { Text = "OK", Location = new Point(440, 4), Size = new Size(70, 24), FlatStyle = FlatStyle.System, DialogResult = DialogResult.OK };
        _btnOK.Click += (s, e) => SelectAndClose();
        _btnCancel = new Button { Text = "Cancel", Location = new Point(516, 4), Size = new Size(70, 24), FlatStyle = FlatStyle.System, DialogResult = DialogResult.Cancel };
        btnPanel.Controls.AddRange(new Control[] { _btnOK, _btnCancel });

        AcceptButton = _btnOK;
        CancelButton = _btnCancel;
        Controls.Add(topPanel);
        Controls.Add(_lvTracks);
        Controls.Add(btnPanel);
    }

    private void TrackPickerForm_Load(object? sender, EventArgs e) => RefreshList();

    private void RefreshList()
    {
        _lvTracks.BeginUpdate();
        _lvTracks.Items.Clear();
        var term = _txtSearch.Text.Trim();
        var tracks = MusicDb?.GetAll().AsEnumerable() ?? Enumerable.Empty<MusicTrack>();
        if (!string.IsNullOrEmpty(term))
            tracks = tracks.Where(t => t.Artist.Contains(term, StringComparison.OrdinalIgnoreCase)
                                    || t.Title.Contains(term, StringComparison.OrdinalIgnoreCase));
        foreach (var t in tracks)
        {
            var lvi = new ListViewItem(t.Artist);
            lvi.SubItems.Add(t.Title);
            lvi.SubItems.Add(t.Genre);
            lvi.SubItems.Add(t.Duration > TimeSpan.Zero ? t.Duration.ToString(@"m\:ss") : "");
            lvi.Tag = t;
            _lvTracks.Items.Add(lvi);
        }
        _lvTracks.EndUpdate();
    }

    private void SelectAndClose()
    {
        if (_lvTracks.SelectedItems.Count == 0) return;
        SelectedTrack = (MusicTrack?)_lvTracks.SelectedItems[0].Tag;
        DialogResult = DialogResult.OK;
        Close();
    }
}
