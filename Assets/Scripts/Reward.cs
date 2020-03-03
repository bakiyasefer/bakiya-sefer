using UnityEngine;
using FullInspector;

[System.Serializable]
public class UserReward
{
    /*[SetInEditor]*/
    public UserInvItemType type;
    /*[SetInEditor]*/
    public int amount = 10;
}
[System.Serializable]
public class RewardPack
{
    /*[SetInEditor]*/
    public UserReward[] rewards = null;
}
[System.Serializable]
public class RewardGroup
{
    public class Item : IProbRandomItem
    {
        /*[SetInEditor]*/
        public UserReward[] rewards = null;

        /*[SetInEditor]*/
        [InspectorRange(0f, 1f)]
        public float prob = 1.0f;
        public float ProbValue() { return prob; }
        [NotSerialized, HideInInspector]
        public float ProbHalfSum { get; set; }
    }
    /*[SetInEditor]*/
    public Item[] items = null;

    public int RandomIndex()
    {
        return ProbRandom.UpdateAndGet(items);
    }
}
[System.Serializable]
public class RewardSerializeData
{
    /*[SetInEditor]*/
    public int id = 0;
    /*[SetInEditor]*/
    public RewardGroup mystery_box = null;
    /*[SetInEditor]*/
    public RewardGroup super_mystery_box = null;
    /*[SetInEditor]*/
    [InspectorCollectionRotorzFlags(ShowIndices = true)]
    public RewardPack[] chprog = null;
    /*[SetInEditor]*/
    [InspectorCollectionRotorzFlags(ShowIndices = true)]
    public RewardGroup chday = null;
    /*[SetInEditor]*/
    [InspectorCollectionRotorzFlags(ShowIndices = true)]
    public RewardGroup chspec = null;
    /*[SetInEditor]*/
    [InspectorCollectionRotorzFlags(ShowIndices = true)]
    public RewardGroup daily_vid = null;
    /*[SetInEditor]*/
    public int daily_vid_luck = 0;
    /*[SetInEditor]*/
    public RewardPack signup = null;
    /*[SetInEditor]*/
    public int new_coins_value = 0;
    /*[SetInEditor]*/
    public int new_luck_value = 0;
}