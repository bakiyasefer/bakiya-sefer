using UnityEngine;
using System.Collections;
using FullInspector;

public class AdvancedAnimationController : BaseBehavior<FullSerializerSerializer>
{
    public const float MIN_CROSSFADE = 0.02f;
    public const float MIN_SPEED = 0.05f;
    public const float MIN_WEIGHT = 0.05f;
    public enum SelectBy { BY_INDEX, FROM_GROUP }
    public enum PlayType { LOOPS_PARTIAL, LOOPS_COMPLETE, PLAYTIME }
    public class Clip
    {
        /*[SetInEditor]*/
        public AnimationClip clip = null;
        /*[SetInEditor]*/
        [InspectorRange(MIN_SPEED, 5f)]
        public float speed = 1f;
        /*[SetInEditor]*/
        [InspectorRange(0f, 1f)]
        public float fadein = 0f;
        /*[SetInEditor]*/
        [InspectorRange(0f, 1f)]
        public float fadeout = 0f;
    }
    public class ClipRefItem : IProbRandomItem
    {
        /*[SetInEditor]*/
        public int index = 0;
        /*[SetInEditor]*/
        [InspectorRange(0f, 5f)]
        public float speed_over = 0;
        /*[SetInEditor]*/
        [InspectorRange(0f, 1f)]
        public float fadein_over = 0;
        /*[SetInEditor]*/
        [InspectorRange(0f, 1f)]
        public float fadeout_over = 0;

        /*[SetInEditor]*/
        [InspectorRange(0f, 1f)]
        public float prob = 1.0f;
        public float ProbValue() { return prob; }
        [NotSerialized, HideInInspector]
        public float ProbHalfSum { get; set; }
    }
    public class SequenceRefItem : IProbRandomItem
    {
        /*[SetInEditor]*/
        public int index = 0;
        /*[SetInEditor]*/
        [InspectorRange(0f, 5f)]
        public float speedmult_over = 0;

        /*[SetInEditor]*/
        [InspectorRange(0f, 1f)]
        public float prob = 1.0f;
        public float ProbValue() { return prob; }
        [NotSerialized, HideInInspector]
        public float ProbHalfSum { get; set; }
    }
    public class SequenceElement
    {
        public string name = string.Empty;
        public SelectBy select_type = SelectBy.BY_INDEX;
        public int index = 0;
        [InspectorRange(MIN_WEIGHT, 1f)]
        public float weight = 1f;
        public PlayType play_type = PlayType.LOOPS_COMPLETE;
        public Vector2 play_value;
    }
    public class Sequence
    {
        public string name = string.Empty;
        [InspectorRange(0f, 5f)]
        public float speed_mult = 0f;
        [InspectorRange(0f, 1f)]
        public float weight_mult = 0f;
        public SequenceElement[] elements = null;

#if UNITY_EDITOR
        [InspectorHeader("Playtime. Complete Loops"), InspectorMargin(10), ShowInInspector, InspectorHidePrimary]
        internal bool __inspector_playtime;
#endif
        public Vector2 num_loops;

#if UNITY_EDITOR
        [InspectorHeader("Proceed to Sequence"), InspectorMargin(10), ShowInInspector, InspectorHidePrimary]
        internal bool __inspector_proceed;
#endif
        public SelectBy select_type = SelectBy.BY_INDEX;
        public int index = 0;
    }
    public class Layer
    {
        /*[SetInEditor]*/
        [InspectorCollectionRotorzFlags(ShowIndices = true)]
        public Clip[] clips = null;
        /*[SetInEditor]*/
        [InspectorCollectionRotorzFlags(ShowIndices = true)]
        public SelectorRefGroup<ClipRefItem>[] state_groups = null;
        /*[SetInEditor]*/
        public SelectBy autostart_select_type = SelectBy.BY_INDEX;
        /*[SetInEditor]*/
        public int autostart_index = 0;
        /*[SetInEditor]*/
        [InspectorCollectionRotorzFlags(ShowIndices = true)]
        public Sequence[] sequences = null;
        /*[SetInEditor]*/
        [InspectorCollectionRotorzFlags(ShowIndices = true)]
        public SelectorRefGroup<SequenceRefItem>[] sequence_groups = null;

        AnimationFader fader = null;
        GameTimer state_timer = null;
        AnimationState[] states = null;

