using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace WC2;

/// <summary>
/// A small TinyXML compatible facade over System.Xml.Linq so that the ported XML code keeps
/// its original shape (FirstChildElement / NextSiblingElement / Attribute / QueryIntAttribute).
/// The game data files are not always well formed XML (several top level elements, missing
/// spaces between attributes, bare ampersands); the loader is tolerant of those cases.
/// </summary>
public static class TiXmlConst
{
    public const int TIXML_SUCCESS = 0;
    public const int TIXML_NO_ATTRIBUTE = 1;
    public const int TIXML_WRONG_TYPE = 2;
    public const int TIXML_ENCODING_UNKNOWN = 0;
    public const int TIXML_ENCODING_UTF8 = 1;
    public const int TIXML_ENCODING_LEGACY = 2;
}

/// <summary>Base of every node (document, element, text, declaration).</summary>
public class TiXmlNode
{
    internal XNode xnode;
    internal XContainer Container => xnode as XContainer;

    internal TiXmlNode() { }
    internal TiXmlNode(XNode node) { xnode = node; }

    internal static TiXmlNode Wrap(XNode node)
    {
        switch (node)
        {
            case null: return null;
            case XElement e: return new TiXmlElement(e);
            case XText t: return new TiXmlText(t);
            case XDocument d: return new TiXmlDocument(d);
            default: return new TiXmlNode(node);
        }
    }

    public virtual string Value()
    {
        switch (xnode)
        {
            case XElement e: return e.Name.LocalName;
            case XText t: return t.Value;
            default: return "";
        }
    }

    public TiXmlNode FirstChild()
    {
        var c = Container;
        return c == null ? null : Wrap(c.FirstNode);
    }

    public TiXmlNode FirstChild(string name)
    {
        var c = Container;
        if (c == null) return null;
        var e = c.Elements().FirstOrDefault(x => x.Name.LocalName == name);
        return e == null ? null : new TiXmlElement(e);
    }

    public TiXmlElement FirstChildElement()
    {
        var c = Container;
        if (c == null) return null;
        var e = c.Elements().FirstOrDefault();
        return e == null ? null : new TiXmlElement(e);
    }

    public TiXmlElement FirstChildElement(string name)
    {
        var c = Container;
        if (c == null) return null;
        var e = c.Elements().FirstOrDefault(x => x.Name.LocalName == name);
        return e == null ? null : new TiXmlElement(e);
    }

    public TiXmlNode NextSibling()
    {
        var n = xnode?.NextNode;
        return n == null ? null : Wrap(n);
    }

    public TiXmlNode NextSibling(string name)
    {
        if (xnode == null) return null;
        var e = xnode.ElementsAfterSelf().FirstOrDefault(x => x.Name.LocalName == name);
        return e == null ? null : new TiXmlElement(e);
    }

    public TiXmlElement NextSiblingElement()
    {
        if (xnode == null) return null;
        var e = xnode.ElementsAfterSelf().FirstOrDefault();
        return e == null ? null : new TiXmlElement(e);
    }

    public TiXmlElement NextSiblingElement(string name)
    {
        if (xnode == null) return null;
        var e = xnode.ElementsAfterSelf().FirstOrDefault(x => x.Name.LocalName == name);
        return e == null ? null : new TiXmlElement(e);
    }

    public TiXmlNode Parent()
    {
        var p = xnode?.Parent;
        return p == null ? null : new TiXmlElement(p);
    }

    public TiXmlElement ToElement() => this as TiXmlElement;
    public TiXmlDocument ToDocument() => this as TiXmlDocument;
    public TiXmlText ToText() => this as TiXmlText;

    public bool NoChildren() => Container == null || !Container.Nodes().Any();

    /// <summary>Appends a child node and returns it (TinyXML keeps ownership semantics).</summary>
    public TiXmlNode LinkEndChild(TiXmlNode child)
    {
        var c = Container;
        if (c == null || child?.xnode == null) return child;
        if (child.xnode.Parent != null || child.xnode.Document != null && child.xnode.Document.Root == child.xnode)
            child.xnode.Remove();
        c.Add(child.xnode);
        return child;
    }

