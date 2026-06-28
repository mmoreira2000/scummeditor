using System.Collections.Generic;

namespace ScummEditor.Engine.Encoders
{
    /// <summary>
    /// Statically scans SCUMM v6/v7 script bytecode for LITERAL setCurrentPalette(roomN) calls - the
    /// roomOps opcode (0x9C) with sub-op SO_ROOM_NEW_PALETTE (213) immediately preceded by a literal
    /// pushByte/pushWord. These reveal which room's palette a cutscene loads at runtime, which is useful as
    /// a VIEW-ONLY candidate palette for codec-16 AKOS cels (they carry no palette of their own).
    ///
    /// Only LITERAL arguments are recovered: when the room number is pushed from a variable/array (the usual
    /// way a full palette is set) it cannot be known without running the script, so it is simply not found.
    /// This is a best-effort byte scan for a display aid - a stray match only adds an extra candidate the
    /// user can ignore (the caller further filters to rooms that actually exist and have a palette).
    /// </summary>
    public static class ScriptPaletteScanner
    {
        private const byte PushByte = 0x00;
        private const byte PushWord = 0x01;
        private const byte RoomOps = 0x9C;
        private const byte SoRoomNewPalette = 213; // SO_ROOM_NEW_PALETTE (ScummVM scumm_v6.h)

        /// <summary>Room numbers referenced by a literal setCurrentPalette(N) in this script's bytecode.</summary>
        public static IEnumerable<int> FindCurrentPaletteRooms(byte[] code, int startOffset)
        {
            var rooms = new List<int>();
            if (code == null)
            {
                return rooms;
            }

            for (int i = startOffset; i + 1 < code.Length; i++)
            {
                if (code[i] != RoomOps || code[i + 1] != SoRoomNewPalette)
                {
                    continue;
                }

                // The room number is whatever was pushed just before roomOps; recover it only when that push
                // is a literal. pushWord is checked first so a 0x01 0x00 .. word is not misread as a pushByte.
                if (i - 3 >= startOffset && code[i - 3] == PushWord)
                {
                    rooms.Add(code[i - 2] | (code[i - 1] << 8));
                }
                else if (i - 2 >= startOffset && code[i - 2] == PushByte)
                {
                    rooms.Add(code[i - 1]);
                }
            }
            return rooms;
        }
    }
}
