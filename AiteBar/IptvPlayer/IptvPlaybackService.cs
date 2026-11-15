using System;
using System.IO;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using LibVLCSharp.Shared;
using VlcMediaPlayer = LibVLCSharp.Shared.MediaPlayer;

namespace AiteBar.IptvPlayer;

[SupportedOSPlatform("windows6.1")]
public sealed class IptvPlaybackService : IDisposable
{
    private static bool s_coreInitialized;
    private static readonly object s_initLock = new();

    private readonly LibVLC _libVlc;
    private readonly VlcMediaPlayer _mediaPlayer;

    private IptvChannel? _currentChannel;
    private IptvPlaybackState _state = IptvPlaybackState.Stopped;
    private int _reconnectAttempt;
    private CancellationTokenSource? _reconnectCts;
    private bool _disposed;

    public event Action<IptvPlaybackState, string?>? StateChanged;
    public event Action<int>? VolumeChanged;
    public event Action<bool>? MutedChanged;
    public event Action<bool>? CanPauseChanged;

    public IptvPlaybackService()
    {
        EnsureCoreInitialized();

        string[] options =
        [
            "--no-video-title-show",
            "--no-osd",
            "--ignore-config",
            "--network-caching=1500",
            "--live-caching=1500",
            "--avcodec-hw=none",
            "--drop-late-frames",
            "--skip-frames"
        ];

        _libVlc = new LibVLC(options);
        _mediaPlayer = new VlcMediaPlayer(_libVlc);

        _mediaPlayer.Opening += OnMediaPlayerOpening;
        _mediaPlayer.Buffering += OnMediaPlayerBuffering;
        _mediaPlayer.Playing += OnMediaPlayerPlaying;
        _mediaPlayer.Paused += OnMediaPlayerPaused;
        _mediaPlayer.Stopped += OnMediaPlayerStopped;
        _mediaPlayer.EncounteredError += OnMediaPlayerEncounteredError;
        _mediaPlayer.PausableChanged += OnMediaPlayerPausableChanged;
    }

    public VlcMediaPlayer MediaPlayer => _mediaPlayer;
    public IptvPlaybackState State => _state;
    public IptvChannel? CurrentChannel => _currentChannel;
    public int Volume => _mediaPlayer.Volume;
    public bool IsMuted => _mediaPlayer.Mute;
    public bool CanPause => _mediaPlayer.CanPause;

