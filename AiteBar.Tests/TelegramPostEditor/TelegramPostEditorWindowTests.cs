using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Threading;
using AiteBar.TelegramPostEditor.Models;
using AiteBar.TelegramPostEditor.Persistence;
using AiteBar.TelegramPostEditor.Services;

namespace AiteBar.Tests.TelegramPostEditor;

[Collection("WpfTestCollection")]
[Trait("Category", "WPF")]
public sealed class TelegramPostEditorWindowTests
{
    [Fact]
    public async Task Composer_ReviewsExplicitChannelsAndRetriesOnlyFailedDelivery()
    {
        Exception? captured = null;
        var tcs = new TaskCompletionSource();
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            try
            {
                string tempRoot = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempRoot);
                try
                {
                    var settings = new AppSettingsService(
                        Path.Combine(tempRoot, "buttons.json"),
                        Path.Combine(tempRoot, "settings.json"));
                    var migrator = new PostDbMigrator(Path.Combine(tempRoot, "posts.db"));
                    var secrets = new TelegramSecretStore(new TelegramPublishingTests.InMemoryCredentialStore());
                    int sends = 0;
                    using var http = new HttpClient(new TelegramPublishingTests.StubHttpHandler(_ =>
                    {
                        sends++;
                        return sends == 2
                            ? new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = JsonContent.Create(new { ok = false, error_code = 403 }) }
                            : new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { ok = true, result = new { message_id = sends } }) };
                    }));
                    var client = new TelegramBotClient(http);
                    var channels = new TelegramChannelService(migrator, client, secrets);
                    const string botId = "bot";
                    channels.SaveBot(new BotAccount { Id = botId, DisplayName = "Test", TokenSecretRef = secrets.SaveBotToken(botId, "0:fake") });
                    channels.SaveChannel(new ChannelAccount { BotId = botId, DisplayName = "News", Username = "news", TelegramChatId = -101 });
                    channels.SaveChannel(new ChannelAccount { BotId = botId, DisplayName = "Test", Username = "test", TelegramChatId = -202, IsTestChannel = true });

                    var writer = new PostInstructionAndAiTests.FakeTelegramAiGateway();
                    const string serverUrl = "http://127.0.0.1:18000";
                    int scheduleRequests = 0;
                    using var scheduler = new HttpClient(new TelegramPublishingTests.StubHttpHandler(request =>
                    {
                        if (request.Method != HttpMethod.Post)
                            return new HttpResponseMessage(HttpStatusCode.OK);

                        scheduleRequests++;
                        using var body = JsonDocument.Parse(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
                        int revision = body.RootElement.GetProperty("revision").GetInt32();
                        var targets = body.RootElement.GetProperty("targets").EnumerateArray().Select(item => new
                        {
                            targetId = item.GetProperty("targetId").GetString(),
                            channelId = item.GetProperty("channelId").GetString(),
                            scheduledAtUtc = item.GetProperty("scheduledAtUtc").GetDateTime(),
                            status = "QUEUED_SERVER",
                            revision
                        }).ToArray();
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = JsonContent.Create(new { serverPublicationId = "job", targets })
                        };
                    }));
                    var window = new TelegramPostEditorWindow(settings, null, new AiGateway(settings), migrator, secrets, client, writer, scheduler);
                    Assert.Equal(0, window.CmbInstruction.SelectedIndex);
                    var selectedInstruction = Assert.IsType<PostInstruction>(window.CmbInstruction.SelectedItem);
                    Assert.Equal(string.Empty, selectedInstruction.Id);

                    Assert.Equal(Enum.GetValues<PostAiAction>().Length, window.CmbAiAction.Items.Count);
                    var selectedAction = Assert.IsType<TelegramPostEditorWindow.PostAiActionItem>(window.CmbAiAction.SelectedItem);
                    Assert.Equal(PostAiAction.Improve, selectedAction.Action);
                    Assert.False(string.IsNullOrWhiteSpace(selectedAction.DisplayName));

                    Assert.Equal(Visibility.Visible, window.EditorPage.Visibility);
                    Assert.Equal(Visibility.Collapsed, window.BackNavigationButton.Visibility);
                    Assert.True(window.DraftActionsButton.ContextMenu.OverridesDefaultStyle);
                    foreach (Button formatButton in new[] { window.BoldButton, window.ItalicButton, window.UnderlineButton,
                        window.StrikethroughButton, window.SpoilerButton, window.CodeButton, window.QuoteButton })
                    {
                        Assert.Equal(Visibility.Visible, formatButton.Visibility);
                        Assert.Null(formatButton.ContextMenu);
                        Assert.False(string.IsNullOrWhiteSpace(System.Windows.Automation.AutomationProperties.GetName(formatButton)));
                    }
                    Assert.Equal(Visibility.Collapsed, window.PreviewPane.Visibility);
                    Assert.False(window.SendPostButton.IsEnabled);
                    Assert.True(window.ReviewButton.IsEnabled);

                    window.AiOptionsPanel.Visibility = Visibility.Visible;
                    window.AiPromptBox.Text = "Announce the new release";
                    Click(window.AiDraftButton);
                    WaitFor(() => window.ApplyAiButton.IsEnabled);
                    Assert.Contains("Announce the new release", writer.LastRequest!.Messages[^1].Content);
                    Click(window.ApplyAiButton);
                    WaitFor(() => window.AiSuggestionPanel.Visibility == Visibility.Collapsed);
                    Assert.Contains("Improved output", ReadEditor(window));

                    var excerpt = new Run("Edit this");
                    var followingText = new Run(" after");
                    var paragraph = new Paragraph();
                    paragraph.Inlines.Add(new Run("Before "));
                    paragraph.Inlines.Add(excerpt);
                    paragraph.Inlines.Add(followingText);
                    window.PostEditorRichTextBox.Document.Blocks.Clear();
                    window.PostEditorRichTextBox.Document.Blocks.Add(paragraph);
                    window.PostEditorRichTextBox.Selection.Select(excerpt.ContentStart, excerpt.ContentEnd);
                    window.AiPromptBox.Clear();
                    Click(window.AiShortenButton);
                    WaitFor(() => window.ApplyAiButton.IsEnabled);
                    string prompt = writer.LastRequest!.Messages[^1].Content;
                    Assert.Contains("Edit this", prompt);
                    Assert.DoesNotContain("Before", prompt);
                    Assert.DoesNotContain(" after", prompt);
                    Click(window.ApplyAiButton);
                    WaitFor(() => window.AiSuggestionPanel.Visibility == Visibility.Collapsed);
                    Assert.Contains("Before Improved output after", ReadEditor(window));

                    window.PostEditorRichTextBox.Selection.Select(
                        window.PostEditorRichTextBox.Document.ContentStart, window.PostEditorRichTextBox.Document.ContentStart);
                    Click(window.AiRewriteButton);
                    WaitFor(() => window.ApplyAiButton.IsEnabled);
                    window.PostEditorRichTextBox.AppendText(" Modified by hand.");
                    Click(window.ApplyAiButton);
                    WaitFor(() => !window.ApplyAiButton.IsEnabled);
                    Assert.Contains("Modified by hand.", ReadEditor(window));
                    window.AiOptionsPanel.Visibility = Visibility.Collapsed;

                    window.PostEditorRichTextBox.Document.Blocks.Clear();
                    window.PostEditorRichTextBox.Document.Blocks.Add(new Paragraph(new Bold(new Run("Post text"))));
                    Assert.True(window.SendPostButton.IsEnabled);
                    Click(window.PreviewButton);
                    Assert.Equal(Visibility.Visible, window.PreviewPane.Visibility);
                    Assert.Equal(FontWeights.Bold, Assert.IsType<Run>(
                        Assert.IsType<Paragraph>(window.PreviewDocument.Document.Blocks.FirstBlock).Inlines.FirstInline).FontWeight);

                    var editorDocument = window.PostEditorRichTextBox.Document;
                    Click(window.DraftsNavigationButton);
                    WaitFor(() => window.DraftsPage.Visibility == Visibility.Visible && window.WorkspaceNavigation.IsEnabled);
                    Assert.Equal(Visibility.Visible, window.BackNavigationButton.Visibility);
                    Click(window.QueueNavigationButton);
                    Assert.Equal(Visibility.Visible, window.QueuePage.Visibility);
                    Click(window.BackNavigationButton);
                    Assert.Equal(Visibility.Visible, window.EditorPage.Visibility);
                    Assert.Same(editorDocument, window.PostEditorRichTextBox.Document);

                    Click(window.SendPostButton);
                    Assert.Equal(Visibility.Visible, window.PublishPage.Visibility);
                    Assert.Equal(Visibility.Visible, window.EditorPage.Visibility);
                    Assert.Same(editorDocument, window.PostEditorRichTextBox.Document);
                    Assert.False(window.SendPostButton.IsEnabled);
                    Assert.Equal(0, sends);
                    foreach (object channel in (IEnumerable)window.LstChannels.ItemsSource)
                    {
                        var row = Assert.IsType<Border>(window.LstChannels.ItemTemplate.LoadContent());
                        var checkBox = Assert.IsType<CheckBox>(row.Child);
                        checkBox.DataContext = channel;
                        checkBox.IsChecked = true;
                        checkBox.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                    }
                    Assert.Contains("@news", window.TxtSelectedChannelsSummary.Text);
                    Assert.Contains("@test", window.TxtSelectedChannelsSummary.Text);
                    Assert.True(window.SendPostButton.IsEnabled);
                    Click(window.QueueNavigationButton);
                    Click(window.BackNavigationButton);
                    Assert.Equal(Visibility.Visible, window.PublishPage.Visibility);
                    Assert.Contains("@news", window.TxtSelectedChannelsSummary.Text);
                    Assert.Contains("@test", window.TxtSelectedChannelsSummary.Text);
                    Click(window.BackNavigationButton);
                    Assert.Equal(Visibility.Visible, window.EditorPage.Visibility);
                    Assert.Same(editorDocument, window.PostEditorRichTextBox.Document);
                    Assert.Contains("@news", window.EditorRecipients.Text);
                    Assert.Contains("@test", window.EditorRecipients.Text);
                    Click(window.WhenButton);
                    Assert.Equal(Visibility.Visible, window.TimingSection.Visibility);
                    Assert.True(window.SendPostButton.IsEnabled);
                    window.ScheduleOption.IsChecked = true;
                    Assert.Equal(Visibility.Visible, window.ScheduleFields.Visibility);
                    Assert.False(window.SendPostButton.IsEnabled);
                    window.SendNowOption.IsChecked = true;
                    Click(window.BackNavigationButton);
                    Click(window.SendPostButton);
                    WaitFor(() => window.ResultPage.Visibility == Visibility.Visible && window.RetryPublicationButton.IsEnabled);
                    Assert.Equal(2, sends);
                    Assert.Equal(Visibility.Visible, window.RetryPublicationButton.Visibility);
                    Click(window.QueueNavigationButton);
                    Assert.Equal(Visibility.Collapsed, window.RetryPublicationButton.Visibility);
                    Click(window.BackNavigationButton);
                    Assert.Equal(Visibility.Visible, window.ResultPage.Visibility);
                    Click(window.RetryPublicationButton);
                    WaitFor(() => window.RetryPublicationButton.Visibility == Visibility.Collapsed);
                    Assert.Equal(3, sends);
                    Click(window.BackNavigationButton);
                    Assert.Equal(Visibility.Visible, window.EditorPage.Visibility);
                    settings.UpdateSettings(app =>
                    {
                        app.TelegramPostEditorDeviceId = "device";
                        app.TelegramPostEditorServerUrl = serverUrl;
                    });
                    secrets.SaveServerToken("device", "token");
                    window.TxtServerUrl.Text = serverUrl;
                    Click(window.WhenButton);
                    window.ScheduleDate.SelectedDate = DateTime.Now.Date.AddDays(2);
                    window.ScheduleTime.Text = "09:00";
                    window.ScheduleOption.IsChecked = true;
                    Assert.Equal(LocalizationService.Get("TelegramPostEditor_Schedule"), window.SendPostButton.Content);
                    Click(window.BackNavigationButton);
                    Click(window.SendPostButton);
                    WaitFor(() => window.ResultPage.Visibility == Visibility.Visible && window.WorkspaceNavigation.IsEnabled);
                    Assert.Equal(1, scheduleRequests);
                    Assert.Equal(3, sends);
                    bool closed = false;
                    window.Closed += (_, _) => closed = true;
                    window.Close();
                    WaitFor(() => closed);
                }
                finally
                {
                    if (Directory.Exists(tempRoot))
                    {
                        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                        Directory.Delete(tempRoot, recursive: true);
                    }
                }
            }
            catch (Exception ex)
            {
                captured = ex;
            }
            finally
            {
                tcs.SetResult();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await tcs.Task;

        Assert.Null(captured);
    }

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

    private static string ReadEditor(TelegramPostEditorWindow window) =>
        new TextRange(window.PostEditorRichTextBox.Document.ContentStart, window.PostEditorRichTextBox.Document.ContentEnd).Text;

    private static void WaitFor(Func<bool> condition)
    {
        if (condition()) return;
        var frame = new DispatcherFrame();
        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(20) };
        timer.Tick += (_, _) => frame.Continue = !condition() && DateTime.UtcNow < deadline;
        timer.Start();
        Dispatcher.PushFrame(frame);
        timer.Stop();
        Assert.True(condition());
    }
}
