using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using FullInspector;


public enum UserInvItemType
{
    //User Items
    LUCK,
    COINS,
    LUCK_X,
    SCORE_X,
    COINS_X,
    MBOX,
    SMBOX,

    //Playchar Items
    DROPS,
}
public class UserItemDesc
{
    public Sprite icon = null;
    public string name = string.Empty;
    public string desc = string.Empty;
}
public enum PlaycharLevelType
{
    MAG_POWER,
    MAG_TIME,
    COINSX_TIME,
    SCOREX_TIME,
    LUCKX_TIME,
    DROP_POWER,
    STAMINA_DECTIME
}
[System.Serializable]
public class PlaycharLevel
{
    public const int MAGNET_POWER_MAXLV = 3;
    public const int MAGNET_TIME_MAXLV = 3;
    public const int SCOREX_TIME_MAXLV = 3;
    public const int COINSX_TIME_MAXLV = 3;
    public const int LUCKX_TIME_MAXLV = 3;
    public const int DROP_POWER_MAXLV = 2;
    public const int STAMINA_DECTIME_MAXLV = 3;
    public static int MaxLevelFor(PlaycharLevelType type)
    {
        switch (type) {
        case PlaycharLevelType.MAG_POWER: return MAGNET_POWER_MAXLV;
        case PlaycharLevelType.MAG_TIME: return MAGNET_TIME_MAXLV;
        case PlaycharLevelType.SCOREX_TIME: return SCOREX_TIME_MAXLV;
        case PlaycharLevelType.COINSX_TIME: return COINSX_TIME_MAXLV;
        case PlaycharLevelType.LUCKX_TIME: return LUCKX_TIME_MAXLV;
        case PlaycharLevelType.DROP_POWER: return DROP_POWER_MAXLV;
        case PlaycharLevelType.STAMINA_DECTIME: return STAMINA_DECTIME_MAXLV;
        default: return 0;
        }
    }
    public static bool MagnetPullsCoin(CoinType type, int magnetPower)
    {
        switch (type) {
        case CoinType.SMALL:
            return true;
        //first level
        case CoinType.MEDIUM:
            return magnetPower > 0;
        //second level
        case CoinType.STAMINA:
        case CoinType.DROP:
            return magnetPower > 1;
        case CoinType.SCOREX:
        case CoinType.COINSX:
        case CoinType.LUCKX:
        case CoinType.LUCK:
            return magnetPower > 2;
        //max level
        case CoinType.MBOX:
        case CoinType.SMBOX:
        case CoinType.SYMBOL:
        case CoinType.LETTER:
        case CoinType.MAGNET:
        default: return false;
        }
    }

    /*[SetInEditor]*/
    public int magnet_power = 0;
    /*[SetInEditor]*/
    public int magnet_time = 0;
    /*[SetInEditor]*/
    public int scorex_time = 0;
    /*[SetInEditor]*/
    public int coinsx_time = 0;
    /*[SetInEditor]*/
    public int luckx_time = 0;
    /*[SetInEditor]*/
    public int drop_power = 0;
    /*[SetInEditor]*/
    public int stamina_dectime = 0;

    public int LevelFor(PlaycharLevelType type)
    {
        switch (type) {
        case PlaycharLevelType.MAG_POWER: return magnet_power;
        case PlaycharLevelType.MAG_TIME: return magnet_time;
        case PlaycharLevelType.SCOREX_TIME: return scorex_time;
        case PlaycharLevelType.COINSX_TIME: return coinsx_time;
        case PlaycharLevelType.LUCKX_TIME: return luckx_time;
        case PlaycharLevelType.DROP_POWER: return drop_power;
        case PlaycharLevelType.STAMINA_DECTIME: return stamina_dectime;
        default: return 0;
        }
    }
    public void AddLevelFor(PlaycharLevelType type)
    {
        switch (type) {
        case PlaycharLevelType.MAG_POWER:
            if (magnet_power < MAGNET_POWER_MAXLV) { ++magnet_power; }
            break;
        case PlaycharLevelType.MAG_TIME:
            if (magnet_time < MAGNET_TIME_MAXLV) { ++magnet_time; }
            break;
        case PlaycharLevelType.SCOREX_TIME:
            if (scorex_time < SCOREX_TIME_MAXLV) { ++scorex_time; }
            break;
        case PlaycharLevelType.COINSX_TIME:
            if (coinsx_time < COINSX_TIME_MAXLV) { ++coinsx_time; }
            break;
        case PlaycharLevelType.LUCKX_TIME:
            if (luckx_time < LUCKX_TIME_MAXLV) { ++luckx_time; }
            break;
        case PlaycharLevelType.DROP_POWER:
            if (drop_power < DROP_POWER_MAXLV) { ++drop_power; }
            break;
        case PlaycharLevelType.STAMINA_DECTIME:
            if (stamina_dectime < STAMINA_DECTIME_MAXLV) { ++stamina_dectime; }
            break;
        }
    }
}
[System.Serializable]
public class UserState
{
    public System.DateTime timestamp;
    public string name = string.Empty;
    //inventory
    public int luck = 0;
    public int coins = 0;
    public int luck_x = 0;
    public int score_x = 0;
    public int coins_x = 0;
    public int[] drops = null;
    public int[] avail_playchars = null;
    public int[] avail_themeslots = null;
    //selection
    public int playchar_slot_index = 0;
    public int playchar_index_in_slot = 0;
    public int theme_slot_index = 0;
    //results
    public int highscore = 0;
    //levels
    public PlaycharLevel[] levels = null;

    //chprog
    public int chprog_id = -1;
    public int chprog_prog_index = 0;
    public int chprog_random_index = 0;
    public int chprog_reward_index = 0;
    public object chprog_state = null;
    //chday
    public int chday_id = -1;
    public int chday_random_index = 0;
    public int chday_serie_count = 0;
    public bool chday_done = false;
    public int chday_reward_index = 0;
    public object chday_state = null;
    public const float CHDAY_EXPIRE_HOURS = 24f;
    public System.DateTime chday_expire;
    //chspec
    public int chspec_id = -1;
    public int chspec_random_index = 0;
    public int chspec_serie_count = 0;
    public bool chspec_done = false;
    public int chspec_reward_index = 0;
    public object chspec_state = null;
    public const float CHSPEC_EXPIRE_HOURS = 72f;
    public System.DateTime chspec_expire;
    

    public bool IsPlaycharAvailable(int playcharIndex)
    {
        return avail_playchars[playcharIndex] == 1;
    }
    public void SetPlaycharAvailable(int playcharIndex)
    {
        avail_playchars[playcharIndex] = 1;
    }
    public bool IsThemeSlotAvailable(int themeSlotIndex)
    {
        return avail_themeslots[themeSlotIndex] == 1;
    }
    public void SetThemeSlotAvailable(int themeSlotIndex)
    {
        avail_themeslots[themeSlotIndex] = 1;
    }
}