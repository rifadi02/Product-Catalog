namespace Catalog.IntegrationTests.Fixtures;

/// <summary>
/// The RFC 7807 envelope from §3.0.1, as the SPA would deserialise it. Declared here rather than
/// reusing the framework's <c>ProblemDetails</c> so the tests assert against the wire contract —
/// including the <c>code</c> and <c>traceId</c> extensions, which are the parts that matter.
/// </summary>
internal sealed record ProblemResponse
{
    [JsonPropertyName("type")] public string? Type { get; init; }
    [JsonPropertyName("title")] public string? Title { get; init; }
    [JsonPropertyName("status")] public int Status { get; init; }
    [JsonPropertyName("detail")] public string? Detail { get; init; }
    [JsonPropertyName("instance")] public string? Instance { get; init; }

    [JsonPropertyName("code")] public string? Code { get; init; }
    [JsonPropertyName("traceId")] public string? TraceId { get; init; }

    [JsonPropertyName("errors")]
    public Dictionary<string, string[]> Errors { get; init; } = [];
}
