namespace WC2;

/// <summary>Receiver of routed touches (logical units). The game uses <see cref="GameTouchSink"/>; the self test records them.</summary>
public interface ITouchSink
{
    void Begin(float x, float y, int index);
    void Move(float x, float y, int index);
    void End(float x, float y, int index);
    void Cancel(int index);
    void Back();
}

/// <summary>Delivers routed touches to the game's input entry (ecGame), where off-screen gestures are dropped.</summary>
public sealed class GameTouchSink : ITouchSink
{
    public void Begin(float x, float y, int index) => ecGame.ecTouchBegin(x, y, index);
    public void Move(float x, float y, int index) => ecGame.ecTouchMove(x, y, index);
    public void End(float x, float y, int index) => ecGame.ecTouchEnd(x, y, index);
    public void Cancel(int index) => ecGame.ecTouchCancel(index);
    public void Back() => ecGame.CallNativeExit();
}

/// <summary>
/// Port addition between the host's touch screen and the game. It takes raw touches in window
/// pixels with the platform's pointer ids (reused by Android), gives every gesture a fresh game
/// index, maps the points to logical units with the same transform the renderer draws with
/// (<see cref="ecGraphics.WindowToLogical"/>), and recognizes the edge swipe that means "back".
///
/// The edge swipe replaces Android's system back gesture, which the activity turns off along the side
/// edges so that drags starting there reach the game (the system would otherwise cancel them):
/// <list type="bullet">
/// <item>A first finger that goes down within <see cref="EdgeZoneDp"/> of the left or right edge of the
/// window is held back until it is clear what it is.</item>
/// <item>Moving inward (more across than along the edge) past the touch slop makes it a back swipe; the
/// game never sees it. Released at least <see cref="BackDistanceDp"/> inward it is "back", released
/// shorter it is abandoned, like the system gesture.</item>
/// <item>Anything else is handed to the game with its original start point: a tap (released before the
/// slop), a drag along or out of the edge, a finger held still for <see cref="EdgeHoldSeconds"/>, or
/// a second finger going down (pinch).</item>
/// </list>
/// All distances are in dp (density independent pixels, <see cref="Density"/> window pixels each), so
/// the gesture feels the same on every screen; the game's own thresholds stay in logical units, as in
/// the original. Must be used from the game thread.
/// </summary>
public sealed class TouchRouter
{
    private static TouchRouter s_instance;
    /// <summary>The router the host feeds (other instances are for the self test).</summary>
    public static TouchRouter Instance() => s_instance ??= new TouchRouter();

    /// <summary>Width of the edge strips, the default inset of Android's back gesture.</summary>
    public const float EdgeZoneDp = 24f;
    /// <summary>Movement that tells a drag from a tap (Android's ViewConfiguration touch slop).</summary>
    public const float TouchSlopDp = 8f;
    /// <summary>Inward travel at release that commits the back swipe (at most a quarter of the window width).</summary>
    public const float BackDistanceDp = 48f;
    /// <summary>An edge finger held still this long belongs to the game (press and hold, slow drags).</summary>
    public const float EdgeHoldSeconds = 0.35f;

    /// <summary>Window pixels per dp (Android DisplayMetrics.Density; 1 on the desktop unless --density is given).</summary>
    public float Density = 1f;
    /// <summary>Recognize the edge swipe (touch screens); the mouse never goes through the edge logic.</summary>
    public bool EdgeBackEnabled = true;
    /// <summary>
    /// Width in pixels of the system back-gesture zone (Android, from the window insets; it follows the
    /// user's back sensitivity setting). The edge strip is never narrower, so the swipe starts where
    /// the user expects the system gesture. Written by the UI thread.
    /// </summary>
    public volatile int SystemEdgePx;
    public ITouchSink Sink = new GameTouchSink();

    private sealed class Track
    {
        public int id;
        public int index;
        public float startX, startY, x, y;
        public double startTime;
        /// <summary>+1 left edge (inward = +x), -1 right edge, 0 none.</summary>
        public int edge;
        /// <summary>Not delivered to the game yet.</summary>
        public bool held;
        /// <summary>Held edge finger moving inward: a back swipe in progress.</summary>
        public bool swiping;
    }

    private readonly Dictionary<int, Track> tracks = new();
    private readonly List<Track> scratch = new();
    // game indices of routed touches; the mouse and the scripts use 1 and 2
    private const int FirstIndex = 100, LastIndex = 999;
    private int nextIndex = FirstIndex;
    private double now;

    public int ActiveCount => tracks.Count;

