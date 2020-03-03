using UnityEngine;
using System.Collections;

public class TutorialPlaycharController : MonoBehaviour
{
    GameController.Event<Collider> trigger_enter = null;
    public void SetTriggerEnterEvent(GameController.Event<Collider> func)
    {
        trigger_enter = func;
    }
    void OnTriggerEnter(Collider coll)
    {
        if (trigger_enter != null) { trigger_enter(coll); }
    }
}
