using UnityEngine;
using System.Collections;

public class TestTextureAnimation : MonoBehaviour
{
    private float iX = 0;
    private float iY = 1;
    public int _uvTileX = 1;
    public int _uvTileY = 1;
    public int _fps = 10;
    private Vector2 _size;
    private Renderer _myRenderer;
    private int _lastIndex = -1;

    void Start()
    {
        _size = new Vector2(1.0f / _uvTileX, 1.0f / _uvTileY);
        _myRenderer = GetComponent<Renderer>();
        if (_myRenderer == null) enabled = false;
        _myRenderer.material.SetTextureScale("_MainTex", _size);
    }
    void Update()
    {
        int index = (int)(Time.timeSinceLevelLoad * _fps) % (_uvTileX * _uvTileY);
        if (index != _lastIndex) {
            Vector2 offset = new Vector2(iX * _size.x, 1 - (_size.y * iY));
            ++iX;
            if (iX / _uvTileX == 1) {
                if (_uvTileY != 1) ++iY;
                iX = 0;
                if (iY / _uvTileY == 1) iY = 1;
            }
            _myRenderer.material.SetTextureOffset("_MainTex", offset);
            _lastIndex = index;
        }
    }
}
