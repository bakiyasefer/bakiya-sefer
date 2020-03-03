using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using FullInspector;

public enum ChallengeState { NOTE, GREEN, RED }
public interface IChallengeTask
{
    void OnEnable(
#if CODEDEBUG
        string debug_data
#endif
);
    void OnDestroy();
    void OnPlayingBegin(bool forceReset);
    void OnPlayingEnd();
    bool IsGreen();
    void Complete();

    bool IsSavingState();
    object SaveState();
    void LoadState(object state);

    string ProgressDesc();
    string TaskDesc();
    string TipDesc();

    GameObject MainUi();
    GameObject NoteUi();

    void SetOnStateChanged(GameController.Event<IChallengeTask, ChallengeState> handler);
}
public class ChComposite : IChallengeTask
{
    /*[SetInEditor]*/
    public IChallengeTask[] tasks = null;

#if CODEDEBUG
    protected string debug_data = string.Empty;
#endif

    public virtual void OnEnable(
#if CODEDEBUG
        string debugData
#endif
)
    {
#if CODEDEBUG
        debug_data = debugData;
#endif
        if (tasks == null) {
            tasks = new IChallengeTask[0];
        }
        for (int i = 0, l = tasks.Length; i < l; ++i) {
            tasks[i].OnEnable(
#if CODEDEBUG
string.Format("{0}.tasks[{1}]", debug_data, i)
#endif
);
        }
    }
    public virtual void OnDestroy()
    {
        for (int i = 0, l = tasks.Length; i < l; ++i) {
            tasks[i].OnDestroy();
        }
    }
    public virtual void OnPlayingBegin(bool forceReset)
    {
        bool is_green = IsGreen();
        for (int i = 0, l = tasks.Length; i < l; ++i) {
            tasks[i].OnPlayingBegin(forceReset || !is_green);
        }
    }
    public virtual void OnPlayingEnd()
    {
        for (int i = 0, l = tasks.Length; i < l; ++i) {
            tasks[i].OnPlayingEnd();
        }
    }
    public virtual bool IsGreen()
    {
        for (int i = 0, l = tasks.Length; i < l; ++i) {
            if (!tasks[i].IsGreen()) return false;
        }
        return true;
    }
    public virtual void Complete()
    {
        for (int i = 0, l = tasks.Length; i < l; ++i) {
            tasks[i].Complete();
        }
    }
    public virtual bool IsSavingState()
    {
        for (int i = 0, l = tasks.Length; i < l; ++i) {
            if (tasks[i].IsSavingState()) return true;
        }
        return false;
    }
    public virtual object SaveState()
    {
        Dictionary<int, object> state = null;
        for (int i = 0, l = tasks.Length; i < l; ++i) {
            if (tasks[i].IsSavingState()) {
                if (state == null) { state = new Dictionary<int, object>(); }
                state[i] = tasks[i].SaveState();
            }
        }
        return state;
    }
    public virtual void LoadState(object state)
    {
        if (tasks.Length == 0 || state == null) return;
        var dic = state as Dictionary<int, object>;
        if (dic == null) return;
        for (int i = 0, l = tasks.Length; i < l; ++i) {
            if (tasks[i].IsSavingState()) {
                object val = null;
                if (dic.TryGetValue(i, out val)) {
                    tasks[i].LoadState(val);
                }
            }
        }
    }
    public virtual string TaskDesc()
    {
        return tasks.Length > 0 ? tasks[0].TaskDesc() : string.Empty;
    }
    public virtual string ProgressDesc()
    {
        return tasks.Length > 0 ? tasks[0].ProgressDesc() : string.Empty;
    }
    public virtual string TipDesc()
    {
        return tasks.Length > 0 ? tasks[0].TipDesc() : string.Empty;
    }
    public virtual GameObject MainUi()
    {
        return tasks.Length > 0 ? tasks[0].MainUi() : null;
    }
    public virtual GameObject NoteUi() 
    {
        return tasks.Length > 0 ? tasks[0].NoteUi() : null;
    }

    GameController.Event<IChallengeTask, ChallengeState> onStateChanged = null;
    public virtual void SetOnStateChanged(GameController.Event<IChallengeTask, ChallengeState> handler)
    {
        onStateChanged = handler;
        for (int i = 0, l = tasks.Length; i < l; ++i) {
            tasks[i].SetOnStateChanged(OnStateChanged);
        }
    }
    void OnStateChanged(IChallengeTask task, ChallengeState state)
    {
        switch (state) {
            case ChallengeState.GREEN:
                if (IsGreen() && onStateChanged != null) { onStateChanged(this, state); }
                break;
            case ChallengeState.NOTE:
                if (onStateChanged != null) { onStateChanged(task, state); }
                break;
        }
    }
}
public class WhileIndex : IChallengeTask
{
    public enum Type { PLAYER, THEME, LANE }
    /*[SetInEditor]*/
    public bool not = false;
    /*[SetInEditor]*/
    public Type type = Type.PLAYER;
    /*[SetInEditor]*/
    public int index = 0;

    GameController.GetValue<bool> green_func;

    public void OnPlayingBegin(bool forceReset)
    {
        switch (type) {
        case Type.PLAYER: green_func = IsGreen_PlayerIndex; break;
        case Type.THEME: green_func = IsGreen_ThemeIndex; break;
        case Type.LANE: green_func = IsGreen_LaneIndex; break;
        }
    }

    bool IsGreen_PlayerIndex()
    {
        return !not == (GameController.Instance.SelectedPlaycharIndex() == index);
    }
    bool IsGreen_ThemeIndex()
    {
        return !not == (GameController.Instance.THEME_CurrentIndex() == index);
    }
    bool IsGreen_LaneIndex()
    {
        return !not == (GameController.Instance.PlaycharChallengeEvents().LastLane() == index);
    }

    public void OnEnable(
#if CODEDEBUG
string debug_data
#endif
) { }
    public void OnDestroy() { }
    public void OnPlayingEnd() { }
    public bool IsGreen() { return green_func(); }
    public void Complete() { }
    public bool IsSavingState() { return false; }
    public object SaveState() { return null; }
    public void LoadState(object state) { }
    public string TaskDesc() { return string.Empty; }
    public string ProgressDesc() { return string.Empty; }
    public string TipDesc() { return string.Empty; }
    public GameObject MainUi() { return null; }
    public GameObject NoteUi() { return null; }
    public void SetOnStateChanged(GameController.Event<IChallengeTask, ChallengeState> handler) { }
}
public class WhileChaserIs : IChallengeTask
{
    public enum Type { STANDING, CHASING, ONSCREEN, ATTACKING }

    /*[SetInEditor]*/
    public bool not = false;
    /*[SetInEditor]*/
    public Type type = Type.CHASING;

    GameController.GetValue<bool> green_func;

    public void OnPlayingBegin(bool forceReset)
    {
        switch (type) {
        case Type.CHASING: green_func = IsGreen_ChaserChasing; break;
        case Type.STANDING: green_func = IsGreen_ChaserStanding; break;
        case Type.ONSCREEN: green_func = IsGreen_ChaserOnScreen; break;
        case Type.ATTACKING: green_func = IsGreen_ChaserAttacking; break;
        }
    }

    bool IsGreen_ChaserActive()
    {
        return !not == GameController.Instance.ChaserChallengeEvents().IsActive();
    }
    bool IsGreen_ChaserStanding()
    {
        return !not == GameController.Instance.ChaserChallengeEvents().IsStanding();
    }
    bool IsGreen_ChaserChasing()
    {
        return !not == GameController.Instance.ChaserChallengeEvents().IsChasing();
    }
    bool IsGreen_ChaserOnScreen()
    {
        return !not == GameController.Instance.ChaserChallengeEvents().IsOnScreen();
    }
    bool IsGreen_ChaserAttacking()
    {
        return !not == GameController.Instance.ChaserChallengeEvents().IsAttacking();
    }

    public void OnEnable(
#if CODEDEBUG
string debug_data
#endif
) { }
    public void OnDestroy() { }
    public void OnPlayingEnd() { }
    public bool IsGreen() { return green_func(); }
    public void Complete() { }
    public bool IsSavingState() { return false; }
    public object SaveState() { return null; }
    public void LoadState(object state) { }
    public string TaskDesc() { return string.Empty; }
    public string ProgressDesc() { return string.Empty; }
    public string TipDesc() { return string.Empty; }
    public GameObject MainUi() { return null; }
    public GameObject NoteUi() { return null; }
    public void SetOnStateChanged(GameController.Event<IChallengeTask, ChallengeState> handler) { }
}
public class WhilePlayerIs : IChallengeTask
{
    public enum Type { GROUNDED, SLOPE, STRAFING, SLIDING, TIRED, MAGNET }
    /*[SetInEditor]*/
    public bool not = false;
    /*[SetInEditor]*/
    public Type type = Type.GROUNDED;

    GameController.GetValue<bool> green_func;

    public void OnPlayingBegin(bool forceReset)
    {
        switch (type) {
        case Type.GROUNDED: green_func = IsGreen_Grounded; break;
        case Type.SLOPE: green_func = IsGreen_OnSlope; break;
        case Type.STRAFING: green_func = IsGreen_Strafing; break;
        case Type.SLIDING: green_func = IsGreen_Sliding; break;
        case Type.TIRED: green_func = IsGreen_Tired; break;
        case Type.MAGNET: green_func = IsGreen_Magnet; break;
        }
    }

    bool IsGreen_Grounded()
    {
        return !not == (GameController.Instance.PlaycharChallengeEvents().IsGrounded());
    }
    bool IsGreen_OnSlope()
    {
        return !not == (GameController.Instance.PlaycharChallengeEvents().IsOnSlope());
    }
    bool IsGreen_Strafing()
    {
        return !not == (GameController.Instance.PlaycharChallengeEvents().IsStrafing());
    }
    bool IsGreen_Sliding()
    {
        return !not == (GameController.Instance.PlaycharChallengeEvents().IsSliding());
    }
    bool IsGreen_Tired()
    {
        return !not == (GameController.Instance.PlaycharChallengeEvents().CurrentSpeedState() == PlayerSpeedState.TIRED);
    }
    bool IsGreen_Magnet()
    {
        return !not == (GameController.Instance.PlaycharChallengeEvents().IsMagnetActive());
    }

