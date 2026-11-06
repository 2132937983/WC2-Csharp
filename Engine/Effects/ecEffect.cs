using System.Globalization;

namespace WC2;

/// <summary>
/// One emitter definition of an effect XML (port of the original ecEmitterAttr): spawn shape,
/// picture, random ranges, an emission rate track (up to 8 keys) and a life track (up to 8 keys)
/// with per key flags telling which properties the key animates.
/// </summary>
public sealed class ecEmitterAttr
{
    public float offsetX, offsetY;
    /// <summary>0 = emit once for "life" seconds, 1 = keep emitting.</summary>
    public int settingsMode;
    /// <summary>0 = point, 1 = line, 2 = area, 3 = ellipse.</summary>
    public int settingsType;
    public float widthOrLenOrR, height;
    /// <summary>1 = the particle is rotated along its movement direction ("regular").</summary>
    public float rotAngleType;
    public float rotAngleMin, rotAngleMax;
    public ecImage image;
    public float imageWidth, imageHeight;
    public float life;
    public float lifeMin, lifeMax;
    public float angleMin, angleMax;
    public float speedMin, speedMax;
    public float gravityMin, gravityMax;
    public float scaleMin, scaleMax;
    public float rotSpeedMin, rotSpeedMax;
    public byte rMin, gMin, bMin, aMin, rMax, gMax, bMax, aMax;
    public int nTimeTrack;
    public readonly float[] timeTrackTime = new float[8];
    public readonly byte[] timeTrackQuantity = new byte[8];
    public int nLifeTrack;
    public readonly byte[] lifeTrackFlag = new byte[8];
    public readonly float[] lifeTrackLife = new float[8];
    public readonly float[] lifeTrackSpeed = new float[8];
    public readonly float[] lifeTrackGravity = new float[8];
    public readonly float[] lifeTrackScale = new float[8];
    public readonly float[] lifeTrackRotSpeed = new float[8];
    public readonly float[] lifeTrackR = new float[8];
    public readonly float[] lifeTrackG = new float[8];
    public readonly float[] lifeTrackB = new float[8];
    public readonly float[] lifeTrackA = new float[8];
}

/// <summary>An effect definition: up to four emitters loaded from "effect_*.xml".</summary>
public sealed class ecEffectRes
{
    public string name = "";
    public readonly List<ecEmitterAttr> emitterAttr = new();
    public int nRes => emitterAttr.Count;

