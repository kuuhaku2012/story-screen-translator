using System.Net;
using System.Text;
using System.Text.Json;
using StoryTranslator.Core;

namespace StoryTranslator.Core.Tests;

public sealed class GroqTextTranslatorTests
{
    [Fact]
    public async Task SpatialBatchMapsOutOfOrderIdsAndCachesIndividualBlocks()
    {
        var handler = new Handler(async (request, token) =>
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            Assert.Equal("json_schema", body.RootElement.GetProperty("response_format").GetProperty("type").GetString());
            using var input = JsonDocument.Parse(body.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!);
            Assert.Equal(2, input.RootElement.GetArrayLength());
            return Response("{\"translations\":[{\"id\":1,\"text\":\"Phải\"},{\"id\":0,\"text\":\"Trái\"}]}");
        });
        using var client = new HttpClient(handler);
        var translator = new GroqTextTranslator(client, "test-secret", "openai/gpt-oss-20b");
        Assert.Equal(new[] { "Trái", "Phải" }, await translator.TranslateBatchAsync(new[] { "Left", "Right" }, default));
        Assert.Equal(new[] { "Phải", "Trái" }, await translator.TranslateBatchAsync(new[] { "Right", "Left" }, default));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task SpatialBatchSendsDuplicateTextOnlyOnceAndMapsItBackToEveryItem()
    {
        var handler = new Handler(async (request, token) =>
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            using var input = JsonDocument.Parse(body.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!);
            Assert.Equal(2, input.RootElement.GetArrayLength());
            return Response("{\"translations\":[{\"id\":0,\"text\":\"Xin chào\"},{\"id\":1,\"text\":\"Tạm biệt\"}]}");
        });
        using var client = new HttpClient(handler);
        var translator = new GroqTextTranslator(client, "test-secret", "openai/gpt-oss-20b");

        var translated = await translator.TranslateBatchAsync(new[] { "Hello", "Hello", "Goodbye" }, default);

        Assert.Equal(new[] { "Xin chào", "Xin chào", "Tạm biệt" }, translated);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task SuccessfulRequestDoesNotAddArtificialCooldown()
    {
        var limiter = new GroqRateLimiter();
        using var client = new HttpClient(new Handler((_, _) => Task.FromResult(Response("Xin chào"))));
        var translator = new GroqTextTranslator(client, "test-secret", "llama-3.3-70b-versatile", limiter);

        await translator.TranslateAsync("Hello", "vi", default);

        Assert.Equal(TimeSpan.Zero, limiter.RemainingDelay);
    }

    [Fact]
    public async Task ExhaustedRequestBudgetUsesProviderResetHeader()
    {
        var limiter = new GroqRateLimiter();
        var response = Response("Xin chào");
        response.Headers.TryAddWithoutValidation("x-ratelimit-remaining-requests", "0");
        response.Headers.TryAddWithoutValidation("x-ratelimit-reset-requests", "12s");
        using var client = new HttpClient(new Handler((_, _) => Task.FromResult(response)));
        var translator = new GroqTextTranslator(client, "test-secret", "llama-3.3-70b-versatile", limiter);

        await translator.TranslateAsync("Hello", "vi", default);

        Assert.InRange(limiter.RemainingDelay, TimeSpan.FromSeconds(11), TimeSpan.FromSeconds(13));
    }

    [Fact]
    public async Task RateLimitUsesProviderResetHeaderInsteadOfGenericLongDelay()
    {
        var limiter = new GroqRateLimiter();
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StringContent("private error") };
        response.Headers.TryAddWithoutValidation("x-ratelimit-remaining-requests", "0");
        response.Headers.TryAddWithoutValidation("x-ratelimit-reset-requests", "5s");
        using var client = new HttpClient(new Handler((_, _) => Task.FromResult(response)));
        var translator = new GroqTextTranslator(client, "test-secret", "llama-3.3-70b-versatile", limiter);

        await Assert.ThrowsAsync<GroqRateLimitException>(() => translator.TranslateAsync("Hello", "vi", default));

