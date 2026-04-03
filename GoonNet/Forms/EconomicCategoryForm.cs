using System;
using System.Drawing;
using System.Windows.Forms;

namespace GoonNet;

/// <summary>
/// Economic categories management form.
/// Categories are used to classify advertisers and spots for billing/reporting purposes.
/// </summary>
public class EconomicCategoryForm : Form
{
    public EconomicCategoryDatabase CategoryDb { get; set; } = null!;

    private ListView _lvCategories = null!;
    private Button _btnAdd = null!;
    private Button _btnEdit = null!;
    private Button _btnDelete = null!;
    private Button _btnSave = null!;
    private TextBox _txtSearch = null!;
    private Label _lblCount = null!;

    public EconomicCategoryForm()
    {
        InitializeComponent();
        Load += (s, e) => RefreshList();
    }

    private void InitializeComponent()
    {
        Text = "Economic Categories";
        Size = new Size(780, 500);
        MinimumSize = new Size(600, 380);
        BackColor = SystemColors.Control;
        Font = new Font("Microsoft Sans Serif", 8f);

        var toolPanel = new Panel { Dock = DockStyle.Top, Height = 32, BackColor = Color.FromArgb(240, 242, 252) };

        var lblSearch = new Label { Text = "Search:", Location = new Point(4, 8), Size = new Size(48, 16) };
        _txtSearch = new TextBox { Location = new Point(54, 5), Size = new Size(180, 22) };
        _txtSearch.TextChanged += (s, e) => RefreshList();

        _btnAdd = new Button { Text = "+ Add", Location = new Point(244, 4), Size = new Size(70, 24), FlatStyle = FlatStyle.System };
        _btnAdd.Click += BtnAdd_Click;

        _btnEdit = new Button { Text = "✏ Edit", Location = new Point(320, 4), Size = new Size(70, 24), FlatStyle = FlatStyle.System };
        _btnEdit.Click += BtnEdit_Click;

        _btnDelete = new Button { Text = "🗑 Delete", Location = new Point(396, 4), Size = new Size(80, 24), FlatStyle = FlatStyle.System };
        _btnDelete.Click += BtnDelete_Click;

        _btnSave = new Button { Text = "💾 Save", Location = new Point(484, 4), Size = new Size(70, 24), FlatStyle = FlatStyle.System };
        _btnSave.Click += async (s, e) =>
        {
            await CategoryDb.SaveAsync();
            MessageBox.Show("Categories saved.", "GoonNet");
        };

        _lblCount = new Label { Text = "0 categories", Location = new Point(566, 8), Size = new Size(120, 16) };

        toolPanel.Controls.AddRange(new Control[] { lblSearch, _txtSearch, _btnAdd, _btnEdit, _btnDelete, _btnSave, _lblCount });

        _lvCategories = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            BorderStyle = BorderStyle.Fixed3D,
            Font = new Font("Microsoft Sans Serif", 8f)
        };
        _lvCategories.Columns.Add("Name", 180);
        _lvCategories.Columns.Add("Description", 260);
        _lvCategories.Columns.Add("Rate/sec", 80);
        _lvCategories.Columns.Add("Rate/spot", 80);
        _lvCategories.Columns.Add("Color", 70);
        _lvCategories.Columns.Add("Active", 50);
        _lvCategories.DoubleClick += (s, e) => BtnEdit_Click(s, e);

        Controls.Add(_lvCategories);
        Controls.Add(toolPanel);
    }

    private void RefreshList()
    {
        _lvCategories.BeginUpdate();
        _lvCategories.Items.Clear();
        string search = _txtSearch?.Text.Trim().ToLowerInvariant() ?? string.Empty;
        int count = 0;
        foreach (var cat in CategoryDb?.GetAll() ?? System.Linq.Enumerable.Empty<EconomicCategory>())
        {
            if (!string.IsNullOrEmpty(search) &&
                !cat.Name.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                !cat.Description.Contains(search, StringComparison.OrdinalIgnoreCase))
                continue;

            var lvi = new ListViewItem(cat.Name);
            lvi.SubItems.Add(cat.Description);
            lvi.SubItems.Add(cat.RatePerSecond.ToString("0.00"));
            lvi.SubItems.Add(cat.RatePerSpot.ToString("0.00"));
            lvi.SubItems.Add(cat.ColorHex);
            lvi.SubItems.Add(cat.IsActive ? "Yes" : "No");
            lvi.Tag = cat.Id;
            try { lvi.BackColor = System.Drawing.ColorTranslator.FromHtml(cat.ColorHex); } catch { }
            if (!cat.IsActive) lvi.ForeColor = SystemColors.GrayText;
            _lvCategories.Items.Add(lvi);
            count++;
        }
        _lvCategories.EndUpdate();
        if (_lblCount != null) _lblCount.Text = $"{count} categor{(count == 1 ? "y" : "ies")}";
    }

    private void BtnAdd_Click(object? sender, EventArgs e)
    {
        var cat = new EconomicCategory();
        using var dlg = new EconomicCategoryDialog(cat);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            CategoryDb.Add(cat);
            RefreshList();
        }
    }

    private void BtnEdit_Click(object? sender, EventArgs e)
    {
        if (_lvCategories.SelectedItems.Count == 0) return;
        var id = (Guid)_lvCategories.SelectedItems[0].Tag!;
        var cat = CategoryDb.GetById(id);
        if (cat == null) return;
        using var dlg = new EconomicCategoryDialog(cat);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            CategoryDb.Update(cat);
            RefreshList();
        }
    }

    private void BtnDelete_Click(object? sender, EventArgs e)
    {
        if (_lvCategories.SelectedItems.Count == 0) return;
        var id = (Guid)_lvCategories.SelectedItems[0].Tag!;
        var cat = CategoryDb.GetById(id);
        if (cat == null) return;
        if (MessageBox.Show($"Delete category '{cat.Name}'?", "GoonNet", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            CategoryDb.Delete(id);
            RefreshList();
        }
    }
}

