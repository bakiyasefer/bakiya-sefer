using UnityEngine;
using System.Collections;
using System.IO;
using FullInspector;

public class ObstacleScriptableObject : BaseScriptableObject<FullSerializerSerializer>
{
    public class ObstacleRefGroup : RefGroup<SimpleRefGroupItem>
    {
        /*[SetInEditor]*/
        public int size_override = -1;
    }

    /*[SetInEditor]*/
    [InspectorCollectionRotorzFlags(ShowIndices = true), InspectorCategory("Obstacles")]
    public PrefabPool[] obstacles;
    /*[SetInEditor]*/
    [InspectorCollectionRotorzFlags(ShowIndices = true), InspectorCategory("Groups")]
    public ObstacleRefGroup[] obstacle_groups = null;

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Assets/Create/Obstacles")]
    public static void CreateAsset()
    {
        var asset = BaseScriptableObject<FullSerializerSerializer>.CreateInstance<ObstacleScriptableObject>();

        string path = UnityEditor.AssetDatabase.GetAssetPath(UnityEditor.Selection.activeObject);
        if (System.String.IsNullOrEmpty(path)) {
            path = "Assets";
        } else if (!System.String.IsNullOrEmpty(Path.GetExtension(path))) {
            path = Path.GetDirectoryName(path);
        }

        UnityEditor.AssetDatabase.CreateAsset(asset, path + "/obstacles.asset");
        UnityEditor.AssetDatabase.SaveAssets();

        UnityEditor.EditorUtility.FocusProjectWindow();

        UnityEditor.Selection.activeObject = asset;
    }
#endif
}
