namespace WC2;

/// <summary>
/// Base of every widget: a rectangle relative to the parent, a doubly linked child list and
/// the three virtual callbacks (OnEvent / OnRender / OnUpdate).
///
/// Event routing follows the original libeasytech binary exactly:
/// <list type="bullet">
/// <item><see cref="PostEvent"/> walks the tree depth first (first child first); the element's own
/// <see cref="OnEvent"/> only runs when none of its children consumed the event.</item>
/// <item>The base <see cref="OnEvent"/> bubbles GUI events (button presses, scroll bars, custom OTHER
/// events, fades) to the parent and ignores everything else; the GUIManager hands them to the state.</item>
/// <item>The first child is the top-most one: it is rendered last and receives events first.</item>
/// </list>
/// </summary>
public class GUIElement
{
    public GUIElement parent;
    public GUIElement firstChild;
    public GUIElement lastChild;
    public GUIElement previousSibling;
    public GUIElement nextSibling;
    public int childCount;
    public GUIRect rect;
    public uint flags;
    public uint handle;

    /// <summary>The element draws itself (SetVisible toggles this bit; the children are unaffected).</summary>
    public const uint ACTIVE = 1u << 16;
    /// <summary>The element and its subtree are rendered and receive events (Show / Hide).</summary>
    public const uint SHOWN = 1u << 17;
    /// <summary>Enabled: buttons react to touches and render in normal colors.</summary>
    public const uint ENABLED = 1u << 18;

    /// <summary>Shared texture atlas used by GUI widgets (GUIManager.LoadTextureRes fills it).</summary>
    public static readonly ecTextureRes s_TextureRes = new();

    private static uint s_nextHandle;
    private bool disposed;

    public GUIElement()
    {
        flags = ACTIVE | SHOWN | ENABLED;
        handle = ++s_nextHandle;
        // the original constructor starts every element with the classic 480x320 screen rectangle
        rect = new GUIRect(0f, 0f, 480f, 320f);
    }

    public bool IsShown => (flags & SHOWN) != 0;
    public bool IsEnabled => (flags & ENABLED) != 0;
    public bool IsDisposed => disposed;

    // ------------------------------------------------------------------------------------
    // Virtuals
    // ------------------------------------------------------------------------------------

    /// <summary>
    /// Default handler: GUI events bubble to the parent, touch and back events are not consumed.
    /// (Children are visited by <see cref="PostEvent"/> before this is called.)
    /// </summary>
    public virtual bool OnEvent(Event evt)
    {
        if (evt == null) return false;
        if (evt.type == Event.GUI)
            return parent != null && parent.OnEvent(evt);
        return false;
    }

    public virtual void OnRender() { }

    public virtual void OnUpdate(float deltaTimeS) { }

    /// <summary>Destructor equivalent: releases children and owned resources.</summary>
    public virtual void Dispose()
    {
        if (disposed) return;
        disposed = true;
        FreeAllChild();
        parent?.RemoveChild(this);
    }

    // ------------------------------------------------------------------------------------
    // Tree
    // ------------------------------------------------------------------------------------

    /// <summary>
    /// Adds a child. lastChild = true appends it at the tail (drawn first, i.e. below its
    /// siblings); lastChild = false inserts it at the head (top-most, gets events first).
    /// </summary>
    public void AddChild(GUIElement child, bool lastChildIn = true)
    {
        if (child == null) return;
        if (child.parent != null) child.parent.RemoveChild(child);
        child.parent = this;
        if (lastChildIn || firstChild == null)
        {
            child.previousSibling = lastChild;
            child.nextSibling = null;
            if (lastChild != null) lastChild.nextSibling = child;
            lastChild = child;
            if (firstChild == null) firstChild = child;
        }
        else
        {
            child.nextSibling = firstChild;
            child.previousSibling = null;
            firstChild.previousSibling = child;
            firstChild = child;
        }
        childCount++;
    }

    public bool RemoveChild(GUIElement child)
    {
        if (child == null || child.parent != this) return false;
        if (child.previousSibling != null) child.previousSibling.nextSibling = child.nextSibling;
        else firstChild = child.nextSibling;
        if (child.nextSibling != null) child.nextSibling.previousSibling = child.previousSibling;
        else lastChild = child.previousSibling;
        child.parent = null;
        child.previousSibling = null;
        child.nextSibling = null;
        childCount--;
        return true;
    }

    /// <summary>Removes the child and destroys it.</summary>
    public virtual void FreeChild(GUIElement child)
    {
        if (child == null) return;
        if (child.parent == this) RemoveChild(child);
        child.Dispose();
    }

    public virtual void FreeAllChild()
    {
        while (firstChild != null)
        {
            var child = firstChild;
            RemoveChild(child);
            child.Dispose();
        }
    }

    /// <summary>
    /// Moves the child to the head of its parent's list so it renders on top and gets events
    /// first. Like the original, the whole subtree is searched for the element.
    /// </summary>
    public bool MoveToFront(GUIElement child)
    {
        if (child == null) return false;
        for (var c = firstChild; c != null; c = c.nextSibling)
        {
            if (c.MoveToFront(child)) return true;
            if (ReferenceEquals(c, child))
            {
                if (!ReferenceEquals(firstChild, c))
                {
                    RemoveChild(c);
                    AddChild(c, false);
                }
                return true;
            }
        }
        return false;
    }