    public static void EnsureCoreInitialized()
    {
        if (s_coreInitialized)
        {
            return;
        }

        lock (s_initLock)
        {
            if (s_coreInitialized)
            {
                return;
            }

            try
            {
                // Инициализируем LibVLC core (расположение нативных библиотек)
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string x64Path = Path.Combine(baseDir, "libvlc", "win-x64");
                if (Directory.Exists(x64Path))
                {
                    Core.Initialize(x64Path);
                }
                else
                {
                    Core.Initialize();
                }
                s_coreInitialized = true;
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }
    }

    public void PlayChannel(IptvChannel channel)
    {
        if (channel == null)
        {
            return;
        }

        CancelReconnect();
        _currentChannel = channel;
        _reconnectAttempt = 0;

        string streamUrl = channel.CurrentStreamUrl;
        PlayStreamUrl(streamUrl);
    }

    public void PlayStreamUrl(string streamUrl)
    {
        if (string.IsNullOrWhiteSpace(streamUrl))
        {
            SetState(IptvPlaybackState.Error, "Stream URL is empty");
            return;
        }

        SetState(IptvPlaybackState.Connecting, "Connecting...");

        Task.Run(() =>
        {
            try
            {
                var mediaUri = Uri.TryCreate(streamUrl, UriKind.Absolute, out var uri)
                    ? uri
                    : new Uri(Path.GetFullPath(streamUrl));

                var media = new Media(_libVlc, mediaUri);
                media.AddOption(":network-caching=1500");
                media.AddOption(":live-caching=1500");

                _mediaPlayer.Media = media;
                _mediaPlayer.Play();
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                SetState(IptvPlaybackState.Error, ex.Message);
                ScheduleReconnect();
            }
        });
    }

    public void TogglePlayPause()
    {
        Task.Run(() =>
        {
            try
            {
                if (_state == IptvPlaybackState.Playing)
                {
                    if (_mediaPlayer.CanPause)
                    {
                        _mediaPlayer.Pause();
                    }
                    else
                    {
                        _mediaPlayer.Stop();
                        SetState(IptvPlaybackState.Stopped, null);
                    }
                }
                else if (_state == IptvPlaybackState.Paused)
                {
                    _mediaPlayer.Play();
                }
                else if (_state == IptvPlaybackState.Stopped && _currentChannel != null)
                {
                    PlayChannel(_currentChannel);
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        });
    }

    public void Stop()
    {
        CancelReconnect();
        Task.Run(() =>
        {
            try
            {
                _mediaPlayer.Stop();
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            SetState(IptvPlaybackState.Stopped, null);
        });
    }

    public void SetVolume(int volume)
    {
        int clamped = Math.Clamp(volume, 0, 100);
        Task.Run(() =>
        {
            try
            {
                _mediaPlayer.Volume = clamped;
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        });
        VolumeChanged?.Invoke(clamped);
    }

    public void ToggleMute()
    {
        Task.Run(() =>
        {
            try
            {
                bool muted = !_mediaPlayer.Mute;
                _mediaPlayer.Mute = muted;
                MutedChanged?.Invoke(muted);
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        });
    }

    public void SelectVariant(int variantIndex)
    {
        if (_currentChannel?.Variants == null || variantIndex < 0 || variantIndex >= _currentChannel.Variants.Count)
        {
            return;
        }

        _currentChannel.ActiveVariantIndex = variantIndex;
        CancelReconnect();
        _reconnectAttempt = 0;
        PlayStreamUrl(_currentChannel.CurrentStreamUrl);
    }

    private void ScheduleReconnect()
    {
        if (_disposed || _currentChannel == null)
        {
            return;
        }

        _reconnectAttempt++;
        if (_reconnectAttempt > 3)
        {
            // Пробуем следующий вариант качества, если есть
            if (_currentChannel.Variants.Count > 1 &&
                _currentChannel.ActiveVariantIndex + 1 < _currentChannel.Variants.Count)
            {
                _currentChannel.ActiveVariantIndex++;
                _reconnectAttempt = 0;
                PlayStreamUrl(_currentChannel.CurrentStreamUrl);
                return;
            }

            SetState(IptvPlaybackState.Error, "Playback failed after reconnect attempts");
            return;
        }

        int delayMs = _reconnectAttempt switch
        {
            1 => 1500,
            2 => 3000,
            _ => 5000
        };

        SetState(IptvPlaybackState.Reconnecting, $"Reconnecting ({_reconnectAttempt}/3)...");

        _reconnectCts = new CancellationTokenSource();
        var token = _reconnectCts.Token;

        Task.Delay(delayMs, token).ContinueWith(t =>
        {
            if (t.IsCompletedSuccessfully && !token.IsCancellationRequested && !_disposed)
            {
                if (_currentChannel != null)
                {
                    PlayStreamUrl(_currentChannel.CurrentStreamUrl);
                }
            }
        }, TaskScheduler.Default);
    }

    private void CancelReconnect()
    {
        if (_reconnectCts != null)
        {
            try
            {
                _reconnectCts.Cancel();
                _reconnectCts.Dispose();
            }
            catch
            {
                // ignore
            }
            _reconnectCts = null;
        }
    }

    private void SetState(IptvPlaybackState newState, string? message)
    {
        _state = newState;
        Task.Run(() =>
        {
            try
            {
                StateChanged?.Invoke(newState, message);
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        });
    }

    private void OnMediaPlayerOpening(object? sender, EventArgs e)
    {
        SetState(IptvPlaybackState.Connecting, "Connecting...");
    }

    private void OnMediaPlayerBuffering(object? sender, MediaPlayerBufferingEventArgs e)
    {
        if (e.Cache < 100)
        {
            SetState(IptvPlaybackState.Buffering, $"Buffering {Math.Round(e.Cache)}%...");
        }
    }

    private void OnMediaPlayerPlaying(object? sender, EventArgs e)
    {
        _reconnectAttempt = 0;
        SetState(IptvPlaybackState.Playing, null);
    }

    private void OnMediaPlayerPaused(object? sender, EventArgs e)
    {
        SetState(IptvPlaybackState.Paused, null);
    }

    private void OnMediaPlayerStopped(object? sender, EventArgs e)
    {
        if (_state != IptvPlaybackState.Reconnecting)
        {
            SetState(IptvPlaybackState.Stopped, null);
        }
    }

    private void OnMediaPlayerEncounteredError(object? sender, EventArgs e)
    {
        SetState(IptvPlaybackState.Error, "Stream error");
        ScheduleReconnect();
    }

    private void OnMediaPlayerPausableChanged(object? sender, MediaPlayerPausableChangedEventArgs e)
    {
        CanPauseChanged?.Invoke(e.Pausable != 0);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            CancelReconnect();

            try
            {
                _mediaPlayer.Opening -= OnMediaPlayerOpening;
                _mediaPlayer.Buffering -= OnMediaPlayerBuffering;
                _mediaPlayer.Playing -= OnMediaPlayerPlaying;
                _mediaPlayer.Paused -= OnMediaPlayerPaused;
                _mediaPlayer.Stopped -= OnMediaPlayerStopped;
                _mediaPlayer.EncounteredError -= OnMediaPlayerEncounteredError;
                _mediaPlayer.PausableChanged -= OnMediaPlayerPausableChanged;

                _mediaPlayer.Stop();
                _mediaPlayer.Dispose();
                _libVlc.Dispose();
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }
    }
}
