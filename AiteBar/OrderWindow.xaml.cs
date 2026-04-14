using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Windows;

using ComboBoxItem = System.Windows.Controls.ComboBoxItem;
using ListBoxItem = System.Windows.Controls.ListBoxItem;
using Orientation = System.Windows.Controls.Orientation;
using StackPanel = System.Windows.Controls.StackPanel;
using TextBlock = System.Windows.Controls.TextBlock;
using Button = System.Windows.Controls.Button;

namespace AiteBar
{
    [SupportedOSPlatform("windows6.1")]
    public partial class OrderWindow : DarkWindow
    {
        private readonly MainWindow _mainWindow;
        private List<CustomElement> _snapshot = new();

        public OrderWindow(MainWindow mainWindow)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            ReloadSnapshot();
            CmbOrderBlock.SelectedIndex = 0;
            RefreshOrderList();
        }

        private void ReloadSnapshot()
        {
            _snapshot = _mainWindow.GetElementsSnapshot().ToList();
        }

        private void CmbOrderBlock_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            RefreshOrderList();
        }

        private void RefreshOrderList()
        {
            if (CmbOrderBlock.SelectedItem is not ComboBoxItem blockItem)
                return;

            int blockId = int.Parse(blockItem.Tag?.ToString() ?? "4");
            var blockElements = _snapshot.Where(x => x.BlockId == blockId).ToList();

            LstOrderButtons.Items.Clear();
            foreach (var el in blockElements)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal };
                
                if (!string.IsNullOrEmpty(el.ImagePath) && System.IO.File.Exists(el.ImagePath))
                {
                    var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(el.ImagePath);
                    bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bitmap.EndInit();

                    row.Children.Add(new System.Windows.Controls.Image
                    {
                        Source = bitmap,
                        Width = 16, Height = 16,
                        Margin = new Thickness(0, 0, 8, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    });
                }
                else
                {
                    row.Children.Add(new TextBlock
                    {
                        Text = el.Icon,
                        FontFamily = FontHelper.Resolve(el.IconFont),
                        Margin = new Thickness(0, 0, 8, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    });
                }

                row.Children.Add(new TextBlock
                {
                    Text = el.Name,
                    VerticalAlignment = VerticalAlignment.Center
                });

                LstOrderButtons.Items.Add(new ListBoxItem { Content = row, Tag = el.Id });
            }

            if (LstOrderButtons.Items.Count > 0)
                LstOrderButtons.SelectedIndex = 0;
        }

        private void BtnMoveUp_Click(object sender, RoutedEventArgs e)
        {
            MoveSelected(-1);
        }

        private void BtnMoveDown_Click(object sender, RoutedEventArgs e)
        {
            MoveSelected(1);
        }

        private void MoveSelected(int direction)
        {
            if (CmbOrderBlock.SelectedItem is not ComboBoxItem blockItem || LstOrderButtons.SelectedItem is not ListBoxItem selected)
                return;

            int blockId = int.Parse(blockItem.Tag?.ToString() ?? "4");
            string selectedId = selected.Tag?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(selectedId))
                return;

            var blockIds = _snapshot.Where(x => x.BlockId == blockId).Select(x => x.Id).ToList();
            int index = blockIds.FindIndex(x => x == selectedId);
            if (index < 0)
                return;

            int targetIndex = index + direction;
            if (targetIndex < 0 || targetIndex >= blockIds.Count)
                return;

            (blockIds[index], blockIds[targetIndex]) = (blockIds[targetIndex], blockIds[index]);

            var byId = _snapshot
                .Where(x => x.BlockId == blockId)
                .ToDictionary(x => x.Id, x => x, StringComparer.Ordinal);

            var reordered = blockIds.Select(id => byId[id]).ToList();
            var result = new List<CustomElement>(_snapshot.Count);
            bool inserted = false;

            foreach (var item in _snapshot)
            {
                if (item.BlockId == blockId)
                {
                    if (!inserted)
                    {
                        result.AddRange(reordered);
                        inserted = true;
                    }
                    continue;
                }

                result.Add(item);
            }

            _snapshot = result;
            RefreshOrderList();
            LstOrderButtons.SelectedIndex = targetIndex;
        }

        private async void BtnSaveOrder_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null)
                button.IsEnabled = false;

            try
            {
                if (CmbOrderBlock.SelectedItem is not ComboBoxItem blockItem)
                    return;

                int blockId = int.Parse(blockItem.Tag?.ToString() ?? "4");
                var orderedIds = _snapshot.Where(x => x.BlockId == blockId).Select(x => x.Id).ToList();
                await _mainWindow.SaveBlockOrder((DockBlock)blockId, orderedIds);
                ReloadSnapshot();
                RefreshOrderList();
                new DarkDialog("Порядок кнопок сохранен.") { Owner = this }.ShowDialog();
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                new DarkDialog($"Ошибка:\n{ex.Message}") { Owner = this }.ShowDialog();
            }
            finally
            {
                if (button != null)
                    button.IsEnabled = true;
            }
        }
    }
}
