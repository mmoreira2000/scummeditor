using System.Collections.Generic;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;
using ScummEditor.Engine.Structures.IndexFile;

namespace ScummEditor.Engine.Encoders
{
    /// <summary>
    /// Builds the synthetic, view-only BlockBase tree for the v2 / v3-old (GF_OLD_BUNDLE) games so they
    /// render in the SAME block tree as v4-v6. Per NN.LFL file the layout mirrors the v4 LF container:
    ///
    ///   NN.LFL (file)
    ///     RO                       room container
    ///       HD                     room header / properties
    ///       BM                     background image
    ///       ZP                     walk-behind (z-plane), if present
    ///       OC x N                 one per object (id/name/size + verb code)
    ///         OI                   object image, if present
    ///         ZP                   object z-plane, if present (v3)
    ///       EN / EX                entry / exit scripts
    ///       LS x N                 local scripts (v3)
    ///     SC x N                   global scripts located in this file
    ///     CO x N                   costumes located in this file
    ///     SO x N                   sounds located in this file
    ///
    /// and the index becomes four directory blocks (0R/0S/0N/0C), like the v4 index. The blocks are pure
    /// position/ref overlays (see OldBundleBlock); the owning containers save bytes verbatim, so this
    /// cannot affect a save.
    /// </summary>
    public static class OldBundleBlockBuilder
    {
        /// <summary>Builds the room file's block children (RO + global scripts/costumes/sounds) into DataFile.Childrens.</summary>
        public static void BuildFileBlocks(ScummGameData game, ScummV3OldBundleDataFile dataFile, int roomNo)
        {
            GameInfo gameInfo = game.LoadedGameInfo;
            var index = game.IndexFile as ScummV3OldBundleIndexFile;
            OldBundleRoomModel model = OldBundleNavigator.BuildRoomModel(game, dataFile, roomNo);

            var fileChildren = new List<BlockBase>();

            // RO room container + its sub-resources.
            var ro = new OldBundleBlock(dataFile, gameInfo, "RO", OldBundleNodeKind.Room)
            { DataFile = dataFile, RoomNo = roomNo, IsV2 = model.IsV2 };

            ro.Childrens.Add(new OldBundleBlock(ro, gameInfo, "HD", OldBundleNodeKind.Header)
            { DataFile = dataFile, RoomNo = roomNo, IsV2 = model.IsV2 });

            if (model.HasBackground)
                ro.Childrens.Add(Image(ro, gameInfo, dataFile, roomNo, model.IsV2, "BM", OldBundleImageKind.Background, 0));
            if (model.HasBackgroundZPlane)
                ro.Childrens.Add(Image(ro, gameInfo, dataFile, roomNo, model.IsV2, "ZP", OldBundleImageKind.BackgroundZPlane, 0));

            foreach (OldBundleObjectInfo obj in model.Objects)
            {
                var oc = new OldBundleBlock(ro, gameInfo, "OC", OldBundleNodeKind.Object)
                { DataFile = dataFile, RoomNo = roomNo, IsV2 = model.IsV2, IsIndy3 = model.IsIndy3, ObjectInfo = obj, ObjectIndex = obj.Index };
                if (obj.HasImage)
                    oc.Childrens.Add(Image(oc, gameInfo, dataFile, roomNo, model.IsV2, "OI", OldBundleImageKind.Object, obj.Index));
                if (obj.HasZPlane)
                    oc.Childrens.Add(Image(oc, gameInfo, dataFile, roomNo, model.IsV2, "ZP", OldBundleImageKind.ObjectZPlane, obj.Index));
                ro.Childrens.Add(oc);
            }

            // Room scripts (entry / exit / local) sit under RO; global scripts are file-level (like v4 SC).
            foreach (OldBundleCodeRange s in model.Scripts)
            {
                string tag = RoomScriptTag(s.Kind);
                if (tag != null) ro.Childrens.Add(Script(ro, gameInfo, dataFile, model, s, tag));
            }
            fileChildren.Add(ro);

            foreach (OldBundleCodeRange s in model.Scripts)
                if (s.Kind == OldBundleCodeKind.GlobalScript)
                    fileChildren.Add(Script(dataFile, gameInfo, dataFile, model, s, "SC"));

            AddCostumes(fileChildren, game, dataFile, index, model.IsV2, gameInfo);
            AddSounds(fileChildren, dataFile, index, roomNo, gameInfo);

            dataFile.Childrens = fileChildren;
        }

        /// <summary>Builds the four index resource-directory blocks (0R/0S/0N/0C), like the v4 index directories.</summary>
        public static List<BlockBase> BuildIndexBlocks(ScummV3OldBundleIndexFile index, GameInfo gameInfo)
        {
            var blocks = new List<BlockBase>();
            blocks.Add(Directory(gameInfo, "0R", "Room Directory", index.RoomDirectory));
            blocks.Add(Directory(gameInfo, "0S", "Script Directory", index.ScriptDirectory));
            blocks.Add(Directory(gameInfo, "0N", "Sound Directory", index.SoundDirectory));
            blocks.Add(Directory(gameInfo, "0C", "Costume Directory", index.CostumeDirectory));
            return blocks;
        }

