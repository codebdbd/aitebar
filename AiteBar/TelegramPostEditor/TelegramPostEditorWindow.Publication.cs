using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using AiteBar.TelegramPostEditor.Models;
using AiteBar.TelegramPostEditor.Services;

namespace AiteBar;

public partial class TelegramPostEditorWindow
{
    private bool _publicationBusy;
    private bool _refreshingQueue;
    private string? _publicationError;
    private PostDocument? _submissionSnapshot;
    private IReadOnlyList<PublicationTarget> _submissionResults = [];

    private bool IsScheduled => ScheduleOption?.IsChecked == true;

    private async void SendPost_Click(object sender, RoutedEventArgs e)
    {
        if (_publicationBusy || _changingDraft || !_hasPublishableContent || EditorPage.Visibility != Visibility.Visible) return;
        RefreshChannelsAndBots();
        var recipients = _channelSelections.Where(item => item.IsSelected).ToArray();
        if (recipients.Length == 0 || recipients.Any(item => !item.HasValidBotToken))
        {
            OpenPublicationSetup();
            return;
        }

        if (IsScheduled && (!SchedulerIsPaired() || !TryReadSchedule(out _)))
        {
            OpenPublicationSetup(chooseTime: true);
            return;
        }

        EditorSendError.Text = string.Empty;
        await PublishAsync(recipients.Select(item => item.Channel).ToArray(), IsScheduled);
    }

    private void Review_Click(object sender, RoutedEventArgs e)
    {
        if (_publicationBusy || _changingDraft || _currentPost == null) return;
        RefreshChannelsAndBots();
        OpenPublicationSetup();
    }

    private void ChooseTiming_Click(object sender, RoutedEventArgs e)
    {
        if (_publicationBusy || _changingDraft || _currentPost == null) return;
        RefreshChannelsAndBots();
        OpenPublicationSetup(chooseTime: true);
    }

    private void OpenPublicationSetup(bool chooseTime = false)
    {
        _publicationError = null;
        RecipientsSection.Visibility = chooseTime ? Visibility.Collapsed : Visibility.Visible;
        TimingSection.Visibility = chooseTime ? Visibility.Visible : Visibility.Collapsed;
        RoutingHeading.Text = LocalizationService.Get(chooseTime ? "TelegramComposer_When" : "TelegramComposer_WhereToSend");
        ShowPage(PublishPage);
        if (chooseTime)
        {
            TimeOptions.BringIntoView();
            (IsScheduled ? ScheduleOption : SendNowOption).Focus();
        }
        else if (_channelSelections.Count == 0)
            PwdQuickBotToken.Focus();
        else
            LstChannels.MoveFocus(new System.Windows.Input.TraversalRequest(System.Windows.Input.FocusNavigationDirection.First));
    }

    private void PublicationTiming_Changed(object sender, RoutedEventArgs e)
    {
        if (ScheduleFields == null) return;
        ScheduleFields.Visibility = IsScheduled ? Visibility.Visible : Visibility.Collapsed;
        if (IsScheduled && !SchedulerIsPaired())
            SchedulerSettings.IsExpanded = true;
        ScheduleInput_Changed(sender, e);
    }

    private void ScheduleInput_Changed(object sender, RoutedEventArgs e)
    {
        _publicationError = null;
        if (TxtModeBadge != null && TxtServerUrl != null)
            UpdatePairingStatus();
        UpdatePublicationControls();
    }

    private bool SchedulerIsPaired()
    {
        AppSettings settings = _settingsService.Settings;
        return !string.IsNullOrWhiteSpace(settings.TelegramPostEditorDeviceId) &&
            !string.IsNullOrWhiteSpace(settings.TelegramPostEditorServerUrl) &&
            string.Equals(settings.TelegramPostEditorServerUrl, TxtServerUrl.Text.Trim().TrimEnd('/'), StringComparison.Ordinal);
    }

    private void UpdatePairingStatus()
    {
        TxtModeBadge.Text = LocalizationService.Get(SchedulerIsPaired()
            ? "TelegramPostEditor_QueueConnected" : "TelegramComposer_PairRequired");
    }

    private bool TryReadSchedule(out DateTime utc)
    {
        utc = default;
        if (ScheduleDate.SelectedDate is not { } date ||
            !TimeOnly.TryParseExact(ScheduleTime.Text.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out TimeOnly time))
            return false;

        return TryResolveFutureTime(date.Date.Add(time.ToTimeSpan()), out utc);
    }

