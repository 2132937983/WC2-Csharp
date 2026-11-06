namespace WC2;

/// <summary>One save slot of the save / load page (GUISaveItem.cpp).</summary>
public sealed class GUISaveItem : GUIRadioButton
{
    private const float kSaveItemNameTextScale = 0.75f;
    private const float kSaveItemAutoSaveTextScale = 0.75f;
    private const float kSaveItemTextYOffset = 7.0f;
    private const float kSaveItemTextXOffset = 2.0f;

    public ecImage imageFlag;
    public readonly ecText textTime = new();
    public readonly ecText textName = new();
    public readonly ecText textAutoSave = new();
    public bool isAutoSave;
    public float flashColor;
    public float flashColorChangeRate;
    public int xE0;
    public float yTextName;

    public GUISaveItem() { }

    /// <summary>Releases the flag image.</summary>
    public override void Dispose()
    {
        if (IsDisposed) return;
        imageFlag = null;
        base.Dispose();
    }

    /// <summary>Initializes the background, the time text, the name text and the auto save text.</summary>
    public void Init(in GUIRect rectIn, string saveBackImageName)
    {
        Init(saveBackImageName, saveBackImageName, rectIn, null);

        textTime.Init(ecGraphics.Instance().widthMode == 3 ? g_Num4b : g_Num4);
        textTime.SetColor(0xFF354A3Bu);

        textName.Init(g_FontHt);
        textAutoSave.Init(g_FontHt);
        textName.SetColor(0xFFD5E7EFu);
        textAutoSave.SetColor(0xFFD5E7EFu);

        flashColor = 1.0f;
        flashColorChangeRate = -0.4f;
        xE0 = 0;
        yTextName = 0.0f;

        string language = g_LocalizableStrings.GetString("language");
        if (language != "en")
            yTextName = ecGraphics.Instance().widthMode == 3 ? -4.0f : -2.0f;
    }

    /// <summary>Sets the flag, the time, the battle name and the auto save mark of the slot.</summary>
    public void SetInfo(string countryName, int gameMode, int series, int level, int year, int month, int day, int hour, int minute, bool isAutoSaveIn)
    {
        imageFlag = null;
        textTime.SetText("");

        string text;
        if (countryName != null)
        {
            text = Sprintf("flag_%s.png", countryName);
            ecImageAttr flagAttr = s_TextureRes.GetImage(text);
            if (flagAttr != null)
                imageFlag = new ecImage(flagAttr);
        }

        if (isAutoSaveIn)
            text = Sprintf("%02d:%02d\n%04d/%02d/%02d", hour, minute, year, month, day);
        else
            text = Sprintf("%02d:%02d %04d/%02d/%02d", hour, minute, year, month, day);
        textTime.SetText(text);

        string nameText;
        if (gameMode == (int)CGameManager.CONQUEST)
        {
            string stringKey = Sprintf("conquest name %d", level + 1);
            nameText = g_StringTable.GetString(stringKey);
        }
        else
        {
            string stringKey = Sprintf("alliance name %d", series + 1);
            string allianceName = g_StringTable.GetString(stringKey);
            nameText = Sprintf("%s %d", allianceName, level + 1);
        }
        textName.SetText(nameText);

        if (isAutoSaveIn)
            textAutoSave.SetText(g_StringTable.GetString("AutoSave"));
        isAutoSave = isAutoSaveIn;
    }

