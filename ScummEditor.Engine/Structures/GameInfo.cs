using System.Collections.Generic;

namespace ScummEditor.Engine.Structures
{
    public class GameInfo
    {
        public int ScummVersion { get; set; }
        public ScummGame LoadedGame { get; set; }

        /// <summary>The detected release language (from the index-file MD5 / content heuristic); Unknown if undetermined.</summary>
        public ScummLanguage Language { get; set; } = ScummLanguage.Unknown;
        public bool Xored { get; set; }
        public int XorKey { get; set; }

        /// <summary>
        /// XOR key for the index file. Same as <see cref="XorKey"/> on v5/v6, but 0 (plaintext)
        /// on v4, whose 000.LFL index is not whole-file encrypted.
        /// </summary>
        public int IndexXorKey { get; set; }

        /// <summary>
        /// True when the data/index blocks use the "small header" ([size:4 LE][tag:2]) layout - the
        /// SCUMM v4 games and the GF_OLD256 v3 games (Indy3 VGA, Zak/Loom FM-Towns). v5/v6 use the
        /// "big header" ([tag:4][size:4 BE]); the v3 old-bundle games (Loom EGA) use neither (untagged
        /// uint16-size chunks). Drives BlockBase.IsSmallHeader.
        /// </summary>
        public bool UsesSmallHeader { get; set; }

        /// <summary>
        /// True for the v3 "old bundle" games (Loom EGA, Indy3 EGA, Zak DOS): whole files XOR 0xFF,
        /// a magic-prefixed fixed-layout 00.LFL index, and untagged [size:uint16 LE] room chunks.
        /// These do not use the BlockBase small/big header machinery at all.
        /// </summary>
        public bool UsesOldBundle { get; set; }

        /// <summary>
        /// Bytes per entry in the old-bundle 00.LFL global-object table: 4 for v3 (Loom/Indy3 EGA: 3-byte
        /// class data + 1-byte owner/state) and 1 for v1/v2 (Maniac/Zak: owner/state only). The index
        /// reader skips numGlobalObjects * this to reach the resource directories. Default 4 (v3old).
        /// </summary>
        public int GlobalObjectEntrySize { get; set; } = 4;

        /// <summary>
        /// True for the SCUMM v1 "classic" games (Maniac/Zak DOS floppy, index magic 0x0A31): the 00.LFL
        /// index stores NO resource counts and NO global-object-count word - they are hardcoded per game -
        /// and each directory is a bare [roomno bytes][uint16 offsets] with no count prefix. The v2/v3old
        /// "enhanced" index (magic 0x0100) stores all of them. Drives ScummV3OldBundleIndexFile's classic parse.
        /// </summary>
        public bool UsesClassicIndex { get; set; }

        /// <summary>True when the release ships recorded speech (the CD / talkie edition).</summary>
        public bool IsTalkie { get; set; }

        /// <summary>
        /// True when the release ships ripped CD audio (CDDA.SOU) instead of a speech file -
        /// e.g. the Monkey Island 1 CD edition, whose music lives on CD audio tracks.
        /// </summary>
        public bool HasCdAudio { get; set; }

        /// <summary>
        /// Path of the speech/effects container (MONSTER.SOU or the FM Towns-style
        /// "game".SOU) when the release ships one, regardless of its size; null otherwise.
        /// </summary>
        public string SpeechFilePath { get; set; }

        /// <summary>Path of the ripped CD audio container (CDDA.SOU) when present; null otherwise.</summary>
        public string CdAudioFilePath { get; set; }

        /// <summary>
        /// The graphics edition (EGA/VGA/CD), determined after loading. Used for the status bar;
        /// only meaningful for v4 floppy games (v5/v6 use IsTalkie/HasCdAudio instead).
        /// </summary>
        public GameEdition Edition { get; set; }

        public string IndexFile { get; set; }
        public string DataFile { get; set; }

        /// <summary>
        /// All data containers that make up the game. v5/v6 keep everything in a single file
        /// (so this has one entry); v4 spreads the rooms over several DISKnn.LEC disks.
        /// </summary>
        public List<string> DataFiles { get; set; }

        /// <summary>
        /// Standalone font files (v4 keeps its charsets as separate plaintext 90x.LFL files,
        /// unlike v5/v6 which embed CHAR blocks in the data file). Null/empty for v5/v6.
        /// </summary>
        public List<string> FontFiles { get; set; }
    }
}
