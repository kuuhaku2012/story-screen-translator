using System.Drawing;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using StoryTranslator.Core;

namespace StoryTranslator.App;

public partial class MainWindow : Window
{
    private readonly TranslationOverlay _overlay;
    private readonly AppRuntimeServices _services;
    private readonly GroqRateLimiter _groqRateLimiter = new();
    private readonly HttpClient _http = new(new SocketsHttpHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(30) };
    private CaptureWorker? _worker;
    private SpatialTranslationPipeline? _pipeline;
    private CutsceneTimingRecorder? _timing;
    private CancellationTokenSource? _session;
    private CancellationTokenSource? _diagnosticCancellation;
    private Rectangle? _selectedRegion;
    private bool _closing;

    public MainWindow() : this(new TranslationOverlay(), AppComposition.Create(@"E:\tool_dich_man_hinh\data")) { }

    internal MainWindow(TranslationOverlay overlay, AppRuntimeServices services)
    {
        InitializeComponent();
        _overlay = overlay;
        _services = services;
        _selectedRegion = services.SettingsStore.Load().CaptureRegion;
        if (_selectedRegion is { } region) RegionText.Text = $"{region.Width} × {region.Height} px";
        StateChanged += (_, _) =>
        {
            if (_session is not null && WindowState != WindowState.Minimized) PauseForEditing();
        };
        Closing += (_, _) =>
        {
            _closing = true;
            _diagnosticCancellation?.Cancel();
            TurnOff();
            ApiKeyBox.Clear();
            _overlay.Close();
            _http.Dispose();
        };
    }

    private async void SelectRegion_Click(object sender, RoutedEventArgs e)
    {
        TurnOff();
        Hide();
        await Task.Delay(180);
        try
        {
            var selector = new RegionSelectorWindow();
            if (selector.ShowDialog() == true && selector.SelectedRegion is { } region)
            {
                _selectedRegion = region;
                RegionText.Text = $"{region.Width} × {region.Height} px";
                var settings = _services.SettingsStore.Load();
                _services.SettingsStore.Save(settings with { CaptureRegion = region });
            }
        }
        catch { StatusText.Text = "Không lưu/chọn được vùng. Kiểm tra quyền ghi thư mục data và thử lại."; }
        finally { if (!_closing) { Show(); Activate(); } }
    }

