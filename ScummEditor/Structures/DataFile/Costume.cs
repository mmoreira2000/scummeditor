using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ScummEditor.Exceptions;

namespace ScummEditor.Structures.DataFile
{

    /*
     Costumes store the animations used for characters in the game. 
     The format is relatively complex. It basically consists of a bunch of frames and commands to make use of them :) 
     I had a hard time really figuring out how this works.

      size               : 32le can be 0 (see num anim) or the size (sometimes with an offset of one ??)
      header             : 2*8 always contain "CO"
      num anim           : 8  if(size) num_anim++
      format             : 8  bit 7 set means that west anims must NOT be mirrored, bit 0 is the palette size (0: 16 colors, 1: 32 colors)
      palette            : 8*num colors coded in format
      anim cmds offset   : 16le access the anim cmds array
      limbs offset       : 16*16le access limb picture table
      anim offsets       : 16le*num anim  access anim definitions
      anim
        limb mask        : 16le
        anim definitions : variable length, one definition for each bit set
                           to 1 in the limb mask.
            0xFFFF       : 16le disabled limb code
         OR
            start        : 16le
            noloop       : 1
            end offset   : 7 offset of the last frame, or len-1
      anim cmds
        cmd              : 8
      limbs
        pict  offset     : 16le
      picts
        width            : 16le
        height           : 16le
        rel_x            : s16le
        rel_y            : s16le
        move_x           : s16le
        move_y           : s16le
        redir_limb       : 8 only present if((format & 0x7E) == 0x60)
        redir_pict       : 8 only present if((format & 0x7E) == 0x60)
        rle data
     */
    public class Costume : BlockBase
    {
        public Costume(BlockBase blockBase) : base(blockBase) { }

        public override string BlockType
        {
            get { return "COST"; }
        }
        public uint Size { get; set; }
        public string Header { get; set; }
        public byte NumAnim { get; set; }
        public byte Format { get; set; }
        public List<byte> Palette { get; set; }
        public ushort AnimCommandsOffset { get; set; }
        public List<ushort> LimbsOffsets { get; set; }
        public List<ushort> AnimOffsets { get; set; }
        public List<Animation> Animations { get; set; }
        public List<byte> Commands { get; set; }
        public List<Limb> Limbs { get; set; }
        public List<CostumeImageData> Pictures { get; set; }

        public byte CloseByte { get; set; } //for some reason costumes seem to end with a close byte !?!?
        public bool HasCloseByte { get; set; }
        public int PaletteSize { get; private set; }

        public override void CalculateBlockSize()
        {
            base.CalculateBlockSize();
            uint block = 0;

            if (_gameInfo.ScummVersion == 6)
            {
                block += 4; //Size
                block += 2; //Header
            }
            block += 1; //NumAnim
            block += 1; //Format
            block += (uint)Palette.Count(); //1 for each palette entry
            block += 2; //AnimCommandsOffset
            block += (uint)LimbsOffsets.Count * 2; //2 bytes fo each limb offset
            block += (uint)AnimOffsets.Count * 2; //2 bytes fo each animation offset

            foreach (Animation animation in Animations)
            {
                block += animation.GetSize(); //Each animation has a diffetent size, so the class calculate this own size, we only get the value here.
            }

            block += (uint)Commands.Count; //1 byte for each command.

            foreach (Limb limb in Limbs)
            {
                block += (ushort)(limb.ImageOffsets.Count * 2); //2 bytes for each imageoffset the limb has.
            }
            foreach (CostumeImageData picture in Pictures)
            {
                block += picture.GetSize(); //Each picture has a different size, so the class calculate his own size, we only get the value here.
            }

            if (Size > 0)
            {
                //if (Size != block) Debugger.Break();
                Size = block;
            }
            if (HasCloseByte)
            {
                block += 1; //Some costumes ends with a 0x00 byte, i don´t know why, but on this cases, we need to add this extra byte.
            }

            BlockSize += block;
        }

