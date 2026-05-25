using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AiteBar;

internal static class ProfileRotationHelper
{
    public static IReadOnlyList<BrowserProfileInfo> GetEligibleProfiles(
        IEnumerable<BrowserProfileInfo> profiles,
        IEnumerable<string>? selectedProfilePaths)
    {
        var profileList = profiles.ToList();
        var selected = new HashSet<string>(selectedProfilePaths ?? [], StringComparer.OrdinalIgnoreCase);
        if (selected.Count == 0)
        {
            return profileList;
        }

        return [.. profileList.Where(profile => selected.Contains(profile.ProfilePath))];
    }

    public static string AdvanceProfile(
        IEnumerable<BrowserProfileInfo> profiles,
        IEnumerable<string>? selectedProfilePaths,
        string lastUsedProfile)
    {
        var eligibleProfiles = GetEligibleProfiles(profiles, selectedProfilePaths);
        if (eligibleProfiles.Count == 0)
        {
            return "";
        }

        int idx = eligibleProfiles.ToList().FindIndex(p =>
            string.Equals(p.LaunchName, lastUsedProfile, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p.ProfileName, lastUsedProfile, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p.ProfilePath, lastUsedProfile, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetFileName(p.ProfilePath), lastUsedProfile, StringComparison.OrdinalIgnoreCase));

        if (idx < 0)
        {
            return eligibleProfiles[0].LaunchName;
        }

        return eligibleProfiles[(idx + 1) % eligibleProfiles.Count].LaunchName;
    }
}
