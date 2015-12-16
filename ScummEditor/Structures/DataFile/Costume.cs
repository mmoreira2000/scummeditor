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

        public byte CloseByte { get; set; } //sei lah, mas parece que os costumes vem com um close byte !?!?
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
                        end offset   : 7 offset of the last frame, or len-1  (Pelo que entendi, esse não é a posição final, e sim o comprimento do offset.
             */
            Animations = new List<Animation>();
            for (int i = 0; i < AnimOffsets.Count; i++)
            {
                //Por alguma razão, o NumAnimations não é o numero real de animações. Parece que tem um array "reservando" posições para animações 
                //não utilizadas.
                //O que eu quero dizer é que tem o indice de animações, mas esse indice as vezes aponta para um offset de animação 0, ou seja,
                //o indice existe e diz que não aponta para nenhuma animação, pelo que entendi. Então só vou continuar lendo do binary stream
                //quando for o indice apontara para a próxima animação.
                if (AnimOffsets[i] > 0)
                {
                    Animation existingAnimation = Animations.SingleOrDefault(x => x.Offset == AnimOffsets[i]);
                    if (existingAnimation == null)
                    {
                        //Pra isso aqui funciona, a posição do binaryReader deve ser a mesma informada no offset. 
                        //Se não for, então para no debug, porque tem alguma coisa errada com o meu código e 
                        //preciso verificar o que é.
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


            //Segundo o site do SCUMMC, achar o tamanho do CMDArray e outro itens é somente olhando os indices, pois uma coisa começa onde a
            //outra termina. Segue o texto original abaixo:
            //
            //It seems the data is always properly ordered. That is, the first picture of the first limb comes right after the last limb table. 
            //The first limb table start right after the cmd array, and so on. Currently this seems to be the only way to determine how long the 
            //cmd array is, or how long the last limb table is. Clumsy but it works, however a simple decoder doesn’t need to compute these lengths :)

            //      anim cmds
            //        cmd: 8

            //Essa conta ta ficando 1 byte atrasado. Não sei se a sessão anterior termina com 00 e eu não estou pulando
            //ou se tem algum erro mesmo. Verificar se nos próximos costumes é sempre 00 ou 00 00...
            //por hora, vou comentar o código de start+length e calcular igual o site fala.

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

            //Pega uma lista apenas com os limbs distintos, sem as repetições:
            //, e ignora o ultimo valor, sei lá porque, mas o ultimo valor parece apontar para o final da lista!
            List<ushort> differentLimbsOnly = LimbsOffsets.Distinct().ToList();
            for (int i = 0; i < differentLimbsOnly.Count - 1; i++)
            {
                Limb currentLimb = new Limb();
                currentLimb.OffSet = differentLimbsOnly[i];
                currentLimb.Size = (ushort)(differentLimbsOnly[i + 1] - differentLimbsOnly[i]);

                Limbs.Add(currentLimb);
            }
            //Para determinar o tamanho do ultimo limb, é preciso saber onde começa a primeira imagem do primeiro limb, 
            //pois ela vem, segundo o texto do scummc, logo apos a ultima tabela de limb. Então eu pego o offset da
            //primeira imagem do primeiro limb (dando peek no ushort, que será o primeiro valor lido) 
            //e subtraio o offset de inicio do ultimo limb, com isso eu descubro o tamanho.
            Limb lastLimb = new Limb();
            lastLimb.OffSet = differentLimbsOnly[differentLimbsOnly.Count - 1];

            //TESTE para tentar descobrir que porra ta acontecendo aqui
            ushort nextValue = binaryReader.PeekUint16();
            if (nextValue == 0)
            {
                //Debugger.Break();
            }
            else
            {
                lastLimb.Size = (ushort)(nextValue - lastLimb.OffSet);
            }

            //Eu to achando que se o size for 0, então na verdade esse limb não existe.
            //O negócio é que eu acho que pra determinar o tamanho do limb, o engine do scumm
            //desconta o offset do limb seguinte do limb atual, então o ultimo limb, se não
            //tiver tamanho, era porque seu offset só serviria para determinar o tamanho do limb
            //anterior. 
            //ACHO que talvez por isso, em algumas vezes o nextValue logo acima é 0,
            //porque por alguma razão o valor não era o do inicio da próxima imagem e dai foi nulado. Mas isso tudo pode ser besteira tb.
            if (lastLimb.Size > 0)
            {
                Limbs.Add(lastLimb);
            }


            foreach (Limb limb in Limbs)
            {
                if ((limb.Size % 2) != 0) Debugger.Break();
                if (limb.OffSet != (DebugGetCurrentRelativePosition(binaryReader))) Debugger.Break();

                //Como cada indice tem 2 bytes (ushort), então o total de entradas é o tamanho do limb dividido por 2
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

            //Primeiro vamos calcular o tamanho que tera os dados de cada imagem.
            //Para isso, precisamos pegar a imagem atual + 1, e descontar do offset dela
            //o offset da imagem atual. Com isso teremos o tamanho total da imagem.
            //Desse total, descontamos 14 (ou 12), que é o número de bytes do cabeçalho da imagem.
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
                        //pra determinar o tamanho da ultima imagem, vamos usar o parametro size(??) que foi o primeiro valor lido pelo costume.
                        //Talvez eu deva utilizar o blocksize, não sei... é que esse size não faz muito sentido, visto que o blocksize
                        //é justamente pra isso. Sei lá, tem alguma pegadinha aqui. Mas em alguns casos o SIZE é 0, dai tem que usar o blocksize mesmo (?)
                        sizeVerify = Size == 0 ? BlockSize - 8 : Size;
                        break;
                    default:
                        Debugger.Break(); //Não era pra cair aqui.
                        break;
                }
                lastImageData.ImageDataSize = (ushort)((sizeVerify - lastImageData.ImageStartOffSet) - picturesHeaderSize);
                Pictures.Add(lastImageData);
            }

            //Agora sim, finalmente, depois dessa volta MEDONHA, parece que conseguimos chegar de fato aos dados dos frames das
            //animações... espero que sim pelo menos.
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
                    //Mexendo, a impressão que parece é que só tem informações de REDIR_LIMB e REDIR_PICT quando
                    //o size == 0. Não sei porque, mas é isso que ta parecendo.
                    //Vou fazer mais uns testes.
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
                    //não faz nada
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

        //Como varios indicies tb apontam para o mesmooffset de animação, vou lelo apenas uma vez, para isso, vou armazenar aqui o offset
        //da animação, assim, já tenho como consulta-lo se ele foi lido ou não. Porem é obvio que ele não entra no calculo do tamanho.
        public ushort Offset { get; set; }

        public Animation()
        {
            AnimDefinitions = new List<AnimationDefinition>();
        }

        //LimbMask contem a quantidade de limbs e suas respectivas posições. Cada bit ligado é um lib que será usado, sendo o indice o mesmo do bit.
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

        //Deixa pré-calculado o numero de limbs.
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
        end offset   : 7 offset of the last frame, or len-1  (Ate onde entendi, isso é o tamanho e não o offset final. O texto abaixo foi tirado
                                                              do SCUMMC, no github:
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
        //As propriedades abaixo são calculadas pelo reader para ajudar a extrair a informação e depois regera-la,
        //atualizando as informações de posição dos limbs.
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