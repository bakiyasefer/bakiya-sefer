using UnityEngine;
using FullInspector;
using System.Collections;

public interface IPoolObject
{
    //virtuals
    void Pool_Init(PrefabPool parent, int index, PoolSharedData sharedData
#if CODEDEBUG
        , string debug_data
#endif
        );
    void Pool_Placed();
    void Pool_Placed(object data);
    void Pool_Placed(PatternBox.Element el, int lane_index);
    void Pool_Releasing();

    //non virtuals
    void Release();
    T GetSharedData<T>() where T : PoolSharedData;
    int SizeCells();
}
public class PoolObject : BaseBehavior<FullSerializerSerializer>, IPoolObject
{
    protected int size_in_cells = 0;
    protected float z_after = 0f;

    //parent info
    PrefabPool pool_parent = null;
    int pool_index = -1;

    //cached for performance
    protected Transform this_transform = null;

    protected const float TIME_TO_WAIT = 1f;
    protected Routine.RealtimeWait release_check_waiter;

    #region [IPoolObject]
    public virtual void Pool_Init(PrefabPool parent, int index, PoolSharedData sharedData
#if CODEDEBUG
        , string debug_data
#endif
        )
    {
        pool_parent = parent;
        pool_index = index;
        size_in_cells = sharedData.size_in_cells;
        z_after = size_in_cells * GameController.CELL_DEPTH;
        this_transform = transform;
        release_check_waiter = new Routine.RealtimeWait();
        release_check_waiter.SetDuration(TIME_TO_WAIT);
    }
    public virtual void Pool_Placed()
    {
        release_check_waiter.ResetBreak();
        GameController.Instance.AddWorkRoutine(Routine.Waiter(PoolObject_CheckForRelease()));
    }
    public virtual void Pool_Placed(object data)
    {
        throw new System.NotImplementedException();
    }
    public virtual void Pool_Placed(PatternBox.Element el, int lane_index)
    {
        throw new System.NotImplementedException();
    }
    public virtual void Pool_Releasing()
    {
        release_check_waiter.Complete(true);
    }
    public void Release()
    {
        pool_parent.Release(pool_index);
    }
    public T GetSharedData<T>() where T : PoolSharedData
    {
        return pool_parent.GetSharedData<T>();
    }
    public int SizeCells() 
    {
        return size_in_cells; 
    }
    #endregion //[IPoolObject]

    protected virtual IEnumerator PoolObject_CheckForRelease()
    {
        while (this_transform.position.z + z_after > GameController.LANE_NEAR_Z) {
            yield return release_check_waiter.Reset();
        }
        Release();
    }
}
