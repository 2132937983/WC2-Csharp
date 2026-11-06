namespace WC2;

/// <summary>Scrolling level list of the battle selection screen (gui/menus/battles/GUIBattleList.cpp).</summary>
public sealed class GUIBattleList : GUIElement
{
    public List<GUIBattleItem> battleItem = new();
    public float hBattleItem;
    public float dyBattleItem;
    public float selectedOffset;
    public float hBottomGap;
    public int selectedLevel;
    public CTouchInertia touchInertia = new CTouchInertia();
    public bool touched;
    public float xTouch;
    public float yTouch;
    public int xC8;
    public float xTouchBegin;
    public float yTouchBegin;
    public bool touchMoved;
    public int campaignSeriesId;
    public int xDC;

    /// <summary>The original only builds the empty item vector and the touch inertia; the scalars are set by Init.</summary>
    public GUIBattleList() { }

    /// <summary>battleItem only references the children; the child tree frees them.</summary>
    public override void Dispose()
    {
        if (IsDisposed) return;
        base.Dispose();
    }

    /// <summary>
    /// Updates the inertia scrolling, the bounce back at both ends and the item positions.
    /// The original does not call GUIElement::OnUpdate here.
    /// </summary>
    public override void OnUpdate(float deltaTimeS)
    {
        touchInertia.Update(deltaTimeS);

        if (touchInertia.GetSpeed(out float speedX, out float speedY))
        {
            // the horizontal speed is tracked by CTouchInertia but the list only scrolls along Y
            hBottomGap += deltaTimeS * speedY;
        }

        if (!touched)
        {
            // top overflow: bounce back to zero; the step is at least 0.4 so it never stalls
            if (hBottomGap > 0f)
            {
                float correction = deltaTimeS * hBottomGap * 5f;
                if (correction < 0.4f)
                {
                    correction = 0.4f;
                }
                hBottomGap -= correction;
                if (hBottomGap < 0f)
                {
                    hBottomGap = 0f;
                }
            }

            float contentBottom = battleItem.Count * dyBattleItem - (dyBattleItem - hBattleItem);
            float visibleBottom = contentBottom <= rect.h ? contentBottom : rect.h;

            // bottom overflow: bounce back towards the smallest allowed offset without crossing it
            if (hBottomGap + contentBottom < visibleBottom)
            {
                float correction = deltaTimeS * (visibleBottom - hBottomGap - contentBottom) * 5f;
                if (correction < 0.4f)
                {
                    correction = 0.4f;
                }

                float nextGap = hBottomGap + correction;
                hBottomGap = nextGap + contentBottom > visibleBottom
                           ? visibleBottom - contentBottom
                           : nextGap;
            }
        }

        float itemY = hBottomGap;
        foreach (GUIBattleItem item in battleItem)
        {
            item.GetPos(out float itemX, out float unusedY);
            item.SetPos(itemX, itemY);
            itemY += dyBattleItem;
        }
    }

    /// <summary>
    /// Finds the selectable item under the given point. Locked items never match, so the touch
    /// flow does not have to check the unlock state again.
    /// </summary>
    public int GelSelItem(float x, float y)
    {
        for (int index = 0; index < battleItem.Count; index++)
        {
            GUIBattleItem item = battleItem[index];
            if (!item.locked && item.CheckInRect(x, y))
            {
                return index;
            }
        }
        return -1;
    }

    /// <summary>Resets the touch state (apparently unused).</summary>
    public void ResetTouchState()
    {
        touchInertia.Init();
        touched = false;
    }

    /// <summary>
    /// Clears the selected display of the current item. A selected item is moved by
    /// selectedOffset; the offset is added back to its current X.
    /// </summary>
    public void ResetSelect()
    {
        if (selectedLevel < 0)
        {
            return;
        }

        GUIBattleItem item = battleItem[selectedLevel];
        item.SetSelected(false);

        item.GetPos(out float itemX, out float itemY);
        item.SetPos(itemX + selectedOffset, itemY);

        selectedLevel = -1;
    }

    /// <summary>
    /// Selects the given item and notifies the parent selection screen. The original does not
    /// clear the old selection here, the caller runs ResetSelect first. The item moves left by
    /// selectedOffset and to the front of the draw order to show its expanded look.
    /// </summary>
    public void SetSelect(int levelIn)
    {
        selectedLevel = levelIn;

        GUIBattleItem item = battleItem[levelIn];
        item.SetSelected(true);

        item.GetPos(out float itemX, out float itemY);
        item.SetPos(itemX - selectedOffset, itemY);
        MoveToFront(item);

        // OTHER/7 = the level selection changed; handled by the parent GUISelBattle
        var selectEvent = Event.Gui(Event.Info.GUIInfo.OTHER, this, 7);
        base.OnEvent(selectEvent);
    }

    /// <summary>
    /// Selects the last unlocked item of the list. Skips the locked items from the end; an
    /// empty or fully locked list does nothing. Does not reset the current selection.
    /// </summary>
    public void SelectLastUnlocked()
    {
        for (int level = battleItem.Count - 1; level >= 0; --level)
        {
            if (!battleItem[level].locked)
            {
                SetSelect(level);
                return;
            }
        }
    }