    private static bool Query(TiXmlElement e, string attrName, out float value)
    {
        value = 0f;
        string s = e?.Attribute(attrName);
        return s != null && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static bool QueryInt(TiXmlElement e, string attrName, out int value)
    {
        value = 0;
        string s = e?.Attribute(attrName);
        return s != null && int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static float Rad(float deg) => (float)(deg * Math.PI / 180.0);

    public void Release() => emitterAttr.Clear();

    /// <summary>Parses the effect file. Pictures are taken from the effect texture atlas.</summary>
    public void LoadEffect(string fileName, ecTextureRes textureEffect)
    {
        Release();
        name = fileName ?? "";
        var doc = new TiXmlDocument(GetPath(fileName));
        if (!doc.LoadFile()) { Log("Effect missing: " + fileName); return; }
        var root = doc.FirstChildElement("effect");
        if (root == null) return;
        for (var em = root.FirstChildElement(); em != null; em = em.NextSiblingElement())
        {
            var a = new ecEmitterAttr();
            if (Query(em, "life", out float f)) a.life = f;
            if (Query(em, "offsetx", out f)) a.offsetX = f;
            if (Query(em, "offsety", out f)) a.offsetY = f;
            for (var p = em.FirstChildElement(); p != null; p = p.NextSiblingElement())
            {
                string pname = p.Attribute("name") ?? "";
                switch (pname)
                {
                    case "settings":
                        a.settingsMode = (p.Attribute("mode") ?? "") != "once" ? 1 : 0;
                        string type = p.Attribute("type") ?? "";
                        if (type == "line") { a.settingsType = 1; if (Query(p, "len", out f)) a.widthOrLenOrR = f; }
                        else if (type == "area")
                        {
                            a.settingsType = 2;
                            if (Query(p, "width", out f)) a.widthOrLenOrR = f;
                            if (Query(p, "height", out f)) a.height = f;
                        }
                        else if (type == "ellipse") { a.settingsType = 3; if (Query(p, "r", out f)) a.widthOrLenOrR = f; }
                        else a.settingsType = 0;
                        break;
                    case "image":
                        string file = p.Attribute("file");
                        if (file != null)
                        {
                            var attr = textureEffect?.GetImage(file);
                            if (attr != null)
                            {
                                a.image = new ecImage(attr);
                                if ((p.Attribute("blend") ?? "") == "add") a.image.blendMode = 1;
                            }
                            else Log("Effect image not found: " + file + " (" + fileName + ")");
                        }
                        if (Query(p, "width", out f)) a.imageWidth = f;
                        if (Query(p, "height", out f)) a.imageHeight = f;
                        break;
                    case "life":
                        if (Query(p, "min", out f)) a.lifeMin = f;
                        if (Query(p, "max", out f)) a.lifeMax = f;
                        break;
                    case "angle":
                        if (Query(p, "min", out f)) a.angleMin = Rad(f);
                        if (Query(p, "max", out f)) a.angleMax = Rad(f);
                        break;
                    case "rotangle":
                        if ((p.Attribute("type") ?? "") == "regular") a.rotAngleType = 1f;
                        else
                        {
                            a.rotAngleType = 0f;
                            if (Query(p, "min", out f)) a.rotAngleMin = Rad(f);
                            if (Query(p, "max", out f)) a.rotAngleMax = Rad(f);
                        }
                        break;
                    case "speed":
                        if (Query(p, "min", out f)) a.speedMin = f;
                        if (Query(p, "max", out f)) a.speedMax = f;
                        break;
                    case "gravity":
                        if (Query(p, "min", out f)) a.gravityMin = f;
                        if (Query(p, "max", out f)) a.gravityMax = f;
                        break;
                    case "scale":
                        if (Query(p, "min", out f)) a.scaleMin = f;
                        if (Query(p, "max", out f)) a.scaleMax = f;
                        break;
                    case "rotspeed":
                        if (Query(p, "min", out f)) a.rotSpeedMin = f;
                        if (Query(p, "max", out f)) a.rotSpeedMax = f;
                        break;
                    case "r":
                        if (QueryInt(p, "min", out int iv)) a.rMin = (byte)iv;
                        if (QueryInt(p, "max", out iv)) a.rMax = (byte)iv;
                        break;
                    case "g":
                        if (QueryInt(p, "min", out iv)) a.gMin = (byte)iv;
                        if (QueryInt(p, "max", out iv)) a.gMax = (byte)iv;
                        break;
                    case "b":
                        if (QueryInt(p, "min", out iv)) a.bMin = (byte)iv;
                        if (QueryInt(p, "max", out iv)) a.bMax = (byte)iv;
                        break;
                    case "a":
                        if (QueryInt(p, "min", out iv)) a.aMin = (byte)iv;
                        if (QueryInt(p, "max", out iv)) a.aMax = (byte)iv;
                        break;
                    case "timetrack":
                    {
                        int j = 0;
                        for (var t = p.FirstChildElement(); t != null && j < 8; t = t.NextSiblingElement())
                        {
                            if (Query(t, "time", out f)) a.timeTrackTime[j] = f;
                            if (QueryInt(t, "quantity", out iv)) a.timeTrackQuantity[j] = (byte)iv;
                            j++;
                        }
                        a.nTimeTrack = j;
                        break;
                    }
                    case "lifetrack":
                    {
                        int k = 0;
                        for (var t = p.FirstChildElement(); t != null && k < 8; t = t.NextSiblingElement())
                        {
                            if (!Query(t, "life", out f)) continue;
                            a.lifeTrackLife[k] = f;
                            if (Query(t, "speed", out f)) { a.lifeTrackSpeed[k] = f; a.lifeTrackFlag[k] |= 1; }
                            if (Query(t, "gravity", out f)) { a.lifeTrackGravity[k] = f; a.lifeTrackFlag[k] |= 2; }
                            if (Query(t, "scale", out f)) { a.lifeTrackScale[k] = f; a.lifeTrackFlag[k] |= 4; }
                            if (Query(t, "rotspeed", out f)) { a.lifeTrackRotSpeed[k] = f; a.lifeTrackFlag[k] |= 8; }
                            if (Query(t, "r", out f)) { a.lifeTrackR[k] = f; a.lifeTrackFlag[k] |= 16; }
                            if (Query(t, "g", out f)) { a.lifeTrackG[k] = f; a.lifeTrackFlag[k] |= 16; }
                            if (Query(t, "b", out f)) { a.lifeTrackB[k] = f; a.lifeTrackFlag[k] |= 16; }
                            if (Query(t, "a", out f)) { a.lifeTrackA[k] = f; a.lifeTrackFlag[k] |= 32; }
                            k++;
                        }
                        a.nLifeTrack = k;
                        break;
                    }
                }
            }
            emitterAttr.Add(a);
            if (emitterAttr.Count >= 4) break;
        }
    }
}

/// <summary>Owns the effect texture atlas and caches the effect definitions by file name.</summary>
public sealed class ecEffectResManager
{
    private static ecEffectResManager s_instance;
    public static ecEffectResManager Instance() => s_instance ??= new ecEffectResManager();

    public readonly ecTextureRes textureRes = new();
    public readonly Dictionary<string, ecEffectRes> effectRes = new(StringComparer.OrdinalIgnoreCase);

    private ecEffectResManager() { }

    /// <summary>Loads the effect atlas (the @2x texture, when present, already yields logical coordinates).</summary>
    public void LoadTextureRes(string name) => textureRes.LoadRes(name, false);
    public void LoadTextureRes(string name, bool hd) => textureRes.LoadRes(name, hd);
    public void ReleaseTextureRes() => textureRes.Release();

    public ecEffectRes LoadEffectRes(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (effectRes.TryGetValue(name, out var res)) return res;
        res = new ecEffectRes();
        res.LoadEffect(name, textureRes);
        if (res.nRes == 0) { Log("Effect has no emitters: " + name); return null; }
        effectRes[name] = res;
        return res;
    }

    public void ReleaseEffectRes(string name)
    {
        if (name != null && effectRes.Remove(name, out var res)) res.Release();
    }

    public void ReleaseEffectRes(ecEffectRes res)
    {
        if (res == null) return;
        ReleaseEffectRes(res.name);
    }

    public void ReleaseAllEffectRes()
    {
        foreach (var r in effectRes.Values) r.Release();
        effectRes.Clear();
    }
}

/// <summary>
/// One emitter instance (port of the original ecParticleSystem, 100 particles). Speeds are in
/// logical units per second, the emission rate follows the time track and every particle
/// animates its properties along the life track.
/// </summary>
public sealed class ecParticleSystem
{
    private sealed class Particle
    {
        public float x, y;
        public float dirCos, dirSin;          // velocity
        public float timeRate, timeRateAcc;   // speed multiplier and its rate
        public float timeStart, time, timeRateG; // gravity base, current gravity, gravity rate
        public float flipRatioStart, flipRatio, flipRatioTimeRate; // scale base, current scale, rate
        public float theta, thetaTimeRateStart, thetaTimeRate, thetaTimeRateAcc;
        public byte r, g, b, a;
        public float rRatio = 1f, gRatio = 1f, bRatio = 1f, aRatio = 1f;
        public float rRatioRate, gRatioRate, bRatioRate, aRatioRate;
        public float age, timeLimit;
        public int startIndex;

        public void CopyFrom(Particle o)
        {
            x = o.x; y = o.y; dirCos = o.dirCos; dirSin = o.dirSin; timeRate = o.timeRate; timeRateAcc = o.timeRateAcc;
            timeStart = o.timeStart; time = o.time; timeRateG = o.timeRateG;
            flipRatioStart = o.flipRatioStart; flipRatio = o.flipRatio; flipRatioTimeRate = o.flipRatioTimeRate;
            theta = o.theta; thetaTimeRateStart = o.thetaTimeRateStart; thetaTimeRate = o.thetaTimeRate; thetaTimeRateAcc = o.thetaTimeRateAcc;
            r = o.r; g = o.g; b = o.b; a = o.a;
            rRatio = o.rRatio; gRatio = o.gRatio; bRatio = o.bRatio; aRatio = o.aRatio;
            rRatioRate = o.rRatioRate; gRatioRate = o.gRatioRate; bRatioRate = o.bRatioRate; aRatioRate = o.aRatioRate;
            age = o.age; timeLimit = o.timeLimit; startIndex = o.startIndex;
        }
    }

    public const int MaxParticles = 100;
    public ecEmitterAttr emitterAttr;
    public bool live;
    public float fireTime;
    public float time;
    public float timeRate;
    public float timeDecimalPart;
    public float lastX, lastY, currentX, currentY;
    /// <summary>Angle offset handed to FireAt.</summary>
    public float angleOffset;
    public int liveCount;
    public int count;
    public float flipX = 1f, flipY = 1f;
    private readonly Particle[] particle = new Particle[MaxParticles];

    // the original uses the classic LCG with the high word as the random value
    private static uint s_seed = 1;
    private static float Rand01() { s_seed = unchecked(214013u * s_seed + 2531011u); return ((s_seed >> 16) & 0xFFFF) * 0.000015259022f; }
    private static int RandInt(int range) { s_seed = unchecked(214013u * s_seed + 2531011u); uint v = s_seed ^ (s_seed >> 15); return range <= 0 ? 0 : (int)(v % (uint)range); }

    public ecParticleSystem()
    {
        for (int i = 0; i < MaxParticles; i++) particle[i] = new Particle();
    }

    public void Init(ecEmitterAttr attr)
    {
        emitterAttr = attr;
        var img = attr.image;
        flipX = img != null && img.textureRect.w > 0f ? attr.imageWidth / img.textureRect.w : 1f;
        flipY = img != null && img.textureRect.h > 0f ? attr.imageHeight / img.textureRect.h : 1f;
        liveCount = 0;
        live = false;
        count = 0;
        lastX = lastY = currentX = currentY = 0f;
        fireTime = 0f;
        time = attr.timeTrackQuantity[0];
        timeRate = 0f;
        timeDecimalPart = 0f;
        angleOffset = 0f;
    }

    public void Fire() { live = true; fireTime = 0f; }

    public void Stop(bool killParticles)
    {
        live = false;
        if (killParticles) liveCount = 0;
    }

    public void MoveTo(float x, float y, bool moveParticles)
    {
        x += emitterAttr.offsetX;
        y += emitterAttr.offsetY;
        if (moveParticles)
        {
            float dx = x - currentX, dy = y - currentY;
            for (int i = 0; i < liveCount; i++) { particle[i].x += dx; particle[i].y += dy; }
            lastX += dx; lastY += dy;
            currentX = x; currentY = y;
        }
        else if (live)
        {
            lastX = currentX; lastY = currentY;
            currentX = x; currentY = y;
        }
        else
        {
            lastX = currentX = x;
            lastY = currentY = y;
        }
    }

    public void FireAt(float x, float y, float angleOffsetIn)
    {
        angleOffset = angleOffsetIn;
        Stop(false);
        MoveTo(x, y, false);
        Fire();
    }

    public bool IsLive() => liveCount > 0 || live;

    public void Render()
    {
        var img = emitterAttr?.image;
        if (img == null) return;
        for (int i = 0; i < liveCount; i++)
        {
            var p = particle[i];
            uint r = (uint)Math.Clamp((int)(p.r * p.rRatio), 0, 255);
            uint g = (uint)Math.Clamp((int)(p.g * p.gRatio), 0, 255);
            uint b = (uint)Math.Clamp((int)(p.b * p.bRatio), 0, 255);
            uint a = (uint)Math.Clamp((int)(p.a * p.aRatio), 0, 255);
            img.SetColor((a << 24) | (b << 16) | (g << 8) | r, -1);
            img.RenderEx(p.x, p.y, p.theta, p.flipRatio * flipX, flipY * p.flipRatio);
        }
    }

    public void Update(float dt)
    {
        var attr = emitterAttr;
        if (attr == null) return;
        if (live)
        {
            fireTime += dt;
            time += timeRate * dt;
            if (attr.settingsMode == 0 && fireTime >= attr.life) live = false;
            // follow the emission rate track
            int n = Math.Min(attr.nTimeTrack, 8);
            if (n > 0)
            {
                while (count < n && fireTime >= attr.timeTrackTime[count])
                {
                    if (count >= n - 1) { timeRate = 0f; time = attr.timeTrackQuantity[count]; break; }
                    count++;
                    float diff = attr.timeTrackTime[count] - fireTime;
                    if (diff >= 0f)
                    {
                        timeRate = (attr.timeTrackQuantity[count] - time) / MathF.Max(dt, diff);
                        break;
                    }
                }
            }
        }

        // animate the particles
        for (int i = 0; i < liveCount; i++)
        {
            var p = particle[i];
            float step = p.timeRate * dt;
            p.x += step * p.dirCos;
            p.y += step * p.dirSin;
            p.dirSin += p.time * dt;
            p.time += p.timeRateG * dt;
            p.timeRate += p.timeRateAcc * dt;
            p.flipRatio += p.flipRatioTimeRate * dt;
            p.theta += p.thetaTimeRate * dt;
            p.thetaTimeRate += p.thetaTimeRateAcc * dt;
            p.rRatio += p.rRatioRate * dt;
            p.gRatio += p.gRatioRate * dt;
            p.bRatio += p.bRatioRate * dt;
            p.aRatio += p.aRatioRate * dt;
            p.age += dt;
            if (p.age >= p.timeLimit)
            {
                liveCount--;
                if (i != liveCount) p.CopyFrom(particle[liveCount]);
                i--;
                continue;
            }
            int nLife = Math.Min(attr.nLifeTrack, 8);
            int k = p.startIndex + 1;
            while (k < nLife + 1 && p.startIndex < nLife - 1)
            {
                if (k < nLife && p.age < attr.lifeTrackLife[k] * p.timeLimit) break;
                if (k >= nLife)
                {
                    int last = nLife - 1;
                    p.timeRate = attr.lifeTrackSpeed[last]; p.timeRateAcc = 0f;
                    p.time = attr.lifeTrackGravity[last] * p.timeStart; p.timeRateG = 0f;
                    p.flipRatio = attr.lifeTrackScale[last] * p.flipRatioStart; p.flipRatioTimeRate = 0f;
                    p.thetaTimeRate = attr.lifeTrackRotSpeed[last] * p.thetaTimeRateStart; p.thetaTimeRateAcc = 0f;
                    p.rRatio = attr.lifeTrackR[last]; p.gRatio = attr.lifeTrackG[last]; p.bRatio = attr.lifeTrackB[last]; p.aRatio = attr.lifeTrackA[last];
                    p.rRatioRate = p.gRatioRate = p.bRatioRate = p.aRatioRate = 0f;
                    break;
                }
                p.startIndex = k;
                float remain = attr.lifeTrackLife[k] * p.timeLimit - p.age;
                if (remain >= 0f)
                {
                    float inv = 1f / MathF.Max(dt, remain);
                    byte flag = attr.lifeTrackFlag[k];
                    p.timeRateAcc = (flag & 1) != 0 ? (attr.lifeTrackSpeed[k] - p.timeRate) * inv : 0f;
                    p.timeRateG = (flag & 2) != 0 ? (attr.lifeTrackGravity[k] * p.timeStart - p.time) * inv : 0f;
                    p.flipRatioTimeRate = (flag & 4) != 0 ? (attr.lifeTrackScale[k] * p.flipRatioStart - p.flipRatio) * inv : 0f;
                    p.thetaTimeRateAcc = (flag & 8) != 0 ? (attr.lifeTrackRotSpeed[k] * p.thetaTimeRateStart - p.thetaTimeRate) * inv : 0f;
                    if ((flag & 16) != 0)
                    {
                        p.rRatioRate = (attr.lifeTrackR[k] - p.rRatio) * inv;
                        p.gRatioRate = (attr.lifeTrackG[k] - p.gRatio) * inv;
                        p.bRatioRate = (attr.lifeTrackB[k] - p.bRatio) * inv;
                    }
                    else p.rRatioRate = p.gRatioRate = p.bRatioRate = 0f;
                    p.aRatioRate = (flag & 32) != 0 ? (attr.lifeTrackA[k] - p.aRatio) * inv : 0f;
                    p.rRatio = Math.Clamp(p.rRatio, 0f, 1f);
                    p.gRatio = Math.Clamp(p.gRatio, 0f, 1f);
                    p.bRatio = Math.Clamp(p.bRatio, 0f, 1f);
                    p.aRatio = Math.Clamp(p.aRatio, 0f, 1f);
                }
                k++;
            }
        }

        // emit new particles
        if (!live) return;
        float amount = dt * time + timeDecimalPart;
        int spawn = (int)amount;
        timeDecimalPart = amount - spawn;
        for (int s = 0; s < spawn && liveCount < MaxParticles; s++)
        {
            var p = particle[liveCount];
            p.age = 0f;
            p.startIndex = 0;
            p.timeLimit = attr.lifeMin + Rand01() * (attr.lifeMax - attr.lifeMin);
            if (attr.settingsType == 0)
            {
                float t = Rand01();
                float bx = lastX + (currentX - lastX) * t, by = lastY + (currentY - lastY) * t;
                p.x = bx + (Rand01() * 4f - 2f);
                p.y = by + (Rand01() * 4f - 2f);
            }
            else if (attr.settingsType == 2)
            {
                float t = Rand01();
                float bx = lastX + (currentX - lastX) * t, by = lastY + (currentY - lastY) * t;
                p.x = bx + ((attr.widthOrLenOrR + attr.widthOrLenOrR) * Rand01() - attr.widthOrLenOrR) * 0.5f;
                p.y = by + ((attr.height + attr.height) * Rand01() - attr.height) * 0.5f;
            }
            // (types 1 and 3 keep the slot's previous position, as in the original)
            float angle = attr.angleMin + angleOffset + ((attr.angleMax + angleOffset) - (attr.angleMin + angleOffset)) * Rand01();
            float cos = MathF.Cos(angle), sin = MathF.Sin(angle);
            float speed = attr.speedMin + (attr.speedMax - attr.speedMin) * Rand01();
            p.timeRate = 1f; p.timeRateAcc = 0f;
            p.dirCos = cos * speed; p.dirSin = speed * sin;
            float gravity = attr.gravityMin + Rand01() * (attr.gravityMax - attr.gravityMin);
            p.timeRateG = 0f; p.timeStart = gravity; p.time = gravity;
            float scale = attr.scaleMin + Rand01() * (attr.scaleMax - attr.scaleMin);
            p.flipRatioTimeRate = 0f; p.flipRatioStart = scale; p.flipRatio = scale;
            if (attr.rotAngleType == 1f) p.theta = angle + angleOffset;
            else p.theta = (attr.rotAngleMin + angleOffset) + ((attr.rotAngleMax + angleOffset) - (attr.rotAngleMin + angleOffset)) * Rand01();
            float rot = attr.rotSpeedMin + (attr.rotSpeedMax - attr.rotSpeedMin) * Rand01();
            p.thetaTimeRateAcc = 0f; p.thetaTimeRateStart = rot; p.thetaTimeRate = rot;
            p.r = (byte)(attr.rMin + RandInt(attr.rMax - attr.rMin + 1));
            p.g = (byte)(attr.gMin + RandInt(attr.gMax - attr.gMin + 1));
            p.b = (byte)(attr.bMin + RandInt(attr.bMax - attr.bMin + 1));
            p.a = (byte)(attr.aMin + RandInt(attr.aMax - attr.aMin + 1));
            p.rRatio = p.gRatio = p.bRatio = p.aRatio = 1f;
            p.rRatioRate = p.gRatioRate = p.bRatioRate = p.aRatioRate = 0f;
            if (attr.nLifeTrack > 0)
            {
                p.timeRate = attr.lifeTrackSpeed[0];
                p.time = gravity * attr.lifeTrackGravity[0];
                p.flipRatio = scale * attr.lifeTrackScale[0];
                p.thetaTimeRate = rot * attr.lifeTrackRotSpeed[0];
                p.rRatio = attr.lifeTrackR[0]; p.gRatio = attr.lifeTrackG[0]; p.bRatio = attr.lifeTrackB[0]; p.aRatio = attr.lifeTrackA[0];
            }
            liveCount++;
        }
    }
}

/// <summary>A particle effect: up to four emitters driven together (port of the original ecEffect).</summary>
public sealed class ecEffect
{
    public readonly List<ecParticleSystem> particleSystem = new();
    public ecEffectRes res;
    /// <summary>Managed effects are removed by the manager once they die.</summary>
    public bool autoRemove = true;
    public string name = "";

    public int nParticleSystem => particleSystem.Count;

    public bool Init(string fileName, object pool = null)
    {
        name = fileName ?? "";
        particleSystem.Clear();
        res = ecEffectResManager.Instance().LoadEffectRes(fileName);
        if (res == null) return false;
        foreach (var attr in res.emitterAttr)
        {
            var ps = new ecParticleSystem();
            ps.Init(attr);
            particleSystem.Add(ps);
        }
        return true;
    }

    public void FireAt(float x, float y, float angleOffset = 0f) { foreach (var ps in particleSystem) ps.FireAt(x, y, angleOffset); }
    public void Fire() { foreach (var ps in particleSystem) ps.Fire(); }
    public void Stop(bool killParticles) { foreach (var ps in particleSystem) ps.Stop(killParticles); }
    public void MoveTo(float x, float y, bool moveParticles) { foreach (var ps in particleSystem) ps.MoveTo(x, y, moveParticles); }
    public void Update(float dt) { foreach (var ps in particleSystem) ps.Update(dt); }
    public void Render() { foreach (var ps in particleSystem) ps.Render(); }

    public bool IsLive()
    {
        foreach (var ps in particleSystem) if (ps.IsLive()) return true;
        return false;
    }
}

/// <summary>Owns the managed effects and updates/renders them each frame.</summary>
public sealed class ecEffectManager
{
    private static ecEffectManager s_instance;
    public static ecEffectManager Instance() => s_instance ??= new ecEffectManager();

    public readonly List<ecEffect> effect = new();

    private ecEffectManager() { }

    /// <summary>Creates an effect owned by the caller (the caller updates and renders it); null when the definition is missing.</summary>
    public ecEffect CreateEffect(string effectName)
    {
        var e = new ecEffect();
        return e.Init(effectName) ? e : null;
    }

    /// <summary>Creates an effect managed by this manager; it is removed once it dies when autoRemove is true.</summary>
    public ecEffect AddEffect(string effectName, bool autoRemove)
    {
        var e = CreateEffect(effectName);
        if (e == null) return null;
        e.autoRemove = autoRemove;
        effect.Add(e);
        return e;
    }

    public void Update(float deltaTimeS)
    {
        for (int i = effect.Count - 1; i >= 0; i--)
        {
            var e = effect[i];
            e.Update(deltaTimeS);
            if (!e.IsLive() && e.autoRemove) effect.RemoveAt(i);
        }
    }

    public void Render()
    {
        foreach (var e in effect) e.Render();
    }

    public void RemoveAll() => effect.Clear();
}
