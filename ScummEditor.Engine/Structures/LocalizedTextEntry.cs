namespace ScummEditor.Engine.Structures
{
    /// <summary>One editable localized string: a stable key (room.index or define name) and its text.
    /// Shared by the external localized-text files (The Dig LANGUAGE.BND and the .TRS subtitle files).</summary>
    public class LocalizedTextEntry
    {
        public string Key { get; set; }
        public string Text { get; set; }

        // Byte range of the (encoded) text within the file's original bytes, used to rebuild it in place.
        internal int TextStart;
        internal int TextEnd;

        /// <summary>Counts characters that cannot be stored in the game's 8-bit code page - the Latin-1
        /// encoder writes them as '?' - so an import can warn about, e.g., smart quotes pasted from a word
        /// processor that would silently corrupt the saved text.</summary>
        internal static int CountUnmappable(string text)
        {
            int n = 0;
            if (text != null)
            {
                foreach (char c in text)
                {
                    if (c > 0xFF) n++;
                }
            }
            return n;
        }
    }
}
