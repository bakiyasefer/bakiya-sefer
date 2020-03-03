using UnityEngine;
using System.Collections;
using FullInspector;

public enum ApproachObstacleSpeed
{
    SLOW,
    FAST
}
public class AppObstSharedData : ObstacleSharedData
{
    /*[SetInEditor]*/
    public ApproachObstacleSpeed speed;
}
public class ApproachRoadObstacleController : RoadObstacleController
{
    protected GameTween<float> zpos_tween = null;
    protected Vector3 local_pos = Vector3.zero;

    protected AppObstSharedData shared_data = null;

    public override void Pool_Init(PrefabPool parent, int index, PoolSharedData sharedData
#if CODEDEBUG
        , string debug_data
#endif
        )
    {
        base.Pool_Init(parent, index, sharedData
#if CODEDEBUG
            , debug_data
#endif
            );

        if (zpos_tween == null) {
            zpos_tween = new FloatTween();
            zpos_tween.SetSetter(UpdatePos);
            zpos_tween.SetTimeStepGetter(GameController.Instance.PlayingTime);
            zpos_tween.SetEase(Easing.Linear);
        }

        shared_data = sharedData as AppObstSharedData;
    }
    public override void Pool_Placed(PatternBox.Element el, int lane_index)
    {
        base.Pool_Placed(el, lane_index);

        GameController gc = GameController.Instance;
        var playchar = gc.PlaycharCtrl();
        if (el.write_serie) {
            this_transform.SetPositionZ(gc.ApproachObstSerieOffset() + (gc.AppObstSerieCells(lane_index) + num_cells_before * GameController.CELL_DEPTH));
        }
        float zpos = this_transform.position.z;
        float playchar_approach_time = ((el.write_serie ? gc.ApproachObstSerieOffset() : zpos) - gc.pch_so.playchar_zpos) / playchar.RunSpeed();
        float speed = ((shared_data.speed == ApproachObstacleSpeed.SLOW) ? gc.pch_so.appobst_slow_speed : gc.pch_so.appobst_fast_speed);
        float pushback_distance = playchar_approach_time * speed;

        local_pos = this_transform.localPosition;
        float begin_offset = local_pos.z + pushback_distance;
        float end_offset = local_pos.z - zpos;
        float tween_time = (begin_offset - end_offset) / speed;
        if (tween_time < 0) {
#if CODEDEBUG
            GameController.LogWarning("AppController", string.Format("tween_time is {0}", tween_time));
#endif
            tween_time *= -1f;
        }
        zpos_tween.SetValues(begin_offset, end_offset);
        zpos_tween.Restart(tween_time);

        local_pos.z = begin_offset;
        this_transform.localPosition = local_pos;
    }
    public override void Pool_Releasing()
    {
        base.Pool_Releasing();

        zpos_tween.SetEnabled(false);
    }

    public override float CoinsStepMult() { return 1f; }
    public override float CoinsWaitTimeMult() { return 0.5f; }

    void Update()
    {
        zpos_tween.UpdateAndApply();
    }

    void UpdatePos(float value)
    {
        local_pos.z = value;
        this_transform.localPosition = local_pos;
    }
}
