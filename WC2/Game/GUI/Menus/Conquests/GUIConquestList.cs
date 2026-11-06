namespace WC2;

/// <summary>Paged conquest list of the main menu; the layout lives here, the paging in GUIPagedList (gui/menus/conquests/GUIConquestList.cpp).</summary>
public sealed class GUIConquestList : GUIPagedList
{
    public List<GUIButton> buttons = new();
    public int conquestCount;
    public bool wideLayout;
    public float itemHeight;
    public float rowSpacing;
    public int itemsPerPage = 1;

    public GUIConquestList()
    {
        conquestCount = 0;
        wideLayout = false;
        itemHeight = 0f;
        rowSpacing = 0f;
        itemsPerPage = 1;
    }

    /// <summary>
    /// Initializes the conquest entries and the two column layout.
    /// </summary>
    /// <param name="rectIn">position and size of the list area</param>
    /// <param name="count">number of loaded conquests</param>
    /// <param name="wide">iPad / HD doubled layout</param>
    public void Init(in GUIRect rectIn, int count, bool wide)
    {
        wideLayout = wide;
        conquestCount = Math.Max(0, count);

        float buttonWidth = wide ? 308f : 154f;
        itemHeight = wide ? 94f : 47f;
        rowSpacing = wide ? 120f : 59f;

        // the conquest entries keep the original four rows / two columns, at most eight per page
        itemsPerPage = 8;
        // the page button resources, sizes and the side reserves are managed by GUIPagedList
        base.Init(rectIn, itemsPerPage);

        buttons.Clear();

        GUIManager manager = GUIManager.Instance();
        for (int index = 0; index < conquestCount; ++index)
        {
            string imageName = Sprintf("button_conquest%d.png", index + 1);
            // new conquests without an own icon fall back to the first conquest icon
            if (s_TextureRes.GetImage(imageName) == null)
                imageName = "button_conquest1.png";

            int column = index % 2;
            int row = index / 2;
            float x = wide ? (column == 0 ? 12f : 464f)
                           : (column == 0 ? 10f : 240f);
            var buttonRect = new GUIRect(x, rowSpacing * row, buttonWidth, itemHeight);
            GUIButton button = manager.AddButton(imageName, imageName, buttonRect, pageContent, null);
            button.greyEnable = true;
            buttons.Add(button);
            RegisterPageItem(button);
        }

        // the page button resources and base positions belong to GUIPagedList; only the conquest
        // specific content centering and the arrow texture shift are adjusted here
        if (HasPageButtons())
        {
            // center the page buttons on the real content height of the four rows, not on the container
            float contentHeight = rowSpacing * 3f + itemHeight;
            CenterPageButtons(contentHeight);

            // the icons carry their own anchor; only the arrow picture moves, the hit area stays
            float arrowShift = wide ? 2f : 1f;
            ShiftPageButtonIcons(arrowShift);
        }

        LayoutButtons();
    }

    /// <summary>Returns the 0-based conquest id of the clicked widget, or -1 when it is no conquest button.</summary>
    public int GetConquestId(GUIElement element)
    {
        if (element == null)
            return -1;
        for (int index = 0; index < buttons.Count; ++index)
        {
            if (ReferenceEquals(buttons[index], element))
                return index;
        }
        return -1;
    }

    /// <summary>Horizontal gap between the page buttons and the conquest entries for the current layout.</summary>
    protected override float GetPageButtonGap()
    {
        return wideLayout ? 4f : 2f;
    }

    /// <summary>Returns the button of the conquest or null for an invalid id.</summary>
    public GUIButton GetButton(int conquestId)
    {
        if (conquestId < 0 || conquestId >= buttons.Count)
            return null;
        return buttons[conquestId];
    }

    public int GetConquestCount()
    {
        return conquestCount;
    }

    /// <summary>Recomputes the two column positions of the conquest buttons for the current page.</summary>
    private void LayoutButtons()
    {
        for (int index = 0; index < buttons.Count; ++index)
        {
            GUIButton button = buttons[index];
            if (button == null)
                continue;
            int localIndex = index % itemsPerPage;
            int column = localIndex % 2;
            int row = localIndex / 2;
            float x = wideLayout
                      ? (column == 0 ? 12f : 464f)
                      : (column == 0 ? 10f : 240f);
            button.SetPos(x, rowSpacing * row);
        }
    }
}
