using UnityEngine;
using System.Collections.Generic;

public class GameTimer
{
    public const float MIN_DURATION = 0.001f;
    bool is_enabled = false;
    public bool IsEnabled() { return is_enabled; }
    bool is_looping = false;
    public bool IsLooping() { return is_looping; }
    bool is_started = false;
    public bool IsStarted() { return is_started; }
    float duration = 1f;
    public float Duration() { return duration; }
    float time_passed = 0f;
    public float TimePassed() { return time_passed; }
    public float TimePassedNorm() { return time_passed / duration; }
    public float TimeRemaining() { return duration - time_passed; }
    public float TimeRemainingNorm() { return (duration - time_passed) / duration; }
    public bool NearEnd() { return (duration - time_passed) < MIN_DURATION; }
    public bool NearBegin() { return time_passed < MIN_DURATION; }
    float time_scale = 1f;
    public float TimeScale() { return time_scale; }

    bool autoupdate = false;

    GameController.GetValue<float> time_step_getter = null, duration_getter = null;
    GameController.Event update_func = null;

    GameController.Event complete_event = null;
    GameController.Event complete_once_event = null;

    public GameTimer()
    {
        time_step_getter = _defaultTimestepGetter;
        update_func = _updateInactive;
        is_started = is_enabled = false;
    }
    public void Update()
    {
        update_func();
    }
    public void Reset()
    {
        Reset((duration_getter != null) ? duration_getter() : duration);
    }
    public void Reset(float time)
    {
        SetDuration(time);
        time_passed = 0f;

        if (autoupdate) { GameController.Instance.AddUpdateOnMT(Update); }

        update_func = _update;
        is_enabled = is_started = true;
    }
    public void SetTimePassed(float timePassed)
    {
        time_passed = (timePassed < 0f) ? 0f : (timePassed > duration) ? duration : timePassed;
    }
    public void SetTimePassedNorm(float timePassedNorm)
    {
        SetTimePassed(timePassedNorm * duration);
    }
    public void SetLooping(bool looping)
    {
        is_looping = looping;
    }
    public void Complete(bool callEvents)
    {
        if (!is_started) return;

        if (callEvents) {
            if (complete_once_event != null) { GameController.Instance.InvokeOnMT(complete_once_event); complete_once_event = null; }
            if (complete_event != null) { GameController.Instance.InvokeOnMT(complete_event); }
        }
        if (is_looping) {
            Reset();
        } else {
            if (autoupdate) { GameController.Instance.RemoveUpdateOnMT(Update); }
            time_passed = duration;
            update_func = _updateInactive;
            is_started = is_enabled = false;
        }
    }
    public void SetEnabled(bool enabled)
    {
        if (is_started) {
            if ((is_enabled = enabled)) update_func = _update;
            else update_func = _updateInactive;
        }
    }
    public void SetAutoUpdate(bool enabled)
    {
        if (enabled) {
            if (is_started) { GameController.Instance.AddUpdateOnMT(Update); }
        } else {
            if (autoupdate) { GameController.Instance.RemoveUpdateOnMT(Update); }
        }
        autoupdate = enabled;
    }
    public void SetOnComplete(GameController.Event func)
    {
        complete_event = func;
    }
    public void AddOnComplete(GameController.Event func)
    {
        complete_event += func;
    }
    public void RemoveOnComplete(GameController.Event func)
    {
        complete_event -= func;
    }
    public void SetOnCompleteOnce(GameController.Event func)
    {
        complete_once_event = func;
    }
    public void AddOnCompleteOnce(GameController.Event func)
    {
        complete_once_event += func;
    }
    public void RemoveOnCompleteOnce(GameController.Event func)
    {
        complete_once_event -= func;
    }
    public void SetTimeStepGetter(GameController.GetValue<float> timeStepGetter)
    {
        time_step_getter = (timeStepGetter == null) ? _defaultTimestepGetter : timeStepGetter;
    }
    public void SetDuration(float durationTime)
    {
        duration = (durationTime < MIN_DURATION) ? MIN_DURATION : durationTime;
    }
    public void SetDurationGetter(GameController.GetValue<float> durationGetter)
    {
        duration_getter = durationGetter;
    }
    public void SetTimeScale(float timeScale)
    {
        time_scale = (timeScale < 0f) ? 0f : timeScale;
    }
    public void ClearEvents()
    {
        complete_event = null;
        complete_once_event = null;
    }
    float _defaultTimestepGetter()
    {
        return Time.deltaTime;
    }
    void _update()
    {
        if ((time_passed += (time_step_getter() * time_scale)) > duration) { Complete(true); }
    }
    void _updateInactive()
    {
    }
}
public enum TweenAutoUpdateMode { NONE, UPDATE, APPLY }
public abstract class GameTween<T>
{
    protected T begin_value, end_value, delta_value, current_value;
    public T CurrentValue() { return current_value; }
    protected GameController.GetValue<T> begin_getter = null, end_getter = null;
    protected GameController.Event<T> setter = null;
    protected bool dynamic_begin = false, dynamic_end = false;
    public bool IsDynamic() { return dynamic_begin || dynamic_end; }

