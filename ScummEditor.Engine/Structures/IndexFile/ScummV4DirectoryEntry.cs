using System.Collections.Generic;
using System.IO;

namespace ScummEditor.Engine.Structures.IndexFile
{
    /// <summary>
    /// One entry of a v4 resource directory. Besides the stored room/offset, it carries a link to
    /// the tree block that holds the resource (resolved once at load), so the offset can be
    /// recomputed from that block's new position after edits change resource sizes.
    /// </summary>
    public class ScummV4DirectoryEntry
    {
        public byte RoomNumber { get; set; }
        public uint Offset { get; set; }

        /// <summary>UniqueId of the deepest tree block that contains this resource's bytes (or null if unlinked).</summary>
        public string ContainingBlockId { get; set; }

        /// <summary>Offset of the resource within that containing block.</summary>
        public uint OffsetWithinBlock { get; set; }
    }
}
