using System.Collections.Generic;
using GodAmp.Media.Metadata;
using GodAmp.Operations;

namespace GodAmp.Media.Importing;

internal sealed record ImportResult(IReadOnlyList<TrackMetadata> Tracks, IReadOnlyList<OperationIssue> Issues, bool EmptyPlaylist);
public sealed record ImportProgress(int Completed, int Total, string Path);