    public delegate float EaseFunction(float time, float begin_value, float delta_value, float duration);
    protected EaseFunction ease_function = Easing.Linear;
    protected AnimationCurve ease_curve = null;
    GameController.GetValue<T> evalget_func = null;
    GameController.Event evalset_func = null;

    int loop_count = 1, loops_completed = 0;
    public int LoopsCompleted { get { return loops_completed; } }

    TweenAutoUpdateMode autoupdate_mode = TweenAutoUpdateMode.NONE;

    GameController.GetValue<float> duration_getter = null, delay_getter = null;
    float delay = 0f, duration = 1f;
    public float Duration() { return duration; }
    public float Delay() { return delay; }
    public float TimePassed() { return is_delaying ? 0f : timer.TimePassed(); }
    public float TimePassedNorm() { return is_delaying ? 0f : timer.TimePassedNorm(); }
    bool is_delaying = false;
    public bool IsDelaying() { return is_delaying; }
    GameTimer timer = null;
    public bool IsEnabled() { return timer.IsEnabled(); }
    public bool IsStarted() { return timer.IsStarted(); }

    GameController.Event complete_event = null,
        complete_once_event = null,
        loop_complete_event = null,
        loop_complete_once_event = null;

    protected abstract void UpdateCurrentValue(float time, float duration);
    protected abstract void UpdateDeltaValue();


