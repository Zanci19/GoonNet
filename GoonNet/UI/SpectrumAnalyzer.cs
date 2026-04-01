using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using NAudio.Dsp;

namespace GoonNet;

/// <summary>
/// Large, full-width FFT spectrum analyzer control with smooth peak-hold animation.
/// </summary>
public class SpectrumAnalyzer : UserControl
{
    private const int BarCount = 64;
    private float[] _fftMagnitudes = Array.Empty<float>();
    private readonly float[] _barHeights = new float[BarCount];
    private readonly float[] _peakHeights = new float[BarCount];
    private readonly int[] _peakHoldCounters = new int[BarCount];

    private const float DecayRate = 0.055f;
    private const float PeakDecayRate = 0.015f;
    private const int PeakHoldFrames = 18;

    private readonly System.Windows.Forms.Timer _animTimer;
    private bool _hasData;

    public SpectrumAnalyzer()
    {
        SetStyle(
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw, true);
        BackColor = Color.Black;
        MinimumSize = new Size(200, 80);

        _animTimer = new System.Windows.Forms.Timer { Interval = 33 }; // ~30 fps
        _animTimer.Tick += AnimTimer_Tick;
        _animTimer.Start();
    }

    /// <summary>Update the spectrum with new FFT data from the audio pipeline.</summary>
    public void UpdateFft(Complex[] fftData)
    {
        if (fftData == null || fftData.Length == 0) return;
        int half = fftData.Length / 2;
        _fftMagnitudes = new float[half];
        for (int i = 0; i < half; i++)
        {
            float re = fftData[i].X;
            float im = fftData[i].Y;
            _fftMagnitudes[i] = MathF.Sqrt(re * re + im * im);
        }
        _hasData = true;
    }

    /// <summary>Reset all bars to zero (called when playback stops).</summary>
    public void Reset()
    {
        _hasData = false;
        _fftMagnitudes = Array.Empty<float>();
        Array.Clear(_barHeights, 0, _barHeights.Length);
        Array.Clear(_peakHeights, 0, _peakHeights.Length);
        Array.Clear(_peakHoldCounters, 0, _peakHoldCounters.Length);
        Invalidate();
    }

