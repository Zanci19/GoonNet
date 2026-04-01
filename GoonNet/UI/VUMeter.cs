using System;
using System.Drawing;
using System.Windows.Forms;

namespace GoonNet;

public class VUMeter : UserControl
{
    private double _level;
    private double _peak;
    private int _peakHoldCount;
    private const int Segments = 20;
    private const int SegmentHeight = 8;
    private const int SegmentGap = 2;
    private const int MeterWidth = 28;
    private readonly System.Windows.Forms.Timer _peakTimer;

    public VUMeter()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        Width = MeterWidth + 4;
        Height = (SegmentHeight + SegmentGap) * Segments + 4;
        BackColor = Color.Black;

        _peakTimer = new System.Windows.Forms.Timer { Interval = 80 };
        _peakTimer.Tick += PeakTimer_Tick;
        _peakTimer.Start();
    }

    public double Level
    {
        get => _level;
        set
        {
            _level = Math.Clamp(value, 0.0, 1.0);
            if (_level > _peak) { _peak = _level; _peakHoldCount = 15; }
            Invalidate();
        }
    }

    public void UpdateLevel(double level) => Level = level;

    private void PeakTimer_Tick(object? sender, EventArgs e)
    {
        if (_peakHoldCount > 0)
            _peakHoldCount--;
        else if (_peak > 0)
        {
            _peak = Math.Max(0, _peak - 0.02);
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.Clear(Color.Black);

        int litSegments = (int)(_level * Segments);
        int peakSegment = (int)(_peak * Segments);

        for (int i = 0; i < Segments; i++)
        {
            int y = Height - 2 - (i + 1) * (SegmentHeight + SegmentGap);
            var rect = new Rectangle(2, y, MeterWidth, SegmentHeight);
            Color segColor;
            if (i >= Segments - 3)
                segColor = i < litSegments ? Color.Red : Color.FromArgb(80, 0, 0);
            else if (i >= Segments - 7)
                segColor = i < litSegments ? Color.Yellow : Color.FromArgb(80, 80, 0);
            else
                segColor = i < litSegments ? Color.Lime : Color.FromArgb(0, 60, 0);
            using (var brush = new SolidBrush(segColor))
                g.FillRectangle(brush, rect);

            if (i == peakSegment - 1 && _peak > 0)
                g.FillRectangle(Brushes.White, rect);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _peakTimer.Dispose();
        base.Dispose(disposing);
    }
}