        [ShowInInspector, InspectorDisabled]
        int current_sequence_index = -1;
        public int CurrentSequenceIndex() { return current_sequence_index; }
        [ShowInInspector, InspectorDisabled]
        int current_element_index = -1;
        [ShowInInspector, InspectorDisabled]
        int current_state_index = -1;
        int num_sequence_loops = 1;
        [ShowInInspector, InspectorDisabled]
        int current_sequence_loop = 0;
        float current_state_fadeout = 0f;
        float current_sequence_speedmult_over = 0f;

#if CODEDEBUG
        string debug_data = string.Empty;
#endif

        public void Init(Animation anim, int layer, AnimationBlendMode blendMode, string stateNameFormat
#if CODEDEBUG
            , string debugData
#endif
            )
        {
#if CODEDEBUG
            if (fader != null) {
                string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
                GameController.LogError(METHOD_NAME, (debug_data + " already initialized"));
                return;
            }
#endif
            InitFader();

            for (int i = 0, l = clips.Length; i < l; ++i) {
                anim.AddClip(clips[i].clip, string.Format(stateNameFormat, i));
            }
            states = new AnimationState[clips.Length];
            for (int i = 0, l = clips.Length; i < l; ++i) {
                AnimationState state = anim[string.Format(stateNameFormat, i)];
                state.layer = layer;
                state.blendMode = blendMode;
                state.speed = clips[i].speed;
                states[i] = state;
            }
            if (state_groups == null) { state_groups = new SelectorRefGroup<ClipRefItem>[0]; }
            for (int i = 0; i < state_groups.Length; ++i) {
                state_groups[i].Update();
            }
            if (sequence_groups == null) { sequence_groups = new SelectorRefGroup<SequenceRefItem>[0]; }
            for (int i = 0; i < sequence_groups.Length; ++i) {
                sequence_groups[i].Update();
            }
            if (sequences == null) { sequences = new Sequence[0]; }

            if (autostart_index >= 0 && autostart_index < sequences.Length) {
                PlaySequence(autostart_index, autostart_select_type);
            }
        }
        public void PlaySequence(int index, SelectBy select_type = SelectBy.BY_INDEX)
        {
            //store values
            current_element_index = -1;
            current_sequence_speedmult_over = 0f;

            //switch next sequence
            switch (select_type) {
            case SelectBy.BY_INDEX:
                current_sequence_index = index;
                break;
            case SelectBy.FROM_GROUP:
                var group_item = sequence_groups[index].Get();
                current_sequence_index = group_item.index;
                if (group_item.speedmult_over > MIN_SPEED) {
                    current_sequence_speedmult_over = group_item.speedmult_over;
                }
                break;
            }

            //loops
            Sequence current_sequence = sequences[current_sequence_index];
            if (current_sequence.num_loops.x <= 0f) {
                //infinite loops
                //break by ProceedSequence
                num_sequence_loops = -1;
            } else {
                if (current_sequence.num_loops.y > 0 && current_sequence.num_loops.y < current_sequence.num_loops.x) {
#if CODEDEBUG
                    string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
                    GameController.LogWarning(METHOD_NAME, (debug_data + "sequences[{0}].num_loops.y < x"), current_sequence_index);
#endif
                    current_sequence.num_loops.y = current_sequence.num_loops.x;
                }
                //only complete loops
                if (current_sequence.num_loops.y <= 0 || current_sequence.num_loops.y == current_sequence.num_loops.x) {
                    num_sequence_loops = (int)(0.5f + current_sequence.num_loops.x);
                } else {
                    num_sequence_loops = (int)(0.5f + current_sequence.num_loops.x + (Random.value * (current_sequence.num_loops.y - current_sequence.num_loops.x)));
                }
            }
            current_sequence_loop = 0;

            if (current_sequence.elements == null || current_sequence.elements.Length == 0) {
                if (num_sequence_loops > 0) {
                    //proceed to next sequence
                    GameController.Instance.InvokeOnMT(ProceedSequence);
                }
                //else wait for ProceedSequence command
            } else {
                //proceed to first state
                ProceedState();
            }
        }
        public void ProceedState()
        {
            Sequence current_sequence = sequences[current_sequence_index];
            float last_fadeout = current_state_fadeout;
            //switch next element
            if (++current_element_index >= current_sequence.elements.Length) {
                //correct value
                current_element_index = current_sequence.elements.Length - 1;
                //report
                //if (onSequenceLoopComplete != null) { if (!onSequenceLoopComplete()) return; }
                //inside sequence. reached end
                if (num_sequence_loops > 0 && ++current_sequence_loop >= num_sequence_loops) {
                    //reached end of loops
                    //report
                    //if (onSequenceComplete != null) { if (!onSequenceComplete()) return; }
                    //this removes potential infinite call loop
                    GameController.Instance.InvokeOnMT(ProceedSequence);
                    return;
                }
                //another loop
                current_element_index = 0;
            }

            float fadein = 0f;
            float playspeed = 1f;            
            int last_state_index = current_state_index;
            SequenceElement next_element = current_sequence.elements[current_element_index];
            //switch next clip
            switch (next_element.select_type) {
            case SelectBy.BY_INDEX: {
                    current_state_index = next_element.index;
                    var clip = clips[current_state_index];
                    current_state_fadeout = clip.fadeout;
                    if (clip.speed > MIN_SPEED) { playspeed = clip.speed; }
                    fadein = clip.fadein;
                }
                break;
            case SelectBy.FROM_GROUP: {
                    var group_element = state_groups[next_element.index].Get();
                    current_state_index = group_element.index;
                    //default values
                    var clip = clips[current_state_index];
                    current_state_fadeout = clip.fadeout;
                    if (clip.speed > MIN_SPEED) { playspeed = clip.speed; }
                    fadein = clip.fadein;
                    //override values
                    if (group_element.fadeout_over > MIN_CROSSFADE) { current_state_fadeout = group_element.fadeout_over; }
                    if (group_element.speed_over > MIN_SPEED) { playspeed = group_element.speed_over; }
                    if (group_element.fadein_over > MIN_CROSSFADE) { fadein = group_element.fadein_over; }
                }
                break;
            }
            //weight
            float playweight = next_element.weight;
            if (current_sequence.weight_mult > MIN_WEIGHT) {
                playweight *= current_sequence.weight_mult;
            }

            //set speed
            if (current_sequence_speedmult_over > MIN_SPEED) { 
                playspeed *= current_sequence_speedmult_over;
            } else if (current_sequence.speed_mult > MIN_SPEED) {
                playspeed *= current_sequence.speed_mult;
            }
            states[current_state_index].speed = playspeed;

            //crossfade time
            float crossfade = 0;
            if (last_state_index != current_state_index) {
                crossfade = last_fadeout;
                if (fadein > MIN_CROSSFADE) {
                    if (last_fadeout > MIN_CROSSFADE) {
                        crossfade = (fadein + last_fadeout) * 0.5f;
                    } else {
                        crossfade = fadein;
                    }
                }
            }
            //fader
            if (crossfade > MIN_CROSSFADE) {
                fader.Crossfade(states[current_state_index], -1, crossfade, playweight);
            } else {
                fader.Play(states[current_state_index], true, playweight);
            }

            //state timer
            if (next_element.play_value.x <= 0) {
                //infinite loop
                //breaks by external call to ProceedState
                state_timer.SetEnabled(false);
            } else {
                if (next_element.play_value.y > 0 && next_element.play_value.y < next_element.play_value.x) {
#if CODEDEBUG
                    string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
                    GameController.LogWarning(METHOD_NAME, (debug_data + "sequences[{0}].element[0].proceed_value.y < x"), current_sequence_index);
#endif
                    next_element.play_value.y = next_element.play_value.x;
                }

                float play_time = 0f;
                if (next_element.play_value.y <= 0 || next_element.play_value.y == next_element.play_value.x) {
                    switch (next_element.play_type) {
                    case PlayType.LOOPS_PARTIAL:
                        play_time = next_element.play_value.x * states[current_state_index].length;
                        break;
                    case PlayType.LOOPS_COMPLETE:
                        play_time = ((int)(0.5f + next_element.play_value.x)) * states[current_state_index].length;
                        break;
                    case PlayType.PLAYTIME:
                        play_time = next_element.play_value.x;
                        break;
                    }
                } else {
                    switch (next_element.play_type) {
                    case PlayType.LOOPS_PARTIAL:
                        play_time = (next_element.play_value.x + (Random.value * (next_element.play_value.y - next_element.play_value.x))) * states[current_state_index].length;
                        break;
                    case PlayType.LOOPS_COMPLETE:
                        play_time = ((int)(0.5f + next_element.play_value.x + (Random.value * (next_element.play_value.y - next_element.play_value.x)))) * states[current_state_index].length;
                        break;
                    case PlayType.PLAYTIME:
                        play_time = next_element.play_value.x + (Random.value * (next_element.play_value.y - next_element.play_value.x));
                        break;
                    }
                }

                play_time -= current_state_fadeout;
                state_timer.Reset(play_time);
                state_timer.SetTimeScale(playspeed);
            }
        }
        public void ProceedSequence()
        {
            if (current_sequence_index < 0) {
                PlaySequence(autostart_index);
                return;
            }
            var current_sequence = sequences[current_sequence_index];
            PlaySequence(current_sequence.index, current_sequence.select_type);
        }
        public void SetSequenceSpeedMultOverride(float speedMult)
        {
            float last_speedmult_over = current_sequence_speedmult_over;
            current_sequence_speedmult_over = speedMult;

            if (current_sequence_index == -1) return;

            var current_sequence = sequences[current_sequence_index];
            float playspeed = states[current_state_index].speed;

            //remove speedmult
            if (last_speedmult_over > MIN_SPEED) {
                playspeed /= last_speedmult_over;
            } else if (current_sequence.speed_mult > MIN_SPEED) {
                playspeed /= current_sequence.speed_mult;
            }

            //add speedmult
            if (current_sequence_speedmult_over > MIN_SPEED) {
                playspeed *= current_sequence_speedmult_over;
            } else if (current_sequence.speed_mult > MIN_SPEED) {
                playspeed *= current_sequence.speed_mult;
            }

            states[current_state_index].speed = playspeed;
            state_timer.SetTimeScale(playspeed);
        }
        public void SetSequenceSpeedMult(int sequenceIndex, float speedMult)
        {
            if (sequenceIndex == current_sequence_index) {
                //sequence playing now

                //check if speedmult override active
                if (current_sequence_speedmult_over < MIN_SPEED) {
                    //sequence mult active
                    var current_sequence = sequences[current_sequence_index];
                    float playspeed = states[current_state_index].speed;

                    //remove old speedmult
                    if (current_sequence.speed_mult > MIN_SPEED) {
                        playspeed /= current_sequence.speed_mult;
                    }

                    //apply new speedmult
                    if (speedMult > MIN_SPEED) {
                        playspeed *= speedMult;
                    }
                    states[current_state_index].speed = playspeed;
                    state_timer.SetTimeScale(playspeed);
                }
            }
            //store new mult
            sequences[sequenceIndex].speed_mult = speedMult;
        }
        public void SetStateSpeed(int state_index, float speed)
        {
            if (state_index == current_state_index) {
                //state is active at the moment

                var current_sequence = sequences[current_sequence_index];
                //add speedmult
                if (current_sequence_speedmult_over > MIN_SPEED) {
                    speed *= current_sequence_speedmult_over;
                } else if (current_sequence.speed_mult > MIN_SPEED) {
                    speed *= current_sequence.speed_mult;
                }

                state_timer.SetTimeScale(speed);
            }

            states[state_index].speed = speed;
        }
        public void SetTimestepGetter(GameController.GetValue<float> getter)
        {
            state_timer.SetTimeStepGetter(getter);
            fader.SetTimeStepGetter(getter);
        }
        public void Update()
        {
            fader.Update();
            state_timer.Update();
        }
        public void OnEnable()
        {
            if (fader == null) return;
            
            if (!state_timer.IsStarted() && autostart_index >= 0) {
                PlaySequence(autostart_index, autostart_select_type);
            } else {
                fader.SetEnabled(true, true);
                state_timer.SetEnabled(true);
            }
        }
        public void OnDisable()
        {
            if (fader == null) return;

            fader.SetEnabled(false, true);
            state_timer.SetEnabled(false);
        }

