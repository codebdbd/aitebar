

namespace AiteBar;

public partial class MainWindow
{
    private Button? _draggedButton = null;
    private Point _dragStartPos;
    private bool _isReordering = false;
    private int _draggedOriginalIndex;
    private Button? _suppressUserButtonClickFor;

    private Button CreateUnifiedButton(UnifiedButton item, int panelVersion)
    {
        var btn = CreatePanelButton(string.Empty, item.Name, async (s, e) =>
        {
            if (ReferenceEquals(s, _suppressUserButtonClickFor))
            {
                _suppressUserButtonClickFor = null;
                return;
            }
            await ExecuteUnifiedButtonActionAsync(item);
        }, GetCachedBrush(item.Color));

        btn.RenderTransform = new TranslateTransform();
        btn.Tag = item.Id;

        // Drag-and-drop handlers
        btn.PreviewMouseDown += (s, e) =>
        {
            if (e.ChangedButton != MouseButton.Left) return;
            _draggedButton = s as Button;
            _dragStartPos = e.GetPosition(this);
            _isReordering = false;
            _draggedOriginalIndex = _unifiedButtons.IndexOf(_draggedButton!);
        };

        btn.PreviewMouseMove += (s, e) =>
        {
            if (_draggedButton == null || e.LeftButton != MouseButtonState.Pressed) return;

            Point currentPos = e.GetPosition(this);
            double deltaX = currentPos.X - _dragStartPos.X;
            double deltaY = currentPos.Y - _dragStartPos.Y;

            if (!_isReordering && (Math.Abs(deltaX) > 10 || Math.Abs(deltaY) > 10))
            {
                _isReordering = true;
                _draggedButton.Opacity = 0.7;
                Panel.SetZIndex(_draggedButton, 100);
                _draggedButton.CaptureMouse();
            }

            if (_isReordering)
            {
                bool isVertical = AppSettings.Edge == DockEdge.Left || AppSettings.Edge == DockEdge.Right;
                var tt = (TranslateTransform)_draggedButton.RenderTransform;
                if (isVertical) tt.Y = deltaY; else tt.X = deltaX;

                UpdateUnifiedReorderPositions(currentPos);
            }
        };

        btn.PreviewMouseUp += async (s, e) =>
        {
            if (_draggedButton == null) return;

            if (_isReordering)
            {
                if (_draggedButton.IsMouseCaptured)
                {
                    _draggedButton.ReleaseMouseCapture();
                }

                _suppressUserButtonClickFor = _draggedButton;
                e.Handled = true;
                _draggedButton.Opacity = 1.0;
                int newIndex = CalculateUnifiedTargetIndex(e.GetPosition(this));
                if (newIndex >= 0 && newIndex < _currentUnifiedButtons.Count && newIndex != _draggedOriginalIndex)
                {
                    // Reorder logic
                    var draggedItem = _currentUnifiedButtons[_draggedOriginalIndex];

                    if (draggedItem.Type == UnifiedButtonType.User)
                    {
                        // Reorder user elements in current context
                        var contextId = AppSettings.ActiveContextId;
                        // Calculate original index within context-specific user elements
                        var contextUserElements = _settingsService.Elements.Where(el => el.ContextId == contextId).ToList();
                        var originalUserIndex = contextUserElements.FindIndex(el => el.Id == draggedItem.Id);
                        // Calculate new index within context-specific user elements
                        var targetItemInNewIndex = _currentUnifiedButtons[newIndex];
                        if (targetItemInNewIndex.Type == UnifiedButtonType.User)
                        {
                            var targetUserElement = contextUserElements.FirstOrDefault(el => el.Id == targetItemInNewIndex.Id);
                            if (targetUserElement != null)
                            {
                                var newUserIndex = contextUserElements.IndexOf(targetUserElement);
                                _settingsService.ReorderElements(originalUserIndex, newUserIndex, contextId);
                                await SaveSettingsWithNotificationAsync();
                            }
                        }
                    }
                    else if (draggedItem.Type == UnifiedButtonType.Utility)
                    {
                        // Reorder utility buttons
                        var visibleUtilityIds = _currentUnifiedButtons
                            .Where(b => b.Type == UnifiedButtonType.Utility)
                            .Select(b => b.Id)
                            .ToList();

                        // Find original index in visibleUtilityIds
                        int originalVisibleIndex = visibleUtilityIds.IndexOf(draggedItem.Id);
                        if (originalVisibleIndex < 0)
                        {
                            // Button not found in visible list, skip
                            _draggedButton = null;
                            _isReordering = false;
                            return;
                        }

                        // Remove the dragged one from current position
                        visibleUtilityIds.RemoveAt(originalVisibleIndex);

                        // Determine where to insert in visible utility list
                        int insertIndexInVisible = 0;
                        // Iterate through compressed newIndex, map to actual index (skip dragged button)
                        for (int compressedIndex = 0; compressedIndex < newIndex; compressedIndex++)
                        {
                            int actualIndex = compressedIndex >= _draggedOriginalIndex ? compressedIndex + 1 : compressedIndex;
                            if (_currentUnifiedButtons[actualIndex].Type == UnifiedButtonType.Utility)
                            {
                                insertIndexInVisible++;
                            }
                        }

                        // Insert the dragged one in the new position
                        visibleUtilityIds.Insert(insertIndexInVisible, draggedItem.Id);

                        // Now build the full UtilityButtonOrder including possibly hidden ones
                        // Get settings from service (clone)
                        var settings = _settingsService.Settings;

                        var fullOrder = new List<string>();
                        foreach (var id in settings.UtilityButtonOrder)
                        {
                            if (id != draggedItem.Id)
                            {
                                fullOrder.Add(id);
                            }
                        }

                        // Merge visible order with full order, keeping hidden ones in their original positions
                        var finalOrder = new List<string>();
                        int visibleIndex = 0;
                        foreach (var id in fullOrder)
                        {
                            if (visibleUtilityIds.Contains(id))
                            {
                                // If it's a visible button, take the next from visible order
                                finalOrder.Add(visibleUtilityIds[visibleIndex]);
                                visibleIndex++;
                            }
                            else
                            {
                                // Keep hidden buttons in their original position
                                finalOrder.Add(id);
                            }
                        }

                        // Add any remaining visible buttons at the end
                        while (visibleIndex < visibleUtilityIds.Count)
                        {
                            finalOrder.Add(visibleUtilityIds[visibleIndex]);
                            visibleIndex++;
                        }

                        // Update settings clone and save it to service
                        settings.UtilityButtonOrder = finalOrder;
                        _settingsService.Settings = settings;
                        await SaveSettingsWithNotificationAsync();
                    }
                }
                RefreshPanel();
            }

            _draggedButton = null; _isReordering = false;
        };

        btn.MouseRightButtonUp += (s, e) =>
        {
            btn.ContextMenu = BuildUnifiedButtonContextMenu(item);
        };

        ApplyUnifiedButtonIcon(btn, item, panelVersion);
        return btn;
    }

