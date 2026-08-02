using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AiteBar;

internal static class FileSorterUndoStateHelper
{
    public static FileSortUndoState? Find(IEnumerable<FileSortUndoState> states, string rootPath)
    {
        ArgumentNullException.ThrowIfNull(states);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        return states.FirstOrDefault(state => PathsEqual(state.RootPath, rootPath));
    }

    public static List<FileSortUndoState> Merge(
        IEnumerable<FileSortUndoState> existingStates,
        IEnumerable<FileSortResult> results)
    {
        ArgumentNullException.ThrowIfNull(existingStates);
        ArgumentNullException.ThrowIfNull(results);

        List<FileSortUndoState> merged = Deduplicate(existingStates);
        foreach (FileSortResult result in results)
        {
            if (result.UndoState == null)
            {
                continue;
            }

            merged.RemoveAll(state => PathsEqual(state.RootPath, result.RootPath));
            merged.Add(result.UndoState);
        }

        return merged;
    }

    public static List<FileSortUndoState> Replace(
        IEnumerable<FileSortUndoState> existingStates,
        string rootPath,
        FileSortUndoState? replacement)
    {
        ArgumentNullException.ThrowIfNull(existingStates);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        List<FileSortUndoState> updated = Deduplicate(existingStates);
        updated.RemoveAll(state => PathsEqual(state.RootPath, rootPath));
        if (replacement != null)
        {
            updated.Add(replacement);
        }

        return updated;
    }

    internal static bool PathsEqual(string left, string right)
    {
        try
        {
            string normalizedLeft = Path.TrimEndingDirectorySeparator(Path.GetFullPath(left));
            string normalizedRight = Path.TrimEndingDirectorySeparator(Path.GetFullPath(right));
            return string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or System.Security.SecurityException)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static List<FileSortUndoState> Deduplicate(IEnumerable<FileSortUndoState> states)
    {
        var deduplicated = new List<FileSortUndoState>();
        foreach (FileSortUndoState state in states)
        {
            deduplicated.RemoveAll(existing => PathsEqual(existing.RootPath, state.RootPath));
            deduplicated.Add(state);
        }

        return deduplicated;
    }
}
