namespace WC2;

/// <summary>Buy card overlay with the five tab lists (GUIBuyCard.cpp).</summary>
public sealed class GUIBuyCard : GUIElement
{
    // Display scale of the card name and description; independently adjustable.
    private const float kBuyCardNameTextScale = 0.85f;
    private const float kBuyCardIntroTextScale = 0.65f;

    public GUIButton buttonBack;
    public GUIButton buttonOk;
    public GUICardTab cardTab;
    public GUIGold goldGui;
    public GUITech techGui;
    public GUIMedal medalGui;
    public ecTexture buyCardBgTexture;
    public ecImage buyCardBgImage;
    public ecTexture broadTexture;
    public ecImage broadImage;
    public ecTexture unknownTexture;
    public ecImage unknownImage;
    public readonly ecUniFont font = new();
    public readonly ecText nameText = new();
    public readonly ecText introText = new();
    public GUICardList[] cardListGui = new GUICardList[5];
    public int xD8;
    public int selCardId = -1;
    public int xE0;
    public bool cardCanTarget;
    public bool cardTargetArmy;

    /// <summary>Creates the overlay; the widgets and the selection state are set by Init and the flow.</summary>
    public GUIBuyCard()
    {
        buttonBack = null;
        buttonOk = null;
    }

    /// <summary>Releases the images and textures held directly.</summary>
    public override void Dispose()
    {
        if (IsDisposed) return;
        buyCardBgImage = null;
        if (buyCardBgTexture != null)
        {
            ecGraphics.Instance().FreeTexture(buyCardBgTexture);
            buyCardBgTexture = null;
        }

        broadImage = null;
        if (broadTexture != null)
        {
            ecGraphics.Instance().FreeTexture(broadTexture);
            broadTexture = null;
        }

        unknownImage = null;
        if (unknownTexture != null)
        {
            ecGraphics.Instance().FreeTexture(unknownTexture);
            unknownTexture = null;
        }
        base.Dispose();
    }