    public void OnEnable(
#if CODEDEBUG
        string debug_data
#endif
) { }
    public void OnDestroy() { }
    public void OnPlayingEnd() { }
    public bool IsGreen() { return green_func(); }
    public void Complete() { }
    public bool IsSavingState() { return false; }
    public object SaveState() { return null; }
    public void LoadState(object state) { }
    public string TaskDesc() { return string.Empty; }
    public string ProgressDesc() { return string.Empty; }
    public string TipDesc() { return string.Empty; }
    public GameObject MainUi() { return null; }
    public GameObject NoteUi() { return null; }
    public void SetOnStateChanged(GameController.Event<IChallengeTask, ChallengeState> handler) { }
}
public class WhilePlayerWithin : IChallengeTask
{
    public enum Type { WITHIN, SLOPE, FLOOR }
    /*[SetInEditor]*/
    public bool not = false;
    /*[SetInEditor]*/
    public Type type = Type.WITHIN;
    /*[SetInEditor]*/
    public ObstacleType obst_type;

    ObstacleController within_obst = null;

#if CODEDEBUG
    string debug_data = string.Empty;
#endif

    public void OnPlayingBegin(bool forceReset)
    {
        var playchar_events = GameController.Instance.PlaycharChallengeEvents();
        if (playchar_events == null) {
#if CODEDEBUG
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogError(METHOD_NAME, (debug_data + " playchar_events is NULL"));
#endif
            return;
        }
        switch (type) {
        case Type.WITHIN:
            playchar_events.onWithinBegin += OnWithinEnterType;
            playchar_events.onWithinEnd += OnWithinLeave;
            break;
        case Type.SLOPE:
            playchar_events.onSlope += OnFloorEnterType;
            playchar_events.onPosStateChanged += OnPosStateChanged;
            break;
        case Type.FLOOR:
            playchar_events.onFloor += OnFloorEnterType;
            playchar_events.onPosStateChanged += OnPosStateChanged;
            break;
        }
    }
    public void OnPlayingEnd()
    {
        var playchar_events = GameController.Instance.PlaycharChallengeEvents();
        if(playchar_events == null) {
            return;
        }
        switch (type) {
        case Type.WITHIN:
            playchar_events.onWithinBegin -= OnWithinEnterType;
            playchar_events.onWithinEnd -= OnWithinLeave;
            break;
        case Type.SLOPE:
            playchar_events.onSlope -= OnFloorEnterType;
            playchar_events.onPosStateChanged -= OnPosStateChanged;
            break;
        case Type.FLOOR:
            playchar_events.onFloor -= OnFloorEnterType;
            playchar_events.onPosStateChanged -= OnPosStateChanged;
            break;
        }
    }
    public bool IsGreen()
    {
        return !not == (within_obst != null);
    }
    void OnWithinEnterType(ObstacleController obst)
    {
        if (obst.ObstType() == obst_type) {
            within_obst = obst;
        }
    }
    void OnWithinLeave(ObstacleController obst)
    {
        if (object.ReferenceEquals(obst, within_obst)) {
            within_obst = null;
        }
    }
    void OnFloorEnterType(ObstacleController obst)
    {
        within_obst = (obst.ObstType() == obst_type) ? obst : null;
    }
    void OnPosStateChanged(PosState pos)
    {
        switch (pos) {
        case PosState.JUMP_FALLING:
        case PosState.JUMP_RISING:
            within_obst = null;
            break;
        }
    }

    public void OnEnable(
#if CODEDEBUG
string debugData
#endif
)
    {
#if CODEDEBUG
        debug_data = debugData;
#endif
    }
    public void Complete() { }
    public void OnDestroy() { }
    public bool IsSavingState() { return false; }
    public object SaveState() { return null; }
    public void LoadState(object state) { }
    public string TaskDesc() { return string.Empty; }
    public string ProgressDesc() { return string.Empty; }
    public string TipDesc() { return string.Empty; }
    public GameObject MainUi() { return null; }
    public GameObject NoteUi() { return null; }
    public void SetOnStateChanged(GameController.Event<IChallengeTask, ChallengeState> handler) { }
}
public class WhileItemActive : IChallengeTask
{
    public enum Type { START_SCOREX, START_COINSX, START_LUCKX, RUN_SCOREX, RUN_COINSX, RUN_LUCKX }
    /*[SetInEditor]*/
    public bool not = false;
    /*[SetInEditor]*/
    public Type type;

    GameController.GetValue<bool> green_func;

    public void OnPlayingBegin(bool forceReset)
    {
        switch (type) {
        case Type.START_SCOREX: green_func = IsGreen_Start_ScoreX; break;
        case Type.START_COINSX: green_func = IsGreen_Start_CoinsX; break;
        case Type.START_LUCKX: green_func = IsGreen_Start_LuckX; break;
        case Type.RUN_SCOREX: green_func = IsGreen_Run_ScoreX; break;
        case Type.RUN_COINSX: green_func = IsGreen_Run_CoinsX; break;
        case Type.RUN_LUCKX: green_func = IsGreen_Run_LuckX; break;
        }
    }

    bool IsGreen_Start_ScoreX()
    {
        return !not == GameController.Instance.StartItem_ScoreX_Active();
    }
    bool IsGreen_Start_CoinsX()
    {
        return !not == GameController.Instance.StartItem_CoinsX_Active();
    }
    bool IsGreen_Start_LuckX()
    {
        return !not == GameController.Instance.StartItem_LuckX_Active();
    }
    bool IsGreen_Run_ScoreX()
    {
        return !not == GameController.Instance.Run_ScoreX_Active();
    }
    bool IsGreen_Run_CoinsX()
    {
        return !not == GameController.Instance.Run_CoinsX_Active();
    }
    bool IsGreen_Run_LuckX()
    {
        return !not == GameController.Instance.Run_LuckX_Active();
    }

    public void OnEnable(
#if CODEDEBUG
string debug_data
#endif
) { }
    public void OnDestroy() { }
    public void OnPlayingEnd() { }
    public bool IsGreen() { return green_func(); }
    public void Complete() { }
    public bool IsSavingState() { return false; }
    public object SaveState() { return null; }
    public void LoadState(object state) { }
    public string TaskDesc() { return string.Empty; }
    public string ProgressDesc() { return string.Empty; }
    public string TipDesc() { return string.Empty; }
    public GameObject MainUi() { return null; }
    public GameObject NoteUi() { return null; }
    public void SetOnStateChanged(GameController.Event<IChallengeTask, ChallengeState> handler) { }
}
public class WhileTimer : IChallengeTask
{
    [InspectorComment("Use Timer in parallel with tasks it is affecting.\n Timer IsGreen while working.")]
    /*[SetInEditor]*/
    public bool not = false;
    /*[SetInEditor]*/
    public float duration = 0;

/*
#if UNITY_EDITOR
    [InspectorHeader("UI"), InspectorMargin(10), InspectorDivider, ShowInInspector, InspectorHidePrimary]
    bool __inspector_ui;
#endif
    / *[SetInEditor]* /
    public string task_desc = string.Empty;
    / *[SetInEditor]* /
    public string progress_desc = string.Empty;*/

    GameTimer timer = null;
    bool is_completed = false;

/*
#if CODEDEBUG
    string debug_data = string.Empty;
#endif*/

    public void OnEnable(
#if CODEDEBUG
        string debugData
#endif
)
    {
/*
#if CODEDEBUG
        debug_data = debugData;
#endif*/
    }
    public void OnDestroy()
    {
    }
    public void OnPlayingBegin(bool forceReset)
    {
        TimerReset();
    }
    public void OnPlayingEnd()
    {
        TimerStop();
    }
    public bool IsGreen()
    {
        return !not != is_completed;
    }
    public void Complete()
    {
        TimerStop();
        is_completed = true;
    }
    public bool IsSavingState() 
    {
        return false;
    }
    public object SaveState()
    {
        return null;
    }
    public void LoadState(object data)
    {
    }
    public string TaskDesc()
    {
        //return string.Format(task_desc, duration);
        return string.Empty;
    }
    public string ProgressDesc() 
    {
        //return is_completed ? string.Empty : string.Format(progress_desc, (int)(timer.Duration() - timer.TimePassed()));
        return string.Empty;
    }
    public string TipDesc() { return string.Empty; }
    public GameObject MainUi() { return null; }
    public GameObject NoteUi() { return null; }

    GameController.Event<IChallengeTask, ChallengeState> onStateChanged = null;
    public void SetOnStateChanged(GameController.Event<IChallengeTask, ChallengeState> handler)
    {
        onStateChanged = handler;
    }
    void TimerReset()
    {
        if (timer == null) {
            timer = new GameTimer();
            timer.SetTimeStepGetter(GameController.Instance.PlayingTime);
            timer.SetAutoUpdate(true);
            timer.SetDuration(duration);
            timer.SetOnComplete(OnTime);
        }
        timer.Reset();
        is_completed = false;
    }
    void TimerStop()
    {
        if (timer != null) {
            //complete timer to disconnect from autoupdate
            timer.Complete(false);
        }
    }
    void OnTime()
    {
        Complete();
        //report
        if (onStateChanged != null) { onStateChanged(this, not ? ChallengeState.GREEN : ChallengeState.RED); }
    }
}
public class DoPlaycharMove : ChComposite
{
    public enum Type
    {
        JUMP,
        STRAFE,
        SLIDE,
        PUSHTOGROUND,
        TIRED,
        TURBO,
        BUMP,
        STUMBLE,
        CRASH,
        CHASER_CATCH,
        REST,
        CONTINUE
    }
    /*[SetInEditor]*/
    public bool not = false;
    /*[SetInEditor]*/
    public Type type = Type.STUMBLE;
    /*[SetInEditor]*/
    public bool use_obst_type = false;
    /*[SetInEditor]*/
    public ObstacleType obst_type;
    /*[SetInEditor]*/
    public int num_hits = 1;
    /*[SetInEditor]*/
    public bool reset_on_playing = true;
#if UNITY_EDITOR
    [InspectorHeader("UI"), InspectorMargin(10), InspectorDivider, ShowInInspector, InspectorHidePrimary]
    bool __inspector_ui;
#endif
    /*[SetInEditor]*/
    public string task_desc = string.Empty;
    /*[SetInEditor]*/
    public string progress_desc = string.Empty;
    /*[SetInEditor]*/
    [InspectorTextArea(45f)]
    public string tip_desc = string.Empty;

    int current_hits = 0;