        Assert.InRange(limiter.RemainingDelay, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(7));
    }

    [Fact]
    public async Task SpatialBatchRejectsMissingIdsInsteadOfPlacingTextOnWrongRegion()
    {
        using var client = new HttpClient(new Handler((_, _) => Task.FromResult(Response("{\"translations\":[{\"id\":0,\"text\":\"Trái\"}]}"))));
        var translator = new GroqTextTranslator(client, "test-secret", "openai/gpt-oss-20b");
        await Assert.ThrowsAsync<InvalidOperationException>(() => translator.TranslateBatchAsync(new[] { "Left", "Right" }, default));
    }

    [Fact]
    public async Task LongScreenTextIsSplitWithoutLosingTextAndCachedInMemory()
    {
        var chunks = new List<string>();
        var source = string.Concat(Enumerable.Range(0, 700).Select(i => $"Line {i} contains English screen text.\n"));
        var handler = new Handler(async (request, token) =>
        {
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            var chunk = json.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!;
            Assert.InRange(chunk.Length, 1, 1800);
            Assert.Equal("low", json.RootElement.GetProperty("reasoning_effort").GetString());
            chunks.Add(chunk);
            return Response("Bản dịch");
        });
        using var client = new HttpClient(handler);
        var translator = new GroqTextTranslator(client, "test-secret", "openai/gpt-oss-20b");
        var first = await translator.TranslateAsync(source, "vi", default);
        Assert.Equal(source, string.Concat(chunks));
        Assert.True(handler.Calls > 1);
        var calls = handler.Calls;
        Assert.Equal(first, await translator.TranslateAsync(source, "vi", default));
        Assert.Equal(calls, handler.Calls);
    }

    [Fact]
    public async Task GlossaryPlaceholderIsNotSplitAtChunkBoundary()
    {
        var chunks = new List<string>();
        var source = new string('a', 1795) + "⟬STG_0001⟭" + new string('b', 2400);
        var handler = new Handler(async (request, token) =>
        {
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            var text = json.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!;
            chunks.Add(text);
            return Response(text);
        });
        using var client = new HttpClient(handler);
        var translator = new GroqTextTranslator(client, "test-secret", "openai/gpt-oss-20b");
        await translator.TranslateAsync(source, "vi", default);
        Assert.Equal(source, string.Concat(chunks));
        Assert.Single(chunks, c => c.Contains("⟬STG_0001⟭"));
    }

    [Fact]
    public async Task SendsOnlyTextAndReturnsVietnameseWithoutExposingKeyInBody()
    {
        var handler = new Handler(async (request, token) =>
        {
            Assert.Equal("https://api.groq.com/openai/v1/chat/completions", request.RequestUri!.AbsoluteUri);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal("test-secret", request.Headers.Authorization.Parameter);
            var body = await request.Content!.ReadAsStringAsync(token);
            Assert.DoesNotContain("test-secret", body);
            using var json = JsonDocument.Parse(body);
            Assert.Equal("Hello Rover", json.RootElement.GetProperty("messages")[1].GetProperty("content").GetString());
            return Response("Xin chào Rover");
        });
        using var client = new HttpClient(handler);
        var translator = new GroqTextTranslator(client, "test-secret", "llama-3.3-70b-versatile");
        Assert.Equal("Xin chào Rover", await translator.TranslateAsync("Hello Rover", "vi", default));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task RateLimitDoesNotRetryOrIncludeServerBodyInError()
    {
        var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        { Content = new StringContent("private-echo-test-secret") }));
        using var client = new HttpClient(handler);
        var translator = new GroqTextTranslator(client, "test-secret", "llama-3.3-70b-versatile");
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => translator.TranslateAsync("Hello", "vi", default));
        Assert.Contains("429", error.Message);
        Assert.DoesNotContain("private-echo", error.Message);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task RejectsLostGlossaryToken()
    {
        var handler = new Handler((_, _) => Task.FromResult(Response("Xin chào")));
        using var client = new HttpClient(handler);
        var translator = new GroqTextTranslator(client, "test-secret", "llama-3.3-70b-versatile");
        await Assert.ThrowsAsync<InvalidOperationException>(() => translator.TranslateAsync("Hello ⟬STG_0001⟭", "vi", default));
    }

    [Fact]
    public async Task CancellationReachesHttpRequest()
    {
        var handler = new Handler(async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return Response("unused");
        });
        using var client = new HttpClient(handler);
        var translator = new GroqTextTranslator(client, "test-secret", "llama-3.3-70b-versatile");
        using var cancellation = new CancellationTokenSource();
        var pending = translator.TranslateAsync("Hello", "vi", cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    private static HttpResponseMessage Response(string text) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = text }, finish_reason = "stop" } } }), Encoding.UTF8, "application/json")
    };

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return send(request, cancellationToken);
        }
    }
}
