using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace AiteBar.Tests;

[Collection("WpfTestCollection")]
public sealed class QuickNoteSnapIntegrationTests
{
    [InteractiveSnapFact]
    public Task NativeForms_SharedResize_CapturesShellPreview() => QuickNoteWindowCloseTests.RunStaAsync(async () =>
    {
        IntPtr foreground = GetForegroundWindow();
        GetCursorPos(out NativePoint cursor);
        using var left = new System.Windows.Forms.Form { Text = "Native WinForms reference A", Width = 600, Height = 400, Left = 100, Top = 100, StartPosition = System.Windows.Forms.FormStartPosition.Manual, BackColor = System.Drawing.Color.LightYellow };
        using var right = new System.Windows.Forms.Form { Text = "Native WinForms reference B", Width = 600, Height = 400, Left = 100, Top = 100, StartPosition = System.Windows.Forms.FormStartPosition.Manual, BackColor = System.Drawing.Color.LightBlue };
        left.Controls.Add(new System.Windows.Forms.Label { Text = "Native GDI content A", AutoSize = true, Left = 24, Top = 24 });
        right.Controls.Add(new System.Windows.Forms.Label { Text = "Native GDI content B", AutoSize = true, Left = 24, Top = 24 });
        try
        {
            left.Show(); right.Show();
            await SnapAsync(right.Handle, false);
            await SnapAsync(left.Handle, true);
            NativeRect a = Bounds(left.Handle), b = Bounds(right.Handle);
            int x = (a.Right + b.Left) / 2, y = (a.Top + a.Bottom) / 2;
            CaptureResizeFrame(left.Handle, right.Handle, false, true, "native-before");
            SetCursorPos(x, y);
            await Task.Delay(650);
            SendMouseButton(0x0002);
            try
            {
                await Task.Delay(200);
                for (int step = 1; step <= 12; step++)
                {
                    Assert.True(SetCursorPos(x + step * 10, y));
                    await Task.Delay(35);
                    if (step is 6 or 12) CaptureResizeFrame(left.Handle, right.Handle, false, true, $"native-step-{step}");
                }
            }
            finally { SendMouseButton(0x0004); }
            await Task.Delay(500);
            CaptureResizeFrame(left.Handle, right.Handle, false, true, "native-after");
            Assert.True(Bounds(left.Handle).Width > a.Width + 40);
            Assert.True(Bounds(right.Handle).Width < b.Width - 40);
        }
        finally
        {
            left.Close(); right.Close();
            SetCursorPos(cursor.X, cursor.Y);
            if (foreground != IntPtr.Zero) SetForegroundWindow(foreground);
        }
    });

