namespace ScummEditor.Structures.IndexFile
{
    public class DirectoryItem
    {
        public byte Number { get; set; }
        public uint Offset { get; set; }

        //Used initially to keep rooms numbers, because
        //on SAM&MAX one room can be referenced more than
        //one time on the index file.
        public string ItemId { get; set; }
    }
}