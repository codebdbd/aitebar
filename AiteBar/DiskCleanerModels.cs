using System;
using System.Collections.Generic;

namespace AiteBar;

public sealed record DiskCleanCategory(
    string Id,
    string TitleKey,
    string DescriptionKey,
    long SizeBytes,
    bool IsSelected = true,
    bool IsSafe = true);

public sealed record DiskCleanProgress(
    string CategoryId,
    string StatusMessage,
    double Percentage = 0.0);

public sealed record DiskCleanScanResult(
    IReadOnlyList<DiskCleanCategory> Categories,
    long TotalSizeBytes);

public sealed record DiskCleanResult(
    long TotalFreedBytes,
    int ItemsCleanedCount,
    int SkippedLockedCount,
    IReadOnlyList<string> CleanedCategoryIds);
