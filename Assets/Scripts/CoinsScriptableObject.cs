using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FullInspector;

public class CoinsScriptableObject : BaseScriptableObject<FullSerializerSerializer>
{
#if UNITY_EDITOR
    [InspectorHeader("Coins"), InspectorMargin(20), InspectorDivider, ShowInInspector, InspectorHidePrimary]
    internal bool __inspector_coins;
#endif
    /*[SetInEditor]*/
    [InspectorRange(0.03f, 0.3f)]
    [InspectorCommentAttribute("Coin show delay time\nPlayer lowest and highest speed")]
    public float coins_show_time_begin = 0.12f;
    /*[SetInEditor]*/
    [InspectorRange(0.015f, 0.15f)]
    public float coins_show_time_end = 0.025f;
    /*[SetInEditor]*/
    [InspectorRange(1f, 4f)]
    public float coins_place_step = 2f;
    /*[SetInEditor]*/
    [InspectorRange(0.5f, 2f)]
    public float coins_arc_place_step = 0.5f;
    /*[SetInEditor]*/
    [InspectorCommentAttribute("Magnet Radius at lowest and highest speed")]
    public float magnet_radius_slow = 4f;
    /*[SetInEditor]*/
    public float magnet_radius_fast = 8f;
    /*[SetInEditor]*/
    [InspectorRange(0.1f, 1.0f)]
    public float small_coin_fly_time = 0.3f;
    /*[SetInEditor]*/
    [InspectorRange(0.1f, 1.0f)]
    public float super_coin_fly_time = 0.3f;
    /*[SetInEditor]*/
    public float audio_small_coin_begin_pitch = 1f;

    /*[SetInEditor]*/
    public CoinInitData small_coin_data = null;
    /*[SetInEditor]*/
    public Dictionary<CoinType, SuperCoinInitData> super_coin_data = null;
    /*[SetInEditor]*/
    public CoinType[] super_coins = null;
    /*[SetInEditor]*/
    [InspectorCollectionRotorzFlags(ShowIndices = true)]
    public CoinInitData[] special_coins = null;
    public GameObject letter_coin_particle_prefab = null;
    /*[SetInEditor]*/
    public GameObject coins_arc_trigger_prefab = null;

#if UNITY_EDITOR
    [InspectorHeader("Hand Items"), InspectorMargin(20), InspectorDivider, ShowInInspector, InspectorHidePrimary]
    internal bool __inspector_items;
#endif
    public GameObject hand_magnet_prefab = null;

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Assets/Create/Coins")]
    public static void CreateAsset()
    {
        var asset = BaseScriptableObject<FullSerializerSerializer>.CreateInstance<CoinsScriptableObject>();

        string path = UnityEditor.AssetDatabase.GetAssetPath(UnityEditor.Selection.activeObject);
        if (System.String.IsNullOrEmpty(path)) {
            path = "Assets";
        } else if (!System.String.IsNullOrEmpty(Path.GetExtension(path))) {
            path = Path.GetDirectoryName(path);
        }

        UnityEditor.AssetDatabase.CreateAsset(asset, path + "/coins.asset");
        UnityEditor.AssetDatabase.SaveAssets();

        UnityEditor.EditorUtility.FocusProjectWindow();

        UnityEditor.Selection.activeObject = asset;
    }
#endif
}
