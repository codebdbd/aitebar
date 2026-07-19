using System.Collections.Concurrent;
using System.Net;

namespace AiteBar;

public sealed class AiGateway
{
    private static readonly TimeSpan ModelCacheLifetime = TimeSpan.FromMinutes(15);
    private readonly AppSettingsService _settingsService;
    private readonly AiProviderClient _providerClient;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, AiConnectionRuntimeStatus> _connectionStatuses = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, AiConnectionRuntimeStatus> _quotaStatuses = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, CachedModels> _modelCache = new(StringComparer.Ordinal);

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
        AppSettings appSettings = _settingsService.Settings;
        AiSettings settings = appSettings.Ai ?? new AiSettings();
        IReadOnlyList<AiConnectionSettings> candidates = BuildCandidates(settings, request);
        if (candidates.Count == 0)
        {
            throw new InvalidOperationException("No enabled AI connections are configured.");
        }

        Exception? lastError = null;
        foreach (AiConnectionSettings connection in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsConnectionAvailable(connection))
            {
                continue;
            }

            try
            {
                IReadOnlyList<AiModelDescriptor> models = await GetModelsCachedAsync(connection, cancellationToken).ConfigureAwait(false);
                AiModelDescriptor? model = SelectModel(settings, connection, models, request);
                if (model == null)
                {
                    continue;
                }

                AiProviderResponse response = await _providerClient.GenerateAsync(
                    connection,
                    model,
                    request,
                    cancellationToken).ConfigureAwait(false);
                MarkSuccessful(connection);
                return new AiGatewayResponse(
                    response.Content,
                    response.ProviderId,
                    connection.Id,
                    response.ModelId,
                    response.PromptTokens,
                    response.CompletionTokens);
            }
            catch (AiProviderHttpException ex)
            {
                lastError = ex;
                ApplyFailure(connection, ex);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastError = ex;
                ApplyTemporaryConnectionFailure(connection, ex.Message);
            }
        }

        throw new InvalidOperationException(
            "No configured AI connection is currently available.",
            lastError);
    }

    internal AiConnectionRuntimeStatus? GetConnectionStatus(string connectionId) =>
        _connectionStatuses.TryGetValue(connectionId, out AiConnectionRuntimeStatus? status)
            ? status
            : null;

    internal AiConnectionRuntimeStatus? GetQuotaStatus(AiConnectionSettings connection) =>
        _quotaStatuses.TryGetValue(GetQuotaKey(connection), out AiConnectionRuntimeStatus? status)
            ? status
            : null;

    internal void ResetConnection(string connectionId)
    {
        _connectionStatuses.TryRemove(connectionId, out _);
        _modelCache.TryRemove(connectionId, out _);
    }

    private IReadOnlyList<AiConnectionSettings> BuildCandidates(AiSettings settings, AiChatRequest request)
    {
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

        return (settings.Connections ?? [])
            .Where(connection => connection.IsEnabled && AiProviderCatalog.TryGet(connection.ProviderId, out _))
            .OrderBy(connection => providerRanks.GetValueOrDefault(connection.ProviderId, int.MaxValue))
            .ThenBy(connection => connection.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private bool IsConnectionAvailable(AiConnectionSettings connection)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        if (_connectionStatuses.TryGetValue(connection.Id, out AiConnectionRuntimeStatus? connectionStatus))
        {
            if (connectionStatus.State is AiConnectionState.InvalidCredential or AiConnectionState.PermissionDenied)
            {
                return false;
            }
            if (connectionStatus.CooldownUntil > now)
            {
                return false;
            }
        }

        if (_quotaStatuses.TryGetValue(GetQuotaKey(connection), out AiConnectionRuntimeStatus? quotaStatus) &&
            quotaStatus.CooldownUntil > now)
        {
            return false;
        }
        return true;
    }

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

        IReadOnlyList<AiModelDescriptor> models = await _providerClient.GetModelsAsync(connection, cancellationToken).ConfigureAwait(false);
        _modelCache[connection.Id] = new CachedModels(models, now);
        return models;
    }

    private static AiModelDescriptor? SelectModel(
        AiSettings settings,
        AiConnectionSettings connection,
        IReadOnlyList<AiModelDescriptor> models,
        AiChatRequest request)
    {
        IEnumerable<AiModelDescriptor> eligible = models.Where(model =>
            !model.IsDeprecated &&
            (model.Capabilities & request.RequiredCapabilities) == request.RequiredCapabilities);
        if (settings.FreeTierOnly)
        {
            eligible = eligible.Where(model =>
                model.CostStatus is AiCostStatus.VerifiedFree or AiCostStatus.FreeTierAvailable);
        }

        AiModelDescriptor[] candidates = eligible.ToArray();
        string? requestedModel = request.PreferredModelId ?? connection.PreferredModelId;
        if (!string.IsNullOrWhiteSpace(requestedModel))
        {
            AiModelDescriptor? selected = candidates.FirstOrDefault(model =>
                string.Equals(model.ModelId, requestedModel, StringComparison.OrdinalIgnoreCase));
            if (selected != null)
            {
                return selected;
            }
        }

        return candidates.FirstOrDefault();
    }

    private void ApplyFailure(AiConnectionSettings connection, AiProviderHttpException exception)
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
                _quotaStatuses[GetQuotaKey(connection)] = new(
                    AiConnectionState.CoolingDown,
                    now + (exception.RetryAfter ?? TimeSpan.FromMinutes(1)),
                    exception.Message,
                    now);
                break;
            case HttpStatusCode.PaymentRequired:
                _quotaStatuses[GetQuotaKey(connection)] = new(
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

    private void MarkSuccessful(AiConnectionSettings connection)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        _connectionStatuses[connection.Id] = new(AiConnectionState.Available, null, null, now);
        _quotaStatuses.TryRemove(GetQuotaKey(connection), out _);
    }

    private static string GetQuotaKey(AiConnectionSettings connection) =>
        $"{connection.ProviderId}:{connection.QuotaScopeId}";

    private sealed record CachedModels(IReadOnlyList<AiModelDescriptor> Models, DateTimeOffset RefreshedAt);
}