    public override void OnEnable(
#if CODEDEBUG
string debugData
#endif
)
    {
        base.OnEnable(
#if CODEDEBUG
debugData
#endif
);
        DoReset();

#if CODEDEBUG
        if (num_hits < 1) {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogWarning(METHOD_NAME, (debug_data + " num_hits is {0}"), num_hits);
        }
        if (not && !reset_on_playing) {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogWarning(METHOD_NAME, (debug_data + "not is {0}, reset_on_playing is {1}"), not, reset_on_playing);
        }
#endif
    }
    public override void OnPlayingBegin(bool forceReset)
    {
#if CODEDEBUG
        string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
#endif
        if (!forceReset && !not && IsGreen()) {
#if CODEDEBUG
            GameController.Log(METHOD_NAME, (debug_data + " already green"));
#endif
            return;
        }
        if (reset_on_playing || forceReset) {
            DoReset();
        }

        if (!ConnectEvents()) {
#if CODEDEBUG
            GameController.LogError(METHOD_NAME, (debug_data + " events not connected"));
#endif
            return;
        }
        base.OnPlayingBegin(forceReset);
    }
    public override void OnPlayingEnd()
    {
        base.OnPlayingEnd();
        DisconnectEvents();
    }
    public override bool IsGreen()
    {
        return !not == (current_hits >= num_hits);
    }
    public override void Complete()
    {
        base.Complete();
        current_hits = num_hits;
        //disconnect
        OnPlayingEnd();
        OnDestroy();
    }
    public override bool IsSavingState()
    {
        //challenge cannot save green state
        return true;
        //return !reset_on_playing || base.IsSavingState();
    }
    public override object SaveState()
    {
        bool this_saving = !reset_on_playing;
        bool baseSaving = base.IsSavingState();
        if (this_saving && baseSaving) {
            return new object[] { current_hits, base.SaveState() };
        } else if (baseSaving) {
            return base.SaveState();
        } else if (this_saving) {
            return current_hits;
        }
        return null;
    }
    public override void LoadState(object data)
    {
#if CODEDEBUG
        string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
        if (!IsSavingState()) {
            GameController.LogError(METHOD_NAME, (debug_data + " should not load"));
            return;
        }
#endif

        bool this_saving = !reset_on_playing;
        bool base_saving = base.IsSavingState();
        if (this_saving && base_saving) {
            object[] state = data as object[];
            if (state == null) {
#if CODEDEBUG
                GameController.LogError(METHOD_NAME, (debug_data + " data is not array"));
#endif
                return;
            }
            if (state.Length != 2) {
#if CODEDEBUG
                GameController.LogError(METHOD_NAME, (debug_data + " state.Length is {0}, expected {1}"), state.Length, 2);
#endif
                return;
            }
            //load base
            base.LoadState(state[1]);
            if (!(state[0] is int)) {
#if CODEDEBUG
                GameController.LogError(METHOD_NAME, (debug_data + " state[0] is not int"));
#endif
                return;
            }
            //load this
            current_hits = (int)state[0];
        } else if (base_saving) {
            //load base
            base.LoadState(data);
        } else if (this_saving) {
            if (!(data is int)) {
#if CODEDEBUG
                GameController.LogError(METHOD_NAME, (debug_data + " data is not int"));
#endif
                return;
            }
            //load this
            current_hits = (int)data;
        }
    }
    public override string TaskDesc()
    {
        return string.Format(GameController.Instance.Localize(task_desc), num_hits);
    }
    public override string ProgressDesc()
    {
        return current_hits < num_hits ? string.Format(GameController.Instance.Localize(progress_desc), num_hits - current_hits) : string.Empty;
    }
    public override string TipDesc()
    {
        return tip_desc;
    }
    public override GameObject MainUi() { return null; }
    public override GameObject NoteUi() { return null; }
    GameController.Event<IChallengeTask, ChallengeState> onStateChanged = null;
    public override void SetOnStateChanged(GameController.Event<IChallengeTask, ChallengeState> handler) { onStateChanged = handler; }

    bool ConnectEvents()
    {
        var playchar_events = GameController.Instance.PlaycharChallengeEvents();
        if (playchar_events == null) {
#if CODEDEBUG
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogError(METHOD_NAME, (debug_data + " playchar_events is NULL"));
#endif
            return false;
        }

        switch (type) {
        case Type.JUMP:
        case Type.PUSHTOGROUND:
            playchar_events.onPosStateChanged += OnPosStateChanged;
            break;
        case Type.STRAFE:
            playchar_events.onStrafeStateChanged += OnStrafeStateChanged;
            break;
        case Type.SLIDE:
            playchar_events.onSlideStateChanged += OnSlideStateChanged;
            break;
        case Type.REST:
            playchar_events.onRest += TryIncreaseHits;
            break;
        case Type.CONTINUE:
            playchar_events.onContinue += TryIncreaseHits;
            break;
        case Type.TIRED:
        case Type.TURBO:
            playchar_events.onSpeedStateChanged += OnSpeedStateChanged;
            break;
        case Type.STUMBLE:
            playchar_events.onStumble += OnBumpStumbleCrash;
            break;
        case Type.CRASH:
            playchar_events.onCrash += OnBumpStumbleCrash;
            break;
        case Type.BUMP:
            playchar_events.onSideBump += OnBumpStumbleCrash;
            break;
        case Type.CHASER_CATCH:
            playchar_events.onChaserCatch += TryIncreaseHits;
            break;
        }
        return true;
    }
    void DisconnectEvents()
    {
        var playchar_events = GameController.Instance.PlaycharChallengeEvents();
        if (playchar_events == null) {
            return;
        }

        switch (type) {
        case Type.JUMP:
        case Type.PUSHTOGROUND:
            playchar_events.onPosStateChanged -= OnPosStateChanged;
            break;
        case Type.STRAFE:
            playchar_events.onStrafeStateChanged -= OnStrafeStateChanged;
            break;
        case Type.SLIDE:
            playchar_events.onSlideStateChanged -= OnSlideStateChanged;
            break;
        case Type.REST:
            playchar_events.onRest -= TryIncreaseHits;
            break;
        case Type.CONTINUE:
            playchar_events.onContinue -= TryIncreaseHits;
            break;
        case Type.TIRED:
        case Type.TURBO:
            playchar_events.onSpeedStateChanged -= OnSpeedStateChanged;
            break;
        case Type.STUMBLE:
            playchar_events.onStumble -= OnBumpStumbleCrash;
            break;
        case Type.CRASH:
            playchar_events.onCrash -= OnBumpStumbleCrash;
            break;
        case Type.BUMP:
            playchar_events.onSideBump -= OnBumpStumbleCrash;
            break;
        case Type.CHASER_CATCH:
            playchar_events.onChaserCatch -= TryIncreaseHits;
            break;
        }
    }
    void OnPosStateChanged(PosState pos_state)
    {
        switch (type) {
            case Type.JUMP:
                if (pos_state == PosState.JUMP_RISING) TryIncreaseHits();
                break;
            case Type.PUSHTOGROUND:
                if (pos_state == PosState.JUMP_PUSHTG) TryIncreaseHits();
                break;
        }
    }
    void OnStrafeStateChanged(StrafeTo strafe_to)
    {
        if (strafe_to != StrafeTo.NONE) TryIncreaseHits();
    }
    void OnSlideStateChanged(bool is_sliding)
    {
        if (is_sliding) TryIncreaseHits();
    }
    void OnBumpStumbleCrash(ObstacleController obst)
    {
        if (use_obst_type) {
            if (obst.ObstType() == obst_type) TryIncreaseHits();
        } else {
            TryIncreaseHits();
        }
    }
    void OnSpeedStateChanged(PlayerSpeedState speed_state)
    {
        switch (type) {
            case Type.TIRED:
                if (speed_state == PlayerSpeedState.TIRED) TryIncreaseHits();
                break;
            case Type.TURBO:
                if (speed_state == PlayerSpeedState.TURBO) TryIncreaseHits();
                break;
        }
    }
    void DoReset()
    {
        current_hits = 0;
    }
    void TryIncreaseHits()
    {
        if (base.IsGreen() && ++current_hits >= num_hits) {
            Complete();
            //report
            if (onStateChanged != null) { onStateChanged(this, ChallengeState.GREEN); }
        }
    }
}
public class DoChaserHit : ChComposite
{
    public enum Type { LOSE, CRASH, CRASH_LOSE }

    /*[SetInEditor]*/
    public bool not = false;
    /*[SetInEditor]*/
    public Type type = Type.LOSE;
    /*[SetInEditor]*/
    public int num_hits = 1;
    /*[SetInEditor]*/
    public bool use_obst_type = false;
    /*[SetInEditor]*/
    public ObstacleType obst_type;
    /*[SetInEditor]*/
    public bool reset_on_playing = true;
#if UNITY_EDITOR
    [InspectorHeader("UI"), InspectorMargin(10), InspectorDivider, ShowInInspector, InspectorHidePrimary]
    bool __inspector_ui;
#endif
    /*[SetInEditor]*/
    public string task_desc = string.Empty;
    /*[SetInEditor]*/
    public string progress_desc = string.Empty;
    /*[SetInEditor]*/
    [InspectorTextArea(45f)]
    public string tip_desc = string.Empty;

    int current_hits = 0;

