namespace WC2;

/// <summary>
/// Country list of the conquest mode, the counterpart of the campaign GUIBattleList
/// (gui/menus/conquests/GUICountryList.cpp).
/// </summary>
public sealed class GUICountryList : GUIElement
{
    public List<GUICountryItem> countryItem = new();
    public float hCountryItem;
    public float dyCountryItem;
    public float selectedOffset;
    public float y;
    public int select;
    public CTouchInertia touchInertia = new CTouchInertia();
    public bool touched;
    public float xTouch;
    public float yTouch;
    public int xC8;
    public float xTouchBegin;
    public float yTouchBegin;
    public bool touchMoved;
    public int conquestId;
    public int xDC;
    public float ensureTargetY;
    public bool ensureScrolling;

    /// <summary>The original only builds the empty item vector and the touch inertia; the scalars are set by Init.</summary>
    public GUICountryList() { }

    /// <summary>countryItem only references the children; the child tree frees them.</summary>
    public override void Dispose()
    {
        if (IsDisposed) return;
        base.Dispose();
    }

    public override void OnUpdate(float deltaTimeS)
    {
        // the inertia only changes the scroll offset y; the horizontal speed is tracked but unused
        touchInertia.Update(deltaTimeS);

        if (touchInertia.GetSpeed(out float speedX, out float speedY))
        {
            y += deltaTimeS * speedY;
        }

        if (ensureScrolling && !touched)
        {
            float difference = ensureTargetY - y;
            float step = difference * Math.Min(1f, deltaTimeS * 10f);
            if (MathF.Abs(difference) < 0.5f)
            {
                y = ensureTargetY;
                ensureScrolling = false;
            }
            else
            {
                y += step;
            }
        }

        if (!touched)
        {
            // top overflow: bounce back proportionally, with a minimum step so it never stalls at low frame rates
            if (y > 0f)
            {
                float correction = deltaTimeS * y * 5f;
                if (correction < 0.4f)
                {
                    correction = 0.4f;
                }
                y -= correction;
                if (y < 0f)
                {
                    y = 0f;
                }
            }

            float contentBottom = countryItem.Count * dyCountryItem - (dyCountryItem - hCountryItem);
            float visibleBottom = contentBottom <= rect.h ? contentBottom : rect.h;

            // bottom overflow: bounce back towards the smallest allowed offset without crossing it
            if (y + contentBottom < visibleBottom)
            {
                float correction = deltaTimeS * (visibleBottom - y - contentBottom) * 5f;
                if (correction < 0.4f)
                {
                    correction = 0.4f;
                }

                float nextY = y + correction;
                y = nextY + contentBottom > visibleBottom
                  ? visibleBottom - contentBottom
                  : nextY;
            }
        }

        // re-layout every item from the current scroll offset; the X of each item is kept
        float itemY = y;
        foreach (GUICountryItem item in countryItem)
        {
            item.GetPos(out float itemX, out float unusedY);
            item.SetPos(itemX, itemY);
            itemY += dyCountryItem;
        }
    }

    /// <summary>Drag scrolling and tap selection of the country list.</summary>
    public override bool OnEvent(Event evt)
    {
        if (evt.type != Event.TOUCH)
        {
            return base.OnEvent(evt);
        }

        // original engine order: the children (country items) see the touch before the list
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

                ensureScrolling = false;

                // a second finger drops the previous inertia track
                if (ecMultipleTouch.Instance().touch.Count != 1)
                {
                    touchInertia.Init();
                }

                if (touchInertia.TouchBegin(touchX, touchY, touchIndex))
                {
                    xTouch = touchX;
                    yTouch = touchY;
                    touched = true;
                    touchMoved = false;
                    xTouchBegin = touchX;
                    yTouchBegin = touchY;
                }
                // a BEGIN inside the list is consumed even when the inertia tracker refused it
                return true;
            }

            case Event.Info.TouchInfo.MOVE:
            {
                if (touched && touchInertia.TouchMove(touchX, touchY, touchIndex))
                {
                    y += touchY - yTouch;
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
                }
                // the original passes MOVE on to the child tree whether handled or not
                return false;
            }

            case Event.Info.TouchInfo.END:
            {
                if (touched && touchInertia.TouchEnd(touchX, touchY, touchIndex))
                {
                    touched = false;
                    if (!touchMoved)
                    {
                        int selectedIndex = GelSelItem(touchX, touchY);
                        if (selectedIndex >= 0 && select != selectedIndex)
                        {
                            ResetSelect();
                            SetSelect(selectedIndex);
                        }
                    }
                }
                // like MOVE, END continues with the default dispatch
                return false;
            }

