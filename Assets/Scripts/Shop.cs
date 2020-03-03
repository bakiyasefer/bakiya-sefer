using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FullInspector;

public enum CurrencyType { LUCK, COINS }
[System.Serializable]
public class PriceData
{
    /*[SetInEditor]*/
    public CurrencyType type = CurrencyType.COINS;
    /*[SetInEditor]*/
    public int value = 0;
}
[System.Serializable]
public class BuyItemData
{
    /*[SetInEditor]*/
    public int item_threshold = 0;
    /*[SetInEditor]*/
    public int item_value = 0;
    /*[SetInEditor]*/
    public PriceData price = null;
}
[System.Serializable]
public class BuyElementData<EnumType>
{
    public EnumType type;
    public int limit_threshold = 0;
    public BuyItemData[] items = null;
}
[System.Serializable]
public class ShopSerializeData
{
    /*[SetInEditor]*/
    public int id = 0;
    /*[SetInEditor]*/
    public BuyElementData<UserInvItemType>[] items = null;
    /*[SetInEditor]*/
    public BuyElementData<PlaycharLevelType>[] levels = null;
    /*[SetInEditor]*/
    public PriceData[] playchars = null;
    /*[SetInEditor]*/
    public PriceData[] themes = null;
    /*[SetInEditor]*/
    public PriceData chs = null;
}