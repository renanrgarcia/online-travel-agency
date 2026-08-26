using Microsoft.Extensions.AI;

namespace FlightAi.Agents.Services;

/// <summary>
/// A deterministic stand-in for a real model-backed <see cref="IChatClient"/>, so the whole pipeline
/// runs with <c>dotnet run</c> and no API key. See docs/specs/tasks/09-offline-chat-client.md.
/// <para>
/// Responses are keyed by simple substring matching against the last user message — nothing cleverer,
/// so tasks 10-11 can assert on exact output. Register every prompt a test needs with
/// <see cref="RegisterResponse"/> before calling the agent; an unmatched prompt fails loudly rather
/// than returning something misleading.
/// </para>
/// </summary>
public sealed class OfflineChatClient : IChatClient
{
    private readonly List<(string PromptContains, string ResponseText)> _rules = [];

    /// <summary>Registers the response to return whenever the last user message contains
    /// <paramref name="promptContains"/> (ordinal, case-insensitive). Later registrations take
    /// priority when more than one would match, so a specific case can override a broader one.</summary>
    public OfflineChatClient RegisterResponse(string promptContains, string responseText)
    {
        _rules.Add((promptContains, responseText));
        return this;
    }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var prompt = messages.LastOrDefault(m => m.Role == ChatRole.User)?.Text ?? "";
        var (PromptContains, ResponseText) = _rules.LastOrDefault(r => prompt.Contains(r.PromptContains, StringComparison.OrdinalIgnoreCase));

        if (ResponseText is null)
            throw new InvalidOperationException($"OfflineChatClient has no registered response matching prompt: \"{prompt}\"");

        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, ResponseText)));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, options, cancellationToken);
        yield return new ChatResponseUpdate(ChatRole.Assistant, response.Text);
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}
