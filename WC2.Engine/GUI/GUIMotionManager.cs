namespace WC2;

/// <summary>
/// Slides GUI elements between a start and a target position (HUD entrance animations).
/// Port of the original manager: 16 slots handed out sequentially, per axis speeds in logical
/// pixels per frame, an optional wait (in frames), and a GUI WARNING event posted to the
/// current state for every motion that arrives (ptr = the element, info = the motion index).
/// Activation flags: bit 1 (value 2) = run from the target back to the start, bit 2 (value 4) =
/// loop, bit 3 (value 8) = make ProcMotion return true while the motion runs.
/// </summary>
public sealed class GUIMotionManager
{
    private static GUIMotionManager s_instance;
    public static GUIMotionManager Instance() => s_instance ??= new GUIMotionManager();

    public sealed class GUIMotion
    {
        public GUIElement element;
        public float startX, startY, targetX, targetY, activeX, activeY, speedX, speedY;
        public ushort procWait, procWaitRemaining;
        public byte activeFlag;
    }

    public const int MaxMotions = 16;
    public readonly GUIMotion[] motion = new GUIMotion[MaxMotions];
    public readonly int[] addMotionOrder = new int[MaxMotions];
    public readonly int[] activeMotionOrder = new int[MaxMotions];
    public int motionCount;
    public int activeMotionCount;
    public bool frozen;
    public IEventReceiver currentState;

    /// <summary>Frame rate the per frame speeds and waits refer to.</summary>
    public const float FrameRate = 60f;
    private float frameAccumulator;

    private GUIMotionManager()
    {
        for (int i = 0; i < MaxMotions; i++) motion[i] = new GUIMotion();
    }

    public void ClearMotion()
    {
        for (int i = 0; i < motionCount && i < MaxMotions; i++) motion[addMotionOrder[i]].element = null;
        foreach (var m in motion) { m.element = null; m.activeFlag = 0; }
        motionCount = 0;
        activeMotionCount = 0;
        frozen = false;
    }

    public void SetMotion(uint index, float startX, float startY, float targetX, float targetY, float speed, ushort wait)
    {
        if (index >= MaxMotions) return;
        var m = motion[index];
        if (m.element == null) return;
        m.startX = startX; m.startY = startY; m.targetX = targetX; m.targetY = targetY;
        m.procWait = wait;
        if (startX == targetX) { m.speedX = 0f; m.speedY = speed; }
        else if (startY == targetY) { m.speedX = speed; m.speedY = 0f; }
        else
        {
            float dx = targetX - startX, dy = targetY - startY;
            float len = MathF.Sqrt(dx * dx + dy * dy);
            m.speedX = Math.Abs((int)(speed * dx / len));
            m.speedY = Math.Abs((int)(speed * dy / len));
        }
    }

    public int AddMotion(GUIElement element, float startX, float startY, float targetX, float targetY, float speed, int wait)
    {
        int i = motionCount;
        if (element == null || i >= MaxMotions || motion[i].element != null) return -1;
        var m = motion[i];
        m.element = element;
        m.activeFlag = 0;
        SetMotion((uint)i, startX, startY, targetX, targetY, speed, (ushort)Math.Clamp(wait, 0, ushort.MaxValue));
        addMotionOrder[motionCount] = i;
        motionCount++;
        return i;
    }

    public int AddMotion(GUIElement element, float targetX, float targetY, float speed, int wait)
    {
        if (element == null) return -1;
        return AddMotion(element, element.rect.x, element.rect.y, targetX, targetY, speed, wait);
    }

    public int AddMotionX(GUIElement element, float targetX, float speed, int wait)
    {
        if (element == null) return -1;
        return AddMotion(element, element.rect.x, element.rect.y, targetX, element.rect.y, speed, wait);
    }

    public int AddMotionY(GUIElement element, float targetY, float speed, int wait)
    {
        if (element == null) return -1;
        return AddMotion(element, element.rect.x, element.rect.y, element.rect.x, targetY, speed, wait);
    }

    public void SetSpeed(uint index, float speedX, float speedY)
    {
        if (index >= MaxMotions) return;
        motion[index].speedX = speedX; motion[index].speedY = speedY;
    }

    /// <summary>Starts a motion (see the class remarks for the flag bits).</summary>
    public void ActiveMotion(int index, byte flagsIn)
    {
        if (index < 0 || index >= MaxMotions) return;
        var m = motion[index];
        if (m.element == null || (m.activeFlag & 1) != 0) return;
        if ((flagsIn & 2) != 0) { m.activeX = m.targetX; m.activeY = m.targetY; }
        else { m.activeX = m.startX; m.activeY = m.startY; }
        m.procWaitRemaining = m.procWait;
        m.activeFlag = (byte)(flagsIn | 1);
        if (activeMotionCount < MaxMotions) activeMotionOrder[activeMotionCount++] = index;
    }

