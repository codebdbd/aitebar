

namespace AiteBar;

public partial class MainWindow
{
    private void RootBorder_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        SetPanelInputMode(PanelInputMode.Pointer, clearFocus: true);

        if (RootBorder.IsMouseCaptured)
        {
            RootBorder.ReleaseMouseCapture();
        }
    }

    private async void RootBorder_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        try
        {
            if (_isAnimating || !_shown) return;
            if (e.Delta == 0) return;

            e.Handled = true;
            SetPanelInputMode(PanelInputMode.Pointer, clearFocus: true);
            CaptureMouseForWheel();

            DateTime now = DateTime.UtcNow;
            if (now - _lastContextWheelSwitchUtc < ContextWheelSwitchCooldown)
            {
                return;
            }

            if (_contextWheelDelta != 0 && Math.Sign(_contextWheelDelta) != Math.Sign(e.Delta))
            {
                _contextWheelDelta = 0;
            }

            _contextWheelDelta += e.Delta;
            if (Math.Abs(_contextWheelDelta) < WheelDeltaPerContextSwitch)
            {
                return;
            }

            int direction = _contextWheelDelta > 0 ? -1 : 1;
            _contextWheelDelta = 0;
            _lastContextWheelSwitchUtc = now;
            await SwitchActiveContextAsync(direction);
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
        }
    }

    private void CaptureMouseForWheel()
    {
        if (!RootBorder.IsMouseCaptured)
        {
            RootBorder.CaptureMouse();
        }
        int currentToken = ++_mouseWheelCaptureToken;
        _ = ReleaseMouseCaptureAfterDelayAsync(currentToken);
    }

    private async Task ReleaseMouseCaptureAfterDelayAsync(int captureToken)
    {
        try
        {
            await Task.Delay(500);
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
            {
                return;
            }

            await Dispatcher.InvokeAsync(() =>
            {
                if (_mouseWheelCaptureToken == captureToken)
                {
                    if (RootBorder.IsMouseCaptured)
                    {
                        RootBorder.ReleaseMouseCapture();
                    }
                }
            });
        }
        catch (Exception ex)
        {
            _ = Logger.LogAsync(ex);
        }
    }

    private void Border_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = CanAcceptDropData(e.Data) ? DragDropEffects.Link : DragDropEffects.None;
        e.Handled = true;
    }

    private static bool CanAcceptDropData(System.Windows.IDataObject data)
    {
        return TryGetDropTarget(data, out _, out _, out _);
    }

    private static bool TryGetDropTarget(System.Windows.IDataObject data, out string? value, out ActionType type, out string? errorMessage)
    {
        value = null;
        type = ActionType.Web;
        errorMessage = null;

        if (data.GetDataPresent(DataFormats.FileDrop))
        {
            var droppedItems = data.GetData(DataFormats.FileDrop) as string[];
            if (droppedItems == null || droppedItems.Length == 0)
            {
                errorMessage = LocalizationService.Get("Drop_ReadFailed");
                return false;
            }

            if (droppedItems.Length > 1)
            {
                errorMessage = LocalizationService.Get("Drop_OnlyOne");
                return false;
            }

            string candidate = droppedItems[0];
            if (Directory.Exists(candidate))
            {
                value = candidate;
                type = ActionType.Folder;
                return true;
            }

            if (!File.Exists(candidate))
            {
                errorMessage = LocalizationService.Get("Drop_ExistingFilesFoldersOnly");
                return false;
            }

            string extension = Path.GetExtension(candidate).ToLowerInvariant();
            if (extension == ".url")
            {
                try
                {
                    var lines = File.ReadAllLines(candidate);
                    var urlLine = lines.FirstOrDefault(line => line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrWhiteSpace(urlLine))
                    {
                        string url = urlLine[4..].Trim();
                        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                        {
                            value = url;
                            type = ActionType.Web;
                            return true;
                        }
                    }
                }
                catch (Exception ex) { Logger.Log(ex); }

                errorMessage = LocalizationService.Get("Drop_UrlShortcutOnly");
                return false;
            }

            if (ActionTargetHelper.IsScriptPath(candidate))
            {
                value = candidate;
                type = ActionType.ScriptFile;
                return true;
            }

            if (ActionTargetHelper.IsProgramPath(candidate))
            {
                value = candidate;
                type = ActionType.Program;
                return true;
            }

            value = candidate;
            type = ActionType.File;
            return true;
        }

        string? text = null;
        if (data.GetDataPresent(DataFormats.UnicodeText))
            text = (data.GetData(DataFormats.UnicodeText) as string)?.Trim();
        else if (data.GetDataPresent(DataFormats.Text))
            text = (data.GetData(DataFormats.Text) as string)?.Trim();

        if (string.IsNullOrWhiteSpace(text))
        {
            errorMessage = LocalizationService.Get("Drop_FileFolderOrUrlOnly");
            return false;
        }

        if (Uri.TryCreate(text, UriKind.Absolute, out var textUri) &&
            (textUri.Scheme == Uri.UriSchemeHttp || textUri.Scheme == Uri.UriSchemeHttps))
        {
            value = text;
            type = ActionType.Web;
            return true;
        }

        errorMessage = LocalizationService.Get("Drop_SupportedTargets");
        return false;
    }

    private async void Border_Drop(object sender, DragEventArgs e)
    {
        try
        {
            if (!TryGetDropTarget(e.Data, out string? val, out ActionType type, out string? errorMessage))
            {
                new DarkDialog(errorMessage ?? LocalizationService.Get("Drop_NotSupported")) { Owner = this }.ShowDialog();
                return;
            }

            if (!string.IsNullOrEmpty(val))
            {
                string? iconPath = null;
                bool isWeb = val.StartsWith("http", StringComparison.OrdinalIgnoreCase) || val.StartsWith("www.", StringComparison.OrdinalIgnoreCase);

                if (isWeb && !val.StartsWith("http", StringComparison.OrdinalIgnoreCase)) val = "https://" + val;

                if (type == ActionType.Program || type == ActionType.ScriptFile || type == ActionType.File)
                    iconPath = IconHelper.ExtractAndSaveIcon(val);

                var newElement = new CustomElement
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = isWeb ? (Uri.TryCreate(val, UriKind.Absolute, out var uri) ? uri.Host : val) : Path.GetFileNameWithoutExtension(val),
                    ActionValue = val,
                    ActionType = type.ToString(),
                    ImagePath = iconPath ?? "",
                    Browser = isWeb ? BrowserHelper.GetSystemDefaultBrowser() : BrowserType.Chrome,
                    ContextId = AppSettings.ActiveContextId
                };

                await SaveElement(newElement);
            }
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            new DarkDialog(LocalizationService.Format("Action_Failed", ex.Message)) { Owner = this }.ShowDialog();
        }
    }

    private async Task UpdateDownloadedFaviconAsync(string elementId, string actionValue, string webIcon)
    {
        try
        {
            bool updated = false;
            await _settingsService.UpdateElementAsync(elementId, element =>
            {
                if (!string.Equals(element.ActionType, nameof(ActionType.Web), StringComparison.Ordinal) ||
                    !string.Equals(element.ActionValue, actionValue, StringComparison.Ordinal) ||
                    !string.IsNullOrEmpty(element.ImagePath) ||
                    (!string.IsNullOrEmpty(element.Icon) && element.Icon != "\uF45B"))
                {
                    return;
                }

                element.ImagePath = webIcon;
                updated = true;
            });

            if (updated)
            {
                RefreshPanel();
            }
        }
        catch (Exception ex)
        {
            _ = Logger.LogAsync(ex);
        }
    }
}
