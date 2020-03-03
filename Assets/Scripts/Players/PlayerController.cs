using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using FullInspector;

public enum PosState
{
    GROUNDED_FLOOR,
    GROUNDED_SLOPE,
    JUMP_RISING,
    JUMP_FALLING,
    JUMP_PUSHTG
}
public enum StrafeTo
{
    NONE,
    LEFT,
    RIGHT
}
public enum PlayerSpeedState
{
    NOT_DEFINED,
    TIRED,
    NORMAL,
    TURBO
}
public interface IPlaycharChallenge
{
    event GameController.Event<PosState> onPosStateChanged;
    event GameController.Event<StrafeTo> onStrafeStateChanged;
    event GameController.Event<bool> onSlideStateChanged;
    event GameController.Event<PlayerSpeedState> onSpeedStateChanged;
    event GameController.Event onRest;
    event GameController.Event onContinue;
    event GameController.Event<ObstacleController> onFloor;
    event GameController.Event<ObstacleController> onSlope;
    event GameController.Event<ObstacleController> onSideBump;
    event GameController.Event<ObstacleController> onStumble;
    event GameController.Event<ObstacleController> onWithinBegin;
    event GameController.Event<ObstacleController> onWithinEnd;
    event GameController.Event<ObstacleController> onCrash;
    event GameController.Event onChaserCatch;

    PosState CurrentPosState();
    bool IsGrounded();
    bool IsOnSlope();
    PlayerSpeedState CurrentSpeedState();
    bool IsSliding();
    int CurrentLane();
    int CurrentLaneExact(float threshold);
    int LastLane();
    int CurrentStamina();
    bool IsStrafing();
    StrafeTo IsStrafingTo();
    bool IsMagnetActive();
}
public interface IPlaycharInput
{
    void INPUT_SwipeUp();
    void INPUT_SwipeDown();
    void INPUT_SwipeLeft();
    void INPUT_SwipeRight();
    void INPUT_DoubleTap();
}
public class PlayerController : BaseBehavior<FullSerializerSerializer>, IPlaycharInput, IPlaycharChallenge
{
    const int LEFT_LANE_INDEX = 0;
    const int RIGHT_LANE_INDEX = GameController.NUM_RUN_LANES - 1;    

    GameController.Event play_func = GameController.Stub;
    GameController.Event fixed_play_func = GameController.Stub;
    GameController.Event<Collider> trigger_enter_func = GameController.Stub;
    GameController.Event<Collider> trigger_exit_func = GameController.Stub;
    
    Vector3 player_pos = Vector3.zero;

    Collider current_floor_collider = null;
    Collider current_slope_collider = null;
    ObstacleController current_within_obst = null;
    public ObstacleController WithinObst() { return current_within_obst; }
    float fall_begin_height = 0f;

    PlayerSpeedState speed_state = PlayerSpeedState.NOT_DEFINED;
    public PlayerSpeedState CurrentSpeedState() { return speed_state; }

    const int RUN_SPEED_LEVELS = 20;
    const float RUN_SPEED_LEVEL_INC = 1.0f / RUN_SPEED_LEVELS;
    const float RUN_SPEED_LEVEL_DEC = RUN_SPEED_LEVEL_INC / 2.0f;
    /*[SetInEditor]*/
    public float run_speed_begin = 8f;
    /*[SetInEditor]*/
    public float run_speed_end = 28f;
    /*[SetInEditor]*/
    public AnimationCurve speed_curve = null;
    /*[SetInEditor]*/
    public float run_speed_level_inc_time = 5f;
    GameTimer run_speed_timer = null;
    float run_speed = 8f;
    float run_speed_level_norm = 0f;
    float current_total_speed = 0f;
    public float CurrentSpeed() { return current_total_speed; }
    public float RunSpeed() { return run_speed; }
    public float RunSpeedNorm() { return run_speed_level_norm; }

    /*[SetInEditor]*/
    [InspectorRange(1.1f, 2.5f)]
    public float turbo_speed_mult = 2.0f;
    /*[SetInEditor]*/
    [InspectorRange(0.2f, 0.9f)]
    public float stumble_speed_mult = 0.5f;
    /*[SetInEditor]*/
    [InspectorRange(0.2f, 0.9f)]
    public float tired_speed_mult = 0.8f;
    /*[SetInEditor]*/
    [InspectorRange(0.2f, 5.0f)]
    public float turbo_stay_time = 1.0f;
    GameTween<float> speedmult_tween = null;
    /*[SetInEditor]*/
    public float speedmult_tween_time = 0.5f;

    /*[SetInEditor]*/
    [InspectorRange(2.0f, 10.0f)]
    public float jump_height = 4.0f;
    /*[SetInEditor]*/
    [InspectorRange(1.0f, 6.0f)]
    public float second_jump_height = 3.0f;
    bool jump_reserved = false;

    public float JumpLength() { return (jump_rise_time + fall_time) * run_speed; }
    public int JumpCells() { return (int)(JumpLength() / GameController.CELL_DEPTH); }

    public const int STAMINA_MAX_VALUE = 5;
    /*[SetInEditor]*/
    int current_stamina = STAMINA_MAX_VALUE;
    GameTimer stamina_timer = null;
    public int CurrentStamina() { return current_stamina; }
    public bool IsTired() { return current_stamina <= 0; }

    bool player_can_rest = false;
    public bool PlayerCanRest() { return player_can_rest; }    

#if UNITY_EDITOR
    [InspectorHeader("Animation Control"), InspectorMargin(20), InspectorDivider, ShowInInspector, InspectorHidePrimary]
    internal bool __inspector_00;
#endif
    /*[SetInEditor]*/
    [InspectorRange(0.05f, 0.6f)]
    public float jump_rise_time_begin = 0.25f;
    [InspectorRange(0.01f, 0.3f)]
    public float jump_rise_time_end = 0.15f;
    float jump_rise_time = 0f;
    public float JumpRiseTime() { return jump_rise_time; }
    /*[SetInEditor]*/
    [InspectorRange(0.1f, 1.0f)]
    public float fall_time_begin = 0.3f;
    [InspectorRange(0.1f, 1.0f)]
    public float fall_time_end = 0.15f;
    public float FallTime() { return fall_time; }
    float fall_time = 0f;
    /*[SetInEditor]*/
    [InspectorRange(2f, 5.0f)]
    public float long_fall_time = 3f;
    /*[SetInEditor]*/
    [InspectorRange(0.1f, 1.0f)]
    public float push_to_ground_time_mult_begin = 0.4f;
    [InspectorRange(0.1f, 1.0f)]
    public float push_to_ground_time_mult_end = 0.3f;
    float push_to_ground_time_mult = 0f;
    /*[SetInEditor]*/
    [InspectorRange(0.5f, 1.5f)]
    public float slide_time = 1.0f;