        public override void CalculateOffsets()
        {
            base.CalculateOffsets();

            //Lets calculate the new offset for each image, and then we change the information on limbs.
            if (Pictures.Count == 0)
            {
                Debugger.Break();
                return;
            }

            //The offset of first picture never changes (for now, because we do not change the size of other parameters, only the images data.)
            var nextOffset = (ushort)(Pictures[0].ImageStartOffSet + Pictures[0].GetSize());
            for (int i = 1; i < Pictures.Count; i++)
            {
                ushort currentOffset = Pictures[i].ImageStartOffSet;

                //Search on all limbs for images that match the old offset position and updates to the new position.
                foreach (Limb limb in Limbs)
                {
                    for (int j = 0; j < limb.ImageOffsets.Count; j++)
                    {
                        if (limb.ImageOffsets[j] == currentOffset)
                        {
                            limb.ImageOffsets[j] = nextOffset;
                        }
                    }

                }
                nextOffset += Pictures[i].GetSize();
            }
        }

        public override void SaveToBinaryWriter(Stream binaryWriter)
        {
            base.SaveToBinaryWriter(binaryWriter);

            if (_gameInfo.ScummVersion == 6)
            {
                binaryWriter.Write(Size);
                binaryWriter.Write(BinaryHelper.ConvertUTF8StringToByteArray(Header));
            }

            binaryWriter.Write(NumAnim);
            binaryWriter.Write(Format);

            foreach (byte b in Palette)
            {
                binaryWriter.Write(b);
            }

            binaryWriter.Write(AnimCommandsOffset);

            foreach (ushort limbsOffset in LimbsOffsets)
            {
                binaryWriter.Write(limbsOffset);
            }

            foreach (ushort animOffset in AnimOffsets)
            {
                binaryWriter.Write(animOffset);
            }

            foreach (Animation animation in Animations)
            {
                binaryWriter.Write(animation.LimbMask);
                foreach (AnimationDefinition animationDefinition in animation.AnimDefinitions)
                {
                    binaryWriter.Write(animationDefinition.Start);
                    if (!animationDefinition.Disabled)
                    {
                        binaryWriter.Write(animationDefinition.NoLoopAndEndOffset);
                    }
                }
            }

            foreach (byte command in Commands)
            {
                binaryWriter.Write(command);
            }

            foreach (Limb limb in Limbs)
            {
                foreach (ushort imageOffset in limb.ImageOffsets)
                {
                    binaryWriter.Write(imageOffset);
                }
            }

            foreach (CostumeImageData costumeImageData in Pictures)
            {
                binaryWriter.Write(costumeImageData.Width);
                binaryWriter.Write(costumeImageData.Height);
                binaryWriter.Write(costumeImageData.RelX);
                binaryWriter.Write(costumeImageData.RelY);
                binaryWriter.Write(costumeImageData.MoveX);
                binaryWriter.Write(costumeImageData.MoveY);
                if (costumeImageData.HasRedirInfo)
                {
                    binaryWriter.Write(costumeImageData.RedirLimb);
                    binaryWriter.Write(costumeImageData.RedirPict);
                }
                binaryWriter.Write(costumeImageData.ImageData);
            }

            if (HasCloseByte)
            {
                binaryWriter.Write(CloseByte);
            }
        }

