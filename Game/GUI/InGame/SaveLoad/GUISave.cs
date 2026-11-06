namespace WC2;

/// <summary>Save / load page with seven slots (GUISave.cpp).</summary>
public sealed class GUISave : GUIElement
{
    public GUIButton buttonBack;
    public GUIButton buttonOk;
    public GUISaveItem[] saveItem = new GUISaveItem[7];
    public ecTexture textureCommonBg;
    public ecImage imageCommonBg;
    public int gameMode;
    public bool load;
    public int selectedItem;

    public GUISave() { }

    /// <summary>Releases the background and unloads the flag atlas of the page; items and buttons belong to the child tree.</summary>
    public override void Dispose()
    {
        if (IsDisposed) return;
        imageCommonBg = null;

        if (textureCommonBg != null)
        {
            ecGraphics.Instance().FreeTexture(textureCommonBg);
            textureCommonBg = null;
        }

        bool useHdFlags = Layout.Tablet || g_contenscalefactor == 2.0f;
        s_TextureRes.UnloadRes(useHdFlags ? "flag_hd.xml" : "flag.xml");
        base.Dispose();
    }

    /// <summary>Initializes the save or load page and reads the headers of the seven slots.</summary>
    public void Init(in GUIRect rectIn, int gameModeIn, bool isLoad)
    {
        ecGraphics graphics = ecGraphics.Instance();
        bool wideLayout = Layout.Tablet;
        bool useHdFlags = wideLayout || g_contenscalefactor == 2.0f;
        s_TextureRes.LoadRes(useHdFlags ? "flag_hd.xml" : "flag.xml", !wideLayout && useHdFlags);

        rect = rectIn;
        gameMode = gameModeIn;
        load = isLoad;

        string backgroundName;
        if (wideLayout) backgroundName = "commonbg_iPad.png";
        else if (Layout.Width480) backgroundName = "commonbg.png";
        else if (Layout.WidthUpTo568) backgroundName = "commonbg-568h.png";
        else backgroundName = "commonbg-640h.png";

        textureCommonBg = graphics.LoadTexture(backgroundName);
        imageCommonBg = new ecImage(textureCommonBg, 0.0f, 0.0f, rect.w, rect.h);

        GUIRect buttonRect = wideLayout ? new GUIRect(0.0f, 730.0f, 40.0f, 38.0f) : new GUIRect(0.0f, 290.0f, 32.0f, 30.0f);

        buttonBack = new GUIButton();
        buttonBack.Init("buttontext_back.png", "buttontext_back_press.png", buttonRect, null);
        buttonBack.SetBackground("buttonboard_gray.png");
        AddChild(buttonBack, true);

        buttonRect = wideLayout ? new GUIRect(984.0f, 730.0f, 40.0f, 38.0f) : new GUIRect(graphics.screenW - 32.0f, 290.0f, 32.0f, 30.0f);

        buttonOk = new GUIButton();
        buttonOk.Init("buttontext_ok.png", "buttontext_ok_press.png", buttonRect, null);
        buttonOk.SetBackground("buttonboard_green.png");
        AddChild(buttonOk, true);

        float[] wideItemX = { 21.0f, 21.0f, 21.0f, 634.0f, 634.0f, 634.0f, 400.0f };
        float[] wideItemY = { 81.0f, 267.0f, 453.0f, 81.0f, 267.0f, 453.0f, 145.0f };
        float[] normalItemX = { -2.0f, -2.0f, -2.0f, 302.0f, 302.0f, 302.0f, 186.0f };
        float[] normalItemY = { 12.0f, 105.0f, 198.0f, 12.0f, 105.0f, 198.0f, 45.0f };

        float xBase = wideLayout ? 0.0f : (graphics.screenW - 480.0f) * 0.5f;
        for (int index = 0; index < 7; ++index)
        {
            bool autoSaveSlot = index == 6;
            var itemRect = new GUIRect();
            itemRect.x = (wideLayout ? wideItemX[index] : normalItemX[index]) + xBase;
            itemRect.y = wideLayout ? wideItemY[index] : normalItemY[index];
            if (wideLayout)
            {
                itemRect.w = autoSaveSlot ? 220.0f : 360.0f;
                itemRect.h = autoSaveSlot ? 412.0f : 173.0f;
            }
            else
            {
                itemRect.w = autoSaveSlot ? 110.0f : 180.0f;
                itemRect.h = autoSaveSlot ? 206.0f : 86.0f;
            }

            saveItem[index] = new GUISaveItem();
            saveItem[index].Init(itemRect, autoSaveSlot ? "btnsaveback2.png" : "btnsaveback1.png");
            AddChild(saveItem[index], true);
            saveItem[index].SetEnable(false);
        }

        var saveHeader = new SaveHeader();
        for (int index = 0; index < 7; ++index)
        {
            string saveName = gameMode == (int)CGameManager.CONQUEST
                ? Sprintf("conquest%d.sav", index)
                : Sprintf("game%d.sav", index);

            if (!g_GameManager.GetSaveHeader(saveName, saveHeader)) continue;

            string countryName = saveHeader.playerCountryName[0];
            if (saveHeader.gameMode == (int)CGameManager.MULTIPLAYER)
                countryName = Sprintf("%dv%d", 1, 1);

            saveItem[index].SetInfo(countryName,
                                    saveHeader.gameMode,
                                    saveHeader.campaignSeriesId,
                                    saveHeader.campaignLevelId,
                                    saveHeader.year,
                                    saveHeader.month,
                                    saveHeader.day,
                                    saveHeader.hour,
                                    saveHeader.min,
                                    index == 6);
            saveItem[index].SetEnable(true);
        }

        if (!isLoad)
        {
            for (int index = 0; index < 6; ++index)
                saveItem[index].SetEnable(true);
            saveItem[6].SetEnable(false);
        }

        selectedItem = -1;
    }

