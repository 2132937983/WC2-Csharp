namespace WC2;

/// <summary>Vertical scrolling container with touch inertia (port of GUIScrollList.cpp).</summary>
public class GUIScrollList : GUIElement
{
    public GUIElement content;
    public readonly CTouchInertia touchInertia = new();
    public float contentHeight;
    public float offsetY;
    public float touchY;
    public bool touched;
    public bool moved;
    public bool contentTouch;

    public GUIScrollList() { }

    public void Init(in GUIRect rectIn, float contentHeightIn)
    {
        rect = rectIn;
        contentHeight = Math.Max(contentHeightIn, rectIn.h);
        offsetY = 0f;
        touchY = 0f;
        touched = false;
        moved = false;
        touchInertia.Init();

        content = new GUIElement();
        content.rect = new GUIRect(0f, 0f, rectIn.w, contentHeight);
        AddChild(content, true);
    }

    public override void OnRender() => base.OnRender();

    public void SetOffset(float offsetYIn)
    {
        float minOffset = Math.Min(0f, rect.h - contentHeight);
        offsetY = Math.Max(minOffset, Math.Min(0f, offsetYIn));
        content?.SetPos(0f, offsetY);
    }

    public override void OnUpdate(float deltaTimeS)
    {
        touchInertia.Update(deltaTimeS);
        if (touchInertia.GetSpeed(out float speedX, out float speedY))
            SetOffset(offsetY + speedY * deltaTimeS);

        if (!touched && offsetY < 0f)
        {
            float minOffset = Math.Min(0f, rect.h - contentHeight);
            if (offsetY < minOffset)
                SetOffset(offsetY + (minOffset - offsetY) * Math.Min(1f, deltaTimeS * 8f));
        }
    }

    public override bool OnEvent(Event evt)
    {
        if (evt.type != Event.TOUCH) return base.OnEvent(evt);

        float x = evt.info.touch.pos[0];
        float y = evt.info.touch.pos[1];
        int index = evt.info.touch.index;
        switch (evt.info.touch.type)
        {
            case Event.Info.TouchInfo.BEGIN:
                // (the children were already offered the press by PostEvent)
                if (!CheckInRect(x, y)) return base.OnEvent(evt);
                touchInertia.TouchBegin(x, y, index);
                touchY = y;
                touched = true;
                moved = false;
                return true;
            case Event.Info.TouchInfo.MOVE:
                if (touched && touchInertia.TouchMove(x, y, index))
                {
                    SetOffset(offsetY + y - touchY);
                    if (Math.Abs(y - touchY) > 2f) moved = true;
                    touchY = y;
                }
                return true;
            case Event.Info.TouchInfo.END:
                if (touched) touchInertia.TouchEnd(x, y, index);
                touched = false;
                if (!moved) return base.OnEvent(evt);
                moved = false;
                return true;
            default:
                return true;
        }
    }
}

/// <summary>Generic paged list: handles page state and item visibility (port of GUIPagedList.cpp).</summary>
public class GUIPagedList : GUIElement
{
    protected readonly List<GUIElement> pageItems = new();
    protected GUIElement pageContent;
    protected int pageSize = 1;
    protected int currentPage;
    protected GUIButtonEx previousPageButton;
    protected GUIButtonEx nextPageButton;

    public GUIPagedList() { }

    private static string GetPageButtonBackground()
    {
        if (s_TextureRes.GetImage("green_normal.png") != null) return "green_normal.png";
        if (s_TextureRes.GetImage("buttonboard_green.png") != null) return "buttonboard_green.png";
        return null;
    }

    private static ecImageAttr GetPageButtonAttr()
    {
        string background = GetPageButtonBackground();
        return background == null ? null : s_TextureRes.GetImage(background);
    }

    protected virtual float GetPageButtonGap() => 0f;