        public override void LoadFromBinaryReader(Stream binaryReader)
        {
            base.LoadFromBinaryReader(binaryReader);

            if (_gameInfo.ScummVersion == 6)
            {
                Size = binaryReader.ReadUint32(); //size: 32le can be 0 (see num anim) or the size (sometimes with an offset of one ??)
                Header = binaryReader.ReadUTF8Sring(2); //header: 2*8 always contain "CO"
                if (Header != "CO") throw new InvalidFileFormatException(string.Format("Unknown costume Header. Expected 'CO' but was '{0}'", Header));
            }

            NumAnim = binaryReader.ReadByte1(); //num anim: 8  if(size) num_anim++

            //lets assign to a new variable to keep the read value intact
            byte totalAnimations = NumAnim;
            if (Size > 0 || _gameInfo.ScummVersion == 5) totalAnimations++;

            Format = binaryReader.ReadByte1(); //format: 8  bit 7 set means that west anims must NOT be mirrored, bit 0 is the palette size (0: 16 colors, 1: 32 colors)

            //discover the palette size to continue reading
            PaletteSize = 16;
            if (BinaryHelper.CheckBitState(Format, 0))
            {
                PaletteSize = 32;
            }

            //palette            : 8*num colors coded in format
            Palette = new List<byte>();
            for (int i = 0; i < PaletteSize; i++)
            {
                byte paletteEntry = binaryReader.ReadByte1();
                Palette.Add(paletteEntry);
            }

            AnimCommandsOffset = binaryReader.ReadUint16(); //anim cmds offset   : 16le access the anim cmds array

            LimbsOffsets = new List<ushort>();
            for (int i = 0; i < 16; i++)
            {
                ushort limbOffSet = binaryReader.ReadUint16();
                LimbsOffsets.Add(limbOffSet);//limbs offset       : 16*16le access limb picture table
            }

            //anim offsets       : 16le*num anim  access anim definitions
            AnimOffsets = new List<ushort>();
            for (int i = 0; i < totalAnimations; i++)
            {
                ushort currentOffset = binaryReader.ReadUint16();
                AnimOffsets.Add(currentOffset);
            }

            /*
                anim
                    limb mask        : 16le
                    anim definitions : variable length, one definition for each bit set to 1 in the limb mask.
                        0xFFFF       : 16le disabled limb code
                        OR
                        start        : 16le
                        noloop       : 1
                        end offset   : 7 offset of the last frame, or len-1  (As far as I understood this is not the final position but the offset length.)
             */
            Animations = new List<Animation>();
            for (int i = 0; i < AnimOffsets.Count; i++)
            {
                //For some reason NumAnimations is not the real number of animations: the array seems to "reserve"
                //slots for unused animations. The index sometimes points to animation offset 0, meaning "no
                //animation here". So we only keep reading the binary stream when the index points to a real one.
                if (AnimOffsets[i] > 0)
                {
                    Animation existingAnimation = Animations.SingleOrDefault(x => x.Offset == AnimOffsets[i]);
                    if (existingAnimation == null)
                    {
                        //For this to work the binaryReader position must match the announced offset.
                        //If it does not, break into the debugger: something is wrong with this code
                        //and it needs to be checked.
                        if (AnimOffsets[i] != DebugGetCurrentRelativePosition(binaryReader)) Debugger.Break();

                        var currentAnimation = new Animation();
                        currentAnimation.Offset = AnimOffsets[i];
                        currentAnimation.LimbMask = binaryReader.ReadUint16();

                        for (int j = 0; j < currentAnimation.NumLimbs; j++)
                        {
                            var currentDefinition = new AnimationDefinition();
                            currentDefinition.Start = binaryReader.ReadUint16();
                            if (!currentDefinition.Disabled)
                            {
                                currentDefinition.NoLoopAndEndOffset = binaryReader.ReadByte1();
                            }
                            currentAnimation.AnimDefinitions.Add(currentDefinition);
                        }
                        Animations.Add(currentAnimation);
                    }
                }
            }


            //According to the SCUMMC site, the size of the CMD array (and other items) comes from the
            //indexes alone: one thing starts where the previous one ends. Original text below:
            //
            //It seems the data is always properly ordered. That is, the first picture of the first limb comes right after the last limb table. 
            //The first limb table start right after the cmd array, and so on. Currently this seems to be the only way to determine how long the 
            //cmd array is, or how long the last limb table is. Clumsy but it works, however a simple decoder doesn’t need to compute these lengths :)

            //      anim cmds
            //        cmd: 8

            //This count ends up 1 byte behind. Either the previous section ends with a 00 that is not being
            //skipped, or there is a real bug. Check whether the next costumes always end with 00 or 00 00...
            //For now the start+length code is commented out and the size is computed the way the site describes.

            //Tamanhos do CMD Array
            if (AnimCommandsOffset != (DebugGetCurrentRelativePosition(binaryReader))) Debugger.Break();

            int cmdArraySize = (int)(LimbsOffsets.First() - (DebugGetCurrentRelativePosition(binaryReader)));
            //cmdArraySize = 0;
            //foreach (Animation animation in Animations)
            //{
            //    foreach (AnimationDefinition animationDefinition in animation.AnimDefinitions)
            //    {
            //        if (!animationDefinition.Disabled)
            //        {
            //            int cmdArrayFinalPosition = animationDefinition.Start + animationDefinition.Length;
            //            if (cmdArrayFinalPosition > cmdArraySize)
            //            {
            //                cmdArraySize = cmdArrayFinalPosition;
            //            }
            //        }
            //    }
            //}

            Commands = new List<byte>();
            for (int i = 0; i < cmdArraySize; i++)
            {
                Commands.Add(binaryReader.ReadByte1());
            }


            Limbs = new List<Limb>();

            //Take only the distinct limb offsets, without repetitions, and ignore the last value -
            //the last value seems to point to the end of the list!
            List<ushort> differentLimbsOnly = LimbsOffsets.Distinct().ToList();
            for (int i = 0; i < differentLimbsOnly.Count - 1; i++)
            {
                Limb currentLimb = new Limb();
                currentLimb.OffSet = differentLimbsOnly[i];
                currentLimb.Size = (ushort)(differentLimbsOnly[i + 1] - differentLimbsOnly[i]);

                Limbs.Add(currentLimb);
            }
            //To size the last limb we need to know where the first picture of the first limb starts: per the
            //SCUMMC text it comes right after the last limb table. So peek the first picture offset of the
            //first limb (the first ushort to be read) and subtract the start offset of the last limb.
            Limb lastLimb = new Limb();
            lastLimb.OffSet = differentLimbsOnly[differentLimbsOnly.Count - 1];

            //TEST to figure out what is going on here
            ushort nextValue = binaryReader.PeekUint16();
            if (nextValue == 0)
            {
                //Debugger.Break();
            }
            else
            {
                lastLimb.Size = (ushort)(nextValue - lastLimb.OffSet);
            }

            //If the size is 0 the limb probably does not exist. The SCUMM engine seems to size a limb by
            //subtracting its offset from the next limb offset, so a size-less last limb would exist
            //only so its offset can size the previous one.
            //anterior. 
            //MAYBE that is why nextValue above is sometimes 0: the value was not the start of the next
            //picture and was nulled. This whole theory may also be nonsense.
            if (lastLimb.Size > 0)
            {
                Limbs.Add(lastLimb);
            }


            foreach (Limb limb in Limbs)
            {
                if ((limb.Size % 2) != 0) Debugger.Break();
                if (limb.OffSet != (DebugGetCurrentRelativePosition(binaryReader))) Debugger.Break();

                //Each index entry has 2 bytes (ushort), so the number of entries is the limb size divided by 2
                for (int i = 0; i < (limb.Size / 2); i++)
                {
                    ushort currentImageOffset = binaryReader.ReadUint16();
                    limb.ImageOffsets.Add(currentImageOffset);
                }
            }

            Pictures = new List<CostumeImageData>();
            int picturesHeaderSize = 12;
            if ((Format & 0x7E) == 0x60)
            {
                //soh vai ter redir_limb e redir_pic se os bits 6 e 7 do format estiver ligados.
                picturesHeaderSize = 14;
            }

            //First compute the data size of each picture.
            //For that we take the next picture offset and subtract this one
            //the current picture offset, giving the total picture size.
            //From that total we subtract 14 (or 12), the size of the picture header.
            CostumeImageData lastImageData = null;
            foreach (Limb limb in Limbs)
            {
                if (limb.ImageOffsets.Count > 0)
                {
                    if (lastImageData != null)
                    {
                        ushort firstWithImageOffSet = limb.ImageOffsets.Where(x => x > 0).First();
                        lastImageData.ImageDataSize = (ushort)((firstWithImageOffSet - lastImageData.ImageStartOffSet) - picturesHeaderSize);
                        Pictures.Add(lastImageData);
                    }

                    for (int i = 0; i < limb.ImageOffsets.Count - 1; i++)
                    {
                        if (limb.ImageOffsets[i] > 0)
                        {
                            //if (limb.ImageOffsets[i] != (DebugGetCurrentRelativePosition(binaryReader))) Debugger.Break();

                            ushort nextWithImageOffset = limb.ImageOffsets.Skip(i + 1).Where(x => x > 0).First();


                            CostumeImageData currentCostumeImageData = new CostumeImageData();
                            currentCostumeImageData.ImageStartOffSet = limb.ImageOffsets[i];
                            currentCostumeImageData.ImageDataSize = (ushort)((nextWithImageOffset - currentCostumeImageData.ImageStartOffSet) - picturesHeaderSize);

                            Pictures.Add(currentCostumeImageData);
                        }
                    }
                    lastImageData = new CostumeImageData();
                    ushort lastWithImageOffset = limb.ImageOffsets.Where(x => x > 0).Last();
                    lastImageData.ImageStartOffSet = lastWithImageOffset; //limb.ImageOffsets[limb.ImageOffsets.Count - 1];
                }
            }
            if (lastImageData != null)
            {
                uint sizeVerify = 0;

                switch (_gameInfo.ScummVersion)
                {
                    case 5:
                        sizeVerify = BlockSize - 2;
                        break;
                    case 6:
                        //To size the last picture we use the size(??) parameter, the first value the costume read.
                        //Maybe the blocksize should be used instead - this size field makes little sense given the
                        //blocksize exists exactly for that. When SIZE is 0 the blocksize must be used anyway (?)
                        sizeVerify = Size == 0 ? BlockSize - 8 : Size;
                        break;
                    default:
                        Debugger.Break(); //Should never get here.
                        break;
                }
                lastImageData.ImageDataSize = (ushort)((sizeVerify - lastImageData.ImageStartOffSet) - picturesHeaderSize);
                Pictures.Add(lastImageData);
            }

            //Finally, after this DREADFUL detour, we seem to reach the actual animation frame data...
            //hopefully, at least.
            foreach (CostumeImageData picture in Pictures)
            {
                /*
                width            : 16le
                height           : 16le
                rel_x            : s16le
                rel_y            : s16le
                move_x           : s16le
                move_y           : s16le
                redir_limb       : 8 only present if((format & 0x7E) == 0x60)
                redir_pict       : 8 only present if((format & 0x7E) == 0x60)
                rle data
                 */
                picture.Width = binaryReader.ReadUint16();
                picture.Height = binaryReader.ReadUint16();
                picture.RelX = binaryReader.ReadInt16();
                picture.RelY = binaryReader.ReadInt16();
                picture.MoveX = binaryReader.ReadInt16();
                picture.MoveY = binaryReader.ReadInt16();
                if (picturesHeaderSize == 14)
                {
                    //Experimenting, REDIR_LIMB and REDIR_PICT information seems to be present only when
                    //size == 0. No idea why, but that is how it looks. More tests needed.
                    picture.HasRedirInfo = true;
                    picture.RedirLimb = binaryReader.ReadByte1();
                    picture.RedirPict = binaryReader.ReadByte1();
                }
                picture.ImageData = binaryReader.ReadBytes(picture.ImageDataSize);
            }

            if (_gameInfo.ScummVersion == 6)
            {
                uint blockSizeWithoutHeader = (BlockSize - 8);
                if (blockSizeWithoutHeader == Size)
                {
                    //does nothing
                }
                else if (blockSizeWithoutHeader == Size + 1)
                {
                    HasCloseByte = true;
                    CloseByte = binaryReader.ReadByte1();
                }
            }

            //TEM GATO NA TUBA!?!?
            if (binaryReader.Position - BlockOffSet != BlockSize) Debugger.Break();

        }

