using UnityEngine;
using System.Collections;
using System.IO;
using FullInspector;

public class PatternScriptableObject : BaseScriptableObject<FullSerializerSerializer>
{
    /*[SetInEditor]*/
    [InspectorDatabaseEditor/*InspectorCollectionRotorzFlags(ShowIndices = true)*/, InspectorCategory("PBoxes")]
    public PatternBox[] pboxes = null;
    /*[SetInEditor]*/
    [InspectorCollectionRotorzFlags(ShowIndices = true), InspectorCategory("PBoxes")]
    public SimpleRefGroup[] pbox_groups = null;
    /*[SetInEditor]*/
    [InspectorDatabaseEditor, InspectorCategory("Roads")]
    public RoadObstacleSet[] roads = null;
    /*[SetInEditor]*/
    [InspectorDatabaseEditor, InspectorCategory("PSBoxes")]
    public PatternSuperBox[] psboxes = null;
    /*[SetInEditor]*/
    [InspectorCollectionRotorzFlags(ShowIndices = true), InspectorCategory("PSBoxes")]
    public SimpleRefGroup[] psbox_groups = null;
    /*[SetInEditor]*/
    [InspectorDatabaseEditor/*InspectorCollectionRotorzFlags(ShowIndices = true)*/, InspectorCategory("Patterns")]
    public ObstaclePattern[] patterns = null;
    /*[SetInEditor]*/
    [InspectorCollectionRotorzFlags(ShowIndices = true), InspectorCategory("Patterns")]
    public SimpleRefGroup[] pattern_groups = null;

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Assets/Create/Patterns")]
    public static void CreateAsset()
    {
        var asset = BaseScriptableObject<FullSerializerSerializer>.CreateInstance<PatternScriptableObject>();

        string path = UnityEditor.AssetDatabase.GetAssetPath(UnityEditor.Selection.activeObject);
        if (System.String.IsNullOrEmpty(path)) {
            path = "Assets";
        } else if (!System.String.IsNullOrEmpty(Path.GetExtension(path))) {
            path = Path.GetDirectoryName(path);
        }

        UnityEditor.AssetDatabase.CreateAsset(asset, path + "/patterns.asset");
        UnityEditor.AssetDatabase.SaveAssets();

        UnityEditor.EditorUtility.FocusProjectWindow();

        UnityEditor.Selection.activeObject = asset;
    }
#endif
}
