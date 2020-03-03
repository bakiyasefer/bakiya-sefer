using UnityEngine;
using System.Collections;
using FullInspector;

public class BoundsExtend : BaseBehavior<FullSerializerSerializer>
{
    [InspectorButton]
    void Recalculate()
    {
        var filters = gameObject.GetComponentsInChildren<MeshFilter>();
        foreach (var mf in filters) {
            mf.sharedMesh.RecalculateBounds();
        }
    }
    [InspectorButton]
    void Extend()
    {
        var filters = gameObject.GetComponentsInChildren<MeshFilter>();
        foreach (var mf in filters) {
            var mesh = mf.sharedMesh;
            if (mesh == null) continue;
            var bounds = mesh.bounds;
            bounds.center = Vector3.zero;
            bounds.extents = new Vector3(100, 100, 100);
            mesh.bounds = bounds;
        }
    }
    void Start()
    {
        Extend();
    }
}