        private long DebugGetCurrentRelativePosition(Stream binaryReader)
        {
            switch (_gameInfo.ScummVersion)
            {
                case 5:
                    return binaryReader.Position - (BlockOffSet + 2);
                case 6:
                    return binaryReader.Position - (BlockOffSet + 8);
            }

            return 0;
        }
    }

    /*
    anim
        limb mask        : 16le
        anim definitions : variable length, one definition for each bit set to 1 in the limb mask.
    */
    public class Animation
    {
        private ushort _limbMask;
        private byte _numLimbs;

        //Several indexes can point to the same animation offset, so each animation is read only once:
        //the offset is stored here to check whether it was already read. Obviously it does not
        //count towards the size.
        public ushort Offset { get; set; }

        public Animation()
        {
            AnimDefinitions = new List<AnimationDefinition>();
        }

        //LimbMask holds the number of limbs and their positions: each set bit is a limb in use, the limb index being the bit index.
        public ushort LimbMask
        {
            get { return _limbMask; }
            set
            {
                _limbMask = value;
                for (int i = 0; i < 16; i++)
                {
                    if (BinaryHelper.CheckBitState(_limbMask, i)) _numLimbs++;
                }
            }
        }

        //Pre-computes the number of limbs.
        public byte NumLimbs { get { return _numLimbs; } }