    public override void OnEnable(
#if CODEDEBUG
        string debugData
#endif
        )
    {
        base.OnEnable(
#if CODEDEBUG
            debugData
#endif
            );

        DoReset();

#if CODEDEBUG
        if (num_hits < 1) {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogWarning(METHOD_NAME, (debug_data + " num_hits is {0}"), num_hits);
        }
        if (not && !reset_on_playing) {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogWarning(METHOD_NAME, (debug_data + "not is {0}, reset_on_playing is {1}"), not, reset_on_playing);
        }
#endif
    }
    public override void OnPlayingBegin(bool forceReset)
    {
        if (!forceReset && !not && IsGreen()) {
#if CODEDEBUG
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.Log(METHOD_NAME, (debug_data + " already green"));
#endif
            return;
        }

        if (reset_on_playing || forceReset) {
            DoReset();
        }

        if (!ConnectEvents()) {
#if CODEDEBUG
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogError(METHOD_NAME, (debug_data + " events not connected"));
#endif
            return;
        }

        base.OnPlayingBegin(forceReset);
    }
    public override void OnPlayingEnd() 
    {
        base.OnPlayingEnd();
        DisconnectEvents();
    }
    public override bool IsGreen()
    {
        return !not == (current_hits >= num_hits);
    }
    public override void Complete()
    {
        base.Complete();
        //disconnect
        current_hits = num_hits;
        OnPlayingEnd();
        OnDestroy();
    }
    public override bool IsSavingState()
    {
        //challenge cannot save green state
        return true;
        //return !reset_on_playing || base.IsSavingState();
    }
    public override object SaveState() 
    {
#if CODEDEBUG
        if (!IsSavingState()) {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogError(METHOD_NAME, (debug_data + " should not save"));
        }
#endif
        bool this_saving = !reset_on_playing;
        bool base_saving = base.IsSavingState();
        if (this_saving && base_saving) {
            return new object[2] { current_hits, base.SaveState() };
        } else if(base_saving){
            return base.SaveState();
        } else if (this_saving) {
            return current_hits;
        }
#if CODEDEBUG
 else {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogError(METHOD_NAME, (debug_data + " nobody saving"));
        }
#endif
        return null;
    }
    public override void LoadState(object data)
    {
#if CODEDEBUG
        string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
        if (!IsSavingState()) {
            GameController.LogError(METHOD_NAME, (debug_data + " should not load"));
            return;
        }
#endif
        if (data == null) {
#if CODEDEBUG
            GameController.LogError(METHOD_NAME, (debug_data + " data is NULL"));
#endif
            return;
        }
        bool this_saving = !reset_on_playing;
        bool base_saving = base.IsSavingState();
        if (this_saving && base_saving) {
            object[] state = data as object[];
            if (state == null) {
#if CODEDEBUG
                GameController.LogError(METHOD_NAME, (debug_data + " state is not array"));
#endif
                return;
            }
            if (state.Length != 2) {
#if CODEDEBUG
                GameController.LogError(METHOD_NAME, (debug_data + " state.Length is {0}, expected {1}"), state.Length, 2);
                return;
#endif
            }
            //load base
            base.LoadState(state[1]);
            if (!(state[0] is int)) {
#if CODEDEBUG
                GameController.LogError(METHOD_NAME, (debug_data + " state[0] is not int"));
                return;
#endif
            }
            //load this
            current_hits = (int)state[0];
        } else if (base_saving) {
            //load base
            base.LoadState(data);
        } else if (this_saving) {
            if (!(data is int)) {
#if CODEDEBUG
                GameController.LogError(METHOD_NAME, (debug_data + " data is not int"));
                return;
#endif
            }
            //load this
            current_hits = (int)data;
        }
#if CODEDEBUG
 else {
            GameController.LogError(METHOD_NAME, (debug_data + " nobody loading"));
        }
#endif
    }
    public override string TaskDesc()
    {
        return string.Format(GameController.Instance.Localize(task_desc), num_hits);
    }
    public override string ProgressDesc() 
    {
        return current_hits < num_hits ? string.Format(GameController.Instance.Localize(progress_desc), num_hits - current_hits) : string.Empty;
    }
    public override string TipDesc()
    {
        return tip_desc;
    }
    public override GameObject MainUi() { return null; }
    public override GameObject NoteUi() { return null; }
    GameController.Event<IChallengeTask, ChallengeState> onStateChanged = null;
    public override void SetOnStateChanged(GameController.Event<IChallengeTask, ChallengeState> handler) { onStateChanged = handler; }

    void DoReset()
    {
        current_hits = 0;
    }

    bool ConnectEvents()
    {
        var chaser_events = GameController.Instance.ChaserChallengeEvents();
        if (chaser_events == null) {
#if CODEDEBUG
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogError(METHOD_NAME, "chaser_events is NULL");
#endif
            return false;
        }

        switch (type) {
        case Type.CRASH: chaser_events.onCrash += OnChaserCrash; break;
        case Type.CRASH_LOSE: chaser_events.onCrashLose += TryIncreaseHits; break;
        case Type.LOSE: chaser_events.onLose += TryIncreaseHits; break;
        }
        return true;
    }
    void DisconnectEvents()
    {
        var chaser_events = GameController.Instance.ChaserChallengeEvents();
        if(chaser_events == null) return;

        switch (type) {
        case Type.CRASH: chaser_events.onCrash -= OnChaserCrash; break;
        case Type.CRASH_LOSE: chaser_events.onCrashLose -= TryIncreaseHits; break;
        case Type.LOSE: chaser_events.onLose -= TryIncreaseHits; break;
        }
    }

    void OnChaserCrash(ObstacleController obst)
    {
        if (use_obst_type) {
            if (obst.ObstType() == obst_type) TryIncreaseHits();
        } else {
            TryIncreaseHits();
        }
    }
    void TryIncreaseHits()
    {
        //state changed detection here. NOT IsGreen
        if (base.IsGreen() && ++current_hits >= num_hits) {
            Complete();
            //report
            if (onStateChanged != null) { onStateChanged(this, ChallengeState.GREEN); }
        }
    }
}
public class DoCoinCollect : ChComposite
{
    /*[SetInEditor]*/
    public bool not = false;
    /*[SetInEditor]*/
    public CoinCategory coin_category = CoinCategory.COIN;
    /*[SetInEditor]*/
    public bool use_coin_type = false;
    /*[SetInEditor]*/
    public CoinType coin_type = CoinType.SMALL;
    /*[SetInEditor]*/
    public int num_hits = 1;
    /*[SetInEditor]*/
    public bool reset_on_playing = true;
#if UNITY_EDITOR
    [InspectorHeader("UI"), InspectorMargin(10), InspectorDivider, ShowInInspector, InspectorHidePrimary]
    bool __inspector_ui;
#endif
    /*[SetInEditor]*/
    public string task_desc = string.Empty;
    /*[SetInEditor]*/
    public string progress_desc = string.Empty;
    /*[SetInEditor]*/
    [InspectorTextArea(45f)]
    public string tip_desc = string.Empty;

    int current_hits = 0;

    public override void OnEnable(
#if CODEDEBUG
string debugData
#endif
)
    {
        base.OnEnable(
#if CODEDEBUG
debugData
#endif
);
        DoReset();

#if CODEDEBUG
        if (num_hits < 1) {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogWarning(METHOD_NAME, (debug_data + " num_hits is {0}"), num_hits);
        }
        if (not && !reset_on_playing) {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogWarning(METHOD_NAME, (debug_data + "not is {0}, reset_on_playing is {1}"), not, reset_on_playing);
        }
#endif
    }
    public override void OnPlayingBegin(bool forceReset)
    {
        if (!forceReset && !not && IsGreen()) {
#if CODEDEBUG
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.Log(METHOD_NAME, (debug_data + " already green"));
#endif
            return;
        }

        if (reset_on_playing || forceReset) {
            DoReset();
        }

        //connect
        if (use_coin_type) {
            GameController.Instance.onCoinCollected += OnCoinCollectedType;
        } else {
            if (coin_category == CoinCategory.COIN) {
                GameController.Instance.onCoinCollected += OnCoinCollectedCategoryValue;
            } else {
                GameController.Instance.onCoinCollected += OnCoinCollectedCategory;
            }
        }

        base.OnPlayingBegin(forceReset);
    }
    public override void OnPlayingEnd()
    {
        base.OnPlayingEnd();

        //disconnect
        GameController.Instance.onCoinCollected -= OnCoinCollectedTypeValue;
        GameController.Instance.onCoinCollected -= OnCoinCollectedType;
        GameController.Instance.onCoinCollected -= OnCoinCollectedCategoryValue;
        GameController.Instance.onCoinCollected -= OnCoinCollectedCategory;
    }
    public override bool IsGreen()
    {
        return !not == (current_hits >= num_hits);
    }
    public override void Complete()
    {
        base.Complete();
        current_hits = num_hits;
        //disconnect
        OnPlayingEnd();
        OnDestroy();
    }
    public override bool IsSavingState()
    {
        //challenge cannot save green state
        return true;
        //return !reset_on_playing || base.IsSavingState();
    }
    public override object SaveState()
    {
#if CODEDEBUG
        string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
        if (!IsSavingState()) {
            GameController.LogError(METHOD_NAME, (debug_data + " should not save"));
        }
#endif
        bool this_saving = !reset_on_playing;
        bool base_saving = base.IsSavingState();
        if (this_saving && base_saving) {
            return new object[2] { current_hits, base.SaveState() };
        } else if(base_saving) {
            return base.SaveState();
        } else if (this_saving) {
            return current_hits;
        }
#if CODEDEBUG
        GameController.LogWarning(METHOD_NAME, (debug_data + " nobody saving"));
#endif
        return null;
    }
    public override void LoadState(object data)
    {
#if CODEDEBUG
        string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
        if (!IsSavingState()) {
            GameController.LogError(METHOD_NAME, (debug_data + " should not load state"));
            return;
        }
#endif
        if (data == null) {
#if CODEDEBUG
            GameController.LogError(METHOD_NAME, (debug_data + " data is NULL"));
#endif
            return;
        }
        bool this_saving = !reset_on_playing;
        bool base_saving = base.IsSavingState();
        if (this_saving && base_saving) {
            object[] state = data as object[];
            if (state == null) {
#if CODEDEBUG
                GameController.LogError(METHOD_NAME, (debug_data + " state is not array"));
#endif
                return;
            }
            if (state.Length != 2) {
#if CODEDEBUG
                GameController.LogError(METHOD_NAME, (debug_data + " state.Length is {0}, expected {1}"), state.Length, 2);
                return;
#endif
            }
            //load base
            base.LoadState(state[1]);
            if (!(state[0] is int)) {
#if CODEDEBUG
                GameController.LogError(METHOD_NAME, (debug_data + " state[0] is not int"));
                return;
#endif
            }
            //load this
            current_hits = (int)state[0];
        } else if(base_saving) {
            //load base
            base.LoadState(data);
        } else if(this_saving) {
            if (!(data is int)) {
#if CODEDEBUG
                GameController.LogError(METHOD_NAME, (debug_data + " data is not int"));
                return;
#endif
            }
            //load this
            current_hits = (int)data;
        }
#if CODEDEBUG
 else {
            GameController.LogError(METHOD_NAME, (debug_data + " nobody loading"));
        }
#endif
    }
    public override string TaskDesc()
    {
        return string.Format(GameController.Instance.Localize(task_desc), num_hits);
    }
    public override string ProgressDesc() 
    {
        return current_hits < num_hits ? string.Format(GameController.Instance.Localize(progress_desc), num_hits - current_hits) : string.Empty;
    }
    public override string TipDesc()
    {
        return tip_desc;
    }
    public override GameObject MainUi() { return null; }
    public override GameObject NoteUi() { return null; }
    GameController.Event<IChallengeTask, ChallengeState> onStateChanged = null;
    public override void SetOnStateChanged(GameController.Event<IChallengeTask, ChallengeState> handler)
    {
        onStateChanged = handler;
    }

