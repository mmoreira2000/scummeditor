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
    }
}