    private static bool TryResolveFutureTime(DateTime local, out DateTime utc)
    {
        utc = default;
        TimeZoneInfo zone = TimeZoneInfo.Local;
        if (zone.IsInvalidTime(local) || zone.IsAmbiguousTime(local))
            return false;
        utc = TimeZoneInfo.ConvertTimeToUtc(local, zone);
        return utc > DateTime.UtcNow;
    }

    private void UpdatePublicationControls()
    {
        if (SendPostButton == null || TxtSelectedChannelsSummary == null) return;
        var selected = _channelSelections.Where(item => item.IsSelected).ToList();
        string recipients = string.Join(", ", selected.Select(item => item.DestinationLabel));
        EditorRecipients.Text = LocalizationService.Format("TelegramComposer_WhereSummary", selected.Count == 0
            ? LocalizationService.Get("TelegramComposer_SelectRecipient") : recipients);
        ReviewButton.ToolTip = EditorRecipients.Text;
        TxtSelectedChannelsSummary.Text = selected.Count == 0
            ? LocalizationService.Get("TelegramPostEditor_SelectChannelWarning")
            : recipients;
        TxtSelectedChannelsSummary.Visibility = selected.Count == 0 && RecipientsSection.Visibility == Visibility.Visible
            ? Visibility.Visible : Visibility.Collapsed;
        TxtSelectedChannelsSummary.ToolTip = selected.Count == 0 ? null
            : string.Join(Environment.NewLine, selected.Select(item => item.DestinationLabel));

        string? issue = selected.Any(item => !item.HasValidBotToken)
            ? LocalizationService.Get("TelegramPostEditor_ChannelMissingBotToken")
            : null;
        string timing = LocalizationService.Get("TelegramComposer_Now");
        if (IsScheduled)
        {
            bool hasTime = TryReadSchedule(out DateTime scheduledUtc);
            timing = hasTime ? scheduledUtc.ToLocalTime().ToString("g", LocalizationService.ResolvedCulture)
                : LocalizationService.Get("TelegramComposer_ChooseTime");
            if (!SchedulerIsPaired())
                issue ??= LocalizationService.Get("TelegramComposer_PairRequired");
            else if (!hasTime)
                issue ??= LocalizationService.Get("TelegramComposer_FutureTime");
        }

        WhenButton.Content = LocalizationService.Format("TelegramComposer_WhenSummary", timing);
        WhenButton.IsEnabled = ReviewButton.IsEnabled = !_publicationBusy && ResultPage.Visibility != Visibility.Visible;
        SendPostButton.IsEnabled = _hasPublishableContent && !_publicationBusy &&
            (PublishPage.Visibility != Visibility.Visible || selected.Count > 0 && issue == null);
        SendPostButton.Content = LocalizationService.Get(_publicationBusy
            ? "TelegramComposer_Submitting"
            : IsScheduled ? "TelegramPostEditor_Schedule" : "TelegramComposer_Send");
        TxtValidationStatus.Text = _publicationError ?? issue ?? string.Empty;
        TxtValidationStatus.Foreground = (System.Windows.Media.Brush)FindResource(
            _publicationError == null ? "ComposerMuted" : "ComposerError");
    }

    private void SetPublicationBusy(bool inProgress)
    {
        _publicationBusy = inProgress;
        WorkspaceNavigation.IsEnabled = !inProgress;
        EditorPage.IsEnabled = !inProgress;
        PublishOptions.IsEnabled = !inProgress;
        ResultRows.IsEnabled = !inProgress;
        QueueRows.IsEnabled = !inProgress && !_refreshingQueue;
        RetryPublicationButton.IsEnabled = !inProgress;
        UpdatePublicationControls();
    }

    private async void BtnPairServer_Click(object sender, RoutedEventArgs e)
    {
        if (_publicationBusy) return;
        SetPublicationBusy(true);
        _publicationError = null;
        try
        {
            string url = PublicationSyncService.ValidateServerUrl(TxtServerUrl.Text);
            ServerPairingResult pairing = await _syncService.PairAsync(url, TxtPairingCode.Text, Environment.MachineName);
            _settingsService.UpdateSettings(settings =>
            {
                settings.TelegramPostEditorServerUrl = url;
                settings.TelegramPostEditorDeviceId = pairing.DeviceId;
            });
            await _settingsService.SaveAsync();
            TxtServerUrl.Text = url;
            TxtPairingCode.Clear();
            SchedulerSettings.IsExpanded = false;
            UpdatePairingStatus();
        }
        catch (Exception ex)
        {
            _publicationError = ex.Message;
        }
        finally
        {
            SetPublicationBusy(false);
        }
    }

