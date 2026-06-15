using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ScummEditor.Gui
{
    /// <summary>
    /// Plays a Standard MIDI File through the Windows MCI sequencer (the same mechanism the v5/v6
    /// sound viewer uses). Writes the bytes to a temp file, opens it under a private alias and plays;
    /// Stop closes the alias. One instance plays one thing at a time.
    /// </summary>
    public class MidiMciPlayer
    {
        [DllImport("winmm.dll", CharSet = CharSet.Auto)]
        private static extern int mciSendString(string command, StringBuilder returnValue, int returnLength, IntPtr callback);

        [DllImport("winmm.dll", CharSet = CharSet.Auto)]
        private static extern bool mciGetErrorString(int errorCode, StringBuilder errorText, int errorLength);

        private readonly string _alias;
        private readonly string _tempPath;
        private bool _open;

        public MidiMciPlayer(string aliasSuffix)
        {
            _alias = "scummEditorMidi_" + aliasSuffix;
            _tempPath = Path.Combine(Path.GetTempPath(), "scummeditor_" + aliasSuffix + ".mid");
        }

        /// <summary>Plays the MIDI bytes. Returns true on success; otherwise sets <paramref name="error"/>.</summary>
        public bool Play(byte[] midi, out string error)
        {
            error = null;
            Stop();

            if (midi == null || midi.Length == 0)
            {
                error = "No MIDI data.";
                return false;
            }

            try
            {
                File.WriteAllBytes(_tempPath, midi);
            }
            catch (IOException ex)
            {
                error = ex.Message;
                return false;
            }

            int openResult = mciSendString("open \"" + _tempPath + "\" type sequencer alias " + _alias, null, 0, IntPtr.Zero);
            if (openResult != 0)
            {
                error = MciErrorText(openResult);
                return false;
            }
            _open = true;

            int playResult = mciSendString("play " + _alias, null, 0, IntPtr.Zero);
            if (playResult != 0)
            {
                Stop();
                error = MciErrorText(playResult);
                return false;
            }

            return true;
        }

        public void Stop()
        {
            if (!_open) return;
            mciSendString("stop " + _alias, null, 0, IntPtr.Zero);
            mciSendString("close " + _alias, null, 0, IntPtr.Zero);
            _open = false;
        }

        private static string MciErrorText(int errorCode)
        {
            var text = new StringBuilder(256);
            mciGetErrorString(errorCode, text, text.Capacity);
            return "(" + errorCode + ") " + text;
        }
    }
}
