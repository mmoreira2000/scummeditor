using ScummEditor.Engine.Encoders;
using Xunit;

namespace ScummEditor.UnitTests
{
    /// <summary>
    /// Regression guard for the v2 language-detection path. The detector must extract v2 text with the
    /// PLAIN codec (GameTextCodecV12.Default), never a language-specific accent map: a remap turns
    /// punctuation slots into accented LETTERS, which char.IsLetter then merges into adjacent words and
    /// biases the word-frequency heuristic toward that very language. This pins the byte->char semantics
    /// so a future change cannot silently re-introduce the bias (see GameLanguageDetector.Detect).
    /// </summary>
    public class ScummLanguageDetectorV2Tests
    {
        // The exact slots GameTextCodecV12.Portuguese() repurposes (~ | \ [ ] _ < > = * / % ").
        private static readonly byte[] AccentSlotBytes =
            { 0x7E, 0x7C, 0x5C, 0x5B, 0x5D, 0x5F, 0x3C, 0x3E, 0x3D, 0x2A, 0x2F, 0x25, 0x22 };

        [Fact]
        public void DefaultCodecDecodesAccentSlotsAsPunctuation_NotLetters()
        {
            string decoded = GameTextCodecV12.Default().Decode(AccentSlotBytes, 0, AccentSlotBytes.Length);

            int letters = CountLetters(decoded);
            Assert.Equal(0, letters); // every slot stays a non-letter symbol -> a word boundary, no bias
        }

        [Fact]
        public void PortugueseCodecDecodesTheSameSlotsAsAccentedLetters()
        {
            // Proves the slots WOULD become letters under the language map - the bias the detector avoids.
            string decoded = GameTextCodecV12.Portuguese().Decode(AccentSlotBytes, 0, AccentSlotBytes.Length);

            int letters = CountLetters(decoded);
            Assert.Equal(AccentSlotBytes.Length, letters);
        }

        private static int CountLetters(string text)
        {
            int count = 0;
            foreach (char c in text)
                if (char.IsLetter(c)) count++;
            return count;
        }
    }
}
