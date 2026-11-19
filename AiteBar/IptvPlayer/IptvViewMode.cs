using System;
using Rect = System.Windows.Rect;

namespace AiteBar.IptvPlayer;

internal enum IptvViewMode
{
    Standard,
    VideoOnly,
    PictureInPicture,
    Fullscreen
}

internal sealed class IptvViewModes
{
    public IptvViewMode Windowed { get; private set; }
    public IptvViewMode Current { get; private set; }
    private IptvViewMode _beforeFullscreen;

    public void ToggleVideoOnly()
    {
        Windowed = Windowed == IptvViewMode.Standard ? IptvViewMode.VideoOnly : IptvViewMode.Standard;
        Current = Windowed;
    }

    public void TogglePictureInPicture() => Current = Current == IptvViewMode.PictureInPicture
        ? Windowed : IptvViewMode.PictureInPicture;

    public void ToggleFullscreen()
    {
        if (Current == IptvViewMode.Fullscreen)
        {
            Current = _beforeFullscreen;
            return;
        }

        _beforeFullscreen = Current;
        Current = IptvViewMode.Fullscreen;
    }

    public void ReturnToWindowed() => Current = Windowed;
}

public sealed record IptvPipPlacement(string Monitor, double OffsetX, double OffsetY, double Width, double Height);

internal static class IptvPipLayout
{
    public static Rect Fit(Rect workArea, double scale, IptvPipPlacement? saved)
    {
        double preferredWidth = saved is { Width: > 0 } && double.IsFinite(saved.Width) ? saved.Width : 360;
        double aspectRatio = IptvVideoViewport.AspectRatio;
        double maxWidth = Math.Min(workArea.Width, workArea.Height * aspectRatio);
        double width = Math.Min(maxWidth, Math.Max(300, preferredWidth) * scale);
        double height = width / aspectRatio;
        double left = saved is { OffsetX: var x } && double.IsFinite(x)
            ? workArea.Left + x * scale : workArea.Right - width - 16 * scale;
        double top = saved is { OffsetY: var y } && double.IsFinite(y)
            ? workArea.Top + y * scale : workArea.Bottom - height - 16 * scale;
        return new Rect(Math.Clamp(left, workArea.Left, workArea.Right - width),
            Math.Clamp(top, workArea.Top, workArea.Bottom - height), width, height);
    }

}
