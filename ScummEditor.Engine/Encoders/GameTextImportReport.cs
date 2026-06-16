using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;

namespace ScummEditor.Engine.Encoders
{
    public class GameTextImportReport
    {
        public int LinesParsed { get; set; }
        public int EntriesMatched { get; set; }
        public int StringsChanged { get; set; }
        public int BlocksRebuilt { get; set; }
        public List<string> Errors = new List<string>();
        public List<string> Warnings = new List<string>();
        public List<string> GlyphNotes = new List<string>();

        public bool HasChanges { get { return StringsChanged > 0; } }

        public string Summary()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Text lines read: " + LinesParsed);
            sb.AppendLine("Texts matched in the game: " + EntriesMatched);
            sb.AppendLine("Texts changed: " + StringsChanged);
            sb.AppendLine("Blocks rebuilt: " + BlocksRebuilt);
            if (Errors.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("ERRORS (" + Errors.Count + "):");
                for (int i = 0; i < Errors.Count && i < 20; i++) sb.AppendLine("  " + Errors[i]);
                if (Errors.Count > 20) sb.AppendLine("  ... and " + (Errors.Count - 20) + " more");
            }
            if (Warnings.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Warnings (" + Warnings.Count + "):");
                for (int i = 0; i < Warnings.Count && i < 20; i++) sb.AppendLine("  " + Warnings[i]);
                if (Warnings.Count > 20) sb.AppendLine("  ... and " + (Warnings.Count - 20) + " more");
            }
            if (GlyphNotes.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Fonts (CHAR) x characters used:");
                foreach (string n in GlyphNotes) sb.AppendLine("  " + n);
            }
            return sb.ToString();
        }
    }
}