        public List<AnimationDefinition> AnimDefinitions { get; set; }

        public ushort GetSize()
        {
            ushort size = 2; //LimbMask;
            foreach (AnimationDefinition animationDefinition in AnimDefinitions)
            {
                size += animationDefinition.GetSize();
            }
            return size;
        }
    }


    /*
    anim definition
        0xFFFF       : 16le disabled limb code
        OR
        start        : 16le
        noloop       : 1
        end offset   : 7 offset of the last frame, or len-1  (As far as I understood this is the size, not the final offset. The text below was
                                                              taken from SCUMMC, on github:
        if the index is not 0xFFFF, then it’s followed by the length of the sequence (8 bits). 
        The highest bit of the length is used to indicate whether the sequence should loop, if it is set the animation doesn’t loop.
     */
    public class AnimationDefinition
    {
        public ushort Start { get; set; }
        public byte NoLoopAndEndOffset { get; set; }

        public bool NoLoop
        {
            get
            {
                return BinaryHelper.CheckBitState(NoLoopAndEndOffset, 7);
            }
        }

        public byte Length
        {
            get
            {
                return BinaryHelper.GetBitsFromByte(NoLoopAndEndOffset, 7);
            }
        }

        public bool Disabled
        {
            get
            {
                return Start == 0xFFFF;
            }
        }