        void InitFader()
        {
            state_timer = new GameTimer();
            state_timer.SetOnComplete(OnStateTimer);
            fader = new AnimationFader();
        }
        void OnStateTimer()
        {
            //report
            //if (onStateComplete != null) { if (!onStateComplete()) return; }

            //dont call UpdateOnMT
            ProceedState();
        }
    }

    [InspectorDatabaseEditor]
    public Layer[] layers = null;
    [InspectorDatabaseEditor]
    public Layer[] additives = null;

    bool is_initialized = false;

    /*GameController.GetValue<bool> onSequenceLoopComplete = null;
    GameController.GetValue<bool> onSequenceComplete = null;
    GameController.GetValue<bool> onStateComplete = null;*/

    void Start()
    {
        if (!is_initialized) { Init(); }
    }
    void Update()
    {
        for (int i = 0; i < layers.Length; ++i) {
            layers[i].Update();
        }
        for (int i = 0; i < additives.Length; ++i) {
            additives[i].Update();
        }
    }
    void OnEnable()
    {
        if (!is_initialized) return;

        for (int i = 0; i < layers.Length; ++i) {
            layers[i].OnEnable();
        }
        for (int i = 0; i < additives.Length; ++i) {
            additives[i].OnEnable();
        }
    }
    void OnDisable()
    {
        if (!is_initialized) return;

        for (int i = 0; i < layers.Length; ++i) {
            layers[i].OnDisable();
        }
        for (int i = 0; i < additives.Length; ++i) {
            additives[i].OnDisable();
        }
    }
    
