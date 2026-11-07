namespace WC2;

/// <summary>
/// Pictures of a country that the original atlases do not know (a mod's new country, MODDING.md
/// "New countries"). A mod needs only one picture per country: <c>flag_&lt;id&gt;.png</c>, the plain
/// flag (any size, wider than tall; 150 x 102 pixels matches the original proportions). The game
/// builds everything else from it when the atlases have nothing better:
/// <list type="bullet">
/// <item>the map flag: an atlas flag's pole with this cloth (the atlas flags are 65 x 115 pictures,
/// pole at x 18-23, cloth from (19, 19) to (65, 46), anchored at the pole base (21, 99));</item>
/// <item>the flag of the boxes (attack, AI progress, defeat, saves): the picture at the flag table's
/// size (50 x 34);</item>
/// <item>the conquest list button (<c>button_&lt;id&gt;.png</c>) and the small preview flag
/// (<c>sflag_&lt;id&gt;.png</c>): a picture of that name when the mod has one (WC2.Tools build-scenario
/// draws both), otherwise the plain flag.</item>
/// </list>
/// A picture taller than wide is taken as a complete map flag with its pole, laid out like the atlas
/// flags (its cloth area serves the boxes).
/// </summary>
public static class CountryArt
{
    // the atlas map flag, as fractions of its picture
    private const float PoleWidth = 34f / 65f;
    private const float ClothLeft = 19f / 65f, ClothTop = 19f / 115f, ClothWidth = 46f / 65f, ClothHeight = 27f / 115f;
    /// <summary>Logical width of the flag table's pictures.</summary>
    private const float UiFlagWidth = 50f;

    private static readonly Dictionary<string, ecImageAttr> pictures = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The picture of that name from the asset layers (mods first), or null. A plain flag is scaled to the
    /// flag table's logical width; a picture taller than wide keeps its pixel size.
    /// </summary>
    public static ecImageAttr Picture(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (pictures.TryGetValue(name, out var cached)) return cached;
        ecImageAttr attr = null;
        if (ecGraphics.ResolveTextureFile(name, out _) != null &&
            ecGraphics.Instance().LoadTexture(name) is ecTexture tex && tex.pixelW > 0 && tex.pixelH > 0)
        {
            if (tex.pixelW >= tex.pixelH && name.StartsWith("flag_", StringComparison.OrdinalIgnoreCase))
            {
                // a plain flag: logical size = the flag table's (50 wide), whatever its pixels
                tex.xC = tex.pixelW / UiFlagWidth;
                tex.w = (int)UiFlagWidth;
                tex.h = Math.Max(1, (int)MathF.Round(tex.pixelH / tex.xC));
            }
            attr = new ecImageAttr { texture = tex, name = name, x = 0, y = 0, w = tex.w, h = tex.h };
        }
        pictures[name] = attr;
        return attr;
    }

    private static bool IsPoleFlag(ecImageAttr a) => a != null && a.h > a.w;

    /// <summary>The flag of the boxes for "flag_&lt;id&gt;.png" when the flag table has none.</summary>
    public static ecImageAttr UiFlag(string flagName)
    {
        var a = Picture(flagName);
        if (a == null || !IsPoleFlag(a)) return a;
        // a complete map flag: its cloth
        return new ecImageAttr
        {
            texture = a.texture, name = a.name,
            x = a.w * ClothLeft, y = a.h * ClothTop, w = a.w * ClothWidth, h = a.h * ClothHeight,
        };
    }

    /// <summary>The conquest list button of a country: the UI atlas, a picture of that name, the plain flag.</summary>
    public static ecImageAttr Button(ecTextureRes ui, string countryName) =>
        ui.GetImage($"button_{countryName}.png") ?? Picture($"button_{countryName}.png") ?? UiFlag($"flag_{countryName}.png");

    /// <summary>The small preview flag of a country: the UI atlas, a picture of that name, the plain flag.</summary>
    public static ecImageAttr SmallFlag(ecTextureRes ui, string countryId) =>
        ui.GetImage($"sflag_{countryId}.png") ?? Picture($"sflag_{countryId}.png") ?? UiFlag($"flag_{countryId}.png");

    /// <summary>A map flag built from a mod's picture: drawn at an atlas flag's size and anchor.</summary>
    public sealed class MapFlag
    {
        private ecImage pole;
        private ecImage cloth;
        private float offsetX, offsetY, width, height;

        /// <summary>
        /// The map flag of a country without an atlas flag, or null without a picture.
        /// <paramref name="donor"/> is any atlas map flag (its pole and size are used).
        /// </summary>
        public static MapFlag Create(string countryName, ecImageAttr donor)
        {
            if (donor == null) return null;
            var picture = Picture($"flag_{countryName}.png");
            if (picture == null) return null;
            var flag = new MapFlag();
            if (IsPoleFlag(picture))
            {
                // a complete map flag: the whole picture at the atlas flag's size and anchor
                flag.cloth = new ecImage(picture);
                flag.offsetX = -donor.refx; flag.offsetY = -donor.refy;
                flag.width = donor.w; flag.height = donor.h;
                return flag;
            }
            flag.pole = new ecImage(donor);
            flag.pole.SetTextureRect(donor.x, donor.y, donor.w * PoleWidth, donor.h);
            flag.cloth = new ecImage(picture);
            flag.offsetX = donor.w * ClothLeft - donor.refx;
            flag.offsetY = donor.h * ClothTop - donor.refy;
            flag.width = donor.w * ClothWidth;
            flag.height = donor.h * ClothHeight;
            return flag;
        }

        /// <summary>Draws the flag with its pole base at (x, y), like an atlas flag.</summary>
        public void Render(float x, float y)
        {
            pole?.Render(x, y);
            cloth.Render(x + offsetX, y + offsetY, width, height);
        }
    }
}