    void DoReset()
    {
        current_hits = 0;
    }
    void OnCoinCollectedType(CoinSharedData coin)
    {
        if (coin.type == coin_type) TryIncreaseHits();
    }
    void OnCoinCollectedTypeValue(CoinSharedData coin)
    {
        if (coin.type == coin_type) TryIncreaseHits(coin.coin_value);
    }
    void OnCoinCollectedCategoryValue(CoinSharedData coin)
    {
        if (coin.category == coin_category) TryIncreaseHits(coin.coin_value);
    }
    void OnCoinCollectedCategory(CoinSharedData coin)
    {
        if (coin.category == coin_category) TryIncreaseHits();
    }
    void TryIncreaseHits(int hits_amount = 1)
    {
        //state changed detection here. NOT IsGreen
        if (base.IsGreen() && (current_hits += hits_amount) >= num_hits) {
            Complete();
            //report
            if (onStateChanged != null) { onStateChanged(this, not ? ChallengeState.RED : ChallengeState.GREEN); }
        }
    }
}
public class DoCoinBank : IChallengeTask
{
    /*[SetInEditor]*/
    public int num_hits = 1;
    /*[SetInEditor]*/
    public bool reset_on_playing = true;
#if UNITY_EDITOR
    [InspectorHeader("UI"), InspectorMargin(10), InspectorDivider, ShowInInspector, InspectorHidePrimary]
    bool __inspector_ui;
#endif
    /*[SetInEditor]*/
    public string task_desc = string.Empty;
    /*[SetInEditor]*/
    public string progress_desc = string.Empty;
    /*[SetInEditor]*/
    [InspectorTextArea(45f)]
    public string tip_desc = string.Empty;

    int current_hits = 0;

#if CODEDEBUG
    string debug_data = string.Empty;
#endif

    public void OnEnable(
#if CODEDEBUG
string debugData
#endif
)
    {
#if CODEDEBUG
        debug_data = debugData;

        if (num_hits < 1) {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogWarning(METHOD_NAME, (debug_data + " num_hits is {0}"), num_hits);
        }
#endif

        DoReset();
    }
    public void OnDestroy() { }
    public void OnPlayingBegin(bool forceReset)
    {
        if (!forceReset && IsGreen()) {
#if CODEDEBUG
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.Log(METHOD_NAME, (debug_data + " already green"));
#endif
            return;
        }

        if (reset_on_playing || forceReset) {
            DoReset();
        }

        //connect
        GameController.Instance.onCoinsBanked -= TryIncreaseHits;
        GameController.Instance.onCoinsBanked += TryIncreaseHits;
    }
    public void OnPlayingEnd()
    {
        //disconnect
        GameController.Instance.onCoinsBanked -= TryIncreaseHits;
    }
    public bool IsGreen()
    {
        return current_hits >= num_hits;
    }
    public void Complete()
    {
        current_hits = num_hits;
        //disconnect
        OnPlayingEnd();
        OnDestroy();
    }
    public bool IsSavingState()
    {
        //challenge cannot save green state
        return true;
        //return !reset_on_playing;
    }
    public object SaveState()
    {
#if CODEDEBUG
        string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
        if (!IsSavingState()) {
            GameController.LogError(METHOD_NAME, (debug_data + " should not save"));
        }
#endif
        return current_hits;
    }
    public void LoadState(object data)
    {
        if (!IsSavingState()) {
#if CODEDEBUG
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogError(METHOD_NAME, debug_data + "should not load state");
#endif
            return;
        }

        if (data is int) {
            current_hits = (int)data;
        }
#if CODEDEBUG
 else {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogError(METHOD_NAME, debug_data + "data is not int");
        }
#endif
    }

    void DoReset()
    {
        current_hits = 0;
    }
    void TryIncreaseHits(int hits_amount)
    {
        //state changed detection here. NOT IsGreen
        if ((current_hits += hits_amount) >= num_hits) {
            Complete();
            //report
            if (onStateChanged != null) { onStateChanged(this, ChallengeState.GREEN); }
        }
    }
    public string TaskDesc()
    {
        return string.Format(GameController.Instance.Localize(task_desc), num_hits);
    }
    public string ProgressDesc()
    {
        return current_hits < num_hits ? string.Format(GameController.Instance.Localize(progress_desc), num_hits - current_hits) : string.Empty;
    }
    public string TipDesc()
    {
        return current_hits < num_hits ? tip_desc : string.Empty;
    }
    public GameObject MainUi() { return null; }
    public GameObject NoteUi() { return null; }
    GameController.Event<IChallengeTask, ChallengeState> onStateChanged = null;
    public void SetOnStateChanged(GameController.Event<IChallengeTask, ChallengeState> handler)
    {
        onStateChanged = handler;
    }
}
public class DoSpecialCoinCollect : IChallengeTask
{
    /*[SetInEditor]*/
    public int coin_index = 0;
    /*[SetInEditor]*/
    [InspectorRange(0f, 1f)]
    public float coin_prob = 0f;
    /*[SetInEditor]*/
    public int num_hits = 1;
    /*[SetInEditor]*/
    public bool reset_on_playing = true;
#if UNITY_EDITOR
    [InspectorHeader("UI"), InspectorMargin(10), InspectorDivider, ShowInInspector, InspectorHidePrimary]
    bool __inspector_ui;
#endif
    /*[SetInEditor]*/
    public string task_desc = string.Empty;
    /*[SetInEditor]*/
    public string progress_desc = string.Empty;
    /*[SetInEditor]*/
    [InspectorTextArea(45f)]
    public string tip_desc = string.Empty;

    PrefabPool special_coin = null;
    SuperCoinPlacer coin_placer = null;
    int current_hits = 0;

#if CODEDEBUG
    string debug_data = string.Empty;
#endif

    public void OnEnable(
#if CODEDEBUG
        string debugData
#endif
)
    {
#if CODEDEBUG
        debug_data = debugData;

        if (num_hits < 1) {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogWarning(METHOD_NAME, (debug_data + " num_hits is {0}"), num_hits);
        }
#endif
        DoReset();
    }
    public void OnDestroy()
    {
    }
    public void OnPlayingBegin(bool forceReset)
    {
#if CODEDEBUG
        string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
#endif
        if (!forceReset && IsGreen()) {
#if CODEDEBUG
            GameController.Log(METHOD_NAME, "already green");
#endif
            return;
        }

        if (reset_on_playing || forceReset) {
            DoReset();
        }

        if (special_coin == null) {
            special_coin = GameController.Instance.InitSpecialCoin(coin_index);
        }
        if (special_coin == null) {
#if CODEDEBUG
            GameController.LogWarning(METHOD_NAME, (debug_data + ".special_coin is NULL"));
#endif
            return;
        }

        if (coin_placer == null) {
            coin_placer = new SimpleSuperCoinPlacer() {
                probability = coin_prob,
                place_method = special_coin.PlaceAsCoin
            };
        }

        //request coin
        var coin_data = special_coin.GetSharedData<CoinSharedData>();
        coin_data.pick_method = OnCoinCollected;
        coin_data.can_fly = PlaycharLevel.MagnetPullsCoin(coin_data.type, GameController.Instance.SelectedPlaycharLevel(PlaycharLevelType.MAG_POWER));

        //connect
        GameController.Instance.AddSuperCoinPlacer(coin_placer);
    }
    public void OnPlayingEnd()
    {
        if (special_coin == null) {
            return;
        }

        GameController.Instance.ReleaseSpecialCoin(coin_index);
        special_coin = null;

        //disconnect
        GameController.Instance.RemoveSuperCoinPlacer(coin_placer);
    }
    public bool IsGreen() { return (current_hits >= num_hits); }
    public void Complete()
    {
        current_hits = num_hits;
        //disconnect
        OnPlayingEnd();
        //release coin
        OnDestroy();
    }
    public bool IsSavingState()
    {
        //challenge cannot save green state
        return true;
        //return !reset_on_playing;
    }
    public object SaveState()
    {
#if CODEDEBUG
        if (!IsSavingState()) {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogError(METHOD_NAME, debug_data + "should not save state");
        }
#endif
        return current_hits;
    }
    public void LoadState(object data)
    {
        if (!IsSavingState()) {
#if CODEDEBUG
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogError(METHOD_NAME, debug_data + "should not load state");
#endif
            return;
        }

        if (data is int) {
            current_hits = (int)data;
        } 
#if CODEDEBUG
        else {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogError(METHOD_NAME, debug_data + "data is not int");
        }
#endif
    }
    public string TaskDesc()
    {
        return string.Format(GameController.Instance.Localize(task_desc), num_hits);
    }
    public string ProgressDesc()
    {
        return current_hits < num_hits ? string.Format(GameController.Instance.Localize(progress_desc), num_hits - current_hits) : string.Empty;
    }
    public string TipDesc()
    {
        return tip_desc;
    }
    public GameObject MainUi() { return null; }
    public GameObject NoteUi() { return null; }

    void DoReset()
    {
        current_hits = 0;
    }
    void OnCoinCollected(CoinSharedData data)
    {
        //report collected
        GameController.Instance.ReportPlaycharCoinCollected(data);
        //state changed detection here. NOT IsGreen
        if (++current_hits >= num_hits) {
            Complete();
            //report
            if (onStateChanged != null) { onStateChanged(this, ChallengeState.GREEN); }
        }
    }

    GameController.Event<IChallengeTask, ChallengeState> onStateChanged = null;
    public void SetOnStateChanged(GameController.Event<IChallengeTask, ChallengeState> handler) { onStateChanged = handler; }
}
public class DoLetterCoinsCollect : IChallengeTask
{
    const char DELIMITER = '.';
    /*[SetInEditor]*/
    public string letter_string = string.Empty;
    /*[SetInEditor]*/
    [InspectorRange(0f, 1f)]
    public float coin_prob = 0f;
    /*[SetInEditor]*/
    public bool reset_on_playing = true;
#if UNITY_EDITOR
    [InspectorHeader("UI"), InspectorMargin(10), InspectorDivider, ShowInInspector, InspectorHidePrimary]
    bool __inspector_ui;
#endif
    /*[SetInEditor]*/
    public string task_desc = string.Empty;