    /// <summary>
    /// Touch scrolling and tap selection. BEGIN inside the list starts the inertia tracking;
    /// MOVE scrolls along Y and flags a drag after more than 10 pixels; END selects the unlocked
    /// item under the finger when it was no drag.
    /// </summary>
    public override bool OnEvent(Event evt)
    {
        if (evt.type != Event.TOUCH)
        {
            return base.OnEvent(evt);
        }

        // original engine order: the children (battle items) see the touch before the list
        if (base.OnEvent(evt))
        {
            return true;
        }

        // like the original: without the shown/enabled bits the touch is rejected
        if ((flags & (ACTIVE | SHOWN)) != (ACTIVE | SHOWN))
        {
            return false;
        }
        if ((flags & ENABLED) == 0)
        {
            return false;
        }

        float touchX = evt.info.touch.pos[0];
        float touchY = evt.info.touch.pos[1];
        int touchIndex = evt.info.touch.index;

        switch (evt.info.touch.type)
        {
            case Event.Info.TouchInfo.BEGIN:
            {
                if (!CheckInRect(touchX, touchY))
                {
                    return false;
                }

                // a second finger drops the previous inertia track
                if (ecMultipleTouch.Instance().touch.Count != 1)
                {
                    touchInertia.Init();
                }

                if (touchInertia.TouchBegin(touchX, touchY, touchIndex))
                {
                    touched = true;
                    xTouch = touchX;
                    yTouch = touchY;
                    touchMoved = false;
                    xTouchBegin = touchX;
                    yTouchBegin = touchY;
                }
                // a BEGIN inside the list is consumed even when the inertia tracker refused it
                return true;
            }

            case Event.Info.TouchInfo.MOVE:
            {
                if (!touched ||
                    !touchInertia.TouchMove(touchX, touchY, touchIndex))
                {
                    return false;
                }

                hBottomGap += touchY - yTouch;
                xTouch = touchX;
                yTouch = touchY;

                int deltaX = (int)(xTouchBegin - touchX);
                if (deltaX < 0)
                {
                    deltaX = -deltaX;
                }
                int deltaY = (int)(yTouchBegin - touchY);
                if (deltaY < 0)
                {
                    deltaY = -deltaY;
                }
                if (deltaX > 10 || deltaY > 10)
                {
                    touchMoved = true;
                }
                return false;
            }

            case Event.Info.TouchInfo.END:
            {
                if (!touched ||
                    !touchInertia.TouchEnd(touchX, touchY, touchIndex))
                {
                    return false;
                }

                touched = false;
                if (!touchMoved)
                {
                    int level = GelSelItem(touchX, touchY);
                    if (level >= 0 && level != selectedLevel)
                    {
                        ResetSelect();
                        SetSelect(level);
                    }
                }
                return false;
            }

            default:
                return false;
        }
    }

    /// <summary>
    /// Builds the scrollable level list of a series. Every level gets a GUIBattleItem that is
    /// both a child and an entry of battleItem. Normal series only unlock the level after the
    /// last finished one; series 4 has no progress lock.
    /// </summary>
    public void Init(in GUIRect rectIn, int series)
    {
        rect = rectIn;
        campaignSeriesId = series;
        xDC = 0;

        touchInertia.Init();
        hBottomGap = 0f;
        selectedLevel = -1;
        touched = false;
        xC8 = 0;
        xTouchBegin = 0f;
        yTouchBegin = 0f;
        touchMoved = false;

        // item height, vertical spacing and selection offset depend on the wide layout
        if (ecGraphics.Instance().widthMode == 3)
        {
            hBattleItem = 80f;
            dyBattleItem = 90f;
            selectedOffset = 40f;
        }
        else
        {
            hBattleItem = 40f;
            dyBattleItem = 45f;
            selectedOffset = 20f;
        }

        int numBattles = GetNumBattles(series);
        int completedBattles = numBattles;
        int unlockedBattles = numBattles;
        if (series != 4)
        {
            completedBattles = g_Commander.GetNumPlayedBatttles(series);
            // until the series is finished the level after the last completed one is open too
            unlockedBattles = completedBattles < numBattles
                              ? completedBattles + 1
                              : numBattles;
        }

        for (int level = 0; level < numBattles; ++level)
        {
            var itemRect = new GUIRect(0f, level * dyBattleItem, rect.w, hBattleItem);

            var item = new GUIBattleItem();
            item.Init(series, level, itemRect);
            AddChild(item, true);
            battleItem.Add(item);

            item.rank = g_Commander.GetNumBattleStars(series, level);
            if (level >= unlockedBattles)
            {
                item.SetEnable(false);
                item.locked = true;
            }
        }

        // only an unfinished normal series scrolls the last unlocked level into view
        if (series != 4 && completedBattles < numBattles)
        {
            float lastUnlockedBottom = unlockedBattles * dyBattleItem - (dyBattleItem - hBattleItem);
            hBottomGap = lastUnlockedBottom <= rect.h
                       ? 0f
                       : rect.h - lastUnlockedBottom;

            float itemY = hBottomGap;
            foreach (GUIBattleItem item in battleItem)
            {
                item.GetPos(out float itemX, out float unusedY);
                item.SetPos(itemX, itemY);
                itemY += dyBattleItem;
            }
        }
    }
}