    /*[SetInEditor]*/
    public float strafe_time_begin = 0.25f;
    public float strafe_time_end = 0.1f;
    float strafe_time = 0.25f;
    public float StrafeTime() { return strafe_time; }

#if UNITY_EDITOR
    [InspectorHeader("Curves"), InspectorMargin(20), InspectorDivider, ShowInInspector, InspectorHidePrimary]
    bool __inspector_curves;
#endif
    /*[SetInEditor]*/
    public bool use_jump_curves = false;
    /*[SetInEditor]*/
    public AnimationCurve jump_curve = null;
    /*[SetInEditor]*/
    public AnimationCurve second_jump_curve = null;
    /*[SetInEditor]*/
    public bool use_fall_curve = false;
    /*[SetInEditor]*/
    public AnimationCurve fall_curve = null;
    /*[SetInEditor]*/
    public bool use_long_fall_curve = false;
    /*[SetInEditor]*/
    public AnimationCurve long_fall_curve = null;
    /*[SetInEditor]*/
    public bool use_push_to_ground_curve = false;
    /*[SetInEditor]*/
    public AnimationCurve push_to_ground_curve = null;
    /*[SetInEditor]*/
    public bool use_strafe_curve = false;
    /*[SetInEditor]*/
    public AnimationCurve strafe_curve = null;

    GameTween<float> player_jump_fall_tween = null;
    float FallTimeFromCurrentHeight() { return fall_time * (this_rigidbody.position.y / jump_height); }
    float obst_floor_height = 0f;
    float obst_slope_factor = 1f;
    public float FloorHeight() { return obst_floor_height; }
    
    PosState pos_state = PosState.GROUNDED_FLOOR;
    public PosState CurrentPosState() { return pos_state; }
    public bool IsGrounded() { return pos_state == PosState.GROUNDED_FLOOR || pos_state == PosState.GROUNDED_SLOPE; }
    public bool IsOnSlope() { return pos_state == PosState.GROUNDED_SLOPE; }
    
    GameTween<float> strafe_tween = null;
    StrafeTo is_strafing_to = StrafeTo.NONE;
    public StrafeTo IsStrafingTo() { return is_strafing_to; }
    public bool IsStrafing() { return is_strafing_to != StrafeTo.NONE; }
    int current_lane_index = 1; // center lane index
    public int CurrentLane() { return current_lane_index; }
    public int CurrentLaneExact(float threshold = 0.5f) { return (IsStrafing() && strafe_tween.TimePassedNorm() < threshold) ? (is_strafing_to == StrafeTo.LEFT) ? (current_lane_index + 1) : (current_lane_index - 1) : current_lane_index; }
    public int LastLane() { return is_strafing_to != StrafeTo.NONE ? is_strafing_to == StrafeTo.LEFT ? current_lane_index + 1 : current_lane_index - 1 : current_lane_index; }

    
    /*[SetInEditor]*/
    public float capsule_slide_pos_y = 0f;
    /*[SetInEditor]*/
    public float capsule_slide_height = 1f;
    bool is_sliding = false;
    public bool IsSliding() { return is_sliding; }
    CapsuleCollider player_capsule = null;
    float capsule_initial_height = 0f;
    Vector3 capsule_initial_pos = Vector3.zero;
    Vector3 capsule_slide_pos = Vector3.zero;
    GameTimer slide_timer = null;

    GameObject shadow_go = null;

    Transform this_transform = null;
    Rigidbody this_rigidbody = null;

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
    public int pushtg_sequence_index = 0;
    /*[SetInEditor]*/
    public int strafeleft_sequence_index = 0;
    /*[SetInEditor]*/
    public int straferight_sequence_index = 0;
    /*[SetInEditor]*/
    public int slide_sequence_index = 0;
    /*[SetInEditor]*/
    public int stumble_sequence_index = 0;
    /*[SetInEditor]*/
    public int drop_sequence_index = 0;
    /*[SetInEditor]*/
    public int chaser_onscreen_sequence_index = 0;
    /*[SetInEditor]*/
    public int air_strafeleft_sequence_index = 0;
    /*[SetInEditor]*/
    public int air_straferight_sequence_index = 0;

    AdvancedAnimationController player_anim = null;
    AdvancedAnimationController.Layer anim_main_layer = null;
    AdvancedAnimationController.Layer anim_add_layer = null;
    SelectorRefGroup<AdvancedAnimationController.ClipRefItem> anim_run_group = null;

    //Chaser
    ChaserController chaser_ctr = null;

    int num_chaser_attacks_allowed = 0;
    public const int NUM_CHASE_CELLS = 1;
    int current_chase_cell = 0;
    public int CurrentChaseCell() { return current_chase_cell; }    
    GameTimer chaser_delay_timer = null;
    
    float chaser_reaction_time = 1f;
    public float ChaserReactionTime() { return chaser_reaction_time; }

    //Magnet
    SphereCollider player_magnet = null;
    float magnet_off_radius = 0.1f;
    float magnet_on_radius = 0.1f;
    bool magnet_active = false;
    public bool IsMagnetActive() { return magnet_active; }

    //Particles
    Transform par_root_tr = null;
    public Transform ParticlesNode() { return par_root_tr; }
    Transform par_coins_tr = null;
    public Transform CoinParticleNode() { return par_coins_tr; }

#if UNITY_EDITOR
    [InspectorHeader("Hand Slot"), InspectorMargin(20), InspectorDivider, ShowInInspector, InspectorHidePrimary]
    bool __inspector_hand;
#endif
    public string hand_name = string.Empty;
    Transform hand_slot_tr = null;
    public Transform HandSlot() { return hand_slot_tr; }

    GameController gc = null;

