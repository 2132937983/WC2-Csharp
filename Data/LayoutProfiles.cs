using System.Text.Json.Nodes;

namespace WC2;

/// <summary>One screen layout of the game (an entry of <c>data/json/layout/profiles.json</c>).</summary>
public sealed class LayoutProfile
{
    /// <summary>Name (phone-640, phone-568, phone-534, tablet, phone-480).</summary>
    public string id = "";
    /// <summary>The profile applies when the screen's aspect ratio (width / height) is above this ...</summary>
    public float aspectAbove = float.NegativeInfinity;
    /// <summary>... and below this. Profiles are tried in order; the first match wins.</summary>
    public float aspectBelow = float.PositiveInfinity;
    /// <summary>Logical screen size the whole UI is laid out in.</summary>
    public int width, height;
    /// <summary>The original's width mode: 1 narrow (portrait phone), 2 phone, 3 tablet.</summary>
    public int widthMode;
    /// <summary>Art variant folder this layout prefers (ASSET_LAYOUT.md).</summary>
    public string artVariant = "hd";

    public bool IsTablet => widthMode == 3;
    public bool Matches(float aspect) => aspect > aspectAbove && aspect < aspectBelow;
    public override string ToString() => $"{id} ({width}x{height})";
}

/// <summary>
/// The screen layouts (<c>data/json/layout/profiles.json</c>) and the one chosen for this screen. The
/// built-in profiles are the original nativeInit buckets: above 1.8875 640x320, above 1.721875 568x320,
/// above 1.584375 534x320, below 1.40625 the 1024x768 tablet layout, otherwise 480x320.
/// <code>
/// { "profiles": [ { "id": "phone-640", "aspectAbove": 1.8875, "width": 640, "height": 320, "widthMode": 2, "artVariant": "phone-640-hd" }, ... ] }
/// The aspect ratio is the screen width divided by its height (the game runs in landscape).
/// </code>
/// </summary>
public static class LayoutProfiles
{
    public static List<LayoutProfile> profiles = BuiltIn();
    /// <summary>The layout of this run (set by <see cref="Select"/>).</summary>
    public static LayoutProfile Current = profiles[^1];

    private static List<LayoutProfile> BuiltIn() => new()
    {
        new LayoutProfile { id = "phone-640", aspectAbove = 1.8875f, width = 640, height = 320, widthMode = 2, artVariant = "phone-640-hd" },
        new LayoutProfile { id = "phone-568", aspectAbove = 1.721875f, width = 568, height = 320, widthMode = 2, artVariant = "phone-568-hd" },
        new LayoutProfile { id = "phone-534", aspectAbove = 1.584375f, width = 534, height = 320, widthMode = 2, artVariant = "hd" },
        new LayoutProfile { id = "tablet", aspectBelow = 1.40625f, width = 1024, height = 768, widthMode = 3, artVariant = "tablet" },
        new LayoutProfile { id = "phone-480", width = 480, height = 320, widthMode = 2, artVariant = "hd" },
    };

    public static void LoadBuiltIn() => profiles = BuiltIn();

    public static bool LoadJson()
    {
        LoadBuiltIn();
        var root = JsonData.LoadMerged(DataPaths.LayoutProfiles);
        if (root?.Arr("profiles") is not JsonArray list || list.Count == 0) return false;
        var loaded = new List<LayoutProfile>();
        foreach (var node in list)
        {
            if (node is not JsonObject o) continue;
            loaded.Add(new LayoutProfile
            {
                id = o.Str("id", ""),
                aspectAbove = o.Has("aspectAbove") ? o.Float("aspectAbove", 0f) : float.NegativeInfinity,
                aspectBelow = o.Has("aspectBelow") ? o.Float("aspectBelow", 0f) : float.PositiveInfinity,
                width = o.Int("width", 480),
                height = o.Int("height", 320),
                widthMode = o.Int("widthMode", 2),
                artVariant = o.Str("artVariant", "hd"),
            });
        }
        if (loaded.Count > 0) profiles = loaded;
        return true;
    }

    /// <summary>Picks the profile for a screen (width / height, as the original) and makes it <see cref="Current"/>.</summary>
    public static LayoutProfile Select(int screenW, int screenH)
    {
        float aspect = (float)screenW / Math.Max(1, screenH);
        Current = profiles.FirstOrDefault(p => p.Matches(aspect)) ?? profiles[^1];
        return Current;
    }

    public static JsonObject BuiltInJson()
    {
        LoadBuiltIn();
        var list = new JsonArray();
        foreach (var p in profiles)
        {
            var o = new JsonObject { ["id"] = p.id };
            if (!float.IsNegativeInfinity(p.aspectAbove)) o["aspectAbove"] = JsonData.Value(p.aspectAbove.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            if (!float.IsPositiveInfinity(p.aspectBelow)) o["aspectBelow"] = JsonData.Value(p.aspectBelow.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            o["width"] = p.width;
            o["height"] = p.height;
            o["widthMode"] = p.widthMode;
            o["artVariant"] = p.artVariant;
            list.Add(o);
        }
        return new JsonObject
        {
            ["format"] = 1,
            ["about"] = "Screen layouts, tried in order: the first whose aspect range (width / height) contains the screen's wins. " +
                        "The values are the original buckets. Field reference: DATA_SCHEMA.md#layout-profiles.",
            ["profiles"] = list,
        };
    }

    public static object Snapshot() => profiles.Select(p => new { p.id, p.aspectAbove, p.aspectBelow, p.width, p.height, p.widthMode, p.artVariant }).ToList();
}
