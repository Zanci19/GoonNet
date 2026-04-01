using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace GoonNet;

public class MainForm : Form
{
    public UserAccount? CurrentUser { get; private set; }

    public MusicDatabase MusicDb { get; } = new();
    public JingleDatabase JingleDb { get; } = new();
    public AdDatabase AdDb { get; } = new();
    public BackgroundDatabase BackgroundDb { get; } = new();
    public BlockDatabase BlockDb { get; } = new();
    public EventDatabase EventDb { get; } = new();
    public PlaylistDatabase PlaylistDb { get; } = new();
    public UserDatabase UserDb { get; } = new();
    public LogDatabase LogDb { get; } = new();

    private System.Windows.Forms.Timer _clockTimer = null!;
    private ToolStripStatusLabel _statusLeft = null!;
    private ToolStripStatusLabel _statusMiddle = null!;
    private ToolStripStatusLabel _statusRight = null!;

    private static string AppDataPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GoonNet");

    public MainForm()
    {
        InitializeDatabases();
        InitializeComponent();
        Load += MainForm_Load;
        FormClosing += MainForm_FormClosing;
    }

    private void InitializeDatabases()
    {
        var basePath = AppDataPath;
        MusicDb.Initialize(Path.Combine(basePath, "music.xml"));
        JingleDb.Initialize(Path.Combine(basePath, "jingles.xml"));
        AdDb.Initialize(Path.Combine(basePath, "ads.xml"));
        BackgroundDb.Initialize(Path.Combine(basePath, "backgrounds.xml"));
        BlockDb.Initialize(Path.Combine(basePath, "blocks.xml"));
        EventDb.Initialize(Path.Combine(basePath, "events.xml"));
        PlaylistDb.Initialize(Path.Combine(basePath, "playlists.xml"));
        UserDb.Initialize(Path.Combine(basePath, "users.xml"));
        LogDb.Initialize(Path.Combine(basePath, "log.xml"));
    }

    private void InitializeComponent()
    {
        Text = "GoonNet Radio Automation";
        Size = new Size(1024, 768);
        StartPosition = FormStartPosition.CenterScreen;
        IsMdiContainer = true;
        BackColor = SystemColors.AppWorkspace;
        Font = new Font("Microsoft Sans Serif", 8f);

        BuildMenuStrip();
        BuildToolStrip();
        BuildStatusStrip();

        _clockTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _clockTimer.Tick += (s, e) => _statusMiddle.Text = DateTime.Now.ToString("HH:mm:ss  ddd dd/MM/yyyy");
        _clockTimer.Start();
    }

    private void BuildMenuStrip()
    {
        var menu = new MenuStrip { BackColor = SystemColors.Control, Font = new Font("Microsoft Sans Serif", 8f) };

        var fileMenu = new ToolStripMenuItem("&File");
        fileMenu.DropDownItems.AddRange(new ToolStripItem[]
        {
            new ToolStripMenuItem("&New Playlist", null, (s, e) => OpenPlaylistEditor()),
            new ToolStripMenuItem("&Open...", null, (s, e) => OpenPlaylistEditor()),
            new ToolStripMenuItem("&Settings...", null, (s, e) => OpenSettings()),
            new ToolStripSeparator(),
            new ToolStripMenuItem("E&xit", null, (s, e) => Close())
        });

        var viewMenu = new ToolStripMenuItem("&View");
        viewMenu.DropDownItems.AddRange(new ToolStripItem[]
        {
            new ToolStripMenuItem("&Studio", null, (s, e) => OpenStudio()),
            new ToolStripMenuItem("S&cheduler", null, (s, e) => OpenScheduler()),
            new ToolStripMenuItem("&Log Viewer", null, (s, e) => OpenLogViewer()),
            new ToolStripSeparator(),
            new ToolStripMenuItem("&Cascade", null, (s, e) => LayoutMdi(MdiLayout.Cascade)),
            new ToolStripMenuItem("&Tile", null, (s, e) => LayoutMdi(MdiLayout.TileHorizontal))
        });

        var libMenu = new ToolStripMenuItem("&Libraries");
        libMenu.DropDownItems.AddRange(new ToolStripItem[]
        {
            new ToolStripMenuItem("&Music Library", null, (s, e) => OpenMusicLibrary()),
            new ToolStripMenuItem("&Jingles", null, (s, e) => MessageBox.Show("Jingle Manager coming soon", "GoonNet")),
            new ToolStripMenuItem("&Advertisements", null, (s, e) => MessageBox.Show("Ad Manager coming soon", "GoonNet")),
            new ToolStripMenuItem("&Backgrounds", null, (s, e) => MessageBox.Show("Background Manager coming soon", "GoonNet")),
            new ToolStripMenuItem("Bl&ocks", null, (s, e) => MessageBox.Show("Block Manager coming soon", "GoonNet"))
        });

        var schedMenu = new ToolStripMenuItem("&Schedule");
        schedMenu.DropDownItems.AddRange(new ToolStripItem[]
        {
            new ToolStripMenuItem("&Event Editor", null, (s, e) => OpenScheduler()),
            new ToolStripMenuItem("&Playlist Editor", null, (s, e) => OpenPlaylistEditor()),
            new ToolStripMenuItem("Playlist &Sequences", null, (s, e) => MessageBox.Show("Sequence editor coming soon", "GoonNet"))
        });

        var toolsMenu = new ToolStripMenuItem("&Tools");
        toolsMenu.DropDownItems.AddRange(new ToolStripItem[]
        {
            new ToolStripMenuItem("&File Manager", null, (s, e) => OpenFileManager()),
            new ToolStripMenuItem("&User Manager", null, (s, e) => OpenUserManager()),
            new ToolStripMenuItem("&Streaming...", null, (s, e) => OpenStreaming()),
            new ToolStripMenuItem("&Email Settings", null, (s, e) => OpenSettings()),
            new ToolStripMenuItem("&Telnet Server", null, (s, e) => MessageBox.Show("Telnet server settings coming soon", "GoonNet"))
        });

        var helpMenu = new ToolStripMenuItem("&Help");
        helpMenu.DropDownItems.Add(new ToolStripMenuItem("&About GoonNet", null, ShowAbout));

        menu.Items.AddRange(new ToolStripItem[] { fileMenu, viewMenu, libMenu, schedMenu, toolsMenu, helpMenu });
        Controls.Add(menu);
        MainMenuStrip = menu;
    }

