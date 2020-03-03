using UnityEngine;
using System.Collections;
using System.IO;
using FullInspector;

public class PlaycharScriptableObject : BaseScriptableObject<FullSerializerSerializer>
{
    /*[SetInEditor]*/
    public float playchar_zpos = 3f;
    /*[SetInEditor]*/
    public float playchar_roll_height_threshold = 1.5f;
    /*[SetInEditor]*/
    public float chaser_zpos = 1f;
    /*[SetInEditor]*/
    public float chaser_offscreen_zpos = -1f;
    /*[SetInEditor]*/
    public float chaser_cell_time = 2f;
    /*[SetInEditor]*/
    [InspectorStepRange(5f, 60f, 1f)]
    public float chaser_delay_time = 20f;
    /*[SetInEditor]*/
    public GameObject run_camera_prefab = null;
    /*[SetInEditor]*/
    [InspectorRange(8f, 18f)]
    public float appobst_slow_speed = 12f;
    [InspectorRange(14f, 24f)]
    /*[SetInEditor]*/
    public float appobst_fast_speed = 18f;

#if UNITY_EDITOR
    [InspectorHeader("Particles"), InspectorMargin(20), InspectorDivider, ShowInInspector, InspectorHidePrimary]
    bool __inspector_particles;
#endif
    /*[SetInEditor]*/
    public GameObject par_playchar_land_prefab = null;
    /*[SetInEditor]*/
    public GameObject par_playchar_bump_prefab = null;
    /*[SetInEditor]*/
    public GameObject par_stamina_end_prefab = null;
    /*[SetInEditor]*/
    public GameObject par_playchar_lucky_prefab = null;
    /*[SetInEditor]*/
    public GameObject par_playchar_crash_continue_prefab = null;
    /*[SetInEditor]*/
    public GameObject par_chaser_land_prefab = null;
    /*[SetInEditor]*/
    public GameObject par_chaser_crash_prefab = null;
    /*[SetInEditor]*/
    public GameObject par_chaser_crashlose_prefab = null;

    /*[SetInEditor]*/
    [InspectorDatabaseEditor]
    public PlaycharSlot[] playchar_slots = null;
    /*[SetInEditor]*/
    [InspectorDatabaseEditor]
    public Playchar[] playchars = null;

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Assets/Create/Playchars")]
    public static void CreateAsset()
    {
        var asset = BaseScriptableObject<FullSerializerSerializer>.CreateInstance<PlaycharScriptableObject>();

        string path = UnityEditor.AssetDatabase.GetAssetPath(UnityEditor.Selection.activeObject);
        if (System.String.IsNullOrEmpty(path)) {
            path = "Assets";
        } else if (!System.String.IsNullOrEmpty(Path.GetExtension(path))) {
            path = Path.GetDirectoryName(path);
        }

        UnityEditor.AssetDatabase.CreateAsset(asset, path + "/playchars.asset");
        UnityEditor.AssetDatabase.SaveAssets();

        UnityEditor.EditorUtility.FocusProjectWindow();

        UnityEditor.Selection.activeObject = asset;
    }
#endif
}
