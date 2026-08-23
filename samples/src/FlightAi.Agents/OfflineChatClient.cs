using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace FlightAi.Agents;

/// <summary>
/// A deterministic stand-in for a real model-backed <see cref="IChatClient"/> (Azure OpenAI, Microsoft
/// Foundry, or Anthropic via Foundry) so this whole sample runs with <c>dotnet run</c> and no API key.
/// Everything above this class — <c>ChatClientAgent</c>, <c>RunAsync&lt;T&gt;</c>, tool wiring — is the
/// real Microsoft Agent Framework surface and does not change when you swap this out. See the README
/// for the one-line swap to a live model.
/// </summary>
public sealed class OfflineChatClient(Func<IReadOnlyList<ChatMessage>, ChatOptions?, string> respond) : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var list = messages as IReadOnlyList<ChatMessage> ?? messages.ToList();
        var text = respond(list, options);
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, text)));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, options, cancellationToken);
        foreach (var update in response.ToChatResponseUpdates())
            yield return update;
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose() { }
}
