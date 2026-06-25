namespace ScummEditor.Engine.Encoders
{
    public enum ImageType
    {
        Unknown=0,
        Background = 1,
        ZPlane = 2,
        Object = 3,
        ObjectsZPlane = 4,
        Costume = 5,
        AkosCostume = 6 // SCUMM v7 AKOS costume cel (decoded/encoded by AkosImageDecoder/AkosImageEncoder)
    }
}