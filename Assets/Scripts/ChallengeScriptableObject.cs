using UnityEngine;
using System.Collections;
using System.IO;
using FullInspector;

public class ChallengeScriptableObject : BaseScriptableObject<FullSerializerSerializer>
{
    /*[SetInEditor]*/
    public ChallengeSerializeData ch = null;

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Assets/Create/Challenge")]
    public static void CreateChallengeStateAsset()
    {
        var asset = BaseScriptableObject<FullSerializerSerializer>.CreateInstance<ChallengeScriptableObject>();

        string path = UnityEditor.AssetDatabase.GetAssetPath(UnityEditor.Selection.activeObject);
        if (System.String.IsNullOrEmpty(path)) {
            path = "Assets";
        } else if (!System.String.IsNullOrEmpty(Path.GetExtension(path))) {
            path = Path.GetDirectoryName(path);
        }

        UnityEditor.AssetDatabase.CreateAsset(asset, path + "/ch.asset");
        UnityEditor.AssetDatabase.SaveAssets();

        UnityEditor.EditorUtility.FocusProjectWindow();

        UnityEditor.Selection.activeObject = asset;
    }

    public string filename = "ch.txt";
    [InspectorButton]
    void SerializeToFile()
    {
        if (System.String.IsNullOrEmpty(filename))
            return;

        string path = Path.Combine(Application.persistentDataPath, filename);
        Debug.Log(string.Format("Saving to: {0}", path));
        string content = SerializationHelpers.SerializeToContent<ChallengeSerializeData, FullSerializerSerializer>(ch);
        File.WriteAllText(path, content);
    }
#endif
}
