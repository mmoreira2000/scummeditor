using System.Collections.Generic;

namespace ScummEditor.Engine.Structures
{
    /// <summary>
    /// An external localized-text file (The Dig LANGUAGE.BND or a .TRS subtitle/UI file). Exposes its
    /// editable strings as keyed entries, rebuilds the file bytes from the (possibly edited) entries
    /// byte-identically when unchanged, and round-trips through a plain KEY&lt;TAB&gt;TEXT export/import.
    /// </summary>
    public interface ILocalizedTextFile
    {
        string FilePath { get; }
        string FileName { get; }
        bool IsValid { get; }

        /// <summary>The editable strings. Editing an entry's Text and then calling BuildContent applies it.</summary>
        IReadOnlyList<LocalizedTextEntry> Entries { get; }

        /// <summary>The current file bytes (the template with edited entries re-encoded in place).</summary>
        byte[] BuildContent();

        /// <summary>Dumps the strings as KEY&lt;TAB&gt;TEXT lines for external editing.</summary>
        string ExportToText();

        /// <summary>Applies edits from a KEY&lt;TAB&gt;TEXT dump; returns a short report.</summary>
        string ImportFromText(string content);

        /// <summary>Normalises a string just edited in the GUI to this file's structural rules before it is
        /// stored as an entry's Text, so the value written back stays faithful to the file's format: a
        /// LANGUAGE.BND collapses any newline to a space (one message per line), a .TRS converts newlines to
        /// the file's own native terminator (so editing a Mac LF file does not silently turn it into CRLF).</summary>
        string NormalizeEditedText(string text);
    }
}