    public event GameController.Event onRestStateChanged = null;
    public event GameController.Event<bool> onPlayerCrash = null;
    public event GameController.Event<float> onRunSpeedChanged;
    public event GameController.Event onChaseCellChanged;
    public event GameController.Event onStaminaChanged;
    public event GameController.Event<float> onLand;
    public event GameController.Event onLucky;
    //challenge events
    public event GameController.Event<PosState> onPosStateChanged;
    public event GameController.Event<PlayerSpeedState> onSpeedStateChanged;
    public event GameController.Event<StrafeTo> onStrafeStateChanged;
    public event GameController.Event<bool> onSlideStateChanged;
    public event GameController.Event onRest;
    public event GameController.Event onContinue;
    public event GameController.Event<ObstacleController> onFloor;
    public event GameController.Event<ObstacleController> onSlope;
    public event GameController.Event<ObstacleController> onSideBump;
    public event GameController.Event<ObstacleController> onStumble;
    public event GameController.Event<ObstacleController> onWithinBegin;
    public event GameController.Event<ObstacleController> onWithinEnd;
    public event GameController.Event<ObstacleController> onCrash;
    public event GameController.Event onChaserCatch;
    
    
    #region GameController
    /*[Direct call by GameController]*/
    public void GC_PlayerPlaced()
    {
#if CODEDEBUG
        string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
#endif
        gc = GameController.Instance;
        
        this_transform = transform;
        this_rigidbody = GetComponent<Rigidbody>();
        this_rigidbody.isKinematic = true;

        if (player_jump_fall_tween == null) {
            player_jump_fall_tween = new FloatTween();
            player_jump_fall_tween.SetTimeStepGetter(() => Time.fixedDeltaTime * gc.PlayingTimeScale());
            player_jump_fall_tween.SetBeginGetter(() => this_rigidbody.position.y, false);
        }

        if (run_speed_timer == null) {
            run_speed_timer = new GameTimer();
            run_speed_timer.SetTimeStepGetter(gc.PlayingTime);
            run_speed_timer.SetOnComplete(() => RunSpeedChange(true));
            run_speed_timer.SetDuration(run_speed_level_inc_time);
        }

        if (speedmult_tween == null) {
            speedmult_tween = new FloatTween();
            speedmult_tween.SetTimeStepGetter(() => Time.fixedDeltaTime);
            speedmult_tween.SetDuration(speedmult_tween_time);
            //speed_tween.SetEase(Easing.CubicInOut);
        }

        if (strafe_tween == null) {
            strafe_tween = new FloatTween();
            strafe_tween.SetTimeStepGetter(() => Time.fixedDeltaTime * gc.PlayingTimeScale());
            strafe_tween.SetBeginGetter(() => this_rigidbody.position.x, false);
            strafe_tween.SetEndGetter(() => gc.THEME_LaneOffsetX(current_lane_index), false);
            strafe_tween.SetOnComplete(OnStrafeComplete);
            if (use_strafe_curve) strafe_tween.SetEase(strafe_curve);
            else strafe_tween.SetEase(Easing.CubicOut);
            //duration is set in UpdateRunSpeed
        }
        

        Transform player_node = this_transform.Find("player_node");
        //player animations
        if (player_anim == null) {
            player_anim = player_node.GetComponent<AdvancedAnimationController>();
            player_anim.Init();
            anim_main_layer = player_anim.layers[0];
            anim_run_group = anim_main_layer.state_groups[0];
            anim_add_layer = player_anim.additives[0];
        }

        //hand
        GameController.DestroyComponents(GameController.FindComponentsRecursive<MeshFilter>(player_node.GetChild(1)));
        GameController.DestroyComponents(GameController.FindComponentsRecursive<MeshRenderer>(player_node.GetChild(1)));
        hand_slot_tr = GameController.FindNodeWithName(player_node.GetChild(1), hand_name);
#if CODEDEBUG
        if (hand_slot_tr == null) {
            GameController.LogError(METHOD_NAME, "hand_slot is NULL");
        }
#endif


        if (slide_timer == null) {
            slide_timer = new GameTimer();
            slide_timer.SetTimeStepGetter(gc.PlayingTime);
            slide_timer.SetOnComplete(OnSlideTime);
            slide_timer.SetDuration(slide_time);
        }

        if (stamina_timer == null) {
            stamina_timer = new GameTimer();
            stamina_timer.SetTimeStepGetter(gc.PlayingTime);
            stamina_timer.SetOnComplete(StaminaDecrease);
            stamina_timer.SetDuration(gc.SelectedPlaycharStaminaTime());
        }

        //player collide capsule
        player_capsule = GetComponent<CapsuleCollider>() as CapsuleCollider;
#if CODEDEBUG
        if (player_capsule == null) {
            GameController.LogError(METHOD_NAME, "player_capsule is {0}", 0);
        }
#endif
        capsule_initial_height = player_capsule.height;
        capsule_initial_pos = player_capsule.center;
        capsule_slide_pos = capsule_initial_pos;
        capsule_slide_pos.y = capsule_slide_pos_y;

        //player magnet
        player_magnet = transform.Find("picker/magnet").GetComponent<SphereCollider>() as SphereCollider;
#if CODEDEBUG
        if (player_magnet == null) {
            GameController.LogError(METHOD_NAME, "player_magnet is NULL");
        }
#endif
        magnet_on_radius = gc.coins_so.magnet_radius_slow;
        player_magnet.radius = magnet_off_radius;

        //particles
        par_root_tr = this_transform.Find("particles");
        //coin particle node
        par_coins_tr = par_root_tr.Find("coins");

        //shadow
        shadow_go = this_transform.Find("Shadow").gameObject;
        shadow_go.SetActive(true);

        //chaser
        chaser_ctr = gc.ChaserCtrl();
        if (chaser_delay_timer == null) {
            chaser_delay_timer = new GameTimer();
            chaser_delay_timer.SetTimeStepGetter(gc.PlayingTime);
        }
        chaser_ctr.Pl_Init();
        
        //register event listeners
        onPosStateChanged += Playchar_OnPosStateChanged;

        chaser_ctr.onChaseStateChanged += OnChaseStateChanged;
        gc.onPlayingStateChanged += OnPlayingStateChanged;

        //SETUP INITIAL STATE
        run_speed_level_norm = 0f;
        UpdateRunSpeed();
        _setupInitialState(false);

        //chaser
        chaser_ctr.Pl_PlaceOffScreen(true);
    }
    /*[Direct call by GameController]*/
    public void GC_PlayerRemoving()
    {
        //stop all callbacks
        _stopAllTweensTimers();

        //events
        onPosStateChanged -= Playchar_OnPosStateChanged;

        //destroy chaser
        chaser_ctr.onChaseStateChanged -= OnChaseStateChanged;
        chaser_ctr.Pl_PlayerRemoving();
        chaser_ctr = null;

        //stop all animations
        player_anim.enabled = false;

        //clear events
        onSpeedStateChanged = null;
        onPosStateChanged = null;
        onRunSpeedChanged = null;
        onStrafeStateChanged = null;
        onPlayerCrash = null;
        onRestStateChanged = null;

        //GameController
        gc.onPlayingStateChanged -= OnPlayingStateChanged;
        gc = null;
    }
    public void GC_SetMagnetActive(bool active)
    {
        player_magnet.radius = active ? magnet_on_radius : magnet_off_radius;
        anim_run_group.selector = active ? 1 : 0;
        if (anim_main_layer.CurrentSequenceIndex() == run_sequence_index) { 
            anim_main_layer.PlaySequence(run_sequence_index);
        }
        magnet_active = active;
    }
    void OnPlayingStateChanged()
    {
        switch (gc.CurrentPlayingState()) {
        case GameController.GamePlayingState.MAIN:
            _unpause();
            break;
        case GameController.GamePlayingState.PAUSE:
            _pause();
            break;
        case GameController.GamePlayingState.CUTSCENE:
            _pause();
            switch (gc.CurrentCutsceneState()) {
            case GameController.CutsceneState.CONTINUE_CHASER:
            case GameController.CutsceneState.CONTINUE_OBSTACLE:
                PlayerContinue();
                break;
            case GameController.CutsceneState.REST:
                PlayerRest();
                break;
            }
            break;
        }
    }
    void _setupInitialState(bool callEvents)
    {
        _stopAllTweensTimers();

        //player pos
        current_lane_index = 1; //center lane
        player_pos = this_transform.localPosition;
        strafe_tween.SetCurrentValue(gc.THEME_LaneOffsetX(current_lane_index));
        is_strafing_to = StrafeTo.NONE;
        if (callEvents && onStrafeStateChanged != null) { onStrafeStateChanged(is_strafing_to); }
        obst_floor_height = 0f;
        player_jump_fall_tween.SetCurrentValue(0f);
        pos_state = PosState.GROUNDED_FLOOR;
        if (callEvents && onPosStateChanged != null) { onPosStateChanged(pos_state); }

        //player animations
        anim_main_layer.PlaySequence(run_sequence_index);

        speed_state = PlayerSpeedState.NORMAL;
        if (callEvents && onSpeedStateChanged != null) { onSpeedStateChanged(speed_state); }
        speedmult_tween.SetCurrentValue(1f);
        /*speedmult_tween.SetValues(stumble_speed_mult, 1.0f);
        speedmult_tween.Restart();*/
        stamina_timer.Reset();

        play_func = PLAYING_Update;
        fixed_play_func = PLAYING_FixedUpdate;
        trigger_enter_func = TriggerEnter_Playing;
        trigger_exit_func = TriggerExit_Playing;
    }
    void _pause()
    {
        //chaser
        chaser_ctr.PL_Pause();

        //animations
        player_anim.enabled = false;

        play_func = GameController.Stub;
        fixed_play_func = GameController.Stub;
        trigger_enter_func = GameController.Stub;
        trigger_exit_func = GameController.Stub;
    }
    void _unpause()
    {
        //chaser
        chaser_ctr.Pl_UnPause();

        //animations
        player_anim.enabled = true;

        play_func = PLAYING_Update;
        fixed_play_func = PLAYING_FixedUpdate;
        trigger_enter_func = TriggerEnter_Playing;
        trigger_exit_func = TriggerExit_Playing;
    }