    public virtual void InitPageContent(in GUIRect rectIn)
    {
        if (pageContent == null)
        {
            pageContent = new GUIElement();
            AddChild(pageContent, true);
        }
        pageContent.rect = rectIn;
        if (previousPageButton != null) MoveToFront(previousPageButton);
        if (nextPageButton != null) MoveToFront(nextPageButton);
    }

    public void Init(in GUIRect rectIn, int pageSizeIn, bool reserveButtonSpace = true)
    {
        pageItems.Clear();
        pageSize = Math.Max(1, pageSizeIn);
        currentPage = 0;
        previousPageButton = null;
        nextPageButton = null;

        var pageButtonAttr = GetPageButtonAttr();
        float contentInset = reserveButtonSpace && pageButtonAttr != null ? pageButtonAttr.w + GetPageButtonGap() : 0f;
        rect = rectIn;
        if (reserveButtonSpace)
        {
            rect.x -= contentInset;
            rect.w += contentInset * 2f;
        }
        if (pageButtonAttr != null && pageButtonAttr.w > 0f && pageButtonAttr.h > 0f &&
            s_TextureRes.GetImage("buttontext_prev.png") != null &&
            s_TextureRes.GetImage("buttontext_next.png") != null)
        {
            InitPageButtonsAtSides(0f, pageButtonAttr.w, pageButtonAttr.h);
        }

        var contentRect = new GUIRect(contentInset, 0f, rectIn.w, rectIn.h);
        InitPageContent(contentRect);
    }

    public void RegisterPageItem(GUIElement item)
    {
        if (item == null || pageItems.Contains(item)) return;
        GUIElement contentParent = pageContent ?? this;
        if (item.parent != contentParent) contentParent.AddChild(item, true);
        pageItems.Add(item);
        RefreshPageItems();
    }

    public void InitPageButtons(in GUIRect previousRect, in GUIRect nextRect)
    {
        string bg = GetPageButtonBackground();
        if (previousPageButton == null)
        {
            previousPageButton = new GUIButtonEx();
            previousPageButton.Init(bg, bg, previousRect, null);
            previousPageButton.SetImageText("buttontext_prev.png", "buttontext_prev_press.png");
            AddChild(previousPageButton, true);
        }
        else previousPageButton.rect = previousRect;
        if (nextPageButton == null)
        {
            nextPageButton = new GUIButtonEx();
            nextPageButton.Init(bg, bg, nextRect, null);
            nextPageButton.SetImageText("buttontext_next.png", "buttontext_next_press.png");
            AddChild(nextPageButton, true);
        }
        else nextPageButton.rect = nextRect;
        MoveToFront(previousPageButton);
        MoveToFront(nextPageButton);
        RefreshPageItems();
    }

    public void InitPageButtonsAtSides(float gap, float buttonWidth, float buttonHeight)
    {
        float centerY = (rect.h - buttonHeight) * 0.5f;
        var previousRect = new GUIRect(0f, centerY, buttonWidth, buttonHeight);
        var nextRect = new GUIRect(rect.w - buttonWidth, centerY, buttonWidth, buttonHeight);
        InitPageButtons(previousRect, nextRect);
    }

    protected bool HasPageButtons() => previousPageButton != null && nextPageButton != null;

    protected float GetPageButtonHeight() => HasPageButtons() ? previousPageButton.rect.h : 0f;

    protected void CenterPageButtons(float contentHeightIn)
    {
        if (!HasPageButtons()) return;
        float buttonY = (contentHeightIn - GetPageButtonHeight()) * 0.5f;
        previousPageButton.SetPos(previousPageButton.rect.x, buttonY);
        nextPageButton.SetPos(nextPageButton.rect.x, buttonY);
    }

    protected void ShiftPageButtonIcons(float shift)
    {
        if (!HasPageButtons()) return;
        ecImage[] images = { previousPageButton.imageNotPressed, previousPageButton.imagePressed, nextPageButton.imageNotPressed, nextPageButton.imagePressed };
        foreach (var image in images)
            if (image != null) image.textureRect.refx += shift;
    }

