using UnityEngine;
using System.Collections;
using System.IO;
using FullInspector;

public class ShopScriptableObject : BaseScriptableObject<FullSerializerSerializer>
{
    /*[SetInEditor]*/
    public ShopSerializeData shop_data = null;

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Assets/Create/ShopData")]
    public static void CreateShopStateAsset()
    {
        var asset = BaseScriptableObject<FullSerializerSerializer>.CreateInstance<ShopScriptableObject>();

        string path = UnityEditor.AssetDatabase.GetAssetPath(UnityEditor.Selection.activeObject);
        if (System.String.IsNullOrEmpty(path)) {
            path = "Assets";
        } else if (!System.String.IsNullOrEmpty(Path.GetExtension(path))) {
            path = Path.GetDirectoryName(path);
        }

        UnityEditor.AssetDatabase.CreateAsset(asset, path + "/shdt.asset");
        UnityEditor.AssetDatabase.SaveAssets();

        UnityEditor.EditorUtility.FocusProjectWindow();
        UnityEditor.Selection.activeObject = asset;
    }

    public string filename = "shdt.txt";
    [InspectorButton]
    void SerializeToFile()
    {
        if (System.String.IsNullOrEmpty(filename))
            return;

        string path = Path.Combine(Application.persistentDataPath, filename);
        Debug.Log(string.Format("Saving to: {0}", path));
        string content = SerializationHelpers.SerializeToContent<ShopSerializeData, FullSerializerSerializer>(shop_data);
        File.WriteAllText(path, content);
    }
#endif
}