    public GameTween()
    {
        timer = new GameTimer();
        timer.SetOnComplete(() => _doComplete(true));
        SetEnabled(false);
    }
    public void Restart()
    {
        loops_completed = 0;
        _doRestart((delay_getter != null) ? delay_getter() : delay, (duration_getter != null) ? duration_getter() : duration);
    }
    public void Restart(float durationTime)
    {
        loops_completed = 0;
        _doRestart(-1f, durationTime);
    }
    public void Restart(float delayTime, float durationTime)
    {
        loops_completed = 0;
        _doRestart(delayTime, durationTime);
    }
    public void Reverse()
    {
        //swap values.
        //use delta here.
        //if tween is active delta will be updated
        delta_value = begin_value;
        begin_value = end_value;
        end_value = delta_value;

        //swap getters
        var temp_getter = begin_getter;
        begin_getter = end_getter;
        end_getter = temp_getter;

        //swap getter bools
        /*bool temp_bool = dynamic_begin;
        dynamic_begin = dynamic_end;
        dynamic_end = temp_bool;*/

        if (timer.IsStarted()) {
            //update if working
            if (begin_getter != null) { begin_value = begin_getter(); }
            if (end_getter != null) { end_value = end_getter(); }
            UpdateDeltaValue();

            timer.SetTimePassed(timer.Duration() - timer.TimePassed());
        }
    }
    public void SetTimePosition(float timePos)
    {
        CompleteDelay();
        timer.SetTimePassed(timePos);
    }
    public void SetTimePositionNorm(float timePosNorm)
    {
        CompleteDelay();
        timer.SetTimePassedNorm(timePosNorm);
    }
    public void CompleteDelay()
    {
        if (is_delaying) {
            _doComplete(false);
        }
    }
    public void Complete(bool callEvents)
    {
        CompleteDelay();
        loop_count = 1;
        timer.Complete(false);
        _doComplete(callEvents);
    }
    public void CompleteAndClear(bool callEvents)
    {
        if (timer.IsStarted()) { Complete(callEvents); }
        ClearEvents();
        SetGetters(null, false, null, false);
        SetSetter(null);
        SetEase(Easing.Linear);
    }
    public void SetEnabled(bool enabled)
    {
        timer.SetEnabled(enabled);
        if (timer.IsEnabled()) {
            if (is_delaying) {
                if (dynamic_begin) {
                    //dynamic begin
                    evalget_func = _evaluateGet_BeginDynamic;
                    evalset_func = _evaluateSet_BeginDynamic;
                } else {
                    //delaying
                    evalget_func = _evaluateGet_Delay;
                    evalset_func = _evaluateSet_Delay;
                }
            } else {
                //active
                evalget_func = _evaluateGet;
                evalset_func = _evaluateSet;
            }
        } else if(timer.IsStarted()) {
            //pause
            if (IsDynamic()) {
                //full dynamic
                evalget_func = _evaluateGet;
                evalset_func = _evaluateSet;
            } else {
                //inactive
                evalget_func = _evaluateGet_Inactive;
                evalset_func = _evaluateSet_Inactive;
            }
        } else {
            //complete or not started
            if (dynamic_end && loops_completed > 0) {
                //dynamic end
                evalget_func = _evaluateGet_EndDynamic;
                evalset_func = _evaluateSet_EndDynamic;
            } else {
                //inactive
                evalget_func = _evaluateGet_Inactive;
                evalset_func = _evaluateSet_Inactive;
            }
        }
    }
    public void SetValues(T beginValue, T endValue)
    {
        begin_value = beginValue;
        end_value = endValue;
        UpdateDeltaValue();
    }
    public void SetBeginValue(T beginValue)
    {
        begin_value = beginValue;
        UpdateDeltaValue();
    }
    public void SetEndValue(T endValue)
    {
        end_value = endValue;
        UpdateDeltaValue();
    }
    public void SetCurrentValue(T currentValue)
    {
        current_value = currentValue;
    }
    public void SetBeginGetter(GameController.GetValue<T> beginGetter, bool getBeginOnUpdate)
    {
        begin_getter = beginGetter;
        dynamic_begin = getBeginOnUpdate;
        SetEnabled(timer.IsEnabled());
    }
    public void SetEndGetter(GameController.GetValue<T> endGetter, bool getEndOnUpdate)
    {
        end_getter = endGetter;
        dynamic_end = getEndOnUpdate;
        SetEnabled(timer.IsEnabled());
    }
    public void SetGetters(GameController.GetValue<T> beginGetter, bool getBeginOnUpdate, GameController.GetValue<T> endGetter, bool getEndOnUpdate)
    {
        begin_getter = beginGetter;
        dynamic_begin = getBeginOnUpdate;
        end_getter = endGetter;
        dynamic_end = getEndOnUpdate;
        SetEnabled(timer.IsEnabled());
    }
    public void SetSetter(GameController.Event<T> setterFunc)
    {
        setter = setterFunc;
    }
    public void AddSetter(GameController.Event<T> setterFunc)
    {
        setter += setterFunc;
    }
    public void RemoveSetter(GameController.Event<T> setterFunc)
    {
        setter -= setterFunc;
    }
    public void SetEase(EaseFunction easeFunc)
    {
        ease_curve = null;
        ease_function = easeFunc;
    }
    public void SetEase(AnimationCurve curve)
    {
        ease_curve = curve;
        ease_function = EvaluateCurve;
    }
    public void SetAutoUpdate(TweenAutoUpdateMode mode)
    {
        switch (autoupdate_mode) {
        case TweenAutoUpdateMode.UPDATE: GameController.Instance.RemoveUpdateOnMT(Update); break;
        case TweenAutoUpdateMode.APPLY: GameController.Instance.RemoveUpdateOnMT(UpdateAndApply); break;
        }
        autoupdate_mode = mode;
        switch (autoupdate_mode) {
        case TweenAutoUpdateMode.UPDATE: GameController.Instance.AddUpdateOnMT(Update); break;
        case TweenAutoUpdateMode.APPLY: GameController.Instance.AddUpdateOnMT(UpdateAndApply); break;
        }
    }
    public void SetTimeStepGetter(GameController.GetValue<float> timeStepFunc)
    {
        timer.SetTimeStepGetter(timeStepFunc);
    }
    public void SetDuration(float durationTime)
    {
        if (!is_delaying) {
            timer.SetDuration(durationTime);
            //get corrected value
            duration = timer.Duration();
        } else {
            //save unchecked time value for now
            duration = durationTime;
        }
    }
    public void SetDurationGetter(GameController.GetValue<float> durationGetter)
    {
        duration_getter = durationGetter;
    }
    public void SetDelay(float delayTime)
    {
        if (is_delaying) {
            timer.SetDuration(delayTime);
            //get corrected value
            delay = timer.Duration();
        } else {
            //save unchecked time value for now
            delay = delayTime;
        }
    }
    public void SetDelayGetter(GameController.GetValue<float> delayGetter)
    {
        delay_getter = delayGetter;
    }
    public void SetLoopCount(int loopCount)
    {
        loop_count = loopCount;
    }
    public void SetOnComplete(GameController.Event func)
    {
        complete_event = func;
    }
    public void AddOnComplete(GameController.Event func)
    {
        complete_event += func;
    }
    public void RemoveOnComplete(GameController.Event func)
    {
        complete_event -= func;
    }
    public void SetOnCompleteOnce(GameController.Event func)
    {
        complete_once_event = func;
    }
    public void AddOnCompleteOnce(GameController.Event func)
    {
        complete_once_event += func;
    }
    public void RemoveOnCompleteOnce(GameController.Event func)
    {
        complete_once_event -= func;
    }
    public void SetOnLoopComplete(GameController.Event func)
    {
        loop_complete_event = func;
    }
    public void AddOnLoopComplete(GameController.Event func)
    {
        loop_complete_event += func;
    }
    public void RemoveOnLoopComplete(GameController.Event func)
    {
        loop_complete_event -= func;
    }
    public void SetOnLoopCompleteOnce(GameController.Event func)
    {
        loop_complete_once_event = func;
    }
    public void AddOnLoopCompleteOnce(GameController.Event func)
    {
        loop_complete_once_event += func;
    }
    public void RemoveOnLoopCompleteOnce(GameController.Event func)
    {
        loop_complete_once_event -= func;
    }
    public void ClearEvents()
    {
        complete_event = null;
        complete_once_event = null;
        loop_complete_event = null;
        loop_complete_once_event = null;
    }
    public T EvaluateAndGet()
    {
        return evalget_func();
    }
    public void EvaluateAndSet()
    {
        setter(evalget_func());
    }
    public void Update()
    {
        timer.Update();
    }
    public T UpdateAndGet()
    {
        timer.Update();
        return evalget_func();
    }
    public void UpdateAndApply()
    {
        timer.Update();
        evalset_func();
    }
    float EvaluateCurve(float t, float b, float e, float d)
    {
        return e * ease_curve.Evaluate((t / d) * ease_curve[ease_curve.length - 1].time) + b;
    }
    T _evaluateGet()
    {
        UpdateCurrentValue(timer.TimePassed(), duration);
        return current_value;
    }
    T _evaluateGet_Inactive()
    {
        return current_value;
    }
    T _evaluateGet_Delay()
    {
        //while in delay Do update once
        UpdateCurrentValue(0f, duration);
        //then change to inactive
        evalget_func = _evaluateGet_Inactive;
        evalset_func = _evaluateSet_Inactive;
        return current_value;
    }
    T _evaluateGet_BeginDynamic()
    {
        UpdateCurrentValue(0f, duration);
        return current_value;
    }
    T _evaluateGet_EndDynamic()
    {
        UpdateCurrentValue(duration, duration);
        return current_value;
    }
    void _evaluateSet()
    {
        UpdateCurrentValue(timer.TimePassed(), duration);
        setter(current_value);
    }
    void _evaluateSet_Inactive()
    {
        //do not call setter while inactive
    }
    void _evaluateSet_Delay()
    {
        //while in delay Do update once
        UpdateCurrentValue(0f, duration);
        //then change to inactive
        evalget_func = _evaluateGet_Inactive;
        evalset_func = _evaluateSet_Inactive;
        setter(current_value);
    }
    void _evaluateSet_BeginDynamic()
    {
        UpdateCurrentValue(0f, duration);
        setter(current_value);
    }
    void _evaluateSet_EndDynamic()
    {
        UpdateCurrentValue(duration, duration);
        setter(current_value);
    }
    void _doRestart(float delayTime, float durationTime)
    {
        delay = delayTime;
        duration = durationTime;
        if (delay > 0f) {
            timer.Reset(delay);
            is_delaying = true;
            //get corrected value
            delay = timer.Duration();
        } else {
            timer.Reset(duration);
            is_delaying = false;
            //get corrected value
            duration = timer.Duration();
        }

        if (dynamic_begin && is_delaying) {
            evalget_func = _evaluateGet_BeginDynamic;
            evalset_func = _evaluateSet_BeginDynamic;
        } else if (is_delaying) {
            evalget_func = _evaluateGet_Delay;
            evalset_func = _evaluateSet_Delay;
        } else {
            evalget_func = _evaluateGet;
            evalset_func = _evaluateSet;
        }

        //update values
        if (begin_getter != null)
            begin_value = begin_getter();
        if (end_getter != null)
            end_value = end_getter();
        UpdateDeltaValue();
        current_value = begin_value;

        //autoupdate
        switch (autoupdate_mode) {
        case TweenAutoUpdateMode.UPDATE: GameController.Instance.AddUpdateOnMT(Update); break;
        case TweenAutoUpdateMode.APPLY: GameController.Instance.AddUpdateOnMT(UpdateAndApply); break;
        }
    }
    void _doComplete(bool callEvents)
    {
        if (is_delaying) {
            if (duration_getter != null) { duration = duration_getter(); }
            timer.Reset(duration);
            is_delaying = false;
            evalget_func = _evaluateGet;
            evalset_func = _evaluateSet;
        } else {
            ++loops_completed;
            if (callEvents) {
                if (loop_complete_once_event != null) { GameController.Instance.InvokeOnMT(loop_complete_once_event); loop_complete_once_event = null; }
                if (loop_complete_event != null) { GameController.Instance.InvokeOnMT(loop_complete_event); }
            }
            if (loop_count <= 0 || loops_completed < loop_count) {
                _doRestart((delay_getter != null) ? delay_getter() : delay, (duration_getter != null) ? duration_getter() : duration);
            } else {
                if (dynamic_end) {
                    evalget_func = _evaluateGet_EndDynamic;
                    evalset_func = _evaluateSet_EndDynamic;
                } else {
                    //autoupdate
                    switch (autoupdate_mode) {
                    case TweenAutoUpdateMode.UPDATE: GameController.Instance.RemoveUpdateOnMT(Update); break;
                    case TweenAutoUpdateMode.APPLY: GameController.Instance.RemoveUpdateOnMT(UpdateAndApply); break;
                    }
                    //set current_value to end
                    UpdateCurrentValue(duration, duration);
                    if (setter != null) { setter(current_value); }
                    evalget_func = _evaluateGet_Inactive;
                    evalset_func = _evaluateSet_Inactive;
                }
                if (callEvents) {
                    if (complete_once_event != null) { GameController.Instance.InvokeOnMT(complete_once_event); complete_once_event = null; }
                    if (complete_event != null) { GameController.Instance.InvokeOnMT(complete_event); }
                }
            }
        }
    }
}
public class FloatTween : GameTween<float>
{
    protected override void UpdateCurrentValue(float time, float duration)
    {
        bool delta_needs_update = false;
        if (dynamic_begin) { begin_value = begin_getter(); delta_needs_update = true; }
        if (dynamic_end) { end_value = end_getter(); delta_needs_update = true; }
        if (delta_needs_update) { UpdateDeltaValue(); }

        current_value = ease_function(time, begin_value, delta_value, duration);
    }
    protected override void UpdateDeltaValue()
    {
        delta_value = end_value - begin_value;
    }
}
public class Vector3Tween : GameTween<Vector3>
{
    protected override void UpdateCurrentValue(float time, float duration)
    {
        bool delta_needs_update = false;
        if (dynamic_begin) { begin_value = begin_getter(); delta_needs_update = true; }
        if (dynamic_end) { end_value = end_getter(); delta_needs_update = true; }
        if (delta_needs_update) { UpdateDeltaValue(); }

        current_value.x = ease_function(time, begin_value.x, delta_value.x, duration);
        current_value.y = ease_function(time, begin_value.y, delta_value.y, duration);
        current_value.z = ease_function(time, begin_value.z, delta_value.z, duration);
    }
    protected override void UpdateDeltaValue()
    {
        delta_value = end_value - begin_value;
    }
}
public static class AnimationExtension
{
    public static AnimationState StateAt(this Animation anim, int index)
    {
        foreach (AnimationState state in anim) {
            if (--index < 0) return state;
        }
        return null;
    }
    public static void SampleAt(this Animation anim, int index, float norm_time = 0f)
    {
        anim.SampleState(anim.StateAt(index), norm_time);
    }
    public static void SampleState(this Animation anim, AnimationState anim_state = null, float norm_time = 0f)
    {
        if (anim_state == null) {
            //use first state
            anim_state = anim.StateAt(0);
        }
        float old_weight = anim_state.weight, old_time = anim_state.time;
        anim_state.enabled = true;
        anim_state.weight = 1f;
        anim_state.normalizedTime = norm_time;
        anim.Sample();
        anim_state.weight = old_weight;
        anim_state.time = old_time;
        anim_state.enabled = false;
    }
    public static void PlayAt(this Animation anim, int index, float time = 0f, float speed = 1f, float weight = 1f)
    {
        var state = StateAt(anim, index);
#if CODEDEBUG
        if (state == null) {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogError(METHOD_NAME, "anim[{0}] is NULL", index);
            return;
        }
#endif
        state.speed = speed;
        state.time = time;
        state.weight = weight;
        state.enabled = true;
    }
    public static void StopLayer(this Animation anim, int layer)
    {
        foreach (AnimationState state in anim) {
            if (state.layer == layer) {
                state.enabled = false;
                state.weight = 0f;
            }
        }
    }
}
public class AnimationFader
{
    public static void StopState(AnimationState state)
    {
        state.enabled = false;
        state.weight = 0f;
    }

