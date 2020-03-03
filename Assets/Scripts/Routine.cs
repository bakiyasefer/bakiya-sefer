using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public abstract class Routine
{
    public interface IWait
    {
        bool Breaking();
        bool Waiting();
    }
    public static IEnumerator Waiter(IEnumerator enumerator)
    {
        while (enumerator.MoveNext()) {
            object current = enumerator.Current;
            IWait wait = current as IWait;
            if (wait != null) {
                while (wait.Waiting()) { yield return null; }
                if (wait.Breaking()) { break; }
            } else {
                yield return current;
            }
        }
    }
    public static IEnumerator DelayInvoke(GameController.Event func, IntervalWait wait)
    {
        wait.Reset();
        while (wait.Waiting()) { yield return null; }
        if(!wait.Breaking()) func();
    }
    public static IEnumerator DelayUpdate(GameController.Event func, IntervalWait wait)
    {
        while (true) {
            wait.Reset();
            while (wait.Waiting()) { yield return null; }
            if (wait.Breaking()) break;
            func();
        }
    }
    public abstract class IntervalWait : IWait
    {
        //IWait
        protected bool break_routine = false;
        public bool Breaking() { return break_routine; }
        public abstract bool Waiting();

        public abstract IntervalWait Reset();
    }
    public class TimeWait : IntervalWait
    {
        static float DefaultTimeStepGetter() { return UnityEngine.Time.deltaTime; }

        const float MIN_DURATION = 0.02f;
        float current_time = 0f;
        public float Time() { return current_time; }
        public float TimeNorm() { return current_time / duration; }
        float duration = MIN_DURATION;
        public float Duration() { return duration; }
        GameController.GetValue<float> time_step_getter = DefaultTimeStepGetter;

        //IWait
        public override bool Waiting() { return (current_time += time_step_getter()) < duration; }

        public void SetTimeStepGetter(GameController.GetValue<float> timeStepGetter)
        {
            time_step_getter = timeStepGetter != null ? timeStepGetter : DefaultTimeStepGetter;
        }
        public override IntervalWait Reset()
        {
            current_time = 0f;
            return this;
        }
        public IntervalWait Reset(float timeToWait)
        {
            SetDuration(timeToWait);
            return Reset();
        }
        public void ResetBreak()
        {
            break_routine = false;
        }
        public void SetDuration(float time)
        {
            duration = System.Math.Max(time, MIN_DURATION);
        }
        public void Complete(bool breakRoutine = false)
        {
            current_time = duration;
            if (breakRoutine) {
                break_routine = true;
            }
        }
    }
    public class RealtimeWait : IntervalWait
    {
        const float MIN_DURATION = 0.02f;
        float duration = MIN_DURATION;
        float target_time = 0f;

        //IWait
        public override bool Waiting() { return Time.realtimeSinceStartup < target_time; }

        public override IntervalWait Reset()
        {
            target_time = Time.realtimeSinceStartup + duration;
            return this;
        }
        public IntervalWait Reset(float timeToWait)
        {
            SetDuration(timeToWait);
            return Reset();
        }
        public void ResetBreak()
        {
            break_routine = false;
        }
        public void SetDuration(float time)
        {
            duration = System.Math.Max(time, MIN_DURATION);
        }
        public void Complete(bool breakRoutine = false)
        {
            target_time = 0f;
            if (breakRoutine) { break_routine = true; }
        }
    }
    public class FramesWait : IntervalWait
    {
        const int MIN_FRAMES_TO_WAIT = 1;
        int current_frame = 0;
        int duration = MIN_FRAMES_TO_WAIT;

        //IWait
        public override bool Waiting() { return (++current_frame) < duration; }

        public override IntervalWait Reset()
        {
            current_frame = 0;
            return this;
        }
        public IntervalWait Reset(int framesToWait)
        {
            SetDuration(framesToWait);
            return Reset();
        }
        public void ResetBreak()
        {
            break_routine = false;
        }
        public void SetDuration(int framesToWait)
        {
            duration = System.Math.Max(framesToWait, MIN_FRAMES_TO_WAIT);
        }
        public void Complete(bool breakRoutine = false)
        {
            current_frame = duration;
            if (breakRoutine) { break_routine = true; }
        }
    }
}
public class ParallelRoutine : Routine
{
    const int MIN_CAPACITY = 8;
    List<IEnumerator> routines = null;
    public ParallelRoutine(int capacity)
    {
        routines = new List<IEnumerator>(System.Math.Max(MIN_CAPACITY, capacity));
    }
    public void Add(IEnumerator routine, bool callOnce)
    {
        if (callOnce) routine.MoveNext();
        routines.Add(routine);
    }
    public void StopAll()
    {
        routines.Clear();
    }
    public void Update()
    {
        int count = routines.Count;
        if (count == 0) return;

        for (int i = 0; i < count; ++i) {
            if (!routines[i].MoveNext()) {
                routines.RemoveAt(i);
                break;
            }
        }
    }
}
public class SequentialRoutine : Routine
{
    LinkedList<IEnumerator> routines = null;
    public SequentialRoutine()
    {
        routines = new LinkedList<IEnumerator>();
    }
    public void Push(IEnumerator routine)
    {
        routines.AddLast(routine);
    }
    public void Overlap(IEnumerator routine)
    {
        routines.AddFirst(routine);
    }
    public void StopAll()
    {
        routines.Clear();
    }
    public void Update()
    {
        if (routines.First == null) return;

        if (!routines.First.Value.MoveNext()) {
            routines.RemoveFirst();
        }
    }
}