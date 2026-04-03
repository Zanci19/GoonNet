using System;
using System.Drawing;
using System.Windows.Forms;

namespace GoonNet;

public class QueuePointEditorForm : Form
{
    private readonly MusicTrack _track;
    private Panel _wavePanel = null!;
    private TrackBar _tbHotStart = null!;
    private TrackBar _tbIntro = null!;
    private TrackBar _tbMixIn = null!;
    private TrackBar _tbVoiceOut = null!;

    public QueuePointEditorForm(MusicTrack track)
    {
        _track = track;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = $"Queue Points — {_track.Artist} - {_track.Title}";
        Size = new Size(700, 380);
        StartPosition = FormStartPosition.CenterParent;

        _wavePanel = new Panel { Dock = DockStyle.Top, Height = 130, BackColor = Color.FromArgb(15, 20, 30) };
        _wavePanel.Paint += WavePanel_Paint;

        int maxSec = Math.Max(30, (int)Math.Ceiling((_track.Duration > TimeSpan.Zero ? _track.Duration : TimeSpan.FromMinutes(4)).TotalSeconds));
        _tbHotStart = BuildTrackBar("Hot Start", 140, maxSec, (int)_track.HotStart.TotalSeconds);
        _tbIntro = BuildTrackBar("Intro", 185, maxSec, (int)_track.Intro.TotalSeconds);
        _tbMixIn = BuildTrackBar("Mix In", 230, maxSec, (int)_track.MixIn.TotalSeconds);
        _tbVoiceOut = BuildTrackBar("Voice Out", 275, maxSec, (int)_track.VoiceOut.TotalSeconds);

        var btnOk = new Button { Text = "OK", Location = new Point(500, 315), Size = new Size(80, 24), DialogResult = DialogResult.OK };
        btnOk.Click += (s, e) =>
        {
            _track.HotStart = TimeSpan.FromSeconds(_tbHotStart.Value);
            _track.Intro = TimeSpan.FromSeconds(_tbIntro.Value);
            _track.MixIn = TimeSpan.FromSeconds(_tbMixIn.Value);
            _track.VoiceOut = TimeSpan.FromSeconds(_tbVoiceOut.Value);
        };
        var btnCancel = new Button { Text = "Cancel", Location = new Point(590, 315), Size = new Size(80, 24), DialogResult = DialogResult.Cancel };

        Controls.AddRange(new Control[] { _wavePanel, _tbHotStart, _tbIntro, _tbMixIn, _tbVoiceOut, btnOk, btnCancel });
    }

    private TrackBar BuildTrackBar(string label, int top, int max, int value)
    {
        Controls.Add(new Label { Text = label, Location = new Point(10, top + 4), Size = new Size(70, 18) });
        var tb = new TrackBar
        {
            Location = new Point(90, top),
            Width = 580,
            Minimum = 0,
            Maximum = max,
            TickFrequency = Math.Max(5, max / 12),
            Value = Math.Clamp(value, 0, max)
        };
        tb.ValueChanged += (s, e) => _wavePanel.Invalidate();
        return tb;
    }

    private void WavePanel_Paint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(_wavePanel.BackColor);
        int w = _wavePanel.ClientSize.Width;
        int h = _wavePanel.ClientSize.Height;

        using var wavePen = new Pen(Color.FromArgb(80, 160, 220), 2f);
        for (int x = 0; x < w; x += 4)
        {
            double t = x / (double)w;
            int amp = (int)(10 + 40 * Math.Abs(Math.Sin(t * 15)));
            g.DrawLine(wavePen, x, h / 2 - amp / 2, x, h / 2 + amp / 2);
        }

        DrawMarker(g, _tbHotStart, Color.Lime, "Hot");
        DrawMarker(g, _tbIntro, Color.Cyan, "Intro");
        DrawMarker(g, _tbMixIn, Color.Yellow, "MixIn");
        DrawMarker(g, _tbVoiceOut, Color.OrangeRed, "VoiceOut");
    }

    private void DrawMarker(Graphics g, TrackBar tb, Color color, string text)
    {
        int max = Math.Max(1, tb.Maximum);
        int x = (int)((tb.Value / (double)max) * (_wavePanel.ClientSize.Width - 1));
        using var p = new Pen(color, 2f);
        g.DrawLine(p, x, 0, x, _wavePanel.ClientSize.Height);
        g.DrawString(text, Font, new SolidBrush(color), new PointF(Math.Min(x + 3, _wavePanel.ClientSize.Width - 60), 4));
    }
}