    #region Input
    /*[Direct call by GameController]*/
    public void INPUT_SwipeUp()
    {
        if (IsGrounded()) {
            //player is on ground and can jump
            //tween to jump height
            player_jump_fall_tween.SetEndValue(jump_height + this_rigidbody.position.y);
            if (use_jump_curves) player_jump_fall_tween.SetEase(jump_curve);
            else player_jump_fall_tween.SetEase(Easing.CubicOut);
            player_jump_fall_tween.SetOnComplete(BeginToFall);
            player_jump_fall_tween.Restart(jump_rise_time);

            SetSlidingEnabled(false);
            jump_reserved = false;

            pos_state = PosState.JUMP_RISING;
            if (onPosStateChanged != null) { onPosStateChanged(pos_state); }

            //player animations
            anim_main_layer.PlaySequence(jumpfall_sequence_index);
        } else {
            //second jump
            jump_reserved = true;
            //challenge event
        }
    }
    /*[Direct call by GameController]*/
    public void INPUT_SwipeDown()
    {
        if (IsGrounded()) {
            if (!is_sliding) {
                //slide
                SetSlidingEnabled(true);

                //player animation
                anim_main_layer.PlaySequence(slide_sequence_index);
            }
        } else if (pos_state != PosState.JUMP_PUSHTG) {
            //push to ground
            obst_floor_height = 0;
            player_jump_fall_tween.SetEndValue(0f);
            if (use_push_to_ground_curve) player_jump_fall_tween.SetEase(push_to_ground_curve);
            else player_jump_fall_tween.SetEase(Easing.Linear);
            player_jump_fall_tween.SetOnComplete(OnFallToFloorCompleted);
            player_jump_fall_tween.Restart(FallTimeFromCurrentHeight() * push_to_ground_time_mult);

            //switch state before sliding
            pos_state = PosState.JUMP_PUSHTG;
            if (onPosStateChanged != null) onPosStateChanged(pos_state);

            //slide
            SetSlidingEnabled(true);

            //player animations
            anim_main_layer.PlaySequence(pushtg_sequence_index);
        }
    }
    /*[Direct call by GameController]*/
    public void INPUT_SwipeLeft()
    {
        if (current_lane_index == LEFT_LANE_INDEX) return;

        --current_lane_index;
        _doStrafe(StrafeTo.LEFT);

        is_strafing_to = StrafeTo.LEFT;
        if (onStrafeStateChanged != null) onStrafeStateChanged(is_strafing_to);
    }
    /*[Direct call by GameController]*/
    public void INPUT_SwipeRight()
    {
        if (current_lane_index == RIGHT_LANE_INDEX) return;
        
        ++current_lane_index;
        _doStrafe(StrafeTo.RIGHT);

        is_strafing_to = StrafeTo.RIGHT;
        if (onStrafeStateChanged != null) onStrafeStateChanged(is_strafing_to);
    }
    void _doStrafe(StrafeTo strafe_to)
    {
        strafe_tween.Restart();

        if (IsGrounded()) {
            if (is_sliding) SetSlidingEnabled(false);
            //player animations
            anim_main_layer.PlaySequence((strafe_to == StrafeTo.LEFT) ? strafeleft_sequence_index : straferight_sequence_index);
        } else {
            //player animations
            anim_add_layer.PlaySequence((strafe_to == StrafeTo.LEFT) ? air_strafeleft_sequence_index : air_straferight_sequence_index);
        }
    }
    /*[Direct call by GameController]*/
    public void INPUT_DoubleTap()
    {
        //Turbo run state
        /*if (IsGrounded()) {
            if (current_speed_state != PlayerSpeedState.NORMAL) return;
            
            speedmult_tween.SetValues(speedmult_tween.CurrentValue(), turbo_speed_mult);
            speedmult_tween.Restart();
            speedmult_timer.Reset(turbo_stay_time);

            if (is_sliding) {
                SetSlidingEnabled(false);
                player_anim_fader.Play(PlAnimLastSpeedState());
            }

            //chaser
            if (chaser_ctr.IsChasing()) {
                ChaseCellIncrease();
            }

            current_speed_state = PlayerSpeedState.TURBO;
            if (onSpeedStateChanged != null) onSpeedStateChanged(current_speed_state);

            //player animations
            player_anim_fader.Crossfade(
                anim_player_turbo[last_anim_player_turbo_index],
                0f,
                speedmult_tween_time * 0.5f);
        } else {
            // batman fly
        }*/
    }
    public bool INPUT_DropButton()
    {
        if (!IsGrounded()) return false;

        //playchar animation
        anim_main_layer.PlaySequence(drop_sequence_index);

        //chaser
        chaser_ctr.Pl_Drop();

        //consume drops
        return true;
    }
    #endregion //Input
    #endregion //GameController

