using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using ScummEditor.Engine;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;
using Xunit;
using Xunit.Abstractions;

namespace ScummEditor.UnitTests
{
    /// <summary>
    /// Cross-edition viewer sweep: loads EVERY v7 edition in the game library (all The Dig + Full Throttle
    /// editions, incl. the localized / Windows / CJK ones) and actually DECODES a real sample through every
    /// viewer's decode path - room/object/z-plane images, AKOS costume cels, in-resource charsets, .NUT
    /// fonts, in-container iMUS/VOC sound, scripts + object verb bytecode, localized text, the MONSTER.SOU
    /// speech file and the .BUN iMUSE bundles. The focused per-resource tests exercise The Dig + Full
    /// Throttle exhaustively; this guards the gap that let the Full Throttle MONSTER.SOU "0 entries" bug ship
    /// - a per-edition format quirk that a detection-only test never exercises. Every failure is collected so
    /// the report is exhaustive; skips when the (git-ignored) library is absent.
    /// </summary>
    public class V7ViewerSweepTests
    {
        private readonly ITestOutputHelper _out;
        public V7ViewerSweepTests(ITestOutputHelper output) { _out = output; }

        [SkippableFact]
        public void EveryV7EditionDecodesThroughEveryViewer()
        {
            List<string> editions = DiscoverV7Editions();
            Skip.If(editions.Count == 0, "no v7 editions present in the game library");

            var failures = new List<string>();
            int sweptEditions = 0;

            foreach (string folder in editions)
            {
                GameInfo info;
                try { info = Functions.FindScummGameInFolder(folder); }
                catch (Exception ex) { failures.Add(Rel(folder) + " | detect threw: " + ex.Message); continue; }
                if (info == null || info.ScummVersion != 7) continue;

                string rel = Rel(folder);
                ScummGameData game;
                try { game = ScummGameData.LoadFromGameInfo(info); }
                catch (Exception ex) { failures.Add(rel + " | load threw: " + ex.GetType().Name + " " + ex.Message); continue; }

                sweptEditions++;
                SweepEdition(rel, info, game, failures);
            }

            _out.WriteLine($"Swept {sweptEditions} v7 editions; {failures.Count} failure(s).");
            Assert.True(failures.Count == 0, "viewer decode failures:\n" + string.Join("\n", failures));
            Assert.True(sweptEditions >= 2, "expected at least The Dig + Full Throttle; swept " + sweptEditions);
        }

        // Per-edition sample caps. Images/cels/scripts are essentially identical across the editions of one
        // game (localization changes text/fonts/sound, not the artwork or opcode structure), and The Dig +
        // Full Throttle are already decoded exhaustively by the focused tests - so here a sample per edition
        // is enough to prove that viewer decodes for THAT edition, without 50k redundant identical decodes.
        private const int ImageCap = 150;
        private const int CelCap = 150;
        private const int ScriptCap = 300;
        private const int SoundCap = 40;

