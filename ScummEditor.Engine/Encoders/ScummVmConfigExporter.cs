using System.IO;
using System.Text;
using ScummEditor.Engine.Structures;

namespace ScummEditor.Engine.Encoders
{
    /// <summary>
    /// Generates a ScummVM configuration "target" (a section of scummvm.ini) for a loaded, possibly
    /// edited, game so it launches with the correct engine and graphics variant.
    ///
    /// Why this is needed: editing a SCUMM game rewrites its index file, so the detection MD5 ScummVM
    /// keeps in its database no longer matches. ScummVM then falls back to heuristic detection, which
    /// for the older floppy games cannot always tell the variants apart and auto-starts the wrong one
    /// (e.g. a modified Monkey Island 1 floppy is detected as Loom, or a modified Loom FM-Towns as the
    /// PC-Engine version). Launching from an explicit target with the right gameid - and a platform only
    /// where it actually disambiguates a variant - makes ScummVM load the edited game correctly.
    /// </summary>
    public static class ScummVmConfigExporter
    {
        /// <summary>The ScummVM engine id - always "scumm" for the games this editor handles.</summary>
        public const string EngineId = "scumm";

        /// <summary>Maps a detected game to its ScummVM gameid, or null when unknown.</summary>
        public static string ResolveGameId(ScummGame game)
        {
            switch (game)
            {
                case ScummGame.IndianaJones3: return "indy3";
                case ScummGame.Loom: return "loom";
                case ScummGame.ZakMcKracken: return "zak";
                case ScummGame.MonkeyIsland1Floppy:
                case ScummGame.MonkeyIsland1VGA:
                case ScummGame.MonkeyIsland1VGASpeech: return "monkey";
                case ScummGame.MonkeyIsland2: return "monkey2";
                case ScummGame.FateOfAtlantis: return "atlantis";
                case ScummGame.DayOfTheTentacle: return "tentacle";
                case ScummGame.SamAndMax: return "samnmax";
                default: return null;
            }
        }

        /// <summary>
        /// The platform to pin in the target, or null to leave it out. ScummVM only needs this where a
        /// gameid has variants its fallback cannot distinguish from the edited files:
        ///  - FM-Towns releases (the v3 Indy3 / Zak / Loom that ship ripped CD audio): "fmtowns",
        ///    otherwise ScummVM picks the PC-Engine variant of Loom and renders garbage.
        ///  - Monkey Island 1 FLOPPY EGA: "pc" (DOS) so ScummVM keeps the EGA variant; without it the
        ///    VGA variant (which is listed first and is platform-agnostic) wins and the EGA data decodes
        ///    wrong.
        ///  - Monkey Island 1 FLOPPY VGA: NO platform - the VGA variant is first and platform-agnostic,
        ///    so adding platform=pc would filter it out and leave EGA.
        /// Every other game/variant is identified uniquely by its files, so no platform is needed.
        /// </summary>
        public static string ResolvePlatform(GameInfo info)
        {
            if (info.ScummVersion == 3 && info.HasCdAudio)
            {
                return "fmtowns";
            }
            if (info.LoadedGame == ScummGame.MonkeyIsland1Floppy && info.Edition == GameEdition.FloppyEga)
            {
                return "pc";
            }
            return null;
        }

        /// <summary>A unique-ish, file-name-safe target id, e.g. "loom-fmtowns-edited".</summary>
        public static string BuildTargetName(GameInfo info)
        {
            string id = ResolveGameId(info.LoadedGame) ?? "scumm-game";
            string suffix = VariantSuffix(info);
            return (suffix == null ? id : id + "-" + suffix) + "-edited";
        }

        private static string VariantSuffix(GameInfo info)
        {
            if (info.ScummVersion == 3 && info.HasCdAudio) return "fmtowns";
            if (info.Edition == GameEdition.FloppyEga) return "ega";
            if (info.Edition == GameEdition.FloppyVga) return "vga";
            if (info.IsTalkie) return "talkie";
            if (info.HasCdAudio) return "cd";
            return null;
        }

