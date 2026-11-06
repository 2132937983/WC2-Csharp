using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace WC2;

/// <summary>
/// The options page layout in JSON (<c>data/ui/options.json</c>), replacing guioptions.xml:
/// <code>
/// {
///   "phone":  [ {"element": "image", "item": "musicVolume", "src": "options_volume_mute.png", "x": 255, "y": 16, "w": 14, "h": 22},
///               {"element": "button", "id": "back", "x": 0, "y": 290, ...}, ... ],
///   "tablet": [ ... ]
/// }
/// </code>
/// Each entry is one control in file order; "element" is its kind (image, text, button, slider,
/// checkbox, level) and the other keys are the attributes documented in guioptions.xml. The page
/// builder (GUIOptions) reads the same attributes either way: this class hands it the JSON as an
/// in-memory document.
/// </summary>
public static class OptionsLayoutJson
{
    /// <summary>The layout document: from data/ui/options.json, or the original guioptions.xml.</summary>
    public static TiXmlDocument Load()
    {
        var root = JsonData.LoadMerged(DataPaths.OptionsLayout);
        if (root == null)
        {
            var xml = new TiXmlDocument(GetPath("guioptions.xml", null));
            return xml.LoadFile(TIXML_ENCODING_UNKNOWN) ? xml : null;
        }
        var options = new XElement("guiOptions");
        foreach (var layoutName in new[] { "phone", "tablet" })
        {
            var entries = root.Arr(layoutName);
            if (entries == null) continue;
            var layout = new XElement(layoutName);
            foreach (var entry in entries.Objects())
            {
                string tag = entry.Str("element");
                if (string.IsNullOrEmpty(tag)) continue;
                var element = new XElement(tag);
                foreach (var (key, value) in entry)
                {
                    if (key == "element") continue;
                    string text = value?.Text();
                    if (text != null) element.SetAttributeValue(key, text);
                }
                layout.Add(element);
            }
            options.Add(layout);
        }
        var doc = new TiXmlDocument();
        return doc.Parse(options.ToString()) ? doc : null;
    }

    /// <summary>Converts guioptions.xml (--convert-data).</summary>
    public static JsonObject ConvertXml()
    {
        var xml = new TiXmlDocument(GetPath("guioptions.xml", null));
        if (!xml.LoadFile(TIXML_ENCODING_UNKNOWN)) return null;
        var root = xml.FirstChildElement("guiOptions");
        if (root == null) return null;
        var result = new JsonObject
        {
            ["format"] = 1,
            ["about"] = "Options page layout (converted from guioptions.xml). phone coordinates are relative to a 480x320 area centred at run time, tablet coordinates are absolute 1024x768. Each entry is one control; \"element\" is image, text, button, slider, checkbox or level.",
        };
        foreach (var layoutName in new[] { "phone", "tablet" })
        {
            var layout = root.FirstChildElement(layoutName);
            if (layout == null) continue;
            var entries = new JsonArray();
            for (var e = layout.FirstChildElement(); e != null; e = e.NextSiblingElement())
            {
                var o = new JsonObject { ["element"] = e.Value() };
                foreach (var (name, value) in Attributes(e)) o[name] = JsonData.Value(value);
                entries.Add(o);
            }
            result[layoutName] = entries;
        }
        return result;
    }

    /// <summary>Every control as "element name=value ..." in file order, for --verify-data.</summary>
    public static List<string> Describe(TiXmlDocument doc)
    {
        var lines = new List<string>();
        var root = doc?.FirstChildElement("guiOptions");
        if (root == null) return lines;
        foreach (var layoutName in new[] { "phone", "tablet" })
        {
            var layout = root.FirstChildElement(layoutName);
            for (var e = layout?.FirstChildElement(); e != null; e = e.NextSiblingElement())
                lines.Add(layoutName + " " + e.Value() + " " + string.Join(" ", Attributes(e).Select(a => a.name + "=" + a.value)));
        }
        return lines;
    }

    private static IEnumerable<(string name, string value)> Attributes(TiXmlElement e) =>
        e.Attributes().Select(kv => (kv.Key, kv.Value));
}
