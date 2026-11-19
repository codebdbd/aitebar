using System;
using System.Diagnostics;
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
    internal sealed record StreamInfo(long Version, uint Width, uint Height, double? FramesPerSecond,
        string? Codec, uint Bitrate, int? DroppedFrames);

    private const int TuneBufferMs = 750;
    private const int RetryBufferMs = 1500;
    private static readonly TimeSpan OutputPollInterval = TimeSpan.FromMilliseconds(100);
    private static bool s_coreInitialized;
    private static readonly object s_initLock = new();

    private readonly LibVLC _libVlc;
    private readonly VlcMediaPlayer _mediaPlayer;
    private readonly IptvPlaybackCommandQueue _commands = new();
    private readonly object _sync = new();

    private IptvChannel? _currentChannel;
    private volatile IptvPlaybackState _state = IptvPlaybackState.Stopped;
    private long _requestedVersion;
    private long _activeVersion;
    private bool _hasStartedPlaying;
    private bool _videoOutputAttached;
    private volatile bool _hasVideoOutput;
    private volatile bool _isAudioOnly;
    private int _attemptNumber;
    private bool _hasMedia;
    private string _videoCrop = string.Empty;
    private long _tuneStartedAt;
    private long _attemptStartedAt;
    private long _mediaAssignedAt;
    private CancellationTokenSource? _startup;
    private bool _disposed;

    public event Action<IptvPlaybackState, string?>? StateChanged;
    public event Action<int>? VolumeChanged;
    public event Action<bool>? MutedChanged;
    public event Action<bool>? CanPauseChanged;
    public event Action? VideoOutputChanged;

    public IptvPlaybackService()
    {
        EnsureCoreInitialized();

        string[] options =
        [
            "--no-video-title-show",
            "--no-osd",
            "--ignore-config",
            "--drop-late-frames",
            "--skip-frames"
        ];

        _libVlc = new LibVLC(options);
        _mediaPlayer = new VlcMediaPlayer(_libVlc);

        _mediaPlayer.Buffering += OnMediaPlayerBuffering;
        _mediaPlayer.Playing += OnMediaPlayerPlaying;
        _mediaPlayer.Paused += OnMediaPlayerPaused;
        _mediaPlayer.EndReached += OnMediaPlayerEnded;
        _mediaPlayer.EncounteredError += OnMediaPlayerEncounteredError;
        _mediaPlayer.Vout += OnMediaPlayerVout;
        _mediaPlayer.PausableChanged += OnMediaPlayerPausableChanged;
    }

    public VlcMediaPlayer MediaPlayer => _mediaPlayer;
    public IptvPlaybackState State => _state;
    public bool HasVideoOutput => _hasVideoOutput;
    internal bool IsAudioOnly => _isAudioOnly;
    public IptvChannel? CurrentChannel => _currentChannel;
    public int Volume => _mediaPlayer.Volume;
    public bool IsMuted => _mediaPlayer.Mute;
    public bool CanPause => _mediaPlayer.CanPause;

    internal bool IsCurrentStreamInfo(StreamInfo info) => HasVideoOutput && IsCurrent(info.Version);

    internal async Task<StreamInfo?> ReadStreamInfoAsync()
    {
        long version;
        lock (_sync)
        {
            if (!IsActiveLocked || !_hasVideoOutput) return null;
            version = _activeVersion;
        }

        StreamInfo? result = null;
        await _commands.Enqueue(() =>
        {
            uint width = 0;
            uint height = 0;
            if (!_mediaPlayer.Size(0, ref width, ref height) || width == 0 || height == 0) return;

            using var media = _mediaPlayer.Media;
            int videoTrackId = _mediaPlayer.VideoTrack;
            var track = Array.Find(media?.Tracks ?? [], item => item.TrackType == TrackType.Video && item.Id == videoTrackId);
            var video = track.Data.Video;
            double? fps = video.FrameRateDen != 0 && video.FrameRateNum != 0
                ? (double)video.FrameRateNum / video.FrameRateDen : null;
            string? codec = track.Codec != 0 ? media?.CodecDescription(TrackType.Video, track.Codec) : null;
            var stats = media?.Statistics;
            int? dropped = stats is { DisplayedPictures: > 0 } ? stats.Value.LostPictures : null;
            result = new StreamInfo(version, width, height, fps, codec, track.Bitrate, dropped);
        }, () => HasVideoOutput && IsCurrent(version)).ConfigureAwait(false);

        return result is not null && IsCurrentStreamInfo(result) ? result : null;
    }

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
        lock (_sync)
        {
            if (_disposed) return;
            _currentChannel = channel;
            BeginTuneLocked(channel);
        }
    }

    private void BeginTuneLocked(IptvChannel channel)
    {
        _tuneStartedAt = Stopwatch.GetTimestamp();
        _attemptNumber = 0;
        RequestStreamLocked(channel);
    }

    private void RequestStreamLocked(IptvChannel channel)
    {
        long version = InvalidateStreamLocked();
        _attemptStartedAt = Stopwatch.GetTimestamp();
        int attempts = ++_attemptNumber;
        TimeSpan timeout = IptvTunePolicy.AttemptTimeout(Stopwatch.GetElapsedTime(_tuneStartedAt, _attemptStartedAt),
            attempts < channel.Variants.Count, attempts == 1 && channel.Variants.Count <= 1);
        if (timeout <= TimeSpan.Zero)
        {
            EndTuneLocked("Stream startup timed out");
            return;
        }

        string url = channel.CurrentStreamUrl;
        int bufferMs = attempts > channel.Variants.Count && attempts > 1 ? RetryBufferMs : TuneBufferMs;
        SetStateLocked(attempts == 1 ? IptvPlaybackState.Connecting : IptvPlaybackState.Reconnecting, null);
        var startup = new CancellationTokenSource();
        _startup = startup;
        _ = _commands.Enqueue(() => OpenStream(url, version, bufferMs), () => IsCurrent(version));
        _ = ObserveStartupAsync(version, timeout, startup);
    }

    private void OpenStream(string url, long version, int bufferMs)
    {
        try
        {
            if (!IsCurrent(version)) return;

            if (string.IsNullOrWhiteSpace(url))
            {
                StopStream();
                throw new InvalidOperationException("Stream URL is empty");
            }

            var uri = Uri.TryCreate(url, UriKind.Absolute, out Uri? absolute)
                ? absolute
                : new Uri(Path.GetFullPath(url));
            using var media = new Media(_libVlc, uri);
            media.AddOption($":network-caching={bufferMs}");
            media.AddOption($":live-caching={bufferMs}");
            // Assigning Media releases the old input while keeping VLC's output resources.
            _mediaPlayer.Media = media;
            _hasMedia = true;
            lock (_sync)
            {
                if (_disposed || _requestedVersion != version) return;
                _mediaAssignedAt = Stopwatch.GetTimestamp();
                _activeVersion = version;
            }

            if (!_mediaPlayer.Play())
            {
                HandleStreamFailure(version, "Stream could not be opened");
            }
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            HandleStreamFailure(version, "Stream could not be opened");
        }
    }

    public void TogglePlayPause()
    {
        long version;
        lock (_sync)
        {
            if (_state is not (IptvPlaybackState.Playing or IptvPlaybackState.Paused)) return;
            version = _requestedVersion;
        }

        QueueControl(() =>
        {
            if (State == IptvPlaybackState.Paused)
                _mediaPlayer.SetPause(false);
            else if (State == IptvPlaybackState.Playing && _mediaPlayer.CanPause)
                _mediaPlayer.SetPause(true);
        }, version);
    }

    public void Stop()
    {
        lock (_sync)
        {
            if (_disposed) return;
            long version = InvalidateStreamLocked();
            SetStateLocked(IptvPlaybackState.Stopped, null);
            QueueControl(StopStream, version);
        }
    }

    private void StopStream()
    {
        if (!_hasMedia) return;
        _mediaPlayer.Stop();
        _mediaPlayer.Media = null;
        _hasMedia = false;
    }

    public void SetVolume(int volume)
    {
        int clamped = Math.Clamp(volume, 0, 100);
        QueueControl(() =>
        {
            _mediaPlayer.Volume = clamped;
            VolumeChanged?.Invoke(clamped);
        });
    }

    public void SetVideoCrop(string geometry)
    {
        lock (_sync)
        {
            if (_disposed || _videoCrop == geometry) return;
            _videoCrop = geometry;
        }
        _ = _commands.Enqueue(() =>
        {
            try { _mediaPlayer.CropGeometry = geometry; }
            catch (Exception ex) { Logger.Log(ex); }
        }, () =>
        {
            lock (_sync) return !_disposed && _videoCrop == geometry;
        });
    }

    public void ToggleMute()
    {
        QueueControl(() =>
        {
            bool muted = !_mediaPlayer.Mute;
            _mediaPlayer.Mute = muted;
            MutedChanged?.Invoke(muted);
        });
    }

    public void SelectVariant(int variantIndex)
    {
        lock (_sync)
        {
            if (_disposed || _currentChannel is not { } channel || variantIndex == channel.ActiveVariantIndex ||
                variantIndex < 0 || variantIndex >= channel.Variants.Count)
            {
                return;
            }

            channel.ActiveVariantIndex = variantIndex;
            BeginTuneLocked(channel);
        }
    }

    private void QueueControl(Action control, long? version = null)
    {
        _ = _commands.Enqueue(() =>
        {
            try { control(); }
            catch (Exception ex) { Logger.Log(ex); }
        }, () => IsCurrent(version));
    }

    private bool IsCurrent(long? version)
    {
        lock (_sync)
        {
            return !_disposed && (version is null || version == _requestedVersion);
        }
    }

    private long InvalidateStreamLocked()
    {
        _activeVersion = 0;
        CancelStartupLocked();
        _hasStartedPlaying = false;
        _videoOutputAttached = false;
        _isAudioOnly = false;
        SetVideoOutputLocked(false);
        CanPauseChanged?.Invoke(false);
        return ++_requestedVersion;
    }

    private void CancelStartupLocked()
    {
        var startup = _startup;
        _startup = null;
        startup?.Cancel();
    }

    private void HandleStreamFailure(long version, string message, bool timedOut = false)
    {
        lock (_sync)
        {
            if (_disposed || version != _requestedVersion || (timedOut && _startup is null) ||
                _currentChannel is not { } channel)
            {
                return;
            }

            if (_hasStartedPlaying)
            {
                _tuneStartedAt = Stopwatch.GetTimestamp();
                _attemptNumber = 1;
            }

            int? next = IptvTunePolicy.NextVariant(channel.ActiveVariantIndex, channel.Variants.Count, _attemptNumber,
                Stopwatch.GetElapsedTime(_tuneStartedAt), timedOut || _hasStartedPlaying);
            if (next is { } variant)
            {
                channel.ActiveVariantIndex = variant;
                RequestStreamLocked(channel);
            }
            else
            {
                EndTuneLocked(message);
            }
        }
    }

    private void EndTuneLocked(string message)
    {
        long version = InvalidateStreamLocked();
        SetStateLocked(IptvPlaybackState.Error, message);
        QueueControl(StopStream, version);
        LogStartup("failed", _tuneStartedAt, _attemptNumber);
    }

    private async Task ObserveStartupAsync(long version, TimeSpan timeout, CancellationTokenSource startup)
    {
        try
        {
            await Task.WhenAll(WatchDeadlineAsync(version, timeout, startup.Token),
                WatchOutputAsync(version, startup.Token)).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
        }
        finally
        {
            lock (_sync) startup.Dispose();
        }
    }

    private async Task WatchDeadlineAsync(long version, TimeSpan timeout, CancellationToken cancellationToken)
    {
        await Task.Delay(timeout, cancellationToken).ConfigureAwait(false);
        HandleStreamFailure(version, "Stream startup timed out", timedOut: true);
    }

    private async Task WatchOutputAsync(long version, CancellationToken cancellationToken)
    {
        while (true)
        {
            await Task.Delay(OutputPollInterval, cancellationToken).ConfigureAwait(false);
            await _commands.Enqueue(() => CheckOutput(version), () => !cancellationToken.IsCancellationRequested &&
                IsCurrent(version)).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private void CheckOutput(long version)
    {
        using var media = _mediaPlayer.Media;
        if (media?.Statistics is not { } stats) return;
        bool video = stats.DisplayedPictures > 0;
        bool audioOnly = false;
        if (!video && stats.PlayedAudioBuffers > 0 && media.Tracks is { Length: > 0 } tracks)
        {
            audioOnly = Array.Exists(tracks, track => track.TrackType == TrackType.Audio) &&
                !Array.Exists(tracks, track => track.TrackType == TrackType.Video);
        }

        lock (_sync)
        {
            if (!IsActiveLocked || _activeVersion != version || _startup is null ||
                (!video && (!audioOnly || _videoOutputAttached))) return;
            CancelStartupLocked();
            _hasStartedPlaying = true;
            _isAudioOnly = !video;
            SetVideoOutputLocked(video);
            SetStateLocked(IptvPlaybackState.Playing, null);
            LogStartup(video ? "video ready" : "audio ready", _tuneStartedAt, _attemptNumber,
                Stopwatch.GetElapsedTime(_attemptStartedAt, _mediaAssignedAt));
        }
    }

    private bool IsActiveLocked => !_disposed && _activeVersion != 0 && _activeVersion == _requestedVersion;

    private void SetStateLocked(IptvPlaybackState state, string? message)
    {
        _state = state;
        StateChanged?.Invoke(state, message);
    }

    private void SetVideoOutputLocked(bool hasVideo)
    {
        if (_hasVideoOutput == hasVideo) return;
        _hasVideoOutput = hasVideo;
        VideoOutputChanged?.Invoke();
    }

    private void OnMediaPlayerBuffering(object? sender, MediaPlayerBufferingEventArgs e)
    {
        lock (_sync)
        {
            if (!IsActiveLocked || _state == IptvPlaybackState.Paused) return;
            if (e.Cache < 100)
            {
                if (_hasStartedPlaying && _state == IptvPlaybackState.Playing)
                    SetStateLocked(IptvPlaybackState.Buffering, null);
            }
            else if (_hasStartedPlaying && _state == IptvPlaybackState.Buffering)
            {
                SetStateLocked(IptvPlaybackState.Playing, null);
            }
        }
    }

    private void OnMediaPlayerPlaying(object? sender, EventArgs e)
    {
        lock (_sync)
        {
            if (IsActiveLocked && _hasStartedPlaying && _state != IptvPlaybackState.Playing)
                SetStateLocked(IptvPlaybackState.Playing, null);
        }
    }

    private void OnMediaPlayerPaused(object? sender, EventArgs e)
    {
        lock (_sync)
        {
            if (IsActiveLocked) SetStateLocked(IptvPlaybackState.Paused, null);
        }
    }

    private void OnMediaPlayerEnded(object? sender, EventArgs e) => OnStreamInterrupted("Stream ended");
    private void OnMediaPlayerEncounteredError(object? sender, EventArgs e) => OnStreamInterrupted("Stream error");

    private void OnStreamInterrupted(string message)
    {
        lock (_sync)
        {
            if (IsActiveLocked) HandleStreamFailure(_activeVersion, message);
        }
    }

    private void OnMediaPlayerVout(object? sender, MediaPlayerVoutEventArgs e)
    {
        lock (_sync)
        {
            if (!IsActiveLocked) return;
            _videoOutputAttached = e.Count > 0;
            if (_hasStartedPlaying && !_isAudioOnly)
                SetVideoOutputLocked(_videoOutputAttached);
        }
    }

    private static void LogStartup(string result, long tuneStartedAt, int attemptNumber, TimeSpan? queueTime = null)
    {
        string measurement = FormattableString.Invariant(
            $"IPTV tune {result}: {Stopwatch.GetElapsedTime(tuneStartedAt).TotalMilliseconds:F0} ms, attempt {attemptNumber}");
        if (queueTime is { } elapsed)
            measurement += FormattableString.Invariant($", queue and media change {elapsed.TotalMilliseconds:F0} ms");
        _ = Task.Run(() => Logger.Log(measurement));
    }

    private void OnMediaPlayerPausableChanged(object? sender, MediaPlayerPausableChangedEventArgs e)
    {
        lock (_sync)
        {
            if (IsActiveLocked) CanPauseChanged?.Invoke(e.Pausable != 0);
        }
    }

    public void Dispose()
    {
        Task teardown;
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
            InvalidateStreamLocked();
            teardown = _commands.Enqueue(() =>
            {
                _mediaPlayer.Buffering -= OnMediaPlayerBuffering;
                _mediaPlayer.Playing -= OnMediaPlayerPlaying;
                _mediaPlayer.Paused -= OnMediaPlayerPaused;
                _mediaPlayer.EndReached -= OnMediaPlayerEnded;
                _mediaPlayer.EncounteredError -= OnMediaPlayerEncounteredError;
                _mediaPlayer.Vout -= OnMediaPlayerVout;
                _mediaPlayer.PausableChanged -= OnMediaPlayerPausableChanged;
                try { StopStream(); }
                finally
                {
                    _mediaPlayer.Dispose();
                    _libVlc.Dispose();
                }
            });
        }

        teardown.GetAwaiter().GetResult();
    }
}
