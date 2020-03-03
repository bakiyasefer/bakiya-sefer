using UnityEngine;

public class TouchTest : MonoBehaviour
{
    bool lastMoved = false;
    bool lastStationary = false;
    public bool logMoved = false;
    public bool logStationary = false;

    Vector3 pos;
    void Start()
    {
        pos = transform.localPosition;
    }
    void Update()
    {
        if (Input.touchCount > 0) {
            Touch touch = Input.GetTouch(0);
            switch (touch.phase) {
            case TouchPhase.Began:
                Debug.Log("Began");
                break;
            case TouchPhase.Moved:
                if (logMoved && !lastMoved) {
                    Debug.Log("Moved");
                    lastStationary = false;
                    lastMoved = true;
                }
                pos.x += touch.deltaPosition.x * 0.05f;
                transform.localPosition = pos;
                break;
            case TouchPhase.Stationary:
                if (logStationary && !lastStationary) {
                    Debug.Log("Stationary");
                    lastStationary = true;
                    lastMoved = false;
                }
                break;
            case TouchPhase.Ended:
                Debug.Log("Ended");
                pos.x = 0;
                transform.localPosition = pos;
                break;
            case TouchPhase.Canceled:
                Debug.Log("Canceled");
                pos.x = 0;
                transform.localPosition = pos;
                break;
            }
        } else {
            lastStationary = false;
            lastMoved = false;
        }
    }
}
