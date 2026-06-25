using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;

namespace ScummEditor.Gui
{
    /// <summary>
    /// Friendly viewer for the SCUMM v7 index meta blocks that are kept verbatim (RawIndexBlock): RNAM
    /// (room names), MAXS (engine maximums + version strings), ANAM (audio resource names), DOBJ (global
    /// object owner/class table) and AARY (arrays). It DECODES each for display (read-only) instead of
    /// the raw hex view; the underlying block stays byte-for-byte (no engine change, round-trip intact).
    /// Formats mirror ScummVM resource.cpp (ScummEngine_v7::readMAXS / readIndexBlock / readRNAM).
    /// </summary>
    public class V7IndexBlockControl : BlockBaseControl
    {
        private readonly TextBox _text;

        public V7IndexBlockControl()
        {
            _text = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Font = new Font("Consolas", 9F),
                BackColor = Color.White,
            };
            Controls.Add(_text);
        }

        public override void SetAndRefreshData(BlockBase blockBase)
        {
            base.SetAndRefreshData(blockBase);

            var raw = blockBase as IRawContentBlock;
            byte[] body = raw != null ? raw.Contents : null;
            _text.Text = Decode(blockBase.BlockType, body).Replace("\n", "\r\n");
        }

        private static string Decode(string tag, byte[] body)
        {
            if (body == null) return "(no data)";
            switch (tag)
            {
                case "RNAM": return DecodeRoomNames(body);
                case "MAXS": return DecodeMaxs(body);
                case "ANAM": return DecodeAudioNames(body);
                case "DOBJ": return DecodeObjects(body);
                case "AARY": return DecodeArrays(body);
                default: return HexAndStrings(tag, body);
            }
        }

        /// <summary>RNAM: [room#:1][name:9 XOR 0xFF] entries, terminated by room#==0.</summary>
        private static string DecodeRoomNames(byte[] b)
        {
            var sb = new StringBuilder("RNAM - room names\n\n");
            int p = 0, count = 0;
            while (p < b.Length)
            {
                int room = b[p++];
                if (room == 0) break;
                if (p + 9 > b.Length) break;
                var name = new char[9];
                int n = 0;
                for (int i = 0; i < 9; i++)
                {
                    byte c = (byte)(b[p + i] ^ 0xFF);
                    if (c == 0) break;
                    name[n++] = (char)c;
                }
                p += 9;
                sb.AppendLine(string.Format("Room {0,3}: {1}", room, new string(name, 0, n)));
                count++;
            }
            sb.AppendLine();
            sb.AppendLine(count + " named rooms.");
            return sb.ToString();
        }

        /// <summary>MAXS (v7): two 50-byte version strings then 15 uint16 engine maximums.</summary>
        private static string DecodeMaxs(byte[] b)
        {
            if (b.Length < 130) return HexAndStrings("MAXS", b);
            var sb = new StringBuilder("MAXS - engine maximums\n\n");
            sb.AppendLine("Engine version : " + AsciiZ(b, 0, 50));
            sb.AppendLine("Data version   : " + AsciiZ(b, 50, 50));
            sb.AppendLine();
            string[] labels =
            {
                "Variables", "Bit variables", "(unused)", "Global objects", "Local objects",
                "New names", "Verbs", "Floating objects", "Inventory", "Arrays",
                "Rooms", "Scripts", "Sounds", "Character sets", "Costumes",
            };
            int o = 100;
            for (int i = 0; i < labels.Length && o + 2 <= b.Length; i++, o += 2)
            {
                sb.AppendLine(string.Format("{0,-16}: {1}", labels[i], b[o] | (b[o + 1] << 8)));
            }
            return sb.ToString();
        }

        /// <summary>ANAM: [count:u16] then count x 9-byte audio resource names.</summary>
        private static string DecodeAudioNames(byte[] b)
        {
            if (b.Length < 2) return HexAndStrings("ANAM", b);
            int count = b[0] | (b[1] << 8);
            var sb = new StringBuilder("ANAM - audio resource names\n\n");
            sb.AppendLine(count + " entries:\n");
            int p = 2;
            for (int i = 0; i < count && p + 9 <= b.Length; i++, p += 9)
            {
                string name = AsciiZ(b, p, 9);
                if (name.Length > 0) sb.AppendLine(string.Format("{0,4}: {1}", i, name));
            }
            return sb.ToString();
        }