    private async void Diagnostic_Click(object sender, RoutedEventArgs e)
    {
        if (_diagnosticCancellation is not null) return;
        if (_selectedRegion is not { } region) { StatusText.Text = "Hãy chọn vùng chữ cần kiểm tra trước."; return; }
        TurnOff();
        using var cancel = new CancellationTokenSource();
        _diagnosticCancellation = cancel;
        DiagnosticButton.IsEnabled = StoryModeCheckBox.IsEnabled = SelectRegionButton.IsEnabled = ComicModeCheckBox.IsEnabled = false;
        var comic = ComicModeCheckBox.IsChecked == true;
        try
        {
            if (_worker is not null) { await _worker.DisposeAsync(); _worker = null; }
            cancel.Token.ThrowIfCancellationRequested();
            Hide();
            await Task.Delay(250, cancel.Token);
            if (DwmFlush() < 0) throw new InvalidOperationException("Không đồng bộ được màn hình trước khi lưu ảnh.");
            using var frame = await new WindowsRegionCapture().CaptureAsync(region, cancel.Token);
            var engine = WindowsOcrEngine.CreateEnglish(comic);
            var start = System.Diagnostics.Stopwatch.GetTimestamp();
            var blocks = await engine.RecognizeLayoutAsync(frame, cancel.Token);
            var milliseconds = System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            using var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
            var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId, stream);
            encoder.SetSoftwareBitmap(((WindowsCapturedFrame)frame).SoftwareBitmap);
            await encoder.FlushAsync();
            cancel.Token.ThrowIfCancellationRequested();
            stream.Seek(0);
            using var reader = new Windows.Storage.Streams.DataReader(stream);
            if (stream.Size > int.MaxValue) throw new InvalidOperationException("Ảnh chẩn đoán quá lớn.");
            await reader.LoadAsync((uint)stream.Size);
            var bytes = new byte[(int)stream.Size];
            reader.ReadBytes(bytes);
            cancel.Token.ThrowIfCancellationRequested();
            var folder = OcrDiagnosticExport.Save(@"E:\tool_dich_man_hinh\artifacts\ocr-diagnostics", bytes, region, blocks, comic, milliseconds);
            OcrText.Text = string.Join("\n\n", blocks.Select(b => b.Text));
            StatusText.Text = $"Đã lưu ảnh và {blocks.Count} box tại {folder}. Không gửi API.";
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(System.IO.Path.Combine(folder, "preview.html")) { UseShellExecute = true });
        }
        catch (OperationCanceledException) { }
        catch { if (!_closing) StatusText.Text = "Không lưu được chẩn đoán OCR. Kiểm tra vùng chọn, MSIX và quyền ghi artifacts."; }
        finally
        {
            _diagnosticCancellation = null;
            if (!_closing)
            {
                DiagnosticButton.IsEnabled = StoryModeCheckBox.IsEnabled = SelectRegionButton.IsEnabled = ComicModeCheckBox.IsEnabled = true;
                Show(); Activate();
            }
        }
    }

    private async void StoryMode_Checked(object sender, RoutedEventArgs e)
    {
        StoryModeCheckBox.IsEnabled = false;
        ApiKeyBox.IsEnabled = ModelBox.IsEnabled = SelectRegionButton.IsEnabled = CutsceneModeCheckBox.IsEnabled = false;
        try
        {
            if (_selectedRegion is not { } region) throw new InvalidOperationException("Hãy chọn vùng chữ trước.");
            if (string.IsNullOrWhiteSpace(ApiKeyBox.Password)) throw new InvalidOperationException("Hãy nhập Groq API key trên cửa sổ này.");
            var cutscene = CutsceneModeCheckBox.IsChecked == true;
            var timing = cutscene ? new CutsceneTimingRecorder() : null;
            _timing = timing;
            var translator = new GroqTextTranslator(_http, ApiKeyBox.Password, ModelBox.Text, _groqRateLimiter,
                timing is null ? null : (stage, elapsed) => timing.Add(stage, elapsed));
            if (_worker is not null) await _worker.DisposeAsync();
            if (_closing) return;
            _overlay.SetRegion(region);
            _overlay.SetPanelOpacity(PanelOpacitySlider.Value);
            var session = new CancellationTokenSource();
            _session = session;
            var pipeline = new SpatialTranslationPipeline(translator, new GlossaryProtector(_services.GlossaryStore.Load()),
                PresentBlocks,
                error => QueueError(session, error is InvalidOperationException ? error.Message : "Không dịch được các vùng chữ. Kiểm tra mạng/model rồi bật lại."),
                cutsceneMode: cutscene,
                translationTiming: timing is null ? null : elapsed => timing.Add("subtitle-to-translation", elapsed));
            _pipeline = pipeline;
            // Upscaling also recovers small cutscene subtitles that standard Windows OCR misses.
            IFrameLayoutRecognizer recognizer = cutscene
                ? new CutsceneAdaptiveOcrEngine(WindowsOcrEngine.CreateEnglish(), WindowsOcrEngine.CreateEnglish(comicMode: true),
                    frame => new System.Drawing.Size(((WindowsCapturedFrame)frame).SoftwareBitmap.PixelWidth,
                        ((WindowsCapturedFrame)frame).SoftwareBitmap.PixelHeight),
                    timing is null ? null : (stage, elapsed) => timing.Add(stage, elapsed))
                : WindowsOcrEngine.CreateEnglish(ComicModeCheckBox.IsChecked == true);
            _worker = new CaptureWorker(new WindowsRegionCapture(VerifyCaptureUncoveredAsync, RestoreOverlayAfterCaptureAsync), recognizer,
                sampleInterval: cutscene ? TimeSpan.FromMilliseconds(250) :
                    (long)region.Width * region.Height > 1920L * 1080 ? TimeSpan.FromSeconds(1) : TimeSpan.FromMilliseconds(500),
                diagnostic: _ => QueueError(session, "Không chụp/OCR được vùng chọn. Chọn lại vùng, kiểm tra English OCR hoặc cách chạy MSIX."));
            _worker.LayoutRecognized += (sender, blocks) =>
            {
                var chosen = cutscene ? CutsceneSubtitleSelector.Select(blocks, region.Size) : blocks;
                _ = ProcessLayoutAsync(pipeline, session, chosen);
            };
            pipeline.Enable(session.Token);
            // Minimize before starting capture so the tool cannot enter the OCR frame.
            WindowState = WindowState.Minimized;
            _worker.Start(region);
            ApiKeyBox.IsEnabled = ModelBox.IsEnabled = SelectRegionButton.IsEnabled = false;
            OverlayButton.IsEnabled = true;
            StatusText.Text = "ĐANG BẬT — giao diện đã thu xuống taskbar. Mở lại cửa sổ để tạm dừng và chỉnh.";
        }
        catch (Exception exception)
        {
            TurnOff();
            StatusText.Text = exception is InvalidOperationException or ArgumentException ? exception.Message : "Không khởi động được chế độ dịch. Kiểm tra OCR/MSIX và vùng chọn.";
        }
        finally { if (!_closing) StoryModeCheckBox.IsEnabled = true; }
    }

    private async Task ProcessLayoutAsync(SpatialTranslationPipeline pipeline, CancellationTokenSource session, IReadOnlyList<ScreenTextBlock> blocks)
    {
        try
        {
            await Dispatcher.InvokeAsync(() => { if (ReferenceEquals(_session, session)) OcrText.Text = string.Join("\n\n", blocks.Select(b => b.Text)); });
            await pipeline.ObserveAsync(blocks);
        }
        catch (OperationCanceledException) { }
        catch (InvalidOperationException exception) { QueueError(session, exception.Message); }
        catch { QueueError(session, "Không kết nối/đọc được kết quả Groq. Kiểm tra mạng và thử bật lại."); }
    }

    private void QueueError(CancellationTokenSource session, string message)
    {
        if (_closing || Dispatcher.HasShutdownStarted) return;
        _ = Dispatcher.BeginInvoke(() =>
        {
            if (!ReferenceEquals(_session, session)) return;
            TurnOff();
            StatusText.Text = "ĐÃ DỪNG — " + message;
        });
    }

    private void PresentBlocks(IReadOnlyList<TranslatedScreenBlock> blocks, Func<bool> allowed)
    {
        if (!Dispatcher.CheckAccess()) { _ = Dispatcher.BeginInvoke(() => PresentBlocks(blocks, allowed)); return; }
        if (!allowed()) return;
        _overlay.ShowBlocks(blocks, allowed);
        TranslatedDetailsText.Text = string.Join("\n\n", blocks.Select(b => b.Text));
    }

    private void PanelOpacity_Changed(object sender, RoutedPropertyChangedEventArgs<double> e) => _overlay?.SetPanelOpacity(e.NewValue);

    private void StoryMode_Unchecked(object sender, RoutedEventArgs e) => TurnOff();

    private async Task VerifyCaptureUncoveredAsync(Rectangle region, CancellationToken token)
    {
        await Dispatcher.InvokeAsync(() =>
        {
            if (WindowState != WindowState.Minimized)
            {
                PauseForEditing();
                return;
            }
            _overlay.BeginCapture();
            if (DwmFlush() < 0) throw new InvalidOperationException("Không đồng bộ được màn hình trước khi OCR.");
        }, DispatcherPriority.Send, token);
        token.ThrowIfCancellationRequested();
    }

    private async Task RestoreOverlayAfterCaptureAsync()
    {
        if (_closing || Dispatcher.HasShutdownStarted) return;
        await Dispatcher.InvokeAsync(() => _overlay.EndCapture(), DispatcherPriority.Send);
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmFlush();

    private void PauseForEditing()
    {
        TurnOff();
        StatusText.Text = "TẠM DỪNG — bạn đã mở giao diện. Chỉnh vùng/model nếu cần, rồi bật dịch để giao diện tự thu xuống taskbar.";
    }

    private void TurnOff()
    {
        _worker?.Stop();
        _pipeline?.TurnOff();
        var timing = _timing;
        _timing = null;
        try { timing?.Save(@"E:\tool_dich_man_hinh\data\cutscene-timing"); } catch { }
        _overlay.HideOverlay();
        _session?.Cancel();
        // Keep the canceled source alive for in-flight callbacks; collected with its session.
        _session = null;
        StoryModeCheckBox.IsChecked = false;
        OverlayButton.IsEnabled = true;
        ApiKeyBox.IsEnabled = ModelBox.IsEnabled = SelectRegionButton.IsEnabled = CutsceneModeCheckBox.IsEnabled = true;
        StatusText.Text = "ĐANG TẮT — không chụp/OCR/gửi yêu cầu mới; bản dịch đã ẩn.";
        if (!_closing && WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
    }

    private void ShowOverlay_Click(object sender, RoutedEventArgs e)
    {
        if (_session is null && _selectedRegion is { } region)
        {
            _overlay.SetRegion(region);
            _overlay.ShowPlacement();
        }
        else if (_session is not null) _overlay.ShowCurrent();
        else StatusText.Text = "Hãy chọn vùng chữ trước.";
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();
}
