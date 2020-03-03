using UnityEngine;
using System.Collections.Generic;
using System.IO;
using FullInspector;

public class LocalizeScriptableObject : BaseScriptableObject<FullSerializerSerializer>
{
    public int[] langs_order = null;
    [InspectorCollectionPager(AlwaysShow = true)]
    public Dictionary<string, string[]> localize_data = null;

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Assets/Create/Localize")]
    public static void CreateAsset()
    {
        var asset = BaseScriptableObject<FullSerializerSerializer>.CreateInstance<LocalizeScriptableObject>();

        string path = UnityEditor.AssetDatabase.GetAssetPath(UnityEditor.Selection.activeObject);
        if (System.String.IsNullOrEmpty(path)) {
            path = "Assets";
        } else if (!System.String.IsNullOrEmpty(Path.GetExtension(path))) {
            path = Path.GetDirectoryName(path);
        }

        UnityEditor.AssetDatabase.CreateAsset(asset, path + "/localize.asset");
        UnityEditor.AssetDatabase.SaveAssets();

        UnityEditor.EditorUtility.FocusProjectWindow();

        UnityEditor.Selection.activeObject = asset;
    }
#endif
}