    public TiXmlNode InsertEndChild(TiXmlNode child) => LinkEndChild(child);

    public void Clear()
    {
        Container?.RemoveNodes();
    }

    public void RemoveChild(TiXmlNode child) => child?.xnode?.Remove();

    public IEnumerable<TiXmlElement> ChildElements()
    {
        var c = Container;
        if (c == null) yield break;
        foreach (var e in c.Elements()) yield return new TiXmlElement(e);
    }

    public IEnumerable<TiXmlElement> ChildElements(string name)
    {
        var c = Container;
        if (c == null) yield break;
        foreach (var e in c.Elements().Where(x => x.Name.LocalName == name)) yield return new TiXmlElement(e);
    }

    public override bool Equals(object obj) => obj is TiXmlNode n && ReferenceEquals(n.xnode, xnode);
    public override int GetHashCode() => xnode?.GetHashCode() ?? 0;
}

public sealed class TiXmlText : TiXmlNode
{
    internal TiXmlText(XText t) : base(t) { }
    public TiXmlText(string text) : base(new XText(text ?? "")) { }
}

public sealed class TiXmlDeclaration : TiXmlNode
{
    public string version, encoding, standalone;
    public TiXmlDeclaration(string version, string encoding, string standalone)
    {
        this.version = version; this.encoding = encoding; this.standalone = standalone;
    }
}

public sealed class TiXmlElement : TiXmlNode
{
    internal XElement Element => (XElement)xnode;

    internal TiXmlElement(XElement e) : base(e) { }
    public TiXmlElement(string name) : base(new XElement(SafeName(name))) { }

    private static string SafeName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "node";
        try { return XmlConvert.VerifyName(name); } catch { return XmlConvert.EncodeLocalName(name); }
    }

    /// <summary>Returns the attribute value, or null when it does not exist (TinyXML semantics).</summary>
    public string Attribute(string name)
    {
        var a = Element.Attributes().FirstOrDefault(x => x.Name.LocalName == name);
        return a?.Value;
    }

    public bool HasAttribute(string name) => Element.Attributes().Any(x => x.Name.LocalName == name);

    /// <summary>TinyXML's Attribute(name, int*) overload: returns the string and fills value.</summary>
    public string Attribute(string name, out int value)
    {
        string s = Attribute(name);
        value = 0;
        if (s != null) TryParseInt(s, out value);
        return s;
    }

    public string Attribute(string name, out double value)
    {
        string s = Attribute(name);
        value = 0;
        if (s != null && TryParseFloat(s, out float f)) value = f;
        return s;
    }

    public int QueryIntAttribute(string name, out int value)
    {
        value = 0;
        string s = Attribute(name);
        if (s == null) return TiXmlConst.TIXML_NO_ATTRIBUTE;
        if (TryParseInt(s, out value)) return TiXmlConst.TIXML_SUCCESS;
        // strtol semantics: accept a numeric prefix ("100 ")
        if (TryParseFloat(s, out float f)) { value = (int)f; return TiXmlConst.TIXML_SUCCESS; }
        return TiXmlConst.TIXML_WRONG_TYPE;
    }

    public int QueryFloatAttribute(string name, out float value)
    {
        value = 0;
        string s = Attribute(name);
        if (s == null) return TiXmlConst.TIXML_NO_ATTRIBUTE;
        return TryParseFloat(s, out value) ? TiXmlConst.TIXML_SUCCESS : TiXmlConst.TIXML_WRONG_TYPE;
    }

    public int QueryDoubleAttribute(string name, out double value)
    {
        int r = QueryFloatAttribute(name, out float f);
        value = f;
        return r;
    }

    public int QueryBoolAttribute(string name, out bool value)
    {
        value = false;
        string s = Attribute(name);
        if (s == null) return TiXmlConst.TIXML_NO_ATTRIBUTE;
        s = s.Trim().ToLowerInvariant();
        if (s == "true" || s == "yes" || s == "1") { value = true; return TiXmlConst.TIXML_SUCCESS; }
        if (s == "false" || s == "no" || s == "0") { value = false; return TiXmlConst.TIXML_SUCCESS; }
        return TiXmlConst.TIXML_WRONG_TYPE;
    }

    /// <summary>Convenience: integer attribute with default.</summary>
    public int IntAttribute(string name, int defaultValue = 0)
        => QueryIntAttribute(name, out int v) == TiXmlConst.TIXML_SUCCESS ? v : defaultValue;

    public float FloatAttribute(string name, float defaultValue = 0f)
        => QueryFloatAttribute(name, out float v) == TiXmlConst.TIXML_SUCCESS ? v : defaultValue;

    public string StringAttribute(string name, string defaultValue = "") => Attribute(name) ?? defaultValue;

    public void SetAttribute(string name, string value) => Element.SetAttributeValue(SafeName(name), value ?? "");
    public void SetAttribute(string name, int value) => Element.SetAttributeValue(SafeName(name), value.ToString(CultureInfo.InvariantCulture));
    public void SetDoubleAttribute(string name, double value) => Element.SetAttributeValue(SafeName(name), value.ToString("0.######", CultureInfo.InvariantCulture));
    public void SetAttribute(string name, float value) => SetDoubleAttribute(name, value);
    public void RemoveAttribute(string name) => Element.Attributes().FirstOrDefault(x => x.Name.LocalName == name)?.Remove();

    /// <summary>Text of the first text child, or null.</summary>
    public string GetText()
    {
        var t = Element.Nodes().OfType<XText>().FirstOrDefault();
        return t?.Value;
    }

    public override string Value() => Element.Name.LocalName;

    public IEnumerable<KeyValuePair<string, string>> Attributes()
    {
        foreach (var a in Element.Attributes()) yield return new KeyValuePair<string, string>(a.Name.LocalName, a.Value);
    }
}