        /// <summary>A human-readable game name for the target description.</summary>
        public static string GameDisplayName(ScummGame game)
        {
            switch (game)
            {
                case ScummGame.IndianaJones3: return "Indiana Jones and the Last Crusade";
                case ScummGame.Loom: return "Loom";
                case ScummGame.ZakMcKracken: return "Zak McKracken and the Alien Mindbenders";
                case ScummGame.MonkeyIsland1Floppy:
                case ScummGame.MonkeyIsland1VGA:
                case ScummGame.MonkeyIsland1VGASpeech: return "The Secret of Monkey Island";
                case ScummGame.MonkeyIsland2: return "Monkey Island 2: LeChuck's Revenge";
                case ScummGame.FateOfAtlantis: return "Indiana Jones and the Fate of Atlantis";
                case ScummGame.DayOfTheTentacle: return "Day of the Tentacle";
                case ScummGame.SamAndMax: return "Sam & Max Hit the Road";
                default: return "SCUMM game";
            }
        }

        /// <summary>
        /// Builds the full text of a small scummvm.ini holding a [scummvm] header and one target for the
        /// edited game, plus comments explaining how to use it and why the platform line is (or is not)
        /// present.
        /// </summary>
        public static string GenerateIni(GameInfo info, string gameFolder)
        {
            string target = BuildTargetName(info);
            string gameId = ResolveGameId(info.LoadedGame);
            string platform = ResolvePlatform(info);
            string name = GameDisplayName(info.LoadedGame) + " (edited)";

            // ScummVM's scummvm.ini parser (ConfigManager) only treats lines starting with '#' as
            // comments - a ';' comment is parsed as a key/value pair and rejects the whole file. So all
            // comment lines here MUST start with '#'.
            var sb = new StringBuilder();
            sb.AppendLine("# ScummVM launch profile generated by ScummEditor.");
            sb.AppendLine("# This game was modified, so its index checksum no longer matches ScummVM's database and");
            sb.AppendLine("# auto-detection may pick the wrong game or graphics variant. Use this profile to force");
            sb.AppendLine("# the correct one:");
            sb.AppendLine("#     scummvm --config=\"" + Path.GetFileName(SafeIniFileName(info)) + "\" " + target);
            sb.AppendLine("# or copy the [" + target + "] section below into your existing scummvm.ini.");
            sb.AppendLine("#");
            sb.AppendLine("# Tips: if there is no music or an audio-device error, set the Music device to \"AdLib");
            sb.AppendLine("# Emulator\" in ScummVM. On FM-Towns games, just click OK if an aspect-ratio dialog appears.");
            sb.AppendLine();
            sb.AppendLine("[scummvm]");
            sb.AppendLine();
            sb.AppendLine("[" + target + "]");
            sb.AppendLine("engineid=" + EngineId);
            sb.AppendLine("gameid=" + gameId);
            if (platform != null)
            {
                sb.AppendLine("# platform pins the variant ScummVM's fallback cannot otherwise pick for an edited game.");
                sb.AppendLine("platform=" + platform);
            }
            else if (info.LoadedGame == ScummGame.MonkeyIsland1Floppy && info.Edition == GameEdition.FloppyVga)
            {
                sb.AppendLine("# NOTE: do NOT add a 'platform' line here - it would make ScummVM load the EGA variant.");
            }
            sb.AppendLine("description=" + name);
            sb.AppendLine("path=" + (gameFolder ?? string.Empty));
            return sb.ToString();
        }

        /// <summary>A suggested file name for the exported profile, e.g. "loom-fmtowns-edited.ini".</summary>
        public static string SafeIniFileName(GameInfo info)
        {
            return BuildTargetName(info) + ".ini";
        }

        /// <summary>Writes the launch profile to <paramref name="outIniPath"/>.</summary>
        public static void Export(GameInfo info, string gameFolder, string outIniPath)
        {
            File.WriteAllText(outIniPath, GenerateIni(info, gameFolder), new UTF8Encoding(false));
        }
    }
}