    //No unicode, No spaces
    string[] coin_letters = null;
    //Unicode, Spaces
    string[] note_letters = null;
    PrefabPool[] coins = null;
    SuperCoinPlacer coin_placer = null;
    int current_hits = 0;

    GameObject ui_main_go = null;
    GameObject ui_note_go = null;
    Text ui_main_green_txt = null;
    Text ui_main_red_txt = null;
    Text ui_note_green_txt = null;
    Text ui_note_red_txt = null;
    bool ui_needs_update = true;
    string green_text = string.Empty;
    string red_text = string.Empty;

#if CODEDEBUG
    string debug_data = string.Empty;
#endif

    public void OnEnable(
#if CODEDEBUG
        string debugData
#endif
) 
    {
#if CODEDEBUG
        debug_data = debugData;
#endif

        if (string.IsNullOrEmpty(letter_string)) {
#if CODEDEBUG
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogError(METHOD_NAME, "letter_string is NULL");
#endif
            return;
        }

        //coin letters
        coin_letters = letter_string.Split(DELIMITER);
        List<string> coin_letters_list = new List<string>(coin_letters.Length);
        for (int i = 0, l = coin_letters.Length; i < l; ++i) {
            string cl = coin_letters[i];
            if (!string.IsNullOrEmpty(cl) && !char.IsWhiteSpace(cl[0])) {
                coin_letters_list.Add(cl);
            }
        }
        coin_letters = coin_letters_list.ToArray();

        //note letters
        note_letters = letter_string.Split(DELIMITER);
        for (int i = 0, l = note_letters.Length; i < l; ++i) {
            string letter = note_letters[i];
            //skip whitespace
            if (char.IsWhiteSpace(letter[0])) continue;

            //skip proper latin chars
            if (letter.Length == 1) continue;

            string replace = string.Empty;
            if (!GameController.Instance.ui_so.chs_letter_replace.TryGetValue(letter, out replace)) {
#if CODEDEBUG
                string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
                GameController.LogError(METHOD_NAME, "cannot find replace for {0}", letter);
#endif
                continue;
            }

            note_letters[i] = replace;
        }

        DoReset();
    }
    public void OnDestroy()
    {
        if (ui_main_go != null) {
            GameObject.Destroy(ui_main_go);
            ui_main_go = null;
        }
        if (ui_note_go != null) {
            GameObject.Destroy(ui_note_go);
            ui_note_go = null;
        }
        ui_main_green_txt = null;
        ui_main_red_txt = null;
        ui_note_green_txt = null;
        ui_note_red_txt = null;
    }
    public void OnPlayingBegin(bool forceReset)
    {
#if CODEDEBUG
        string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
#endif
        if (!forceReset && IsGreen()) {
#if CODEDEBUG
            GameController.Log(METHOD_NAME, (debug_data + " already green"));
#endif
            return;
        }

        //request coins
        if (coins == null) {
            coins = GameController.Instance.InitLetterCoins(coin_letters);
        }
        if (coins == null) {
#if CODEDEBUG
            GameController.LogWarning(METHOD_NAME, (debug_data + " coins is NULL"));
#endif
            return;
        }

        if (coin_placer == null) {
            coin_placer = new SimpleSuperCoinPlacer() {
                probability = coin_prob,
                place_method = PlaceCoin
            };
        }

        if (reset_on_playing || forceReset) {
            DoReset();
        }

        //connect
        GameController.Instance.AddSuperCoinPlacer(coin_placer);
        for(int i = 0, l = coins.Length; i < l; ++i) {
            var coin_data = coins[i].GetSharedData<CoinSharedData>();
            coin_data.pick_method = OnCoinCollected;
            coin_data.can_fly = PlaycharLevel.MagnetPullsCoin(coin_data.type, GameController.Instance.SelectedPlaycharLevel(PlaycharLevelType.MAG_POWER));
        }
    }
    public void OnPlayingEnd()
    {
        if (coins == null) {
            return;
        }

        GameController.Instance.ReleaseLetterCoins();
        coins = null;

        //disconnect
        GameController.Instance.RemoveSuperCoinPlacer(coin_placer);
    }
    public bool IsGreen()
    {
        return current_hits >= coin_letters.Length;
    }
    public void Complete()
    {
        current_hits = coin_letters.Length;
        //disconnect
        OnPlayingEnd();
        //dont destroy. UI is still in use
    }
    public bool IsSavingState()
    {
        //challenge cannot save green state
        return true;
        //return !reset_on_playing;
    }
    public object SaveState()
    {
#if CODEDEBUG
        if (!IsSavingState()) {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogError(METHOD_NAME, (debug_data + " should not save"));
        }
#endif
        return current_hits; 
    }
    public void LoadState(object data)
    {
        if (!IsSavingState()) {
#if CODEDEBUG
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogError(METHOD_NAME, debug_data + "should not load state");
#endif
            return;
        }

        if (data is int) {
            current_hits = (int)data;
            ui_needs_update = true;
        }
#if CODEDEBUG
 else {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogError(METHOD_NAME, debug_data + "data is not int");
        }
#endif
    }
    public string TaskDesc() { return string.Empty; }
    public string ProgressDesc() { return string.Empty; }
    public string TipDesc() { return string.Empty; }
    public GameObject MainUi()
    {
        if (ui_main_go == null) {
            ui_main_go = GameObject.Instantiate(GameController.Instance.ui_so.chs_letters_main_prefab) as GameObject;
            Transform letters_tr = ui_main_go.transform.Find("Letters");
            ui_main_green_txt = letters_tr.Find("Green/Text").GetComponent<Text>();
            ui_main_red_txt = letters_tr.Find("Red/Text").GetComponent<Text>();
            //desc
            ui_main_go.transform.Find("Desc").GetComponent<Text>().text = task_desc;
        }
        UpdateUiStrings();
        ui_main_green_txt.text = green_text;
        ui_main_red_txt.text = red_text;
        return ui_main_go;
    }
    public GameObject NoteUi()
    {
        if (ui_note_go == null) {
            ui_note_go = GameObject.Instantiate(GameController.Instance.ui_so.chs_letters_note_prefab) as GameObject;
            ui_note_green_txt = ui_note_go.transform.Find("Green/Text").GetComponent<Text>();
            ui_note_red_txt = ui_note_go.transform.Find("Red/Text").GetComponent<Text>();
        }
        UpdateUiStrings();
        ui_note_green_txt.text = green_text;
        ui_note_red_txt.text = red_text;
        return ui_note_go; 
    }
    void UpdateUiStrings()
    {
        if (!ui_needs_update) return;

        int num_items_to_green = 0;
        if (current_hits > 0) {
            for (int i = 0; i < current_hits; ++num_items_to_green) {
                if (char.IsWhiteSpace(note_letters[num_items_to_green][0])) continue;
                ++i;
            }
            if (num_items_to_green < note_letters.Length) {
                string[] green = new string[num_items_to_green];
                System.Array.Copy(note_letters, green, num_items_to_green);
                green_text = string.Join(null, green);
            } else {
                green_text = string.Join(null, note_letters);
            }
        } else {
            green_text = string.Empty;
        }

        int num_items_to_red = note_letters.Length - num_items_to_green;
        if(num_items_to_red > 0) {
            if (num_items_to_green > 0) {
                string[] red = new string[num_items_to_red];
                System.Array.Copy(note_letters, num_items_to_green, red, 0, num_items_to_red);
                red_text = string.Join(null, red);
            } else {
                red_text = string.Join(null, note_letters);
            }
        } else {
            red_text = string.Empty;
        }

        ui_needs_update = false;
    }
    void DoReset()
    {
        current_hits = 0;
        ui_needs_update = true;
    }
    void OnCoinCollected(CoinSharedData data)
    {
        //report collected
        GameController.Instance.ReportPlaycharCoinCollected(data);

        ui_needs_update = true;
        //state changed detection here. NOT IsGreen
        if (++current_hits >= coins.Length) {
            Complete();
            if (onStateChanged != null) { onStateChanged(this, ChallengeState.GREEN); }
        } else {
            if (onStateChanged != null) { onStateChanged(this, ChallengeState.NOTE); }
        }
    }
    void PlaceCoin(Vector3 offset, Transform parent)
    {
        coins[current_hits].PlaceAsCoin(offset, parent);
    }

    GameController.Event<IChallengeTask, ChallengeState> onStateChanged = null;
    public void SetOnStateChanged(GameController.Event<IChallengeTask, ChallengeState> handler) { onStateChanged = handler; }
}
public class DoUserSpend : IChallengeTask
{
    /*[SetInEditor]*/
    public CurrencyType type = CurrencyType.COINS;
    /*[SetInEditor]*/
    public int num_hits = 100;
#if UNITY_EDITOR
    [InspectorHeader("UI"), InspectorMargin(10), InspectorDivider, ShowInInspector, InspectorHidePrimary]
    bool __inspector_ui;
#endif
    /*[SetInEditor]*/
    public string task_desc = string.Empty;
    /*[SetInEditor]*/
    public string progress_desc = string.Empty;
    /*[SetInEditor]*/
    [InspectorTextArea(45f)]
    public string tip_desc = string.Empty;

    int current_hits = 0;

#if CODEDEBUG
    string debug_data = string.Empty;
#endif

