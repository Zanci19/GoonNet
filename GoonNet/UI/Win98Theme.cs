using System;
using System.Drawing;
using System.Windows.Forms;

namespace GoonNet;

public static class Win98Theme
{
    public static readonly Color ClassicGray = SystemColors.Control;
    public static readonly Color ClassicBlue = Color.FromArgb(0, 0, 128);
    public static readonly Color ClassicHighlight = SystemColors.Highlight;
    public static readonly Color MissingColor = Color.FromArgb(255, 180, 180);
    public static readonly Color PlayingColor = Color.FromArgb(180, 255, 180);
    public static readonly Color WaitingColor = Color.FromArgb(255, 255, 180);
    public static readonly Color ExpiredColor = Color.FromArgb(210, 210, 210);
    public static readonly Color RecordingColor = Color.FromArgb(255, 160, 160);

    // Cached font instances - shared across the application (do not dispose externally)
    private static readonly Font _classicFont = new Font("Microsoft Sans Serif", 8f, FontStyle.Regular);
    private static readonly Font _boldFont = new Font("Microsoft Sans Serif", 8f, FontStyle.Bold);
    private static readonly Font _largeFont = new Font("Microsoft Sans Serif", 12f, FontStyle.Bold);
    private static readonly Font _clockFont = new Font("Microsoft Sans Serif", 18f, FontStyle.Bold);

    public static Font ClassicFont => _classicFont;
    public static Font BoldFont => _boldFont;
    public static Font LargeFont => _largeFont;
    public static Font ClockFont => _clockFont;

    public static void ApplyTheme(Control control)
    {
        control.BackColor = SystemColors.Control;
        control.ForeColor = SystemColors.ControlText;
        control.Font = ClassicFont;

        foreach (Control child in control.Controls)
        {
            ApplyThemeToControl(child);
            if (child.HasChildren)
                ApplyTheme(child);
        }
    }

    public static void ApplyTheme(Form form)
    {
        form.BackColor = SystemColors.Control;
        form.ForeColor = SystemColors.ControlText;
        form.Font = ClassicFont;
        ApplyTheme((Control)form);
    }

    private static void ApplyThemeToControl(Control c)
    {
        c.Font = ClassicFont;
        switch (c)
        {
            case Button btn:
                btn.FlatStyle = FlatStyle.System;
                btn.BackColor = SystemColors.Control;
                break;
            case Panel panel:
                panel.BackColor = SystemColors.Control;
                break;
            case GroupBox gb:
                gb.BackColor = SystemColors.Control;
                break;
            case Label lbl:
                lbl.BackColor = SystemColors.Control;
                break;
            case TextBox tb:
                tb.BorderStyle = BorderStyle.Fixed3D;
                break;
            case ListBox lb:
                lb.BorderStyle = BorderStyle.Fixed3D;
                break;
            case ListView lv:
                lv.BorderStyle = BorderStyle.Fixed3D;
                break;
            case ComboBox cb:
                cb.FlatStyle = FlatStyle.System;
                break;
            case TabControl tc:
                tc.Appearance = TabAppearance.Normal;
                break;
        }
    }

    public static StatusStrip CreateStatusBar(Form form)
    {
        var strip = new StatusStrip
        {
            SizingGrip = true,
            BackColor = SystemColors.Control,
            Font = ClassicFont
        };
        form.Controls.Add(strip);
        return strip;
    }

    public static ToolStrip CreateToolBar(Form form)
    {
        var strip = new ToolStrip
        {
            GripStyle = ToolStripGripStyle.Hidden,
            RenderMode = ToolStripRenderMode.System,
            BackColor = SystemColors.Control,
            Font = ClassicFont
        };
        form.Controls.Add(strip);
        return strip;
    }

    public static Panel CreateSunkenPanel()
    {
        return new Panel
        {
            BorderStyle = BorderStyle.Fixed3D,
            BackColor = SystemColors.Control
        };
    }

    public static Panel CreateRaisedPanel()
    {
        return new Panel
        {
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = SystemColors.Control
        };
    }

    public static void StyleListView(ListView lv)
    {
        lv.View = View.Details;
        lv.FullRowSelect = true;
        lv.GridLines = true;
        lv.BorderStyle = BorderStyle.Fixed3D;
        lv.Font = ClassicFont;
    }

    public static void StyleButton(Button btn, bool isDefault = false)
    {
        btn.FlatStyle = FlatStyle.System;
        btn.BackColor = SystemColors.Control;
        btn.Font = isDefault ? BoldFont : ClassicFont;
    }

    public static Button CreateButton(string text, int width = 80, int height = 24)
    {
        var btn = new Button
        {
            Text = text,
            Width = width,
            Height = height,
            FlatStyle = FlatStyle.System,
            BackColor = SystemColors.Control,
            Font = ClassicFont
        };
        return btn;
    }
}