    private async Task PublishAsync(IReadOnlyList<ChannelAccount> channels, bool scheduled)
    {
        if (_currentPost is not { } draft || channels.Count == 0) return;
        DateTime utc = default;
        if (scheduled && (!SchedulerIsPaired() || !TryReadSchedule(out utc)))
        {
            UpdatePublicationControls();
            return;
        }

        SetPublicationBusy(true);
        _publicationError = null;
        _submissionSnapshot = null;
        _submissionResults = [];
        try
        {
            await _autosave.FlushAsync();
            _repository.SavePost(draft, createRevision: true);
            PostDocument snapshot = _repository.GetPost(draft.Id)
                ?? throw new InvalidOperationException(LocalizationService.Get("TelegramComposer_DraftUnavailable"));
            _submissionSnapshot = snapshot;

            _submissionResults = scheduled
                ? await SchedulePublicationAsync(snapshot, channels, utc)
                : await _publicationService.PublishNowAsync(snapshot, channels);
            ShowPublicationResults(scheduled);
        }
        catch (Exception ex)
        {
            if (!scheduled && _submissionSnapshot is { } snapshot)
            {
                _submissionResults = _publicationService.GetTargetsForPost(snapshot.Id)
                    .Where(target => target.PostRevision == snapshot.Revision && target.ServerId == null).ToList();
                if (_submissionResults.Count > 0)
                {
                    ShowPublicationResults(scheduled: false, ex.Message);
                    return;
                }
            }

            if (PublishPage.Visibility != Visibility.Visible)
                EditorSendError.Text = ex.Message;
            else
                _publicationError = ex.Message;
        }
        finally
        {
            SetPublicationBusy(false);
        }
    }

    private async Task<IReadOnlyList<PublicationTarget>> SchedulePublicationAsync(
        PostDocument post, IReadOnlyList<ChannelAccount> channels, DateTime utc)
    {
        foreach (ChannelAccount channel in channels)
        {
            post.ChannelOverrides.TryGetValue(channel.Id, out ChannelPostOverride? channelOverride);
            var media = (channelOverride?.Media ?? post.Media)
                .Select(attachment => _mediaStore.GetById(attachment.MediaId)).OfType<PostMediaItem>().ToList();
            PostValidationSummary validation = TelegramValidationService.Validate(post, media, channelOverride);
            if (!validation.IsValid)
                throw new InvalidOperationException($"{channel.DisplayName}{Environment.NewLine}{string.Join(Environment.NewLine, validation.Errors)}");
        }

        string url = _settingsService.Settings.TelegramPostEditorServerUrl!;
        string deviceId = _settingsService.Settings.TelegramPostEditorDeviceId!;
        var bots = _channelService.GetBots().ToDictionary(bot => bot.Id, StringComparer.Ordinal);
        foreach (ChannelAccount channel in channels)
        {
            if (!bots.TryGetValue(channel.BotId, out BotAccount? bot))
                throw new InvalidOperationException($"{channel.DisplayName} · {LocalizationService.Get("TelegramPostEditor_ChannelMissingBotToken")}");
            await _syncService.SyncChannelAsync(url, deviceId, channel, bot, shareBotTokenWithServer: true);
        }

        var receipt = await _syncService.SubmitScheduledAsync(url, deviceId, post, channels.Select(channel => (channel, utc)).ToList());
        IReadOnlyList<PublicationTarget> targets = PublicationSyncService.ReadScheduledTargets(receipt, deviceId, post.Id, channels);
        _publicationService.SaveTargets(targets);
        return targets;
    }

    private async void RetryPublication_Click(object sender, RoutedEventArgs e)
    {
        if (_publicationBusy || _submissionSnapshot is not { } snapshot) return;
        var failed = _submissionResults.Where(CanRetry).ToArray();
        if (failed.Length == 0) return;
        SetPublicationBusy(true);
        try
        {
            await _publicationService.RetryFailedTargetsAsync(snapshot, failed);
            ShowPublicationResults(scheduled: false);
        }
        catch (Exception ex)
        {
            ShowPublicationResults(scheduled: false, ex.Message);
        }
        finally
        {
            SetPublicationBusy(false);
        }
    }

    private static bool CanRetry(PublicationTarget target) => target.Status == WorkflowStatus.FAILED && target.ServerId == null;

