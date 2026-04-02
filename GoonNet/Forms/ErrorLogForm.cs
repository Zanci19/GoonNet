using System;
using System.Drawing;
using System.Windows.Forms;

namespace GoonNet;

public class ErrorLogForm : Form
{
    private ListView _lvErrors = null!;
    private Button _btnClear = null!;
    private Button _btnRefresh = null!;
    private Label _lblCount = null!;

    public ErrorLogForm()
    {
        InitializeComponent();
        Load += ErrorLogForm_Load;
        FormClosed += ErrorLogForm_FormClosed;
    }

    private void InitializeComponent()
    {
        Text = "Error Log";
        Size = new Size(800, 480);
        BackColor = SystemColors.Control;
        Font = new Font("Microsoft Sans Serif", 8f);

        var toolPanel = new Panel { Dock = DockStyle.Top, Height = 32 };

        _btnRefresh = new Button { Text = "Refresh", Location = new Point(4, 4), Size = new Size(60, 22), FlatStyle = FlatStyle.System };
        _btnRefresh.Click += (s, e) => RefreshList();

        _btnClear = new Button { Text = "Clear", Location = new Point(68, 4), Size = new Size(60, 22), FlatStyle = FlatStyle.System };
        _btnClear.Click += BtnClear_Click;

        _lblCount = new Label { Text = "0 errors", Location = new Point(136, 8), Size = new Size(120, 16) };

        toolPanel.Controls.AddRange(new Control[] { _btnRefresh, _btnClear, _lblCount });

        _lvErrors = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            BorderStyle = BorderStyle.Fixed3D,
            Font = new Font("Microsoft Sans Serif", 8f)
        };
        _lvErrors.Columns.Add("Time", 140);
        _lvErrors.Columns.Add("Source", 120);
        _lvErrors.Columns.Add("Message", 500);

        Controls.Add(toolPanel);
        Controls.Add(_lvErrors);
    }

    private void ErrorLogForm_Load(object? sender, EventArgs e)
    {
        ErrorLog.Instance.ErrorAdded += OnErrorAdded;
        ErrorLog.Instance.Cleared += OnCleared;
        RefreshList();
    }

    private void ErrorLogForm_FormClosed(object? sender, FormClosedEventArgs e)
    {
        ErrorLog.Instance.ErrorAdded -= OnErrorAdded;
        ErrorLog.Instance.Cleared -= OnCleared;
    }

    private void OnErrorAdded(object? sender, ErrorLogEntry entry)
    {
        if (InvokeRequired)
            Invoke(() => AddEntry(entry));
        else
            AddEntry(entry);
    }

    private void OnCleared(object? sender, EventArgs e)
    {
        if (InvokeRequired)
            Invoke(ClearList);
        else
            ClearList();
    }

    private void ClearList()
    {
        _lvErrors.Items.Clear();
        _lblCount.Text = "0 errors";
    }

    private ListViewItem CreateItem(ErrorLogEntry entry)
    {
        var lvi = new ListViewItem(entry.Timestamp.ToString("dd/MM/yyyy HH:mm:ss"));
        lvi.SubItems.Add(entry.Source);
        lvi.SubItems.Add(entry.Message);
        lvi.ForeColor = Color.DarkRed;
        lvi.Tag = entry;
        return lvi;
    }

    private void AddEntry(ErrorLogEntry entry)
    {
        _lvErrors.Items.Add(CreateItem(entry));
        _lblCount.Text = $"{_lvErrors.Items.Count} error{(_lvErrors.Items.Count == 1 ? "" : "s")}";
        _lvErrors.EnsureVisible(_lvErrors.Items.Count - 1);
    }

    private void RefreshList()
    {
        _lvErrors.BeginUpdate();
        _lvErrors.Items.Clear();
        foreach (var entry in ErrorLog.Instance.Entries)
            _lvErrors.Items.Add(CreateItem(entry));
        _lvErrors.EndUpdate();
        _lblCount.Text = $"{_lvErrors.Items.Count} error{(_lvErrors.Items.Count == 1 ? "" : "s")}";
    }

    private void BtnClear_Click(object? sender, EventArgs e)
    {
        ErrorLog.Instance.Clear();
    }
}