    /// <summary>Moves the child to the tail of the list so it renders below its siblings.</summary>
    public bool MoveToBack(GUIElement child)
    {
        if (child == null || child.parent != this) return false;
        if (lastChild == child) return true;
        RemoveChild(child);
        AddChild(child, true);
        return true;
    }

    public void GetPos(out float x, out float y) { x = rect.x; y = rect.y; }

    public void SetPos(float x, float y) { rect.x = x; rect.y = y; }

    public void Move(float x, float y) { rect.x += x; rect.y += y; }

    /// <summary>Centers the element in its parent (or on the screen when it has no parent).</summary>
    public void Center()
    {
        float pw, ph;
        if (parent != null) { pw = parent.rect.w; ph = parent.rect.h; }
        else { pw = ecGraphics.Instance().screenW; ph = ecGraphics.Instance().screenH; }
        rect.x = (pw - rect.w) * 0.5f;
        rect.y = (ph - rect.h) * 0.5f;
    }

    public void GetAbsRect(out GUIRect absRect)
    {
        float x = rect.x, y = rect.y;
        for (var p = parent; p != null; p = p.parent) { x += p.rect.x; y += p.rect.y; }
        absRect = new GUIRect(x, y, rect.w, rect.h);
    }

    public GUIRect GetAbsRect()
    {
        GetAbsRect(out var r);
        return r;
    }

    /// <summary>
    /// Hit test: the point is inside the element's absolute rectangle and on the logical screen.
    /// The original hides several controls by moving them off the screen; where the window is wider
    /// or taller than the layout (letterbox bars, other phone aspect ratios) that area is reachable,
    /// so the part of a control outside the screen never counts as hit.
    /// </summary>
    public bool CheckInRect(float x, float y)
    {
        GetAbsRect(out var r);
        return r.Contains(x, y) && IsOnScreen(x, y);
    }

    public bool CheckInRect(float x, float y, in GUIRect rectIn) => rectIn.Contains(x, y) && IsOnScreen(x, y);

    /// <summary>True when the logical point lies on the logical screen (0..screenW, 0..screenH).</summary>
    public static bool IsOnScreen(float x, float y)
    {
        var g = ecGraphics.Instance();
        return x >= 0f && y >= 0f && x < g.screenW && y < g.screenH;
    }

    /// <summary>
    /// True when a touch at (x, y) can reach this element: it and every ancestor are shown and
    /// active, it is enabled, and the point passes <see cref="CheckInRect(float, float)"/>.
    /// </summary>
    public bool AcceptsInputAt(float x, float y)
    {
        for (var p = this; p != null; p = p.parent)
            if ((p.flags & (ACTIVE | SHOWN)) != (ACTIVE | SHOWN)) return false;
        return IsEnabled && CheckInRect(x, y);
    }

    /// <summary>
    /// Toggles the ACTIVE bit: an invisible element does not draw itself and blocks events,
    /// but its children are still rendered (original semantics; use Show/Hide for the subtree).
    /// </summary>
    public void SetVisible(bool visible)
    {
        if (visible) flags |= ACTIVE; else flags &= ~ACTIVE;
    }

    public void Show() => flags |= SHOWN;
    public void Hide() => flags &= ~SHOWN;

    public virtual void SetEnable(bool enable)
    {
        if (enable) flags |= ENABLED; else flags &= ~ENABLED;
    }

    public GUIElement FindByHandle(uint handleIn)
    {
        if (handle == handleIn) return this;
        for (var c = firstChild; c != null; c = c.nextSibling)
        {
            var found = c.FindByHandle(handleIn);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>
    /// Offers the event to the subtree: children first (top-most first), then this element's
    /// own <see cref="OnEvent"/>. Elements that are not ACTIVE and SHOWN ignore the event.
    /// </summary>
    public virtual bool PostEvent(Event evt)
    {
        if (evt == null) return false;
        if ((flags & (ACTIVE | SHOWN)) != (ACTIVE | SHOWN)) return false;
        for (var child = firstChild; child != null;)
        {
            var next = child.nextSibling;
            if (!child.disposed && child.PostEvent(evt)) return true;
            child = next;
        }
        return OnEvent(evt);
    }

    public virtual void Render()
    {
        if ((flags & SHOWN) == 0) return;
        if ((flags & ACTIVE) != 0) OnRender();
        // children are drawn from the last to the first one, so the first child ends up on top
        for (var c = lastChild; c != null;)
        {
            var prev = c.previousSibling;
            c.Render();
            c = prev;
        }
    }

    public virtual void Update(float deltaTimeS)
    {
        // the original updates the children before the element itself
        for (var c = firstChild; c != null;)
        {
            var next = c.nextSibling;
            if (!c.disposed) c.Update(deltaTimeS);
            c = next;
        }
        OnUpdate(deltaTimeS);
    }

    public IEnumerable<GUIElement> Children()
    {
        for (var c = firstChild; c != null; c = c.nextSibling) yield return c;
    }
}
