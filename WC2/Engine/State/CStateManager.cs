namespace WC2;

public enum EState { LOGO, MENU, MATCH, LOAD, GAME, EDIT }

/// <summary>Base of the game states (logo, menu, load, game ...).</summary>
public abstract class CBaseState : IEventReceiver
{
    public EState stateId;
    public bool exist;

    public virtual void OnEnter() { }
    public virtual void OnExit() { }
    public abstract void Update(float deltaTimeS);
    public abstract void Render();
    public abstract bool OnEvent(Event evt);
    public virtual void TouchBegin(float x, float y, int index) { }
    public virtual void TouchMove(float x, float y, int index) { }
    public virtual void TouchEnd(float x, float y, int index) { }
    /// <summary>
    /// The system took the touch away (Android ACTION_CANCEL, the app losing focus): end any drag
    /// without treating it as a tap. Port addition; by default the same as <see cref="TouchEnd"/>.
    /// </summary>
    public virtual void TouchCancel(float x, float y, int index) => TouchEnd(x, y, index);
    public virtual bool BackPressed() => false;
    public virtual void KeyDown(int key) { }
    public virtual void ScrollWheel(float x, float delta, float y) { }
    public virtual void EnterBackground() { }
    public virtual void EnterForeground() { }
}

/// <summary>Owns the registered states and performs deferred state switches.</summary>
public sealed class CStateManager
{
    private static CStateManager s_instance;
    public static CStateManager Instance() => s_instance ??= new CStateManager();

    public readonly CBaseState[] states = new CBaseState[6];
    public EState currentStateId;
    public EState nextUpdateStateId;
    public CBaseState currentState;
    private bool hasPendingSwitch;
    private bool initialized;

    private CStateManager() { }

    public void Init()
    {
        for (int i = 0; i < states.Length; i++) states[i] = null;
        currentState = null;
        hasPendingSwitch = false;
        initialized = true;
    }

    public void Term()
    {
        currentState?.OnExit();
        currentState = null;
        for (int i = 0; i < states.Length; i++) states[i] = null;
        initialized = false;
    }

    public void RegisterState(CBaseState state)
    {
        if (state == null) return;
        states[(int)state.stateId] = state;
    }

    public CBaseState GetStatePtr(EState state) => states[(int)state];

    /// <summary>Requests a state change; it happens at the start of the next Update (a request for the current state is ignored, as in the original).</summary>
    public void SetCurState(EState state)
    {
        if (currentState != null && currentStateId == state) return;
        nextUpdateStateId = state;
        hasPendingSwitch = true;
    }

    private void ApplyPendingSwitch()
    {
        if (!hasPendingSwitch) return;
        hasPendingSwitch = false;
        var next = states[(int)nextUpdateStateId];
        if (next == null) { Log("CStateManager: state not registered: " + nextUpdateStateId); return; }
        if (currentState != null)
        {
            try { currentState.OnExit(); }
            catch (Exception ex) { Log("State OnExit failed: " + ex); }
            currentState.exist = false;
        }
        currentState = next;
        currentStateId = nextUpdateStateId;
        try { currentState.OnEnter(); }
        catch (Exception ex) { Log("State OnEnter failed: " + ex); }
        currentState.exist = true;
    }

    /// <summary>Applies a pending switch, then updates the current state (one switch per frame, like the original).</summary>
    public void Update(float deltaTimeS)
    {
        ApplyPendingSwitch();
        currentState?.Update(deltaTimeS);
    }

    public void Render() => currentState?.Render();

    public void TouchBegin(float x, float y, int index) => currentState?.TouchBegin(x, y, index);
    public void TouchMove(float x, float y, int index) => currentState?.TouchMove(x, y, index);
    public void TouchEnd(float x, float y, int index) => currentState?.TouchEnd(x, y, index);
    public void TouchCancel(float x, float y, int index) => currentState?.TouchCancel(x, y, index);
    public bool BackPressed() => currentState?.BackPressed() ?? false;
    public void KeyDown(int key) => currentState?.KeyDown(key);
    public void ScrollWheel(float x, float delta, float y) => currentState?.ScrollWheel(x, delta, y);
    public void EnterBackground() => currentState?.EnterBackground();
    public void EnterForeground() => currentState?.EnterForeground();
}