    /// <summary>Draws the slot background, the flag, the time and the level info.</summary>
    public override void OnRender()
    {
        GetAbsRect(out var absoluteRect);

        if (imageGlow != null && selected)
        {
            uint glowColor = ((uint)(alpha * 255.0f) << 24) | 0x00FFFFFFu;
            imageGlow.SetColor(glowColor, -1);
            imageGlow.Render(absoluteRect.x, absoluteRect.y);
        }

        ecImage buttonImage = iPressedDown == 1 ? imagePressed : imageNotPresed;
        if (buttonImage != null)
        {
            uint alphaChannel = (uint)(alpha * 255.0f) << 24;
            uint color;
            if (selected)
            {
                uint redBlue = (uint)(flashColor * 200.0f);
                uint green = (uint)(flashColor * 255.0f);
                color = alphaChannel | (redBlue << 16) | (green << 8) | redBlue;
            }
            else if ((flags & ENABLED) != 0)
            {
                color = alphaChannel | (iPressedDown == 1 ? 0x00D2D2D2u : 0x00FFFFFFu);
            }
            else
            {
                color = alphaChannel | 0x00787878u;
            }
            buttonImage.SetColor(color, -1);
            buttonImage.Render(absoluteRect.x, absoluteRect.y);
        }

        bool wideLayout = ecGraphics.Instance().widthMode == 3;
        if (wideLayout)
        {
            if (isAutoSave)
            {
                if (imageFlag != null)
                    imageFlag.Render(absoluteRect.x + 59.0f, absoluteRect.y + 79.0f);
                textTime.DrawText(absoluteRect.x + absoluteRect.w * 0.5f, absoluteRect.y + 248.0f, 2);
                ecTextDrawScaled(textName,
                    absoluteRect.x + absoluteRect.w * 0.5f - 8.0f + kSaveItemTextXOffset,
                    absoluteRect.y + 151.0f + yTextName + kSaveItemTextYOffset, 2,
                    kSaveItemNameTextScale);
                ecTextDrawScaled(textAutoSave,
                    absoluteRect.x + absoluteRect.w * 0.5f - 8.0f + kSaveItemTextXOffset,
                    absoluteRect.y + 3.0f + yTextName + kSaveItemTextYOffset, 2,
                    kSaveItemAutoSaveTextScale);
            }
            else
            {
                if (imageFlag != null)
                    imageFlag.Render(absoluteRect.x + 18.0f, absoluteRect.y + 17.0f);
                textTime.DrawText(absoluteRect.x + 44.0f, absoluteRect.y + 104.0f, 0);
                ecTextDrawScaled(textName, absoluteRect.x + 216.0f + kSaveItemTextXOffset,
                    absoluteRect.y + 2.0f + yTextName + kSaveItemTextYOffset, 2,
                    kSaveItemNameTextScale);
            }
        }
        else if (isAutoSave)
        {
            if (imageFlag != null)
                imageFlag.Render(absoluteRect.x + 31.0f, absoluteRect.y + 39.0f);
            textTime.DrawText(absoluteRect.x + absoluteRect.w * 0.5f, absoluteRect.y + 126.0f, 2);
            ecTextDrawScaled(textName,
                absoluteRect.x + absoluteRect.w * 0.5f - 4.0f + kSaveItemTextXOffset,
                absoluteRect.y + 76.0f + yTextName + kSaveItemTextYOffset, 2,
                kSaveItemNameTextScale);
            ecTextDrawScaled(textAutoSave,
                absoluteRect.x + absoluteRect.w * 0.5f - 4.0f + kSaveItemTextXOffset,
                absoluteRect.y + 2.0f + yTextName + kSaveItemTextYOffset, 2,
                kSaveItemAutoSaveTextScale);
        }
        else
        {
            if (imageFlag != null)
                imageFlag.Render(absoluteRect.x + 10.0f, absoluteRect.y + 8.0f);
            textTime.DrawText(absoluteRect.x + 22.0f, absoluteRect.y + 52.0f, 0);
            ecTextDrawScaled(textName, absoluteRect.x + 113.0f + kSaveItemTextXOffset,
                absoluteRect.y + 1.0f + yTextName + kSaveItemTextYOffset, 2,
                kSaveItemNameTextScale);
        }
    }

    /// <summary>Flashing color of the selected slot.</summary>
    public override void OnUpdate(float deltaTimeS)
    {
        if (!selected) return;

        float nextColor = flashColor + flashColorChangeRate * deltaTimeS;
        if (nextColor <= 0.8f)
        {
            flashColor = 0.8f;
            flashColorChangeRate = -flashColorChangeRate;
        }
        else if (nextColor >= 1.0f)
        {
            flashColor = 1.0f;
            flashColorChangeRate = -flashColorChangeRate;
        }
        else
        {
            flashColor = nextColor;
        }
    }
}