// ── Category editor dialog ───────────────────────────────────────────────────

internal class EconomicCategoryDialog : Form
{
    private readonly EconomicCategory _cat;
    private TextBox _txtName = null!;
    private TextBox _txtDescription = null!;
    private NumericUpDown _nudRateSec = null!;
    private NumericUpDown _nudRateSpot = null!;
    private TextBox _txtColor = null!;
    private CheckBox _chkActive = null!;
    private TextBox _txtNotes = null!;

    public EconomicCategoryDialog(EconomicCategory cat)
    {
        _cat = cat;
        InitializeComponent();
        LoadFields();
    }

    private void InitializeComponent()
    {
        Text = "Economic Category";
        Size = new Size(460, 320);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;

        int y = 14, lx = 10, lw = 100, fx = 116, fw = 300;

        void Row(string label, Control ctrl)
        {
            Controls.Add(new Label { Text = label, Location = new Point(lx, y + 2), Size = new Size(lw, 18), TextAlign = ContentAlignment.MiddleRight });
            ctrl.Location = new Point(fx, y); ctrl.Width = fw;
            Controls.Add(ctrl); y += 28;
        }

        _txtName = new TextBox();
        Row("Name:", _txtName);

        _txtDescription = new TextBox();
        Row("Description:", _txtDescription);

        Controls.Add(new Label { Text = "Rate / sec:", Location = new Point(lx, y + 2), Size = new Size(lw, 18), TextAlign = ContentAlignment.MiddleRight });
        _nudRateSec = new NumericUpDown { Location = new Point(fx, y), Size = new Size(90, 20), Minimum = 0, Maximum = 9999, DecimalPlaces = 2 };
        Controls.Add(_nudRateSec);
        Controls.Add(new Label { Text = "Rate / spot:", Location = new Point(fx + 100, y + 2), Size = new Size(80, 18) });
        _nudRateSpot = new NumericUpDown { Location = new Point(fx + 180, y), Size = new Size(90, 20), Minimum = 0, Maximum = 9999, DecimalPlaces = 2 };
        Controls.Add(_nudRateSpot);
        y += 28;

        _txtColor = new TextBox();
        var btnColor = new Button { Text = "🎨", Location = new Point(fx + fw + 2, y), Size = new Size(28, 20), FlatStyle = FlatStyle.System };
        btnColor.Click += (s, e) =>
        {
            using var cd = new ColorDialog();
            try { cd.Color = ColorTranslator.FromHtml(_txtColor.Text); } catch { }
            if (cd.ShowDialog() == DialogResult.OK) _txtColor.Text = ColorTranslator.ToHtml(cd.Color);
        };
        Controls.Add(btnColor);
        Row("Color (hex):", _txtColor);

        _chkActive = new CheckBox { Text = "Active", Location = new Point(fx, y), Checked = true };
        Controls.Add(_chkActive); y += 26;

        _txtNotes = new TextBox { Multiline = true, Height = 40 };
        Row("Notes:", _txtNotes);

        var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(fx, y), Size = new Size(80, 26), FlatStyle = FlatStyle.System };
        btnOk.Click += BtnOk_Click;
        var btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(fx + 90, y), Size = new Size(80, 26), FlatStyle = FlatStyle.System };
        Controls.Add(btnOk); Controls.Add(btnCancel);
        AcceptButton = btnOk; CancelButton = btnCancel;
        ClientSize = new Size(440, y + 54);
    }

    private void LoadFields()
    {
        _txtName.Text = _cat.Name;
        _txtDescription.Text = _cat.Description;
        _nudRateSec.Value = Math.Clamp(_cat.RatePerSecond, 0, 9999);
        _nudRateSpot.Value = Math.Clamp(_cat.RatePerSpot, 0, 9999);
        _txtColor.Text = _cat.ColorHex;
        _chkActive.Checked = _cat.IsActive;
        _txtNotes.Text = _cat.Notes;
    }

    private void BtnOk_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_txtName.Text))
        {
            MessageBox.Show("Name is required.", "GoonNet", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None; return;
        }
        _cat.Name = _txtName.Text.Trim();
        _cat.Description = _txtDescription.Text.Trim();
        _cat.RatePerSecond = _nudRateSec.Value;
        _cat.RatePerSpot = _nudRateSpot.Value;
        _cat.ColorHex = _txtColor.Text.Trim();
        _cat.IsActive = _chkActive.Checked;
        _cat.Notes = _txtNotes.Text.Trim();
    }
}
