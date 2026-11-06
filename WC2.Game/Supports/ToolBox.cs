namespace WC2;

public struct PinchZoomResult
{
    public float centerX;
    public float centerY;
    public float scale;
}

/// <summary>Free helpers from ToolBox.h / ToolBox.cpp.</summary>
public static class ToolBox
{
    /// <summary>
    /// Health bar fill color (0xAABBGGRR, opaque): red at low HP blending through yellow/green.
    /// Ported arithmetic of the original HpColor.
    /// </summary>
    public static uint HpColor(int HP, int MaxHP)
    {
        if (MaxHP <= 0) MaxHP = 1;
        int r, g, b;
        if (HP * 2 <= MaxHP)
        {
            r = 0;
            g = 255 - 255 * (MaxHP - 2 * HP) / MaxHP;
            b = 255;
        }
        else
        {
            g = 255;
            b = 255 - 255 * (2 * HP - MaxHP) / MaxHP;
            r = 128 - (b >> 1);
        }
        return unchecked((uint)(b + (g << 8) + (r << 16)) | 0xFF000000u);
    }

    /// <summary>Parses a ';' separated list of bit numbers into a mask (kept for compatibility).</summary>
    public static bool GetNumThroughList(string string_in, ref uint to_be_changed)
    {
        if (string_in == null) return false;
        uint initial_num = 0;
        foreach (var part in string_in.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            int num_this = atoi(part);
            if (num_this >= 0 && num_this < 32) initial_num |= 1u << num_this;
        }
        to_be_changed = initial_num;
        return true;
    }

    /// <summary>Looks a string up by key, then by a fallback key, then returns the default text.</summary>
    public static string GetLocalized(string key, string fallbackKey, string fallback)
    {
        string text = key != null && g_StringTable.HasKey(key) ? g_StringTable.GetString(key) : null;
        if (!string.IsNullOrEmpty(text)) return text;
        text = fallbackKey != null && g_StringTable.HasKey(fallbackKey) ? g_StringTable.GetString(fallbackKey) : null;
        return !string.IsNullOrEmpty(text) ? text : fallback;
    }

    public static bool CalculatePinchZoom(float cameraCenterX, float cameraCenterY, float cameraScale,
        float anchorWorldX, float anchorWorldY, float previousDistance, float currentDistance,
        float minScale, float maxScale, ref PinchZoomResult result)
    {
        if (cameraScale <= 0f || previousDistance <= 0f || currentDistance <= 0f) return false;
        float newScale = cameraScale * currentDistance / previousDistance;
        if (minScale > 0f && newScale < minScale) newScale = minScale;
        if (maxScale > 0f && newScale > maxScale) newScale = maxScale;
        float centerRatio = cameraScale / newScale;
        result.centerX = anchorWorldX + centerRatio * (cameraCenterX - anchorWorldX);
        result.centerY = anchorWorldY + centerRatio * (cameraCenterY - anchorWorldY);
        result.scale = newScale;
        return true;
    }

    /// <summary>Alliance color used for unit bases (0xAABBGGRR).</summary>
    public static uint GetAllianceColor(int alliance) => CGameRes.GetAllianceColor(alliance);
}

/// <summary>Campaign/conquest naming helpers (CampaignSize.cpp).</summary>
public static class CampaignSize
{
    public static string GetBattleKeyName(int series, int level)
    {
        string format = series switch
        {
            0 => "axis %d",
            1 => "allies %d",
            2 => "wto %d",
            3 => "nato %d",
            4 => "multiplay %d",
            _ => null,
        };
        return format == null ? "" : Sprintf(format, level + 1);
    }

    public static string GetBattleFileName(int gameMode, int unused, int campaignId, int level)
    {
        string format;
        if (gameMode == 1)
        {
            format = campaignId switch
            {
                0 => "battle_axis%d.xml",
                1 => "battle_allies%d.xml",
                2 => "battle_wto%d.xml",
                3 => "battle_nato%d.xml",
                _ => null,
            };
        }
        else
        {
            switch (gameMode)
            {
                case 2: format = "conquest_%d.xml"; break;
                case 4: format = "multiplay_%d.xml"; break;
                case 5: return "tutorails.xml";
                default: format = null; break;
            }
        }
        return format == null ? "" : Sprintf(format, level + 1);
    }

    public static string GetConquestKeyName(int id) => Sprintf("conquest %d", id + 1);

    public static int GetNumBattles(int SeriesID)
    {
        int i = 0;
        string key = GetBattleKeyName(SeriesID, i);
        while (CObjectDef.Instance().GetBattleDef(key) != null)
        {
            i++;
            if (i >= SERIES_MAX - 1) return i;
            key = GetBattleKeyName(SeriesID, i);
        }
        return i;
    }

    public static int GetNumMultiPlayerBattles(int unused) => 0;
}
