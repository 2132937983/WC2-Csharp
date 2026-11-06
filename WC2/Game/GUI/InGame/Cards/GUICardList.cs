namespace WC2;

/// <summary>Horizontal scrolling card list of one tab (GUICardList.cpp).</summary>
public sealed class GUICardList : GUIElement
{
    /// <summary>The cards are managed by the GUIElement child tree; the list only keeps the references.</summary>
    public readonly List<GUICard> guiCard = new();
    public float x40;
    public float cardWidth;
    public int tab;
    public float x;
    public int selectedItem;
    public readonly CTouchInertia touchInertia = new();
    public bool touched;
    public float xTouch;
    public float yTouch;
    public int xC8;
    public float xTouchBegin;
    public float yTouchBegin;
    public bool touchMoved;

    public GUICardList() { }

    /// <summary>
    /// Initializes the horizontal card list of a tab. The x of each card accumulates cardWidth;
    /// the touch inertia fields are used by the scrolling logic.
    /// </summary>
    public void Init(in GUIRect Rect, int Tab)
    {
        rect.x = Rect.x;
        rect.y = Rect.y;
        rect.w = Rect.w;
        rect.h = Rect.h;
        tab = Tab;
        x40 = 100.0f;
        // wide and normal screens use slightly different card steps so the last card stays in the scroll range
        if (ecGraphics.Instance().widthMode != 3)
            cardWidth = 113.0f;
        else
            cardWidth = 110.0f;

        // enum entries without a CardDef are skipped, no GUI is created for unimplemented cards
        var cardIds = GUICardTab.AllCardTab[Tab];
        for (int i = 0; i < cardIds.Count; i++)
        {
            CARD_ID cardId = cardIds[i];
            CardDef tmpCardDef = CObjectDef.Instance().GetCardDef(cardId);
            if (tmpCardDef != null)
            {
                var tmp_card = new GUICard();
                var tmp_rect = new GUIRect(i * cardWidth, 0.0f, 100.0f, 150.0f);
                tmp_card.Init(tmpCardDef, tmp_rect);
                AddChild(tmp_card, true);
                guiCard.Add(tmp_card);
            }
        }

        touchInertia.Init();
        selectedItem = -1;
        touched = false;
        touchMoved = false;
        x = 0.0f;
        xC8 = 0;
        xTouchBegin = 0.0f;
        yTouchBegin = 0.0f;
    }

    /// <summary>Horizontal drag and tap selection of the list.</summary>
    public override bool OnEvent(Event evt)
    {
        if (evt.type != Event.TOUCH)
            return base.OnEvent(evt);

        if ((flags & (ACTIVE | SHOWN)) != (ACTIVE | SHOWN) || (flags & ENABLED) == 0)
            return false;

        float touchX = evt.info.touch.pos[0];
        float touchY = evt.info.touch.pos[1];
        int touchIndex = evt.info.touch.index;

        // the cards (children) see the touch first, like the original dispatcher; the list logic follows
        bool childHandled = base.OnEvent(evt);

        switch (evt.info.touch.type)
        {
            case Event.Info.TouchInfo.BEGIN:
                if (!CheckInRect(touchX, touchY))
                    return childHandled;

                if (ecMultipleTouch.Instance().touch.Count == 1)
                    touchInertia.Init();
                if (touchInertia.TouchBegin(touchX, touchY, touchIndex))
                {
                    xTouch = touchX;
                    yTouch = touchY;
                    touched = true;
                    touchMoved = false;
                    xTouchBegin = touchX;
                    yTouchBegin = touchY;
                }
                return true;

            case Event.Info.TouchInfo.MOVE:
                if (touched && touchInertia.TouchMove(touchX, touchY, touchIndex))
                {
                    x += touchX - xTouch;
                    xTouch = touchX;
                    yTouch = touchY;

                    int deltaX = (int)(xTouchBegin - touchX);
                    if (deltaX < 0) deltaX = -deltaX;
                    int deltaY = (int)(yTouchBegin - touchY);
                    if (deltaY < 0) deltaY = -deltaY;
                    if (deltaX > 10 || deltaY > 10)
                        touchMoved = true;
                }
                break;

            case Event.Info.TouchInfo.END:
                if (touched && touchInertia.TouchEnd(touchX, touchY, touchIndex))
                {
                    touched = false;
                    if (!touchMoved)
                    {
                        int selectedIndex = GelSelItem(touchX, touchY);
                        if (selectedIndex >= 0 && selectedItem != selectedIndex)
                        {
                            ResetSelect();
                            SetSelect(selectedIndex);
                        }
                    }
                }
                break;
        }

        return childHandled;
    }

    /// <summary>Horizontal inertia, overscroll spring back and card positions.</summary>
    public override void OnUpdate(float deltaTimeS)
    {
        touchInertia.Update(deltaTimeS);

        if (touchInertia.GetSpeed(out float speedX, out float speedY))
            x += deltaTimeS * speedX;

        if (!touched)
        {
            if (x > 0.0f)
            {
                float correction = deltaTimeS * x * 5.0f;
                if (correction < 0.4f) correction = 0.4f;
                x -= correction;
                if (x < 0.0f) x = 0.0f;
            }

            float contentRight = guiCard.Count * cardWidth - (cardWidth - x40);
            float visibleRight = contentRight <= rect.w ? contentRight : rect.w;

            if (x + contentRight < visibleRight)
            {
                float correction = deltaTimeS * (visibleRight - x - contentRight) * 5.0f;
                if (correction < 0.4f) correction = 0.4f;

                float nextX = x + correction;
                x = nextX + contentRight <= visibleRight ? nextX : visibleRight - contentRight;
            }
        }

        // the wide layout keeps the fixed card slots of Init; the original only scrolls on normal screens
        if (ecGraphics.Instance().widthMode != 3)
        {
            float cardX = x;
            foreach (GUICard card in guiCard)
            {
                card.GetPos(out _, out float cardY);
                card.SetPos(cardX, cardY);
                cardX += cardWidth;
            }
        }
    }

    /// <summary>Clears the current selection and its shadow.</summary>
    public void ResetSelect()
    {
        if (selectedItem >= 0)
        {
            if (selectedItem < guiCard.Count) guiCard[selectedItem].showShadow = false;
            selectedItem = -1;
        }
    }

    /// <summary>The card at the index of this tab, or null when out of range.</summary>
    public GUICard GetCard(int index)
    {
        if (index < 0 || index >= guiCard.Count) return null;
        return guiCard[index];
    }

    /// <summary>The index of the card hit by the touch, or -1.</summary>
    public int GelSelItem(float x, float y)
    {
        for (int index = 0; index < guiCard.Count; ++index)
        {
            if (guiCard[index].CheckInRect(x, y))
                return index;
        }
        return -1;
    }

    /// <summary>Selects the card and sends the selection change event to the buy card page.</summary>
    public void SetSelect(int index)
    {
        selectedItem = index;
        guiCard[index].showShadow = true;
        base.OnEvent(Event.Gui(Event.Info.GUIInfo.OTHER, this, 5));
    }

    /// <summary>Re-applies the current selection and sends the selection event again (kept from the original, no known caller).</summary>
    public void ReSelect()
    {
        int selectedIndex = selectedItem;
        if (selectedIndex >= 0)
        {
            ResetSelect();
            SetSelect(selectedIndex);
        }
    }
}
