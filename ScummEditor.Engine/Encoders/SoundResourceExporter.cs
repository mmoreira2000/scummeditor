namespace ScummEditor.Encoders
{
    /// <summary>
    /// Chooses the most useful exported representation of a v5/v6 sound resource and produces its
    /// bytes: the extracted Standard MIDI (.mid), the decoded VOC waveform (.wav, or the raw .voc
    /// when the codec is unsupported), or the raw bytes (.bin) for AdLib/Roland FM data. Pure
    /// engine - the GUI just writes the returned bytes to the chosen file.
    /// </summary>
    public static class SoundResourceExporter
    {
        public static void GetExportBytes(byte[] data, out byte[] outBytes, out string extension)
        {
            switch (SoundConverter.Classify(data))
            {
                case SoundConverter.SoundKind.StandardMidi:
                    outBytes = SoundConverter.ExtractMidi(data) ?? data;
                    extension = ".mid";
                    return;

                case SoundConverter.SoundKind.Voc:
                    byte[] wav = SoundConverter.VocToWav(data);
                    if (wav != null) { outBytes = wav; extension = ".wav"; }
                    else { outBytes = data; extension = ".voc"; }
                    return;

                default:
                    outBytes = data;
                    extension = ".bin";
                    return;
            }
        }
    }
}