    private void ShowPublicationResults(bool scheduled, string? notice = null)
    {
        int sent = _submissionResults.Count(target => target.Status == WorkflowStatus.SENT);
        ResultHeading.Text = scheduled
            ? LocalizationService.Get("TelegramComposer_Scheduled")
            : LocalizationService.Format("TelegramComposer_DeliveredCount", sent, _submissionResults.Count);
        ResultNotice.Text = notice ?? string.Empty;
        ResultRows.ItemsSource = _submissionResults.Select(target => CreateTargetRow(target, showTime: scheduled)).ToList();
        ShowPage(ResultPage);
    }

    private async void ShowQueue_Click(object sender, RoutedEventArgs e)
    {
        if (_publicationBusy || _changingDraft || QueuePage.Visibility == Visibility.Visible) return;
        ShowWorkspacePage(QueuePage);
        await RefreshQueueAsync();
    }

    private async void RefreshQueue_Click(object sender, RoutedEventArgs e) => await RefreshQueueAsync();

    private async Task RefreshQueueAsync()
    {
        if (_refreshingQueue || _publicationBusy) return;
        _refreshingQueue = true;
        QueueRows.IsEnabled = false;
        RefreshQueueButton.IsEnabled = false;
        try
        {
            var targets = _publicationService.GetAllTargets().Where(target => target.ServerId != null)
                .OrderByDescending(target => target.ScheduledUtc).ToList();
            ShowQueueRows(targets);
            if (targets.Count == 0) return;
            string? device = _settingsService.Settings.TelegramPostEditorDeviceId;
            string? url = _settingsService.Settings.TelegramPostEditorServerUrl;
            if (string.IsNullOrWhiteSpace(device) || string.IsNullOrWhiteSpace(url))
            {
                QueueStatus.Text = LocalizationService.Get("TelegramComposer_QueueOffline");
                return;
            }

            var pending = targets.Where(target => target.Status != WorkflowStatus.SENT && target.Status != WorkflowStatus.CANCELED);
            bool failed = false;
            foreach (var batch in pending.Chunk(6))
            {
                var refreshed = await Task.WhenAll(batch.Select(target => RefreshServerTargetAsync(target, url, device)));
                _publicationService.SaveTargets(refreshed.OfType<PublicationTarget>().ToArray());
                failed |= refreshed.Any(target => target == null);
            }
            ShowQueueRows(targets);
            if (failed)
                QueueStatus.Text = LocalizationService.Get("TelegramComposer_QueueOffline");
        }
        catch (Exception ex)
        {
            QueueStatus.Text = ex.Message;
        }
        finally
        {
            _refreshingQueue = false;
            QueueRows.IsEnabled = !_publicationBusy;
            RefreshQueueButton.IsEnabled = true;
        }
    }

    private async Task<PublicationTarget?> RefreshServerTargetAsync(PublicationTarget target, string url, string device)
    {
        try
        {
            var status = await _syncService.GetTargetStatusAsync(url, device, target.ServerId!);
            PublicationSyncService.ApplyServerStatus(target, status);
            return target;
        }
        catch (Exception ex) when (ex is System.Net.Http.HttpRequestException or TaskCanceledException
            or System.IO.InvalidDataException or System.Text.Json.JsonException or InvalidOperationException or ArgumentException)
        {
            return null;
        }
    }

    private void ShowQueueRows(IReadOnlyList<PublicationTarget> targets)
    {
        Dictionary<string, string> titles = targets.Select(target => target.PostId).Distinct(StringComparer.Ordinal)
            .ToDictionary(id => id, id => _repository.GetPost(id)?.Title ?? id, StringComparer.Ordinal);
        QueueRows.ItemsSource = targets.Select(target => CreateTargetRow(target, showTime: true, titles[target.PostId])).ToList();
        QueueStatus.Text = targets.Count == 0 ? LocalizationService.Get("TelegramComposer_QueueEmpty") : string.Empty;
        if (_submissionResults.Any(target => target.ServerId != null))
        {
            var targetsById = targets.ToDictionary(target => target.Id, StringComparer.Ordinal);
            _submissionResults = _submissionResults.Select(target => targetsById.GetValueOrDefault(target.Id) ?? target).ToList();
            ResultRows.ItemsSource = _submissionResults.Select(target => CreateTargetRow(target, showTime: true)).ToList();
        }
    }

