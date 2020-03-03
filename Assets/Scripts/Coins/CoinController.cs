using System.Collections;
using UnityEngine;
using FullInspector;

public enum CoinCategory
{
    COIN,
    POWERUP,
    SPECIAL
}
public enum CoinType
{
    //Coins
    SMALL,
    MEDIUM,
    //Powerups
    STAMINA,
    LUCK,
    MAGNET,
    DROP,
    LUCKX,
    COINSX,
    SCOREX,
    //Special
    MBOX,
    SMBOX,
    SYMBOL,
    LETTER
}
[System.Serializable]
public class CoinSharedData : PoolSharedData
{
    /*[SetInEditor]*/
    public CoinCategory category = CoinCategory.COIN;
    /*[SetInEditor]*/
    public CoinType type = CoinType.SMALL;
    /*[SetInEditor]*/
    public int coin_value = 0;
    [HideInInspector]
    public bool can_fly = true;
    [HideInInspector]
    public GameController.Event<CoinSharedData> pick_method = GameController.Stub;

    public static CoinCategory CoinCategoryForType(CoinType type)
    {
        switch (type) {
        case CoinType.SMALL:
        case CoinType.MEDIUM:
            return CoinCategory.COIN;
        case CoinType.STAMINA:
        case CoinType.LUCK:
        case CoinType.MAGNET:
        case CoinType.DROP:
        case CoinType.COINSX:
        case CoinType.LUCKX:
        case CoinType.SCOREX:
            return CoinCategory.POWERUP;
        default: return CoinCategory.SPECIAL;
        }
    }
}
[RequireComponent(typeof(Rigidbody))]
public class CoinController : PoolObject
{
    CoinSharedData shared_data = null;

    Vector3Tween fly_tween = null;
    Transform fly_target = null;
    Vector3 parent_pos_offset;
    Transform coin_mesh_tr = null;

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

        shared_data = sharedData as CoinSharedData;
#if CODEDEBUG
        if (shared_data.pick_method == null) {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogError(METHOD_NAME, "{0} pick_method is NULL", debug_data);
        }
        if (this_transform.childCount == 0) {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogError(METHOD_NAME, "{0} coin_mech is NULL", debug_data);
        }
#endif
        coin_mesh_tr = this_transform.GetChild(0);

        fly_tween = new Vector3Tween();
        fly_tween.SetTimeStepGetter(GameController.Instance.PlayingTime);
        fly_tween.SetBeginGetter(() => this_transform.parent.position + parent_pos_offset, true);
        fly_tween.SetSetter((value) => this_transform.position = value);
        fly_tween.SetEase(Easing.QuadIn);
        fly_tween.SetDuration(shared_data.type == CoinType.SMALL ? GameController.Instance.coins_so.small_coin_fly_time : GameController.Instance.coins_so.super_coin_fly_time);
        fly_tween.SetAutoUpdate(TweenAutoUpdateMode.APPLY);
        fly_tween.SetOnComplete(CoinPicked);

        z_after = -GameController.Instance.pch_so.playchar_zpos;
        release_check_waiter.SetDuration(0.1f);
    }
    public override void Pool_Releasing()
    {
        fly_tween.SetEndGetter(null, false);
        fly_tween.Complete(false);

        coin_mesh_tr.rotation = Quaternion.identity;

        base.Pool_Releasing();
    }
    #endregion //[PoolObject]

    void OnTriggerEnter(Collider coll)
    {
        if (shared_data.can_fly && !fly_tween.IsEnabled() && coll.CompareTag(GameController.TAG_MAGNET)) {
            parent_pos_offset = this_transform.position - this_transform.parent.position;
            fly_target = coll.transform;
            fly_tween.SetEndGetter(CoinFlyEndGetter, true);
            fly_tween.Restart();
        } else if (coll.CompareTag(GameController.TAG_PICKER)) {
            CoinPicked();
        }
    }
    void CoinPicked()
    {
        shared_data.pick_method(shared_data);
        Release();
    }

    Vector3 CoinFlyEndGetter()
    {
        if (fly_target == null) {
#if CODEDEBUG
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogError(METHOD_NAME, "fly_target is NULL");
#endif
            fly_tween.SetEndGetter(null, false);
            fly_tween.Complete(false);
            return Vector3.zero;
        }
        return fly_target.position;
    }
}
