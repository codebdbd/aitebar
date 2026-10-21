using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace AiteBar;

public partial class MainWindow
{
    private void BuildPanelContextMenu()
    {
        BuildPanelContextMenu(AppSettings.ActiveContextId);
    }

    private void BuildPanelContextMenu(string activeContextId)
    {
        ContextMenu menu = AppContextMenuFactory.CreateMenu(this);
        menu.Opened += (s, e) => _isElementContextMenuOpen = true;
        menu.Closed += (s, e) => _isElementContextMenuOpen = false;

        MenuItem panelsMenu = CreateMenuItem(FluentGlyph(MenuIcons.Panels), LocalizationService.Get("Menu_Panels"));

        foreach (PanelContext context in GetContextsSnapshot())
        {
            bool isActive = string.Equals(context.Id, activeContextId, StringComparison.Ordinal);
            string targetContextId = context.Id;

            MenuItem item = CreateMenuItem(
                glyph: FluentGlyph(MenuIcons.Panels),
                text: ContextStateHelper.GetContextListDisplayName(context),
                onClick: (s, e) => ActivateContextById(targetContextId),
                isActive: isActive
            );

            panelsMenu.Items.Add(item);
        }

        menu.Items.Add(panelsMenu);
        menu.Items.Add(CreateMenuItem(FluentGlyph(MenuIcons.Add), LocalizationService.Get("Menu_AddButton"), async (s, e) =>
        {
            await OpenAddButtonWindowAsync();
        }));
        menu.Items.Add(CreateMenuItem(FluentGlyph(MenuIcons.Import), LocalizationService.Get("Menu_ImportCurrentPanel"), async (s, e) =>
        {
            await RunPanelInteractionAsync(ImportIntoCurrentPanelAsync);
        }));
        menu.Items.Add(CreateMenuItem(FluentGlyph(MenuIcons.Export), LocalizationService.Get("Menu_ExportCurrentPanel"), async (s, e) =>
        {
            await RunPanelInteractionAsync(ExportCurrentPanelAsync);
        }));

        RootBorder.ContextMenu = menu;
    }

    private ContextMenu BuildElementContextMenu(CustomElement element)
    {
        ContextMenu menu = AppContextMenuFactory.CreateMenu(this);
        menu.Opened += (s, e) => _isElementContextMenuOpen = true;
        menu.Closed += (s, e) => _isElementContextMenuOpen = false;

        if (CanRunAsAdmin(element))
        {
            menu.Items.Add(CreateMenuItem(FluentGlyph(MenuIcons.Shield), LocalizationService.Get("Menu_RunAsAdmin"), async (s, e) =>
            {
                await RunPanelInteractionAsync(() => ExecuteActionAsAdminAsync(element));
            }));
        }

        menu.Items.Add(CreateMenuItem(FluentGlyph(MenuIcons.Edit), LocalizationService.Get("Menu_Edit"), (s, e) =>
        {
            RunPanelInteraction(() => new SettingsWindow(this, element) { Owner = this }.ShowDialog());
        }));

        menu.Items.Add(CreateMenuItem(FluentGlyph(MenuIcons.Copy), LocalizationService.Get("Menu_Duplicate"), async (s, e) =>
        {
            await RunPanelInteractionAsync(() => DuplicateElementAsync(element));
        }));

        menu.Items.Add(CreateMenuItem(FluentGlyph(MenuIcons.Rename), LocalizationService.Get("Menu_Rename"), async (s, e) =>
        {
            await RunPanelInteractionAsync(() => RenameElementAsync(element));
        }));

        List<MenuItem> moveTargets = GetContextsSnapshot()
            .Where(context => !string.Equals(context.Id, element.ContextId, StringComparison.Ordinal))
            .Select(context => CreateMenuItem(
                FluentGlyph(MenuIcons.Panels),
                ContextStateHelper.GetContextListDisplayName(context),
                async (s, e) =>
                {
                    await RunPanelInteractionAsync(() => MoveElementToContextAsync(element.Id, context.Id));
                }))
            .ToList();

        if (moveTargets.Count > 0)
        {
            MenuItem moveMenu = CreateMenuItem(FluentGlyph(MenuIcons.Move), LocalizationService.Get("Menu_Move"));
            foreach (MenuItem moveTarget in moveTargets)
            {
                moveMenu.Items.Add(moveTarget);
            }
            menu.Items.Add(moveMenu);
        }

        if (TryCreateCopyActionMenuItem(element, out MenuItem copyItem))
        {
            menu.Items.Add(copyItem);
        }

        if (CanOpenElementLocation(element))
        {
            menu.Items.Add(CreateMenuItem(FluentGlyph(MenuIcons.OpenFolder), LocalizationService.Get("Menu_OpenLocation"), async (s, e) =>
            {
                await RunPanelInteractionAsync(() => OpenElementLocationAsync(element));
            }));
        }

        menu.Items.Add(CreateMenuItem(FluentGlyph(MenuIcons.Delete), LocalizationService.Get("Menu_Delete"), async (s, e) =>
        {
            await RunPanelInteractionAsync(() => DeleteElementAsync(element));
        }, isDanger: true));

        return menu;
    }

