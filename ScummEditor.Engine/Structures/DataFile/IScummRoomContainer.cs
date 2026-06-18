using System.Collections.Generic;

namespace ScummEditor.Engine.Structures.DataFile
{
    /// <summary>
    /// A container that holds one room and its costumes, abstracting the two layouts the v4 graphics
    /// batch walks: a v4 disk block (LF, many per DISKnn.LEC) and a v3 "GF_OLD256" room file
    /// (ScummV3Small256DataFile, one NN.LFL per room). Both expose the same v4 room/costume blocks, so
    /// the batch can treat them uniformly.
    /// </summary>
    public interface IScummRoomContainer
    {
        /// <summary>The room block (RO), or null if the container has none.</summary>
        ScummV4RoomBlock GetRoom();

        /// <summary>The costumes (CO) bundled alongside the room.</summary>
        List<CostumeV4> GetCostumes();
    }
}