    public const float THRESHOLD = 0.05f;
    public AnimationState from = null;
    public AnimationState to = null;
    float from_begin_weight = 1.0f;
    float to_target_weight = 1.0f;

    GameTween<float> tween = null;

    public bool IsTweenEnabled { get { return tween.IsEnabled(); } }
    public bool IsAnimationsEnabled { get { return (from != null && from.enabled) || (to != null && to.enabled); } }
    public AnimationFader()
    {
        tween = new FloatTween();
        tween.SetBeginValue(0f);
        tween.SetEndValue(1f);
        tween.SetEase(Easing.Linear);
        tween.SetSetter(Setter);
        tween.SetOnComplete(_onTweenComplete);
    }
    public void SetEnabled(bool enabled, bool affectAnimations)
    {
        tween.SetEnabled(enabled);
        if (affectAnimations) {
            if (from != null) { from.enabled = enabled; }
            if (to != null) { to.enabled = enabled; }
        }
    }
    public void SetEase(GameTween<float>.EaseFunction easeFunc)
    {
        tween.SetEase(easeFunc);
    }
    public void SetEase(AnimationCurve curve)
    {
        tween.SetEase(curve);
    }
    public void SetTimeStepGetter(GameController.GetValue<float> timeStepGetter)
    {
        tween.SetTimeStepGetter(timeStepGetter);
    }
    public void Play(AnimationState state, bool forceRestart, float playAtWeight)
    {
#if CODEDEBUG
        if (state == null) {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogError(METHOD_NAME, "state is NULL");
            return;
        }
#endif
        //stop tween
        tween.SetEnabled(false);
        if (from != null) {
            StopState(from);
            from = null;
        }

        if (forceRestart || to != state) {
            if (to != null) { StopState(to); }
            to = state;
            /*if (to.speed > 0f) { to.time = 0f; }
            else { to.normalizedTime = 1f; }*/
            to.time = 0;
        }
        to.weight = to_target_weight = playAtWeight;
        to.enabled = true;
    }
    public void Crossfade(AnimationState To, float delayTime, float crossTime, float toWeight)
    {
        //stop old from
        if (from != null) { StopState(from); }

        from = to;
        //it is possible to fade from zero
        if (from != null) {
            from.enabled = true;
            from_begin_weight = from.weight;
        }

        to = To;
        //it is possible to fade to zero
        if (to != null) {
            to.enabled = true;
            to.weight = 0f;
            to.time = 0;
        }

        tween.Restart(delayTime, crossTime);
    }
    public void Update()
    {
        tween.UpdateAndApply();
    }

