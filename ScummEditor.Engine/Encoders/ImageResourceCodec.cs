using System.Drawing;
using ScummEditor.Structures.DataFile;

namespace ScummEditor.Encoders
{
    /// <summary>
    /// Decodes/encodes one v5/v6 room image resource, picking the right codec for its type: room
    /// background, object image (BMAP/SMAP vs BOMP, chosen by the presence of an SMAP), a z-plane
    /// mask, or a costume frame. Pure engine - a GUI just chooses the type + indices and shows or
    /// saves the returned Bitmap, with no knowledge of which decoder applies. Mirrors what the v5/v6
    /// image viewer and the batch export/import both need, so the selection lives in one place.
    /// </summary>
    public static class ImageResourceCodec
    {
        public static Bitmap Decode(RoomBlock room, Costume costume, ImageType type,
            int objectIndex, int imageIndex, int zPlaneIndex, int paletteIndex, bool transparent)
        {
            switch (type)
            {
                case ImageType.Background:
                {
                    var decoder = new ImageDecoder { PaletteIndex = paletteIndex, UseTransparentColor = transparent };
                    return decoder.Decode(room);
                }
                case ImageType.ZPlane:
                    return new ZPlaneDecoder().Decode(room, zPlaneIndex);
                case ImageType.Object:
                    if (room.GetOBIMs()[objectIndex].GetIMxx()[imageIndex].GetSMAP() == null)
                    {
                        var decoder = new BompImageDecoder { PaletteIndex = paletteIndex, UseTransparentColor = transparent };
                        return decoder.Decode(room, objectIndex, imageIndex);
                    }
                    else
                    {
                        var decoder = new ImageDecoder { PaletteIndex = paletteIndex, UseTransparentColor = transparent };
                        return decoder.Decode(room, objectIndex, imageIndex);
                    }
                case ImageType.ObjectsZPlane:
                    return new ZPlaneDecoder().Decode(room, objectIndex, imageIndex, zPlaneIndex);
                case ImageType.Costume:
                {
                    var decoder = new CostumeImageDecoder { PaletteIndex = paletteIndex, UseTransparentColor = transparent };
                    return decoder.Decode(room, costume, imageIndex);
                }
                default:
                    return null;
            }
        }

        public static void Encode(RoomBlock room, Costume costume, ImageType type,
            int objectIndex, int imageIndex, int zPlaneIndex, Bitmap bitmap, ImageEncoder.EncodeTypeSettings compression)
        {
            switch (type)
            {
                case ImageType.Background:
                {
                    var encoder = new ImageEncoder { EncodeSettings = compression };
                    encoder.Encode(room, bitmap);
                    break;
                }
                case ImageType.ZPlane:
                    new ZPlaneEncoder().Encode(room, bitmap, zPlaneIndex);
                    break;
                case ImageType.Object:
                    if (room.GetOBIMs()[objectIndex].GetIMxx()[imageIndex].GetSMAP() == null)
                    {
                        new BompImageEncoder().Encode(room, objectIndex, imageIndex, bitmap);
                    }
                    else
                    {
                        var encoder = new ImageEncoder { EncodeSettings = compression };
                        encoder.Encode(room, objectIndex, imageIndex, bitmap);
                    }
                    break;
                case ImageType.ObjectsZPlane:
                    new ZPlaneEncoder().Encode(room, objectIndex, imageIndex, bitmap, zPlaneIndex);
                    break;
                case ImageType.Costume:
                    new CostumeImageEncoder().Encode(room, costume, imageIndex, bitmap);
                    break;
            }
        }
    }
}
