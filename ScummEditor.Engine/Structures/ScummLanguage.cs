namespace ScummEditor.Engine.Structures
{
    /// <summary>
    /// The release language of a detected game. Detected from content (the index-file MD5 matched against
    /// a ScummVM-derived table, with a content heuristic as fallback), never from the folder name.
    /// </summary>
    public enum ScummLanguage
    {
        Unknown = 0,
        English,
        French,
        German,
        Italian,
        Spanish,
        Portuguese,
        Japanese,
        Hebrew,
        Korean,
        Chinese,
        Russian,
        Dutch,
        Swedish,
        Norwegian
    }

    /// <summary>Display names for <see cref="ScummLanguage"/>.</summary>
    public static class ScummLanguageNames
    {
        public static string DisplayName(ScummLanguage language)
        {
            switch (language)
            {
                case ScummLanguage.English: return "English";
                case ScummLanguage.French: return "French";
                case ScummLanguage.German: return "German";
                case ScummLanguage.Italian: return "Italian";
                case ScummLanguage.Spanish: return "Spanish";
                case ScummLanguage.Portuguese: return "Portuguese";
                case ScummLanguage.Japanese: return "Japanese";
                case ScummLanguage.Hebrew: return "Hebrew";
                case ScummLanguage.Korean: return "Korean";
                case ScummLanguage.Chinese: return "Chinese";
                case ScummLanguage.Russian: return "Russian";
                case ScummLanguage.Dutch: return "Dutch";
                case ScummLanguage.Swedish: return "Swedish";
                case ScummLanguage.Norwegian: return "Norwegian";
                default: return "Unknown";
            }
        }
    }
}
