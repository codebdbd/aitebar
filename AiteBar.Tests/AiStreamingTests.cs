using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;

namespace AiteBar.Tests;

public sealed class AiStreamingTests
{
    [Fact]
    public void RequestTimeout_AllowsLongStreamingGeneration()
    {
        Assert.Equal(TimeSpan.FromMinutes(5), AiProviderClient.RequestTimeout);
        Assert.True(AiProviderClient.RequestTimeout > AiProviderClient.StreamInactivityTimeout);
    }

    [Fact]
    public void ParseOpenAiStreamData_ReturnsContentDelta()
    {
        string? content = AiProviderClient.ParseOpenAiStreamData(
            "data: {\"choices\":[{\"delta\":{\"content\":\"исправ\"}}]}");

        Assert.Equal("исправ", content);
        Assert.Null(AiProviderClient.ParseOpenAiStreamData("data: [DONE]"));
    }

    [Fact]
    public void ParseGeminiStreamData_ReturnsCandidateParts()
    {
        string? content = AiProviderClient.ParseGeminiStreamData(
            "data: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"ис\"},{\"text\":\"прав\"}]}}]}");

        Assert.Equal("исправ", content);
    }

    [Fact]
    public void StreamingPreview_HidesPartialMarkerAndRestoresCompleteMarker()
    {
        var service = new TextProcessingService();
        ProtectedText protectedText = service.ProtectTechnicalFragments("До https://example.com после");
        string marker = Assert.Single(protectedText.Fragments).Key;
        string prefix = protectedText.Text[..protectedText.Text.IndexOf(marker, StringComparison.Ordinal)];

        Assert.Equal(
            prefix,
            TextProcessingWindow.BuildStreamingPreview(prefix + marker[..10], protectedText));
        Assert.Equal(
            "До https://example.com после",
            TextProcessingWindow.BuildStreamingPreview(protectedText.Text, protectedText));
    }

    [Fact]
    public async Task OpenAiCompatibleStreaming_SendsStreamingRequestAndReadsCompleteHttpBody()
    {
        var credentials = new MemoryCredentialStore();
        credentials.Write("AiteBar/AI/test", "secret");
        var handler = new DelegateHandler(async request =>
        {
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("secret", request.Headers.Authorization?.Parameter);
            string payload = await request.Content!.ReadAsStringAsync();
            Assert.Contains("\"stream\":true", payload, StringComparison.Ordinal);
            return Sse(
                "data: {\"choices\":[{\"delta\":{\"content\":\"ис\"}}]}\n\n" +
                "data: {\"choices\":[{\"delta\":{\"content\":\"прав\"}}]}\n\n" +
                "data: [DONE]\n\n");
        });
        var client = new AiProviderClient(new HttpClient(handler), credentials);

        AiProviderStream stream = await client.GenerateStreamingAsync(
            Connection("cerebras"),
            Model("cerebras"),
            Request(),
            CancellationToken.None);

        Assert.Equal("исправ", await CollectAsync(stream.Chunks));
    }

    [Fact]
    public async Task GeminiStreaming_UsesSseEndpointAndReadsCompleteHttpBody()
    {
        var credentials = new MemoryCredentialStore();
        credentials.Write("AiteBar/AI/test", "secret");
        var handler = new DelegateHandler(async request =>
        {
            Assert.Contains(":streamGenerateContent", request.RequestUri!.AbsoluteUri, StringComparison.Ordinal);
            Assert.Contains("alt=sse", request.RequestUri.Query, StringComparison.Ordinal);
            string payload = await request.Content!.ReadAsStringAsync();
            Assert.Contains("\"systemInstruction\"", payload, StringComparison.Ordinal);
            return Sse(
                "data: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"ис\"}]}}]}\n\n" +
                "data: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"прав\"}]}}]}\n\n");
        });
        var client = new AiProviderClient(new HttpClient(handler), credentials);

        AiProviderStream stream = await client.GenerateStreamingAsync(
            Connection("gemini"),
            Model("gemini"),
            Request(),
            CancellationToken.None);

        Assert.Equal("исправ", await CollectAsync(stream.Chunks));
    }

