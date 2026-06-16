using System.Collections.Generic;

namespace ScummEditor.Engine.Structures
{
    public class GameInfo
    {
        public int ScummVersion { get; set; }
        public ScummGame LoadedGame { get; set; }
        public bool Xored { get; set; }
        public int XorKey { get; set; }

        /// <summary>
        /// XOR key for the index file. Same as <see cref="XorKey"/> on v5/v6, but 0 (plaintext)
        /// on v4, whose 000.LFL index is not whole-file encrypted.
        /// </summary>
        public int IndexXorKey { get; set; }

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