    public void Init()
    {
        Animation anim = GetComponent<Animation>();
        if (anim != null && anim.GetClipCount() > 0) {
            GameObject.DestroyImmediate(anim);
            anim = null;
        }
        if (anim == null) {
            anim = gameObject.AddComponent<Animation>();
        }
        //disable automatic playback
        anim.playAutomatically = false;

        //layers
        if (layers == null) { layers = new Layer[0]; }
        for (int i = 0; i < layers.Length; ++i) {
            string state_name_format = string.Format("l{0}_", i);
            state_name_format += "{0}";
            layers[i].Init(anim, i, AnimationBlendMode.Blend, state_name_format
#if CODEDEBUG
, string.Format("layers[{0}]", i)
#endif
);
        }
        //additives
        if (additives == null) { additives = new Layer[0]; }
        for (int i = 0; i < additives.Length; ++i) {
            string state_name_format = string.Format("a{0}_", i);
            state_name_format += "{0}";
            additives[i].Init(anim, i, AnimationBlendMode.Additive, state_name_format
#if CODEDEBUG
, string.Format("additives[{0}]", i)
#endif
);
        }

        is_initialized = true;
    }
    public void PlaySequence(int layerIndex, int index, SelectBy select = SelectBy.BY_INDEX)
    {
        layers[layerIndex].PlaySequence(index, select);
    }
    public void SetTimestepGetter(GameController.GetValue<float> getter)
    {
        for (int i = 0; i < layers.Length; ++i) {
            layers[i].SetTimestepGetter(getter);
        }
    }
    /*public void SetProceedControl(GameController.GetValue<bool> onStateCompleteControl, GameController.GetValue<bool> onSequenceLoopCompleteControl, GameController.GetValue<bool> onSequenceCompleteControl)
    {
        onStateComplete = onStateCompleteControl;
        onSequenceLoopComplete = onSequenceLoopCompleteControl;
        onSequenceComplete = onSequenceCompleteControl;
    }*/

#if UNITY_EDITOR
    [ShowInInspector]
    private int test_layer_index = 0;
    [ShowInInspector]
    private int test_sequence_index = 0;
    [InspectorButton]
    void TestPlaySequence()
    {
        layers[test_layer_index].PlaySequence(test_sequence_index);
    }
    [InspectorButton]
    void TestProceedSequence()
    {
        layers[test_layer_index].ProceedSequence();
    }
    [InspectorButton]
    void TestProceedState()
    {
        layers[test_layer_index].ProceedState();
    }

    [ShowInInspector]
    private int test_add_layer_index = 0;
    [ShowInInspector]
    private int test_add_sequence_index = 0;
    [InspectorButton]
    void TestPlayAddSequence()
    {
        additives[test_add_layer_index].PlaySequence(test_add_sequence_index);
    }
    [InspectorButton]
    void TestProceedAddSequence()
    {
        additives[test_add_layer_index].ProceedSequence();
    }
    [InspectorButton]
    void TestProceedAddState()
    {
        additives[test_add_layer_index].ProceedState();
    }
    /*
        [ShowInInspector]
        [InspectorRange(0f, 5f)]
        float test_speed_mult = 0f;
        [InspectorButton]
        void TestSpeedMultOverride()
        {
            SetSpeedMultOverride(test_speed_mult);
        }*/
#endif
}