    private int CalculateUnifiedTargetIndex(Point currentPos)
    {
        bool isVertical = AppSettings.Edge == DockEdge.Left || AppSettings.Edge == DockEdge.Right;
        for (int i = 0; i < _unifiedButtons.Count; i++)
        {
            if (_unifiedButtons[i] == _draggedButton) continue;
            var pos = _unifiedButtons[i].TransformToAncestor(this).Transform(new Point(0, 0));
            var size = new System.Windows.Size(_unifiedButtons[i].ActualWidth, _unifiedButtons[i].ActualHeight);
            if (isVertical)
            {
                if (currentPos.Y < pos.Y + size.Height / 2) return i > _draggedOriginalIndex ? i - 1 : i;
            }
            else
            {
                if (currentPos.X < pos.X + size.Width / 2) return i > _draggedOriginalIndex ? i - 1 : i;
            }
        }
        return _unifiedButtons.Count - 1;
    }

    private void UpdateUnifiedReorderPositions(Point currentPos)
    {
        if (_unifiedButtons.Count < 2) return;
        int targetIndex = CalculateUnifiedTargetIndex(currentPos);
        bool isVertical = AppSettings.Edge == DockEdge.Left || AppSettings.Edge == DockEdge.Right;

        var buttonMargin = _unifiedButtons[0].Margin;
        double offset = isVertical
            ? _unifiedButtons[0].ActualHeight + buttonMargin.Top + buttonMargin.Bottom
            : _unifiedButtons[0].ActualWidth + buttonMargin.Left + buttonMargin.Right;

        for (int i = 0; i < _unifiedButtons.Count; i++)
        {
            if (_unifiedButtons[i] == _draggedButton) continue;

            double targetOffset = 0;
            if (_draggedOriginalIndex < targetIndex)
            {
                if (i > _draggedOriginalIndex && i <= targetIndex) targetOffset = -offset;
            }
            else if (_draggedOriginalIndex > targetIndex)
            {
                if (i >= targetIndex && i < _draggedOriginalIndex) targetOffset = offset;
            }

            var tt = (TranslateTransform)_unifiedButtons[i].RenderTransform;
            double currentOffset = isVertical ? tt.Y : tt.X;

            if (Math.Abs(currentOffset - targetOffset) > 0.1)
            {
                var anim = new DoubleAnimation(targetOffset, TimeSpan.FromMilliseconds(Constants.AnimationSlideMs))
                {
                    EasingFunction = EasingHelper.DefaultEasing
                };
                tt.BeginAnimation(isVertical ? TranslateTransform.YProperty : TranslateTransform.XProperty, anim);
            }
        }
    }
}
