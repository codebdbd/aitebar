using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace AiteBar.Tests;

[Collection("WpfTestCollection")]
public sealed class QuickNoteWindowCloseTests
{
    [Theory]
    [InlineData("lemon")]
    [InlineData("lavender")]
    public async Task NativeChrome_ExposesAllResizeEdgesAndCaptionButKeepsButtonsInteractive(string themeId)
    {
        await RunStaAsync(async () =>
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            try
            {
                var settings = new AppSettingsService(Path.Combine(tempRoot, "buttons.json"), Path.Combine(tempRoot, "settings.json"));
                settings.UpdateSettings(s => { s.QuickNotePinned = true; s.QuickNoteThemeId = themeId; });
                using var window = new QuickNoteWindow(new ImmediateQuickNotePersistence(), settings)
                {
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = -2000, Top = -2000, Width = 460, Height = 320,
                    ShowActivated = false
                };
                var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                window.Closed += (_, _) => closed.TrySetResult();
                try
                {
                    window.Show();
                    await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                    window.UpdateLayout();
                    IntPtr handle = new WindowInteropHelper(window).Handle;
                    const int wsCaption = 0x00C00000, wsThickFrame = 0x00040000, wsMaximizeBox = 0x00010000;
                    int style = GetWindowLong(handle, -16);
                    Assert.Equal(wsCaption | wsThickFrame | wsMaximizeBox, style & (wsCaption | wsThickFrame | wsMaximizeBox));
                    Assert.Equal(0, GetWindowLong(handle, -20) & 0x00080000); // No layered window.
                    if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
                    {
                        Assert.Equal(0, DwmGetWindowAttribute(handle, 33, out int cornerPreference, sizeof(int)));
                        Assert.Equal(2, cornerPreference); // DWMWCP_ROUND is applied to the actual HWND.
                        IntPtr region = CreateRectRgn(0, 0, 0, 0);
                        Assert.NotEqual(IntPtr.Zero, region);
                        try { Assert.Equal(0, GetWindowRgn(handle, region)); } // No custom region blocking DWM.
                        finally { DeleteObject(region); }
                    }
                    double w = window.ActualWidth, h = window.ActualHeight;
                    Assert.Equal(10, HitTest(new Point(7, h / 2))); // HTLEFT: former overlay strip.
                    Assert.Equal(11, HitTest(new Point(w - 7, h / 2)));
                    Assert.Equal(12, HitTest(new Point(w / 2, 7)));
                    Assert.Equal(15, HitTest(new Point(w / 2, h - 7)));
                    Assert.Equal(13, HitTest(new Point(3, 3)));
                    Assert.Equal(14, HitTest(new Point(w - 3, 3)));
                    Assert.Equal(16, HitTest(new Point(3, h - 3)));
                    Assert.Equal(17, HitTest(new Point(w - 3, h - 3)));
                    Assert.Equal(2, HitTest(new Point(110, 20))); // HTCAPTION.
                    Assert.Equal(1, HitTest(new Point(110, 37))); // Editor begins at the client area.

                    var header = Assert.IsType<Border>(window.FindName("HeaderBar"));
                    var headerGrid = Assert.IsType<Grid>(header.Child);
                    Assert.Empty(headerGrid.Children.OfType<TextBlock>());
                    var pin = Assert.Single(headerGrid.Children.OfType<ToggleButton>());
                    var commands = Assert.Single(headerGrid.Children.OfType<StackPanel>());
                    ButtonBase[] buttons = commands.Children.OfType<ButtonBase>().Prepend(pin).ToArray();
                    Assert.Equal(5, buttons.Length);
                    foreach (ButtonBase button in buttons)
                    {
                        Assert.Equal(1, HitTest(button.TranslatePoint(new Point(button.ActualWidth / 2, button.ActualHeight / 2), window)));
                    }
                    Assert.True(window.IsPinned);
                    Assert.Equal(new Size(460, 320), window.RenderSize);
                    var bitmap = new RenderTargetBitmap(460, 320, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(window);
                    var pixel = new byte[4];
                    bitmap.CopyPixels(new Int32Rect(200, 14, 1, 1), pixel, 4, 0);
                    var expectedHeader = (Color)ColorConverter.ConvertFromString(
                        QuickNoteThemeCatalog.GetHeaderBackground(QuickNoteThemeCatalog.Find(themeId)));
                    Assert.Equal(new byte[] { expectedHeader.B, expectedHeader.G, expectedHeader.R, 255 }, pixel);
                    string? renderDirectory = Environment.GetEnvironmentVariable("AITEBAR_QUICKNOTE_RENDER_DIR");
                    if (!string.IsNullOrWhiteSpace(renderDirectory))
                    {
                        Directory.CreateDirectory(renderDirectory);
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(bitmap));
                        // RenderTargetBitmap excludes DWM's contour. Label it as a client render, not a desktop screenshot.
                        using var stream = File.Create(Path.Combine(renderDirectory, $"quicknote-client-{themeId}.png"));
                        encoder.Save(stream);
                        if (Environment.GetEnvironmentVariable("AITEBAR_QUICKNOTE_CAPTURE_DESKTOP") == "1")
                        {
                            await CaptureDesktopCornersAsync(window, themeId, renderDirectory);
                        }
                    }
                }
                finally
                {
                    window.Close();
                    await closed.Task.WaitAsync(TimeSpan.FromSeconds(5));
                }

                int HitTest(Point point)
                {
                    Point screen = window.PointToScreen(point);
                    int coordinates = (unchecked((ushort)(short)Math.Round(screen.Y)) << 16) |
                        unchecked((ushort)(short)Math.Round(screen.X));
                    return SendMessage(new WindowInteropHelper(window).Handle, 0x0084, IntPtr.Zero, new IntPtr(coordinates)).ToInt32();
                }
            }
            finally { Directory.Delete(tempRoot, recursive: true); }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Close_DrainsEditsMadeDuringNormalOrConflictSave(bool externalChange)
    {
        await RunStaAsync(async () =>
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            try
            {
                var persistence = new DelayedQuickNotePersistence { ExternalChange = externalChange };
                var settings = new AppSettingsService(Path.Combine(tempRoot, "buttons.json"), Path.Combine(tempRoot, "settings.json"));
                settings.UpdateSettings(s => s.QuickNotePinned = true);
                using var window = new QuickNoteWindow(persistence, settings);
                var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                window.Closed += (_, _) => closed.TrySetResult();
                window.Show();
                await Dispatcher.Yield(DispatcherPriority.Loaded);
                window.TxtNote.AppendText("first");
                Task saving = window.SaveNowAsync();
                await persistence.WaitForSaveCountAsync(1).WaitAsync(TimeSpan.FromSeconds(5));
                window.TxtNote.AppendText(" newest");
                window.Close();
                persistence.CompleteSave(0);
                await persistence.WaitForSaveCountAsync(2).WaitAsync(TimeSpan.FromSeconds(5));
                Assert.True(window.IsVisible);
                persistence.CompleteSave(1);
                await saving;
                await closed.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Contains("newest", persistence.SavedTexts[1]);
                Assert.Equal(2, persistence.SaveCount);
            }
            finally { Directory.Delete(tempRoot, recursive: true); }
        });
    }

    [Fact]
    public void ForcedSaveWaitTimeout_IsBounded()
    {
        Assert.InRange(
            QuickNoteWindow.ForcedSaveWaitTimeout,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(15));
    }

    [Fact]
    public async Task Close_WaitsForActiveSaveWithoutWritingUnchangedDocumentAgain()
    {
        await RunStaAsync(async () =>
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);

            try
            {
                var persistence = new DelayedQuickNotePersistence();
                var settingsService = new AppSettingsService(
                    Path.Combine(tempRoot, "buttons.json"),
                    Path.Combine(tempRoot, "settings.json"));
                settingsService.UpdateSettings(settings => settings.QuickNotePinned = true);
                var window = new QuickNoteWindow(persistence, settingsService);
                var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                window.Closed += (_, _) => closed.TrySetResult();
                window.Show();
                await Dispatcher.Yield(DispatcherPriority.Loaded);

                window.TxtNote.Document.Blocks.Clear();
                window.TxtNote.Document.Blocks.Add(new Paragraph(new Run("final smoke text")));
                Task firstSave = window.SaveNowAsync();
                await persistence.WaitForSaveCountAsync(1);

                window.Close();

                Assert.True(window.IsVisible);
                Assert.False(closed.Task.IsCompleted);

                persistence.CompleteSave(0);
                await firstSave;
                await closed.Task.WaitAsync(TimeSpan.FromSeconds(5));

                Assert.False(window.IsVisible);
                Assert.Equal(1, persistence.SaveCount);
            }
            finally
            {
                try
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
                catch
                {
                }
            }
        });
    }

    [Fact]
    public async Task Close_WhenFinalContentSaveFails_KeepsWindowOpenForRetry()
    {
        await RunStaAsync(async () =>
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);

            try
            {
                var persistence = new FailingOnceQuickNotePersistence();
                var settingsService = new AppSettingsService(
                    Path.Combine(tempRoot, "buttons.json"),
                    Path.Combine(tempRoot, "settings.json"));
                settingsService.UpdateSettings(settings => settings.QuickNotePinned = true);
                var window = new QuickNoteWindow(persistence, settingsService);
                var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                window.Closed += (_, _) => closed.TrySetResult();
                window.Show();
                await Dispatcher.Yield(DispatcherPriority.Loaded);

                window.TxtNote.Document.Blocks.Clear();
                window.TxtNote.Document.Blocks.Add(new Paragraph(new Run("must survive failed close")));

                window.Close();

                Assert.True(window.IsVisible);
                Assert.False(closed.Task.IsCompleted);
                Assert.Equal(1, persistence.SaveCount);

                window.Close();
                await closed.Task.WaitAsync(TimeSpan.FromSeconds(5));

                Assert.False(window.IsVisible);
                Assert.Equal(2, persistence.SaveCount);
            }
            finally
            {
                try
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
                catch
                {
                }
            }
        });
    }

    private static Task RunStaAsync(Func<Task> action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            _ = Dispatcher.CurrentDispatcher.InvokeAsync(async () =>
            {
                try
                {
                    await action();
                    completion.TrySetResult();
                }
                catch (Exception ex)
                {
                    completion.TrySetException(ex);
                }
                finally
                {
                    Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
                }
            });
            Dispatcher.Run();
        })
        {
            IsBackground = true
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    private static async Task CaptureDesktopCornersAsync(QuickNoteWindow window, string themeId, string directory)
    {
        // Opt-in interactive check: only synthetic test content and a solid backdrop enter the capture.
        Rect work = SystemParameters.WorkArea;
        var backdrop = new Window
        {
            Width = 520, Height = 380, WindowStartupLocation = WindowStartupLocation.Manual,
            Left = work.Left + (work.Width - 520) / 2, Top = work.Top + (work.Height - 380) / 2,
            WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize,
            Background = new SolidColorBrush(Color.FromRgb(0, 204, 102)),
            ShowActivated = false, ShowInTaskbar = false, Topmost = true
        };
        Window? previousOwner = window.Owner;
        double previousLeft = window.Left, previousTop = window.Top;
        try
        {
            backdrop.Show();
            window.Owner = backdrop;
            window.Left = backdrop.Left + 30;
            window.Top = backdrop.Top + 30;
            Assert.True(SetWindowPos(new WindowInteropHelper(window).Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0013));
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            await Task.Delay(350);
            Assert.Equal(0, DwmFlush());
            Assert.True(GetWindowRect(new WindowInteropHelper(window).Handle, out NativeRect bounds));
            int width = bounds.Right - bounds.Left, height = bounds.Bottom - bounds.Top;
            using var bitmap = new System.Drawing.Bitmap(width + 24, height + 24);
            using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(bounds.Left - 12, bounds.Top - 12, 0, 0, bitmap.Size);
            }
            bitmap.Save(Path.Combine(directory, $"quicknote-desktop-rounded-{themeId}.png"), System.Drawing.Imaging.ImageFormat.Png);
            foreach (var point in new[] { (12, 12), (width + 11, 12), (12, height + 11), (width + 11, height + 11) })
            {
                System.Drawing.Color pixel = bitmap.GetPixel(point.Item1, point.Item2);
                Assert.True(pixel.R < 80 && pixel.G > 100 && pixel.B < 150,
                    $"Expected backdrop through rounded corner at {point}, got {pixel}.");
            }
            System.Drawing.Color header = bitmap.GetPixel(12 + width / 2, 12 + 16);
            Assert.True(header.R > 180 && header.G > 150, $"Header not visible in desktop capture: {header}.");
        }
        finally
        {
            window.Owner = previousOwner;
            window.Left = previousLeft;
            window.Top = previousTop;
            backdrop.Close();
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }

    [DllImport("dwmapi.dll")]
    private static extern int DwmFlush();

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr handle, out NativeRect bounds);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr handle, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr handle, int index);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr handle, int attribute, out int value, int size);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);

    [DllImport("user32.dll")]
    private static extern int GetWindowRgn(IntPtr handle, IntPtr region);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr wParam, IntPtr lParam);

    private sealed class ImmediateQuickNotePersistence : IQuickNotePersistence
    {
        public string? LastConflictCopyPath => null;
        public bool HasExternalChanges() => false;
        public void Load(FlowDocument document)
        {
            document.Blocks.Clear();
            document.Blocks.Add(new Paragraph(new Run("Заметка без надписи в шапке")));
        }
        public Task SaveAsync(FlowDocument document) => Task.CompletedTask;
        public Task<string> SaveConflictCopyAsync(FlowDocument document) => Task.FromResult(string.Empty);
        public void OpenConflictCopy() { }
    }

    private sealed class DelayedQuickNotePersistence : IQuickNotePersistence
    {
        private readonly List<TaskCompletionSource> _saves = [];
        private readonly List<(int Count, TaskCompletionSource Completion)> _saveCountWaiters = [];

        public int SaveCount => _saves.Count;
        public bool ExternalChange { get; init; }
        public List<string> SavedTexts { get; } = [];
        public string? LastConflictCopyPath => null;
        public bool HasExternalChanges() => ExternalChange;
        public void Load(FlowDocument document) => document.Blocks.Clear();

        public Task SaveAsync(FlowDocument document)
        {
            SavedTexts.Add(new TextRange(document.ContentStart, document.ContentEnd).Text);
            var save = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _saves.Add(save);
            CompleteSatisfiedWaiters();
            return save.Task;
        }

        public async Task<string> SaveConflictCopyAsync(FlowDocument document)
        {
            await SaveAsync(document);
            return "QuickNote.conflict-test.aite-note";
        }

        public void OpenConflictCopy()
        {
        }

        public Task WaitForSaveCountAsync(int count)
        {
            if (_saves.Count >= count)
            {
                return Task.CompletedTask;
            }

            var waiter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _saveCountWaiters.Add((count, waiter));
            return waiter.Task;
        }

        public void CompleteSave(int index) => _saves[index].TrySetResult();

        private void CompleteSatisfiedWaiters()
        {
            for (int i = _saveCountWaiters.Count - 1; i >= 0; i--)
            {
                (int count, TaskCompletionSource completion) = _saveCountWaiters[i];
                if (_saves.Count < count)
                {
                    continue;
                }

                _saveCountWaiters.RemoveAt(i);
                completion.TrySetResult();
            }
        }
    }

    private sealed class FailingOnceQuickNotePersistence : IQuickNotePersistence
    {
        public int SaveCount { get; private set; }
        public string? LastConflictCopyPath => null;
        public bool HasExternalChanges() => false;
        public void Load(FlowDocument document) => document.Blocks.Clear();

        public Task SaveAsync(FlowDocument document)
        {
            SaveCount++;
            return SaveCount == 1
                ? Task.FromException(new IOException("Simulated note save failure."))
                : Task.CompletedTask;
        }

        public Task<string> SaveConflictCopyAsync(FlowDocument document) =>
            Task.FromResult(string.Empty);

        public void OpenConflictCopy()
        {
        }
    }
}
