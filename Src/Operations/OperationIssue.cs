namespace GodAmp.Operations;

/// <summary>The stage or capability associated with an operation failure.</summary>
public enum OperationIssueKind { Discovery, Access, UnsupportedFormat, Metadata, Decoding }

/// <summary>The operation that encountered a reported issue.</summary>
public enum OperationKind { Import, Playback, PlaylistSave }

/// <summary>A source or destination problem associated with an application operation.</summary>
/// <param name="Path">Source or destination associated with the issue.</param>
/// <param name="Kind">Stage or capability that failed.</param>
/// <param name="Message">Readable explanation of the failure.</param>
/// <param name="Operation">Application operation that produced the issue.</param>
public sealed record OperationIssue(string Path, OperationIssueKind Kind, string Message, OperationKind Operation = OperationKind.Import);