    private void AnimTimer_Tick(object? sender, EventArgs e)
    {
        if (_hasData && _fftMagnitudes.Length > 0)
        {
            int binCount = _fftMagnitudes.Length;
            for (int bar = 0; bar < BarCount; bar++)
            {
                // Logarithmic frequency mapping: map bar to bin range
                double logStart = Math.Pow(10.0, (double)bar / BarCount * Math.Log10(binCount));
                double logEnd = Math.Pow(10.0, (double)(bar + 1) / BarCount * Math.Log10(binCount));
                int binStart = Math.Clamp((int)logStart, 0, binCount - 1);
                int binEnd = Math.Clamp((int)logEnd, binStart, binCount - 1);

                float peak = 0f;
                for (int b = binStart; b <= binEnd; b++)
                    peak = Math.Max(peak, _fftMagnitudes[b]);

                // Amplify for visual impact and clamp
                float target = Math.Clamp(peak * 6.0f, 0f, 1f);

                if (target > _barHeights[bar])
                    _barHeights[bar] = target;
                else
                    _barHeights[bar] = Math.Max(0f, _barHeights[bar] - DecayRate);

                // Peak hold
                if (_barHeights[bar] >= _peakHeights[bar])
                {
                    _peakHeights[bar] = _barHeights[bar];
                    _peakHoldCounters[bar] = PeakHoldFrames;
                }
                else
                {
                    if (_peakHoldCounters[bar] > 0)
                        _peakHoldCounters[bar]--;
                    else
                        _peakHeights[bar] = Math.Max(0f, _peakHeights[bar] - PeakDecayRate);
                }
            }
        }
        else
        {
            // Decay all bars
            bool anyActive = false;
            for (int bar = 0; bar < BarCount; bar++)
            {
                _barHeights[bar] = Math.Max(0f, _barHeights[bar] - DecayRate * 2f);
                if (_peakHoldCounters[bar] > 0) _peakHoldCounters[bar]--;
                else _peakHeights[bar] = Math.Max(0f, _peakHeights[bar] - PeakDecayRate * 2f);
                if (_barHeights[bar] > 0f || _peakHeights[bar] > 0f) anyActive = true;
            }
            if (!anyActive) return; // Skip repaint when all bars are at zero
        }
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.None;
        g.Clear(Color.Black);

        if (Width < 10 || Height < 10) return;

        const int labelHeight = 16;
        int analyzerHeight = Height - labelHeight - 2;
        if (analyzerHeight < 10) return;

        int totalBarWidth = Width / BarCount;
        int barWidth = Math.Max(1, totalBarWidth - 1);
        int gap = totalBarWidth - barWidth;
        int startX = (Width - BarCount * totalBarWidth) / 2;

        // Subtle grid lines
        using (var gridPen = new Pen(Color.FromArgb(30, 0, 180, 0)))
        {
            for (int lvl = 1; lvl < 4; lvl++)
                g.DrawLine(gridPen, 0, analyzerHeight * lvl / 4, Width, analyzerHeight * lvl / 4);
        }

        // dB level markers
        using var markerFont = new Font("Microsoft Sans Serif", 6f);
        using var markerBrush = new SolidBrush(Color.FromArgb(60, 0, 220, 0));
        string[] dbLabels = { "0 dB", "-6", "-12", "-18" };
        for (int lvl = 0; lvl < 4; lvl++)
        {
            int y = analyzerHeight * lvl / 4;
            g.DrawString(dbLabels[lvl], markerFont, markerBrush, 2, y + 1);
        }

        // Draw bars
        for (int bar = 0; bar < BarCount; bar++)
        {
            int x = startX + bar * totalBarWidth;
            int barH = (int)(_barHeights[bar] * analyzerHeight);
            int y = analyzerHeight - barH;

            if (barH > 0)
            {
                var rect = new Rectangle(x, y, barWidth, barH);
                try
                {
                    using var brush = new LinearGradientBrush(
                        new Rectangle(x, 0, barWidth, analyzerHeight + 1),
                        Color.FromArgb(230, 220, 0, 0),   // red at top
                        Color.FromArgb(230, 0, 220, 0),   // green at bottom
                        LinearGradientMode.Vertical);

                    var blend = new ColorBlend(3)
                    {
                        Colors = new[] { Color.FromArgb(230, 220, 0, 0), Color.FromArgb(230, 220, 200, 0), Color.FromArgb(230, 0, 220, 0) },
                        Positions = new[] { 0f, 0.35f, 1f }
                    };
                    brush.InterpolationColors = blend;
                    g.FillRectangle(brush, rect);
                }
                catch
                {
                    // Fallback to solid color if gradient brush creation fails
                    // (can happen with zero-height rectangles in edge cases)
                    g.FillRectangle(Brushes.Lime, rect);
                }
            }

            // Peak indicator (white line)
            if (_peakHeights[bar] > 0.01f)
            {
                int peakY = Math.Clamp(analyzerHeight - (int)(_peakHeights[bar] * analyzerHeight), 0, analyzerHeight - 2);
                using var peakPen = new Pen(Color.FromArgb(200, 255, 255, 255), 2f);
                g.DrawLine(peakPen, x, peakY, x + barWidth - 1, peakY);
            }
        }

        // Frequency axis labels
        using var labelFont = new Font("Microsoft Sans Serif", 6.5f);
        using var labelBrush = new SolidBrush(Color.FromArgb(140, 140, 140));
        var freqLabels = new (string text, float frac)[]
        {
            ("20Hz",  0.00f), ("50Hz",  0.09f), ("100Hz", 0.18f),
            ("200Hz", 0.27f), ("500Hz", 0.40f), ("1kHz",  0.53f),
            ("2kHz",  0.64f), ("5kHz",  0.76f), ("10kHz", 0.87f), ("20kHz", 0.98f)
        };
        foreach (var (text, frac) in freqLabels)
        {
            int lx = Math.Clamp((int)(frac * Width) - 10, 0, Width - 30);
            g.DrawString(text, labelFont, labelBrush, lx, analyzerHeight + 2);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _animTimer.Dispose();
        base.Dispose(disposing);
    }
}
