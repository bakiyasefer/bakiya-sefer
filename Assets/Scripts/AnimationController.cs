using UnityEngine;
using FullInspector;

public class AnimationController : BaseBehavior<FullSerializerSerializer>
{
    public const float MIN_CROSSFADE = 0.02f;
    public const float MIN_SPEED = 0.1f;
    public class Clip
    {
        /*[SetInEditor]*/
        public AnimationClip clip = null;
        /*[SetInEditor]*/
        [InspectorRange(0f, 5f)]
        public float speed = 1f;
        /*[SetInEditor]*/
        [InspectorRange(0f, 2f)]
        public float fadein = 0f;
        /*[SetInEditor]*/
        [InspectorRange(0f, 2f)]
        public float fadeout = 0f;
    }
    public class SequenceElement
    {
        public string name = string.Empty;
        public int index = 0;
        public float num_loops = 0f;
    }
    public class Sequence
    {
        public string name = string.Empty;
        [InspectorRange(0f, 5f)]
        public float speed_mult = 0f;
        public SequenceElement[] elements = null;

#if UNITY_EDITOR
        [InspectorHeader("Playtime. Complete Loops"), InspectorMargin(10), ShowInInspector, InspectorHidePrimary]
        internal bool __inspector_playtime;
#endif
        public int num_loops = 0;

#if UNITY_EDITOR
        [InspectorHeader("Proceed to Sequence"), InspectorMargin(10), ShowInInspector, InspectorHidePrimary]
        internal bool __inspector_proceed;
#endif
        public int index = 0;
    }

    /*[SetInEditor]*/
    [InspectorCollectionRotorzFlags(ShowIndices = true)]
    public Clip[] clips = null;
    /*[SetInEditor]*/
    public int autostart_index = 0;
    /*[SetInEditor]*/
    [InspectorCollectionRotorzFlags(ShowIndices = true)]
    public Sequence[] sequences = null;

    AnimationFader fader = null;
    AnimationState[] states = null;
    GameTimer state_timer = null;

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

    void Start()
    {
        if (fader == null) { Init(); }
    }
    void Update()
    {
        fader.Update();
        state_timer.Update();
    }
    void OnEnable()
    {
        if (fader == null) { Init(); }

        if (state_timer.IsStarted() == false && autostart_index >= 0) {
            PlaySequence(autostart_index);
        } else {
            fader.SetEnabled(true, true);
            state_timer.SetEnabled(true);
        }
    }
    void OnDisable()
    {
        if (fader == null) return;

        fader.SetEnabled(false, true);
        state_timer.SetEnabled(false);
    }
    public void Init()
    {
        if (fader != null) return;

        InitFader();

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

        for (int i = 0, l = clips.Length; i < l; ++i) {
            anim.AddClip(clips[i].clip, i.ToString());
        }
        states = new AnimationState[clips.Length];
        int state_cursor = 0;
        foreach (AnimationState state in anim) {
            states[state_cursor] = state;
            states[state_cursor].speed = clips[state_cursor].speed;
            ++state_cursor;
        }

#if CODEDEBUG
        if (state_cursor != clips.Length) {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogError(METHOD_NAME, "clips.Length is {0}, states.Length is {1}", clips.Length, state_cursor);
        }
#endif

        if (autostart_index >= 0) {
            PlaySequence(autostart_index);
        }
    }
    public void SetTimestepGetter(GameController.GetValue<float> getter)
    {
        state_timer.SetTimeStepGetter(getter);
        fader.SetTimeStepGetter(getter);
    }
    public float GetClipLength(int index, bool useSpeed = false)
    {
        var clip = clips[index];
        return clip.clip.length * ((useSpeed) ? (1f / System.Math.Max(clip.speed, MIN_SPEED)) : 1f);
    }
    public void PlaySequence(int index)
    {
        //store values
        current_element_index = -1;
        current_sequence_speedmult_over = 0f;

        //switch next sequence
        current_sequence_index = index;

        //loops
        var current_sequence = sequences[current_sequence_index];
        if (current_sequence.num_loops <= 0) {
            //infinite loops
            //break by ProceedSequence
            num_sequence_loops = -1;
        } else {
            num_sequence_loops = current_sequence.num_loops;
        }
        current_sequence_loop = 0;

        if (current_sequence.elements == null || current_sequence.elements.Length == 0) {
            if (num_sequence_loops > 0) {
                //proceed to next sequence
                GameController.Instance.InvokeOnMT(ProceedSequence);
            }
            //else wait foe ProceedSequence command
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
            //inside sequence. reached end
            if (num_sequence_loops > 0 && ++current_sequence_loop >= num_sequence_loops) {
                //reached end of loops
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
        current_state_index = next_element.index;
        var clip = clips[current_state_index];
        current_state_fadeout = clip.fadeout;
        if (clip.speed > MIN_SPEED) { playspeed = clip.speed; }
        fadein = clip.fadein;

        //set speed
        if (current_sequence_speedmult_over > MIN_SPEED) { playspeed *= current_sequence_speedmult_over; }
        else if (current_sequence.speed_mult > MIN_SPEED) { playspeed *= current_sequence.speed_mult; }
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
            fader.Crossfade(states[current_state_index], -1, crossfade, 1f);
        } else {
            fader.Play(states[current_state_index], true, 1f);
        }

        //state timer
        if (next_element.num_loops <= 0) {
            //infinite loop
            //breaks by external call to ProceedState
            state_timer.SetEnabled(false);
        } else {
            float play_time = 0f;
            play_time = next_element.num_loops * states[current_state_index].length;

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
        PlaySequence(current_sequence.index);
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
    void OnStateTimer()
    {
        //dont call UpdateOnMT
        ProceedState();
    }
    void InitFader()
    {
        state_timer = new GameTimer();
        state_timer.SetOnComplete(OnStateTimer);
        fader = new AnimationFader();
    }

#if UNITY_EDITOR
    [ShowInInspector]
    private int test_sequence_index = 0;
    [InspectorButton]
    void TestPlaySequence()
    {
        PlaySequence(test_sequence_index);
    }
    [InspectorButton]
    void TestProceedSequence()
    {
        ProceedSequence();
    }
    [InspectorButton]
    void TestProceedState()
    {
        ProceedState();
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
