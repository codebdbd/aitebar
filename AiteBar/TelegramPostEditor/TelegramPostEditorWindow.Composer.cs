using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AiteBar.TelegramPostEditor.Models;
using AiteBar.TelegramPostEditor.Services;

namespace AiteBar;

public partial class TelegramPostEditorWindow
{
    private bool _hasPublishableContent;
    private bool _previewMediaDirty;
    private FrameworkElement? _workspaceReturnPage;

    private void ShowPage(FrameworkElement page)
    {
        bool editing = page == EditorPage;
        bool results = page == ResultPage;
        EditorPage.Visibility = editing || page == PublishPage || results ? Visibility.Visible : Visibility.Collapsed;
        ComposerDocument.IsEnabled = !results;
        SendPostButton.Visibility = results ? Visibility.Collapsed : Visibility.Visible;
        SidebarDivider.Visibility = page == PublishPage || results ? Visibility.Visible : Visibility.Collapsed;
        DraftsPage.Visibility = page == DraftsPage ? Visibility.Visible : Visibility.Collapsed;
        PublishPage.Visibility = page == PublishPage ? Visibility.Visible : Visibility.Collapsed;
        ResultPage.Visibility = page == ResultPage ? Visibility.Visible : Visibility.Collapsed;
        QueuePage.Visibility = page == QueuePage ? Visibility.Visible : Visibility.Collapsed;
        EditorLocation.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;
        BackNavigationButton.Visibility = editing ? Visibility.Collapsed : Visibility.Visible;
        DraftsNavigationButton.Tag = page == DraftsPage ? "Active" : null;
        QueueNavigationButton.Tag = page == QueuePage ? "Active" : null;
        if (page == PublishPage || results)
        {
            SetPreviewOpen(false);
        }
        UpdatePublicationControls();
        RetryPublicationButton.Visibility = page == ResultPage && _submissionResults.Any(CanRetry)
            ? Visibility.Visible : Visibility.Collapsed;
        if (page != DraftsPage && page != QueuePage)
            _workspaceReturnPage = null;
        if (editing)
            EditorSendError.Text = string.Empty;
    }

    private void ShowWorkspacePage(FrameworkElement page)
    {
        _workspaceReturnPage ??= PublishPage.Visibility == Visibility.Visible ? PublishPage
            : ResultPage.Visibility == Visibility.Visible ? ResultPage : EditorPage;
        ShowPage(page);
    }

    private void BackNavigation_Click(object sender, RoutedEventArgs e)
    {
        if (_publicationBusy || _changingDraft) return;
        FrameworkElement destination = _workspaceReturnPage ?? EditorPage;
        if (destination == PublishPage)
            RefreshChannelsAndBots();
        ShowPage(destination);
        if (destination == EditorPage)
            PostEditorRichTextBox.Focus();
        else
            BackNavigationButton.Focus();
    }

    private async void ShowDrafts_Click(object sender, RoutedEventArgs e)
    {
        if (_publicationBusy || _changingDraft || DraftsPage.Visibility == Visibility.Visible) return;
        await ChangeDraftAsync(() =>
        {
            RefreshPostsList();
            ShowWorkspacePage(DraftsPage);
        }, showEditor: false);
        if (DraftsPage.IsVisible)
            TxtSearchPosts.Focus();
    }

    private async Task ChangeDraftAsync(Action action, bool showEditor = true)
    {
        if (_changingDraft || _publicationBusy) return;
        _changingDraft = true;
        WorkspaceNavigation.IsEnabled = false;
        EditorPage.IsEnabled = false;
        DraftsPage.IsEnabled = false;
        try
        {
            await _autosave.FlushAsync();
            action();
            if (showEditor)
                ShowPage(EditorPage);
        }
        catch (Exception ex)
        {
            ShowWorkspaceError(ex);
        }
        finally
        {
            _changingDraft = false;
            WorkspaceNavigation.IsEnabled = true;
            EditorPage.IsEnabled = true;
            DraftsPage.IsEnabled = true;
        }

        if (showEditor && EditorPage.IsVisible)
            PostEditorRichTextBox.Focus();
    }

    private void DraftSelection_Changed(object sender, SelectionChangedEventArgs? e)
    {
        if (OpenDraftButton != null)
            OpenDraftButton.IsEnabled = LstPosts.SelectedItem is PostDocument;
    }

    private async void OpenSelectedDraft_Click(object sender, RoutedEventArgs e)
    {
        if (LstPosts.SelectedItem is not PostDocument selected) return;
        await ChangeDraftAsync(() =>
        {
            if (selected.Id != _currentPost?.Id)
            {
                PostDocument post = _repository.GetPost(selected.Id)
                    ?? throw new InvalidOperationException(LocalizationService.Get("TelegramComposer_DraftUnavailable"));
                LoadPostIntoEditor(post);
            }
        });
    }

