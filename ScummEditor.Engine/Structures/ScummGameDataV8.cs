using ScummEditor.Engine.Structures.IndexFile;

namespace ScummEditor.Engine.Structures
{
    /// <summary>
    /// Game data for the SCUMM v8 engine (The Curse of Monkey Island). v8 extends v7: the same IFF
    /// "big header" LECF/LOFF/LFLF/ROOM container (not XOR-encrypted), the same AKOS costumes, and the
    /// same external .NUT SMUSH fonts and .BUN iMUSE bundles - so the v7 loader is reused wholesale. v8
    /// differs in:
    ///   - the index file (COMI.LA0): a DRSC block, 4-byte directory counts, a 168-byte MAXS and a DOBJ
    ///     with 40-byte object names, handled by <see cref="ScummV8IndexFile"/>;
    ///   - two data containers (COMI.LA1 + COMI.LA2) instead of one, loaded through the shared multi-disk
    ///     <see cref="ScummGameData.DataDisks"/> path (the same one v4 uses);
    ///   - a remapped script opcode language, a separate RMSC room-scripts block, larger room/object
    ///     headers and 4-byte text escapes - all handled in later milestones by the data-file block
    ///     classes and the v8 disassembler, switched on GameInfo.ScummVersion.
    /// For the foundation milestone the room/RMSC content is read with the generic recursive reader, so
    /// both data files round-trip byte-for-byte before any block gets typed v8 support.
    /// </summary>
    public class ScummGameDataV8 : ScummGameDataV7
    {
        protected override ScummIndexFile CreateIndexFile()
        {
            return new ScummV8IndexFile(LoadedGameInfo);
        }
    }
}
