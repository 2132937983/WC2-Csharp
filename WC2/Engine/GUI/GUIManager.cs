namespace WC2;

/// <summary>
/// Root of the GUI tree. Owns the screen fade, forwards every unconsumed event to the current
/// state and provides factory helpers for the common widgets. Behaviour follows the original
/// binary: fades run at 2.5 per second, a finished fade out leaves the screen black and input
/// blocked (fading == 3) until FadeIn is called, and the fade overlay is drawn above the fade.
/// </summary>
public sealed class GUIManager : GUIElement
{
    private static GUIManager s_instance;
    public static GUIManager Instance() => s_instance ??= new GUIManager();

    public readonly List<GUIElement> freeList = new();
    public IEventReceiver currentState;
    /// <summary>Element drawn above the fade (loading hint); not part of the tree.</summary>
    public GUIElement fadeOverlay;
    /// <summary>Darkness of the screen, 0 = clear, 1 = black.</summary>
    public float fadeTimer;
    /// <summary>0 = idle, 1 = fading in, 2 = fading out, 3 = faded out (black, waiting for FadeIn).</summary>
    public int fading;
    public int fadeInfo;

    /// <summary>Fade speed of the original engine (full fade in 0.4 s).</summary>
    public const float FadeSpeed = 2.5f;

    private readonly Queue<Event> pendingEvents = new();

    private GUIManager()
    {
        flags = ACTIVE | SHOWN | ENABLED;
    }

    public void Init(in GUIRect screenRect)
    {
        rect = screenRect;
    }

    public void LoadTextureRes(string name, bool hd = false) => s_TextureRes.LoadRes(name, hd);
    /// <summary>Like LoadTextureRes but reports whether the atlas existed.</summary>
    public bool LoadTextureResChecked(string name, bool hd = false) => s_TextureRes.LoadRes(name, hd);
    public void UnloadTextureRes(string name) => s_TextureRes.UnloadRes(name);
    public void ReleaseTexureRes() => s_TextureRes.Release();

    // ------------------------------------------------------------------------------------
    // Fades
    // ------------------------------------------------------------------------------------

    /// <summary>Fades from black to the scene; posts FADE_IN(info) when done (the overlay is released then).</summary>
    public void FadeIn(int info)
    {
        fadeInfo = info;
        fading = 1;
        fadeTimer = 1f;
    }

    /// <summary>Fades to black; posts FADE_OUT(info) when done. The overlay (if any) is drawn above the fade until the next fade in completes.</summary>
    public void FadeOut(int info, GUIElement overlay)
    {
        fadeTimer = 0f;
        fading = 2;
        fadeInfo = info;
        if (fadeOverlay != null && !ReferenceEquals(fadeOverlay, overlay))
            ReleaseOverlay();
        fadeOverlay = overlay;
    }

    private void ReleaseOverlay()
    {
        var overlay = fadeOverlay;
        fadeOverlay = null;
        if (overlay == null) return;
        freeList.Remove(overlay);
        overlay.Dispose();
    }

    public bool IsFading => fading != 0;

    // ------------------------------------------------------------------------------------
    // Events
    // ------------------------------------------------------------------------------------

    /// <summary>
    /// Entry point for host generated events. While a fade is running (or the screen is still
    /// black after a fade out) every event is swallowed; otherwise the tree is searched and the
    /// current state is the last receiver.
    /// </summary>
    public override bool PostEvent(Event evt)
    {
        if (evt == null) return false;
        if (fading != 0) return true;
        return base.PostEvent(evt);
    }

    /// <summary>Every event that reaches the root (bubbled GUI events or unconsumed touches) goes to the current state.</summary>
    public override bool OnEvent(Event evt)
    {
        if (evt == null) return false;
        return currentState != null && currentState.OnEvent(evt);
    }

    /// <summary>Queues a GUI event that is delivered at the start of the next update.</summary>
    public void QueueEvent(Event evt)
    {
        if (evt != null) pendingEvents.Enqueue(evt);
    }

