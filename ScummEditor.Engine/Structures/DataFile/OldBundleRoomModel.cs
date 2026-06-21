using System.Collections.Generic;

namespace ScummEditor.Engine.Structures.DataFile
{
    /// <summary>What kind of v1/v2/v3-old bytecode a navigable code range holds (for labelling/grouping).</summary>
    public enum OldBundleCodeKind { EntryScript, ExitScript, LocalScript, GlobalScript, ObjectVerb }

    /// <summary>
    /// A navigable bytecode range inside a v2 / v3-old room file (RawContent-relative). The end is a
    /// generous upper bound (the next structural element); the disassembler self-terminates at the
    /// script's stop opcode, so the viewer shows the real script regardless of a loose end.
    /// </summary>
    public class OldBundleCodeRange
    {
        public OldBundleCodeKind Kind;
        public string Label;   // human label, e.g. "Entry script", "Global script 12", "verb 2"
        public int Number;     // local/global script id, or verb id; -1 when not applicable
        public int Start;      // bytecode start offset within the room RawContent
        public int End;        // exclusive upper bound within the room RawContent
    }

    /// <summary>
    /// One object of a v2 / v3-old room, as the tree/viewers need it: its index and stored id, the
    /// decoded name, declared size, whether it owns a decodable image / z-plane, and its verb-code
    /// ranges (the per-verb bytecode segments).
    /// </summary>
    public class OldBundleObjectInfo
    {
        public int Index;      // 0-based object index in the room object table
        public int Id;         // SCUMM object id stored in the OBCD
        public string Name;    // decoded object name (may be empty)
        public int Width;
        public int Height;
        public bool HasImage;  // the object owns a decodable image
        public bool HasZPlane; // the object owns a decodable z-plane (v3-old only)
        public List<OldBundleCodeRange> VerbCode = new List<OldBundleCodeRange>();
    }

    /// <summary>
    /// The navigable model of a single v2 / v3-old room (NN.LFL): its dimensions, every object (with
    /// image/code), and the room/local/global scripts that belong to it. Built by OldBundleNavigator and
    /// consumed by the GUI tree; carries only positions/ranges, never decoded pixels or rebuilt bytes.
    /// </summary>
    public class OldBundleRoomModel
    {
        public int RoomNo;
        public bool IsV2;
        public bool IsV1;   // v1 classic (Maniac/Zak DOS): same room layout as v2 but the GdiV1 image codec
        public bool IsIndy3;
        public int Width;
        public int Height;
        public int NumObjects;
        public int NumSounds;
        public int NumScripts;
        public bool HasBackground;        // the room background decodes
        public bool HasBackgroundZPlane;  // the room walk-behind (z-plane) decodes
        public List<OldBundleObjectInfo> Objects = new List<OldBundleObjectInfo>();
        public List<OldBundleCodeRange> Scripts = new List<OldBundleCodeRange>(); // entry/exit/local/global
    }
}
