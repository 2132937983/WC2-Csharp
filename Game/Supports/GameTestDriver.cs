namespace WC2;

/// <summary>
/// Test helper behind the <c>autoplay ROUNDS</c> script command: plays a battle without a person.
/// When the human may act it presses the round button (with the AI hosting option the AI then plays
/// the human side too), and it closes every in-game box that waits for a tap. Together with
/// <c>--seed</c> and <c>--fixed-step</c> this replays long games exactly, which is what the golden
/// state dumps need.
/// </summary>
public static class GameTestDriver
{
    private static bool s_active;
    private static int s_roundsLeft;
    private static int s_lastTurn;
    private static float s_cooldown;
    private static float s_idle;
    /// <summary>"settle": no turn is ended; stop as soon as the human side can act and nothing is open.</summary>
    private static bool s_settle;

    public static bool Active => s_active;

    /// <summary>
    /// The <c>settle</c> command: closes pending boxes and fast-forwards AI moves until the human side
    /// can act with nothing open (for example after autoplay, before opening the pause box).
    /// </summary>
    public static void StartSettle()
    {
        s_settle = true;
        s_roundsLeft = int.MaxValue;
        s_lastTurn = g_GameManager.currentTurnNumMinusOne;
        s_cooldown = 0f;
        s_idle = 0f;
        s_active = true;
    }

    public static void StartAutoplay(int rounds)
    {
        s_settle = false;
        s_roundsLeft = Math.Max(0, rounds);
        s_lastTurn = g_GameManager.currentTurnNumMinusOne;
        s_cooldown = 0f;
        s_idle = 0f;
        s_active = s_roundsLeft > 0;
        Log($"Autoplay: {rounds} round(s) from turn {s_lastTurn + 1}");
    }

    /// <summary>Called every frame by the script driver while autoplay runs.</summary>
    public static void Step(float dt)
    {
        if (!s_active) return;
        if (CStateManager.Instance().currentState is not CGameState gs)
        {
            // loading screen or a state switch: wait, but give up if the game left the battle for good
            s_idle += dt;
            if (s_idle > 30f) Stop("left the game state");
            return;
        }
        s_idle = 0f;

        if (g_GameManager.result)
        {
            Stop(g_GameManager.resultWin ? "battle won" : "battle lost");
            return;
        }
        if (g_GameManager.currentTurnNumMinusOne != s_lastTurn)
        {
            s_lastTurn = g_GameManager.currentTurnNumMinusOne;
            if (--s_roundsLeft <= 0)
            {
                Stop($"reached turn {s_lastTurn + 1}");
                return;
            }
        }

        s_cooldown -= dt;
        if (s_cooldown > 0f) return;

        // boxes that wait for a tap
        if (IsVisible(gs.guiDialogue))
        {
            var g = ecGraphics.Instance();
            Tap(g.screenW * 0.5f, g.screenH * 0.5f);
            s_cooldown = 0.5f;
            return;
        }
        GUIButton modalButton =
            IsVisible(gs.guiBattleIntro) ? gs.guiBattleIntro.buttonOk :
            IsVisible(gs.guiBegin) ? gs.guiBegin.buttonOk :
            IsVisible(gs.guiDefeated) ? gs.guiDefeated.buttonOk :
            IsVisible(gs.guiWarning) ? gs.guiWarning.buttonOk : null;
        if (modalButton != null)
        {
            TapElement(modalButton);
            s_cooldown = 0.5f;
            return;
        }

        s_report -= dt;
        if (s_report <= 0f)
        {
            s_report = 30f;
            var cur = g_GameManager.GetCurCountry();
            Log($"Autoplay: turn {g_GameManager.currentTurnNumMinusOne + 1}, country {cur?.id} ai={cur?.ai} " +
                $"manipulate={g_GameManager.IsManipulate()} aiAction={g_GameManager.AIAction} skip={g_GameManager.SkipMode} " +
                $"round button={IsVisible(gs.gameButtonRound)}");
        }

        if (!IsVisible(gs.gameButtonRound)) return;
        if (s_settle && g_GameManager.IsManipulate() && !g_GameManager.AIAction)
        {
            Stop("settled, the human side can act");
            return;
        }
        if (g_GameManager.IsManipulate() && !g_GameManager.AIAction)
        {
            // the human side's turn: hand it over (the AI hosting option plays it)
            TapElement(gs.gameButtonRound);
            s_cooldown = 1f;
        }
        else if (g_GameManager.AIAction && !g_GameManager.SkipMode)
        {
            // the AI is acting: a second press fast-forwards it, as a player would do
            TapElement(gs.gameButtonRound);
            s_cooldown = 1f;
        }
    }

    private static float s_report;

    private static void Stop(string why)
    {
        s_active = false;
        Log("Autoplay finished: " + why);
    }

    private static bool IsVisible(GUIElement e)
    {
        if (e == null || e.IsDisposed) return false;
        for (var p = e; p != null; p = p.parent)
        {
            if ((p.flags & GUIElement.SHOWN) == 0) return false;
            if (p is GUIManager) return true;
        }
        return false;
    }

    private static void TapElement(GUIElement e)
    {
        var r = e.GetAbsRect();
        Tap(r.x + r.w * 0.5f, r.y + r.h * 0.5f);
    }

    private static void Tap(float x, float y)
    {
        ecGame.ecTouchBegin(x, y, 1);
        ecGame.ecTouchEnd(x, y, 1);
    }
}
