using System.Text.Json.Nodes;

namespace WC2;

/// <summary>
/// Which sound file plays for what (<c>data/json/audio/audio.json</c>). The built-in values are the
/// original game's file names.
/// <code>
/// { "music": { "menu": ["battle1.mp3"], "battle": ["battle1.mp3", ...], "defeat": ["defeat_music.mp3"] },
///   "ui": { "click": "btn.wav", ... }, "effects": { "draft": "draft.wav", ... }, "preload": ["btn.wav"] }
/// </code>
/// <c>effects</c> are the in-game sound effects in <see cref="SND_EFFECT"/> order (the key is the enum
/// name without <c>_WAV</c>, lower case). A battle picks one of the <c>battle</c> tracks at random.
/// </summary>
public static class AudioDefs
{
    public static Dictionary<string, List<string>> music = new();
    public static Dictionary<string, string> ui = new();
    public static List<string> preload = new();

    private static readonly (string key, string file)[] BuiltInUi =
    {
        ("click", "btn.wav"),                 // every button, cards, list items, tutorial taps
        ("buy", "buy.wav"),                   // bank, commander purchases
        ("commanderLevelUp", "commander_lvup.wav"),
        ("mainMenu", "main_interface.wav"),   // the main menu slides in
        ("explosion", "boom.wav"),            // preloaded when a battle loads (the original never shipped the file)
    };

    /// <summary>Effect key of an <see cref="SND_EFFECT"/> value ("LV_UP_WAV" -> "lv_up").</summary>
    public static string EffectKey(SND_EFFECT e) => e.ToString().Replace("_WAV", "").ToLowerInvariant();

    public static void LoadBuiltIn()
    {
        music = new Dictionary<string, List<string>>
        {
            ["menu"] = new() { "battle1.mp3" },
            ["battle"] = new() { "battle1.mp3", "battle2.mp3", "battle3.mp3", "battle4.mp3" },
            ["defeat"] = new() { "defeat_music.mp3" },
        };
        ui = BuiltInUi.ToDictionary(x => x.key, x => x.file);
        preload = new List<string> { "btn.wav" };
        CSoundRes.FileNames = CSoundRes.BuiltInFileNames.ToArray();
    }

    public static bool LoadJson()
    {
        LoadBuiltIn();
        var root = JsonData.LoadMerged(DataPaths.Audio);
        if (root == null) return false;
        if (root.Obj("music") is JsonObject m)
            foreach (var (key, node) in m)
                if (node is JsonArray list) music[key] = list.Select(n => n?.Text()).Where(s => !string.IsNullOrEmpty(s)).ToList();
        if (root.Obj("ui") is JsonObject u)
            foreach (var (key, node) in u)
                if (node?.Text() is string f) ui[key] = f;
        if (root.Arr("preload") is JsonArray p) preload = p.Select(n => n?.Text()).Where(s => !string.IsNullOrEmpty(s)).ToList();
        if (root.Obj("effects") is JsonObject e)
        {
            var names = CSoundRes.BuiltInFileNames.ToArray();
            foreach (SND_EFFECT se in Enum.GetValues<SND_EFFECT>())
                if (e.Str(EffectKey(se)) is string f && f.Length > 0) names[(int)se] = f;
            CSoundRes.FileNames = names;
        }
        return true;
    }

    /// <summary>File of an interface sound ("click" -> "btn.wav").</summary>
    public static string Ui(string key) => ui.TryGetValue(key, out var f) ? f : key + ".wav";

    /// <summary>Number of tracks for a music situation.</summary>
    public static int MusicCount(string key) => music.TryGetValue(key, out var l) ? l.Count : 0;

    /// <summary>Track <paramref name="index"/> of a music situation (wraps around); null when there is none.</summary>
    public static string Music(string key, int index = 0) =>
        music.TryGetValue(key, out var l) && l.Count > 0 ? l[((index % l.Count) + l.Count) % l.Count] : null;

    public static JsonObject BuiltInJson()
    {
        LoadBuiltIn();
        var effects = new JsonObject();
        foreach (SND_EFFECT se in Enum.GetValues<SND_EFFECT>()) effects[EffectKey(se)] = CSoundRes.FileNames[(int)se];
        return new JsonObject
        {
            ["format"] = 1,
            ["about"] = "Which sound plays for what (the values are the original game's files). Field reference: DATA_SCHEMA.md#audio.",
            ["music"] = new JsonObject(music.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode)new JsonArray(kv.Value.Select(v => (JsonNode)v).ToArray())))),
            ["ui"] = new JsonObject(ui.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode)kv.Value))),
            ["effects"] = effects,
            ["preload"] = new JsonArray(preload.Select(v => (JsonNode)v).ToArray()),
        };
    }

    public static object Snapshot() => new
    {
        music = music.ToDictionary(kv => kv.Key, kv => string.Join(",", kv.Value)),
        ui = ui.ToDictionary(kv => kv.Key, kv => kv.Value),
        effects = CSoundRes.FileNames.ToList(),
        preload = preload.ToList(),
    };
}
