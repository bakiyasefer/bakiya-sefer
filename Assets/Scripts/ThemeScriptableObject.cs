using UnityEngine;
using System.Collections;
using System.IO;
using FullInspector;

public class ThemeScriptableObject : BaseScriptableObject<FullSerializerSerializer>
{
    /*[SetInEditor]*/
    [InspectorCollectionRotorzFlags(ShowIndices = true)]
    public ThemeSlot[] theme_slots;
    /*[SetInEditor]*/
    [InspectorDatabaseEditor]
    public Theme[] themes;
#if UNITY_EDITOR
    [UnityEditor.MenuItem("Assets/Create/Themes")]
    public static void CreateAsset()
    {
        var asset = BaseScriptableObject<FullSerializerSerializer>.CreateInstance<ThemeScriptableObject>();

        string path = UnityEditor.AssetDatabase.GetAssetPath(UnityEditor.Selection.activeObject);
        if (System.String.IsNullOrEmpty(path)) {
            path = "Assets";
        } else if (!System.String.IsNullOrEmpty(Path.GetExtension(path))) {
            path = Path.GetDirectoryName(path);
        }

        UnityEditor.AssetDatabase.CreateAsset(asset, path + "/themes.asset");
        UnityEditor.AssetDatabase.SaveAssets();

        UnityEditor.EditorUtility.FocusProjectWindow();

        UnityEditor.Selection.activeObject = asset;
    }
#endif
}
