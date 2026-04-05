using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace GoonNet;

/// <summary>
/// Dialog for adding/editing a MusicTrack record backed by the MySQL music table.
/// Fields match the DB schema: author, title, year, category, music_path, playlist.
/// </summary>
public class TrackEditorForm : Form
{
    public MusicTrack? Track { get; private set; }
    public MySqlMusicDatabase? MusicDb { get; set; }

    private TextBox _txtArtist = null!;
    private TextBox _txtTitle = null!;
    private NumericUpDown _nudYear = null!;
    private TextBox _txtGenre = null!;
    private TextBox _txtFileName = null!;
    private ComboBox _cboPlaylist = null!;
    private Button _btnBrowse = null!;
    private Button _btnOK = null!;
    private Button _btnCancel = null!;

    public TrackEditorForm() : this(null) { }

    public TrackEditorForm(MusicTrack? track)
    {
        Track = track ?? new MusicTrack();
        InitializeComponent();
        PopulateFields();
    }

    private void InitializeComponent()
    {
        Text = (Track?.DbId == 0) ? "Add Track" : "Edit Track";
        Size = new Size(440, 250);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = SystemColors.Control;
        Font = new Font("Microsoft Sans Serif", 8f);

        int lx = 8, tx = 110, tw = 290, row = 10, rh = 26;

        Label L(string t) => new Label { Text = t, Location = new Point(lx, row + 3), Size = new Size(100, 16), TextAlign = ContentAlignment.MiddleRight };
        TextBox T(int w = 0) { var tb = new TextBox { Location = new Point(tx, row), Size = new Size(w > 0 ? w : tw, 20), BorderStyle = BorderStyle.Fixed3D }; return tb; }

        var lblArtist = L("Author / Artist:"); _txtArtist = T(); row += rh;
        var lblTitle = L("Title:"); _txtTitle = T(); row += rh;

        var lblYear = L("Year:");
        _nudYear = new NumericUpDown { Location = new Point(tx, row), Size = new Size(80, 20), Minimum = 0, Maximum = 9999, Value = 0 };
        row += rh;

        var lblGenre = L("Category / Genre:"); _txtGenre = T(); row += rh;

        var lblFile = L("File (in /music):");
        _txtFileName = T(220);
        _btnBrowse = new Button { Text = "...", Location = new Point(tx + 224, row), Size = new Size(40, 20), FlatStyle = FlatStyle.System };
        _btnBrowse.Click += BtnBrowse_Click;
        row += rh;

        var lblPlaylist = L("Playlist:");
        _cboPlaylist = new ComboBox { Location = new Point(tx, row), Size = new Size(tw, 20), DropDownStyle = ComboBoxStyle.DropDown, FlatStyle = FlatStyle.System };
        row += rh + 4;

        _btnOK = new Button { Text = "OK", Location = new Point(tx + 150, row), Size = new Size(66, 24), FlatStyle = FlatStyle.System, DialogResult = DialogResult.OK };
        _btnOK.Click += BtnOK_Click;
        _btnCancel = new Button { Text = "Cancel", Location = new Point(tx + 222, row), Size = new Size(66, 24), FlatStyle = FlatStyle.System, DialogResult = DialogResult.Cancel };

        AcceptButton = _btnOK;
        CancelButton = _btnCancel;

        Controls.AddRange(new Control[]
        {
            lblArtist, _txtArtist,
            lblTitle, _txtTitle,
            lblYear, _nudYear,
            lblGenre, _txtGenre,
            lblFile, _txtFileName, _btnBrowse,
            lblPlaylist, _cboPlaylist,
            _btnOK, _btnCancel
        });

        ClientSize = new Size(420, row + 32);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        // Populate playlist dropdown from DB if available
        _cboPlaylist.Items.Clear();
        if (MusicDb != null)
            foreach (var name in MusicDb.GetAllPlaylistNames())
                _cboPlaylist.Items.Add(name);
        if (!string.IsNullOrEmpty(Track?.PlaylistName) && !_cboPlaylist.Items.Contains(Track.PlaylistName))
            _cboPlaylist.Items.Insert(0, Track.PlaylistName);
        if (!string.IsNullOrEmpty(Track?.PlaylistName))
            _cboPlaylist.Text = Track.PlaylistName;
    }

    private void PopulateFields()
    {
        if (Track == null) return;
        _txtArtist.Text = Track.Artist;
        _txtTitle.Text = Track.Title;
        _nudYear.Value = Track.Year >= 0 && Track.Year <= 9999 ? Track.Year : 0;
        _txtGenre.Text = Track.Genre;
        _txtFileName.Text = string.IsNullOrEmpty(Track.Location)
            ? Track.FileName
            : Track.Location.TrimEnd('/') + "/" + Track.FileName;
    }

    private void BtnBrowse_Click(object? sender, EventArgs e)
    {
        var musicFolder = AppSettings.Instance.MusicFolder;
        using var dlg = new OpenFileDialog
        {
            Title = "Select Audio File from /music folder",
            Filter = "Audio Files|*.mp3;*.wav;*.flac;*.ogg;*.aac;*.wma|All Files|*.*",
            InitialDirectory = Directory.Exists(musicFolder) ? musicFolder : AppContext.BaseDirectory
        };
        if (dlg.ShowDialog() == DialogResult.OK)
            _txtFileName.Text = dlg.FileName;
    }

    private void BtnOK_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_txtArtist.Text) || string.IsNullOrWhiteSpace(_txtTitle.Text))
        {
            MessageBox.Show("Author and Title are required.", "GoonNet", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }
        if (string.IsNullOrWhiteSpace(_txtFileName.Text))
        {
            MessageBox.Show("A music file path is required.", "GoonNet", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        Track ??= new MusicTrack();
        Track.Artist = _txtArtist.Text.Trim();
        Track.Title = _txtTitle.Text.Trim();
        Track.Year = (int)_nudYear.Value;
        Track.Genre = _txtGenre.Text.Trim();
        Track.PlaylistName = _cboPlaylist.Text.Trim();

        // Normalise file path: store as /music/filename relative to app root
        var rawPath = _txtFileName.Text.Trim().Replace('\\', '/');
        var appBase = AppContext.BaseDirectory.Replace('\\', '/').TrimEnd('/');
        if (rawPath.StartsWith(appBase, StringComparison.OrdinalIgnoreCase))
            rawPath = rawPath[appBase.Length..];
        if (!rawPath.StartsWith("/"))
            rawPath = "/music/" + Path.GetFileName(rawPath);
        Track.Location = Path.GetDirectoryName(rawPath)?.Replace('\\', '/') ?? "/music";
        Track.FileName = Path.GetFileName(rawPath);
    }
}