    /// <summary>
    /// Creates the overlay and its five tab lists. The wide layout uses the bottom leather board,
    /// the normal layout a full screen background; buttons and top HUD differ between both.
    /// </summary>
    public void Init(in GUIRect rect_in)
    {
        var tmp_rect_texture = new ecTextureRect(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f);
        var tmp_rect = new GUIRect(0.0f, 0.0f, 0.0f, 0.0f);
        rect.x = rect_in.x;
        rect.y = rect_in.y;
        rect.w = rect_in.w;
        rect.h = rect_in.h;
        float tmp_float_y = 0.0f, tmp_float_w = 0.0f;
        var graphics = ecGraphics.Instance();

        if (Layout.Tablet)
        {
            // iPad / wide: the panel sits at the bottom with a dedicated WEBP background and 40x38 buttons
            tmp_rect_texture.w = 1024.0f;
            tmp_rect_texture.x = 0.0f;
            tmp_rect_texture.y = 0.0f;
            tmp_rect_texture.h = 311.0f;
            tmp_rect_texture.refx = 0.0f;
            tmp_rect_texture.refy = 11.0f;
            broadTexture = graphics.LoadWEBPTexture("board_leather_down_iPad.webp");
            broadImage = new ecImage(broadTexture, tmp_rect_texture);
            buttonBack = new GUIButton();
            tmp_rect.x = 0.0f;
            tmp_rect.w = 40.0f;
            tmp_rect.h = 38.0f;
            tmp_rect.y = 730.0f - rect.y;
            buttonBack.Init("buttontext_back.png", "buttontext_back_press.png", tmp_rect, null);
            buttonBack.SetBackground("buttonboard_gray.png");
            AddChild(buttonBack, true);
            tmp_rect.x = 984.0f;
            buttonOk = new GUIButton();
            tmp_rect.w = 40.0f;
            tmp_rect.h = 38.0f;
            tmp_float_y = 43.0f;
            tmp_rect.y = 730.0f - rect.y;
            buttonOk.Init("buttontext_ok.png", "buttontext_ok_press.png", tmp_rect, null);
            tmp_float_w = 1024.0f;
            buttonOk.SetBackground("buttonboard_green.png");
            AddChild(buttonOk, true);
        }
        else
        {
            // normal screens: background by screen width, the overlay covers the visible area
            if (Layout.WidthUpTo568)
            {
                if (Layout.Width480)
                    buyCardBgTexture = graphics.LoadWEBPTexture("buycardbg.webp");
                else
                    buyCardBgTexture = graphics.LoadWEBPTexture("buycardbg-568h.webp");
            }
            else
            {
                buyCardBgTexture = graphics.LoadWEBPTexture("buycardbg-640h.webp");
            }

            buyCardBgImage = new ecImage(buyCardBgTexture, 0f, 0f, graphics.screenW, graphics.screenH);

            buttonBack = new GUIButton();
            tmp_rect.x = 0.0f;
            tmp_rect.y = 290.0f;
            tmp_rect.w = 32.0f;
            tmp_rect.h = 30.0f;
            buttonBack.Init("buttontext_back.png", "buttontext_back_press.png", tmp_rect, null);
            buttonBack.SetBackground("buttonboard_gray.png");
            AddChild(buttonBack, true);
            buttonOk = new GUIButton();
            tmp_float_y = 27.0f;
            tmp_rect.y = 290.0f;
            tmp_rect.w = 32.0f;
            tmp_rect.h = 30.0f;
            tmp_rect.x = (graphics.screenW + 448.0f) - 480.0f;
            buttonOk.Init("buttontext_ok.png", "buttontext_ok_press.png", tmp_rect, null);
            buttonOk.SetBackground("buttonboard_green.png");
            AddChild(buttonOk, true);
            tmp_float_w = graphics.screenW + 439.0f - 480.0f;
        }
        // the tab bar sits above the card lists; the switch events go through the GUI event chain
        cardTab = new GUICardTab();
        tmp_rect.h = rect.h;
        tmp_rect.w = rect.w;
        tmp_rect.x = 0.0f;
        tmp_rect.y = 0.0f;
        cardTab.Init(tmp_rect);
        AddChild(cardTab, true);
        nameText.Init(g_FontKt);
        nameText.SetColor(0xFF0B1F31u);
        introText.Init(g_FontKt);
        introText.SetColor(0xFF0B1F31u);
        // the five lists are created at once and only the first is shown; switching tabs hides the others
        for (int i = 0; i <= 4; i++)
        {
            cardListGui[i] = new GUICardList();
            tmp_rect.x = 21.0f;
            tmp_rect.h = 150.0f;
            tmp_rect.y = tmp_float_y;
            tmp_rect.w = tmp_float_w;
            cardListGui[i].Init(tmp_rect, i);
            AddChild(cardListGui[i], false);
            if (i > 0)
                cardListGui[i].Hide();
        }
        tmp_rect.x = 0.0f;
        tmp_rect.y = 0.0f;
        tmp_rect.w = 1.0f;
        tmp_rect.h = 1.0f;
        if (Layout.Tablet)
        {
            // the wide layout puts the tech info in the top leather strip; the normal one also shows gold and medals
            tmp_rect_texture.w = 1024.0f;
            tmp_rect_texture.x = 0.0f;
            tmp_rect_texture.h = 44.0f;
            tmp_rect_texture.y = 0.0f;
            tmp_rect_texture.refx = 0.0f;
            tmp_rect_texture.refy = 1.0f;
            MoveToFront(GUIManager.Instance().AddImage("board_leather_up_iPad.png", tmp_rect_texture, tmp_rect, this, false));
        }
        else
        {
            if (Layout.WidthUpTo568)
            {
                if (Layout.Width480)
                {
                    tmp_rect_texture.x = 0.0f;
                    tmp_rect_texture.w = 479.0f;
                    tmp_rect_texture.y = 0.0f;
                    tmp_rect_texture.refx = 0.0f;
                    tmp_rect_texture.h = 24.0f;
                    tmp_rect_texture.refy = 0.0f;
                    MoveToFront(GUIManager.Instance().AddImage("board_leather_up.png", tmp_rect_texture, tmp_rect, this, false));
                }
                else
                {
                    tmp_rect_texture.x = 0.0f;
                    tmp_rect_texture.h = 16.0f;
                    tmp_rect_texture.y = 0.0f;
                    tmp_rect_texture.w = 568.0f;
                    tmp_rect_texture.refx = 0.0f;
                    tmp_rect_texture.refy = 0.0f;
                    MoveToFront(GUIManager.Instance().AddImage("board_leather_up-568h.png", tmp_rect_texture, tmp_rect, this, false));
                }
            }
            else
            {
                tmp_rect_texture.x = 0.0f;
                tmp_rect_texture.w = 640.0f;
                tmp_rect_texture.y = 0.0f;
                tmp_rect_texture.h = 16.0f;
                tmp_rect_texture.refx = 0.0f;
                tmp_rect_texture.refy = 0.0f;
                MoveToFront(GUIManager.Instance().AddImage("board_leather_up-640h.png", tmp_rect_texture, tmp_rect, this, false));
            }
        }
        if (Layout.Tablet)
        {
            techGui = new GUITech();
            tmp_rect.y = 1.0f;
            tmp_rect.x = 472.0f;
            tmp_rect.w = 80.0f;
            tmp_rect.h = 30.0f;
            techGui.Init(tmp_rect);
            AddChild(techGui, false);
        }
        else
        {
            goldGui = new GUIGold();
            tmp_rect.x = 0.0f;
            tmp_rect.w = 104.0f;
            tmp_rect.y = 0.0f;
            tmp_rect.h = 38.0f;
            goldGui.Init(tmp_rect);
            AddChild(goldGui, false);
            techGui = new GUITech();
            tmp_rect.y = 0.0f;
            tmp_rect.w = 80.0f;
            tmp_rect.h = 30.0f;
            tmp_rect.x = ((graphics.screenW - 480.0f) * 0.5f) + 200.0f;
            techGui.Init(tmp_rect);
            AddChild(techGui, false);
            medalGui = new GUIMedal();
            tmp_rect.x = 36.0f;
            tmp_rect.y = 297.0f;
            tmp_rect.w = 70.0f;
            tmp_rect.h = 23.0f;
            medalGui.Init(tmp_rect);
            AddChild(medalGui, true);
        }
        cardCanTarget = false;
        cardTargetArmy = false;
    }