    private float EdgeZonePx => MathF.Max(EdgeZoneDp * Density, SystemEdgePx);
    private float SlopPx => TouchSlopDp * Density;
    private float BackDistancePx => MathF.Min(BackDistanceDp * Density, ecGraphics.Instance().layoutW * 0.25f);

    /// <summary>Advances the router clock (seconds); edge fingers held still long enough are handed to the game.</summary>
    public void Update(double time)
    {
        now = time;
        foreach (var t in Snapshot())
            if (t.held && !t.swiping && now - t.startTime >= EdgeHoldSeconds) Release(t);
    }

    public void Begin(int id, float px, float py)
    {
        if (tracks.ContainsKey(id)) Cancel(id); // the release of the previous gesture was lost
        // a second finger: a held edge finger is part of a pinch, not a back swipe
        foreach (var held in Snapshot())
            if (held.held) Release(held);

        var t = new Track { id = id, index = NextIndex(), startX = px, startY = py, x = px, y = py, startTime = now };
        if (EdgeBackEnabled && tracks.Count == 0)
        {
            float zone = EdgeZonePx;
            if (px < zone) t.edge = 1;
            else if (px >= ecGraphics.Instance().layoutW - zone) t.edge = -1;
        }
        tracks[id] = t;
        if (t.edge != 0) { t.held = true; return; }
        Deliver(t, TouchPhase.Begin, px, py);
    }

    public void Move(int id, float px, float py)
    {
        if (!tracks.TryGetValue(id, out var t)) return;
        if (px == t.x && py == t.y) return;
        t.x = px; t.y = py;
        if (!t.held) { Deliver(t, TouchPhase.Move, px, py); return; }
        if (t.swiping) return;
        float inward = (px - t.startX) * t.edge, along = MathF.Abs(py - t.startY);
        if (inward >= SlopPx && inward > along) t.swiping = true;
        else if (along >= SlopPx || inward <= -SlopPx) Release(t); // along or out of the edge: the game's drag
    }

    public void End(int id, float px, float py)
    {
        if (!tracks.Remove(id, out var t)) return;
        t.x = px; t.y = py;
        if (!t.held) { Deliver(t, TouchPhase.End, px, py); return; }
        if (t.swiping)
        {
            float inward = (px - t.startX) * t.edge, along = MathF.Abs(py - t.startY);
            if (inward >= BackDistancePx && inward > along)
            {
                Log($"Edge swipe: back ({(t.edge > 0 ? "left" : "right")} edge, {inward / Density:0} dp)");
                Sink.Back();
            }
            return; // a short swipe is abandoned, like the system gesture
        }
        // a tap at the edge, delivered now
        Deliver(t, TouchPhase.Begin, t.startX, t.startY);
        if (px != t.startX || py != t.startY) Deliver(t, TouchPhase.Move, px, py);
        Deliver(t, TouchPhase.End, px, py);
    }

    /// <summary>The system took the touch away (Android ACTION_CANCEL): no tap, no back.</summary>
    public void Cancel(int id)
    {
        if (!tracks.Remove(id, out var t)) return;
        if (!t.held) Sink.Cancel(t.index);
    }

    /// <summary>Cancels every finger (the app lost focus or was paused).</summary>
    public void CancelAll()
    {
        foreach (var t in Snapshot()) Cancel(t.id);
    }

    /// <summary>Hands a held edge finger to the game: its start point, then where it is now.</summary>
    private void Release(Track t)
    {
        if (!t.held) return;
        t.held = false;
        t.swiping = false;
        Deliver(t, TouchPhase.Begin, t.startX, t.startY);
        if (t.x != t.startX || t.y != t.startY) Deliver(t, TouchPhase.Move, t.x, t.y);
    }

    private enum TouchPhase { Begin, Move, End }

    private void Deliver(Track t, TouchPhase phase, float px, float py)
    {
        ecGraphics.Instance().WindowToLogical(px, py, out float x, out float y);
        switch (phase)
        {
            case TouchPhase.Begin: Sink.Begin(x, y, t.index); break;
            case TouchPhase.Move: Sink.Move(x, y, t.index); break;
            default: Sink.End(x, y, t.index); break;
        }
    }

    private int NextIndex()
    {
        int index = nextIndex;
        nextIndex = nextIndex >= LastIndex ? FirstIndex : nextIndex + 1;
        return index;
    }

    private List<Track> Snapshot()
    {
        scratch.Clear();
        scratch.AddRange(tracks.Values);
        return scratch;
    }
}