        /// <summary>
        /// DOBJ (v7): [count:u16] then THREE sequential tables (not per-object records), matching ScummVM
        /// ScummEngine_v7::readGlobalObjects: [state:count bytes][room:count bytes][class:count x uint32 LE].
        /// (The owner table is not stored in v7 - the engine defaults it to 0xFF.)
        /// </summary>
        private static string DecodeObjects(byte[] b)
        {
            if (b.Length < 2) return HexAndStrings("DOBJ", b);
            int count = b[0] | (b[1] << 8);
            int statesOff = 2;
            int roomsOff = statesOff + count;
            int classOff = roomsOff + count;
            if (classOff + count * 4 > b.Length) return HexAndStrings("DOBJ", b); // truncated - show raw

            var sb = new StringBuilder("DOBJ - global object state/room/class table\n\n");
            sb.AppendLine(count + " global objects.\n");
            sb.AppendLine("  obj  state  room   class");
            for (int i = 0; i < count; i++)
            {
                int co = classOff + i * 4;
                long cls = (uint)(b[co] | (b[co + 1] << 8) | (b[co + 2] << 16) | (b[co + 3] << 24));
                sb.AppendLine(string.Format("{0,5}  {1,5}  {2,4}   0x{3:X6}", i, b[statesOff + i], b[roomsOff + i], cls & 0xFFFFFF));
            }
            return sb.ToString();
        }

        /// <summary>
        /// AARY (predefined arrays), matching ScummVM ScummEngine_v6::readArrayFromIndexFile: repeated
        /// [array#:u16][dim2:u16][dim1:u16][type:u16], terminated by array#==0. The call is
        /// defineArray(array#, type, dim2, dim1), so the third word is the type.
        /// </summary>
        private static string DecodeArrays(byte[] b)
        {
            var sb = new StringBuilder("AARY - predefined arrays\n\n");
            int p = 0, count = 0;
            while (p + 2 <= b.Length)
            {
                int num = b[p] | (b[p + 1] << 8);
                if (num == 0) break;
                if (p + 8 > b.Length) break;
                int dim2 = b[p + 2] | (b[p + 3] << 8);
                int dim1 = b[p + 4] | (b[p + 5] << 8);
                int type = b[p + 6] | (b[p + 7] << 8);
                sb.AppendLine(string.Format("Array {0,4}: dim2={1} dim1={2} type={3}", num, dim2, dim1, type));
                p += 8;
                count++;
            }
            sb.AppendLine();
            sb.AppendLine(count + " arrays.");
            return sb.ToString();
        }

        private static string AsciiZ(byte[] b, int offset, int max)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < max && offset + i < b.Length; i++)
            {
                byte c = b[offset + i];
                if (c == 0) break;
                sb.Append(c >= 32 && c < 127 ? (char)c : '.');
            }
            return sb.ToString().Trim();
        }

        /// <summary>Fallback for any other raw index block: a hex dump plus the readable ASCII runs.</summary>
        private static string HexAndStrings(string tag, byte[] b)
        {
            var sb = new StringBuilder(tag + " - " + b.Length + " bytes\n\n");
            int rows = Math.Min(b.Length, 1024);
            for (int i = 0; i < rows; i += 16)
            {
                sb.Append(i.ToString("X4")).Append("  ");
                for (int j = 0; j < 16 && i + j < rows; j++) sb.Append(b[i + j].ToString("X2")).Append(' ');
                sb.Append(' ');
                for (int j = 0; j < 16 && i + j < rows; j++)
                {
                    byte c = b[i + j];
                    sb.Append(c >= 32 && c < 127 ? (char)c : '.');
                }
                sb.Append('\n');
            }
            if (b.Length > rows) sb.AppendLine("... (" + (b.Length - rows) + " more bytes)");
            return sb.ToString();
        }
    }
}