    #region UnitySlots
    /*[Callback by Unity]*/
    void Update()
    {
        play_func();
	}
    void FixedUpdate()
    {
        fixed_play_func();
    }
    /*[Callback by Unity]*/
    void OnTriggerEnter(Collider other)
    {
        trigger_enter_func(other);
    }
    /*[Callback by Unity]*/
    void OnTriggerExit(Collider other)
    {
        trigger_exit_func(other);
    }

    #endregion //UnitySlots

    #region [Delegate]
    void TriggerEnter_Playing(Collider coll)
    {
#if CODEDEBUG
        if (gc.CurrentGameState() != GameController.GameState.PLAYING) {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogError(METHOD_NAME, "GameState is {0}, expected {1}", gc.CurrentGameState(), GameController.GameState.PLAYING);
            return;
        }
#endif
        Transform coll_tr = coll.transform;
        ObstacleController obst = coll_tr.parent.parent.GetComponent<ObstacleController>();

        if (coll.CompareTag(GameController.TAG_FLOOR)) {
            obst.OnFloor();
            current_floor_collider = coll;
            var box = coll as BoxCollider;
            obst_floor_height = coll_tr.position.y + (box.size.y * 0.5f) - 0.15f;
            //obst_floor_height = obst.TotalHeight();
            if (!IsGrounded()) _fallCompleted();

            /*GameController.Log("", "onFloor. Name: {0} Level: {1}.", obst.name, obst_floor_height);
            GameController.Log("", "{0}->{1}", pos_state, PosState.GROUNDED_FLOOR);*/

            pos_state = PosState.GROUNDED_FLOOR;
            if (onPosStateChanged != null) onPosStateChanged(pos_state);

            //challenge event
            if (onFloor != null) { onFloor(obst); }
        } else if (coll.CompareTag(GameController.TAG_SLOPE)) {
            obst.OnSlope();

            current_slope_collider = coll;
            obst_floor_height = obst.TotalHeight();
            obst_slope_factor = obst.SlopeFactor();
            if (!IsGrounded()) _fallCompleted();

            /*GameController.Log("", "onSlope. Name: {0} Slope: {1}.", obst.name, obst_slope_factor);
            GameController.Log("", "{0}->{1}", pos_state, PosState.GROUNDED_SLOPE);*/

            pos_state = PosState.GROUNDED_SLOPE;
            if (onPosStateChanged != null) onPosStateChanged(pos_state);

            //challenge event
            if (onSlope != null) { onSlope(obst); }
        } else if (coll.CompareTag(GameController.TAG_OBSTACLE_WITHIN)) {
            obst.OnWithin();
            current_within_obst = obst;

            //challenge event
            if (onWithinBegin != null) { onWithinBegin(obst); }
        } else if (coll.CompareTag(GameController.TAG_OBSTACLE_SIDE)) {
            bool hit_side = false;
            //strafe
            if (is_strafing_to == StrafeTo.LEFT) {
                if(current_lane_index < RIGHT_LANE_INDEX/* && coll_tr.position.x < this_transform.position.x*/ ) {
                    ++current_lane_index;
                    _doStrafe(StrafeTo.RIGHT);
                    hit_side = true;
                }
            } else if (is_strafing_to == StrafeTo.RIGHT) {
                if (current_lane_index > LEFT_LANE_INDEX/* && coll_tr.position.x > this_transform.position.x*/) {
                    --current_lane_index;
                    _doStrafe(StrafeTo.LEFT);
                    hit_side = true;
                }
            }

            if (hit_side) {
                obst.OnSideBump();

                //stumble
                speedmult_tween.SetValues(stumble_speed_mult, 1.0f);
                speedmult_tween.Restart();
                if (chaser_ctr.IsChasing()) {
                    ChaseCellDecrease();
                    ++num_chaser_attacks_allowed;
                }

                //challenge event
                if (onSideBump != null) { onSideBump(obst); }
            }
        } else if (coll.CompareTag(GameController.TAG_OBSTACLE_FALL)) {
            if (IsGrounded()) {
                BeginToFall();
            }
        } else if (pos_state != PosState.GROUNDED_SLOPE) {
            if (coll.CompareTag(GameController.TAG_OBSTACLE_STUMBLE)) {
                obst.OnStumble();
                speedmult_tween.SetValues(stumble_speed_mult, 1.0f);
                speedmult_tween.Restart();
                speed_state = PlayerSpeedState.NORMAL;
                if (onSpeedStateChanged != null) { onSpeedStateChanged(speed_state); }

                //player animations
                anim_main_layer.PlaySequence(stumble_sequence_index);
                //chaser activity
                if (chaser_ctr.IsChasing()) {
                    ChaseCellDecrease();
                    ++num_chaser_attacks_allowed;
                }

                //challenge event
                if (onStumble != null) { onStumble(obst); }
            } else if (coll.CompareTag(GameController.TAG_OBSTACLE_CRASH)) {
                obst.OnCrash();

                //challenge event
                if (onCrash != null) { onCrash(obst); }

                //inform GameController
                onPlayerCrash(false);

                if (IsTired()) {
                    chaser_ctr.Pl_PlaceOffScreen(false);
                } else {
                    PlayerCrashContinue();
                }
            } else if (coll.CompareTag(GameController.TAG_CHASER)) {
                //challenge event
                if (onChaserCatch != null) { onChaserCatch(); }

                chaser_ctr.Pl_PlaceOffScreen(false);
                //inform GameController
                onPlayerCrash(true);
            }
        }
        }
    void TriggerExit_Playing(Collider other)
    {
#if CODEDEBUG
        if (gc.CurrentGameState() != GameController.GameState.PLAYING) {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogError(METHOD_NAME, "GameState is {0}, expected PLAYING", gc.CurrentGameState());
            return;
        }
#endif
        ObstacleController obst = other.transform.parent.parent.GetComponent<ObstacleController>();

        if (pos_state == PosState.GROUNDED_FLOOR && other == current_floor_collider) {
            BeginToFall();
        } else if (pos_state == PosState.GROUNDED_SLOPE && other == current_slope_collider) {
            BeginToFall();
        } else if (other.CompareTag(GameController.TAG_OBSTACLE_WITHIN)) {
            //challenge event
            if (onWithinEnd != null) { onWithinEnd(obst); }
            if (obst == current_within_obst) { current_within_obst = null; }
        }
    }
    void PLAYING_Update()
    {
        //update timers
        run_speed_timer.Update();
        slide_timer.Update();
        stamina_timer.Update();
        chaser_delay_timer.Update();
    }
    void PLAYING_FixedUpdate()
    {
        //current_total_speed = run_speed_tween.UpdateAndGet();
        current_total_speed = run_speed * speedmult_tween.UpdateAndGet();

        player_pos.x = strafe_tween.UpdateAndGet();
        player_pos.z = gc.pch_so.playchar_zpos;

        switch (pos_state) {
        case PosState.JUMP_RISING:
        case PosState.JUMP_FALLING:
        case PosState.JUMP_PUSHTG:
            player_pos.y = player_jump_fall_tween.UpdateAndGet();
            break;
        case PosState.GROUNDED_SLOPE:
            player_pos.y += current_total_speed * obst_slope_factor * Time.fixedDeltaTime * gc.PlayingTimeScale();
            break;
        case PosState.GROUNDED_FLOOR:
            player_pos.y = obst_floor_height;
            break;
        }
        //this_rigidbody.MovePosition(player_pos);
        this_rigidbody.position = player_pos;
    }
    #endregion //[Delegate]
    void PlayerContinue()
    {
        //call early, so challenge can get coin count and stamina
        //challenge event
        if (onContinue != null) { onContinue(); }

        if (current_stamina < 2) {
            current_stamina = 2;
            if (onStaminaChanged != null) { onStaminaChanged(); }
        }

        UpdateRunSpeed();
        _setupInitialState(true);

        //chaser
        current_chase_cell = 1;
        if (onChaseCellChanged != null) { onChaseCellChanged(); }
        chaser_ctr.Pl_PlaceOffScreen(true);
    }
    void PlayerRest()
    {
        //call early, so challenge can get coin count and stamina
        //challenge event
        if (onRest != null) { onRest(); }

        current_stamina = 5;
        if (onStaminaChanged != null) { onStaminaChanged(); }

        RunSpeedChange(true);
        _setupInitialState(true);

        //chaser
        current_chase_cell = 1;
        if (onChaseCellChanged != null) { onChaseCellChanged(); }
        chaser_ctr.Pl_PlaceOffScreen(true);
    }
    void PlayerCrashContinue()
    {
        slide_timer.SetEnabled(false);
        chaser_delay_timer.SetEnabled(false);

        //player pos
        current_lane_index = 1; //center lane
        player_pos = this_transform.localPosition;
        strafe_tween.SetEnabled(false);
        strafe_tween.SetCurrentValue(gc.THEME_LaneOffsetX(current_lane_index));
        is_strafing_to = StrafeTo.NONE;
        if (onStrafeStateChanged != null) { onStrafeStateChanged(is_strafing_to); }
        //tween to jump height
        player_jump_fall_tween.SetEndValue(jump_height + this_rigidbody.position.y);
        if (use_jump_curves) player_jump_fall_tween.SetEase(jump_curve);
        else player_jump_fall_tween.SetEase(Easing.CubicOut);
        player_jump_fall_tween.SetOnComplete(BeginToFall);
        player_jump_fall_tween.Restart(jump_rise_time);

        SetSlidingEnabled(false);
        jump_reserved = false;

        pos_state = PosState.JUMP_RISING;
        if (onPosStateChanged != null) { onPosStateChanged(pos_state); }

        //player animations
        anim_main_layer.PlaySequence(jumpfall_sequence_index);

        speed_state = PlayerSpeedState.NORMAL;
        if (onSpeedStateChanged != null) { onSpeedStateChanged(speed_state); }
        
        //chaser
        current_chase_cell = 1;
        if (onChaseCellChanged != null) { onChaseCellChanged(); }
        chaser_ctr.Pl_PlaceOffScreen(true);

        speedmult_tween.SetEnabled(false);
        speedmult_tween.SetCurrentValue(1f);
        current_stamina = 1;
        StaminaDecrease();
    }
    /*[Callback by GameTimer]*/
    /*void OnSpeedTime()
    {
        //returned from turbo. decrease stamina
        StaminaDecrease();

        if (IsTired()) {
            speedmult_tween.SetValues(speedmult_tween.CurrentValue(), tired_speed_mult);
            current_speed_state = PlayerSpeedState.TIRED;
            stamina_timer.SetEnabled(false);
        } else {
            speedmult_tween.SetValues(speedmult_tween.CurrentValue(), 1.0f);
            current_speed_state = PlayerSpeedState.NORMAL;
        }
        speedmult_tween.Restart();
        if (onSpeedStateChanged != null) onSpeedStateChanged(current_speed_state);

        if (IsGrounded()) {
            //player animations
            if (!IsSliding()) {
                player_anim.PlaySequence(run_sequence_index);
            }
        }
    }*/
    /*[Callback by GameTimer]*/
    void OnSlideTime()
    {
        SetSlidingEnabled(false);
    }
    /*[Callback by Tweener]*/
    void OnStrafeComplete()
    {
        is_strafing_to = StrafeTo.NONE;
        if (onStrafeStateChanged != null) onStrafeStateChanged(is_strafing_to);

        //animiations are already enqueued        
    }
    /*[Callback by Tweener]*/
    void BeginToFall()
    {
        current_floor_collider = null;
        current_slope_collider = null;
        obst_floor_height = 0;
        fall_begin_height = this_transform.position.y;

        //from current height tween value to 0
        player_jump_fall_tween.SetEndValue(0f);
        if (use_fall_curve) player_jump_fall_tween.SetEase(fall_curve);
        else player_jump_fall_tween.SetEase(Easing.CubicIn);
        player_jump_fall_tween.SetOnComplete(OnFallToFloorCompleted);
        player_jump_fall_tween.Restart(FallTimeFromCurrentHeight());

        if (IsGrounded()) {
            //play fall animations
            anim_main_layer.PlaySequence(fall_sequence_index);
        }

        pos_state = PosState.JUMP_FALLING;
        if (onPosStateChanged != null) { onPosStateChanged(pos_state); }
    }
    /*[Callback by Tweener]*/
    void OnFallToFloorCompleted()
    {
        _fallCompleted();
        pos_state = PosState.GROUNDED_FLOOR;
        if (onPosStateChanged != null) { onPosStateChanged(pos_state); }
    }
    public void StaminaIncrease(int value)
    {
        if (current_stamina <= 0) {
            speedmult_tween.SetValues(speedmult_tween.CurrentValue(), 1.0f);
            speedmult_tween.Restart();
            speed_state = PlayerSpeedState.NORMAL;
            if (onSpeedStateChanged != null) { onSpeedStateChanged(speed_state); }
        }
        current_stamina += value;

        stamina_timer.Reset();

        //chaser
        if (chaser_ctr.IsActive()) {
            chaser_delay_timer.SetEnabled(false);
            if (chaser_ctr.IsAttacking()) {
                chaser_ctr.Pl_Fallback();
            }
        }
        if (onStaminaChanged != null) { onStaminaChanged(); }
    }
    /*[Callback by StaminaTimer]*/
    void StaminaDecrease()
    {
        if (--current_stamina <= 0) {
            current_stamina = 0;
            stamina_timer.SetEnabled(false);
            if (speed_state == PlayerSpeedState.NORMAL) {
                speedmult_tween.SetValues(speedmult_tween.CurrentValue(), tired_speed_mult);
                speedmult_tween.Restart();
                speed_state = PlayerSpeedState.TIRED;
                if (onSpeedStateChanged != null) onSpeedStateChanged(speed_state);
            }

            if (chaser_ctr.IsChasing()) {
                //start decreasing chase_cells
                chaser_delay_timer.Reset();
            }
        } else {
            stamina_timer.Reset();
        }
        if (onStaminaChanged != null) { onStaminaChanged(); }
    }
    public void RunSpeedChange(bool increase)
    {
        if (increase) {
            if ((run_speed_level_norm += RUN_SPEED_LEVEL_INC) > 1.0f)
                run_speed_level_norm = 1.0f;
        } else {
            if ((run_speed_level_norm -= RUN_SPEED_LEVEL_DEC) < 0f)
                run_speed_level_norm = 0f;
        }

        //update internal state
        UpdateRunSpeed();
    }

