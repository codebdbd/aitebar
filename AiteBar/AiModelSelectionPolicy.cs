namespace AiteBar;

internal sealed record AiRouteCandidate(
    AiConnectionSettings Connection,
    AiModelDescriptor Model,
    int ConnectionOrder);

internal static class AiModelSelectionPolicy
{
    public static IReadOnlyList<AiRouteCandidate> OrderRoutes(
        AiSettings settings,
        AiChatRequest request,
        IEnumerable<AiRouteCandidate> routes)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(routes);
        ValidateRequestContract(request);

        AiRouteCandidate[] candidates = routes.ToArray();
        if (request.RequireExactModel)
        {
            ValidateExactCandidates(request, candidates);
            return candidates
                .OrderBy(candidate => candidate.ConnectionOrder)
                .ToArray();
        }

        IReadOnlyDictionary<string, int> providerRanks = BuildProviderRanks(settings);
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> preferredModelRanks =
            BuildPreferredModelRanks(settings, candidates);

        AiRouteCandidate[] ordered = candidates
            .OrderBy(candidate => providerRanks.GetValueOrDefault(
                candidate.Connection.ProviderId,
                int.MaxValue))
            .ThenBy(candidate => GetPreferredModelRank(preferredModelRanks, candidate))
            .ThenBy(candidate => TextProcessingModelPolicy.GetCertifiedModelRank(candidate.Model))
            .ThenBy(candidate => candidate.Model.ModelId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.ConnectionOrder)
            .ToArray();

        int rotationOffset = request.RotationOffset;
        if (rotationOffset <= 0 && request.IsAlternative)
        {
            rotationOffset = 1;
        }

        if (rotationOffset > 0 && ordered.Length > 1)
        {
            int offset = rotationOffset % ordered.Length;
            return ordered.Skip(offset).Concat(ordered.Take(offset)).ToArray();
        }

        return ordered;
    }

    internal static void ValidateRequestContract(AiChatRequest request)
    {
        bool hasProvider = !string.IsNullOrWhiteSpace(request.PreferredProviderId);
        bool hasModel = !string.IsNullOrWhiteSpace(request.PreferredModelId);
        if (request.RequireExactModel)
        {
            if (!hasProvider || !hasModel)
            {
                throw new InvalidOperationException(
                    "Exact Text Processing requests require both provider and model identifiers.");
            }

            return;
        }

        if (hasProvider || hasModel)
        {
            throw new InvalidOperationException(
                "Automatic Text Processing requests cannot specify provider or model identifiers.");
        }
    }

    private static void ValidateExactCandidates(
        AiChatRequest request,
        IEnumerable<AiRouteCandidate> candidates)
    {
        foreach (AiRouteCandidate candidate in candidates)
        {
            if (!string.Equals(
                    candidate.Connection.ProviderId,
                    request.PreferredProviderId,
                    StringComparison.OrdinalIgnoreCase)
                || !string.Equals(
                    candidate.Model.ModelId,
                    request.PreferredModelId,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "An exact Text Processing route does not match the requested provider and model.");
            }
        }
    }

    private static IReadOnlyDictionary<string, int> BuildProviderRanks(AiSettings settings)
    {
        var orderedProviders = new List<string>();
        var seenProviders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string providerId in (settings.ProviderOrder ?? [])
                     .Concat(AiProviderCatalog.DefaultProviderOrder))
        {
            if (!string.IsNullOrWhiteSpace(providerId) && seenProviders.Add(providerId))
            {
                orderedProviders.Add(providerId);
            }
        }

        return orderedProviders
            .Select((providerId, index) => (providerId, index))
            .ToDictionary(item => item.providerId, item => item.index, StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>>
        BuildPreferredModelRanks(
            AiSettings settings,
            IReadOnlyList<AiRouteCandidate> candidates)
    {
        var participatingConnections = candidates
            .Select(candidate => candidate.Connection.Id)
            .ToHashSet(StringComparer.Ordinal);
        var preferredByProvider = new Dictionary<string, List<string>>(
            StringComparer.OrdinalIgnoreCase);
        var seenByProvider = new Dictionary<string, HashSet<string>>(
            StringComparer.OrdinalIgnoreCase);

        foreach (AiConnectionSettings connection in settings.Connections ?? [])
        {
            if (!connection.IsEnabled
                || !participatingConnections.Contains(connection.Id)
                || !AiProviderCatalog.TryGet(connection.ProviderId, out _)
                || string.IsNullOrWhiteSpace(connection.PreferredModelId))
            {
                continue;
            }

            if (!preferredByProvider.TryGetValue(
                    connection.ProviderId,
                    out List<string>? preferredModels))
            {
                preferredModels = [];
                preferredByProvider.Add(connection.ProviderId, preferredModels);
                seenByProvider.Add(
                    connection.ProviderId,
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            }

            if (seenByProvider[connection.ProviderId].Add(connection.PreferredModelId))
            {
                preferredModels.Add(connection.PreferredModelId);
            }
        }

        return preferredByProvider.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyDictionary<string, int>)pair.Value
                .Select((modelId, index) => (modelId, index))
                .ToDictionary(item => item.modelId, item => item.index, StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
    }

    private static int GetPreferredModelRank(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> preferredModelRanks,
        AiRouteCandidate candidate)
    {
        return preferredModelRanks.TryGetValue(
                   candidate.Connection.ProviderId,
                   out IReadOnlyDictionary<string, int>? providerPreferences)
               && providerPreferences.TryGetValue(candidate.Model.ModelId, out int rank)
            ? rank
            : int.MaxValue;
    }
}