    public void ActiveMotion(uint index, byte flagsIn) => ActiveMotion((int)index, flagsIn);

    public bool GetMotionActive(int index) => index >= 0 && index < MaxMotions && (motion[index].activeFlag & 1) != 0;
    public bool GetMotionActive(uint index) => GetMotionActive((int)index);

    /// <summary>
    /// Advances the active motions. The original runs one step per rendered frame; here the
    /// elapsed time is converted to whole 60 Hz frames so the speed matches on any refresh rate.
    /// Returns true when a motion with flag 8 was processed.
    /// </summary>
    public bool ProcMotion(float deltaTimeS = 1f / FrameRate)
    {
        if (frozen) return false;
        frameAccumulator += Math.Max(0f, deltaTimeS) * FrameRate;
        int steps = (int)frameAccumulator;
        if (steps <= 0) return activeMotionCount > 0 && AnyFlag8();
        frameAccumulator -= steps;
        if (steps > 10) steps = 10;
        bool result = false;
        for (int s = 0; s < steps; s++) result |= Step();
        return result;
    }

    private bool AnyFlag8()
    {
        for (int i = 0; i < activeMotionCount; i++)
            if ((motion[activeMotionOrder[i]].activeFlag & 8) != 0) return true;
        return false;
    }

    private bool Step()
    {
        bool result = false;
        for (int i = 0; i < activeMotionCount; i++)
        {
            int index = activeMotionOrder[i];
            var m = motion[index];
            if (m.element == null || m.element.IsDisposed)
            {
                m.activeFlag &= 0xFE;
                activeMotionCount--;
                if (i != activeMotionCount) { activeMotionOrder[i] = activeMotionOrder[activeMotionCount]; i--; }
                continue;
            }
            if (m.procWaitRemaining > 0)
            {
                m.procWaitRemaining--;
                continue;
            }
            byte flag = m.activeFlag;
            float destX, destY;
            if ((flag & 2) != 0) { destX = m.startX; destY = m.startY; }
            else { destX = m.targetX; destY = m.targetY; }

            if (m.activeX == destX && m.activeY == destY)
            {
                if ((flag & 4) != 0)
                {
                    // looping motion: jump back to the origin and keep going
                    if ((flag & 2) != 0) { m.activeX = m.targetX; m.activeY = m.targetY; }
                    else { m.activeX = m.startX; m.activeY = m.startY; }
                }
                else
                {
                    m.activeFlag = (byte)(flag & 0xFE);
                    activeMotionCount--;
                    if (i != activeMotionCount) { activeMotionOrder[i] = activeMotionOrder[activeMotionCount]; i--; }
                }
                if (currentState != null)
                {
                    try { currentState.OnEvent(Event.Gui(Event.Info.GUIInfo.WARNING, m.element, index)); }
                    catch (Exception ex) { Log("Motion finished event failed: " + ex.Message); }
                }
                if ((flag & 8) != 0) result = true;
                continue;
            }

            float x = m.activeX;
            if (destX < x) { x -= m.speedX; if (destX > x) x = destX; }
            else if (destX > x) { x += m.speedX; if (destX < x) x = destX; }
            m.activeX = x;

            float y = m.activeY;
            if (destY < y) { y -= m.speedY; if (destY > y) y = destY; }
            else if (destY > y) { y += m.speedY; if (destY < y) y = destY; }
            m.activeY = y;

            m.element.SetPos(m.activeX, m.activeY);
            if ((m.activeFlag & 8) != 0) result = true;
        }
        return result;
    }
}

/// <summary>Tracks the active touches so the game can tell single from multi finger gestures.</summary>
public sealed class ecMultipleTouch
{
    private static ecMultipleTouch s_instance;
    public static ecMultipleTouch Instance() => s_instance ??= new ecMultipleTouch();

    public sealed class Touch
    {
        public int index;
        public float x;
        public float y;
        public int moved;
    }

    public readonly List<Touch> touch = new();

    public void Reset() => touch.Clear();

    public Touch Find(int index) => touch.FirstOrDefault(t => t.index == index);

    /// <summary>Registers a touch with an explicit index (from the host) and returns the index.</summary>
    public int TouchBegan(int index, float x, float y)
    {
        var t = Find(index);
        if (t == null) { t = new Touch { index = index }; touch.Add(t); }
        t.x = x; t.y = y; t.moved = 0;
        return index;
    }

    public int TouchBegan(float x, float y)
    {
        int index = 1;
        while (Find(index) != null) index++;
        return TouchBegan(index, x, y);
    }

    public int TouchMoved(int index, float x, float y)
    {
        var t = Find(index);
        if (t == null) return -1;
        t.x = x; t.y = y; t.moved = 1;
        return index;
    }

    public int TouchEnded(int index, float x, float y)
    {
        var t = Find(index);
        if (t == null) return -1;
        touch.Remove(t);
        return index;
    }
}
