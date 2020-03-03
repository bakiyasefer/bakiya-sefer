using System.Collections;
using UnityEngine;

public class RoadObstacleController : ObstacleController
{
    protected int num_cells_before = 0;
    public int NumCellsBefore() { return num_cells_before; }
    protected float z_before = 0f;

    protected PatternBox.Element pattern_element = null;
    public PatternBox.Element PatternElement() { return pattern_element; }
    protected bool pattern_mirror = false;
    public bool PatternMirror() { return pattern_mirror; }
    protected Transform coin_before_node = null;
    protected Transform coin_after_node = null;
    public virtual Transform CoinsBeforeNode() { return coin_before_node; }
    public virtual Transform CoinsAfterNode() { return coin_after_node; }

    const float WAIT_TO_BUILD_COINS = 0.2f;
    Routine.TimeWait coin_place_interval_waiter = null;
    public Routine.TimeWait CoinPlaceWaiter() { return coin_place_interval_waiter; }

    #region [PoolObject]
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

        coin_before_node = new GameObject("coins_before").transform;
        coin_before_node.parent = this_transform;
        coin_before_node.localPosition = Vector3.zero;
        coin_after_node = new GameObject("coins_after").transform;
        coin_after_node.parent = this_transform;
        coin_after_node.localPosition = Vector3.zero;

        coin_place_interval_waiter = new Routine.TimeWait();
    }
    public override void Pool_Placed()
    {
        throw new System.NotImplementedException();
    }
    public override void Pool_Placed(PatternBox.Element el, int lane_index)
    {
        pattern_element = el;
        num_cells_before = pattern_element.num_cells_before;
        z_before = num_cells_before * GameController.CELL_DEPTH;
        z_after = (size_in_cells + 1) * GameController.CELL_DEPTH;
        if (pattern_element.coin_form_mod == CoinPlaceFormMod.ARC) {
            z_after += GameController.Instance.PlaycharCtrl().JumpCells() * GameController.CELL_DEPTH * 0.6f;
        }
        pattern_mirror = GameController.Instance.CurrentPBoxMirror();

        coin_before_node.SetLocalPositionZ(-z_before);
        coin_place_interval_waiter.ResetBreak();
        base.Pool_Placed();
    }
    public override void Pool_Releasing()
    {
        while(coin_before_node.childCount > 0) {
            coin_before_node.GetChild(0).GetComponent<PoolObject>().Release();
        }
        while(coin_after_node.childCount > 0) {
            coin_after_node.GetChild(0).GetComponent<PoolObject>().Release();
        }

        coin_place_interval_waiter.Complete(true);
        base.Pool_Releasing();
    }
    #endregion
    public override float TotalHeight()
    {
        return FloorHeight() + this_transform.position.y;
    }

    public virtual float CoinsStepMult() { return 1f; }
    public virtual float CoinsWaitTimeMult() { return 1f; }

    protected override IEnumerator PoolObject_CheckForRelease()
    {
        //Enter State
        release_check_waiter.SetDuration(WAIT_TO_BUILD_COINS);

        //1 Stage
        //wait for proper distance
        while (this_transform.position.z - z_before > GameController.MIN_DISTANCE_TO_BUILD_COINS) {
            yield return base.release_check_waiter.Reset();
        }

        //2 Stage
        //place all coins
        IEnumerator current = GameController.Instance.PlaceCoins(this);
        while (current.MoveNext()) {
            yield return current.Current;
        }

        //3 Stage
        //release
        current = base.PoolObject_CheckForRelease();
        while (current.MoveNext()) {
            yield return current.Current;
        }

        //Leave
    }
}