    /// <summary>Selects the slot and deselects the others; the selected item moves to the front so its highlight is not covered.</summary>
    public void SelItem(int index)
    {
        selectedItem = index;
        for (int itemIndex = 0; itemIndex < 7; ++itemIndex)
        {
            GUISaveItem item = saveItem[itemIndex];
            if (itemIndex == index)
            {
                item.selected = true;
                MoveToFront(item);
            }
            else
            {
                item.selected = false;
            }
        }
    }

    /// <summary>Back, slot selection and OK of the save / load page.</summary>
    public override bool OnEvent(Event evt)
    {
        switch (evt.type)
        {
            case Event.TOUCH:
                base.OnEvent(evt);
                return true;

            case Event.BACK_PRESSED:
                if ((flags & SHOWN) != 0)
                {
                    OnEvent(Event.Button(buttonBack));
                    return true;
                }
                break;

            case Event.GUI:
                if (evt.info.gui.type == Event.Info.GUIInfo.BUTTON)
                {
                    for (int index = 0; index < 7; ++index)
                    {
                        if (ReferenceEquals(evt.info.gui.ptr, saveItem[index]))
                        {
                            SelItem(index);
                            break;
                        }
                    }

                    if (ReferenceEquals(evt.info.gui.ptr, buttonOk))
                    {
                        int maxSlot = load ? 6 : 5;
                        if (selectedItem < 0 || selectedItem > maxSlot) return true;

                        string saveName = gameMode == (int)CGameManager.CONQUEST
                            ? Sprintf("conquest%d.sav", selectedItem)
                            : Sprintf("game%d.sav", selectedItem);

                        if (load)
                        {
                            var saveHeader = new SaveHeader();
                            g_GameManager.GetSaveHeader(saveName, saveHeader);
                            g_GameManager.LoadGame(saveName);
                        }
                        else
                        {
                            g_GameManager.SaveGame(saveName);
                        }
                    }
                }
                break;

            default:
                break;
        }

        return base.OnEvent(evt);
    }

    /// <summary>Draws the full screen background.</summary>
    public override void OnRender()
    {
        imageCommonBg.Render(0.0f, 0.0f);
    }
}