    /// <summary>Ends the card target selection and restores the target marks of the selected area.</summary>
    public void ReleaseTarget()
    {
        cardCanTarget = false;
        cardTargetArmy = false;
        g_Scene.ResetTarget();
    }

    /// <summary>
    /// Refreshes the buyable look of every card from the resources, tech, cool down and commander
    /// state of the local player, and syncs the OK button. UI only: no resources are spent.
    /// </summary>
    public void ResetCardState()
    {
        CCountry Country = g_GameManager.GetPlayerCountry();

        if (Country == null) return;

        if (Country.ai)
            // the buy page only serves humans; the AI buys through its own strategy
            return;
        foreach (var tabList in GUICardTab.AllCardTab)
        {
            // serial is the index inside the tab, not the global CARD_ID
            int serial = 0;
            foreach (CARD_ID pre_ID in tabList)
            {
                CardDef card_tmp = CObjectDef.Instance().GetCardDef(pre_ID);
                if (card_tmp == null) continue; // the list skipped cards without a definition too
                int money_here = Country.GetCardPrice(card_tmp);
                int industry_here = Country.GetCardIndustry(card_tmp);
                GUICard GUI_tmp = cardListGui[(int)card_tmp.type].GetCard(serial);
                serial++;
                if (GUI_tmp != null)
                {
                    GUI_tmp.SetPrice(money_here);
                    GUI_tmp.SetIndustry(industry_here);
                    // restore the default look first, then apply the tech, resource and cool down limits
                    GUI_tmp.notGrey = true;
                    GUI_tmp.technology = 0;
                    GUI_tmp.coolDown = Country.cardRound[(int)card_tmp.id];
                    if (card_tmp.tech > Country.tech)
                    {
                        // technology renders the required tech icon, it does not only grey the card
                        GUI_tmp.notGrey = false;
                        GUI_tmp.technology = card_tmp.tech;
                    }
                    if (Country.IsEnoughMoney(card_tmp))
                    {
                        GUI_tmp.SetPriceColor(0xFF0F2632u);
                    }
                    else
                    {
                        GUI_tmp.notGrey = false;
                        GUI_tmp.SetPriceColor(0xFF000080u);
                    }
                    if (Country.IsEnoughIndustry(card_tmp))
                    {
                        GUI_tmp.SetIndustryColor(0xFF0F2632u);
                    }
                    else
                    {
                        GUI_tmp.notGrey = false;
                        GUI_tmp.SetIndustryColor(0xFF000080u);
                    }
                    if (card_tmp.id == RESEARCH_CARD)
                    {
                        // the research card cools down with the country techTurn, and is free but unusable at max level
                        GUI_tmp.coolDown = Country.techTurn;
                        if (GUI_tmp.coolDown > 0)
                            GUI_tmp.notGrey = false;
                        if (Country.tech >= 5)
                        {
                            GUI_tmp.notGrey = false;
                            GUI_tmp.SetPrice(0);
                            GUI_tmp.SetIndustry(0);
                        }
                        else
                        {
                            GUI_tmp.SetPrice(money_here);
                            GUI_tmp.SetIndustry(industry_here);
                        }
                        GUI_tmp.researchLevel = Country.tech;
                    }
                    else if (card_tmp.id == COMMANDER_CARD)
                    {
                        // the commander card uses "commander alive / revive cool down" instead of the normal cardRound
                        if (Country.CanUseCommander())
                        {
                            GUI_tmp.coolDown = 0;
                        }
                        else
                        {
                            if (Country.commanderAlive)
                            {
                                GUI_tmp.coolDown = 0;
                                GUI_tmp.notGrey = false;
                            }
                            else
                            {
                                GUI_tmp.coolDown = Country.commanderTurn;
                            }
                        }
                    }
                    if (GUI_tmp.coolDown > 0)
                        GUI_tmp.notGrey = false;
                }
            }
        }
        // the selection may have become invalid after the refresh, so the OK state is recomputed every time
        if (GetSelCard() != null && CanBuySelCard())
            buttonOk.SetEnable(true);
        else
            buttonOk.SetEnable(false);
    }