public sealed class TiXmlDocument : TiXmlNode
{
    private string fileName;
    private string errorDesc = "";
    private bool error;

    /// <summary>Name of the synthetic wrapper element used to tolerate multiple roots.</summary>
    private const string WrapperName = "__tixml_root__";

    public TiXmlDocument()
    {
        xnode = new XDocument(new XElement(WrapperName));
    }

    public TiXmlDocument(string documentPath) : this()
    {
        fileName = documentPath;
    }

    internal TiXmlDocument(XDocument doc) : base(doc) { }

    private XElement Wrapper => ((XDocument)xnode).Root;
    internal new XContainer Container => Wrapper;

    public bool Error() => error;
    public string ErrorDesc() => errorDesc;

    public bool LoadFile(int encoding = TiXmlConst.TIXML_ENCODING_UNKNOWN) => LoadFile(fileName, encoding);

    public bool LoadFile(string path, int encoding = TiXmlConst.TIXML_ENCODING_UNKNOWN)
    {
        fileName = path;
        error = false; errorDesc = "";
        try
        {
            // rooted = a real file (documents folder), relative = an asset
            string text = string.IsNullOrEmpty(path) ? null
                : Path.IsPathRooted(path) ? (File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : null)
                : AssetSystem.ReadAllText(path);
            if (text == null)
            {
                error = true; errorDesc = "File not found: " + path;
                return false;
            }
            return Parse(text);
        }
        catch (Exception ex)
        {
            error = true; errorDesc = ex.Message;
            Log("TiXmlDocument.LoadFile failed for " + path + ": " + ex.Message);
            return false;
        }
    }

    /// <summary>Parses XML text. Returns true on success.</summary>
    public bool Parse(string text)
    {
        error = false; errorDesc = "";
        try
        {
            string body = Sanitize(text);
            XElement root;
            try
            {
                root = XElement.Parse("<" + WrapperName + ">" + body + "</" + WrapperName + ">", LoadOptions.None);
            }
            catch (XmlException)
            {
                // second chance: fix missing spaces between attributes and bare ampersands
                string repaired = RepairLoose(body);
                root = XElement.Parse("<" + WrapperName + ">" + repaired + "</" + WrapperName + ">", LoadOptions.None);
            }
            xnode = new XDocument(root);
            return true;
        }
        catch (Exception ex)
        {
            error = true; errorDesc = ex.Message;
            Log("TiXml parse error: " + ex.Message);
            return false;
        }
    }