    private void BuildToolStrip()
    {
        // ── Navigation bar — switches between main screens ────────────────────
        var navBar = new ToolStrip
        {
            GripStyle = ToolStripGripStyle.Hidden,
            RenderMode = ToolStripRenderMode.System,
            BackColor = Color.FromArgb(30, 60, 100),   // dark blue
            Font = new Font("Microsoft Sans Serif", 8.5f, FontStyle.Bold)
        };

        ToolStripButton NavBtn(string text, string tip, Action action)
        {
            var b = new ToolStripButton(text)
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                ToolTipText = tip,
                ForeColor = Color.FromArgb(200, 230, 255),
                Margin = new Padding(1, 0, 1, 0)
            };
            b.Click += (s, e) => action();
            return b;
        }

        navBar.Items.AddRange(new ToolStripItem[]
        {
            NavBtn("🖥 Studio",    "Open Studio player",       OpenStudio),
            NavBtn("🎵 Library",   "Music Library",            OpenMusicLibrary),
            NavBtn("📅 Schedule",  "Event Scheduler",          OpenScheduler),
            NavBtn("📋 Playlists", "Playlist Editor",          OpenPlaylistEditor),
            new ToolStripSeparator(),
            NavBtn("📂 Files",     "File Manager",             OpenFileManager),
            NavBtn("📜 Log",       "Broadcast Log Viewer",     OpenLogViewer),
            NavBtn("👤 Users",     "User Manager",             OpenUserManager),
        });
        Controls.Add(navBar);

        // ── Operations bar — transport and quick-action buttons ───────────────
        var opsBar = new ToolStrip
        {
            GripStyle = ToolStripGripStyle.Hidden,
            RenderMode = ToolStripRenderMode.System,
            BackColor = Color.FromArgb(50, 30, 30),    // dark maroon/red
            Font = new Font("Microsoft Sans Serif", 8f)
        };

