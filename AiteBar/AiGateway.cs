using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;

namespace AiteBar;

internal enum RouteOrderingMode
{
    Legacy = 0,
    DeterministicTextProcessing = 1
}

public sealed class AiGateway
{
    private static readonly TimeSpan ModelCacheLifetime = TimeSpan.FromMinutes(15);
    private readonly AppSettingsService _settingsService;
    private readonly AiProviderClient _providerClient;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, AiConnectionRuntimeStatus> _connectionStatuses = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, AiConnectionRuntimeStatus> _quotaStatuses = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, CachedModels> _modelCache = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _modelCacheSemaphore = new(1, 1);

    public AiGateway(AppSettingsService settingsService)
        : this(settingsService, new AiProviderClient(new WindowsAiCredentialStore()), TimeProvider.System)
    {
    }

    internal AiGateway(
        AppSettingsService settingsService,
        AiProviderClient providerClient,
        TimeProvider timeProvider)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _providerClient = providerClient ?? throw new ArgumentNullException(nameof(providerClient));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<AiGatewayResponse> GenerateAsync(
        AiChatRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        AiSettings settings = _settingsService.Settings.Ai ?? new AiSettings();
        IReadOnlyList<AiConnectionSettings> candidates = BuildCandidates(settings, request);
        if (candidates.Count == 0)
        {
            throw new NoAvailableConnectionException(
                "No enabled AI connections are configured.",
                reason: AiAvailabilityFailureReason.NoConnectionsConfigured);
        }

        (IReadOnlyList<AiRoute> routes, Exception? lastError) =
            await BuildRoutesAsync(settings, request, candidates, RouteOrderingMode.Legacy, cancellationToken).ConfigureAwait(false);
        foreach (AiRoute route in routes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsRouteAvailable(route.Connection, route.Model.ModelId))
            {
                continue;
            }

            try
            {
                AiProviderResponse response = await _providerClient.GenerateAsync(
                    route.Connection,
                    route.Model,
                    request,
                    cancellationToken).ConfigureAwait(false);
                MarkSuccessful(route.Connection, route.Model.ModelId);
                return new AiGatewayResponse(
                    response.Content,
                    response.ProviderId,
                    route.Connection.Id,
                    response.ModelId,
                    response.PromptTokens,
                    response.CompletionTokens);
            }
            catch (AiProviderHttpException ex)
            {
                lastError = ex;
                ApplyFailure(route.Connection, route.Model.ModelId, ex);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastError = ex;
                ApplyTemporaryConnectionFailure(route.Connection, ex.Message);
            }
        }

