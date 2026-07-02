using System.IO;
using System.Text;

namespace ScummEditor.Engine.Encoders
{
    /// <summary>
    /// Shared naming for batch image export/import. The numeric tokens in an exported file name are
    /// zero-padded to at least three digits (Room#001, Room#002, ... Room#010, ... Room#100) so that a
    /// plain directory listing sorts the files in their natural numeric order instead of the lexical
    /// 1, 10, 100, 2, ... order. Numbers with more than three digits keep their full width.
    ///
    /// The export sites use the .NET "D3" format specifier ("Room#{0:D3}.png") to produce the padded
    /// names. This class supplies the import counterpart: <see cref="ResolveForImport"/> finds a file
    /// regardless of the padding width it was exported with, so folders produced by older, unpadded
    /// versions (Room#1, Room#2, ... Room#10) still re-import cleanly.
    /// </summary>
    public static class BatchImageNaming
    {
        /// <summary>Minimum digit count for a numeric token in an exported file name (the "D3" in the format sites).</summary>
        public const int MinDigits = 3;

        /// <summary>
        /// Resolves an exported image path for import. Tries the given (zero-padded) name first; if it is
        /// not present, retries with the leading zeros stripped from every number so a folder exported by an
        /// older, unpadded version still re-imports. Returns the padded path (which the caller will then find
        /// does not exist) when neither candidate is present.
        /// </summary>
        public static string ResolveForImport(string folder, string paddedFileName)
        {
            string padded = Path.Combine(folder, paddedFileName);
            if (File.Exists(padded)) return padded;

            string legacyName = StripLeadingZeros(paddedFileName);
            if (legacyName != paddedFileName)
            {
                string legacy = Path.Combine(folder, legacyName);
                if (File.Exists(legacy)) return legacy;
            }
            return padded;
        }

        /// <summary>
        /// Removes the leading zeros from every run of digits in a file name (an all-zero run collapses to a
        /// single "0"): "Room#010 Obj#000.png" -> "Room#10 Obj#0.png". A name that already has no leading
        /// zeros comes back unchanged, so this is a safe way to derive the pre-padding (legacy) name.
        /// </summary>
        public static string StripLeadingZeros(string name)
        {
            var sb = new StringBuilder(name.Length);
            int i = 0;
            while (i < name.Length)
            {
                char c = name[i];
                if (c < '0' || c > '9') { sb.Append(c); i++; continue; }

                int start = i;
                while (i < name.Length && name[i] >= '0' && name[i] <= '9') i++;
                string digits = name.Substring(start, i - start);
                string trimmed = digits.TrimStart('0');
                sb.Append(trimmed.Length == 0 ? "0" : trimmed);
            }
            return sb.ToString();
        }
    }
}
