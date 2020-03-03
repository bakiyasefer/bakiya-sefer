using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FullInspector;

public class TestSerialize : MonoBehaviour
{
    Dictionary<string, object> data = null;
    string path = string.Empty;
    void Start()
    {
        path = Path.Combine(Application.persistentDataPath, "data.txt");
        Debug.Log(path);
        data = new Dictionary<string, object>() {
            {"key1", 20},
            {"key2", "value2"},
            {"key3", true},
            {"key4", new[] {1, 2, 2.2, 4, 5}}
        };
    }
    void OnGUI()
    {
        if (GUILayout.Button("Write")) {
            Serialize();
        }
        if (GUILayout.Button("Read")) {
            Deserialize();
        }
    }
    public void Serialize()
    {
        //string content = SerializationHelpers.SerializeToContent<Dictionary<string, string>, FullSerializerSerializer>(data);
        string content = Facebook.MiniJSON.Json.Serialize(data);
        File.WriteAllText(path, content);
    }
    public void Deserialize()
    {
        if (File.Exists(path)) {
            try {
                string content = File.ReadAllText(path);
                //data = SerializationHelpers.DeserializeFromContent<Dictionary<string, string>, FullSerializerSerializer>(content);
                data = Facebook.MiniJSON.Json.Deserialize(content) as Dictionary<string, object>;
                object value;
                data.TryGetValue("key1", out value);
                Debug.Log(string.Format("key1: {0}", value));
            }
            catch (System.Exception e) {
                Debug.LogError(e.ToString());
            }
        }
    }
}