    private static readonly Regex DeclRegex = new Regex(@"<\?xml[^>]*\?>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DoctypeRegex = new Regex(@"<!DOCTYPE[^>\[]*(\[[^\]]*\])?[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex MissingSpaceRegex = new Regex("\"(?=[A-Za-z_][A-Za-z0-9_\\-:.]*\\s*=)", RegexOptions.Compiled);
    private static readonly Regex BareAmpRegex = new Regex(@"&(?!(amp|lt|gt|quot|apos|#\d+|#x[0-9a-fA-F]+);)", RegexOptions.Compiled);

    private static string Sanitize(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        if (text.Length > 0 && text[0] == '﻿') text = text.Substring(1);
        text = DeclRegex.Replace(text, "");
        text = DoctypeRegex.Replace(text, "");
        // strip control characters that are illegal in XML 1.0
        var sb = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            if (c == '\t' || c == '\n' || c == '\r' || c >= 0x20) sb.Append(c);
        }
        return sb.ToString();
    }

    private static string RepairLoose(string text)
    {
        text = MissingSpaceRegex.Replace(text, "\" ");
        text = BareAmpRegex.Replace(text, "&amp;");
        return text;
    }

    public bool SaveFile() => SaveFile(fileName);

    public bool SaveFile(string path)
    {
        try
        {
            if (string.IsNullOrEmpty(path)) return false;
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "    ",
                OmitXmlDeclaration = true,
                Encoding = new UTF8Encoding(false),
                NewLineChars = "\n",
            };
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
            foreach (var node in Wrapper.Nodes())
            {
                if (node is XElement e)
                {
                    using var sw = new StringWriter();
                    using (var xw = XmlWriter.Create(sw, settings)) e.Save(xw);
                    sb.Append(sw.ToString()).Append('\n');
                }
            }
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            return true;
        }
        catch (Exception ex)
        {
            Log("TiXmlDocument.SaveFile failed for " + path + ": " + ex.Message);
            return false;
        }
    }

    /// <summary>The first top level element (TinyXML's RootElement()).</summary>
    public TiXmlElement RootElement()
    {
        var e = Wrapper.Elements().FirstOrDefault();
        return e == null ? null : new TiXmlElement(e);
    }

    public new TiXmlNode FirstChild()
    {
        var n = Wrapper.FirstNode;
        return n == null ? null : Wrap(n);
    }

    public new TiXmlNode FirstChild(string name)
    {
        var e = Wrapper.Elements().FirstOrDefault(x => x.Name.LocalName == name);
        return e == null ? null : new TiXmlElement(e);
    }

    public new TiXmlElement FirstChildElement()
    {
        var e = Wrapper.Elements().FirstOrDefault();
        return e == null ? null : new TiXmlElement(e);
    }

    public new TiXmlElement FirstChildElement(string name)
    {
        var e = Wrapper.Elements().FirstOrDefault(x => x.Name.LocalName == name);
        return e == null ? null : new TiXmlElement(e);
    }

    public new TiXmlNode LinkEndChild(TiXmlNode child)
    {
        if (child == null) return null;
        if (child is TiXmlDeclaration) return child; // the declaration is always written by SaveFile
        if (child.xnode == null) return child;
        if (child.xnode.Parent != null) child.xnode.Remove();
        Wrapper.Add(child.xnode);
        return child;
    }

    public new TiXmlNode InsertEndChild(TiXmlNode child) => LinkEndChild(child);

    public new void Clear() => Wrapper.RemoveNodes();

    public new IEnumerable<TiXmlElement> ChildElements()
    {
        foreach (var e in Wrapper.Elements()) yield return new TiXmlElement(e);
    }

    public new IEnumerable<TiXmlElement> ChildElements(string name)
    {
        foreach (var e in Wrapper.Elements().Where(x => x.Name.LocalName == name)) yield return new TiXmlElement(e);
    }

    /// <summary>Loads a document from the assets folder (GetPath) and returns it, or null on failure.</summary>
    public static TiXmlDocument LoadAsset(string assetName)
    {
        var doc = new TiXmlDocument(GetPath(assetName));
        return doc.LoadFile() ? doc : null;
    }
}
