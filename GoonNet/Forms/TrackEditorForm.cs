using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace GoonNet;

/// <summary>
/// Dialog for adding/editing a MusicTrack record.
/// </summary>
public class TrackEditorForm : Form
{
    public MusicTrack? Track { get; private set; }

    private TextBox _txtArtist = null!;
    private TextBox _txtTitle = null!;
    private TextBox _txtGenre = null!;
    private TextBox _txtFileName = null!;
    private TextBox _txtLocation = null!;
    private TextBox _txtBPM = null!;
    private TextBox _txtComments = null!;
    private NumericUpDown _nudRating = null!;
    private CheckBox _chkNew = null!;
    private CheckBox _chkPromoted = null!;
    private CheckBox _chkArchived = null!;
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
        Text = Track?.Id == Guid.Empty ? "Add Track" : "Edit Track";
        Size = new Size(440, 320);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = SystemColors.Control;
        Font = new Font("Microsoft Sans Serif", 8f);

        int lx = 8, tx = 100, tw = 310, row = 8, rh = 26;

        Label L(string t) => new Label { Text = t, Location = new Point(lx, row + 3), Size = new Size(88, 16), TextAlign = ContentAlignment.MiddleRight };
        TextBox T(int w = 0) { var tb = new TextBox { Location = new Point(tx, row), Size = new Size(w > 0 ? w : tw, 20), BorderStyle = BorderStyle.Fixed3D }; return tb; }

        var lblArtist = L("Artist:"); _txtArtist = T(); row += rh;
        var lblTitle = L("Title:"); _txtTitle = T(); row += rh;
        var lblGenre = L("Genre:"); _txtGenre = T(); row += rh;

        var lblFile = L("File:");
        _txtFileName = T(220);
        _btnBrowse = new Button { Text = "...", Location = new Point(tx + 224, row), Size = new Size(40, 20), FlatStyle = FlatStyle.System };
        _btnBrowse.Click += BtnBrowse_Click;
        row += rh;

        var lblLoc = L("Location:"); _txtLocation = T(); row += rh;
        var lblBPM = L("BPM:"); _txtBPM = new TextBox { Location = new Point(tx, row), Size = new Size(60, 20), BorderStyle = BorderStyle.Fixed3D }; row += rh;
        var lblRating = L("Rating (0-100):"); _nudRating = new NumericUpDown { Location = new Point(tx, row), Size = new Size(60, 20), Minimum = 0, Maximum = 100 }; row += rh;

        _chkNew = new CheckBox { Text = "New", Location = new Point(tx, row), Size = new Size(70, 18) };
        _chkPromoted = new CheckBox { Text = "Promoted", Location = new Point(tx + 74, row), Size = new Size(80, 18) };
        _chkArchived = new CheckBox { Text = "Archived", Location = new Point(tx + 160, row), Size = new Size(80, 18) };
        row += rh;

        var lblComments = L("Comments:"); _txtComments = new TextBox { Location = new Point(tx, row), Size = new Size(tw, 36), BorderStyle = BorderStyle.Fixed3D, Multiline = true }; row += 42;

        _btnOK = new Button { Text = "OK", Location = new Point(tx + 160, row), Size = new Size(70, 24), FlatStyle = FlatStyle.System, DialogResult = DialogResult.OK };
        _btnOK.Click += BtnOK_Click;
        _btnCancel = new Button { Text = "Cancel", Location = new Point(tx + 238, row), Size = new Size(70, 24), FlatStyle = FlatStyle.System, DialogResult = DialogResult.Cancel };

        AcceptButton = _btnOK;
        CancelButton = _btnCancel;

        Controls.AddRange(new Control[]
        {
            lblArtist, _txtArtist, lblTitle, _txtTitle, lblGenre, _txtGenre,
            lblFile, _txtFileName, _btnBrowse, lblLoc, _txtLocation,
            lblBPM, _txtBPM, lblRating, _nudRating,
            _chkNew, _chkPromoted, _chkArchived,
            lblComments, _txtComments, _btnOK, _btnCancel
        });

        ClientSize = new Size(420, row + 32);
    }

    private void PopulateFields()
    {
        if (Track == null) return;
        _txtArtist.Text = Track.Artist;
        _txtTitle.Text = Track.Title;
        _txtGenre.Text = Track.Genre;
        _txtFileName.Text = Track.FileName;
        _txtLocation.Text = Track.Location;
        _txtBPM.Text = Track.BPM > 0 ? Track.BPM.ToString("F1") : "";
        _nudRating.Value = Math.Clamp(Track.Rating, 0, 100);
        _chkNew.Checked = Track.IsNew;
        _chkPromoted.Checked = Track.IsPromoted;
        _chkArchived.Checked = Track.IsArchived;
        _txtComments.Text = Track.Comments;
    }

    private void BtnBrowse_Click(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Select Audio File",
            Filter = "Audio Files|*.mp3;*.wav;*.flac;*.ogg;*.aac;*.wma|All Files|*.*",
            InitialDirectory = string.IsNullOrEmpty(_txtLocation.Text) ? Environment.GetFolderPath(Environment.SpecialFolder.MyMusic) : _txtLocation.Text
        };
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            _txtFileName.Text = Path.GetFileName(dlg.FileName);
            _txtLocation.Text = Path.GetDirectoryName(dlg.FileName) ?? string.Empty;
        }
    }

    private void BtnOK_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_txtArtist.Text) || string.IsNullOrWhiteSpace(_txtTitle.Text))
        {
            MessageBox.Show("Artist and Title are required.", "GoonNet", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        Track ??= new MusicTrack();
        Track.Artist = _txtArtist.Text.Trim();
        Track.Title = _txtTitle.Text.Trim();
        Track.Genre = _txtGenre.Text.Trim();
        Track.FileName = _txtFileName.Text.Trim();
        Track.Location = _txtLocation.Text.Trim();
        Track.BPM = double.TryParse(_txtBPM.Text, out var bpm) ? bpm : 0;
        Track.Rating = (int)_nudRating.Value;
        Track.Comments = _txtComments.Text.Trim();

        TrackFlag flags = TrackFlag.None;
        if (_chkNew.Checked) flags |= TrackFlag.New;
        if (_chkPromoted.Checked) flags |= TrackFlag.Promoted;
        if (_chkArchived.Checked) flags |= TrackFlag.Archived;
        Track.Flags = flags;
    }
}