    // ------------------------------------------------------------------------------------
    // Frame
    // ------------------------------------------------------------------------------------

    public override void Render()
    {
        var graphics = ecGraphics.Instance();
        graphics.SetViewPoint(0f, 0f, 1f);
        base.Render();
        if (fading != 0)
        {
            graphics.SetViewPoint(0f, 0f, 1f);
            graphics.Fade(fadeTimer);
            fadeOverlay?.Render();
        }
    }

    public override void Update(float deltaTimeS)
    {
        while (pendingEvents.Count > 0) OnEvent(pendingEvents.Dequeue());

        if (freeList.Count > 0)
        {
            var list = freeList.ToArray();
            freeList.Clear();
            foreach (var e in list)
            {
                if (e == null || e.IsDisposed) continue;
                if (ReferenceEquals(e, fadeOverlay)) fadeOverlay = null;
                if (e.parent != null) e.parent.FreeChild(e);
                else e.Dispose();
            }
        }

        if (fading == 2)
        {
            float t = fadeTimer + deltaTimeS * FadeSpeed;
            if (t >= 1f)
            {
                fading = 3;
                fadeTimer = 1f;
                OnEvent(Event.Gui(Event.Info.GUIInfo.FADE_OUT, this, fadeInfo));
            }
            else fadeTimer = t;
        }
        else if (fading == 1)
        {
            float t = fadeTimer - deltaTimeS * FadeSpeed;
            if (t > 0f) fadeTimer = t;
            else
            {
                fading = 0;
                fadeTimer = 0f;
                ReleaseOverlay();
                OnEvent(Event.Gui(Event.Info.GUIInfo.FADE_IN, this, fadeInfo));
            }
        }

        base.Update(deltaTimeS);
    }

    /// <summary>Defers destruction of the element until the start of the next update.</summary>
    public void SafeFreeChild(GUIElement child)
    {
        if (child == null || child.IsDisposed) return;
        if (!freeList.Contains(child)) freeList.Add(child);
    }

    public override void FreeAllChild()
    {
        freeList.Clear();
        base.FreeAllChild();
    }

    public override void FreeChild(GUIElement child)
    {
        if (ReferenceEquals(child, fadeOverlay)) fadeOverlay = null;
        freeList.Remove(child);
        base.FreeChild(child);
    }

    // ------------------------------------------------------------------------------------
    // Factories
    // ------------------------------------------------------------------------------------

    public GUIImage AddImage(string imageName, in ecTextureRect textureRect, in GUIRect rectIn, GUIElement addTo, bool pvr)
    {
        var img = new GUIImage();
        bool etc = false, webp = false;
        if (!string.IsNullOrEmpty(imageName))
        {
            char last = imageName[imageName.Length - 1];
            webp = last == 'p';
            etc = last == 'm';
        }
        img.Init(imageName, textureRect, rectIn, pvr, etc, webp);
        (addTo ?? this).AddChild(img, true);
        return img;
    }

    public GUIImage AddImage(string imageName, in GUIRect rectIn, GUIElement addTo)
    {
        var img = new GUIImage();
        img.Init(imageName, rectIn);
        (addTo ?? this).AddChild(img, true);
        return img;
    }

    public GUIButton AddButton(string notPressedImageName, string pressedImageName, in GUIRect rectIn, GUIElement addTo, ecUniFont textFont)
    {
        var button = new GUIButton();
        button.Init(notPressedImageName, pressedImageName, rectIn, textFont);
        (addTo ?? this).AddChild(button, true);
        return button;
    }

    public GUIScrollBar AddScrollBar(in GUIRect rectIn, GUIElement addTo, string sliderNotPressedImageName, string sliderPressedImageName, int xBase, int yBase, int initPos, int scrollPosMultiplier, int horizontal)
    {
        var bar = new GUIScrollBar();
        bar.Init(rectIn, sliderNotPressedImageName, sliderPressedImageName, xBase, yBase, initPos, scrollPosMultiplier, horizontal);
        (addTo ?? this).AddChild(bar, true);
        return bar;
    }
}
