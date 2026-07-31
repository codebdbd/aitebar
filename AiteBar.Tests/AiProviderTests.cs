using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace AiteBar.Tests;

public sealed class AiProviderTests
{
    [Fact]
    public void Catalog_ContainsSupportedFreeTierProvidersAndExcludesDeepInfra()
    {
        string[] ids = AiProviderCatalog.All.Select(provider => provider.Id).ToArray();

        Assert.Equal(["cerebras", "gemini", "groq", "mistral"], ids);
        Assert.DoesNotContain("deepinfra", ids);
        Assert.DoesNotContain("openrouter", ids);
        Assert.DoesNotContain("github", ids);
        Assert.All(AiProviderCatalog.All, provider => Assert.Equal("https", provider.ModelsUri.Scheme));
    }

    [Fact]
    public void SettingsNormalizer_RejectsUnknownProvidersAndUnsafeCredentialTargets()
    {
        var settings = new AiSettings
        {
            FreeTierOnly = false,
            ProviderOrder = ["unknown", "cerebras", "cerebras"],
            Connections =
            [
                Connection("valid", "cerebras", "AiteBar/AI/valid"),
                Connection("unsafe", "cerebras", "OtherApp/credential"),
                Connection("unknown", "missing", "AiteBar/AI/unknown")
            ]
        };

        AiSettings normalized = AiSettingsNormalizer.Normalize(settings, out bool changed);

        Assert.True(changed);
        Assert.True(normalized.FreeTierOnly);
        Assert.Single(normalized.Connections);
        Assert.Equal("valid", normalized.Connections[0].Id);
        Assert.Equal("cerebras", normalized.ProviderOrder[0]);
        Assert.Equal(AiProviderCatalog.DefaultProviderOrder.Count, normalized.ProviderOrder.Count);
        Assert.Equal(normalized.ProviderOrder.Count, normalized.ProviderOrder.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task Gateway_ExactModel_TriesNextConnectionAfterRateLimit()
    {
        var credentials = new MemoryCredentialStore();
        credentials.Write("AiteBar/AI/one", "key-one");
        credentials.Write("AiteBar/AI/two", "key-two");

        var handler = new RoutingHandler();
        var client = new AiProviderClient(new HttpClient(handler), credentials);
        var settingsService = new AppSettingsService();
        settingsService.Settings = new AppSettings
        {
            Ai = new AiSettings
            {
                FreeTierOnly = true,
                ProviderOrder = ["cerebras"],
                Connections =
                [
                    Connection("one", "cerebras", "AiteBar/AI/one"),
                    Connection("two", "cerebras", "AiteBar/AI/two")
                ]
            }
        };
        var gateway = new AiGateway(settingsService, client, TimeProvider.System);

        AiGatewayResponse response = await gateway.GenerateAsync(new AiChatRequest
        {
            Messages = [new AiChatMessage("user", "hello")],
            PreferredProviderId = "cerebras",
            PreferredModelId = "cerebras-llama-3.3-70b",
            RequireExactModel = true
        });

        Assert.Equal("ok", response.Content);
        Assert.Equal("two", response.ConnectionId);
        Assert.Contains("key-one", handler.SeenKeys);
        Assert.Contains("key-two", handler.SeenKeys);
        Assert.Equal(AiConnectionState.CoolingDown,
            gateway.GetQuotaStatus(
                settingsService.Settings.Ai.Connections[0],
                "cerebras-llama-3.3-70b")?.State);
    }

    [Fact]
    public async Task Gateway_AutomaticMode_ExhaustsSameModelRoutesBeforeChangingModel()
    {
        var credentials = new MemoryCredentialStore();
        credentials.Write("AiteBar/AI/one", "key-one");
        credentials.Write("AiteBar/AI/two", "key-two");
        var handler = new ModelFirstRoutingHandler();
        var client = new AiProviderClient(new HttpClient(handler), credentials);
        var settingsService = new AppSettingsService
        {
            Settings = new AppSettings
            {
                Ai = new AiSettings
                {
                    FreeTierOnly = true,
                    ProviderOrder = ["cerebras"],
                    Connections =
                    [
                        Connection("one", "cerebras", "AiteBar/AI/one"),
                        Connection("two", "cerebras", "AiteBar/AI/two")
                    ]
                }
            }
        };
        settingsService.Settings.Ai.Connections[1].PreferredModelId = "model-b";
        var gateway = new AiGateway(settingsService, client, TimeProvider.System);

        AiGatewayResponse response = await gateway.GenerateAsync(new AiChatRequest
        {
            Messages = [new AiChatMessage("user", "hello")],
            RequireFreeModel = true
        });

        Assert.Equal("model-a", response.ModelId);
        Assert.Equal(["key-one:model-a", "key-two:model-a"], handler.GenerationAttempts);
    }

    [Fact]
    public void Gateway_RequestRequiringFreeModel_ExcludesPaidModelsRegardlessOfGlobalSetting()
    {
        var settings = new AiSettings { FreeTierOnly = false };
        AiConnectionSettings connection = Connection("one", "cerebras", "AiteBar/AI/one");
        AiModelDescriptor paid = Model("paid", AiCostStatus.Paid);
        AiModelDescriptor free = Model("free", AiCostStatus.VerifiedFree);
        var request = new AiChatRequest { RequireFreeModel = true };

        AiModelDescriptor? selected = AiGateway.SelectModel(
            settings,
            connection,
            [paid, free],
            request);
        AiModelDescriptor? unavailable = AiGateway.SelectModel(
            settings,
            connection,
            [paid],
            request);

        Assert.Same(free, selected);
        Assert.Null(unavailable);
    }

    [Fact]
    public void Gateway_RequestRequiringWritingModel_ExcludesNonWritingModels()
    {
        var settings = new AiSettings();
        AiConnectionSettings connection = Connection("one", "cerebras", "AiteBar/AI/one");
        AiModelDescriptor audio = Model("speech-to-text", AiCostStatus.VerifiedFree);
        AiModelDescriptor writing = Model("writer", AiCostStatus.VerifiedFree);
        var request = new AiChatRequest
        {
            RequireFreeModel = true,
            RequireWritingModel = true
        };

        AiModelDescriptor? selected = AiGateway.SelectModel(
            settings,
            connection,
            [audio, writing],
            request);

        Assert.Same(writing, selected);
    }

    [Fact]
    public void Gateway_RequestRequiringContext_ExcludesTooSmallModels()
    {
        var settings = new AiSettings();
        AiConnectionSettings connection = Connection("one", "cerebras", "AiteBar/AI/one");
        var tooSmall = new AiModelDescriptor(
            "cerebras",
            "small",
            "Small",
            AiCapabilities.Text,
            1_000,
            AiCostStatus.VerifiedFree);
        var largeEnough = new AiModelDescriptor(
            "cerebras",
            "large",
            "Large",
            AiCapabilities.Text,
            8_000,
            AiCostStatus.VerifiedFree);
        var request = new AiChatRequest { RequiredContextTokens = 4_000 };

        AiModelDescriptor? selected = AiGateway.SelectModel(
            settings,
            connection,
            [tooSmall, largeEnough],
            request);
        AiModelDescriptor? unavailable = AiGateway.SelectModel(
            settings,
            connection,
            [tooSmall],
            request);

        Assert.Same(largeEnough, selected);
        Assert.Null(unavailable);
    }

    [Fact]
    public async Task Gateway_PreferredConnectionId_UsesOnlyRequestedConnection()
    {
        var credentials = new MemoryCredentialStore();
        credentials.Write("AiteBar/AI/one", "key-one");
        credentials.Write("AiteBar/AI/two", "key-two");
        var handler = new RoutingHandler();
        var client = new AiProviderClient(new HttpClient(handler), credentials);
        var settingsService = new AppSettingsService
        {
            Settings = new AppSettings
            {
                Ai = new AiSettings
                {
                    FreeTierOnly = true,
                    Connections =
                    [
                        Connection("one", "cerebras", "AiteBar/AI/one"),
                        Connection("two", "cerebras", "AiteBar/AI/two")
                    ]
                }
            }
        };
        var gateway = new AiGateway(settingsService, client, TimeProvider.System);

        AiGatewayResponse response = await gateway.GenerateAsync(new AiChatRequest
        {
            Messages = [new AiChatMessage("user", "hello")],
            PreferredConnectionId = "two",
            PreferredProviderId = "cerebras",
            PreferredModelId = "cerebras-llama-3.3-70b",
            RequireExactModel = true
        });

        Assert.Equal("two", response.ConnectionId);
        Assert.NotEmpty(handler.SeenKeys);
        Assert.All(handler.SeenKeys, key => Assert.Equal("key-two", key));
    }

    [Fact]
    public async Task Gateway_StartedStreamFailure_DoesNotStartSecondRoute()
    {
        var credentials = new MemoryCredentialStore();
        credentials.Write("AiteBar/AI/one", "key-one");
        credentials.Write("AiteBar/AI/two", "key-two");
        var handler = new MidStreamFailureHandler();
        var client = new AiProviderClient(new HttpClient(handler), credentials);
        var settingsService = new AppSettingsService
        {
            Settings = new AppSettings
            {
                Ai = new AiSettings
                {
                    FreeTierOnly = true,
                    ProviderOrder = ["cerebras"],
                    Connections =
                    [
                        Connection("one", "cerebras", "AiteBar/AI/one"),
                        Connection("two", "cerebras", "AiteBar/AI/two")
                    ]
                }
            }
        };
        var gateway = new AiGateway(settingsService, client, TimeProvider.System);
        AiGatewayStream stream = await gateway.GenerateStreamingAsync(new AiChatRequest
        {
            Messages = [new AiChatMessage("user", "hello")],
            PreferredProviderId = "cerebras",
            PreferredModelId = "writer",
            RequireExactModel = true
        });
        await using IAsyncEnumerator<string> enumerator = stream.Chunks.GetAsyncEnumerator();

        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal("first", enumerator.Current);
        await Assert.ThrowsAsync<IOException>(() => enumerator.MoveNextAsync().AsTask());
        Assert.Equal(["key-one"], handler.GenerationKeys);
    }

    [Fact]
    public void AiModelSelectionPolicy_AutomaticOrder_IsStableAndPreservesEveryRoute()
    {
        AiConnectionSettings first = Connection("first", "cerebras", "AiteBar/AI/first");
        AiConnectionSettings second = Connection("second", "cerebras", "AiteBar/AI/second");
        first.PreferredModelId = "model-b";
        second.PreferredModelId = "model-a";
        var settings = new AiSettings
        {
            ProviderOrder = ["cerebras"],
            Connections = [first, second]
        };
        var request = new AiChatRequest();
        AiRouteCandidate[] routes =
        [
            Route(second, "model-c", 1),
            Route(first, "model-a", 0),
            Route(second, "model-b", 1),
            Route(first, "model-b", 0),
            Route(second, "model-a", 1),
            Route(first, "model-c", 0),
            Route(first, "model-c", 0)
        ];

        string[] firstOrder = RouteIdentities(
            AiModelSelectionPolicy.OrderRoutes(settings, request, routes));
        first.DisplayName = "ZZ renamed";
        second.DisplayName = "AA renamed";
        string[] secondOrder = RouteIdentities(
            AiModelSelectionPolicy.OrderRoutes(settings, request, routes.Reverse()));

        Assert.Equal(
            [
                "first:cerebras:model-b",
                "second:cerebras:model-b",
                "first:cerebras:model-a",
                "second:cerebras:model-a",
                "first:cerebras:model-c",
                "first:cerebras:model-c",
                "second:cerebras:model-c"
            ],
            firstOrder);
        Assert.Equal(firstOrder, secondOrder);
        Assert.Equal(routes.Length, firstOrder.Length);
    }

    [Fact]
    public void AiModelSelectionPolicy_UsesProviderOrderBeforeModelId()
    {
        AiConnectionSettings cerebras = Connection("c", "cerebras", "AiteBar/AI/c");
        AiConnectionSettings groq = Connection("g", "groq", "AiteBar/AI/g");
        var settings = new AiSettings
        {
            ProviderOrder = ["groq", "cerebras"],
            Connections = [cerebras, groq]
        };

        string[] order = RouteIdentities(AiModelSelectionPolicy.OrderRoutes(
            settings,
            new AiChatRequest(),
            [Route(cerebras, "a-model", 0), Route(groq, "z-model", 1)]));

        Assert.Equal(["g:groq:z-model", "c:cerebras:a-model"], order);
    }

    [Fact]
    public void AiModelSelectionPolicy_KeepsSameModelIdsFromDifferentProvidersSeparate()
    {
        AiConnectionSettings cerebras = Connection("c", "cerebras", "AiteBar/AI/c");
        AiConnectionSettings groq = Connection("g", "groq", "AiteBar/AI/g");
        var settings = new AiSettings
        {
            ProviderOrder = ["cerebras", "groq"],
            Connections = [cerebras, groq]
        };

        string[] order = RouteIdentities(AiModelSelectionPolicy.OrderRoutes(
            settings,
            new AiChatRequest(),
            [Route(groq, "shared", 1), Route(cerebras, "shared", 0)]));

        Assert.Equal(["c:cerebras:shared", "g:groq:shared"], order);
    }

    [Fact]
    public void AiModelSelectionPolicy_ExactOrder_UsesConnectionOrderOnly()
    {
        AiConnectionSettings first = Connection("first", "cerebras", "AiteBar/AI/first");
        AiConnectionSettings second = Connection("second", "cerebras", "AiteBar/AI/second");
        var request = new AiChatRequest
        {
            RequireExactModel = true,
            PreferredProviderId = "cerebras",
            PreferredModelId = "wanted"
        };

        string[] order = RouteIdentities(AiModelSelectionPolicy.OrderRoutes(
            new AiSettings { Connections = [first, second] },
            request,
            [Route(second, "wanted", 1), Route(first, "wanted", 0)]));

        Assert.Equal(
            ["first:cerebras:wanted", "second:cerebras:wanted"],
            order);
    }

    [Theory]
    [InlineData(true, null, "model")]
    [InlineData(true, "cerebras", null)]
    [InlineData(false, "cerebras", null)]
    [InlineData(false, null, "model")]
    public void AiModelSelectionPolicy_InvalidScopedContract_FailsClosed(
        bool exact,
        string? providerId,
        string? modelId)
    {
        var request = new AiChatRequest
        {
            RequireExactModel = exact,
            PreferredProviderId = providerId,
            PreferredModelId = modelId
        };

        Assert.Throws<InvalidOperationException>(() =>
            AiModelSelectionPolicy.OrderRoutes(new AiSettings(), request, []));
    }

    [Fact]
    public void AiModelSelectionPolicy_ExactCandidateMismatch_FailsClosed()
    {
        AiConnectionSettings connection = Connection("one", "cerebras", "AiteBar/AI/one");
        var request = new AiChatRequest
        {
            RequireExactModel = true,
            PreferredProviderId = "cerebras",
            PreferredModelId = "wanted"
        };

        Assert.Throws<InvalidOperationException>(() =>
            AiModelSelectionPolicy.OrderRoutes(
                new AiSettings { Connections = [connection] },
                request,
                [Route(connection, "other", 0)]));
    }

    [Fact]
    public void Gateway_ExactSelection_UsesAllRequestedProviderConnectionsAndNeverChangesModel()
    {
        var settingsService = new AppSettingsService();
        AiConnectionSettings first = Connection("first", "cerebras", "AiteBar/AI/first");
        AiConnectionSettings second = Connection("second", "cerebras", "AiteBar/AI/second");
        AiConnectionSettings otherProvider = Connection("other-provider", "groq", "AiteBar/AI/other-provider");
        var settings = new AiSettings
        {
            Connections = [first, second, otherProvider]
        };
        var gateway = new AiGateway(settingsService);
        var request = new AiChatRequest
        {
            PreferredProviderId = "cerebras",
            PreferredModelId = "wanted",
            RequireExactModel = true,
            RequireFreeModel = true
        };

        IReadOnlyList<AiConnectionSettings> candidates = gateway.BuildCandidates(settings, request);
        AiModelDescriptor? missing = AiGateway.SelectModel(
            settings,
            second,
            [Model("other", AiCostStatus.VerifiedFree)],
            request);
        AiModelDescriptor wanted = Model("wanted", AiCostStatus.VerifiedFree);
        AiModelDescriptor? selected = AiGateway.SelectModel(
            settings,
            second,
            [Model("other", AiCostStatus.VerifiedFree), wanted],
            request);
        second.PreferredModelId = "other";
        AiModelDescriptor? noExplicitModel = AiGateway.SelectModel(
            settings,
            second,
            [Model("other", AiCostStatus.VerifiedFree)],
            new AiChatRequest { RequireExactModel = true });

        Assert.Collection(
            candidates,
            connection => Assert.Equal("first", connection.Id),
            connection => Assert.Equal("second", connection.Id));
        Assert.Null(missing);
        Assert.Same(wanted, selected);
        Assert.Null(noExplicitModel);
    }

    [Fact]
    public async Task Gateway_InvalidatingModelCache_ForcesNextCatalogueRequest()
    {
        var credentials = new MemoryCredentialStore();
        credentials.Write("AiteBar/AI/one", "key-one");
        var handler = new RoutingHandler();
        var client = new AiProviderClient(new HttpClient(handler), credentials);
        var settingsService = new AppSettingsService();
        AiConnectionSettings connection = Connection("one", "cerebras", "AiteBar/AI/one");
        var gateway = new AiGateway(settingsService, client, TimeProvider.System);

        await gateway.GetModelsAsync(connection);
        await gateway.GetModelsAsync(connection);
        Assert.Equal(1, handler.ModelRequestCount);

        gateway.InvalidateModelCache(connection.Id);
        await gateway.GetModelsAsync(connection);

        Assert.Equal(2, handler.ModelRequestCount);
    }

    [Fact]
    public async Task Gateway_LegacyMethods_PreserveExactLegacyRouteOrder()
    {
        var credentials = new MemoryCredentialStore();
        credentials.Write("AiteBar/AI/alpha", "key-alpha");
        credentials.Write("AiteBar/AI/beta", "key-beta");
        credentials.Write("AiteBar/AI/gamma", "key-gamma");
        var handler = new AlwaysThrottledRoutingHandler();
        var client = new AiProviderClient(new HttpClient(handler), credentials);
        var settingsService = new AppSettingsService
        {
            Settings = new AppSettings
            {
                Ai = new AiSettings
                {
                    FreeTierOnly = true,
                    ProviderOrder = ["cerebras", "groq"],
                    Connections =
                    [
                        Connection("alpha", "cerebras", "AiteBar/AI/alpha"),
                        Connection("beta", "groq", "AiteBar/AI/beta"),
                        Connection("gamma", "cerebras", "AiteBar/AI/gamma")
                    ]
                }
            }
        };
        settingsService.Settings.Ai.Connections[0].PreferredModelId = "zebra";
        settingsService.Settings.Ai.Connections[2].PreferredModelId = "apple";
        var gateway = new AiGateway(settingsService, client, TimeProvider.System);

        await Assert.ThrowsAsync<NoAvailableConnectionException>(() =>
            gateway.GenerateAsync(new AiChatRequest
            {
                Messages = [new AiChatMessage("user", "hello")],
                RequireFreeModel = true
            }));

        Assert.Equal(
            [
                "alpha:zebra", "gamma:zebra",
                "alpha:shared", "gamma:shared",
                "alpha:apple", "gamma:apple",
                "beta:zebra", "beta:shared", "beta:apple"
            ],
            handler.Attempts.ToArray());
    }

    [Fact]
    public void Gateway_TextProcessingScoped_UsesDeterministicOrderIgnoringDisplayName()
    {
        AiConnectionSettings alpha = Connection("alpha", "cerebras", "AiteBar/AI/alpha");
        AiConnectionSettings beta = Connection("beta", "groq", "AiteBar/AI/beta");
        AiConnectionSettings gamma = Connection("gamma", "cerebras", "AiteBar/AI/gamma");
        alpha.PreferredModelId = "zebra";
        gamma.PreferredModelId = "apple";
        alpha.DisplayName = "ZZ Last";
        gamma.DisplayName = "AA First";
        var settings = new AiSettings
        {
            ProviderOrder = ["cerebras", "groq"],
            Connections = [alpha, beta, gamma]
        };
        var request = new AiChatRequest { RequireFreeModel = true };
        AiRouteCandidate[] routes =
        [
            Route(beta, "apple", 1),
            Route(alpha, "shared", 0),
            Route(gamma, "zebra", 2),
            Route(alpha, "zebra", 0),
            Route(gamma, "shared", 2),
            Route(beta, "zebra", 1),
            Route(alpha, "apple", 0),
            Route(beta, "shared", 1),
            Route(gamma, "apple", 2)
        ];

        string[] firstOrder = RouteIdentities(AiModelSelectionPolicy.OrderRoutes(settings, request, routes));
        Array.Reverse(routes);
        string[] secondOrder = RouteIdentities(AiModelSelectionPolicy.OrderRoutes(settings, request, routes));

        Assert.Equal(
            [
                "alpha:cerebras:zebra", "gamma:cerebras:zebra",
                "alpha:cerebras:apple", "gamma:cerebras:apple",
                "alpha:cerebras:shared", "gamma:cerebras:shared",
                "beta:groq:apple", "beta:groq:shared", "beta:groq:zebra"
            ],
            firstOrder);
        Assert.Equal(firstOrder, secondOrder);
    }

    [Theory]
    [InlineData(true, null, "m")]
    [InlineData(true, "cerebras", null)]
    [InlineData(false, "cerebras", null)]
    [InlineData(false, null, "m")]
    public async Task Gateway_TextProcessingScoped_InvalidContract_PropagatesFailClosed(
        bool exact,
        string? providerId,
        string? modelId)
    {
        var settingsService = new AppSettingsService
        {
            Settings = new AppSettings
            {
                Ai = new AiSettings
                {
                    FreeTierOnly = true,
                    Connections = [Connection("one", "cerebras", "AiteBar/AI/one")]
                }
            }
        };
        var credentials = new MemoryCredentialStore();
        credentials.Write("AiteBar/AI/one", "k");
        var gateway = new AiGateway(
            settingsService,
            new AiProviderClient(new HttpClient(new OrderNoOpHandler()), credentials),
            TimeProvider.System);
        var request = new AiChatRequest
        {
            Messages = [new AiChatMessage("user", "h")],
            RequireExactModel = exact,
            PreferredProviderId = providerId,
            PreferredModelId = modelId,
            RequireFreeModel = true
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            gateway.GenerateTextProcessingStreamingAsync(request));
    }

    [Fact]
    public async Task SettingsService_DeepClonesAiMetadataWithoutAnyApiKeyProperty()
    {
        string root = Path.Combine(Path.GetTempPath(), $"aitebar-ai-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            string settingsPath = Path.Combine(root, "settings.json");
            var service = new AppSettingsService(Path.Combine(root, "legacy.json"), settingsPath);
            service.Settings = new AppSettings
            {
                Ai = new AiSettings
                {
                    Connections = [Connection("id", "cerebras", "AiteBar/AI/id")]
                }
            };

            AppSettings snapshot = service.Settings;
            snapshot.Ai.Connections[0].DisplayName = "mutated";
            await service.SaveAsync();
            string json = await File.ReadAllTextAsync(settingsPath);

            Assert.NotEqual("mutated", service.Settings.Ai.Connections[0].DisplayName);
            Assert.DoesNotContain("ApiKey", json, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("AiteBar/AI/id", json, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SettingsWindow_ContainsAiSectionAndCredentialLifecycleHandlers()
    {
        string repoRoot = FindRepoRoot();
        string xaml = File.ReadAllText(Path.Combine(repoRoot, "AiteBar", "AppSettingsWindow.xaml"));
        string code = File.ReadAllText(Path.Combine(repoRoot, "AiteBar", "AppSettingsWindow.xaml.cs"));

        Assert.Contains("x:Name=\"AiProvidersSettingsSection\"", xaml);
        Assert.Contains("x:Name=\"AiConnectionsList\"", xaml);
        Assert.Contains("BtnAiAddConnection_Click", xaml);
        Assert.Contains("CleanupPendingAiCredentials", code);
        Assert.Contains("CommitAiCredentialChanges", code);
        Assert.Contains("new WindowsAiCredentialStore()", code);
    }

    [Fact]
    public void ConnectionDialog_LinksToSelectedProvidersOfficialKeyPage()
    {
        string repoRoot = FindRepoRoot();
        string xaml = File.ReadAllText(Path.Combine(repoRoot, "AiteBar", "AiConnectionDialog.xaml"));
        string code = File.ReadAllText(Path.Combine(repoRoot, "AiteBar", "AiConnectionDialog.xaml.cs"));

        Assert.Contains("ResourceKey=AiSettings_GetApiKey", xaml);
        Assert.Contains("LinkGetApiKey_Click", xaml);
        Assert.Contains("provider.DocumentationUri.AbsoluteUri", code);
    }

    private static AiConnectionSettings Connection(
        string id,
        string providerId,
        string target) => new()
    {
        Id = id,
        ProviderId = providerId,
        DisplayName = id,
        CredentialTarget = target,
        IsEnabled = true
    };

    private static AiModelDescriptor Model(string id, AiCostStatus costStatus) => new(
        "cerebras",
        id,
        id,
        AiCapabilities.Text,
        32_000,
        costStatus);

    private static AiRouteCandidate Route(
        AiConnectionSettings connection,
        string modelId,
        int connectionOrder) => new(
            connection,
            new AiModelDescriptor(
                connection.ProviderId,
                modelId,
                modelId,
                AiCapabilities.Text,
                32_000,
                AiCostStatus.VerifiedFree),
            connectionOrder);

    private static string[] RouteIdentities(IEnumerable<AiRouteCandidate> routes) =>
        routes.Select(route =>
            $"{route.Connection.Id}:{route.Connection.ProviderId}:{route.Model.ModelId}").ToArray();

    private sealed class MemoryCredentialStore : IAiCredentialStore
    {
        private readonly Dictionary<string, string> _secrets = new(StringComparer.Ordinal);
        public void Write(string target, string secret) => _secrets[target] = secret;
        public string? Read(string target) => _secrets.GetValueOrDefault(target);
        public bool Delete(string target) => _secrets.Remove(target);
    }

    private sealed class RoutingHandler : HttpMessageHandler
    {
        public List<string> SeenKeys { get; } = [];
        public int ModelRequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            string key = request.Headers.Authorization?.Parameter ?? string.Empty;
            SeenKeys.Add(key);
            if (request.Method == HttpMethod.Get)
            {
                ModelRequestCount++;
                return Task.FromResult(Json(HttpStatusCode.OK,
                    "{\"data\":[{\"id\":\"cerebras-llama-3.3-70b\",\"name\":\"Llama 3.3 70B\"}]}"));
            }
            if (key == "key-one")
            {
                var limited = Json(HttpStatusCode.TooManyRequests, "{\"error\":{\"message\":\"limited\"}}");
                limited.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromMinutes(5));
                return Task.FromResult(limited);
            }
            return Task.FromResult(Json(HttpStatusCode.OK,
                "{\"choices\":[{\"message\":{\"content\":\"ok\"}}],\"usage\":{\"prompt_tokens\":1,\"completion_tokens\":1}}"));
        }

        private static HttpResponseMessage Json(HttpStatusCode statusCode, string json) => new(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private sealed class ModelFirstRoutingHandler : HttpMessageHandler
    {
        public List<string> GenerationAttempts { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            string key = request.Headers.Authorization?.Parameter ?? string.Empty;
            if (request.Method == HttpMethod.Get)
            {
                return Json(HttpStatusCode.OK,
                    "{\"data\":[" +
                    "{\"id\":\"model-a\",\"name\":\"Model A\"}," +
                    "{\"id\":\"model-b\",\"name\":\"Model B\"}]}");
            }

            string payload = await request.Content!.ReadAsStringAsync(cancellationToken);
            using JsonDocument document = JsonDocument.Parse(payload);
            string modelId = document.RootElement.GetProperty("model").GetString() ?? string.Empty;
            GenerationAttempts.Add($"{key}:{modelId}");
            if (key == "key-one" && modelId == "model-a")
            {
                return Json(HttpStatusCode.TooManyRequests, "{\"error\":{\"message\":\"limited\"}}");
            }
            return Json(HttpStatusCode.OK,
                $"{{\"model\":\"{modelId}\",\"choices\":[{{\"message\":{{\"content\":\"ok\"}}}}]}}");
        }

        private static HttpResponseMessage Json(HttpStatusCode statusCode, string json) => new(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private sealed class MidStreamFailureHandler : HttpMessageHandler
    {
        public List<string> GenerationKeys { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            string key = request.Headers.Authorization?.Parameter ?? string.Empty;
            if (request.Method == HttpMethod.Get)
            {
                return Task.FromResult(Json(HttpStatusCode.OK,
                    "{\"data\":[{\"id\":\"writer\",\"name\":\"Writer\"}]}"));
            }

            GenerationKeys.Add(key);
            if (key == "key-one")
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StreamContent(new FailingAfterFirstReadStream(
                        "data: {\"choices\":[{\"delta\":{\"content\":\"first\"}}]}\n\n"))
                });
            }

            return Task.FromResult(Json(HttpStatusCode.OK,
                "data: {\"choices\":[{\"delta\":{\"content\":\"second\"}}]}\n\ndata: [DONE]\n\n"));
        }

        private static HttpResponseMessage Json(HttpStatusCode statusCode, string text) => new(statusCode)
        {
            Content = new StringContent(text, Encoding.UTF8, "application/json")
        };
    }

    private sealed class FailingAfterFirstReadStream(string firstRead) : Stream
    {
        private readonly byte[] _firstRead = Encoding.UTF8.GetBytes(firstRead);
        private int _position;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _firstRead.Length;
        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_position >= _firstRead.Length)
            {
                throw new IOException("stream failed");
            }

            int length = Math.Min(count, _firstRead.Length - _position);
            Array.Copy(_firstRead, _position, buffer, offset, length);
            _position += length;
            return length;
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (_position >= _firstRead.Length)
            {
                return ValueTask.FromException<int>(new IOException("stream failed"));
            }

            int length = Math.Min(buffer.Length, _firstRead.Length - _position);
            _firstRead.AsMemory(_position, length).CopyTo(buffer);
            _position += length;
            return ValueTask.FromResult(length);
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class AlwaysThrottledRoutingHandler : HttpMessageHandler
    {
        public List<string> Attempts { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get)
            {
                string? providerId = request.RequestUri?.Segments.Skip(1).FirstOrDefault()?.TrimEnd('/');
                if (string.Equals(providerId, "groq", StringComparison.OrdinalIgnoreCase))
                {
                    return Json(HttpStatusCode.OK,
                        "{\"data\":[{\"id\":\"shared\",\"name\":\"Shared\"}," +
                        "{\"id\":\"apple\",\"name\":\"Apple\"}," +
                        "{\"id\":\"zebra\",\"name\":\"Zebra\"}]}");
                }
                return Json(HttpStatusCode.OK,
                    "{\"data\":[{\"id\":\"zebra\",\"name\":\"Zebra\"}," +
                    "{\"id\":\"shared\",\"name\":\"Shared\"}," +
                    "{\"id\":\"apple\",\"name\":\"Apple\"}]}");
            }

            string key = request.Headers.Authorization?.Parameter ?? string.Empty;
            string payload = await request.Content!.ReadAsStringAsync(cancellationToken);
            using JsonDocument document = JsonDocument.Parse(payload);
            string modelId = document.RootElement.GetProperty("model").GetString() ?? string.Empty;
            Attempts.Add($"{ConnectionByKey(key)}:{modelId}");
            var limited = Json(HttpStatusCode.TooManyRequests, "{\"error\":{\"message\":\"limited\"}}");
            limited.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromMinutes(5));
            return limited;
        }

        private static string ConnectionByKey(string key) => key switch
        {
            "key-alpha" => "alpha",
            "key-beta" => "beta",
            "key-gamma" => "gamma",
            _ => key
        };

        private static HttpResponseMessage Json(HttpStatusCode statusCode, string text) => new(statusCode)
        {
            Content = new StringContent(text, Encoding.UTF8, "application/json")
        };
    }

    private sealed class OrderNoOpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"data\":[{\"id\":\"test\",\"name\":\"Test\"}]}",
                    Encoding.UTF8,
                    "application/json")
            });
        }
    }

    private static string FindRepoRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "AiteBar.sln")))
            {
                return current;
            }
            current = Directory.GetParent(current)?.FullName;
        }
        throw new DirectoryNotFoundException("Repository root with AiteBar.sln was not found.");
    }
}
