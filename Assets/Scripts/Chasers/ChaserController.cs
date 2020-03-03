using UnityEngine;
using FullInspector;

public enum ChaseState
{
    DISABLED,
    STAND_ONROAD,
    CHASE_OFFSCREEN,
    CHASE_ONSCREEN,
    CHASE_ATTACK,
    CHASE_SLIDE,
    CRASH
}
public interface IChaserChallengeEvents
{
    event GameController.Event<ChaseState> onChaseStateChanged;
    event GameController.Event<ObstacleController> onCrash;
    event GameController.Event onLose;
    event GameController.Event onCrashLose;

    bool IsActive();
    bool IsChasing();
    bool IsOnScreen();
    bool IsStanding();
    bool IsAttacking();
}
public class ChaserController : BaseBehavior<FullSerializerSerializer>, IChaserChallengeEvents
{
    ChaseState last_chase_state = ChaseState.DISABLED;
    ChaseState chase_state = ChaseState.DISABLED;
    public ChaseState CurrentChaseState() { return chase_state; }
    public ChaseState LastChaseState() { return last_chase_state; }
    public bool IsChasing() { return chase_state == ChaseState.CHASE_ATTACK || chase_state == ChaseState.CHASE_ONSCREEN || chase_state == ChaseState.CHASE_OFFSCREEN || chase_state == ChaseState.CHASE_SLIDE; }
    public bool IsOnScreen() { return chase_state == ChaseState.CHASE_ONSCREEN || chase_state == ChaseState.CHASE_ATTACK || chase_state == ChaseState.CHASE_SLIDE; }
    public static bool IsOnScreenState(ChaseState state) { return state == ChaseState.CHASE_ONSCREEN || state == ChaseState.CHASE_ATTACK || state == ChaseState.CHASE_SLIDE; }
    public bool IsAttacking() { return chase_state == ChaseState.CHASE_ATTACK; }
    public bool IsActive() { return chase_state != ChaseState.DISABLED; }
    public bool IsStanding() { return chase_state == ChaseState.STAND_ONROAD; }
    GameController.Event chase_func = GameController.Stub;
    GameController.Event<Collider> trigger_enter_func = GameController.Stub;
    GameController.Event<Collider> trigger_exit_func = GameController.Stub;

    GameTween<float> zpos_tween = null;
    Vector3 chaser_pos = Vector3.zero;
    Vector3 chaser_offscreen_pos = Vector3.zero;

    /*[SetInEditor]*/
    public float onscreen_time = 2f;
    /*[SetInEditor]*/
    public float offscreen_time = 1f;
    /*[SetInEditor]*/
    public float attack_time = 2f;
    /*[SetInEditor]*/
    public float fallback_time = 0.6f;
    /*[SetInEditor]*/
    public float reaction_time_mult = 1.1f;
    /*[SetInEditor]*/
    public float drop_time = 1.0f;
    /*[SetInEditor]*/
    public float safe_time = 2f;

    GameTimer strafe_reaction_timer = null;
    GameTimer jump_reaction_timer = null;
    GameTimer drop_delay_timer = null;
    GameTimer safe_timer = null;

    /*[SetInEditor]*/
    public float jump_height_mult = 0.8f;
    GameTween<float> jump_fall_tween = null;
    float obstacle_slope_factor = 1f;
    float obst_floor_height = 0f;
    float FallTimeFromCurrentHeight() { return playchar.FallTime() * jump_height_mult * (this_rigidbody.position.y / (playchar.jump_height * jump_height_mult)); }

    /*[SetInEditor]*/
    [InspectorRange(0.1f, 2.0f)]
    public float strafe_time_mult = 0.5f;
    StrafeTo is_strafing_to = StrafeTo.NONE;

    GameTween<float> strafe_tween = null;
    int current_lane_index = 1; //center lane index

    PosState pos_state = PosState.GROUNDED_FLOOR;
    public PosState CurrentPosState() { return pos_state; }
    public bool IsGrounded() { return pos_state == PosState.GROUNDED_FLOOR || pos_state == PosState.GROUNDED_SLOPE; }

