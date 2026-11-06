namespace WC2;

/// <summary>Key/value table loaded from Apple plist XML files (stringtable_*.xml, Localizable.strings).</summary>
public sealed class ecStringTable
{
    public readonly Dictionary<string, string> table = new(StringComparer.Ordinal);
    public string loadedFile = "";

    public void Clear() => table.Clear();

    /// <summary>Returns the string for the key; unknown keys return an empty string (like the original library).</summary>
    public string GetString(string key)
    {
        if (key == null) return "";
        return table.TryGetValue(key, out var v) ? v : "";
    }

    public bool HasKey(string key) => key != null && table.ContainsKey(key);

    /// <summary>The tables store line breaks as the two characters backslash + n.</summary>
    private static string UnescapeControlSequences(string s)
    {
        if (string.IsNullOrEmpty(s) || s.IndexOf('\\') < 0) return s ?? "";
        return s.Replace("\\n", "\n").Replace("\\t", "\t");
    }

    public void Load(string fileName)
    {
        table.Clear();
        loadedFile = fileName ?? "";
        if (string.IsNullOrEmpty(fileName)) return;
        var doc = new TiXmlDocument(GetPath(fileName));
        if (!doc.LoadFile())
        {
            Log("ecStringTable: cannot load " + fileName);
            return;
        }
        TiXmlElement dict = null;
        var plist = doc.FirstChildElement("plist");
        if (plist != null) dict = plist.FirstChildElement("dict");
        dict ??= doc.FirstChildElement("dict");
        if (dict == null)
        {
            // some tables may be a flat list of <key>/<string> pairs
            dict = doc.RootElement();
            if (dict == null) return;
        }
        string pendingKey = null;
        for (var e = dict.FirstChildElement(); e != null; e = e.NextSiblingElement())
        {
            string tag = e.Value();
            if (tag == "key") pendingKey = e.GetText() ?? "";
            else if (tag == "string" && pendingKey != null)
            {
                table[pendingKey] = UnescapeControlSequences(e.GetText());
                pendingKey = null;
            }
        }
    }
}

/// <summary>Global string tables (ecStringTable.h externs).</summary>
public static class StringTableGlobals
{
    public static readonly ecStringTable g_StringTable = new();
    public static readonly ecStringTable g_LocalizableStrings = new();
}
