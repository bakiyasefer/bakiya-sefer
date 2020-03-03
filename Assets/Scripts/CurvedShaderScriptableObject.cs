using UnityEngine;
using System.Collections;
using System.IO;
using FullInspector;

public class CurvedShaderScriptableObject : BaseScriptableObject<FullSerializerSerializer>
{
    /*[SetInEditor]*/
    [InspectorRange(0f, 400f)]
    public float max_x = 100f;
    /*[SetInEditor]*/
    [InspectorRange(-1f, 100f)]
    public float x_time = 5f;
    /*[SetInEditor]*/
    [InspectorRange(1f, 100f)]
    public float x_wait = 5f;
    public Material[] main_materials;
    /*[SetInEditor]*/
    public Vector4 z_offset = Vector4.zero;
    public Vector4 sphere_offset = Vector4.zero;

    [InspectorButton]
    public void SetSphereValues()
    {
        for (int i = 0; i < main_materials.Length; ++i) {
            main_materials[i].SetVector("_Sphere", sphere_offset);
        }
    }
    [InspectorButton]
    public void SwitchToSphereScreen()
    {
        Shader spherical = Shader.Find("Custom/Spherical_Screen");
        if(spherical == null) return;

        for (int i = 0; i < main_materials.Length; ++i) {
            main_materials[i].shader = spherical;
        }
    }
    [InspectorButton]
    public void SwitchToSphereWorld()
    {
        Shader spherical = Shader.Find("Custom/Spherical_World");
        if (spherical == null) return;

        for (int i = 0; i < main_materials.Length; ++i) {
            main_materials[i].shader = spherical;
        }
    }
    [InspectorButton]
    public void SwitchToCurved()
    {
        Shader curved = Shader.Find("Custom/Curved");
        if(curved == null) return;

        for (int i = 0; i < main_materials.Length; ++i) {
            main_materials[i].shader = curved;
        }
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Assets/Create/CurvedShader")]
    public static void CreateAsset()
    {
        var asset = BaseScriptableObject<FullSerializerSerializer>.CreateInstance<CurvedShaderScriptableObject>();

        string path = UnityEditor.AssetDatabase.GetAssetPath(UnityEditor.Selection.activeObject);
        if (System.String.IsNullOrEmpty(path)) {
            path = "Assets";
        } else if (!System.String.IsNullOrEmpty(Path.GetExtension(path))) {
            path = Path.GetDirectoryName(path);
        }

        UnityEditor.AssetDatabase.CreateAsset(asset, path + "/cshader.asset");
        UnityEditor.AssetDatabase.SaveAssets();

        UnityEditor.EditorUtility.FocusProjectWindow();

        UnityEditor.Selection.activeObject = asset;
    }
#endif
}