    public const float DROP_DAMAGE_OFFSCREEN = 1f;
    public const float DROP_DAMAGE_CRASH = 2f;
    float drop_total_damage = 0f;
    public float DropDamage() { return drop_total_damage; }
    int drop_hits = 0;

    GameObject chaser_node = null;
    GameObject shadow_go = null;

    public event GameController.Event<PosState> onPosStateChanged;
    public event GameController.Event onLand;
    public event GameController.Event<ObstacleController> onSideBump;
    public event GameController.Event onDropHit;
    //challenge events
    public event GameController.Event<ChaseState> onChaseStateChanged;
    public event GameController.Event<ObstacleController> onCrash;
    public event GameController.Event onLose;
    public event GameController.Event onCrashLose;

#if UNITY_EDITOR
    [InspectorHeader("Animation Control"), InspectorMargin(20), InspectorDivider, ShowInInspector, InspectorHidePrimary]
    bool __inspector_animation;
#endif
    /*[SetInEditor]*/
    public int run_sequence_index = 0;
    /*[SetInEditor]*/
    public int jumpfall_sequence_index = 0;
    /*[SetInEditor]*/
    public int fall_sequence_index = 0;
    /*[SetInEditor]*/
    public int land_sequence_index = 0;
    /*[SetInEditor]*/
    public int strafeleft_sequence_index = 0;
    /*[SetInEditor]*/
    public int straferight_sequence_index = 0;
    /*[SetInEditor]*/
    public int stumble_sequence_index = 0;
    /*[SetInEditor]*/
    public int stand_sequence_index = 0;
    /*[SetInEditor]*/
    public int slide_sequence_index = 0;
    /*[SetInEditor]*/
    public int air_strafeleft_sequence_index = 0;
    /*[SetInEditor]*/
    public int air_straferight_sequence_index = 0;

    AdvancedAnimationController chaser_anim = null;
    AdvancedAnimationController.Layer anim_main_layer = null;
    AdvancedAnimationController.Layer anim_add_layer = null;

#if UNITY_EDITOR
    [InspectorHeader("Curves"), InspectorMargin(20), InspectorDivider, ShowInInspector, InspectorHidePrimary]
    bool __inspector_curves;
#endif
    /*[SetInEditor]*/
    public bool use_jump_curves = false;
    /*[SetInEditor]*/
    public AnimationCurve jump_curve = null;
    /*[SetInEditor]*/
    public bool use_fall_curve = false;
    /*[SetInEditor]*/
    public AnimationCurve fall_curve = null;

    Collider current_floor_collider = null;
    Collider current_slope_collider = null;
    ObstacleController crash_obst = null;
    public ObstacleController CrashObstacle() { return crash_obst; }

    //particles
    Transform par_root_tr = null;
    public Transform ParticlesNode() { return par_root_tr; }

    Transform this_transform = null;
    Rigidbody this_rigidbody = null;
    PlayerController playchar = null;
    GameController gc = null;

