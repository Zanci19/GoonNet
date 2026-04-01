using System;
using System.Drawing;
using System.Windows.Forms;

namespace GoonNet;

/// <summary>
/// Simple Win98-style input box dialog (replacement for VB Interaction.InputBox).
/// </summary>
internal class InputBoxForm : Form
{
    private TextBox _txt = null!;
    private Button _btnOK = null!;
    private Button _btnCancel = null!;

    public string Value => _txt.Text;

    public InputBoxForm(string prompt, string title, string defaultValue = "")
    {
        Text = title;
        Size = new Size(340, 140);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = SystemColors.Control;
        Font = new Font("Microsoft Sans Serif", 8f);

        var lbl = new Label { Text = prompt, Location = new Point(8, 12), Size = new Size(314, 16) };
        _txt = new TextBox { Location = new Point(8, 34), Size = new Size(314, 20), BorderStyle = BorderStyle.Fixed3D, Text = defaultValue };
        _btnOK = new Button { Text = "OK", Location = new Point(166, 62), Size = new Size(70, 24), FlatStyle = FlatStyle.System, DialogResult = DialogResult.OK };
        _btnCancel = new Button { Text = "Cancel", Location = new Point(244, 62), Size = new Size(70, 24), FlatStyle = FlatStyle.System, DialogResult = DialogResult.Cancel };

        AcceptButton = _btnOK;
        CancelButton = _btnCancel;
        Controls.AddRange(new Control[] { lbl, _txt, _btnOK, _btnCancel });
    }

    /// <summary>
    /// Shows an input box and returns the entered string, or null if cancelled.
    /// </summary>
    public static string? Show(string prompt, string title, string defaultValue = "", IWin32Window? owner = null)
    {
        using var dlg = new InputBoxForm(prompt, title, defaultValue);
        return dlg.ShowDialog(owner) == DialogResult.OK ? dlg.Value : null;
    }
}