    public void OnEnable(
#if CODEDEBUG
        string debugData
#endif
)
    {
#if CODEDEBUG
        debug_data = debugData;

        if (num_hits < 1) {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogWarning(METHOD_NAME, (debug_data + " num_hits is {0}"), num_hits);
        }
#endif
        switch (type) {
        case CurrencyType.COINS: GameController.Instance.onUserCoinSpend += OnUserSpend; break;
        case CurrencyType.LUCK: GameController.Instance.onUserLuckSpend += OnUserSpend; break;
        }

        current_hits = 0;
    }
    public void OnDestroy()
    {
        switch (type) {
        case CurrencyType.COINS: GameController.Instance.onUserCoinSpend -= OnUserSpend; break;
        case CurrencyType.LUCK: GameController.Instance.onUserLuckSpend -= OnUserSpend; break;
        }
    }
    public void OnPlayingBegin(bool forceReset) { }
    public void OnPlayingEnd() { }
    public bool IsGreen()
    {
        return current_hits >= num_hits;
    }
    public void Complete()
    {
        current_hits = num_hits;
        //disconnect
        OnPlayingEnd();
        OnDestroy();
    }
    public bool IsSavingState()
    {
        return true;
    }
    public object SaveState()
    {
        return current_hits;
    }
    public void LoadState(object data)
    {
        if (!IsSavingState()) {
#if CODEDEBUG
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogError(METHOD_NAME, debug_data + " should not load state");
#endif
            return;
        }

        if (data is int) {
            current_hits = (int)data;
        }
#if CODEDEBUG
 else {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogError(METHOD_NAME, debug_data + "data is not int");
        }
#endif
    }
    public string TaskDesc() {
        return string.Format(GameController.Instance.Localize(task_desc), num_hits);
    }
    public string ProgressDesc() 
    {
        return current_hits < num_hits ? string.Format(GameController.Instance.Localize(progress_desc), num_hits - current_hits) : string.Empty;
    }
    public string TipDesc()
    {
        return tip_desc;
    }
    public GameObject MainUi() { return null; }
    public GameObject NoteUi() { return null; }

    void OnUserSpend(int value)
    {
        //state changed detection here. NOT IsGreen
        if ((current_hits += value) >= num_hits) {
            Complete();
            //report
            if (onStateChanged != null) { onStateChanged(this, ChallengeState.GREEN); }
        }
    }

    GameController.Event<IChallengeTask, ChallengeState> onStateChanged = null;
    public void SetOnStateChanged(GameController.Event<IChallengeTask, ChallengeState> handler) { onStateChanged = handler; }
}
public class DoUserScore : IChallengeTask
{
    /*[SetInEditor]*/
    public int num_hits = 100;
    /*[SetInEditor]*/
    public bool reset_on_playing = true;
#if UNITY_EDITOR
    [InspectorHeader("UI"), InspectorMargin(10), InspectorDivider, ShowInInspector, InspectorHidePrimary]
    bool __inspector_ui;
#endif
    /*[SetInEditor]*/
    public string task_desc = string.Empty;
    /*[SetInEditor]*/
    public string progress_desc = string.Empty;
    /*[SetInEditor]*/
    [InspectorTextArea(45f)]
    public string tip_desc = string.Empty;

    int current_hits = 0;

#if CODEDEBUG
    string debug_data = string.Empty;
#endif
    public void OnEnable(
#if CODEDEBUG
        string debugData
#endif
)
    {
#if CODEDEBUG
        debug_data = debugData;
#endif
        DoReset();
    }
    public void OnDestroy()
    {
        //disconnect
        GameController.Instance.onUserScore -= OnUserScore;
    }
    public void OnPlayingBegin(bool forceReset)
    {
        if (!forceReset && IsGreen()) {
#if CODEDEBUG
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.Log(METHOD_NAME, (debug_data + " already green"));
#endif
            return;
        }

        if (reset_on_playing || forceReset) {
            DoReset();
        }
        //connect
        GameController.Instance.onUserScore -= OnUserScore;
        GameController.Instance.onUserScore += OnUserScore;
    }
    public void OnPlayingEnd() { }
    public bool IsGreen()
    {
        return current_hits >= num_hits;
    }
    public void Complete()
    {
        current_hits = num_hits;
        //disconnect
        OnPlayingEnd();
        OnDestroy();
    }
    public bool IsSavingState()
    {
        //challenge cannot save green state
        return true;
        //return !reset_on_playing;
    }
    public object SaveState()
    {
        if (IsSavingState()) {
            return current_hits;
        }
        return null;
    }
    public void LoadState(object data)
    {
        if (!IsSavingState()) {
#if CODEDEBUG
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogError(METHOD_NAME, debug_data + "should not load state");
#endif
            return;
        }

        if (data is int) {
            current_hits = (int)data;
        }
#if CODEDEBUG
 else {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogError(METHOD_NAME, debug_data + "data is not int");
        }
#endif
    }
    public string TaskDesc()
    {
        return string.Format(GameController.Instance.Localize(task_desc), num_hits);
    }
    public string ProgressDesc()
    {
        return current_hits < num_hits ? string.Format(GameController.Instance.Localize(progress_desc), num_hits - current_hits) : string.Empty;
    }
    public string TipDesc()
    {
        return tip_desc;
    }
    public GameObject MainUi() { return null; }
    public GameObject NoteUi() { return null; }

    void DoReset()
    {
        current_hits = 0;
    }
    void OnUserScore(int value)
    {
        //state changed detection here. NOT IsGreen
        if ((current_hits += value) >= num_hits) {
            Complete();
            //report
            if (onStateChanged != null) { onStateChanged(this, ChallengeState.GREEN); }
        }
    }

    GameController.Event<IChallengeTask, ChallengeState> onStateChanged = null;
    public void SetOnStateChanged(GameController.Event<IChallengeTask, ChallengeState> handler) { onStateChanged = handler; }
}
public class DoUserHighscore : IChallengeTask
{
#if UNITY_EDITOR
    [InspectorHeader("UI"), InspectorMargin(10), InspectorDivider, ShowInInspector, InspectorHidePrimary]
    bool __inspector_ui;
#endif
    /*[SetInEditor]*/
    public string task_desc = string.Empty;
    /*[SetInEditor]*/
    public string progress_desc = string.Empty;

    bool highscore_changed = false;

    public void OnEnable(
#if CODEDEBUG
string debugData
#endif
)
    {
        GameController.Instance.onUserHighscoreChanged += OnUserHighscore;

        highscore_changed = false;
    }
    public void OnDestroy()
    {
        GameController.Instance.onUserHighscoreChanged -= OnUserHighscore;
    }
    public void OnPlayingBegin(bool forceReset) { }
    public void OnPlayingEnd() { }
    public bool IsGreen()
    {
        return highscore_changed;
    }
    public void Complete()
    {
        highscore_changed = true;
        //disconnect
        OnPlayingEnd();
        OnDestroy();
    }
    public bool IsSavingState() { return false; }
    public object SaveState() { return null; }
    public void LoadState(object state) { }
    public string TaskDesc() { return GameController.Instance.Localize(task_desc); }
    public string ProgressDesc() { return string.Format(GameController.Instance.Localize(progress_desc), GameController.Instance.USER_Highscore()); }
    public string TipDesc() { return string.Empty; }
    public GameObject MainUi() { return null; }
    public GameObject NoteUi() { return null; }

    void OnUserHighscore()
    {
        //state changed detection here. NOT IsGreen
        Complete();
        //report
        if (onStateChanged != null) { onStateChanged(this, ChallengeState.GREEN); }
    }

    GameController.Event<IChallengeTask, ChallengeState> onStateChanged = null;
    public void SetOnStateChanged(GameController.Event<IChallengeTask, ChallengeState> handler) { onStateChanged = handler; }
}
public class DoUseStartItem : IChallengeTask
{
    /*[SetInEditor]*/
    public bool not = false;
    /*[SetInEditor]*/
    public bool use_type = false;
    /*[SetInEditor]*/
    public UserInvItemType type;
    /*[SetInEditor]*/
    public int num_hits = 1;
#if UNITY_EDITOR
    [InspectorHeader("UI"), InspectorMargin(10), InspectorDivider, ShowInInspector, InspectorHidePrimary]
    bool __inspector_ui;
#endif
    /*[SetInEditor]*/
    public string task_desc = string.Empty;
    /*[SetInEditor]*/
    public string progress_desc = string.Empty;
    /*[SetInEditor]*/
    [InspectorTextArea(45f)]
    public string tip_desc = string.Empty;

    int current_hits = 0;

#if CODEDEBUG
    string debug_data = string.Empty;
#endif

    public void OnEnable(
#if CODEDEBUG
string debugData
#endif
)
    {
#if CODEDEBUG
        debug_data = debugData;

        if (num_hits < 1) {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogWarning(METHOD_NAME, (debug_data + " num_hits is {0}"), num_hits);
        }
#endif

        DoReset();
    }
    public void OnDestroy() { }
    public void OnPlayingBegin(bool forceReset)
    {
        if (!forceReset && !not && IsGreen()) {
#if CODEDEBUG
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.Log(METHOD_NAME, (debug_data + " already green"));
#endif
            return;
        }

        if (forceReset) {
            DoReset();
        }

        //connect
        GameController.Instance.onStartItemUsed -= TryIncreaseHits;
        GameController.Instance.onStartItemUsed += TryIncreaseHits;
    }
    public void OnPlayingEnd()
    {
        //disconnect
        GameController.Instance.onStartItemUsed -= TryIncreaseHits;
    }
    public bool IsGreen()
    {
        return !not == current_hits >= num_hits;
    }
    public void Complete()
    {
        current_hits = num_hits;
        //disconnect
        OnPlayingEnd();
        OnDestroy();
    }
    public bool IsSavingState()
    {
        return true;
    }
    public object SaveState()
    {
        return current_hits;
    }
    public void LoadState(object data)
    {
        if (data is int) {
            current_hits = (int)data;
        }
#if CODEDEBUG
 else {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogError(METHOD_NAME, debug_data + "data is not int");
        }
#endif
    }

    void DoReset()
    {
        current_hits = 0;
    }
    void TryIncreaseHits(UserInvItemType itemType)
    {
        if (use_type && type != itemType) return;

        //state changed detection here. NOT IsGreen
        if ((++current_hits) >= num_hits) {
            Complete();
            //report
            if (onStateChanged != null) { onStateChanged(this, not ? ChallengeState.RED : ChallengeState.GREEN); }
        }
    }
    public string TaskDesc()
    {
        return string.Format(GameController.Instance.Localize(task_desc), num_hits);
    }
    public string ProgressDesc()
    {
        return current_hits < num_hits ? string.Format(GameController.Instance.Localize(progress_desc), num_hits - current_hits) : string.Empty;
    }
    public string TipDesc()
    {
        return tip_desc;
    }
    public GameObject MainUi() { return null; }
    public GameObject NoteUi() { return null; }
    GameController.Event<IChallengeTask, ChallengeState> onStateChanged = null;
    public void SetOnStateChanged(GameController.Event<IChallengeTask, ChallengeState> handler)
    {
        onStateChanged = handler;
    }
}
public class DoUserBuyItem : IChallengeTask
{
    /*[SetInEditor]*/
    public UserInvItemType type;
    /*[SetInEditor]*/
    public int num_hits = 100;
#if UNITY_EDITOR
    [InspectorHeader("UI"), InspectorMargin(10), InspectorDivider, ShowInInspector, InspectorHidePrimary]
    bool __inspector_ui;
#endif
    /*[SetInEditor]*/
    public string task_desc = string.Empty;
    /*[SetInEditor]*/
    public string progress_desc = string.Empty;
    /*[SetInEditor]*/
    [InspectorTextArea(45f)]
    public string tip_desc = string.Empty;

