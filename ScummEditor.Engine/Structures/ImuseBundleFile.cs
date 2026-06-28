using System.Collections.Generic;
using System.IO;

namespace ScummEditor.Engine.Structures
{
    /// <summary>One entry in an iMUSE bundle: a named sound resource at [Offset, Offset+Size) in the file.</summary>
    public class ImuseBundleEntry
    {
        public string Name { get; set; }
        public int Offset { get; set; }
        public int Size { get; set; }
    }

    /// <summary>
    /// A SCUMM v7 external iMUSE sound bundle (The Dig's DIGMUSIC.BUN / DIGVOICE.BUN). The original shipped
    /// bundles begin with the tag "LB83" and hold a directory of named entries, each a COMP-compressed (or
    /// plain iMUS) sound resource. The directory is parsed lazily and entries are read on demand, so the
    /// 130-260 MB file is never loaded whole. Decompressing an entry (ImuseBundleDecoder) rebuilds its iMUS
    /// resource; ImuseAudioDecoder then turns that into WAV.
    /// Layout (ScummVM dimuse_bndmgr.cpp): header tag(4 BE) + dirOffset(4 BE) + numFiles(4 BE); the
    /// directory at dirOffset is numFiles entries of [8-byte name][4-byte ext][offset:4 BE][size:4 BE].
    /// </summary>
    public class ImuseBundleFile
    {
        public string FilePath { get; private set; }
        public List<ImuseBundleEntry> Entries { get; private set; } = new List<ImuseBundleEntry>();
        public bool IsValid { get; private set; }

        private bool _parsed;

        public ImuseBundleFile(string filePath)
        {
            FilePath = filePath;
        }

        /// <summary>Reads the bundle directory once (header + entry table). Safe to call repeatedly.</summary>
        public void EnsureParsed()
        {
            if (_parsed) return;
            _parsed = true;

            try
            {
                using (var fs = new FileStream(FilePath, FileMode.Open, FileAccess.Read))
                {
                    string tag = ReadTag(fs);
                    if (tag != "LB83" && tag != "LB23")
                    {
                        return; // not an iMUSE bundle we understand
                    }
                    int dirOffset = ReadUInt32BE(fs);
                    int numFiles = ReadUInt32BE(fs);
                    if (numFiles <= 0 || numFiles > 100000) return;
                    // ReadUInt32BE returns a signed int; a corrupt >2GB offset would wrap negative and make
                    // Seek throw. Guard the directory offset against the real file bounds instead.
                    if (dirOffset < 0 || dirOffset > fs.Length) return;

                    fs.Seek(dirOffset, SeekOrigin.Begin);
                    for (int i = 0; i < numFiles; i++)
                    {
                        // LB83: 8-byte name + 4-byte extension (null-padded); LB23: a single 24-byte name.
                        string name;
                        if (tag == "LB23")
                        {
                            name = ReadFixedString(fs, 24);
                        }
                        else
                        {
                            string baseName = ReadFixedString(fs, 8);
                            string ext = ReadFixedString(fs, 4);
                            name = ext.Length > 0 ? baseName + "." + ext : baseName;
                        }
                        int offset = ReadUInt32BE(fs);
                        int size = ReadUInt32BE(fs);
                        Entries.Add(new ImuseBundleEntry { Name = name, Offset = offset, Size = size });
                    }
                    IsValid = true;
                }
            }
            catch (IOException)
            {
                // Leave IsValid false; the viewer reports an unreadable bundle rather than crashing.
            }
        }

        /// <summary>Reads an entry's raw bytes (the COMP chunk, or a plain iMUS resource) from the file.</summary>
        public byte[] ReadEntryRaw(int index)
        {
            EnsureParsed();
            if (index < 0 || index >= Entries.Count) return null;

            ImuseBundleEntry e = Entries[index];
            using (var fs = new FileStream(FilePath, FileMode.Open, FileAccess.Read))
            {
                if (e.Offset < 0 || e.Offset >= fs.Length) return null;
                int size = e.Size;
                if (e.Offset + (long)size > fs.Length) size = (int)(fs.Length - e.Offset);
                if (size <= 0) return null;

                fs.Seek(e.Offset, SeekOrigin.Begin);
                var buffer = new byte[size];
                int read = 0;
                while (read < size)
                {
                    int n = fs.Read(buffer, read, size - read);
                    if (n <= 0) break;
                    read += n;
                }
                return buffer;
            }
        }

        private static string ReadTag(Stream s)
        {
            var b = new byte[4];
            if (s.Read(b, 0, 4) != 4) return string.Empty;
            return string.Concat((char)b[0], (char)b[1], (char)b[2], (char)b[3]);
        }

        private static string ReadFixedString(Stream s, int length)
        {
            var b = new byte[length];
            int read = s.Read(b, 0, length);
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < read; i++)
            {
                if (b[i] == 0) break;
                sb.Append((char)b[i]);
            }
            return sb.ToString();
        }

        private static int ReadUInt32BE(Stream s)
        {
            var b = new byte[4];
            if (s.Read(b, 0, 4) != 4) return 0;
            return (b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3];
        }
    }
}
