using UnityEngine;
using System.Collections;
using FullInspector;

public class TriggerZoneSharedData : PoolSharedData
{
    public string tag = string.Empty;
    public GameController.Event enter_call = null;
    public GameController.Event exit_call = null;
}
public class TriggerZoneController : PoolObject
{
    TriggerZoneSharedData shared_data = null;

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
        shared_data = sharedData as TriggerZoneSharedData;
#if CODEDEBUG
        if (shared_data == null) {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogError(METHOD_NAME, (debug_data + ".shared_data is NULL"));
        }
#endif
    }
    public override void Pool_Placed(object data)
    {
        base.Pool_Placed();

        z_after = (data is float) ? (float)data : 15f;
    }

    void OnTriggerEnter(Collider coll)
    {
        if (shared_data.enter_call != null && coll.CompareTag(shared_data.tag)) {
            shared_data.enter_call();
        }
    }
    void OnTriggerExit(Collider coll)
    {
        if (shared_data.exit_call != null && coll.CompareTag(shared_data.tag)) {
            shared_data.exit_call();
        }
    }
}