    public void SetPage(int page)
    {
        int pageCount = GetPageCount();
        int lastPage = Math.Max(0, pageCount - 1);
        currentPage = Math.Max(0, Math.Min(page, lastPage));
        RefreshPageItems();
    }

    public bool HandlePageButton(GUIElement element)
    {
        if (element == null) return false;
        if (ReferenceEquals(element, previousPageButton)) { SetPage(currentPage - 1); return true; }
        if (ReferenceEquals(element, nextPageButton)) { SetPage(currentPage + 1); return true; }
        return false;
    }

    public int GetCurrentPage() => currentPage;

    public int GetPageCount() => pageItems.Count == 0 ? 0 : (pageItems.Count + pageSize - 1) / pageSize;

    public override bool OnEvent(Event evt)
    {
        if (evt.type == Event.GUI && evt.info.gui.type == Event.Info.GUIInfo.BUTTON && HandlePageButton(evt.info.gui.ptr))
            return true;
        return base.OnEvent(evt);
    }

    protected void RefreshPageItems()
    {
        int firstItem = currentPage * pageSize;
        int lastItem = firstItem + pageSize;
        for (int index = 0; index < pageItems.Count; index++)
        {
            var item = pageItems[index];
            if (item == null) continue;
            if (index >= firstItem && index < lastItem) item.Show(); else item.Hide();
        }
        bool hasPrevious = currentPage > 0;
        bool hasNext = currentPage + 1 < GetPageCount();
        if (previousPageButton != null) { if (hasPrevious) previousPageButton.Show(); else previousPageButton.Hide(); }
        if (nextPageButton != null) { if (hasNext) nextPageButton.Show(); else nextPageButton.Hide(); }
    }
}

/// <summary>Row of blocks used as a discrete level selector (port of GUILevelSel.cpp).</summary>
public class GUILevelSel : GUIElement
{
    public ecImage imageBlock;
    public int currentLevel;
    public int totalLevel;

    public GUILevelSel() { }

    public void Init(in GUIRect rectIn, int totalLevelIn)
    {
        rect = rectIn;
        totalLevel = Math.Max(0, totalLevelIn);
        currentLevel = 0;
        var attr = s_TextureRes.GetImage("block.png");
        imageBlock = attr != null ? new ecImage(attr) : null;
    }

    public override void OnRender()
    {
        if (imageBlock == null) return;
        GetAbsRect(out var abs);
        float x = abs.x;
        if (Layout.Tablet)
        {
            for (int i = 0; i <= currentLevel; i++) { imageBlock.Render(x, abs.y); x += 40f; }
        }
        else
        {
            float phoneY = abs.y + 5f;
            for (int i = 0; i <= currentLevel; i++) { imageBlock.Render(x, phoneY); x += 20f; }
        }
    }

    public override bool OnEvent(Event evt)
    {
        if (evt.type != Event.TOUCH) return base.OnEvent(evt);
        if ((flags & (ACTIVE | SHOWN)) != (ACTIVE | SHOWN)) return false;
        if ((flags & ENABLED) == 0) return false;

        float x = evt.info.touch.pos[0], y = evt.info.touch.pos[1];
        var touchType = evt.info.touch.type;
        if (touchType == Event.Info.TouchInfo.BEGIN)
        {
            if (!CheckInRect(x, y)) return base.OnEvent(evt);
            GetAbsRect(out var abs);
            currentLevel = Math.Clamp((int)(totalLevel * (x - abs.x) / abs.w), 0, Math.Max(0, totalLevel - 1));
            return true;
        }
        if (touchType == Event.Info.TouchInfo.MOVE && CheckInRect(x, y))
        {
            GetAbsRect(out var abs);
            currentLevel = Math.Clamp((int)(totalLevel * (x - abs.x) / abs.w), 0, Math.Max(0, totalLevel - 1));
        }
        return base.OnEvent(evt);
    }
}
