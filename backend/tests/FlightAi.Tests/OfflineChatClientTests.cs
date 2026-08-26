using FlightAi.Agents.Services;
using Microsoft.Extensions.AI;
using Xunit;

namespace FlightAi.Tests;

/// <summary>
/// One test per eval in docs/specs/tasks/09-offline-chat-client.md.
/// </summary>
public class OfflineChatClientTests
{
    private static ChatMessage UserMessage(string text) => new(ChatRole.User, text);

    [Fact] // E1 — the load-bearing eval: it has to be a genuine IChatClient, not a homemade lookalike
    public void E1_GenuinelyImplementsTheRealIChatClientInterface()
    {
        IChatClient client = new OfflineChatClient(); // fails to compile if this isn't a real IChatClient

        Assert.IsAssignableFrom<IChatClient>(client);
    }

    [Fact] // E2 — determinism: tasks 10/11 assert on exact output
    public async Task E2_SamePromptTwice_ReturnsIdenticalResponse()
    {
        var client = new OfflineChatClient().RegisterResponse("cheapest flight", "canned response");

        var first = await client.GetResponseAsync([UserMessage("cheapest flight from A to B")]);
        var second = await client.GetResponseAsync([UserMessage("cheapest flight from A to B")]);

        Assert.Equal(first.Text, second.Text);
    }

    [Fact] // E3 — actually keyed off input, not a constant
    public async Task E3_DifferentPrompts_ReturnDifferentResponses()
    {
        var client = new OfflineChatClient()
            .RegisterResponse("cheapest", "response A")
            .RegisterResponse("fastest", "response B");

        var a = await client.GetResponseAsync([UserMessage("cheapest flight")]);
        var b = await client.GetResponseAsync([UserMessage("fastest flight")]);

        Assert.NotEqual(a.Text, b.Text);
    }

    [Fact] // E4 — supplies task 11 E3's adversarial input from a realistic source
    public async Task E4_RegisteredMisbehavingResponse_ContainsARawDigitOutsideAnyToken()
    {
        var client = new OfflineChatClient()
            .RegisterResponse("best deal", "The best deal is {{PRICE_OFF1}}, only $999 today!");

        var response = await client.GetResponseAsync([UserMessage("what's the best deal")]);

        Assert.Contains("$999", response.Text);
    }

    [Fact] // E5 — a gap here surfaces as a runtime failure two tasks later, in task 13's streaming
    public async Task E5_StreamingApi_YieldsTheSameTextAsTheNonStreamingResponse()
    {
        var client = new OfflineChatClient().RegisterResponse("hello", "streamed response text");

        var updates = new List<string>();
        await foreach (var update in client.GetStreamingResponseAsync([UserMessage("hello")]))
            updates.Add(update.Text);

        Assert.Equal("streamed response text", string.Concat(updates));
    }

    [Fact] // E6 — consistency with the rest of the system's cancellation discipline
    public async Task E6_CancelledToken_IsHonouredPromptly()
    {
        var client = new OfflineChatClient().RegisterResponse("hello", "response");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => client.GetResponseAsync([UserMessage("hello")], cancellationToken: cts.Token));
    }

    [Fact] // an unmatched prompt fails loudly rather than returning something misleading
    public async Task UnregisteredPrompt_ThrowsRatherThanReturningAMisleadingDefault()
    {
        var client = new OfflineChatClient();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GetResponseAsync([UserMessage("anything")]));
    }
}
