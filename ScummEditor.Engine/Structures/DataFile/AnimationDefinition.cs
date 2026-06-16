using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ScummEditor.Exceptions;

namespace ScummEditor.Structures.DataFile
{


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

}