    private void Drafts_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        OpenSelectedDraft_Click(sender, e);
    }

    private void OpenButtonMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { ContextMenu: { } menu } button) return;
        menu.PlacementTarget = button;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void TogglePreview_Click(object sender, RoutedEventArgs e)
    {
        if (PublishPage.Visibility == Visibility.Visible)
            ShowPage(EditorPage);
        bool open = PreviewPane.Visibility != Visibility.Visible;
        SetPreviewOpen(open);
        if (open)
            UpdatePreviewAndValidation();
        PreviewButton.Focus();
    }

    private void SetPreviewOpen(bool open)
    {
        PreviewPane.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        PreviewButton.Tag = open ? "Active" : null;
        string label = LocalizationService.Get(open ? "TelegramComposer_HidePreview" : "TelegramPostEditor_TabPreview");
        PreviewButton.ToolTip = label;
        System.Windows.Automation.AutomationProperties.SetName(PreviewButton, label);
    }

    private void UpdatePreviewAndValidation()
    {
        if (_currentPost == null) return;

        IReadOnlyList<PostMediaItem> media = LstAttachedMedia.ItemsSource as IReadOnlyList<PostMediaItem> ?? [];
        TelegramPreviewModel preview = TelegramPreviewService.BuildPreview(_currentPost, media);
        bool isEmpty = string.IsNullOrWhiteSpace(preview.Content.Text) && _currentPost.Media.Count == 0;
        int limit = _currentPost.Media.Count > 0
            ? TelegramValidationService.MaxCaptionLength
            : TelegramValidationService.MaxTextMessageLength;

        _hasPublishableContent = !isEmpty && preview.Validation.IsValid && !_currentPost.DeletedUtc.HasValue;
        TxtEditorPlaceholder.Visibility = string.IsNullOrWhiteSpace(TelegramDocumentSerializer.ExtractPlainText(_currentPost))
            ? Visibility.Visible : Visibility.Collapsed;
        TxtCharCounter.Text = $"{preview.Content.Text.Length} / {limit}";
        EditorValidation.Text = _currentPost.DeletedUtc.HasValue
            ? LocalizationService.Get("TelegramComposer_RestoreToPublish")
            : isEmpty ? LocalizationService.Get("TelegramPostEditor_EmptyPostHint") : string.Join(Environment.NewLine, preview.Validation.Errors);
        EditorValidation.Foreground = (System.Windows.Media.Brush)FindResource(isEmpty || preview.Validation.IsValid ? "ComposerMuted" : "ComposerError");
        UpdatePublicationControls();

        if (PreviewPane.Visibility != Visibility.Visible) return;
        TxtPreviewChannel.Text = LocalizationService.Get("TelegramComposer_MessagePreview");
        TxtPreviewSubtitle.Text = LocalizationService.Get("TelegramComposer_ApproximatePreview");
        FlowDocument document = TelegramDocumentSerializer.ToFlowDocument(_currentPost);
        document.FontSize = 14;
        document.FontFamily = PostEditorRichTextBox.FontFamily;
        document.TextAlignment = TextAlignment.Left;
        if (_currentPost.IncludePublishTitle && !string.IsNullOrWhiteSpace(_currentPost.PublishTitle))
            document.Blocks.InsertBefore(document.Blocks.FirstBlock!, new Paragraph(new Bold(new Run(_currentPost.PublishTitle.Trim()))));
        if (isEmpty)
            document.Blocks.Add(new Paragraph(new Run(LocalizationService.Get("TelegramPostEditor_PreviewEmpty"))));
        PreviewDocument.Document = document;
        PreviewButtonsList.ItemsSource = preview.Buttons;
        if (_previewMediaDirty)
        {
            PreviewMediaList.ItemsSource = media.Select(CreatePreviewMedia).ToList();
            _previewMediaDirty = false;
        }
    }

    private PreviewAttachment CreatePreviewMedia(PostMediaItem media)
    {
        if (media.Kind != MediaKind.Photo)
            return new PreviewAttachment(media.FileName, null);

        try
        {
            using var file = File.OpenRead(_mediaStore.ResolveAbsolutePath(media.RelativePath));
            var image = new BitmapImage();
            image.BeginInit();
            image.StreamSource = file;
            image.DecodePixelWidth = 260;
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();
            return new PreviewAttachment(media.FileName, image);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or FormatException)
        {
            return new PreviewAttachment(media.FileName, null);
        }
    }

    private void ShowWorkspaceError(Exception ex) => new DarkDialog(ex.Message) { Owner = this }.ShowDialog();

    private sealed record PreviewAttachment(string Name, ImageSource? Image);
}
