using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace GodAmp.Playlists.Serialization;

/// <summary>Format-specific playlist syntax, independent of encoding and filesystem access.</summary>
internal interface IPlaylistSerializer
{
    /// <summary>Parses decoded text into ordered occurrences without resolving source paths.</summary>
    /// <param name="text">Decoded playlist contents.</param>
    /// <param name="cancellation">Cancels parsing between entries.</param>
    /// <returns>Occurrences including optional cached metadata and duplicates.</returns>
    PlaylistEntry[] Parse(string text, CancellationToken cancellation);

    /// <summary>Writes format syntax without taking ownership of the destination.</summary>
    /// <param name="writer">Text destination owned by the file layer.</param>
    /// <param name="entries">Ordered occurrences whose paths are prepared for the destination.</param>
    void Write(TextWriter writer, IEnumerable<PlaylistEntry> entries);
}
