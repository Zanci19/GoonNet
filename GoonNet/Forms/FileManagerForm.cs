using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace GoonNet;

public class FileManagerForm : Form
{
    private TreeView _tvFolders = null!;
    private ListView _lvFiles = null!;
    private Label _lblPath = null!;
    private Button _btnRefresh = null!;

    public FileManagerForm()
    {
        InitializeComponent();
        Load += FileManagerForm_Load;
    }

    private void InitializeComponent()
    {
        Text = "File Manager";
        Size = new Size(700, 480);
        BackColor = SystemColors.Control;
        Font = new Font("Microsoft Sans Serif", 8f);

        var toolPanel = new Panel { Dock = DockStyle.Top, Height = 30 };
        _lblPath = new Label { Location = new Point(4, 8), Size = new Size(500, 16), Text = "Select a folder" };
        _btnRefresh = new Button { Text = "Refresh", Location = new Point(510, 4), Size = new Size(60, 22), FlatStyle = FlatStyle.System };
        _btnRefresh.Click += (s, e) => RefreshFileList();
        toolPanel.Controls.AddRange(new Control[] { _lblPath, _btnRefresh });

        _tvFolders = new TreeView
        {
            Location = new Point(0, 30),
            Size = new Size(220, 420),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom,
            BorderStyle = BorderStyle.Fixed3D,
            Font = new Font("Microsoft Sans Serif", 8f)
        };
        _tvFolders.AfterSelect += TvFolders_AfterSelect;

        _lvFiles = new ListView
        {
            Location = new Point(224, 30),
            Size = new Size(462, 420),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            BorderStyle = BorderStyle.Fixed3D,
            Font = new Font("Microsoft Sans Serif", 8f)
        };
        _lvFiles.Columns.Add("Name", 220);
        _lvFiles.Columns.Add("Size", 80);
        _lvFiles.Columns.Add("Modified", 130);
        _lvFiles.Columns.Add("Ext", 60);

        Controls.Add(toolPanel);
        Controls.Add(_tvFolders);
        Controls.Add(_lvFiles);
    }

    private void FileManagerForm_Load(object? sender, EventArgs e)
    {
        PopulateFolderTree();
    }

    private void PopulateFolderTree()
    {
        _tvFolders.BeginUpdate();
        _tvFolders.Nodes.Clear();
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (!drive.IsReady) continue;
            var node = new TreeNode(drive.Name) { Tag = drive.RootDirectory.FullName };
            node.Nodes.Add(new TreeNode("...")); // Lazy load placeholder
            _tvFolders.Nodes.Add(node);
        }
        _tvFolders.EndUpdate();
    }

    private void TvFolders_AfterSelect(object? sender, TreeViewEventArgs e)
    {
        if (e.Node?.Tag is not string path) return;
        _lblPath.Text = path;

        // Expand children lazily
        if (e.Node.Nodes.Count == 1 && e.Node.Nodes[0].Text == "...")
        {
            e.Node.Nodes.Clear();
            try
            {
                foreach (var dir in Directory.GetDirectories(path))
                {
                    var child = new TreeNode(Path.GetFileName(dir)) { Tag = dir };
                    child.Nodes.Add(new TreeNode("..."));
                    e.Node.Nodes.Add(child);
                }
            }
            catch { /* Access denied etc */ }
        }

        RefreshFileList(path);
    }

    private void RefreshFileList(string? path = null)
    {
        if (path == null) path = _tvFolders.SelectedNode?.Tag as string;
        if (path == null) return;

        _lvFiles.BeginUpdate();
        _lvFiles.Items.Clear();
        try
        {
            foreach (var file in Directory.GetFiles(path, "*.*"))
            {
                var info = new FileInfo(file);
                var lvi = new ListViewItem(info.Name);
                lvi.SubItems.Add(FormatSize(info.Length));
                lvi.SubItems.Add(info.LastWriteTime.ToString("dd/MM/yyyy HH:mm"));
                lvi.SubItems.Add(info.Extension.ToUpperInvariant());
                lvi.Tag = file;

                var ext = info.Extension.ToLowerInvariant();
                if (ext is ".mp3" or ".wav" or ".flac" or ".ogg" or ".aac" or ".wma")
                    lvi.ForeColor = Color.DarkBlue;

                _lvFiles.Items.Add(lvi);
            }
        }
        catch { /* Access denied */ }
        _lvFiles.EndUpdate();
    }

    private static string FormatSize(long bytes)
    {
        if (bytes >= 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        if (bytes >= 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes} B";
    }
}