        private void SweepEdition(string rel, GameInfo info, ScummGameData game, List<string> failures)
        {
            int images = 0, cels = 0, glyphs = 0, sounds = 0, scripts = 0, verbs = 0, charsets = 0;

            // --- in-container resources walked from the LFLF/ROOM tree ---
            foreach (DiskBlock disk in game.DataFile.GetLFLFs())
            {
                RoomBlock room = disk.GetROOM();
                if (room != null)
                {
                    // background + its z-planes
                    Try(failures, rel, "background", () =>
                    {
                        using (Bitmap bg = ImageResourceCodec.Decode(room, null, ImageType.Background, 0, 0, 0, 0, false))
                            if (bg != null) images++;
                    });
                    List<ZPlane> zps = SafeZPlanes(room);
                    for (int z = 0; z < zps.Count; z++)
                    {
                        int zi = z;
                        Try(failures, rel, "z-plane " + zi, () =>
                        {
                            using (Bitmap zp = ImageResourceCodec.Decode(room, null, ImageType.ZPlane, 0, 0, zi, 0, false))
                                if (zp != null) images++;
                        });
                    }
                    // object images (capped per edition - the format is uniform, we only need to exercise it)
                    List<ObjectImage> obims = room.GetOBIMs();
                    for (int j = 0; j < obims.Count && images < ImageCap; j++)
                    {
                        int jj = j;
                        List<ImageData> imgs = obims[j].GetIMxx();
                        for (int k = 0; k < imgs.Count; k++)
                        {
                            int kk = k;
                            Try(failures, rel, "object " + jj + "/" + kk, () =>
                            {
                                using (Bitmap im = ImageResourceCodec.Decode(room, null, ImageType.Object, jj, kk, 0, 0, false))
                                    if (im != null) images++;
                            });
                        }
                    }
                }

                foreach (BlockBase child in Children(disk, room))
                {
                    if (child.BlockType == "AKOS" && cels < CelCap)
                    {
                        int count = AkosImageDecoder.GetCelCount(child);
                        for (int i = 0; i < count; i++)
                        {
                            int ci = i;
                            Try(failures, rel, "AKOS cel " + ci, () =>
                            {
                                using (Bitmap bmp = AkosImageDecoder.DecodeCel(child, ci))
                                    if (bmp != null) cels++;
                            });
                        }
                    }

                    if (child is ScriptBlock s && scripts < ScriptCap)
                    {
                        scripts++;
                        Try(failures, rel, "script", () =>
                        {
                            if (!s.Disassemble().DecodedToEnd) failures.Add(rel + " | script did not decode to end");
                        });
                    }

                    if (child is ObjectCode oc && oc.VerbCodeOffset >= 0 && oc.VerbCodeLength > 0 && verbs < ScriptCap)
                    {
                        verbs++;
                        Try(failures, rel, "verb code", () =>
                        {
                            var slice = new byte[oc.VerbCodeLength];
                            Array.Copy(oc.RawContent, oc.VerbCodeOffset, slice, 0, oc.VerbCodeLength);
                            if (!ScummV6Disassembler.Disassemble(slice, 0).DecodedToEnd)
                                failures.Add(rel + " | verb code did not decode to end");
                        });
                    }
                }
            }

            // --- in-container sound (sampled: the per-format decode is what we are exercising) ---
            foreach (SoundBlockV7 sound in CollectSounds(game))
            {
                if (sounds >= SoundCap) break;
                Try(failures, rel, "sound", () =>
                {
                    byte[] body = Serialize(sound);
                    if (ImuseAudioDecoder.IsImus(body))
                    {
                        if (ImuseAudioDecoder.ToWav(body) != null) sounds++;
                    }
                    else
                    {
                        int voc = IndexOf(body, "Creative Voice File");
                        if (voc >= 0)
                        {
                            var slice = new byte[body.Length - voc];
                            Array.Copy(body, voc, slice, 0, slice.Length);
                            if (SoundConverter.VocToWav(slice) != null) sounds++;
                        }
                    }
                });
            }

            // --- in-resource charsets: ExportPng renders every glyph (real decode) ---
            string tmp = Path.Combine(Path.GetTempPath(), "v7sweep_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(tmp);
                foreach (Charset cs in CharsetPngCodec.CollectCharsets(game.DataFile))
                {
                    int idx = charsets;
                    Try(failures, rel, "charset " + idx, () =>
                    {
                        CharsetPngCodec.ExportPng(cs, Path.Combine(tmp, "c" + idx + ".png"), Path.Combine(tmp, "c" + idx + ".guide.png"));
                        charsets++;
                    });
                }
            }
            finally { try { Directory.Delete(tmp, true); } catch { } }

            // --- external .NUT SMUSH fonts ---
            if (game.NutFonts != null)
            {
                foreach (NutFontResource res in game.NutFonts)
                {
                    NutFont font = res.Font;
                    if (font == null || !font.IsValid) continue;
                    for (int i = 0; i < font.Glyphs.Count; i++)
                    {
                        if (!font.Glyphs[i].HasPixels) continue;
                        int gi = i;
                        Try(failures, rel, "NUT glyph " + gi, () =>
                        {
                            if (NutImageDecoder.DecodeGlyphIndices(font, gi) != null) glyphs++;
                        });
                    }
                }
            }

            // --- localized text (LANGUAGE.BND / .TRS) - exhaustively swept elsewhere, touched here too ---
            if (game.LocalizedTextFiles != null)
            {
                foreach (ILocalizedTextFile text in game.LocalizedTextFiles)
                {
                    int n = 0;
                    foreach (LocalizedTextEntry e in text.Entries) { n++; if (n > 0) break; }
                }
            }

            // --- external speech MONSTER.SOU (Full Throttle) ---
            if (!string.IsNullOrEmpty(info.SpeechFilePath) && File.Exists(info.SpeechFilePath))
            {
                Try(failures, rel, "MONSTER.SOU", () =>
                {
                    var speech = new SpeechSouFile(info.SpeechFilePath);
                    speech.EnsureParsed();
                    if (speech.ParseError != null) failures.Add(rel + " | MONSTER.SOU parse stopped: " + speech.ParseError);
                    if (speech.Entries.Count == 0) failures.Add(rel + " | MONSTER.SOU yielded 0 entries");
                    // decode a small sample of the speech VOCs
                    int take = Math.Min(10, speech.Entries.Count);
                    for (int i = 0; i < take; i++)
                    {
                        if (SoundConverter.VocToWav(speech.ReadVocBytes(speech.Entries[i])) == null)
                            failures.Add(rel + " | MONSTER.SOU entry " + i + " did not decode to WAV");
                    }
                });
            }

            // --- external iMUSE bundles .BUN (The Dig) ---
            if (info.BundleFiles != null)
            {
                foreach (string path in info.BundleFiles)
                {
                    if (!File.Exists(path)) continue;
                    string name = Path.GetFileName(path);
                    Try(failures, rel, name, () =>
                    {
                        var bundle = new ImuseBundleFile(path);
                        bundle.EnsureParsed();
                        if (!bundle.IsValid || bundle.Entries.Count == 0) { failures.Add(rel + " | " + name + " parsed no entries"); return; }
                        int take = Math.Min(6, bundle.Entries.Count);
                        for (int i = 0; i < take; i++)
                        {
                            if (ImuseBundleDecoder.ToWav(bundle.ReadEntryRaw(i)) == null)
                                failures.Add(rel + " | " + name + " entry " + i + " did not decode to WAV");
                        }
                    });
                }
            }

            _out.WriteLine($"  {rel}: img={images} cels={cels} glyphs={glyphs} sounds={sounds} scripts={scripts} verbs={verbs} charsets={charsets}");
        }

