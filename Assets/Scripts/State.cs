using System.Collections;
using System.Collections.Generic;

public interface IState
{
    void Enter();
    void SetActive(bool active);
    bool Update();
    void Leave();
}