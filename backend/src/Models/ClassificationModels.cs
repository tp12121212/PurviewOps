namespace PurviewOps.Api.Models;

public sealed record TestTextRequest(string Text, string? Locale);
public sealed record TestMessageRequest(string Subject, string Body, IReadOnlyList<string> Recipients);
public sealed record TestFileRequest(string FileName, string ContentType, byte[] Content);