    private async Task DuplicateElementAsync(CustomElement source)
    {
        CustomElement duplicate = _settingsService.CloneElement(source);
        duplicate.Id = Guid.NewGuid().ToString();
        duplicate.Name = BuildDuplicateElementName(source.Name);
        duplicate.LastUsedProfile = "";

        await _settingsService.InsertElementAfterAsync(source.Id, duplicate);
        RegisterGlobalHotkey();
        RefreshPanel();
        new SettingsWindow(this, duplicate) { Owner = this }.ShowDialog();
    }

    private string BuildDuplicateElementName(string sourceName) =>
        BuildDuplicateElementName(sourceName, Elements.Select(x => x.Name));

    internal static string BuildDuplicateElementName(string sourceName, IEnumerable<string> existingNames)
    {
        string baseName = string.IsNullOrWhiteSpace(sourceName) ? LocalizationService.Get("Element_NewButton") : sourceName.Trim();
        string firstCandidate = $"{baseName} ({LocalizationService.Get("Element_CopySuffix")})";
        var nameSet = new HashSet<string>(existingNames ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        if (!nameSet.Contains(firstCandidate)) return firstCandidate;

        for (int index = 2; index < 10000; index++)
        {
            string candidate = $"{baseName} ({LocalizationService.Format("Element_CopySuffixFormat", index)})";
            if (!nameSet.Contains(candidate)) return candidate;
        }
        // Fallback if all 9999 names are taken (extremely unlikely)
        return $"{baseName} ({Guid.NewGuid()})";
    }

    private async Task RenameElementAsync(CustomElement source)
    {
        CustomElement? elementToRename = Elements.FirstOrDefault(x => string.Equals(x.Id, source.Id, StringComparison.Ordinal));
        if (elementToRename == null) return;

        TextPromptDialog dialog = new TextPromptDialog("Prompt_RenameButtonTitle", "Prompt_NewName", elementToRename.Name, treatAsResourceKeys: true) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        string newName = dialog.Value.Trim();
        if (string.Equals(elementToRename.Name, newName, StringComparison.Ordinal))
        {
            return;
        }

        await _settingsService.UpdateElementAsync(elementToRename.Id, element => element.Name = newName);
        RefreshPanel();
    }

    private async Task MoveElementToContextAsync(string elementId, string targetContextId)
    {
        CustomElement? elementToMove = Elements.FirstOrDefault(x => string.Equals(x.Id, elementId, StringComparison.Ordinal));
        if (elementToMove == null || string.Equals(elementToMove.ContextId, targetContextId, StringComparison.Ordinal))
        {
            return;
        }

        await _settingsService.UpdateElementAsync(elementToMove.Id, element => element.ContextId = targetContextId);
        RefreshPanel();
    }

    private async Task DeleteElementAsync(CustomElement source)
    {
        CustomElement? elementToDelete = Elements.FirstOrDefault(x => string.Equals(x.Id, source.Id, StringComparison.Ordinal));
        if (elementToDelete == null)
        {
            return;
        }

        DarkDialog dialog = new DarkDialog(LocalizationService.Format("DeleteButtonConfirm", elementToDelete.Name), isConfirm: true) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        await _settingsService.DeleteElementAsync(elementToDelete.Id);
        RegisterGlobalHotkey();
        RefreshPanel();
    }

    private bool TryCreateCopyActionMenuItem(CustomElement element, out MenuItem menuItem)
    {
        menuItem = null!;

        if (!TryGetCopyValue(element, out string caption, out string value))
        {
            return false;
        }

        menuItem = CreateMenuItem(FluentGlyph(MenuIcons.Clipboard), caption, (s, e) =>
        {
            try
            {
                Clipboard.SetText(value);
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                new DarkDialog(LocalizationService.Format("Action_Failed", ex.Message)) { Owner = this }.ShowDialog();
            }
        });
        return true;
    }

    internal static bool TryGetCopyValue(CustomElement element, out string caption, out string value)
    {
        caption = string.Empty;
        value = string.Empty;

        if (string.IsNullOrWhiteSpace(element.ActionValue) ||
            !Enum.TryParse<ActionType>(element.ActionType, out var actionType))
        {
            return false;
        }

        switch (actionType)
        {
            case ActionType.Web:
                caption = LocalizationService.Get("Menu_CopyUrl");
                value = element.ActionValue;
                return true;
            case ActionType.Program:
            case ActionType.File:
            case ActionType.Folder:
            case ActionType.ScriptFile:
                caption = LocalizationService.Get("Menu_CopyPath");
                value = element.ActionValue;
                return true;
            case ActionType.Command:
                caption = LocalizationService.Get("Menu_CopyCommand");
                value = element.ActionValue;
                return true;
            default:
                return false;
        }
    }

    private static string FluentGlyph(int codePoint) => char.ConvertFromUtf32(codePoint);

    internal static bool CanRunAsAdmin(CustomElement element)
    {
        if (!Enum.TryParse<ActionType>(element.ActionType, out var actionType))
        {
            return false;
        }

        return actionType is ActionType.Program or ActionType.ScriptFile;
    }

    private async Task ExecuteActionAsAdminAsync(CustomElement element)
    {
        CustomElement adminEl = _settingsService.CloneElement(element);
        adminEl.RunAsAdmin = true;
        await ExecuteUserButtonActionAsync(adminEl);
    }

    internal static bool CanOpenElementLocation(CustomElement element)
    {
        if (!Enum.TryParse<ActionType>(element.ActionType, out var actionType))
        {
            return false;
        }

        return actionType is ActionType.Program or ActionType.File or ActionType.Folder or ActionType.ScriptFile;
    }

    private async Task OpenElementLocationAsync(CustomElement element)
    {
        try
        {
            if (!Enum.TryParse<ActionType>(element.ActionType, out ActionType actionType))
            {
                return;
            }

            string target = element.ActionValue;
            if (string.IsNullOrWhiteSpace(target))
            {
                return;
            }

            switch (actionType)
            {
                case ActionType.Folder:
                    if (Directory.Exists(target))
                    {
                        var psi = new ProcessStartInfo("explorer.exe") { UseShellExecute = true };
                        psi.ArgumentList.Add(target);
                        Process.Start(psi);
                    }
                    else
                    {
                        new DarkDialog(LocalizationService.Format("Action_TargetNotFound", target)) { Owner = this }.ShowDialog();
                    }
                    break;

                case ActionType.Program:
                case ActionType.File:
                case ActionType.ScriptFile:
                    if (File.Exists(target))
                    {
                        var psi = new ProcessStartInfo("explorer.exe") { UseShellExecute = true };
                        psi.ArgumentList.Add("/select," + target);
                        Process.Start(psi);
                    }
                    else
                    {
                        new DarkDialog(LocalizationService.Format("Action_TargetNotFound", target)) { Owner = this }.ShowDialog();
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
            new DarkDialog(LocalizationService.Format("Action_Failed", ex.Message)) { Owner = this }.ShowDialog();
            await Task.CompletedTask;
        }
    }
}