    /// <summary>Draws the background, the board, the card description and the card name.</summary>
    public override void OnRender()
    {
        GetAbsRect(out var absoluteRect);

        if (buyCardBgImage != null)
            buyCardBgImage.Render(absoluteRect.x, absoluteRect.y);
        if (broadImage != null)
            broadImage.Render(absoluteRect.x, absoluteRect.y);

        if (Layout.Tablet)
        {
            ecTextDrawScaled(nameText, absoluteRect.x + 552.0f, absoluteRect.y + 222.0f, 2, kBuyCardNameTextScale);
            ecTextDrawScaled(introText, absoluteRect.x + 428.0f, absoluteRect.y + 238.0f, 0, kBuyCardIntroTextScale);
            return;
        }

        float wideScreenOffset = Layout.Width480 ? 0.0f : 5.0f;
        ecTextDrawScaled(nameText, absoluteRect.x + 286.0f + wideScreenOffset, absoluteRect.y + 240.0f, 2, kBuyCardNameTextScale);
        ecTextDrawScaled(introText, absoluteRect.x + 152.0f + wideScreenOffset, absoluteRect.y + 258.0f, 0, kBuyCardIntroTextScale);
    }

    /// <summary>Selects the card of the tab, refreshes its name / description and the OK button.</summary>
    public void SelCard(int CardTab, int Index)
    {
        introText.SetText("");
        nameText.SetText("");
        // the card id is the single selection state; SetSelCardIntro fills the texts from it
        var tabList = GUICardTab.AllCardTab[CardTab];
        selCardId = Index >= 0 && Index < tabList.Count ? (int)tabList[Index] : -1;
        SetSelCardIntro();
        if (GetSelCard() != null && CanBuySelCard())
            buttonOk.SetEnable(true);
        else
            buttonOk.SetEnable(false);
    }