    [Fact]
    public async Task Gateway_MarksConnectionSuccessfulOnlyAfterStreamCompletes()
    {
        var credentials = new MemoryCredentialStore();
        credentials.Write("AiteBar/AI/test", "secret");
        var handler = new DelegateHandler(request => Task.FromResult(
            request.Method == HttpMethod.Get
                ? Json("{\"data\":[{\"id\":\"writer\",\"name\":\"Writer\"}]}")
                : Sse("data: {\"choices\":[{\"delta\":{\"content\":\"ok\"}}]}\n\ndata: [DONE]\n\n")));
        var client = new AiProviderClient(new HttpClient(handler), credentials);
        var settings = Settings("cerebras");
        var gateway = new AiGateway(settings, client, TimeProvider.System);

        AiGatewayStream stream = await gateway.GenerateStreamingAsync(Request());

        Assert.NotEqual(AiConnectionState.Available, gateway.GetConnectionStatus("test")?.State);
        Assert.Equal("ok", await CollectAsync(stream.Chunks));
        Assert.Equal(AiConnectionState.Available, gateway.GetConnectionStatus("test")?.State);
    }

    [Fact]
    public async Task Gateway_MarksConnectionUnavailableWhenStartedStreamFails()
    {
        var credentials = new MemoryCredentialStore();
        credentials.Write("AiteBar/AI/test", "secret");
        var handler = new DelegateHandler(request => Task.FromResult(
            request.Method == HttpMethod.Get
                ? Json("{\"data\":[{\"id\":\"writer\",\"name\":\"Writer\"}]}")
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StreamContent(new FailingAfterFirstReadStream(
                        "data: {\"choices\":[{\"delta\":{\"content\":\"first\"}}]}\n\n"))
                }));
        var client = new AiProviderClient(new HttpClient(handler), credentials);
        var settings = Settings("cerebras");
        var gateway = new AiGateway(settings, client, TimeProvider.System);
        AiGatewayStream stream = await gateway.GenerateStreamingAsync(Request());
        await using IAsyncEnumerator<string> enumerator = stream.Chunks.GetAsyncEnumerator();

        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal("first", enumerator.Current);
        await Assert.ThrowsAsync<IOException>(() => enumerator.MoveNextAsync().AsTask());
        Assert.Equal(AiConnectionState.Unavailable, gateway.GetConnectionStatus("test")?.State);
    }

    [Fact]
    public async Task StreamRead_ThrowsTimeoutAfterConfiguredInactivity()
    {
        await Assert.ThrowsAsync<TimeoutException>(() =>
            AiProviderClient.ReadLineWithInactivityTimeoutAsync(
                new BlockingTextReader(),
                TimeSpan.FromMilliseconds(20),
                CancellationToken.None));
    }

    private static AiChatRequest Request() => new()
    {
        Messages =
        [
            new AiChatMessage("system", "instruction"),
            new AiChatMessage("user", "text")
        ],
        RequireFreeModel = true,
        RequireWritingModel = true
    };

    private static AiConnectionSettings Connection(string providerId) => new()
    {
        Id = "test",
        ProviderId = providerId,
        DisplayName = "Test",
        CredentialTarget = "AiteBar/AI/test",
        IsEnabled = true
    };

    private static AiModelDescriptor Model(string providerId) => new(
        providerId,
        "writer",
        "Writer",
        AiCapabilities.Text,
        32_000,
        AiCostStatus.VerifiedFree);

    private static AppSettingsService Settings(string providerId)
    {
        var settings = new AppSettingsService();
        settings.Settings = new AppSettings
        {
            Ai = new AiSettings
            {
                Connections = [Connection(providerId)],
                ProviderOrder = [providerId]
            }
        };
        return settings;
    }

    private static async Task<string> CollectAsync(IAsyncEnumerable<string> chunks)
    {
        var result = new StringBuilder();
        await foreach (string chunk in chunks)
        {
            result.Append(chunk);
        }
        return result.ToString();
    }

    private static HttpResponseMessage Sse(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "text/event-stream")
    };

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private sealed class MemoryCredentialStore : IAiCredentialStore
    {
        private readonly Dictionary<string, string> _secrets = new(StringComparer.Ordinal);
        public void Write(string target, string secret) => _secrets[target] = secret;
        public string? Read(string target) => _secrets.GetValueOrDefault(target);
        public bool Delete(string target) => _secrets.Remove(target);
    }

    private sealed class DelegateHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => handler(request);
    }

    private sealed class BlockingTextReader : TextReader
    {
        public override async ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return null;
        }
    }

    private sealed class FailingAfterFirstReadStream(string firstRead) : Stream
    {
        private readonly byte[] _firstRead = Encoding.UTF8.GetBytes(firstRead);
        private int _position;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _firstRead.Length;
        public override long Position { get => _position; set => throw new NotSupportedException(); }

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
}