        public ushort GetSize()
        {
            ushort size = 2; //Start;
            if (!Disabled)
            {
                size += 1; //NoLoopeAndEndOffset, but have only have this value when start is not 0xFFFF;
            }
            return size;
        }
    }

    public class Limb
    {
        public Limb()
        {
            ImageOffsets = new List<ushort>();
        }
        public ushort OffSet { get; set; }
        public ushort Size { get; set; }
        public List<ushort> ImageOffsets { get; set; }
    }

    /*
    picts
        width            : 16le
        height           : 16le
        rel_x            : s16le
        rel_y            : s16le
        move_x           : s16le
        move_y           : s16le
        redir_limb       : 8 only present if((format & 0x7E) == 0x60)
        redir_pict       : 8 only present if((format & 0x7E) == 0x60)
        rle data

     */
    public class CostumeImageData
    {
        //The properties below are computed by the reader to help extract the data and regenerate it
        //later, updating the limb position information.
        public int ImageDataSize { get; set; }
        public ushort ImageStartOffSet { get; set; }
        public bool HasRedirInfo { get; set; }

        //Dados extraidos do binario
        public ushort Width { get; set; }
        public ushort Height { get; set; }
        public short RelX { get; set; }
        public short RelY { get; set; }
        public short MoveX { get; set; }
        public short MoveY { get; set; }
        public byte RedirLimb { get; set; }
        public byte RedirPict { get; set; }
        public byte[] ImageData { get; set; }

        public ushort GetSize()
        {
            ushort size = 2; //Width;
            size += 2; //Height
            size += 2; //RelX
            size += 2; //RelY
            size += 2; //MoveX
            size += 2; //MoveY
            if (HasRedirInfo)
            {
                size += 1; //RedirLimb
                size += 1; //RedirPict
            }
            size += (ushort)ImageData.Length; //Size of ImageData

            return size;
        }
    }

}