        private static string RoomScriptTag(OldBundleCodeKind kind)
        {
            switch (kind)
            {
                case OldBundleCodeKind.EntryScript: return "EN";
                case OldBundleCodeKind.ExitScript: return "EX";
                case OldBundleCodeKind.LocalScript: return "LS";
                default: return null; // global scripts are added at the file level
            }
        }

        private static OldBundleBlock Image(BlockBase parent, GameInfo gameInfo, ScummV3OldBundleDataFile dataFile,
            int roomNo, bool isV2, string tag, OldBundleImageKind kind, int objectIndex)
        {
            return new OldBundleBlock(parent, gameInfo, tag, OldBundleNodeKind.Image)
            { DataFile = dataFile, RoomNo = roomNo, IsV2 = isV2, ImageKind = kind, ObjectIndex = objectIndex };
        }

        private static OldBundleBlock Script(BlockBase parent, GameInfo gameInfo, ScummV3OldBundleDataFile dataFile,
            OldBundleRoomModel model, OldBundleCodeRange range, string tag)
        {
            return new OldBundleBlock(parent, gameInfo, tag, OldBundleNodeKind.Script)
            {
                DataFile = dataFile,
                IsV2 = model.IsV2,
                IsIndy3 = model.IsIndy3,
                Start = range.Start,
                End = range.End,
                ScriptId = range.Number,
                Title = range.Label
            };
        }

        private static OldBundleBlock Directory(GameInfo gameInfo, string tag, string title, V3OldResourceDirectory dir)
        {
            return new OldBundleBlock(null, gameInfo, tag, OldBundleNodeKind.Directory) { Title = title, Directory = dir };
        }

        private static void AddCostumes(List<BlockBase> fileChildren, ScummGameData game, ScummV3OldBundleDataFile dataFile,
            ScummV3OldBundleIndexFile index, bool isV2, GameInfo gameInfo)
        {
            V3OldResourceDirectory dir = index == null ? null : index.CostumeDirectory;
            if (dir == null) return;
            Dictionary<int, ScummV3OldBundleDataFile> byRoom = MapRoomsByNumber(game);

            for (int c = 0; c < dir.Count; c++)
            {
                // Match the costume to the file that actually holds its bytes by data-file identity.
                ScummV3OldBundleDataFile costumeFile;
                if (!byRoom.TryGetValue(dir.RoomNumbers[c], out costumeFile) || !ReferenceEquals(costumeFile, dataFile)) continue;
                int offset = dir.Offsets[c];
                if (offset == 0xFFFF || offset == 0) continue;

                int frameCount;
                try { frameCount = new CostumeV3Old(dataFile.RawContent, offset).Frames.Count; }
                catch { continue; }
                if (frameCount == 0) continue;

                fileChildren.Add(new OldBundleBlock(dataFile, gameInfo, "CO", OldBundleNodeKind.Costume)
                { DataFile = dataFile, IsV2 = isV2, Offset = offset, ResourceIndex = c });
            }
        }

        private static void AddSounds(List<BlockBase> fileChildren, ScummV3OldBundleDataFile dataFile,
            ScummV3OldBundleIndexFile index, int roomNo, GameInfo gameInfo)
        {
            V3OldResourceDirectory dir = index == null ? null : index.SoundDirectory;
            if (dir == null) return;

            for (int s = 0; s < dir.Count; s++)
            {
                if (dir.RoomNumbers[s] != roomNo) continue;
                int offset = dir.Offsets[s];
                if (offset == 0xFFFF || offset == 0 || dataFile.RawContent == null || offset >= dataFile.RawContent.Length) continue;

                var sound = new ScummV3OldSound(dataFile.RawContent, offset);
                if (sound.AdLibOffset < 0) continue; // nothing playable/exportable

                fileChildren.Add(new OldBundleBlock(dataFile, gameInfo, "SO", OldBundleNodeKind.Sound)
                { DataFile = dataFile, RoomNo = roomNo, Offset = offset, ResourceIndex = s });
            }
        }

        private static Dictionary<int, ScummV3OldBundleDataFile> MapRoomsByNumber(ScummGameData game)
        {
            var byRoom = new Dictionary<int, ScummV3OldBundleDataFile>();
            if (game.DataDisks == null) return byRoom;
            foreach (DataDisk disk in game.DataDisks)
            {
                var df = disk.Tree as ScummV3OldBundleDataFile;
                if (df == null) continue;
                int n;
                if (int.TryParse(System.IO.Path.GetFileNameWithoutExtension(disk.FilePath), out n)) byRoom[n] = df;
            }
            return byRoom;
        }
    }
}
