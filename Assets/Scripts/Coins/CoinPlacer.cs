using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using FullInspector;

public enum CoinPlaceFormMod
{
    NONE,
    TURN_RIGHT,
    TURN_LEFT,
    ARC
}
public enum CoinPlaceAlignment
{
    BEFORE,
    ON_TOP,
    BOTH
}
[System.Serializable]
public class CoinInitData
{
    public GameObject coin_prefab = null;
    public GameObject collect_particle_prefab = null;
    [System.NonSerialized, HideInInspector, InspectorNullable]
    public PrefabPool coin_pool = null;
    [System.NonSerialized, HideInInspector]
    public ParticleHolder collect_particle_holder = null;
}
[System.Serializable]
public class SuperCoinInitData : CoinInitData
{
    [InspectorRange(0f, 1f)]
    public float probability = 0f;
    public int coin_value = 0;
    public int predicate_value = 0;
}
public abstract class SuperCoinPlacer : IProbRandomItem
{
    public abstract void PlaceCoin(Vector3 offset, Transform parent);
    public abstract float ProbValue();
    public float ProbHalfSum { get; set; }
}
public class SimpleSuperCoinPlacer : SuperCoinPlacer
{
    public GameController.Event<Vector3, Transform> place_method = GameController.Stub;
    public GameController.GetValue<bool> prob_predicate = null;
    public float probability = 0f;

    public override float ProbValue()
    {
        return (prob_predicate == null || prob_predicate()) ? probability : 0f;
    }
    public override void PlaceCoin(Vector3 offset, Transform parent)
    {
        place_method(offset, parent);
    }
}