    /// <summary>Refreshes the name and the description from the selected card.</summary>
    public void SetSelCardIntro()
    {
        if (selCardId < 0) return;

        CardDef selectedCard = CObjectDef.Instance().GetCardDef((CARD_ID)selCardId);
        if (selectedCard == null) return;

        nameText.SetText(g_StringTable.GetString(selectedCard.name));
        introText.SetText(g_StringTable.GetString(selectedCard.intro));
    }

    /// <summary>The selected card definition, or null when nothing is selected / undefined.</summary>
    public CardDef GetSelCard()
    {
        if (selCardId < 0) return null;
        return CObjectDef.Instance().GetCardDef((CARD_ID)selCardId);
    }

    /// <summary>Whether the current country can buy the selected card.</summary>
    public bool CanBuySelCard()
    {
        if (selCardId < 0) return false;

        CCountry currentCountry = g_GameManager.GetCurCountry();
        if (currentCountry == null) return false;

        CardDef selectedCard = CObjectDef.Instance().GetCardDef((CARD_ID)selCardId);
        return currentCountry.CanBuyCard(selectedCard);
    }

    /// <summary>Marks the possible targets of the selected targeting card again.</summary>
    public void ResetCardTarget()
    {
        CCountry currentCountry = g_GameManager.GetCurCountry();
        string countryName = currentCountry?.name;

        if (!cardCanTarget || selCardId < 0 || countryName == null) return;

        g_Scene.ClearTargets();
        CardDef selectedCard = CObjectDef.Instance().GetCardDef((CARD_ID)selCardId);
        currentCountry.SetCardTargets(selectedCard);
    }

    /// <summary>Touch, back, tab switch, card selection and OK handling of the buy card panel.</summary>
    public override bool OnEvent(Event evt)
    {
        if (evt.type == Event.TOUCH)
        {
            // the children (buttons, tabs, card lists, HUD) see the touch first; the panel is modal
            base.OnEvent(evt);
            return true;
        }

        if (evt.type == Event.BACK_PRESSED)
        {
            if ((flags & SHOWN) != 0)
            {
                base.OnEvent(Event.Button(buttonBack));
                return true;
            }
        }
        else if (evt.type == Event.GUI)
        {
            if (evt.info.gui.type != Event.Info.GUIInfo.BUTTON)
            {
                if (evt.info.gui.type == Event.Info.GUIInfo.OTHER &&
                    evt.info.gui.info == 5 && cardTab.selectedTab >= 0)
                {
                    int selectedTab = cardTab.selectedTab;
                    SelCard(selectedTab, cardListGui[selectedTab].selectedItem);
                }
            }
            else
            {
                int tabIndex = cardTab.GetCardTabButtonIdx(evt.info.gui.ptr);
                if (tabIndex >= 0)
                {
                    foreach (GUICardList cardList in cardListGui)
                        cardList.Hide();
                    cardListGui[tabIndex].Show();
                    SelCard(tabIndex, cardListGui[tabIndex].selectedItem);
                }

                if (ReferenceEquals(evt.info.gui.ptr, buttonOk))
                {
                    CardDef selectedCard = GetSelCard();
                    if (selectedCard != null && CanBuySelCard())
                    {
                        CCountry currentCountry = g_GameManager.GetCurCountry();
                        int targetType = CObjectDef.Instance().GetCardTargetType(selectedCard);

                        if (targetType == 1 || targetType == 5)
                        {
                            cardCanTarget = true;
                            cardTargetArmy = targetType == 5;
                            if (!currentCountry.ai)
                            {
                                g_Scene.ClearTargets();
                                currentCountry.SetCardTargets(selectedCard);
                            }
                        }
                        else
                        {
                            var action = new CountryAction
                            {
                                actionType = CountryAction.CARD_ACTION,
                                cardId = selectedCard.id,
                                startAreaId = -1,
                                targetAreaId = -1,
                            };
                            currentCountry.Action(action);

                            if (g_GameManager.gameMode == CGameManager.MULTIPLAYER)
                                g_PlayerManager.SendPacketToAllPlayers(action, 0);
                        }

                        if (selectedCard.id == RESEARCH_CARD)
                            ResetCardState();
                        else
                            Hide();
                    }
                }
            }
        }

        return base.OnEvent(evt);
    }
}