            default:
                return false;
        }
    }

    /// <summary>Builds the scrollable country list of the given conquest.</summary>
    public void Init(in GUIRect rectIn, int conquestIdIn)
    {
        rect = rectIn;
        conquestId = conquestIdIn;
        xDC = 0;

        touchInertia.Init();
        touched = false;
        select = -1;
        touchMoved = false;
        y = 0f;
        ensureTargetY = 0f;
        ensureScrolling = false;
        xC8 = 0;
        xTouchBegin = 0f;
        yTouchBegin = 0f;

        if (Layout.Tablet)
        {
            hCountryItem = 80f;
            dyCountryItem = 90f;
            selectedOffset = 32f;
        }
        else
        {
            hCountryItem = 40f;
            dyCountryItem = 45f;
            selectedOffset = 16f;
        }

        string battleFileName = GetBattleFileName(2, 0, 0, conquestId);

        // the temporary list is dropped at the end of the function like the original
        var belligerents = new List<Belligerent>();
        GetBattleBelligerentList(battleFileName, belligerents, true);

        int index = 0;
        foreach (Belligerent belligerent in belligerents)
        {
            var itemRect = new GUIRect(0f, index * dyCountryItem, rect.w, hCountryItem);

            var item = new GUICountryItem();
            item.Init(belligerent.name, belligerent.id, itemRect);
            AddChild(item, true);
            countryItem.Add(item);
            ++index;
        }

        // the original assumes at least one selectable country and selects the first one
        SetSelect(0);
    }

    /// <summary>Id of the selected country, or null when nothing is selected.</summary>
    public string GetSelCountryID()
    {
        if (select < 0)
        {
            return null;
        }
        return countryItem[select].id;
    }

    /// <summary>
    /// Selects the given entry and notifies the parent selection screen. The caller clears the
    /// old selection first; this only sets the new entry, applies the horizontal offset and
    /// moves it to the front of the draw order.
    /// </summary>
    public void SetSelect(int index)
    {
        if (index < 0 || index >= countryItem.Count)
        {
            return;
        }
        select = index;

        GUICountryItem item = countryItem[index];
        item.SetSelected(true);

        item.GetPos(out float itemX, out float itemY);
        item.SetPos(itemX - selectedOffset, itemY);
        MoveToFront(item);

        // OTHER/6 = the conquest country selection changed; bubbled to the parent by the base
        var selectEvent = Event.Gui(Event.Info.GUIInfo.OTHER, this, 6);
        base.OnEvent(selectEvent);
    }

    /// <summary>
    /// Scrolls the given entry into the visible area of the list. Used by the map click
    /// selection; SetSelect itself keeps its original meaning.
    /// </summary>
    public void SelectAndEnsureVisible(int index)
    {
        if (index < 0 || index >= countryItem.Count)
        {
            return;
        }

        ResetSelect();
        SetSelect(index);
        CCSoundBox.GetInstance().PlaySE(AudioDefs.Ui("click"));

        float targetY = y;
        float itemTop = targetY + index * dyCountryItem;
        float itemBottom = itemTop + hCountryItem;
        if (itemTop < 0f)
        {
            targetY -= itemTop;
        }
        else if (itemBottom > rect.h)
        {
            targetY -= itemBottom - rect.h;
        }

        float contentBottom = countryItem.Count * dyCountryItem - (dyCountryItem - hCountryItem);
        if (contentBottom <= rect.h)
        {
            targetY = 0f;
        }
        else
        {
            if (targetY > 0f) targetY = 0f;
            if (targetY + contentBottom < rect.h)
            {
                targetY = rect.h - contentBottom;
            }
        }
        ensureTargetY = targetY;
        ensureScrolling = MathF.Abs(ensureTargetY - y) >= 0.5f;
    }

    /// <summary>Clears the expanded display of the selected country and restores its X position.</summary>
    public void ResetSelect()
    {
        if (select < 0)
        {
            return;
        }

        GUICountryItem item = countryItem[select];
        item.SetSelected(false);

        item.GetPos(out float itemX, out float itemY);
        item.SetPos(itemX + selectedOffset, itemY);

        select = -1;
    }

    /// <summary>Finds the entry under the given point, or -1.</summary>
    public int GelSelItem(float x, float yIn)
    {
        for (int index = 0; index < countryItem.Count; ++index)
        {
            if (countryItem[index].CheckInRect(x, yIn))
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
}