    void _stopAllTweensTimers()
    {
        speedmult_tween.SetEnabled(false);
        player_jump_fall_tween.SetEnabled(false);
        strafe_tween.SetEnabled(false);
        slide_timer.SetEnabled(false);
        stamina_timer.SetEnabled(false);
        chaser_delay_timer.SetEnabled(false);
    }
    void _fallCompleted()
    {
        //in case if this function was called manually
        player_jump_fall_tween.ClearEvents();
        player_jump_fall_tween.SetEnabled(false);

        //possible snap to height. currently in RunFunc

        //fall height
        float fall_height = fall_begin_height - this_transform.position.y;
        
        //player animations
        if (pos_state != PosState.JUMP_PUSHTG) {
            anim_main_layer.PlaySequence((pos_state == PosState.JUMP_RISING || fall_height < gc.pch_so.playchar_roll_height_threshold) ? run_sequence_index : land_sequence_index);
        }

        if (onLand != null) { onLand(fall_height); }
    }
    void UpdateRunSpeed()
    {
        float speed_curve_sample = speed_curve.Evaluate(run_speed_level_norm);
        run_speed = run_speed_begin + speed_curve_sample * (run_speed_end - run_speed_begin);
        run_speed_timer.Reset();

        jump_rise_time = jump_rise_time_begin + speed_curve_sample * (jump_rise_time_end - jump_rise_time_begin);
        fall_time = fall_time_begin + speed_curve_sample * (fall_time_end - fall_time_begin);
        push_to_ground_time_mult = push_to_ground_time_mult_begin + speed_curve_sample * (push_to_ground_time_mult_end - push_to_ground_time_mult_begin);
        strafe_time = strafe_time_begin + speed_curve_sample * (strafe_time_end - strafe_time_begin);
        strafe_tween.SetDuration(strafe_time);
        chaser_reaction_time = (gc.pch_so.playchar_zpos - gc.pch_so.chaser_zpos) / run_speed;
        magnet_on_radius = gc.coins_so.magnet_radius_slow + speed_curve_sample * (gc.coins_so.magnet_radius_fast - gc.coins_so.magnet_radius_slow);

        //animations
        float anim_speed_mult = 1f + speed_curve_sample;
        anim_main_layer.SetSequenceSpeedMult(run_sequence_index, anim_speed_mult);
        anim_main_layer.SetSequenceSpeedMult(strafeleft_sequence_index, anim_speed_mult);
        anim_main_layer.SetSequenceSpeedMult(straferight_sequence_index, anim_speed_mult);
        anim_main_layer.SetSequenceSpeedMult(stumble_sequence_index, anim_speed_mult);
        anim_main_layer.SetSequenceSpeedMult(drop_sequence_index, anim_speed_mult);
        anim_add_layer.SetSequenceSpeedMult(air_strafeleft_sequence_index, anim_speed_mult);
        anim_add_layer.SetSequenceSpeedMult(air_straferight_sequence_index, anim_speed_mult);

        chaser_ctr.Pl_RunSpeedUpdated(speed_curve_sample);

        if (onRunSpeedChanged != null) { onRunSpeedChanged(speed_curve_sample); }
    }
    void SetSlidingEnabled(bool enabled)
    {
        if (is_sliding == enabled) return;

        if (enabled) {
            slide_timer.Reset();
            player_capsule.center = capsule_slide_pos;
            player_capsule.height = capsule_slide_height;
        } else {
            slide_timer.SetEnabled(false);
            player_capsule.center = capsule_initial_pos;
            player_capsule.height = capsule_initial_height;
        }
        is_sliding = enabled;
        if (onSlideStateChanged != null) { onSlideStateChanged(is_sliding); }
    }