    private PublicationRow CreateTargetRow(PublicationTarget target, bool showTime, string? postTitle = null)
    {
        ChannelSelectionItem? item = _channelSelections.FirstOrDefault(item => item.Channel.Id == target.ChannelId);
        ChannelAccount? account = item?.Channel ?? _channelService.GetChannels().FirstOrDefault(channel => channel.Id == target.ChannelId);
        string channel = item?.DestinationLabel ?? account?.DisplayName ?? target.ChannelId;
        string key = target.Status switch
        {
            WorkflowStatus.SENT => "TelegramPostEditor_StatusPublished",
            WorkflowStatus.QUEUED_LOCAL or WorkflowStatus.QUEUED_SERVER => "TelegramPostEditor_StatusScheduled",
            WorkflowStatus.FAILED => "TelegramPostEditor_StatusFailed",
            WorkflowStatus.UNKNOWN_RESULT => "TelegramComposer_UnknownResult",
            WorkflowStatus.SENDING => "TelegramComposer_Submitting",
            WorkflowStatus.RETRY_WAIT => "TelegramComposer_RetryWait",
            WorkflowStatus.PAUSED => "TelegramComposer_Paused",
            WorkflowStatus.CANCELED => "TelegramComposer_Canceled",
            _ => "TelegramPostEditor_StatusDraft"
        };
        string status = LocalizationService.Get(key);
        if (showTime)
            status = $"{target.ScheduledUtc.ToLocalTime():g} · {status}";
        string? details = target.LastErrorDetails ?? target.LastErrorCode;
        if (target.Status == WorkflowStatus.UNKNOWN_RESULT)
            details = string.Join(Environment.NewLine, new[] { details, LocalizationService.Get("TelegramComposer_CheckChannel") }
                .Where(line => !string.IsNullOrWhiteSpace(line)));
        string? link = target.Status == WorkflowStatus.SENT && account != null
            ? TelegramChannelService.BuildMessageLink(account, target.TelegramMessageIds.FirstOrDefault()) : null;
        return new PublicationRow(postTitle == null ? channel : $"{postTitle} · {channel}", status, details, link, target);
    }

    private void OpenPublished_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string link } ||
            !Uri.TryCreate(link, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || uri.Host != "t.me")
            return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ShowWorkspaceError(ex);
        }
    }

    private async void Reschedule_Click(object sender, RoutedEventArgs e)
    {
        if (_publicationBusy || _refreshingQueue || sender is not Button { Tag: PublicationTarget target } ||
            !CanReschedule(target)) return;
        string? url = _settingsService.Settings.TelegramPostEditorServerUrl;
        string? device = _settingsService.Settings.TelegramPostEditorDeviceId;
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(device))
        {
            new DarkDialog(LocalizationService.Get("TelegramComposer_PairRequired")) { Owner = this }.ShowDialog();
            return;
        }

        const string format = "yyyy-MM-dd HH:mm";
        var dialog = new TextPromptDialog(LocalizationService.Get("TelegramComposer_Reschedule"),
            LocalizationService.Format("TelegramComposer_RescheduleTime", TimeZoneInfo.Local.DisplayName),
            target.ScheduledUtc.ToLocalTime().ToString(format, CultureInfo.InvariantCulture)) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        if (!DateTime.TryParseExact(dialog.Value.Trim(), format, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime local)
            || !TryResolveFutureTime(local, out DateTime utc))
        {
            new DarkDialog(LocalizationService.Get("TelegramComposer_FutureTime")) { Owner = this }.ShowDialog();
            return;
        }

        bool fromQueue = QueuePage.Visibility == Visibility.Visible;
        SetPublicationBusy(true);
        try
        {
            var status = await _syncService.RescheduleTargetAsync(url, device, target.ServerId!, utc);
            PublicationSyncService.ApplyServerStatus(target, status);
            _publicationService.SaveTarget(target);
            if (fromQueue)
                ShowQueueRows(_publicationService.GetAllTargets().Where(row => row.ServerId != null)
                    .OrderByDescending(row => row.ScheduledUtc).ToList());
            else
                ShowPublicationResults(scheduled: true);
        }
        catch (Exception ex)
        {
            if (fromQueue)
                QueueStatus.Text = ex.Message;
            else
                ResultNotice.Text = ex.Message;
        }
        finally
        {
            SetPublicationBusy(false);
        }
    }

    private static bool CanReschedule(PublicationTarget target) => target.ServerId != null &&
        target.Status is WorkflowStatus.QUEUED_SERVER or WorkflowStatus.PAUSED or WorkflowStatus.RETRY_WAIT;

    private sealed record PublicationRow(string Title, string Status, string? Details, string? Link, PublicationTarget Target)
    {
        public Visibility LinkVisibility => Link == null ? Visibility.Collapsed : Visibility.Visible;
        public Visibility RescheduleVisibility => CanReschedule(Target) ? Visibility.Visible : Visibility.Collapsed;
    }
}