    [InteractiveSnapTheory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public Task UnpinnedNote_SnapKeepsVisible_ThenOrdinaryDepartureDismisses(bool left, bool keyboard) =>
        QuickNoteWindowCloseTests.RunStaAsync(async () =>
        {
            IntPtr foreground = GetForegroundWindow();
            GetCursorPos(out NativePoint cursor);
            string temp = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            var trace = new System.Text.StringBuilder();
            var settings = new AppSettingsService(Path.Combine(temp, "buttons.json"), Path.Combine(temp, "settings.json"));
            settings.UpdateSettings(s => s.QuickNotePinned = false);
            using var note = new QuickNoteWindow(new MemoryNote(), settings)
            {
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = left ? 400 : SystemParameters.WorkArea.Right - 800,
                Top = 240, Width = 460, Height = 320
            };
            var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            note.Closed += (_, _) => { trace.AppendLine("CLOSED"); closed.TrySetResult(); };
            try
            {
                note.Show();
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                var handle = new WindowInteropHelper(note).Handle;
                HwndSource.FromHwnd(handle).AddHook((IntPtr hwnd, int msg, IntPtr wp, IntPtr lp, ref bool handled) =>
                {
                    if (msg is 0x0006 or 0x001C or 0x0231 or 0x0232)
                    {
                        var name = new System.Text.StringBuilder(256);
                        GetClassName(GetForegroundWindow(), name, name.Capacity);
                        GetWindowThreadProcessId(GetForegroundWindow(), out uint pid);
                        GetWindowThreadProcessId(GetShellWindow(), out uint shellPid);
                        trace.AppendLine($"{Environment.TickCount64}: msg={msg:X} wp={wp} foreground={name} visible={note.IsVisible} style={GetWindowLong(GetForegroundWindow(), -16):X} ex={GetWindowLong(GetForegroundWindow(), -20):X} pid={pid} shell={shellPid} arranged={IsWindowArranged(hwnd)}");
                        if (pid != 0 && name.ToString() == "#32770")
                        {
                            try
                            {
                                using var process = System.Diagnostics.Process.GetProcessById((int)pid);
                                trace.AppendLine($"Interrupting dialog process: {process.ProcessName}");
                            }
                            catch (ArgumentException) { }
                        }
                    }
                    return IntPtr.Zero;
                });
                var monitor = System.Windows.Forms.Screen.FromHandle(handle).WorkingArea;
                if (keyboard)
                {
                    await SnapAsync(note, left);
                }
                else
                {
                    int targetX = left ? monitor.Left : monitor.Right - 1;
                    int targetY = monitor.Top + monitor.Height / 2;
                    for (int attempt = 1; attempt <= 3 && !IsWindowArranged(handle); attempt++)
                    {
                        await ActivateByClickAsync(note);
                        Point start = note.PointToScreen(new Point(110, 20));
                        Assert.True(SetCursorPos((int)start.X, (int)start.Y));
                        Assert.Equal(handle, WindowFromPoint(new NativePoint { X = (int)start.X, Y = (int)start.Y }));
                        SendMouseButton(0x0002);
                        try
                        {
                            await Task.Delay(150); // Let the nonclient button-down enter the native move loop.
                            for (int step = 1; step <= 20; step++)
                            {
                                MoveMouseTo((int)(start.X + (targetX - start.X) * step / 20),
                                    (int)(start.Y + (targetY - start.Y) * step / 20));
                                await Task.Delay(25);
                            }
                            await Task.Delay(500);
                            GetCursorPos(out NativePoint actual);
                            trace.AppendLine($"Attempt={attempt}; target={targetX},{targetY}; cursor={actual.X},{actual.Y}");
                        }
                        finally { SendMouseButton(0x0004); }
                        await Task.Delay(800);
                    }
                }
                await Task.Delay(1200);
                Assert.True(note.IsVisible, trace.ToString());
                Assert.False(note.IsPinned); // No silent pin/preference changes to mask the bug.
                Assert.False(settings.Settings.QuickNotePinned);
                Assert.True(IsWindowArranged(handle), trace.ToString());
                NativeRect bounds = Bounds(note);
                trace.AppendLine($"Snapped: {bounds}");
                Assert.InRange(Math.Abs(bounds.Width - monitor.Width / 2), 0, 20);
                Assert.InRange(Math.Abs((left ? bounds.Left : bounds.Right) - (left ? monitor.Left : monitor.Right)), 0, 8);

                // Returning to the note and then leaving for a regular app must still dismiss it.
                await ActivateByClickAsync(note);
                var other = CreateReference("Ordinary departure verification");
                try
                {
                    other.Show();
                    await ActivateByClickAsync(other);
                    await closed.Task.WaitAsync(TimeSpan.FromSeconds(5));
                    Assert.False(note.IsVisible);
                    trace.AppendLine("Ordinary departure dismissed the unpinned note.");
                }
                finally { other.Close(); }
            }
            finally
            {
                if (!closed.Task.IsCompleted) { note.Close(); await closed.Task.WaitAsync(TimeSpan.FromSeconds(10)); }
                string? directory = Environment.GetEnvironmentVariable("AITEBAR_QUICKNOTE_RENDER_DIR");
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                    File.WriteAllText(Path.Combine(directory, $"snap-unpinned-{left}-{keyboard}.txt"), trace.ToString());
                }
                SetCursorPos(cursor.X, cursor.Y);
                if (foreground != IntPtr.Zero) SetForegroundWindow(foreground);
                Directory.Delete(temp, true);
            }
        });

