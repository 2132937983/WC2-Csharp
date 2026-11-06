namespace WC2;

/// <summary>
/// A texture atlas table: loads "&lt;Texture name=.../&gt;&lt;Images&gt;&lt;Image .../&gt;" XML files and
/// resolves image names to <see cref="ecImageAttr"/> entries (logical units).
/// </summary>
public sealed class ecTextureRes
{
    public readonly Dictionary<string, ecTexture> texture = new(StringComparer.OrdinalIgnoreCase);
    public readonly Dictionary<string, ecImageAttr> image = new(StringComparer.OrdinalIgnoreCase);

    private sealed class ResEntry
    {
        public ecTexture texture;
        public readonly List<string> images = new();
    }

    private readonly Dictionary<string, ResEntry> resources = new(StringComparer.OrdinalIgnoreCase);

    public ecTextureRes() { }

    /// <summary>
    /// Loads an atlas XML. When <paramref name="hd"/> is true the coordinates are divided by two
    /// (the texture is 2x art) so that every image is expressed in logical units.
    /// </summary>
    public bool LoadRes(string name, bool hd)
    {
        if (string.IsNullOrEmpty(name)) return false;
        if (resources.ContainsKey(name)) return true;
        string path = AssetSystem.Resolve(name);
        if (path == null) return false;
        var doc = new TiXmlDocument(path);
        if (!doc.LoadFile()) return false;

        var texElement = doc.FirstChildElement("Texture");
        string texName = texElement?.Attribute("name");
        if (string.IsNullOrEmpty(texName))
        {
            // fall back to a texture named like the xml
            texName = WithExt(name, ".png");
        }
        var graphics = ecGraphics.Instance();
        ecTexture tex = graphics.LoadTexture(texName);
        if (tex == null)
        {
            Log("ecTextureRes.LoadRes: texture missing for " + name + " (" + texName + ")");
            return false;
        }
        if (hd) tex.SetHd(true);
        float scale = tex.xC;

        var entry = new ResEntry { texture = tex };
        texture[name] = tex;
        var images = doc.FirstChildElement("Images");
        if (images != null)
        {
            for (var img = images.FirstChildElement("Image"); img != null; img = img.NextSiblingElement("Image"))
            {
                string imgName = img.Attribute("name");
                if (string.IsNullOrEmpty(imgName)) continue;
                // the original CreateImage keeps the first definition of a name
                if (image.ContainsKey(imgName)) continue;
                var attr = new ecImageAttr
                {
                    texture = tex,
                    name = imgName,
                    x = img.FloatAttribute("x") / scale,
                    y = img.FloatAttribute("y") / scale,
                    w = img.FloatAttribute("w") / scale,
                    h = img.FloatAttribute("h") / scale,
                    refx = img.FloatAttribute("refx") / scale,
                    refy = img.FloatAttribute("refy") / scale,
                };
                image[imgName] = attr;
                entry.images.Add(imgName);
            }
        }
        resources[name] = entry;
        return true;
    }

    /// <summary>Removes every image of the atlas and releases its texture.</summary>
    public void UnloadRes(string name)
    {
        if (string.IsNullOrEmpty(name) || !resources.TryGetValue(name, out var entry)) return;
        foreach (var imgName in entry.images)
        {
            if (image.TryGetValue(imgName, out var attr) && ReferenceEquals(attr.texture, entry.texture))
                image.Remove(imgName);
        }
        texture.Remove(name);
        resources.Remove(name);
        ecGraphics.Instance().FreeTexture(entry.texture);
    }

    public bool IsResLoaded(string name) => resources.ContainsKey(name);

    public void ReleaseTexture(ecTexture tex)
    {
        if (tex == null) return;
        string key = null;
        foreach (var kv in resources) if (ReferenceEquals(kv.Value.texture, tex)) { key = kv.Key; break; }
        if (key != null) UnloadRes(key);
        else ecGraphics.Instance().FreeTexture(tex);
    }

    public void Release()
    {
        foreach (var key in resources.Keys.ToList()) UnloadRes(key);
        foreach (var tex in texture.Values.ToList()) ecGraphics.Instance().FreeTexture(tex);
        texture.Clear();
        image.Clear();
        resources.Clear();
    }

    public ecTexture GetTexture(string name) => name != null && texture.TryGetValue(name, out var t) ? t : null;

    /// <summary>Returns the image attribute or null when the name is unknown.</summary>
    public ecImageAttr GetImage(string name) => name != null && image.TryGetValue(name, out var a) ? a : null;

    public bool HasImage(string name) => name != null && image.ContainsKey(name);

    public ecTexture CreateTexture(string name, bool pvr, bool pkm, bool webp)
    {
        var tex = ecGraphics.Instance().LoadTexture(name);
        if (tex != null) texture[name] = tex;
        return tex;
    }

    public ecImageAttr CreateImage(string name, string textureName, float x, float y, float w, float h, float refx, float refy)
    {
        var tex = GetTexture(textureName) ?? CreateTexture(textureName, false, false, false);
        if (tex == null) return null;
        var attr = new ecImageAttr { texture = tex, name = name, x = x, y = y, w = w, h = h, refx = refx, refy = refy };
        image[name] = attr;
        return attr;
    }

    public void CreateImage(string name, ecTexture tex, float x, float y, float w, float h, float refx, float refy)
    {
        if (tex == null) return;
        image[name] = new ecImageAttr { texture = tex, name = name, x = x, y = y, w = w, h = h, refx = refx, refy = refy };
    }

    /// <summary>
    /// printf style lookup: GetImageByFormat("flag_%s.png", country) returns a new ecImage,
    /// or null when the formatted name is not in the atlas.
    /// </summary>
    public ecImage GetImageByFormat(string format, params object[] args)
    {
        string name = args == null || args.Length == 0 ? format : Sprintf(format, args);
        var attr = GetImage(name);
        return attr == null ? null : new ecImage(attr);
    }

    /// <summary>Returns a new ecImage for the name or null (convenience used by ported code).</summary>
    public ecImage NewImage(string name)
    {
        var attr = GetImage(name);
        return attr == null ? null : new ecImage(attr);
    }
}
