

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
                var tt = (TranslateTransform)_draggedButton.RenderTransform;
                tt.X = deltaX;
                tt.Y = deltaY;

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
        
        // Step 1: Collect all buttons except dragged, along with their original index
        var otherButtons = new List<(Button btn, int originalIdx)>();
        for (int i = 0; i < _unifiedButtons.Count; i++)
        {
            if (_unifiedButtons[i] != _draggedButton)
            {
                otherButtons.Add((_unifiedButtons[i], i));
            }
        }
        
        if (otherButtons.Count == 0)
            return _unifiedButtons.Count - 1;
        
        // Step 2: For each possible target index (0 to otherButtons.Count), create a virtual list and see where dragged button would be
        int bestTargetIndex = 0;
        double minDistance = double.MaxValue;
        
        // Get panel info
        var panel = UnifiedButtonsPanel;
        var finalSize = new System.Windows.Size(panel.ActualWidth, panel.ActualHeight);
        Point currentPosInPanel = this.TranslatePoint(currentPos, panel);
        
        for (int testTargetIdx = 0; testTargetIdx <= otherButtons.Count; testTargetIdx++)
        {
            // Build virtual index map
            var virtualIndexMap = new Dictionary<Button, int>();
            int virtIdx = 0;
            for (int i = 0; i < _unifiedButtons.Count; i++)
            {
                if (i == _draggedOriginalIndex) continue;
                
                if (i == testTargetIdx && _draggedOriginalIndex > testTargetIdx)
                {
                    virtualIndexMap[_draggedButton!] = virtIdx++;
                }
                virtualIndexMap[_unifiedButtons[i]] = virtIdx++;
                if (i == testTargetIdx && _draggedOriginalIndex < testTargetIdx)
                {
                    virtualIndexMap[_draggedButton!] = virtIdx++;
                }
            }
            if (!virtualIndexMap.ContainsKey(_draggedButton!))
            {
                virtualIndexMap[_draggedButton!] = virtIdx;
            }
            
            // Get dragged button's virtual position
            int draggedVirtIdx = virtualIndexMap[_draggedButton!];
            var draggedVirtualSlot = panel.GetArrangedRectForIndex(draggedVirtIdx, _unifiedButtons.Count, finalSize);
            var draggedCenter = new Point(
                draggedVirtualSlot.X + draggedVirtualSlot.Width / 2,
                draggedVirtualSlot.Y + draggedVirtualSlot.Height / 2
            );
            
            // Compute distance to current pos (in panel coords)
            double distance = isVertical
                ? Math.Abs(currentPosInPanel.Y - draggedCenter.Y) * 2 + Math.Abs(currentPosInPanel.X - draggedCenter.X)
                : Math.Abs(currentPosInPanel.X - draggedCenter.X) * 2 + Math.Abs(currentPosInPanel.Y - draggedCenter.Y);
                
            if (distance < minDistance)
            {
                minDistance = distance;
                bestTargetIndex = testTargetIdx;
            }
        }
        
        return bestTargetIndex;
    }

    private void UpdateUnifiedReorderPositions(Point currentPos)
    {
        if (_unifiedButtons.Count < 2) return;
        int targetIndex = CalculateUnifiedTargetIndex(currentPos);
        
        // First create the virtual order list (just indexes for lookup)
        var virtualIndexMap = new Dictionary<Button, int>(); // button -> its index in virtual order
        int virtualIdx = 0;
        for (int i = 0; i < _unifiedButtons.Count; i++)
        {
            if (i == _draggedOriginalIndex) continue;
            
            if (i == targetIndex && _draggedOriginalIndex > targetIndex)       
            {
                virtualIndexMap[_draggedButton!] = virtualIdx++;
            }
            
            virtualIndexMap[_unifiedButtons[i]] = virtualIdx++;
            
            if (i == targetIndex && _draggedOriginalIndex < targetIndex)       
            {
                virtualIndexMap[_draggedButton!] = virtualIdx++;
            }
        }
        if (!virtualIndexMap.ContainsKey(_draggedButton!))
        {
            virtualIndexMap[_draggedButton!] = virtualIdx;
        }
        
        // Get the panel's final size
        var panel = UnifiedButtonsPanel;
        var finalSize = new System.Windows.Size(panel.ActualWidth, panel.ActualHeight);
        
        // Now animate each button (except dragged one)
        for (int i = 0; i < _unifiedButtons.Count; i++)
        {
            var btn = _unifiedButtons[i];
            if (btn == _draggedButton) continue;
            
            // Get original layout slot (position relative to panel, no transform)
            var originalSlot = System.Windows.Controls.Primitives.LayoutInformation.GetLayoutSlot(btn);
            
            // Get virtual index and compute virtual slot
            int virtIdx = virtualIndexMap[btn];
            var virtualSlot = panel.GetArrangedRectForIndex(virtIdx, _unifiedButtons.Count, finalSize);
            
            // Compute delta
            double deltaX = virtualSlot.X - originalSlot.X;
            double deltaY = virtualSlot.Y - originalSlot.Y;
            
            // Get render transform
            var tt = (TranslateTransform)btn.RenderTransform;
            
            // Animate
            if (Math.Abs(tt.X - deltaX) > 0.1 || Math.Abs(tt.Y - deltaY) > 0.1)
            {
                var animX = new DoubleAnimation(deltaX, TimeSpan.FromMilliseconds(Constants.AnimationSlideMs))
                {
                    EasingFunction = EasingHelper.DefaultEasing
                };
                var animY = new DoubleAnimation(deltaY, TimeSpan.FromMilliseconds(Constants.AnimationSlideMs))
                {
                    EasingFunction = EasingHelper.DefaultEasing
                };
                tt.BeginAnimation(TranslateTransform.XProperty, animX);        
                tt.BeginAnimation(TranslateTransform.YProperty, animY);        
            }
        }
    }
}