    [InteractiveSnapTheory]
    [InlineData(false, true)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    public Task SharedBoundary_ResizesBothWindows(bool useQuickNote, bool subjectOnLeft) =>
        RunSharedBoundaryAsync(useQuickNote, subjectOnLeft, resizeSubject: false);

    [InteractiveSnapTheory]
    [InlineData(true)]
    [InlineData(false)]
    public Task NoteEdge_KeepsNoteContentVisibleDuringSharedResize(bool subjectOnLeft) =>
        RunSharedBoundaryAsync(useQuickNote: true, subjectOnLeft, resizeSubject: true);

    [InteractiveSnapTheory]
    [InlineData(true)]
    [InlineData(false)]
    public Task InactiveNoteEdge_ResizesWithVisibleTextWithoutPreclick(bool subjectOnLeft) =>
        RunSharedBoundaryAsync(useQuickNote: true, subjectOnLeft, resizeSubject: true, directEdge: true);

    private static Task RunSharedBoundaryAsync(bool useQuickNote, bool subjectOnLeft, bool resizeSubject, bool directEdge = false) =>
        QuickNoteWindowCloseTests.RunStaAsync(async () =>
        {
            IntPtr foreground = GetForegroundWindow();
            GetCursorPos(out NativePoint cursor);
            string temp = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            Window? subject = null, peer = null;
            var messages = new System.Text.StringBuilder();
            try
            {
                var settings = new AppSettingsService(Path.Combine(temp, "buttons.json"), Path.Combine(temp, "settings.json"));
                settings.UpdateSettings(s => s.QuickNotePinned = true);
                subject = useQuickNote ? new QuickNoteWindow(new MemoryNote(), settings) : CreateReference("Snap reference A");
                peer = CreateReference("Snap reference B");
                Rect work = SystemParameters.WorkArea;
                foreach (Window window in new[] { subject, peer })
                {
                    window.WindowStartupLocation = WindowStartupLocation.Manual;
                    window.Left = work.Left + 100;
                    window.Top = work.Top + 100;
                    window.Width = 600;
                    window.Height = 400;
                    window.Show();
                }
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                // Build the same right-then-left arrangement for both the note and the reference pair.
                if (directEdge)
                    HwndSource.FromHwnd(new WindowInteropHelper(subject).Handle).AddHook((IntPtr hwnd, int msg, IntPtr wp, IntPtr lp, ref bool handled) =>
                    {
                        if (msg is 0x0021 or 0x00A1 or 0x0231 or 0x0232 or 0x0006)
                            messages.AppendLine($"msg={msg:X}; wp={wp}; lp={lp}; fg={GetForegroundWindow()}");
                        return IntPtr.Zero;
                    });
                await SnapAsync(subjectOnLeft ? peer : subject, false);
                await SnapAsync(subjectOnLeft ? subject : peer, true);
                if (resizeSubject)
                {
                    Window activate = directEdge ? peer : subject;
                    NativeRect visible = Bounds(activate);
                    Assert.True(SetCursorPos((visible.Left + visible.Right) / 2, (visible.Top + visible.Bottom) / 2));
                    SendMouseButton(0x0002);
                    SendMouseButton(0x0004);
                    await Task.Delay(200);
                    Assert.Equal(new WindowInteropHelper(activate).Handle, GetForegroundWindow());
                }
                NativeRect beforeSubject = Bounds(subject), beforePeer = Bounds(peer);
                Assert.InRange(Math.Abs(beforeSubject.Width - beforePeer.Width), 0, 30);
                int shared = subjectOnLeft ? beforeSubject.Right : beforeSubject.Left;
                int other = subjectOnLeft ? beforePeer.Left : beforePeer.Right;
                Assert.InRange(Math.Abs(shared - other), 0, 16);
                int inset = directEdge ? 9 : 3;
                int x = (shared + other) / 2 + (resizeSubject ? (subjectOnLeft ? -inset : inset) : 0);
                int y = (beforeSubject.Top + beforeSubject.Bottom) / 2;
                CaptureResizeFrame(subject, peer, useQuickNote, subjectOnLeft, "before");
                if (resizeSubject) AssertNoteTextVisible(subject);
                var inputEvidence = new System.Text.StringBuilder();
                NativeRect afterSubject = beforeSubject, afterPeer = beforePeer;
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    NativeRect currentSubject = Bounds(subject), currentPeer = Bounds(peer);
                    int currentShared = subjectOnLeft ? currentSubject.Right : currentSubject.Left;
                    int currentOther = subjectOnLeft ? currentPeer.Left : currentPeer.Right;
                    x = (currentShared + currentOther) / 2 + (resizeSubject ? (subjectOnLeft ? -inset : inset) : 0);
                    Assert.True(SetCursorPos(x, y));
                    await Task.Delay(650); // Let the shell expose the shared resize handle.
                    IntPtr hitWindow = WindowFromPoint(new NativePoint { X = x, Y = y });
                    IntPtr packedPoint = new IntPtr((y << 16) | (x & 0xFFFF));
                    inputEvidence.AppendLine($"Attempt={attempt}; cursor={x},{y}; window={hitWindow}; foreground={GetForegroundWindow()}; subject={new WindowInteropHelper(subject).Handle}; peer={new WindowInteropHelper(peer).Handle}; hit={SendMessage(hitWindow, 0x84, IntPtr.Zero, packedPoint)}");
                    if (directEdge)
                    {
                        Assert.Equal(new WindowInteropHelper(subject).Handle, hitWindow);
                        Assert.Equal(new IntPtr(subjectOnLeft ? 11 : 10), SendMessage(hitWindow, 0x84, IntPtr.Zero, packedPoint));
                    }
                    SendMouseButton(0x0002);
                    try
                    {
                        await Task.Delay(200);
                        CaptureResizeFrame(subject, peer, useQuickNote, subjectOnLeft, $"held-{attempt}");
                        for (int step = 1; step <= 12; step++)
                        {
                            if (directEdge) MoveMouseTo(x + step * 10, y);
                            else Assert.True(SetCursorPos(x + step * 10, y));
                            await Task.Delay(35);
                            if (step is 6 or 12) CaptureResizeFrame(subject, peer, useQuickNote, subjectOnLeft, $"attempt-{attempt}-step-{step}");
                            if (resizeSubject && step is 6 or 12) AssertNoteTextVisible(subject);
                        }
                    }
                    finally { SendMouseButton(0x0004); }
                    await Task.Delay(500);
                    afterSubject = Bounds(subject);
                    afterPeer = Bounds(peer);
                    if (Math.Abs(afterSubject.Width - beforeSubject.Width) > 40 &&
                        Math.Abs(afterPeer.Width - beforePeer.Width) > 40)
                        break;
                }
                CaptureResizeFrame(subject, peer, useQuickNote, subjectOnLeft, "after");
                GetCursorPos(out NativePoint finalCursor);
                string evidence = $"subject={useQuickNote}, left={subjectOnLeft}\n{inputEvidence}finalCursor={finalCursor.X},{finalCursor.Y}\nBefore: {beforeSubject}; {beforePeer}\nAfter: {afterSubject}; {afterPeer}\n";
                string? directory = Environment.GetEnvironmentVariable("AITEBAR_QUICKNOTE_RENDER_DIR");
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                    File.WriteAllText(Path.Combine(directory, $"snap-{useQuickNote}-{subjectOnLeft}.txt"), evidence);
                }
                Assert.True(Math.Abs(afterSubject.Width - beforeSubject.Width) > 40, evidence);
                Assert.True(Math.Abs(afterPeer.Width - beforePeer.Width) > 40, evidence);
                Assert.InRange(Math.Abs((subjectOnLeft ? afterSubject.Right : afterSubject.Left) -
                    (subjectOnLeft ? afterPeer.Left : afterPeer.Right)), 0, 16);
                Assert.InRange(Math.Abs((subjectOnLeft ? afterSubject.Left : afterSubject.Right) -
                    (subjectOnLeft ? beforeSubject.Left : beforeSubject.Right)), 0, 2);
                Assert.InRange(Math.Abs((subjectOnLeft ? afterPeer.Right : afterPeer.Left) -
                    (subjectOnLeft ? beforePeer.Right : beforePeer.Left)), 0, 2);
            }
            finally
            {
                if (directEdge && Environment.GetEnvironmentVariable("AITEBAR_QUICKNOTE_RENDER_DIR") is string traceDirectory)
                {
                    Directory.CreateDirectory(traceDirectory);
                    File.WriteAllText(Path.Combine(traceDirectory, $"direct-edge-messages-{subjectOnLeft}.txt"), messages.ToString());
                }
                if (subject != null)
                {
                    var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    subject.Closed += (_, _) => closed.TrySetResult();
                    subject.Close();
                    await closed.Task.WaitAsync(TimeSpan.FromSeconds(10));
                    (subject as IDisposable)?.Dispose();
                }
                peer?.Close();
                SetCursorPos(cursor.X, cursor.Y);
                if (foreground != IntPtr.Zero) SetForegroundWindow(foreground);
                Directory.Delete(temp, true);
            }
        });

    private static Window CreateReference(string title) => new()
    {
        Title = title, MinWidth = 200, MinHeight = 160,
        Background = Brushes.LightGray, ShowInTaskbar = true,
        Content = new System.Windows.Controls.TextBlock { Text = title, Margin = new Thickness(24) }
    };

    private static void AssertNoteTextVisible(Window window)
    {
        Point start = window.PointToScreen(new Point(12, 44));
        Point end = window.PointToScreen(new Point(292, 68));
        using var bitmap = new System.Drawing.Bitmap((int)(end.X - start.X), (int)(end.Y - start.Y));
        using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
            graphics.CopyFromScreen((int)start.X, (int)start.Y, 0, 0, bitmap.Size);
        int textPixels = 0, backgroundPixels = 0;
        for (int y = 0; y < bitmap.Height; y++)
        for (int x = 0; x < bitmap.Width; x++)
        {
            var pixel = bitmap.GetPixel(x, y);
            if (pixel.R > 210 && pixel.G > 210 && pixel.B > 210) textPixels++;
            if (pixel.R < 100 && pixel.G < 100 && pixel.B < 100) backgroundPixels++;
        }
        Assert.True(textPixels > 40 && backgroundPixels > 500,
            $"Note text not visible on the desktop: text pixels={textPixels}, background pixels={backgroundPixels}.");
    }

    private static void CaptureResizeFrame(Window subject, Window peer, bool note, bool left, string stage)
        => CaptureResizeFrame(new WindowInteropHelper(subject).Handle, new WindowInteropHelper(peer).Handle, note, left, stage);

    private static void CaptureResizeFrame(IntPtr subject, IntPtr peer, bool note, bool left, string stage)
    {
        string? directory = Environment.GetEnvironmentVariable("AITEBAR_QUICKNOTE_RENDER_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        NativeRect a = Bounds(subject), b = Bounds(peer);
        int x = Math.Min(a.Left, b.Left), y = Math.Min(a.Top, b.Top);
        using var bitmap = new System.Drawing.Bitmap(Math.Max(a.Right, b.Right) - x, Math.Max(a.Bottom, b.Bottom) - y);
        using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
            graphics.CopyFromScreen(x, y, 0, 0, bitmap.Size);
        bitmap.Save(Path.Combine(directory, $"resize-{note}-{left}-{stage}.png"), System.Drawing.Imaging.ImageFormat.Png);
        foreach (NativeRect rect in new[] { a, b })
        {
            IntPtr surface = WindowFromPoint(new NativePoint { X = (rect.Left + rect.Right) / 2, Y = (rect.Top + rect.Bottom) / 2 });
            var name = new System.Text.StringBuilder(256);
            GetClassName(surface, name, name.Capacity);
            GetWindowThreadProcessId(surface, out uint process);
            GetWindowThreadProcessId(GetShellWindow(), out uint shell);
            File.AppendAllText(Path.Combine(directory, $"resize-surfaces-{note}-{left}-{stage}.txt"),
                $"subject={subject}; peer={peer}; visibleSurface={surface}; class={name}; process={process}; shellProcess={shell}\n");
        }
    }

    private static async Task SnapAsync(Window window, bool left)
        => await SnapAsync(new WindowInteropHelper(window).Handle, left);

    private static async Task SnapAsync(IntPtr handle, bool left)
    {
        await ActivateByClickAsync(handle);
        ushort arrow = left ? (ushort)0x25 : (ushort)0x27;
        Input[] keys = [Key(0x5B, false), Key(arrow, false), Key(arrow, true), Key(0x5B, true)];
        Assert.Equal((uint)keys.Length, SendInput((uint)keys.Length, keys, Marshal.SizeOf<Input>()));
        await Task.Delay(800);
    }

    private static async Task ActivateByClickAsync(Window window)
        => await ActivateByClickAsync(new WindowInteropHelper(window).Handle);

    private static async Task ActivateByClickAsync(IntPtr handle)
    {
        // Use a real click on our visible test surface; background test processes cannot force foreground focus.
        Assert.True(SetWindowPos(handle, new IntPtr(-1), 0, 0, 0, 0, 0x0013));
        NativeRect visible = Bounds(handle);
        Assert.True(SetCursorPos((visible.Left + visible.Right) / 2, (visible.Top + visible.Bottom) / 2));
        SendMouseButton(0x0002);
        SendMouseButton(0x0004);
        await Task.Delay(200);
        Assert.True(SetWindowPos(handle, new IntPtr(-2), 0, 0, 0, 0, 0x0013));
        Assert.Equal(handle, GetForegroundWindow());
    }

    private static NativeRect Bounds(Window window)
        => Bounds(new WindowInteropHelper(window).Handle);

    private static NativeRect Bounds(IntPtr handle)
    {
        Assert.Equal(0, DwmGetWindowAttribute(handle, 9, out NativeRect rect, Marshal.SizeOf<NativeRect>()));
        return rect;
    }

    private static Input Key(ushort key, bool up) => new()
    {
        Type = 1, Data = new InputUnion { Keyboard = new KeyboardInput { VirtualKey = key, Flags = (up ? 2u : 0u) | 1u } }
    };
    private static void SendMouseButton(uint flags)
    {
        Input[] input = [new() { Type = 0, Data = new InputUnion { Mouse = new MouseInput { Flags = flags } } }];
        Assert.Equal(1u, SendInput(1, input, Marshal.SizeOf<Input>()));
    }

    private static void MoveMouseTo(int x, int y)
    {
        var desktop = System.Windows.Forms.SystemInformation.VirtualScreen;
        Input[] input = [new() { Type = 0, Data = new InputUnion { Mouse = new MouseInput
        {
            X = (int)(((long)x - desktop.Left) * 65536 / desktop.Width + 32768 / desktop.Width),
            Y = (int)(((long)y - desktop.Top) * 65536 / desktop.Height + 32768 / desktop.Height),
            Flags = 0xC001 // MOUSEEVENTF_MOVE | ABSOLUTE | VIRTUALDESK: native input, not cursor warping.
        } } }];
        Assert.Equal(1u, SendInput(1, input, Marshal.SizeOf<Input>()));
    }

    [StructLayout(LayoutKind.Sequential)] private struct Input { public uint Type; public InputUnion Data; }
    [StructLayout(LayoutKind.Explicit)] private struct InputUnion
    {
        [FieldOffset(0)] public MouseInput Mouse;
        [FieldOffset(0)] public KeyboardInput Keyboard;
    }
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput { public int X, Y; public uint MouseData, Flags, Time; public UIntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct KeyboardInput { public ushort VirtualKey, ScanCode; public uint Flags, Time; public UIntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect
    {
        public int Left, Top, Right, Bottom;
        public int Width => Right - Left;
        public override string ToString() => $"({Left},{Top})-({Right},{Bottom})";
    }
    [DllImport("user32.dll")] private static extern uint SendInput(uint count, Input[] inputs, int size);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr handle, System.Text.StringBuilder name, int size);
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr handle, int index);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);
    [DllImport("user32.dll")] private static extern IntPtr GetShellWindow();
    [DllImport("user32.dll")] private static extern bool IsWindowArranged(IntPtr handle);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr handle);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out NativePoint point);
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(NativePoint point);
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr handle, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr handle, int attribute, out NativeRect rect, int size);

    private sealed class MemoryNote : IQuickNotePersistence
    {
        public string? LastConflictCopyPath => null;
        public bool HasExternalChanges() => false;
        public void Load(FlowDocument document) { document.Blocks.Clear(); document.Blocks.Add(new Paragraph(new Run("Quick Note Snap verification"))); }
        public Task SaveAsync(FlowDocument document) => Task.CompletedTask;
        public Task<string> SaveConflictCopyAsync(FlowDocument document) => Task.FromResult(string.Empty);
        public void OpenConflictCopy() { }
    }
}

public sealed class InteractiveSnapTheoryAttribute : TheoryAttribute
{
    public InteractiveSnapTheoryAttribute()
    {
        if (Environment.GetEnvironmentVariable("AITEBAR_QUICKNOTE_TEST_SNAP") != "1")
            Skip = "Opt-in interactive Windows Snap check (AITEBAR_QUICKNOTE_TEST_SNAP=1).";
    }
}

public sealed class InteractiveSnapFactAttribute : FactAttribute
{
    public InteractiveSnapFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("AITEBAR_QUICKNOTE_TEST_SNAP") != "1")
            Skip = "Opt-in interactive Windows Snap check (AITEBAR_QUICKNOTE_TEST_SNAP=1).";
    }
}