    void Setter(float val)
    {
        if (to != null) { to.weight = val * to_target_weight; }
        if (from != null) { from.weight = (1f - val) * from_begin_weight; }
    }
    void _onTweenComplete()
    {
        if (from != null) {
            StopState(from);
            from = null;
        }
        if (to != null) { to.weight = to_target_weight; }
    }
}
public class EventTimeline
{
    List<KeyValuePair<float, GameController.Event>> marks = null;
    KeyValuePair<float, GameController.Event> next_mark;
    int next_mark_index = 0;
    bool marks_dirty = false;

    public bool IsEnabled() { return timer.IsEnabled(); }
    public bool IsStarted() { return timer.IsStarted(); }
    public float Duration() { return timer.Duration(); }
    public float TimePassed() { return timer.TimePassed(); }
    public float TimeNorm() { return timer.TimePassedNorm(); }
    public float TimeScale() { return timer.TimeScale(); }

    GameTimer timer = null;
    GameController.Event onComplete = null;
    GameController.Event update_func = null;

    public EventTimeline(int capacity)
    {
        timer = new GameTimer();
        timer.SetOnComplete(_onComplete);

        marks = new List<KeyValuePair<float, GameController.Event>>(capacity);
    }
    public void Add(float position, GameController.Event call)
    {
        if (position > timer.Duration()) { timer.SetDuration(position); }
        marks.Add(new KeyValuePair<float, GameController.Event>(position, call));
        marks_dirty = true;
    }
    public void Remove(GameController.Event call)
    {
        marks.RemoveAt(marks.FindIndex((keyvalue) => {return keyvalue.Value == call;}));
    }
    public void SetEnabled(bool enabled)
    {
        timer.SetEnabled(enabled);
        if (timer.IsEnabled() && enabled) {
            update_func = _update;
        } else {
            update_func = _updateInactive;
        }
    }
    public void SetCapacity(int cap)
    {
        marks.Capacity = cap;
    }
    public void SetTimestep(GameController.GetValue<float> timeStepGetter)
    {
        timer.SetTimeStepGetter(timeStepGetter);
    }
    public void SetTimeScale(float timeScale)
    {
        timer.SetTimeScale(timeScale);
    }
    public void SetDuration(float duration)
    {
        if (marks.Count > 0 && duration < marks[marks.Count - 1].Key) {
            duration = marks[marks.Count - 1].Key;
        }
        timer.SetDuration(duration);
    }
    public void SetOnComplete(GameController.Event handler)
    {
        onComplete = handler;
    }
    public void Restart()
    {
        if (marks_dirty) { marks.Sort((s1, s2) => s1.Key.CompareTo(s2.Key)); }
        timer.Reset();
        if (marks.Count > 0) {
            next_mark_index = 0;
            next_mark = marks[next_mark_index];
        } else {
            next_mark_index = -1;
        }

        update_func = _update;
    }

    public void Update()
    {
        update_func();
    }
    void _update()
    {
        timer.Update();
        if (next_mark_index >= 0 && timer.TimePassed() > next_mark.Key) {
            next_mark.Value();
            if (++next_mark_index < marks.Count) { next_mark = marks[next_mark_index]; }
            else { next_mark_index = -1; }
        }
    }
    void _updateInactive()
    {

    }
    void _onComplete()
    {
        update_func = _updateInactive;
        if (onComplete != null) onComplete();
    }
}