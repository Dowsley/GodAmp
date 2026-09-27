namespace GodAmp.Audio;

public enum AudioIssueKind { Discovery, Access, UnsupportedFormat, Metadata, Decoding }

/// <summary>A source-specific problem; metadata issues permit filename fallback.</summary>
public sealed record AudioIssue(string Path, AudioIssueKind Kind, string Message);
