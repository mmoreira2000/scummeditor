using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ScummEditor.Exceptions;

namespace ScummEditor.Structures.DataFile
{

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

}