        throw new NoAvailableConnectionException(
            "No configured AI connection is currently available.",
            lastError,
            DetermineAvailabilityFailureReason(candidates, lastError));
    }

    public Task<AiGatewayStream> GenerateStreamingAsync(
        AiChatRequest request,
        CancellationToken cancellationToken = default) =>
        GenerateStreamingCoreAsync(request, RouteOrderingMode.Legacy, cancellationToken);

    internal Task<AiGatewayStream> GenerateTextProcessingStreamingAsync(
        AiChatRequest request,
        CancellationToken cancellationToken = default) =>
        GenerateStreamingCoreAsync(request, RouteOrderingMode.DeterministicTextProcessing, cancellationToken);

    internal Task<AiGatewayStream> GeneratePromptBuilderStreamingAsync(
        AiChatRequest request,
        CancellationToken cancellationToken = default)
    {
        ClearPromptBuilderTemporaryAvailability();
        return GenerateStreamingCoreAsync(request, RouteOrderingMode.DeterministicTextProcessing, cancellationToken);
    }

    private async Task<AiGatewayStream> GenerateStreamingCoreAsync(
        AiChatRequest request,
        RouteOrderingMode mode,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        AiSettings settings = _settingsService.Settings.Ai ?? new AiSettings();
        IReadOnlyList<AiConnectionSettings> candidates = BuildCandidates(settings, request);
        if (candidates.Count == 0)
        {
            throw new NoAvailableConnectionException(
                "No enabled AI connections are configured.",
                reason: AiAvailabilityFailureReason.NoConnectionsConfigured);
        }

        (IReadOnlyList<AiRoute> routes, Exception? lastError) =
            await BuildRoutesAsync(settings, request, candidates, mode, cancellationToken).ConfigureAwait(false);
        foreach (AiRoute route in routes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsRouteAvailable(route.Connection, route.Model.ModelId))
            {
                continue;
            }

            try
            {
                AiProviderStream stream = await _providerClient.GenerateStreamingAsync(
                    route.Connection,
                    route.Model,
                    request,
                    cancellationToken).ConfigureAwait(false);
                IAsyncEnumerator<string> enumerator = stream.Chunks.GetAsyncEnumerator(cancellationToken);
                bool hasFirstChunk;
                try
                {
                    hasFirstChunk = await enumerator.MoveNextAsync().ConfigureAwait(false);
                }
                catch
                {
                    await enumerator.DisposeAsync().ConfigureAwait(false);
                    throw;
                }
                if (!hasFirstChunk)
                {
                    await enumerator.DisposeAsync().ConfigureAwait(false);
                    var emptyResponse = new EmptyAiResponseException(stream.ProviderId, stream.ModelId);
                    lastError = emptyResponse;
                    ApplyTemporaryModelFailure(route.Connection, route.Model.ModelId, emptyResponse.Message);
                    continue;
                }
                return new AiGatewayStream(
                    stream.ProviderId,
                    route.Connection.Id,
                    stream.ModelId,
                    ObservePrefetchedStreamAsync(
                        route.Connection,
                        route.Model.ModelId,
                        enumerator,
                        enumerator.Current,
                        cancellationToken));
            }
            catch (AiProviderHttpException ex)
            {
                lastError = ex;
                ApplyFailure(route.Connection, route.Model.ModelId, ex);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastError = ex;
                ApplyTemporaryConnectionFailure(route.Connection, ex.Message);
            }
        }

        throw new NoAvailableConnectionException(
            "No configured AI connection is currently available.",
            lastError,
            DetermineAvailabilityFailureReason(candidates, lastError));
    }

    private async IAsyncEnumerable<string> ObservePrefetchedStreamAsync(
        AiConnectionSettings connection,
        string modelId,
        IAsyncEnumerator<string> enumerator,
        string firstChunk,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using (enumerator)
        {
            yield return firstChunk;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                bool hasNext;
                try
                {
                    hasNext = await enumerator.MoveNextAsync().ConfigureAwait(false);
                }
                catch (AiProviderHttpException ex)
                {
                    ApplyFailure(connection, modelId, ex);
                    throw;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    ApplyTemporaryConnectionFailure(connection, ex.Message);
                    throw;
                }

                if (!hasNext)
                {
                    MarkSuccessful(connection, modelId);
                    yield break;
                }
                yield return enumerator.Current;
            }
        }
    }

    internal AiConnectionRuntimeStatus? GetConnectionStatus(string connectionId) =>
        _connectionStatuses.TryGetValue(connectionId, out AiConnectionRuntimeStatus? status)
            ? status
            : null;

    internal AiConnectionRuntimeStatus? GetQuotaStatus(
        AiConnectionSettings connection,
        string? modelId = null)
    {
        if (!string.IsNullOrWhiteSpace(modelId))
        {
            return _quotaStatuses.TryGetValue(GetQuotaKey(connection, modelId), out AiConnectionRuntimeStatus? status)
                ? status
                : null;
        }

        string prefix = GetQuotaKeyPrefix(connection);
        return _quotaStatuses
            .Where(pair => pair.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(pair => pair.Value.UpdatedAt)
            .Select(pair => pair.Value)
            .FirstOrDefault();
    }

    internal void ResetConnection(string connectionId)
    {
        _connectionStatuses.TryRemove(connectionId, out _);
        _modelCache.TryRemove(connectionId, out _);
        string quotaMarker = $":{connectionId}:";
        foreach (string key in _quotaStatuses.Keys.Where(key =>
                     key.Contains(quotaMarker, StringComparison.Ordinal)))
        {
            _quotaStatuses.TryRemove(key, out _);
        }
    }

    internal void InvalidateModelCache(string connectionId) =>
        _modelCache.TryRemove(connectionId, out _);

    public async Task<IReadOnlyList<AiModelDescriptor>> GetModelsAsync(
        AiConnectionSettings connection,
        CancellationToken cancellationToken = default)
    {
        return await GetModelsCachedAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    internal IReadOnlyList<AiConnectionSettings> BuildCandidates(
        AiSettings settings,
        AiChatRequest request)
    {
        IEnumerable<AiConnectionSettings> enabledConnections = (settings.Connections ?? [])
            .Where(connection => connection.IsEnabled && AiProviderCatalog.TryGet(connection.ProviderId, out _));
        if (!string.IsNullOrWhiteSpace(request.PreferredConnectionId))
        {
            return enabledConnections
                .Where(connection => string.Equals(
                    connection.Id,
                    request.PreferredConnectionId,
                    StringComparison.Ordinal))
                .ToArray();
        }
        if (request.RequireExactModel && !string.IsNullOrWhiteSpace(request.PreferredProviderId))
        {
            enabledConnections = enabledConnections.Where(connection => string.Equals(
                connection.ProviderId,
                request.PreferredProviderId,
                StringComparison.OrdinalIgnoreCase));
        }

        var providerOrder = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.PreferredProviderId))
        {
            providerOrder.Add(request.PreferredProviderId);
        }
        providerOrder.AddRange(settings.ProviderOrder ?? []);
        providerOrder.AddRange(AiProviderCatalog.DefaultProviderOrder);
        providerOrder = providerOrder.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var providerRanks = providerOrder
            .Select((providerId, index) => (providerId, index))
            .ToDictionary(item => item.providerId, item => item.index, StringComparer.OrdinalIgnoreCase);

        return enabledConnections
            .OrderBy(connection => providerRanks.GetValueOrDefault(connection.ProviderId, int.MaxValue))
            .ThenBy(connection => connection.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private async Task<(IReadOnlyList<AiRoute> Routes, Exception? LastError)> BuildRoutesAsync(
        AiSettings settings,
        AiChatRequest request,
        IReadOnlyList<AiConnectionSettings> connections,
        RouteOrderingMode mode,
        CancellationToken cancellationToken)
    {
        var routeGroups = new Dictionary<string, List<AiRoute>>(StringComparer.OrdinalIgnoreCase);
        var groupOrder = new List<string>();
        Exception? lastError = null;

        foreach (AiConnectionSettings connection in connections)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsConnectionAvailable(connection))
            {
                continue;
            }

            try
            {
                IReadOnlyList<AiModelDescriptor> models =
                    await GetModelsCachedAsync(connection, cancellationToken).ConfigureAwait(false);
                IEnumerable<AiModelDescriptor> eligibleModels =
                    GetEligibleModels(settings, connection, models, request);
                if (mode == RouteOrderingMode.DeterministicTextProcessing)
                {
                    eligibleModels = ApplyTextProcessingModelPolicy(
                        eligibleModels,
                        request.RequireExactModel);
                }

                foreach (AiModelDescriptor model in eligibleModels)
                {
                    string identity = CreateModelIdentity(model.ProviderId, model.ModelId);
                    if (!routeGroups.TryGetValue(identity, out List<AiRoute>? routes))
                    {
                        routes = [];
                        routeGroups.Add(identity, routes);
                        groupOrder.Add(identity);
                    }
                    if (!routes.Any(route => string.Equals(
                            route.Connection.Id,
                            connection.Id,
                            StringComparison.Ordinal)))
                    {
                        routes.Add(new AiRoute(connection, model));
                    }
                }
            }
            catch (AiProviderHttpException ex)
            {
                lastError = ex;
                ApplyFailure(connection, null, ex);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastError = ex;
                ApplyTemporaryConnectionFailure(connection, ex.Message);
            }
        }

        if (mode == RouteOrderingMode.Legacy)
        {
            return (
                groupOrder.SelectMany(identity => routeGroups[identity]).ToArray(),
                lastError);
        }

        var connectionOrder = new Dictionary<string, int>(StringComparer.Ordinal);
        int index = 0;
        foreach (AiConnectionSettings connection in settings.Connections ?? [])
        {
            if (!connectionOrder.ContainsKey(connection.Id))
            {
                connectionOrder[connection.Id] = index++;
            }
        }

        var allRoutes = new List<AiRoute>(routeGroups.Count);
        foreach (List<AiRoute> groupRoutes in routeGroups.Values)
        {
            allRoutes.AddRange(groupRoutes);
        }

        var routeIndex = new Dictionary<string, Dictionary<string, Dictionary<string, AiRoute>>>(StringComparer.Ordinal);
        foreach (AiRoute route in allRoutes)
        {
            if (!routeIndex.TryGetValue(route.Connection.Id, out var byConn))
            {
                byConn = new Dictionary<string, Dictionary<string, AiRoute>>(StringComparer.OrdinalIgnoreCase);
                routeIndex[route.Connection.Id] = byConn;
            }
            if (!byConn.TryGetValue(route.Model.ProviderId, out var byProvider))
            {
                byProvider = new Dictionary<string, AiRoute>(StringComparer.OrdinalIgnoreCase);
                byConn[route.Model.ProviderId] = byProvider;
            }
            byProvider[route.Model.ModelId] = route;
        }

        var candidates = new AiRouteCandidate[allRoutes.Count];
        for (int i = 0; i < allRoutes.Count; i++)
        {
            AiRoute route = allRoutes[i];
            int order = connectionOrder.TryGetValue(route.Connection.Id, out int stored)
                ? stored
                : int.MaxValue;
            candidates[i] = new AiRouteCandidate(route.Connection, route.Model, order);
        }

        IReadOnlyList<AiRouteCandidate> orderedCandidates =
            AiModelSelectionPolicy.OrderRoutes(settings, request, candidates);

        AiRoute[] result = new AiRoute[orderedCandidates.Count];
        for (int i = 0; i < orderedCandidates.Count; i++)
        {
            AiRouteCandidate candidate = orderedCandidates[i];
            if (!routeIndex.TryGetValue(candidate.Connection.Id, out var byConn)
                || !byConn.TryGetValue(candidate.Model.ProviderId, out var byProvider)
                || !byProvider.TryGetValue(candidate.Model.ModelId, out AiRoute? match))
            {
                throw new InvalidOperationException(
                    "Ordered route candidate could not be matched back to the original AiRoute list.");
            }
            result[i] = match;
        }

        return (result, lastError);
    }

    // A new Prompt Builder submission is an explicit retry. Do not let a stale
    // in-memory cooldown suppress it before an HTTP request is made.
    internal void ClearPromptBuilderTemporaryAvailability()
    {
        foreach ((string connectionId, AiConnectionRuntimeStatus status) in _connectionStatuses)
        {
            if (status.State == AiConnectionState.Unavailable)
            {
                _connectionStatuses.TryRemove(connectionId, out _);
            }
        }

        foreach ((string quotaKey, AiConnectionRuntimeStatus status) in _quotaStatuses)
        {
            if (status.State is AiConnectionState.CoolingDown or AiConnectionState.Unavailable)
            {
                _quotaStatuses.TryRemove(quotaKey, out _);
            }
        }
    }

    internal static IReadOnlyList<AiModelDescriptor> ApplyTextProcessingModelPolicy(
        IEnumerable<AiModelDescriptor> models,
        bool requireExactModel)
    {
        ArgumentNullException.ThrowIfNull(models);
        return models.Where(model => requireExactModel
            ? TextProcessingModelPolicy.Classify(model) != TextProcessingModelTier.Unsupported
            : TextProcessingModelPolicy.Classify(model) == TextProcessingModelTier.CertifiedAutomatic).ToArray();
    }

    private bool IsConnectionAvailable(AiConnectionSettings connection)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        if (!_connectionStatuses.TryGetValue(connection.Id, out AiConnectionRuntimeStatus? status))
        {
            return true;
        }
        if (status.State is AiConnectionState.InvalidCredential or AiConnectionState.PermissionDenied)
        {
            return false;
        }
        return status.CooldownUntil <= now;
    }

    private bool IsRouteAvailable(AiConnectionSettings connection, string modelId)
    {
        if (!IsConnectionAvailable(connection))
        {
            return false;
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        return !HasActiveQuotaStatus(connection, modelId, now) &&
               !HasActiveQuotaStatus(connection, null, now);
    }

    private bool HasActiveQuotaStatus(
        AiConnectionSettings connection,
        string? modelId,
        DateTimeOffset now) =>
        _quotaStatuses.TryGetValue(GetQuotaKey(connection, modelId), out AiConnectionRuntimeStatus? status) &&
        status.CooldownUntil > now;

    private async Task<IReadOnlyList<AiModelDescriptor>> GetModelsCachedAsync(
        AiConnectionSettings connection,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        if (_modelCache.TryGetValue(connection.Id, out CachedModels? cached) &&
            now - cached.RefreshedAt < ModelCacheLifetime)
        {
            return cached.Models;
        }

        await _modelCacheSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            now = _timeProvider.GetUtcNow();
            if (_modelCache.TryGetValue(connection.Id, out cached) &&
                now - cached.RefreshedAt < ModelCacheLifetime)
            {
                return cached.Models;
            }

            IReadOnlyList<AiModelDescriptor> models =
                await _providerClient.GetModelsAsync(connection, cancellationToken).ConfigureAwait(false);
            _modelCache[connection.Id] = new CachedModels(models, now);
            return models;
        }
        finally
        {
            _modelCacheSemaphore.Release();
        }
    }

    internal static AiModelDescriptor? SelectModel(
        AiSettings settings,
        AiConnectionSettings connection,
        IReadOnlyList<AiModelDescriptor> models,
        AiChatRequest request) =>
        GetEligibleModels(settings, connection, models, request).FirstOrDefault();

    private static IReadOnlyList<AiModelDescriptor> GetEligibleModels(
        AiSettings settings,
        AiConnectionSettings connection,
        IReadOnlyList<AiModelDescriptor> models,
        AiChatRequest request)
    {
        IEnumerable<AiModelDescriptor> eligible = models.Where(model =>
            !model.IsDeprecated &&
            (model.Capabilities & request.RequiredCapabilities) == request.RequiredCapabilities &&
            (!request.RequiredContextTokens.HasValue ||
             !model.ContextLength.HasValue ||
             model.ContextLength.Value >= request.RequiredContextTokens.Value));
        if (request.RequireWritingModel)
        {
            eligible = eligible.Where(TextProcessingService.IsSuitableForWritingModel);
        }
        if (settings.FreeTierOnly || request.RequireFreeModel)
        {
            eligible = eligible.Where(model =>
                model.CostStatus is AiCostStatus.VerifiedFree or AiCostStatus.FreeTierAvailable);
        }

        AiModelDescriptor[] candidates = eligible.ToArray();
        string? requestedModel = request.RequireExactModel
            ? request.PreferredModelId
            : request.PreferredModelId ?? connection.PreferredModelId;
        if (request.RequireExactModel)
        {
            return string.IsNullOrWhiteSpace(requestedModel)
                ? []
                : candidates.Where(model => string.Equals(
                    model.ModelId,
                    requestedModel,
                    StringComparison.OrdinalIgnoreCase)).ToArray();
        }
        if (string.IsNullOrWhiteSpace(requestedModel))
        {
            return candidates;
        }

        return candidates
            .OrderByDescending(model => string.Equals(
                model.ModelId,
                requestedModel,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private void ApplyFailure(
        AiConnectionSettings connection,
        string? modelId,
        AiProviderHttpException exception)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        switch (exception.StatusCode)
        {
            case HttpStatusCode.Unauthorized:
                _connectionStatuses[connection.Id] = new(
                    AiConnectionState.InvalidCredential, null, exception.Message, now);
                break;
            case HttpStatusCode.Forbidden:
                _connectionStatuses[connection.Id] = new(
                    AiConnectionState.PermissionDenied, null, exception.Message, now);
                break;
            case HttpStatusCode.TooManyRequests:
                // Only a provider-supplied Retry-After is authoritative. Inventing a
                // one-minute cooldown made transient 429 responses block this window
                // until the application was restarted.
                TimeSpan retryAfter = exception.RetryAfter is { } specifiedRetryAfter && specifiedRetryAfter > TimeSpan.Zero
                    ? specifiedRetryAfter
                    : TimeSpan.Zero;
                _quotaStatuses[GetQuotaKey(connection, modelId)] = new(
                    AiConnectionState.CoolingDown,
                    now + retryAfter,
                    exception.Message,
                    now);
                break;
            case HttpStatusCode.PaymentRequired:
                _quotaStatuses[GetQuotaKey(connection, modelId)] = new(
                    AiConnectionState.QuotaExhausted,
                    now + TimeSpan.FromHours(24),
                    exception.Message,
                    now);
                break;
            default:
                ApplyTemporaryConnectionFailure(connection, exception.Message);
                break;
        }
    }

    private void ApplyTemporaryConnectionFailure(AiConnectionSettings connection, string error)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        _connectionStatuses[connection.Id] = new(
            AiConnectionState.Unavailable,
            now + TimeSpan.FromSeconds(30),
            error,
            now);
    }

    private void MarkSuccessful(AiConnectionSettings connection, string modelId)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        _connectionStatuses[connection.Id] = new(AiConnectionState.Available, null, null, now);
        _quotaStatuses.TryRemove(GetQuotaKey(connection, modelId), out _);
        _quotaStatuses.TryRemove(GetQuotaKey(connection, null), out _);
    }

    private void ApplyTemporaryModelFailure(
        AiConnectionSettings connection,
        string modelId,
        string message)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        _quotaStatuses[GetQuotaKey(connection, modelId)] = new(
            AiConnectionState.Unavailable,
            now + TimeSpan.FromMinutes(5),
            message,
            now);
    }

    private AiAvailabilityFailureReason DetermineAvailabilityFailureReason(
        IReadOnlyList<AiConnectionSettings> connections,
        Exception? lastError)
    {
        if (lastError is AiProviderHttpException providerError)
        {
            return providerError.StatusCode switch
            {
                HttpStatusCode.Unauthorized => AiAvailabilityFailureReason.Unauthorized,
                HttpStatusCode.Forbidden => AiAvailabilityFailureReason.Forbidden,
                HttpStatusCode.PaymentRequired => AiAvailabilityFailureReason.QuotaExhausted,
                HttpStatusCode.TooManyRequests => AiAvailabilityFailureReason.RateLimited,
                _ => AiAvailabilityFailureReason.TemporarilyUnavailable
            };
        }
        if (lastError is HttpRequestException)
        {
            return AiAvailabilityFailureReason.Network;
        }
        if (lastError is TimeoutException)
        {
            return AiAvailabilityFailureReason.Timeout;
        }

        AiConnectionRuntimeStatus[] quotaStatuses = connections
            .Select(connection => GetQuotaStatus(connection))
            .Where(status => status != null)
            .Cast<AiConnectionRuntimeStatus>()
            .ToArray();
        if (quotaStatuses.Any(status => status.State == AiConnectionState.CoolingDown))
        {
            return AiAvailabilityFailureReason.RateLimited;
        }
        if (quotaStatuses.Any(status => status.State == AiConnectionState.QuotaExhausted))
        {
            return AiAvailabilityFailureReason.QuotaExhausted;
        }

        AiConnectionRuntimeStatus[] connectionStatuses = connections
            .Select(connection => GetConnectionStatus(connection.Id))
            .Where(status => status != null)
            .Cast<AiConnectionRuntimeStatus>()
            .ToArray();
        if (connectionStatuses.Any(status => status.State == AiConnectionState.InvalidCredential))
        {
            return AiAvailabilityFailureReason.Unauthorized;
        }
        if (connectionStatuses.Any(status => status.State == AiConnectionState.PermissionDenied))
        {
            return AiAvailabilityFailureReason.Forbidden;
        }

        return AiAvailabilityFailureReason.TemporarilyUnavailable;
    }

    private static string CreateModelIdentity(string providerId, string modelId) =>
        $"{providerId.Trim()}\n{modelId.Trim()}";

    private static string GetQuotaKeyPrefix(AiConnectionSettings connection) =>
        $"{connection.ProviderId}:{connection.Id}:";

    private static string GetQuotaKey(AiConnectionSettings connection, string? modelId) =>
        $"{GetQuotaKeyPrefix(connection)}{(string.IsNullOrWhiteSpace(modelId) ? "*" : modelId.Trim())}";

    private sealed record AiRoute(AiConnectionSettings Connection, AiModelDescriptor Model);
    private sealed record CachedModels(IReadOnlyList<AiModelDescriptor> Models, DateTimeOffset RefreshedAt);
}