    int current_hits = 0;

#if CODEDEBUG
    string debug_data = string.Empty;
#endif

    public void OnEnable(
#if CODEDEBUG
string debugData
#endif
)
    {
#if CODEDEBUG
        debug_data = debugData;

        if (num_hits < 1) {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogWarning(METHOD_NAME, (debug_data + " num_hits is {0}"), num_hits);
        }
#endif
        //connect
        GameController.Instance.onUserItemBuy += OnUserBuy;

        //reset
        current_hits = 0;
    }
    public void OnDestroy()
    {
        //disconnect
        GameController.Instance.onUserItemBuy -= OnUserBuy;
    }
    public void OnPlayingBegin(bool forceReset) { }
    public void OnPlayingEnd() { }
    public bool IsGreen()
    {
        return current_hits >= num_hits;
    }
    public void Complete()
    {
        current_hits = num_hits;
        //disconnect
        OnPlayingEnd();
        OnDestroy();
    }
    public bool IsSavingState()
    {
        return true;
    }
    public object SaveState()
    {
        return current_hits;
    }
    public void LoadState(object data)
    {
        if (!IsSavingState()) {
#if CODEDEBUG
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogError(METHOD_NAME, debug_data + "should not load state");
#endif
            return;
        }

        if (data is int) {
            current_hits = (int)data;
        }
#if CODEDEBUG
 else {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogError(METHOD_NAME, debug_data + "data is not int");
        }
#endif
    }
    public string TaskDesc()
    {
        return string.Format(GameController.Instance.Localize(task_desc), num_hits);
    }
    public string ProgressDesc()
    {
        return current_hits < num_hits ? string.Format(GameController.Instance.Localize(progress_desc), num_hits - current_hits) : string.Empty;
    }
    public string TipDesc()
    {
        return tip_desc;
    }
    public GameObject MainUi() { return null; }
    public GameObject NoteUi() { return null; }

    void OnUserBuy(UserInvItemType itemType, int value)
    {
        if (type != itemType) return;

        //state changed detection here. NOT IsGreen
        if ((current_hits += value) >= num_hits) {
            Complete();
            //report
            if (onStateChanged != null) { onStateChanged(this, ChallengeState.GREEN); }
        }
    }

    GameController.Event<IChallengeTask, ChallengeState> onStateChanged = null;
    public void SetOnStateChanged(GameController.Event<IChallengeTask, ChallengeState> handler) { onStateChanged = handler; }
}
public class DoUserBuyLevel : IChallengeTask
{
    /*[SetInEditor]*/
    public bool use_type = false;
    /*[SetInEditor]*/
    public PlaycharLevelType type;
    /*[SetInEditor]*/
    public int num_hits = 100;
#if UNITY_EDITOR
    [InspectorHeader("UI"), InspectorMargin(10), InspectorDivider, ShowInInspector, InspectorHidePrimary]
    bool __inspector_ui;
#endif
    /*[SetInEditor]*/
    public string task_desc = string.Empty;
    /*[SetInEditor]*/
    public string progress_desc = string.Empty;
    /*[SetInEditor]*/
    [InspectorTextArea(45f)]
    public string tip_desc = string.Empty;

    int current_hits = 0;

#if CODEDEBUG
    string debug_data = string.Empty;
#endif

    public void OnEnable(
#if CODEDEBUG
string debugData
#endif
)
    {
#if CODEDEBUG
        debug_data = debugData;

        if (num_hits < 1) {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogWarning(METHOD_NAME, (debug_data + " num_hits is {0}"), num_hits);
        }
#endif
        //connect
        GameController.Instance.onUserLevelBuy += OnUserBuy;

        //reset
        current_hits = 0;
    }
    public void OnDestroy()
    {
        //disconnect
        GameController.Instance.onUserLevelBuy -= OnUserBuy;
    }
    public void OnPlayingBegin(bool forceReset) { }
    public void OnPlayingEnd() { }
    public bool IsGreen()
    {
        return current_hits >= num_hits;
    }
    public void Complete()
    {
        current_hits = num_hits;
        //disconnect
        OnPlayingEnd();
        OnDestroy();
    }
    public bool IsSavingState()
    {
        return true;
    }
    public object SaveState()
    {
        return current_hits;
    }
    public void LoadState(object data)
    {
        if (!IsSavingState()) {
#if CODEDEBUG
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogError(METHOD_NAME, debug_data + "should not load state");
#endif
            return;
        }

        if (data is int) {
            current_hits = (int)data;
        }
#if CODEDEBUG
 else {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogError(METHOD_NAME, debug_data + "data is not int");
        }
#endif
    }
    public string TaskDesc()
    {
        return string.Format(GameController.Instance.Localize(task_desc), num_hits);
    }
    public string ProgressDesc()
    {
        return current_hits < num_hits ? string.Format(GameController.Instance.Localize(progress_desc), num_hits - current_hits) : string.Empty;
    }
    public string TipDesc()
    {
        return tip_desc;
    }
    public GameObject MainUi() { return null; }
    public GameObject NoteUi() { return null; }

    void OnUserBuy(PlaycharLevelType levelType)
    {
        if (use_type && type != levelType) return;

        //state changed detection here. NOT IsGreen
        if ((++current_hits) >= num_hits) {
            Complete();
            //report
            if (onStateChanged != null) { onStateChanged(this, ChallengeState.GREEN); }
        }
    }

    GameController.Event<IChallengeTask, ChallengeState> onStateChanged = null;
    public void SetOnStateChanged(GameController.Event<IChallengeTask, ChallengeState> handler) { onStateChanged = handler; }
}
public class Challenge
{
    /*[SetInEditor]*/
    public IChallengeTask[] tasks = null;

    GameController.NoteInfo chs_note = null;

#if CODEDEBUG
    string debug_data = string.Empty;
#endif
    public void Enable(
#if CODEDEBUG
        string debugData
#endif
)
    {
#if CODEDEBUG
        debug_data = debugData;
#endif
        foreach (var t in tasks) {
            t.SetOnStateChanged(OnTaskStateChanged);
            t.OnEnable(
#if CODEDEBUG
                debug_data
#endif
);
            if (chs_note == null) {
                chs_note = new GameController.NoteInfo();
            }
        }
    }
    public void Destroy()
    {
        for (int i = 0, l = tasks.Length; i < l; ++i) {
            tasks[i].OnDestroy();
        } 
    }
    public void OnPlayingBegin()
    {
        for (int i = 0, l = tasks.Length; i < l; ++i) {
            tasks[i].OnPlayingBegin(false);
        } 
    }
    public void OnPlayingEnd()
    {
        for (int i = 0, l = tasks.Length; i < l; ++i) {
            tasks[i].OnPlayingEnd();
        } 
    }
    public void LoadState(object data)
    {
#if CODEDEBUG
        string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
#endif
        if (tasks.Length == 0) {
#if CODEDEBUG
            GameController.LogError(METHOD_NAME, (debug_data + ".tasks.Length is {0}"), tasks.Length);
#endif
            return;
        }
        if (data == null) {
#if CODEDEBUG
            GameController.LogError(METHOD_NAME, (debug_data + " data is NULL"));
#endif
            return;
        }
        var state = data as object[];
        if (state == null) {
#if CODEDEBUG
            GameController.LogError(METHOD_NAME, (debug_data + " state is NULL"));
#endif
            return;
        }
        if (state.Length != tasks.Length) {
#if CODEDEBUG
            GameController.LogError(METHOD_NAME, (debug_data + " state.Length is {0}, tasks.Length is {1}, must be equal"), state.Length, tasks.Length);
#endif
            return;
        }
        for (int i = 0, l = tasks.Length; i < l; ++i) {
            if (tasks[i].IsSavingState()) {
                tasks[i].LoadState(state[i]);
            }
        }
    }
    public object SaveState()
    {
        object[] state = null;
        for (int i = 0, l = tasks.Length; i < l; ++i) {
            if (tasks[i].IsSavingState()) {
                if (state == null) { state = new object[tasks.Length]; }
                state[i] = tasks[i].SaveState();
            }
        }
        return state;
    }
    public bool IsGreen()
    {
        for (int i = 0, l = tasks.Length; i < l; ++i) {
            if (!tasks[i].IsGreen()) return false;
        }
        return true;
    }
    GameController.Event onGreen = null;
    public void SetOnGreen(GameController.Event handler) { onGreen = handler; }

    //UI
    public int NumTasks() { return tasks.Length; }
    public bool IsTaskGreen(int index) { return tasks[index].IsGreen(); }
    public void CompleteTask(int index) { tasks[index].Complete(); OnTaskStateChanged(tasks[index], ChallengeState.GREEN); }
    public GameObject TaskMainUi(int index) { return tasks[index].MainUi(); }
    public string TaskDesc(int index) { return tasks[index].TaskDesc(); }
    public string TaskProgressDesc(int index) { return tasks[index].ProgressDesc(); }
    public string TaskTipDesc(int index) { return tasks[index].TipDesc(); }

    void OnTaskStateChanged(IChallengeTask task, ChallengeState state)
    {
        switch (state) {
        case ChallengeState.GREEN:
            chs_note.icon = GameController.Instance.ui_so.chs_green_icon;
            chs_note.sound_type = GameController.UiSoundType.NOTE;
            chs_note.text = task.TaskDesc();
            chs_note.custom_ui = task.NoteUi();
            GameController.Instance.Notify(chs_note);
            if (IsGreen()) { onGreen(); }
            break;
        case ChallengeState.NOTE:
            chs_note.icon = null;
            chs_note.sound_type = GameController.UiSoundType.NOTE;
            chs_note.text = task.ProgressDesc();
            chs_note.custom_ui = task.NoteUi();
            GameController.Instance.Notify(chs_note);
            break;
        }
    }
}
public class ChallengeSerializeData
{
    /*[SetInEditor]*/
    public int id = 0;
    /*[SetInEditor]*/
    [InspectorDatabaseEditor]
    public Challenge[] ch = null;
}