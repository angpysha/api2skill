namespace Api2Skill.Tests;

/// <summary>
/// Every test class that starts a real <see cref="System.Net.HttpListener"/> shares this
/// collection so xUnit runs them sequentially relative to each other (collections still run in
/// parallel with each other — only classes within the same collection are serialized).
///
/// This exists because <see cref="LoopbackHttpListenerFactory"/>'s own bind-retry logic wasn't
/// enough on its own: the cross-platform managed <see cref="System.Net.HttpListener"/>
/// implementation (used on macOS/Linux — native http.sys is Windows-only) tracks
/// prefix-&gt;listener mappings in a process-wide internal registry, and running multiple
/// listener instances concurrently — even on genuinely distinct ports — was still observed to
/// intermittently throw <c>HttpListenerException: Address already in use</c> from inside
/// <c>HttpListener.Close()</c>, not just from <c>Start()</c>. Serializing every test class that
/// starts a real HttpListener (including hosted-relay stubs) removed the flakiness where the
/// retry-on-bind fix alone did not.
///
/// No <c>DisableParallelization</c> property is needed here — that's an xUnit v2
/// <c>[CollectionBehavior]</c> (assembly-level) concern. Membership in the same named
/// collection is what serializes these classes relative to each other.
/// </summary>
[CollectionDefinition("LoopbackHttp")]
public class LoopbackHttpCollection;
