using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiteBar.TelegramPostEditor;
using AiteBar.TelegramPostEditor.Models;
using AiteBar.TelegramPostEditor.Services;

namespace AiteBar.Tests.TelegramPostEditor;

[Collection("WpfTestCollection")]
[Trait("Category", "WPF")]
public sealed class TelegramPostEditorWindowTests
{
    [Fact]
    public async Task TelegramPostEditorWindow_ConstructsWithoutException()
    {
        Exception? captured = null;
        var tcs = new TaskCompletionSource();
        var thread = new Thread(() =>
        {
            try
            {
                string tempRoot = Path.Combine(Path.GetTempPath(), "AiteBarTests", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempRoot);
                try
                {
                    var settings = new AppSettingsService(
                        Path.Combine(tempRoot, "buttons.json"),
                        Path.Combine(tempRoot, "settings.json"));
                    var window = new TelegramPostEditorWindow(settings, null, new AiGateway(settings));
                    Assert.Equal(0, window.CmbInstruction.SelectedIndex);
                    var selectedInstruction = Assert.IsType<PostInstruction>(window.CmbInstruction.SelectedItem);
                    Assert.Equal(string.Empty, selectedInstruction.Id);

                    Assert.Equal(Enum.GetValues<PostAiAction>().Length, window.CmbAiAction.Items.Count);
                    var selectedAction = Assert.IsType<TelegramPostEditorWindow.PostAiActionItem>(window.CmbAiAction.SelectedItem);
                    Assert.Equal(PostAiAction.Improve, selectedAction.Action);
                    Assert.False(string.IsNullOrWhiteSpace(selectedAction.DisplayName));
                    window.Close();
                }
                finally
                {
                    if (Directory.Exists(tempRoot))
                    {
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
}
