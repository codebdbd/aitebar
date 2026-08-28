using System;
using System.Collections.Generic;
using System.Linq;

namespace AiteBar;

public enum CategorySafetyLevel
{
    Safe,
    Caution
}

public enum DiskCleanCategoryStatus
{
    Succeeded,
    PartiallyCleaned,
    Failed,
    Skipped
}

public sealed record DiskCleanCategory(
    string Id,
    string TitleKey,
    string DescriptionKey,
    long SizeBytes,
    bool IsSelected = false,
    bool IsSafe = true,
    string? WarningKey = null)
{
    public CategorySafetyLevel SafetyLevel => IsSafe ? CategorySafetyLevel.Safe : CategorySafetyLevel.Caution;
}

public sealed record DiskCleanProgress(
    string CategoryId,
    string StatusMessage,
    double Percentage = 0.0);

public sealed record DiskCleanScanResult(
    IReadOnlyList<DiskCleanCategory> Categories,
    long TotalSizeBytes);

public sealed record DiskCleanCategoryReport(
    string CategoryId,
    DiskCleanCategoryStatus Status,
    long FreedBytes,
    int CleanedCount,
    int LockedCount,
    string? FailureReason = null);

public sealed record DiskCleanResult(
    long TotalFreedBytes,
    int TotalCleanedCount,
    int TotalLockedCount,
    IReadOnlyList<DiskCleanCategoryReport> Reports)
{
    public bool HasErrors => Reports.Any(r => r.Status == DiskCleanCategoryStatus.Failed);
    public bool HasPartial => Reports.Any(r => r.Status == DiskCleanCategoryStatus.PartiallyCleaned);
    public int SucceededCount => Reports.Count(r => r.Status == DiskCleanCategoryStatus.Succeeded);
    public int FailedCount => Reports.Count(r => r.Status == DiskCleanCategoryStatus.Failed);
    public int PartialCount => Reports.Count(r => r.Status == DiskCleanCategoryStatus.PartiallyCleaned);
    public int SkippedCount => Reports.Count(r => r.Status == DiskCleanCategoryStatus.Skipped);
}
