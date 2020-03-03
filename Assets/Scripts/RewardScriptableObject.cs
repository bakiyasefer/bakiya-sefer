using UnityEngine;
using System.Collections;
using System.IO;
using FullInspector;

public class RewardScriptableObject : BaseScriptableObject<FullSerializerSerializer>
{
    /*[SetInEditor]*/
    public RewardSerializeData reward = null;
#if UNITY_EDITOR
    [UnityEditor.MenuItem("Assets/Create/Reward")]
    public static void CreateChallengeStateAsset()
    {
        var asset = BaseScriptableObject<FullSerializerSerializer>.CreateInstance<RewardScriptableObject>();

        string path = UnityEditor.AssetDatabase.GetAssetPath(UnityEditor.Selection.activeObject);
        if (System.String.IsNullOrEmpty(path)) {
            path = "Assets";
        } else if (!System.String.IsNullOrEmpty(Path.GetExtension(path))) {
            path = Path.GetDirectoryName(path);
        }

        UnityEditor.AssetDatabase.CreateAsset(asset, path + "/reward.asset");
        UnityEditor.AssetDatabase.SaveAssets();

        UnityEditor.EditorUtility.FocusProjectWindow();

        UnityEditor.Selection.activeObject = asset;
    }

    public string filename = "reward.txt";
    [InspectorButton]
    void SerializeToFile()
    {
        if (System.String.IsNullOrEmpty(filename))
            return;

        string path = Path.Combine(Application.persistentDataPath, filename);
        Debug.Log(string.Format("Saving to: {0}", path));
        string content = SerializationHelpers.SerializeToContent<RewardSerializeData, FullSerializerSerializer>(reward);
        File.WriteAllText(path, content);
    }
#endif
}
