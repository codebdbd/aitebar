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
    public async Task Gateway_TriesNextConnectionAfterRateLimit()
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
            Messages = [new AiChatMessage("user", "hello")]
        });

        Assert.Equal("ok", response.Content);
        Assert.Equal("two", response.ConnectionId);
        Assert.Contains("key-one", handler.SeenKeys);
        Assert.Contains("key-two", handler.SeenKeys);
        Assert.Equal(AiConnectionState.CoolingDown,
            gateway.GetQuotaStatus(settingsService.Settings.Ai.Connections[0])?.State);
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

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            string key = request.Headers.Authorization?.Parameter ?? string.Empty;
            SeenKeys.Add(key);
            if (request.Method == HttpMethod.Get)
            {
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
