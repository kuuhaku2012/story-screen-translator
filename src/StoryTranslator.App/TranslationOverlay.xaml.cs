using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using StoryTranslator.Core;
using Brushes = System.Windows.Media.Brushes;
using MediaColor = System.Windows.Media.Color;
using WpfSize = System.Windows.Size;

namespace StoryTranslator.App;

public partial class TranslationOverlay : Window, ITranslationOverlayVisibility
{
    private readonly OverlayCaptureState _state = new();
    private Rectangle? _region;
    private IReadOnlyList<TranslatedScreenBlock> _blocks = [];
    private double _panelOpacity = .95;
    public bool IsExcludedFromCapture { get; private set; }
    public int RenderedPanelCount => BlockCanvas.Children.Count;

    public TranslationOverlay()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            var style = GetWindowLongPtr(handle, -20).ToInt64();
            SetWindowLongPtr(handle, -20, new IntPtr(style | 0x20 | 0x08000000));
            // Windows 10 2004+: keep the translation visible on the monitor while
            // desktop capture reads the original pixels underneath our own window.
            IsExcludedFromCapture = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041) &&
                SetWindowDisplayAffinity(handle, 0x11) &&
                GetWindowDisplayAffinity(handle, out var affinity) && affinity == 0x11;
        };
        Hide();
    }

    public void SetRegion(Rectangle region)
    {
        if (region.Width <= 0 || region.Height <= 0) throw new ArgumentOutOfRangeException(nameof(region));
        HideOverlay();
        _blocks = [];
        BlockCanvas.Children.Clear();
        _region = region;
        _ = new WindowInteropHelper(this).EnsureHandle();
        PositionOnRegion();
    }

    public void SetPanelOpacity(double opacity)
    {
        _panelOpacity = Math.Clamp(opacity, .35, 1);
        RebuildPanels();
    }

    public void ShowBlocks(IReadOnlyList<TranslatedScreenBlock> blocks, Func<bool>? allowed = null)
    {
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.BeginInvoke(() => ShowBlocks(blocks, allowed));
            return;
        }
        if (allowed is not null && !allowed()) return;
        _blocks = blocks.ToArray();
        if (_blocks.Count == 0) _state.Hide();
        else _state.RequestShow(allowed);
        RebuildPanels();
        ReconcileVisibility();
    }

    public void ShowTranslation(string translation) => _ = TryShowTranslation(translation);
    public bool TryShowTranslation(string translation, Func<bool>? allowed = null)
    {
        if (!Dispatcher.CheckAccess()) { _ = Dispatcher.BeginInvoke(() => TryShowTranslation(translation, allowed)); return true; }
        if (allowed is not null && !allowed()) return false;
        if (_region is not { } region) return false;
        ShowBlocks([new TranslatedScreenBlock(translation, new RectangleF(16, 16, Math.Max(1, region.Width - 32), Math.Min(70, region.Height - 16)), 20)], allowed);
        return true;
    }

    public void HideOverlay() { _state.Hide(); Hide(); }
    public void BeginCapture()
    {
        if (IsExcludedFromCapture) return;
        _state.BeginCapture(); Hide();
    }
    public void EndCapture()
    {
        if (IsExcludedFromCapture) return;
        _state.EndCapture(); ReconcileVisibility();
    }
    public void ShowCurrent() => ReconcileVisibility();
    public void ShowPlacement() => TryShowTranslation("XEM TRƯỚC VÙNG — chưa dịch. Bật dịch trong giao diện để bắt đầu.");

    private void ReconcileVisibility()
    {
        if (!_state.ShouldShow || _region is null) { Hide(); return; }
        if (!IsVisible) Show();
        PositionOnRegion();
    }

    private void PositionOnRegion()
    {
        if (_region is not { } region) return;
        if (!SetWindowPos(new WindowInteropHelper(this).Handle, new IntPtr(-1), region.X, region.Y, region.Width, region.Height, 0x0010))
            throw new InvalidOperationException("Không đặt được bản dịch tại vùng chọn.");
    }

    private void RebuildPanels()
    {
        if (BlockCanvas is null || _region is not { } region || ActualWidth <= 0 || ActualHeight <= 0) return;
        BlockCanvas.Children.Clear();
        var placements = SpatialPanelLayout.Arrange(_blocks, new SizeF(region.Width, region.Height));
        var sx = ActualWidth / region.Width;
        var sy = ActualHeight / region.Height;
        for (var i = 0; i < _blocks.Count; i++)
        {
            var box = placements[i];
            var width = box.Width * sx;
            var height = box.Height * sy;
            if (width < 12 || height < 12) continue;
            var text = new TextBlock
            {
                Text = _blocks[i].Text, Foreground = Brushes.White, FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                FontWeight = FontWeights.SemiBold, FontSize = Math.Clamp(_blocks[i].LineHeight * sy, 14, 22),
                TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.None,
                Width = Math.Max(1, width - 10)
            };
            TextOptions.SetTextFormattingMode(text, TextFormattingMode.Display);
            while (text.FontSize > 11)
            {
                text.Measure(new WpfSize(text.Width, double.PositiveInfinity));
                if (text.DesiredSize.Height <= height - 8) break;
                text.FontSize -= .5;
            }
            text.Measure(new WpfSize(text.Width, double.PositiveInfinity));
            var overflow = text.DesiredSize.Height > height - 8 || text.DesiredSize.Width > text.Width;
            if (overflow)
            {
                // A visible marker is preferable to presenting a clipped fragment as a complete translation.
                text.Text = "…";
                text.FontSize = 11;
                text.Foreground = Brushes.Gold;
                text.Width = Math.Max(1, width - 4);
                text.TextAlignment = TextAlignment.Center;
            }
            text.MaxHeight = Math.Max(1, height - (overflow ? 2 : 8));
            var card = new Border
            {
                Width = width, Height = height, Padding = overflow ? new Thickness(1, 0, 1, 0) : new Thickness(4, 3, 4, 3), CornerRadius = new CornerRadius(3),
                Background = new SolidColorBrush(MediaColor.FromArgb((byte)(_panelOpacity * 255), 20, 24, 32)),
                BorderBrush = overflow ? Brushes.Gold : new SolidColorBrush(MediaColor.FromArgb(100, 180, 195, 210)), BorderThickness = new Thickness(.5),
                Child = text, ClipToBounds = true
            };
            Canvas.SetLeft(card, box.X * sx);
            Canvas.SetTop(card, box.Y * sy);
            BlockCanvas.Children.Add(card);
        }
    }

    private void Overlay_SizeChanged(object sender, SizeChangedEventArgs e) => RebuildPanels();
    void ITranslationOverlayVisibility.Show() => ShowCurrent();
    bool ITranslationOverlayVisibility.TryShowTranslation(string text, Func<bool> allowed) => TryShowTranslation(text, allowed);
    void ITranslationOverlayVisibility.Hide() => HideOverlay();

    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowDisplayAffinity(IntPtr window, uint affinity);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowDisplayAffinity(IntPtr window, out uint affinity);

    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)] private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)] private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);
}