    #region [PlayerController]
    /*[Direct call by PlayerController]*/
    public void Pl_Init()
    {
        gc = GameController.Instance;
        playchar = gc.PlaycharCtrl();
        this_transform = transform;
        this_rigidbody = GetComponent<Rigidbody>();

        current_lane_index = 1;
        chaser_offscreen_pos = new Vector3(0f, 0f, gc.pch_so.chaser_offscreen_zpos);

        if (chaser_node == null) {
            chaser_node = this_transform.Find("chaser_node").gameObject;
            chaser_anim = chaser_node.GetComponent<AdvancedAnimationController>();
            chaser_anim.Init();
            anim_main_layer = chaser_anim.layers[0];
            anim_add_layer = chaser_anim.additives[0];
        }
        if (shadow_go == null) {
            shadow_go = this_transform.Find("Shadow").gameObject;
            shadow_go.transform.SetParent(chaser_node.transform, false);
        }

        if (jump_fall_tween == null) {
            jump_fall_tween = new FloatTween();
            jump_fall_tween.SetTimeStepGetter(gc.PlayingTime);
            jump_fall_tween.SetBeginGetter(() => this_transform.localPosition.y, false);
        }

        if (strafe_tween == null) {
            strafe_tween = new FloatTween();
            strafe_tween.SetTimeStepGetter(gc.PlayingTime);
            strafe_tween.SetBeginGetter(() => this_transform.localPosition.x, false);
            strafe_tween.SetEndGetter(() => gc.THEME_LaneOffsetX(current_lane_index), false);
            strafe_tween.SetEase(Easing.CubicOut);
            strafe_tween.SetOnComplete(OnStrafeComplete);
            //duration is set in Pl_RunSpeedUpdated
        }

        if (zpos_tween == null) {
            zpos_tween = new FloatTween();
            zpos_tween.SetBeginGetter(() => this_transform.localPosition.z, false);
            zpos_tween.SetEase(Easing.QuadInOut);
        }

        if (jump_reaction_timer == null) {
            jump_reaction_timer = new GameTimer();
            jump_reaction_timer.SetTimeStepGetter(gc.PlayingTime);
            jump_reaction_timer.SetDurationGetter(ReactionDurationGetter);
            jump_reaction_timer.SetOnComplete(OnPlayerJumpReactionTime);
        }
        if (strafe_reaction_timer == null) {
            strafe_reaction_timer = new GameTimer();
            strafe_reaction_timer.SetTimeStepGetter(gc.PlayingTime);
            strafe_reaction_timer.SetDurationGetter(ReactionDurationGetter);
            strafe_reaction_timer.SetOnComplete(OnPlayerStrafeReactionTime);
        }
        if (drop_delay_timer == null) {
            drop_delay_timer = new GameTimer();
            drop_delay_timer.SetTimeStepGetter(gc.PlayingTime);
            drop_delay_timer.SetDurationGetter(() => (gc.pch_so.playchar_zpos - this_rigidbody.position.z) / playchar.RunSpeed());
        }
        if (safe_timer == null) {
            safe_timer = new GameTimer();
            safe_timer.SetTimeStepGetter(gc.PlayingTime);
            safe_timer.SetDuration(safe_time);
        }

        //particles
        par_root_tr = this_transform.Find("particles");

        playchar.onStrafeStateChanged += OnPlayerStrafeStateChanged;
        playchar.onPosStateChanged += OnPlayerPosChanged;
        playchar.onSlideStateChanged += OnPlayerSlideStateChanged;

        trigger_enter_func = GameController.Stub;
        trigger_exit_func = GameController.Stub;
        chase_func = GameController.Stub;
        last_chase_state = chase_state;
        chase_state = ChaseState.DISABLED;
    }
    /*[Direct call by PlayerController]*/
    public void Pl_PlaceOnRoad()
    {
        //register event to find empty place
        gc.onPatternBoxPlaced += OnPlaceOnRoad;
    }
    /*[Direct call by PlayerController]*/
    public void Pl_PlaceOffScreen(bool callEvents)
    {
        //place chaser
        if (callEvents) {
            OnOffScreenComplete();
        } else {
            _goOffscreen();
        }
    }
    /*[Direct call by PlayerController]*/
    public void Pl_PlayerRemoving()
    {
        playchar.onStrafeStateChanged -= OnPlayerStrafeStateChanged;
        playchar.onPosStateChanged -= OnPlayerPosChanged;
        playchar.onSlideStateChanged -= OnPlayerSlideStateChanged;

        gc.onPatternBoxPlaced -= OnPlaceOnRoad;

        //clear events
        onChaseStateChanged = null;
    }
    /*[Direct call by PlayerController]*/
    public void Pl_Attack()
    {
        if (!IsOnScreen() || IsAttacking() || is_strafing_to != StrafeTo.NONE) return;

        //attack
        zpos_tween.ClearEvents();
        zpos_tween.SetEndValue(gc.pch_so.playchar_zpos);
        zpos_tween.Restart(attack_time);
        
        last_chase_state = chase_state;
        chase_state = ChaseState.CHASE_ATTACK;
        if (onChaseStateChanged != null) { onChaseStateChanged(chase_state); }
    }
    /*[Direct call by PlayerController]*/
    public void Pl_Fallback()
    {
        if (!IsAttacking()) return;

        //fallback
        zpos_tween.SetEndValue(gc.pch_so.chaser_zpos - 0.7f);
        zpos_tween.ClearEvents();
        zpos_tween.Restart(fallback_time);

        //chaser animation
        if (IsGrounded()) {
            anim_main_layer.PlaySequence(stumble_sequence_index);
        }

        last_chase_state = chase_state;
        chase_state = ChaseState.CHASE_ONSCREEN;
        if (onChaseStateChanged != null) { onChaseStateChanged(chase_state); }
    }
    /*[Direct call by PlayerController]*/
    public void Pl_OnScreen2OffScreen()
    {
#if CODEDEBUG
        string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
        if (chase_state != ChaseState.CHASE_ONSCREEN) {
            GameController.LogError(METHOD_NAME, "ChaseState is {0}, expected {1}", chase_state, ChaseState.CHASE_ONSCREEN);
        }
#endif
        //to offscreen
        zpos_tween.SetEndValue(gc.pch_so.chaser_offscreen_zpos);
        zpos_tween.SetOnCompleteOnce(OnOffScreenComplete);
        zpos_tween.Restart(offscreen_time);
    }
    /*[Direct call by PlayerController]*/
    public void Pl_OffScreen2OnScreen()
    {
#if CODEDEBUG
        if (IsOnScreen()) {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogError(METHOD_NAME, "ChaseState is {0}, expected OFFSCREEN", chase_state);
            return;
        }
#endif
        chaser_node.SetActive(true);

        //to screen
        zpos_tween.SetEndValue(gc.pch_so.chaser_zpos);
        zpos_tween.ClearEvents();
        zpos_tween.Restart(onscreen_time);

        //safe timer
        safe_timer.Reset();

        //chaser animation
        chaser_anim.enabled = true;
        anim_main_layer.PlaySequence(run_sequence_index);

        trigger_enter_func = OnTriggerEnter_ChaseOnScreen;
        trigger_exit_func = OnTriggerExit_ChaseOnScreen;
        chase_func = PLAY_ChaseOnScreen;
        last_chase_state = chase_state;
        chase_state = ChaseState.CHASE_ONSCREEN;
        if (onChaseStateChanged != null) { onChaseStateChanged(chase_state); }

        OnPlayerStrafeReactionTime();    
    }
    /*[Direct call by PlayerController]*/
    public void Pl_CrashLose()
    {
        last_chase_state = chase_state;
        chase_state = ChaseState.DISABLED;
        if (onChaseStateChanged != null) { onChaseStateChanged(chase_state); }
        if (onCrashLose != null) { onCrashLose(); }
    }
    /*[Direct call by PlayerController]*/
    public void Pl_Lose()
    {
        last_chase_state = chase_state;
        chase_state = ChaseState.DISABLED;
        if (onChaseStateChanged != null) { onChaseStateChanged(chase_state); }
        if (onLose != null) { onLose(); }
    }
    /*[Direct call by PlayerController]*/
    public void Pl_Drop()
    {
        if (drop_hits == 0) {
            //wait for drop
            drop_delay_timer.SetOnCompleteOnce(OnDropDamageBeginTime);
            drop_delay_timer.Reset();
        }
        ++drop_hits;
    }
    /*[Direct call by PlayerController]*/
    public void Pl_RunSpeedUpdated(float curve_sample)
    {
        float anim_speedmult = 1f + curve_sample;
        anim_main_layer.SetSequenceSpeedMult(run_sequence_index, anim_speedmult);
        anim_main_layer.SetSequenceSpeedMult(strafeleft_sequence_index, anim_speedmult);
        anim_main_layer.SetSequenceSpeedMult(straferight_sequence_index, anim_speedmult);
        anim_add_layer.SetSequenceSpeedMult(air_strafeleft_sequence_index, anim_speedmult);
        anim_add_layer.SetSequenceSpeedMult(air_straferight_sequence_index, anim_speedmult);
        strafe_tween.SetDuration(playchar.StrafeTime() * strafe_time_mult);
    }
    /*[Direct call by PlayerController]*/
    public void PL_Pause()
    {
        trigger_enter_func = GameController.Stub;
        trigger_exit_func = GameController.Stub;
        chase_func = GameController.Stub;

        //animations
        chaser_anim.enabled = false;
    }
    /*[Direct call by PlayerController]*/
    public void Pl_UnPause()
    {
        switch (chase_state) {
        case ChaseState.CHASE_ONSCREEN:
        case ChaseState.CHASE_ATTACK:
        case ChaseState.CHASE_SLIDE:
            trigger_enter_func = OnTriggerEnter_ChaseOnScreen;
            trigger_exit_func = OnTriggerExit_ChaseOnScreen;
            chase_func = PLAY_ChaseOnScreen;
            break;
        case ChaseState.CHASE_OFFSCREEN:
            trigger_enter_func = GameController.Stub;
            trigger_exit_func = GameController.Stub;
            chase_func = GameController.Stub;
            break;
        case ChaseState.CRASH:
        case ChaseState.STAND_ONROAD:
            trigger_enter_func = GameController.Stub;
            trigger_exit_func = GameController.Stub;
            chase_func = PLAY_OnRoad;
            break;
        }

        //animations
        chaser_anim.enabled = true;
    }
    /*[Event by PlayerController]*/
    void OnPlayerSlideStateChanged(bool is_sliding)
    {
        if (!IsOnScreen() || jump_reaction_timer.IsEnabled()) return;

        jump_reaction_timer.Reset();
    }
    void OnPlayerPosChanged(PosState player_pos_state)
    {
        if (!IsOnScreen() || jump_reaction_timer.IsEnabled()) return;

        if (player_pos_state == PosState.JUMP_RISING) {
            //player jumped
            jump_reaction_timer.Reset();
        }
    }
    /*[Event by PlayerController]*/
    void OnPlayerStrafeStateChanged(StrafeTo strafe_to)
    {
        if (!IsOnScreen()) return;

        int player_lane = playchar.CurrentLane();
        if (strafe_reaction_timer.IsEnabled()) {
            if (player_lane == current_lane_index)
                strafe_reaction_timer.SetEnabled(false);
        } else {
            if (player_lane != current_lane_index) {
                strafe_reaction_timer.Reset();

                //fallback
                Pl_Fallback();
            }
        }
    }
    #endregion [PlayerController]