    #region [Chaser]
    /*[Event by ChaserController]*/
    void OnChaseStateChanged(ChaseState state)
    {
        switch (state) {
        case ChaseState.DISABLED:
            //reset timer to place chaser
            chaser_delay_timer.ClearEvents();
            chaser_delay_timer.SetOnCompleteOnce(TimeToPlaceChaser);
            chaser_delay_timer.Reset(gc.pch_so.chaser_delay_time);

            //can rest
            player_can_rest = true;
            if (onRestStateChanged != null) { onRestStateChanged(); }
            break;
        case ChaseState.STAND_ONROAD:
            //cannot rest
            player_can_rest = false;
            if (onRestStateChanged != null) { onRestStateChanged(); }
            break;
        /*case ChaserController.ChaseState.CRASH:
            //wait for crash complete
            break;*/
        case ChaseState.CHASE_ONSCREEN:
            /*if (chaser_ctr.LastChaseState() != ChaseState.CHASE_ATTACK && IsGrounded()) {
                //animation
                anim_main_layer.PlaySequence(chaser_onscreen_sequence_index);
            }*/
            break;
        case ChaseState.CHASE_OFFSCREEN:
            //cannot rest
            player_can_rest = false;
            if (onRestStateChanged != null) { onRestStateChanged(); }

            switch (chaser_ctr.LastChaseState()) {
            case ChaseState.CHASE_ONSCREEN:
                current_chase_cell = 1;
                break;
            case ChaseState.DISABLED:
                current_chase_cell = 1;
                break;
            case ChaseState.STAND_ONROAD:
                //current_chase_cell = (CurrentSpeedState() == PlayerSpeedState.TURBO) ? 3 : 1;
                current_chase_cell = 1;
                break;
            case ChaseState.CRASH:
                if (gc.PLAYCHAR_IsLucky()) {
                    //lose
                    GameController.Instance.InvokeOnMT(_chaserCrashLose);
                    if (onLucky != null) { onLucky(); }
                    return;
                }
                current_chase_cell = 1;
                break;
            case ChaseState.CHASE_SLIDE:
                bool is_lucky = gc.PLAYCHAR_IsLucky();
                if (chaser_ctr.DropDamage() > ChaserController.DROP_DAMAGE_CRASH || is_lucky) {
                    //crash lose
                    GameController.Instance.InvokeOnMT(_chaserCrashLose);
                    if (is_lucky && onLucky != null) { onLucky(); }
                    return;
                }
                current_chase_cell = 1;
                break;
            }
            if (onChaseCellChanged != null) { onChaseCellChanged(); }
            //if chaser was not removed by crash state
            if (chaser_ctr.IsChasing()) {
                chaser_delay_timer.ClearEvents();
                chaser_delay_timer.SetOnComplete(ChaseCellDecrease);
                chaser_delay_timer.SetDuration(gc.pch_so.chaser_cell_time);
                if (IsTired()) chaser_delay_timer.Reset();
            }
            break;
        }
    }
    /*[Callback by GameTimer]*/
    void TimeToPlaceChaser()
    {
#if CODEDEBUG
        if (chaser_ctr.IsActive()) {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogError(METHOD_NAME, "ChaseState is {0}, expected {1}", chaser_ctr.CurrentChaseState(), ChaseState.DISABLED);
            return;
        }
#endif
        //place chaser
        chaser_ctr.Pl_PlaceOnRoad();
    }
    void ChaseCellIncrease()
    {
#if CODEDEBUG
        if (!chaser_ctr.IsChasing()) {
            return;
        }
#endif
        ++current_chase_cell;
        if (chaser_ctr.IsOnScreen()) {
            if (chaser_ctr.IsAttacking()) { chaser_ctr.Pl_Fallback(); }
            chaser_ctr.Pl_OnScreen2OffScreen();
        } else if (current_chase_cell > NUM_CHASE_CELLS) {
            GameController.Instance.InvokeOnMT(_chaserLose);
        }
        if (onChaseCellChanged != null) { onChaseCellChanged(); }
    }
    void _chaserLose()
    {
        chaser_ctr.Pl_Lose();
    }
    void _chaserCrashLose()
    {
        chaser_ctr.Pl_CrashLose();
    }
    /*[Callback by GameTimer]*/
    void ChaseCellDecrease()
    {
#if CODEDEBUG
        if (!chaser_ctr.IsChasing()) {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogError(METHOD_NAME, "ChaseState is {0}, expected {1}", chaser_ctr.CurrentChaseState(), "CHASE");
            return;
        }
#endif
        if (chaser_ctr.IsAttacking()) return;

        if (--current_chase_cell <= 0) {
            if (!chaser_ctr.IsOnScreen()) {
                //show chaser
                chaser_ctr.Pl_OffScreen2OnScreen();
            } else {
                //attack
                chaser_ctr.Pl_Attack();
            }
            current_chase_cell = 0;
        } else if (IsTired()) {
            //continue decreasing cells
            chaser_delay_timer.Reset();
        }

        if (onChaseCellChanged != null) { onChaseCellChanged(); }
    }
    public bool ChaserReadyToAttack()
    {
        if (num_chaser_attacks_allowed > 0) {
            --num_chaser_attacks_allowed;
            return true;
        }
        return IsTired();
    }
    #endregion //[Chaser]

