using UnityEngine;
using System.Collections;

public class SwipeTest : MonoBehaviour
{
    const float SWIPE_THRESHOLD = 5.0f;
    public enum SwipeDir { Tap, Left, Right, Up, Down }
    public static SwipeDir GetSwipeDirection(Vector2 diff_pos)
    {
        Vector2 diff_abs = diff_pos;
        diff_abs.x = System.Math.Abs(diff_pos.x);
        diff_abs.y = System.Math.Abs(diff_pos.y);
        SwipeDir current_dir = SwipeDir.Tap;

        if (diff_pos.x > SWIPE_THRESHOLD) { //Right half
            if (diff_pos.y > SWIPE_THRESHOLD) { //RightUpper half
                current_dir = (diff_abs.x > diff_abs.y) ? SwipeDir.Right : SwipeDir.Up;
            } else if (diff_pos.y < -SWIPE_THRESHOLD) { //RightLower half
                current_dir = (diff_abs.x > diff_abs.y) ? SwipeDir.Right : SwipeDir.Down;
            } else current_dir = SwipeDir.Right;
        } else if (diff_pos.x < -SWIPE_THRESHOLD) { //Left half
            if (diff_pos.y > SWIPE_THRESHOLD) { //LeftUpper half
                current_dir = (diff_abs.x > diff_abs.y) ? SwipeDir.Left : SwipeDir.Up;
            } else if (diff_pos.y < -SWIPE_THRESHOLD) { //LeftLower half
                current_dir = (diff_abs.x > diff_abs.y) ? SwipeDir.Left : SwipeDir.Down;
            } else current_dir = SwipeDir.Left;
        } else {
            if (diff_pos.y > SWIPE_THRESHOLD) current_dir = SwipeDir.Up;
            else if (diff_pos.y < -SWIPE_THRESHOLD) current_dir = SwipeDir.Down;
        }
        return current_dir;
    }

    UnityEngine.UI.Text swipe_txt = null;
    GameObject move_go = null;
    GameObject end_go = null;

    bool gesture_stationary = false;

    void Start()
    {
        Transform canvas = GameObject.Find("Canvas").transform;
        swipe_txt = canvas.Find("Swipe").GetChild(0).GetComponent<UnityEngine.UI.Text>();
        move_go = canvas.Find("Move").GetChild(0).gameObject;
        end_go = canvas.Find("End").GetChild(0).gameObject;
    }

    void Update()
    {
        if (Input.touchCount > 0) {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began) {
                move_go.SetActive(false);
                end_go.SetActive(false);
                gesture_stationary = true;
            } else if (gesture_stationary && touch.phase == TouchPhase.Moved) {
                SwipeDir dir = GetSwipeDirection(touch.deltaPosition);
                switch (dir) {
                case SwipeDir.Left:
                    gesture_stationary = false;
                    move_go.SetActive(true);
                    break;
                case SwipeDir.Right:
                    gesture_stationary = false;
                    move_go.SetActive(true);
                    break;
                case SwipeDir.Up:
                    gesture_stationary = false;
                    move_go.SetActive(true);
                    break;
                case SwipeDir.Down:
                    gesture_stationary = false;
                    move_go.SetActive(true);
                    break;
                }
                swipe_txt.text = dir.ToString();
            } else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) {
                end_go.SetActive(true);
                /*if (gesture_stationary) {
                    
                }*/
            }
        }
    }
}