    #region [Unity Slots]
    void Update()
    {
        chase_func();
    }
    void OnTriggerEnter(Collider other)
    {
        trigger_enter_func(other);
    }
    void OnTriggerExit(Collider other)
    {
        trigger_exit_func(other);
    }
    #endregion [Unity Slots]

    /*[Callback by GameTimer]*/
    void OnPlayerJumpReactionTime()
    {
#if CODEDEBUG
        if (!IsOnScreen()) {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogError(METHOD_NAME, "ChaseState is {0}, expected {1}", chase_state, ChaseState.CHASE_ONSCREEN);
        }
#endif
        if (chase_state == ChaseState.CHASE_SLIDE) return;

        //do jump
        if (IsGrounded()) {
            jump_fall_tween.SetEndValue((playchar.jump_height * jump_height_mult) + this_rigidbody.position.y);
            if (use_jump_curves) jump_fall_tween.SetEase(jump_curve);
            else jump_fall_tween.SetEase(Easing.CubicOut);
            jump_fall_tween.SetOnComplete(BeginToFall);
            jump_fall_tween.Restart(playchar.JumpRiseTime() * jump_height_mult);

            //shadow
            shadow_go.SetActive(false);

            pos_state = PosState.JUMP_RISING;
            if (onPosStateChanged != null) { onPosStateChanged(pos_state); }

            //chaser animation
            anim_main_layer.PlaySequence(jumpfall_sequence_index);
        }
    }
    /*[Callback by GameTimer]*/
    void OnPlayerStrafeReactionTime()
    {
        if (!IsOnScreen()) {
#if CODEDEBUG
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogError(METHOD_NAME, "ChaseState is {0}, expected {1}", chase_state, ChaseState.CHASE_ONSCREEN);
#endif
            return;
        }
        if (chase_state == ChaseState.CHASE_SLIDE) return;

        int player_lane = playchar.CurrentLane();
        if (player_lane != current_lane_index) {
            if (player_lane < current_lane_index) { StrafeToLeft(); }
            else { StrafeToRight(); }
        } else {
            //not strafing
            CheckForAttack();
        }
    }
    void StrafeToLeft()
    {
        if (--current_lane_index < 0) {
#if CODEDEBUG
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogError(METHOD_NAME, "current_lane_index is {0}", current_lane_index);
#endif
            current_lane_index = 0;
            return;
        }
        strafe_tween.Restart();
        is_strafing_to = StrafeTo.LEFT;

        //chaser animation
        if (IsGrounded()) {
            anim_main_layer.PlaySequence(strafeleft_sequence_index);
        } else {
            anim_add_layer.PlaySequence(air_strafeleft_sequence_index);
        }
    }
    void StrafeToRight()
    {
        if (++current_lane_index >= GameController.NUM_RUN_LANES) {
#if CODEDEBUG
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogError(METHOD_NAME, "current_lane_index is {0}", current_lane_index);
#endif
            current_lane_index = GameController.NUM_RUN_LANES - 1;
            return;
        }
        strafe_tween.Restart();
        is_strafing_to = StrafeTo.RIGHT;

        //chaser animation
        if (IsGrounded()) {
            anim_main_layer.PlaySequence(straferight_sequence_index);
        } else {
            anim_add_layer.PlaySequence(air_straferight_sequence_index);
        }
    }
    /*[Callback by GameTween]*/
    void OnStrafeComplete()
    {
        if (!IsOnScreen()) {
#if CODEDEBUG
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogError(METHOD_NAME, "ChaseState is {0}, expected {1}", chase_state, ChaseState.CHASE_ONSCREEN);
#endif
            return;
        }
        is_strafing_to = StrafeTo.NONE;
        CheckForAttack();
    }
    void CheckForAttack()
    {
        if (playchar.ChaserReadyToAttack()) {
            Pl_Attack();
        } else {
            Pl_Fallback();
        }
    }
    /*[Callback by GameTween]*/
    void BeginToFall()
    {
        //recover from slide
        if (chase_state == ChaseState.CHASE_SLIDE) {
            _dropRecover();
        }

        current_floor_collider = null;
        current_slope_collider = null;
        obst_floor_height = 0;

        //from current height tween value to 0
        jump_fall_tween.SetEndValue(0f);
        if (use_fall_curve) jump_fall_tween.SetEase(fall_curve);
        else jump_fall_tween.SetEase(Easing.CubicIn);
        jump_fall_tween.SetOnComplete(OnFallToFloorCompleted);
        jump_fall_tween.Restart(FallTimeFromCurrentHeight());

        if (pos_state != PosState.JUMP_RISING) {
            //shadow
            shadow_go.SetActive(false);
            //play fall animation
            anim_main_layer.PlaySequence(fall_sequence_index);
        }

        pos_state = PosState.JUMP_FALLING;
        if (onPosStateChanged != null) { onPosStateChanged(pos_state); }
    }
    /*[Callback by GameTween]*/
    void OnFallToFloorCompleted()
    {
        _fallCompleted();
        pos_state = PosState.GROUNDED_FLOOR;
        if (onPosStateChanged != null) { onPosStateChanged(pos_state); }
    }
    void _fallCompleted()
    {
        jump_fall_tween.SetEnabled(false);

        //shadow
        shadow_go.SetActive(true);

        //chaser animations
        anim_main_layer.PlaySequence(land_sequence_index);

        if (onLand != null) { onLand(); }
    }
    void OnPlaceOnRoad()
    {
#if CODEDEBUG
        if (chase_state != ChaseState.DISABLED) {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogError(METHOD_NAME, "ChaseState is {0}, expected {1}", chase_state, ChaseState.DISABLED);
        }
#endif
        gc.THEME_PlaceCustomObstacle(1, 10, 10, this_transform);

        chaser_node.SetActive(true);
        //chaser animations
        chaser_anim.enabled = true;
        anim_main_layer.PlaySequence(stand_sequence_index);

        trigger_enter_func = GameController.Stub;
        trigger_exit_func = GameController.Stub;
        chase_func = PLAY_OnRoad;
        last_chase_state = chase_state;
        chase_state = ChaseState.STAND_ONROAD;
        if (onChaseStateChanged != null) { onChaseStateChanged(chase_state); }

        gc.onPatternBoxPlaced -= OnPlaceOnRoad;
    }
    void OnCrash()
    {
        _stopAllTweensTimers();

        //chaser animations
        anim_main_layer.PlaySequence(stumble_sequence_index);

        //reattach
        transform.parent = gc.AttachNodeRoot();

        trigger_enter_func = GameController.Stub;
        trigger_exit_func = GameController.Stub;
        chase_func = PLAY_OnRoad;
        last_chase_state = chase_state;
        chase_state = ChaseState.CRASH;
        if (onChaseStateChanged != null) { onChaseStateChanged(chase_state); }
    }
    /*[Callback by GameTween]*/
    void OnOffScreenComplete()
    {
        last_chase_state = chase_state;
        chase_state = ChaseState.CHASE_OFFSCREEN;
        if (onChaseStateChanged != null) { onChaseStateChanged(chase_state); }

        //order matters. playchar reads damage
        _goOffscreen();
    }
    void _goOffscreen()
    {
        chaser_anim.enabled = false;
        _stopAllTweensTimers();
        chaser_node.SetActive(false);
        this_transform.SetParent(gc.PlaycharAttachNode(), false);
        this_transform.localPosition = chaser_offscreen_pos;
        is_strafing_to = StrafeTo.NONE;
        current_lane_index = 1;
        strafe_tween.SetCurrentValue(0f);

        //if was in the middle of jump
        obst_floor_height = 0f;
        pos_state = PosState.GROUNDED_FLOOR;
        jump_fall_tween.SetCurrentValue(0f);

        drop_total_damage = 0f;
        drop_hits = 0;

        trigger_enter_func = GameController.Stub;
        trigger_exit_func = GameController.Stub;
        chase_func = GameController.Stub;
    }
    void OnDropDamageBeginTime()
    {
        if (!IsGrounded()) return;
        //drop hit
        drop_total_damage += gc.SelectedPlaycharDropPower() * drop_hits;
        drop_hits = 0;
        
        if (IsAttacking()) {
            //fallback
            zpos_tween.SetEndValue(gc.pch_so.chaser_zpos);
            zpos_tween.ClearEvents();
            zpos_tween.Restart(fallback_time);
        }

        if (drop_total_damage > 1f) {
            //go offscreen
            zpos_tween.SetEndValue(gc.pch_so.chaser_offscreen_zpos);
            zpos_tween.SetOnCompleteOnce(OnOffScreenComplete);
            zpos_tween.Restart(offscreen_time / drop_total_damage);
        } else {
            //recover time
            drop_delay_timer.SetOnCompleteOnce(OnDropRecoverTime);
            drop_delay_timer.Reset(drop_total_damage * drop_time);
        }

        if (chase_state != ChaseState.CHASE_SLIDE) {
            //chaser animation
            anim_main_layer.PlaySequence(slide_sequence_index);
            last_chase_state = chase_state;
            chase_state = ChaseState.CHASE_SLIDE;
            if (onChaseStateChanged != null) { onChaseStateChanged(chase_state); }
        }

        if (onDropHit != null) { onDropHit(); }
    }
    void OnDropRecoverTime()
    {
        _dropRecover();

        //chase animation
        anim_main_layer.PlaySequence(run_sequence_index);

        //check player lane position
        OnPlayerStrafeReactionTime();
    }
    void _dropRecover()
    {
        drop_delay_timer.ClearEvents();
        drop_delay_timer.SetEnabled(false);

        drop_total_damage = 0f;
        drop_hits = 0;

        //to screen
        zpos_tween.SetEndValue(gc.pch_so.chaser_zpos);
        zpos_tween.ClearEvents();
        zpos_tween.Restart(onscreen_time);

        //restore state
        last_chase_state = chase_state;
        chase_state = ChaseState.CHASE_ONSCREEN;
        if (onChaseStateChanged != null) { onChaseStateChanged(chase_state); }
    }
    #region [Delegate]
    void PLAY_OnRoad()
    {
        if (this_transform.position.z < gc.pch_so.chaser_offscreen_zpos) {
            OnOffScreenComplete();
        }
    }
    void PLAY_ChaseOnScreen()
    {
        chaser_pos.x = strafe_tween.UpdateAndGet();
        chaser_pos.z = zpos_tween.UpdateAndGet();

        switch (pos_state) {
        case PosState.JUMP_FALLING:
        case PosState.JUMP_RISING:
            chaser_pos.y = jump_fall_tween.UpdateAndGet();
            break;
        case PosState.GROUNDED_SLOPE:
            chaser_pos.y += playchar.CurrentSpeed() * obstacle_slope_factor * gc.PlayingTime();
            break;
        case PosState.GROUNDED_FLOOR:
            chaser_pos.y = obst_floor_height;
            break;
        }
        this_transform.localPosition = chaser_pos;

        strafe_reaction_timer.Update();
        jump_reaction_timer.Update();
        drop_delay_timer.Update();
        safe_timer.Update();
    }
    void OnTriggerEnter_ChaseOnScreen(Collider coll)
    {
#if CODEDEBUG
        if (!IsOnScreen()) {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogError(METHOD_NAME, "ChaseState is {0}, expected {1}", chase_state, ChaseState.CHASE_ONSCREEN);
        }
#endif
        Transform coll_tr = coll.transform;
        ObstacleController obst = coll_tr.parent.parent.GetComponent<ObstacleController>();

        if (coll.CompareTag(GameController.TAG_FLOOR)) {
            current_floor_collider = coll;
            var box = coll as BoxCollider;
            obst_floor_height = coll_tr.position.y + (box.size.y * 0.5f) - 0.15f;
            if (!IsGrounded()) _fallCompleted();

            pos_state = PosState.GROUNDED_FLOOR;
            if (onPosStateChanged != null) { onPosStateChanged(pos_state); }
        } else if (coll.CompareTag(GameController.TAG_SLOPE)) {
            current_slope_collider = coll;
            obst_floor_height = obst.TotalHeight();
            obstacle_slope_factor = obst.SlopeFactor();
            if (!IsGrounded()) _fallCompleted();

            pos_state = PosState.GROUNDED_SLOPE;
            if (onPosStateChanged != null) { onPosStateChanged(pos_state); }
        } else if(coll.CompareTag(GameController.TAG_OBSTACLE_FALL)) {
            if (IsGrounded()) {
                BeginToFall();
            }
        } else if (pos_state != PosState.GROUNDED_SLOPE) {
            if (coll.CompareTag(GameController.TAG_OBSTACLE_SIDE)) {
                if (is_strafing_to != StrafeTo.NONE) {
                    if (is_strafing_to == StrafeTo.LEFT) StrafeToRight();
                    else StrafeToLeft();
                    strafe_reaction_timer.Reset();
                    if (onSideBump != null) { onSideBump(obst); }
                }
            } else if (coll.CompareTag(GameController.TAG_OBSTACLE_CRASH)) {
                if (!safe_timer.IsEnabled()) {
                    obst.OnCrash();
                    crash_obst = obst;

                    if (onCrash != null) { onCrash(obst); }
                    OnCrash();
                }
            }
        }
    }
    void OnTriggerExit_ChaseOnScreen(Collider other)
    {
#if CODEDEBUG
        if (!IsOnScreen()) {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogError(METHOD_NAME, "ChaseState is {0}, expected {1}", chase_state, ChaseState.CHASE_ONSCREEN);
        }
#endif
        if (pos_state == PosState.GROUNDED_FLOOR && other == current_floor_collider) {
            BeginToFall();
        } else if (pos_state == PosState.GROUNDED_SLOPE && other == current_slope_collider) {
            BeginToFall();
        }
    }
    #endregion

    void _stopAllTweensTimers()
    {
        jump_fall_tween.SetEnabled(false);
        strafe_tween.SetEnabled(false);
        zpos_tween.SetEnabled(false);
        strafe_reaction_timer.SetEnabled(false);
        jump_reaction_timer.SetEnabled(false);
        drop_delay_timer.SetEnabled(false);
        safe_timer.SetEnabled(false);
    }
    float ReactionDurationGetter()
    {
        return playchar.ChaserReactionTime() * reaction_time_mult;
    }
}