    #region [Events]
    void Playchar_OnPosStateChanged(PosState state)
    {
        //shadow
        if (IsGrounded()) {
            shadow_go.SetActive(true);
            if (jump_reserved) {
                GameController.Instance.InvokeOnMT(INPUT_SwipeUp);
            }
        } else {
            shadow_go.SetActive(false);
        }
    }
    #endregion

#if CODEDEBUG
    #region [DEVGUI]

    Text ui_speed_txt = null;
    Text ui_stamina_txt = null;
    Text ui_chasecell_txt = null;
    Text ui_magradius_txt = null;
    public void SetDevGui(GameObject ui_root_go)
    {
        Transform ui_controls_tr = ui_root_go.transform.FindChild("Controls");
        //speed
        Transform ui_speed_tr = ui_controls_tr.Find("Speed/Controls");
        ui_speed_txt = ui_speed_tr.Find("Value/Text").GetComponent<Text>();
        ui_speed_tr.FindChild("IncreaseBtn").GetComponent<Button>().onClick.AddListener(() => RunSpeedChange(true));
        ui_speed_tr.FindChild("DecreaseBtn").GetComponent<Button>().onClick.AddListener(() => RunSpeedChange(false));
        //stamina
        Transform ui_stamina_tr = ui_controls_tr.Find("Stamina/Controls");
        ui_stamina_txt = ui_stamina_tr.Find("Value/Text").GetComponent<Text>();
        ui_stamina_tr.FindChild("IncreaseBtn").GetComponent<Button>().onClick.AddListener(() => StaminaIncrease(5));
        ui_stamina_tr.FindChild("DecreaseBtn").GetComponent<Button>().onClick.AddListener(() => StaminaDecrease());
        //ChaserCell
        Transform ui_chasecell_tr = ui_controls_tr.Find("ChaserCell/Controls");
        ui_chasecell_txt = ui_chasecell_tr.Find("Value/Text").GetComponent<Text>();
        ui_chasecell_tr.FindChild("IncreaseBtn").GetComponent<Button>().onClick.AddListener(() => ChaseCellIncrease());
        ui_chasecell_tr.FindChild("DecreaseBtn").GetComponent<Button>().onClick.AddListener(() => ChaseCellDecrease());
        //ChaserCell
        Transform ui_magradius_tr = ui_controls_tr.Find("MagRadius/Controls");
        ui_magradius_txt = ui_magradius_tr.Find("Value/Text").GetComponent<Text>();
        ui_magradius_tr.FindChild("IncreaseBtn").GetComponent<Button>().onClick.AddListener(() => { player_magnet.radius = (++magnet_off_radius); ui_magradius_txt.text = magnet_off_radius.ToString(); });
        ui_magradius_tr.FindChild("DecreaseBtn").GetComponent<Button>().onClick.AddListener(() => { player_magnet.radius = (--magnet_off_radius); ui_magradius_txt.text = magnet_off_radius.ToString(); });

        ui_speed_txt.text = run_speed.ToString();
        ui_stamina_txt.text = current_stamina.ToString();
        ui_chasecell_txt.text = current_chase_cell.ToString();
        ui_magradius_txt.text = magnet_off_radius.ToString();

        onRunSpeedChanged += (value) => ui_speed_txt.text = run_speed.ToString("n1");
        onStaminaChanged += () => ui_stamina_txt.text = current_stamina.ToString();
        onChaseCellChanged += () => ui_chasecell_txt.text = current_chase_cell.ToString();
    }
    #endregion
#endif //CODEDEBUG
}
