using System.IO;

namespace ScummEditor.Gui
{
    public class ImageInfo
    {
        public string Filename { get; private set; }
        public ImageType ImageType { get; private set; }

        public int RoomIndex { get; private set; }
        public int ZPlaneIndex { get; private set; }

        public int ObjectIndex { get; private set; }
        public int ImageIndex { get; private set; }

        public int CostumeIndex { get; private set; }
        public int FrameIndex { get; private set; }

        public ImageInfo(string fileName)
        {
            Filename = fileName;

            RoomIndex = -1;
            ZPlaneIndex = -1;
            ObjectIndex = -1;
            ImageIndex = -1;
            CostumeIndex = -1;
            FrameIndex = -1;
            ImageType = ImageType.Unknown;

            Parse();
        }

        private void Parse()
        {
            string[] fileParts = Filename.Split(' ');
            foreach (var filePart in fileParts)
            {
                string pName = Path.GetFileNameWithoutExtension(filePart);
                var pairValues = pName.Split('#');
                switch (pairValues[0])
                {
                    case "Room":
                        RoomIndex = int.Parse(pairValues[1]);
                        break;
                    case "Costume":
                        CostumeIndex = int.Parse(pairValues[1]);
                        break;
                    case "FrameIndex":
                        FrameIndex = int.Parse(pairValues[1]);
                        break;
                    case "Obj":
                        ObjectIndex = int.Parse(pairValues[1]);
                        break;
                    case "Img":
                        ImageIndex = int.Parse(pairValues[1]);
                        break;
                    case "ZP":
                        ZPlaneIndex = int.Parse(pairValues[1]);
                        break;
                }
            }

            //Determine the ImageType
            if (RoomIndex < 0) return;

            if (CostumeIndex >= 0)
            {
                ImageType = ImageType.Costume;
            }
            else if (ObjectIndex >= 0)
            {
                if (ZPlaneIndex >= 0)
                {
                    ImageType = ImageType.ObjectsZPlane;
                }
                else
                {
                    ImageType = ImageType.Object;
                }
            }
            else
            {
                if (ZPlaneIndex >= 0)
                {
                    ImageType = ImageType.ZPlane;
                }
                else
                {
                    ImageType = ImageType.Background;
                }
            }
        }
    }
}