        ToolStripButton OpsBtn(string text, string tip, Color fg, Action action)
        {
            var b = new ToolStripButton(text)
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                ToolTipText = tip,
                ForeColor = fg,
                Margin = new Padding(1, 0, 1, 0)
            };
            b.Click += (s, e) => action();
            return b;
        }

        var stopColor = Color.FromArgb(255, 100, 100);
        var stdColor  = Color.FromArgb(220, 210, 200);

        opsBar.Items.AddRange(new ToolStripItem[]
        {
            OpsBtn("■ STOP ALL", "Stop all playback immediately",
                stopColor, () => { AudioEngine.Instance.Stop(); AudioEngine.Instance.Stop(AudioDeviceType.Preview); }),
            new ToolStripSeparator(),
            OpsBtn("📡 Streaming…",  "Web streaming settings",  stdColor, OpenStreaming),
            OpsBtn("⚙ Settings…",   "Application settings",    stdColor, OpenSettings),
            new ToolStripSeparator(),
            OpsBtn("ℹ About",        "About GoonNet",           stdColor, ShowAbout),
        });
        Controls.Add(opsBar);
    }

    private void BuildStatusStrip()
    {
        var strip = new StatusStrip { BackColor = SystemColors.Control, Font = new Font("Microsoft Sans Serif", 8f), SizingGrip = true };
        _statusLeft = new ToolStripStatusLabel("Not logged in") { Spring = false, TextAlign = ContentAlignment.MiddleLeft };
        _statusMiddle = new ToolStripStatusLabel(DateTime.Now.ToString("HH:mm:ss")) { Spring = true, TextAlign = ContentAlignment.MiddleCenter };
        _statusRight = new ToolStripStatusLabel("Ready") { Spring = false, TextAlign = ContentAlignment.MiddleRight };
        strip.Items.AddRange(new ToolStripItem[] { _statusLeft, new ToolStripSeparator(), _statusMiddle, new ToolStripSeparator(), _statusRight });
        Controls.Add(strip);
    }

    private async void MainForm_Load(object? sender, EventArgs e)
    {
        await MusicDb.LoadAsync();
        await JingleDb.LoadAsync();
        await AdDb.LoadAsync();
        await BackgroundDb.LoadAsync();
        await BlockDb.LoadAsync();
        await EventDb.LoadAsync();
        await PlaylistDb.LoadAsync();
        await UserDb.LoadAsync();
        await LogDb.LoadAsync();
        UserDb.EnsureDefaultAdmin();

        using var login = new LoginForm { UserDb = UserDb };
        if (login.ShowDialog() != DialogResult.OK || login.LoggedInUser == null)
        {
            Close();
            return;
        }
        CurrentUser = login.LoggedInUser;
        _statusLeft.Text = $"User: {CurrentUser.FullName} ({CurrentUser.Role})";
        _statusRight.Text = "Databases loaded";

        OpenStudio();
    }

    private async void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        _clockTimer.Stop();
        AudioEngine.Instance.Stop();
        AudioEngine.Instance.Stop(AudioDeviceType.Preview);
        AudioEngine.Instance.Dispose();

        await MusicDb.SaveAsync();
        await JingleDb.SaveAsync();
        await AdDb.SaveAsync();
        await BackgroundDb.SaveAsync();
        await BlockDb.SaveAsync();
        await EventDb.SaveAsync();
        await PlaylistDb.SaveAsync();
        await UserDb.SaveAsync();
        await LogDb.SaveAsync();
    }

    private void OpenStudio()
    {
        foreach (Form child in MdiChildren)
            if (child is StudioForm) { child.Activate(); return; }
        var f = new StudioForm { MdiParent = this, PlaylistDb = PlaylistDb, MusicDb = MusicDb, LogDb = LogDb };
        f.Show();
    }

    private void OpenMusicLibrary()
    {
        var f = new MusicLibraryForm { MdiParent = this, MusicDb = MusicDb };
        f.Show();
    }

    private void OpenScheduler()
    {
        var f = new SchedulerForm { MdiParent = this, EventDb = EventDb };
        f.Show();
    }

    private void OpenPlaylistEditor()
    {
        var f = new PlaylistEditorForm { MdiParent = this, PlaylistDb = PlaylistDb, MusicDb = MusicDb };
        f.Show();
    }

    private void OpenFileManager()
    {
        var f = new FileManagerForm { MdiParent = this };
        f.Show();
    }

    private void OpenLogViewer()
    {
        var f = new LogViewerForm { MdiParent = this, LogDb = LogDb };
        f.Show();
    }

    private void OpenSettings()
    {
        using var f = new SettingsForm();
        f.ShowDialog(this);
    }

    private void OpenUserManager()
    {
        if (CurrentUser?.Role != UserRole.Admin)
        {
            MessageBox.Show("Access denied. Admin role required.", "GoonNet", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        var f = new UserManagerForm { MdiParent = this, UserDb = UserDb };
        f.Show();
    }

    private void OpenStreaming()
    {
        foreach (Form child in MdiChildren)
            if (child is StreamingForm) { child.Activate(); return; }
        var f = new StreamingForm { MdiParent = this };
        f.Show();
    }

    private void ShowAbout(object? sender, EventArgs e) => ShowAbout();
    private void ShowAbout()
    {
        MessageBox.Show(
            "GoonNet Radio Automation System\n\nVersion 1.0\n\nA classic Windows 98-style radio automation system.\n\nBuilt with C# and NAudio.",
            "About GoonNet",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }
}