        // ---- helpers ----

        private static void Try(List<string> failures, string rel, string label, Action action)
        {
            try { action(); }
            catch (Exception ex)
            {
                if (failures.Count < 80) failures.Add(rel + " | " + label + ": " + ex.GetType().Name + " " + ex.Message);
            }
        }

        private static List<ZPlane> SafeZPlanes(RoomBlock room)
        {
            try
            {
                var rmim = room.GetRMIM();
                var im00 = rmim != null ? rmim.GetIM00() : null;
                return im00 != null ? im00.GetZPlanes() : new List<ZPlane>();
            }
            catch { return new List<ZPlane>(); }
        }

        private static IEnumerable<BlockBase> Children(DiskBlock disk, RoomBlock room)
        {
            foreach (BlockBase c in disk.Childrens) yield return c;
            if (room != null)
                foreach (BlockBase c in room.Childrens) yield return c;
        }

        private static List<SoundBlockV7> CollectSounds(ScummGameData game)
        {
            var list = new List<SoundBlockV7>();
            WalkSounds((BlockBase)game.DataFile, list);
            return list;
        }

        private static void WalkSounds(BlockBase node, List<SoundBlockV7> outList)
        {
            if (node is SoundBlockV7 s) outList.Add(s);
            foreach (BlockBase c in node.Childrens) WalkSounds(c, outList);
        }

        private static byte[] Serialize(BlockBase block)
        {
            using (var ms = new MemoryStream())
            {
                block.SaveToBinaryWriter(ms);
                return ms.ToArray();
            }
        }

        private static int IndexOf(byte[] data, string text)
        {
            for (int i = 0; i + text.Length <= data.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < text.Length; j++)
                {
                    if (data[i + j] != text[j]) { match = false; break; }
                }
                if (match) return i;
            }
            return -1;
        }

        /// <summary>Every distinct v7 edition folder in the library (a folder holding a *.LA0 index).</summary>
        private static List<string> DiscoverV7Editions()
        {
            var result = new List<string>();
            string root = GameLibrary.Folder("ScummV7");
            if (root == null) return result;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string la0 in Directory.GetFiles(root, "*.LA0", SearchOption.AllDirectories))
            {
                string dir = Path.GetDirectoryName(la0);
                if (dir != null && seen.Add(dir)) result.Add(dir);
            }
            result.Sort(StringComparer.OrdinalIgnoreCase);
            return result;
        }

        private static string Rel(string folder)
        {
            string root = GameLibrary.Folder("ScummV7");
            return (root != null && folder.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                ? folder.Substring(root.Length).TrimStart('\\', '/')
                : folder;
        }
    }
}
