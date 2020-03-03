using UnityEngine;
using FullInspector;

public class Singleton<T> : BaseBehavior<FullSerializerSerializer> where T : MonoBehaviour
{
    private static T _instance;
	private static object _lock = new object();
    public static T Instance
    {
        get
        {
            if (applicationIsQuitting) {
#if UNITY_EDITOR
                Debug.LogWarning("Instance request after OnDestroy");
#endif
                return null;
            }
            if (_instance == null) {
                lock (_lock) {
                    _instance = (T)FindObjectOfType(typeof(T));
                    if (FindObjectsOfType(typeof(T)).Length > 1) {
#if UNITY_EDITOR
                        Debug.LogError("Multiple Singletons Detected");
#endif
                    }
                    if (_instance == null) {
                        GameObject singleton = new GameObject();
                        _instance = singleton.AddComponent<T>();
                        singleton.name = typeof(T).ToString() + "_singl";

                        DontDestroyOnLoad(singleton);
#if UNITY_EDITOR
                        Debug.Log("Singleton: " + singleton.name + " created");
#endif
                    }

                }//lock

            }//if null
            return _instance;
        }
    }

    private static bool applicationIsQuitting = false;

    void OnDestroy()
    {
        applicationIsQuitting = true;
    }
}