namespace WC2;

// The stacked army side bar lets the player look at the units of any country; only areas of
// the local player allow reordering. When a card targets a single unit of a stack the same
// side bar does the target selection.

/// <summary>One unit slot of the side bar (GUIArmyItem.h).</summary>
public sealed class GUIArmyItem : GUIButton
{
    /// <summary>The army belongs to the area; the item neither owns nor releases it.</summary>
    public CArmy army;

    public GUIArmyItem() { }

    /// <summary>
    /// Renders the unit preview. The side bar always uses the land unit style; a unit with the
    /// commander card shows the commander level of its country instead of the unit level.
    /// </summary>
    public override void OnRender()
    {
        GetAbsRect(out var rectAbs);
        if (army == null)
            // an empty slot draws nothing, so no uninitialized ArmyDef or country is touched
            return;
        g_GameRes.RenderUIArmy(army.country.name, rectAbs.x + rectAbs.w * 0.5f,
                               rectAbs.y + rectAbs.h, army.basicAbilities.id, false,
                               army.hp, army.GetMaxStrength(), army.movement,
                               army.cards, (army.cards & (1 << (int)ARMY_CARD.COMMANDER)) != 0
                                           ? army.country.GetCommanderLevel()
                                           : army.level);
    }
}

/// <summary>Stacked army selection panel (GUISelArmy.h / SelectArmy.cpp).</summary>
public sealed class GUISelArmy : GUIElement
{
    public ecImage imageBroad;
    public GUIArmyItem[] armyItem = new GUIArmyItem[4];
    public int armyCount;
    public int areaId;
    public int targetArmyIndex;
    public bool useCard;

    public GUISelArmy() { }

    /// <summary>Releases the background; the items belong to the child tree.</summary>
    public override void Dispose()
    {
        if (IsDisposed) return;
        imageBroad = null;
        base.Dispose();
    }

    /// <summary>Initializes the panel and its four unit slots.</summary>
    public void Init(in GUIRect rectIn)
    {
        rect = rectIn;

        imageBroad = new ecImage(s_TextureRes.GetImage("choosearmy_board.png"));
        imageBroad.SetAlpha(0.7f, -1);

        float itemWidth = rect.w;
        float itemHeight = rect.h * 0.25f;
        for (int index = 0; index < 4; ++index)
        {
            var itemRect = new GUIRect(0.0f, index * itemHeight, itemWidth, itemHeight);

            armyItem[index] = new GUIArmyItem();
            armyItem[index].Init(null, null, itemRect, null);
            armyItem[index].Hide();
            AddChild(armyItem[index], false);
        }

        armyCount = 0;
        useCard = false;
        targetArmyIndex = 0;
    }

    /// <summary>Draws the half transparent background.</summary>
    public override void OnRender()
    {
        GetAbsRect(out var absRect);
        imageBroad.Render(absRect.x, absRect.y);
    }

    /// <summary>Binds the panel to the area and refreshes the four slots.</summary>
    public void SetArea(int areaIdIn)
    {
        areaId = areaIdIn;
        CArea area = g_Scene.GetArea(areaIdIn);
        armyCount = area.armyCount;

        for (int index = 0; index < 4; ++index)
        {
            GUIArmyItem item = armyItem[index];
            if (index < armyCount)
            {
                item.army = area.GetArmy(index);
                item.Show();
            }
            else
            {
                item.army = null;
                item.Hide();
            }
        }
    }

    /// <summary>Moves the unit of the slot to the front and syncs the widget order and the network action.</summary>
    public void MoveArmyToFront(int index)
    {
        if (index < armyCount && armyItem[index] != null)
        {
            GUIArmyItem selectedItem = armyItem[index];
            for (int current = index; current > 0; --current)
                armyItem[current] = armyItem[current - 1];
            armyItem[0] = selectedItem;
        }

        float itemHeight = rect.h * 0.25f;
        for (int itemIndex = 0; itemIndex < armyCount; ++itemIndex)
        {
            armyItem[itemIndex].SetPos(0.0f, itemIndex * itemHeight);
            MoveToFront(armyItem[itemIndex]);
        }

        g_Scene.GetArea(areaId).MoveArmyToFront(index, true);

        if (g_GameManager.gameMode == CGameManager.MULTIPLAYER)
        {
            var action = new CountryAction
            {
                actionType = CountryAction.MOVE_FRONT_ACTION,
                startAreaId = areaId,
                armyIndex = index,
            };
            g_PlayerManager.SendPacketToAllPlayers(action, 0);
        }
    }

    /// <summary>Records the unit chosen as card target and asks the game state to submit the card action.</summary>
    public void TargetArmy(int index)
    {
        targetArmyIndex = index;
        base.OnEvent(Event.Gui(Event.Info.GUIInfo.OTHER, this, 4));
    }

    /// <summary>
    /// Touch and unit button handling. Tapping an own unit either picks it as card target or
    /// moves it to the front, depending on the card targeting state; units of other countries
    /// can only be looked at.
    /// </summary>
    public override bool OnEvent(Event evt)
    {
        if (evt.type == Event.TOUCH)
        {
            if (evt.info.touch.type == Event.Info.TouchInfo.BEGIN &&
                CheckInRect(evt.info.touch.pos[0], evt.info.touch.pos[1]))
            {
                // the items see the press first, then the side bar keeps it away from the map
                base.OnEvent(evt);
                return true;
            }
        }
        else if (evt.type == Event.GUI)
        {
            // only button events of an area of the player country may reorder or target units
            if (evt.info.gui.type == Event.Info.GUIInfo.BUTTON &&
                ReferenceEquals(g_Scene.GetArea(areaId).country, g_GameManager.GetPlayerCountry()))
            {
                for (int i = 0; i < 4; i++)
                {
                    if (ReferenceEquals(evt.info.gui.ptr, armyItem[i]))
                    {
                        var game = (CGameState)CStateManager.Instance().GetStatePtr(GAME);
                        if (game.guiBuyCard.GetSelCard() != null &&
                            game.guiBuyCard.cardCanTarget && game.guiBuyCard.cardTargetArmy)
                            // "unit target" card mode: the tap picks the i-th unit
                            TargetArmy(i);
                        else
                            // normal mode: move the tapped unit to the front for map moves / attacks
                            MoveArmyToFront(i);
                        break;
                    }
                }
            }
        }
        return base.OnEvent(evt);
    }
}
