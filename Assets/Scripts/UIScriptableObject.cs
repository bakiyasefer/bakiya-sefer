using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FullInspector;

public class UIScriptableObject : BaseScriptableObject<FullSerializerSerializer>
{
    /*[SetInEditor]*/
    public GameObject shop_chartheme_scrollitem_prefab = null;
    /*[SetInEditor]*/
    public GameObject shop_chartheme_scrollitem_selected_prefab = null;
    /*[SetInEditor]*/
    public GameObject shop_buyitem_element_prefab = null;
    /*[SetInEditor]*/
    public GameObject shop_buylevel_element_prefab = null;
    /*[SetInEditor]*/
    public GameObject shop_buychs_element_prefab = null;
    /*[SetInEditor]*/
    public GameObject shop_buyvid_element_prefab = null;
    /*[SetInEditor]*/
    public GameObject shop_confirm_prefab = null;
    /*[SetInEditor]*/
    public Sprite shop_buyitemlevel_element_collapsed_img = null;
    /*[SetInEditor]*/
    public Sprite shop_buyitemlevel_element_expanded_img = null;
    /*[SetInEditor]*/
    public Sprite shop_level_icon_empty = null;
    /*[SetInEditor]*/
    public Sprite shop_level_icon_full = null;

    /*[SetInEditor]*/
    public GameObject shop_char_question_prefab = null;
    public Sprite shop_char_question_icon = null;
    public string shop_char_question_name = string.Empty;
    public string shop_char_question_desc = string.Empty;
    /*[SetInEditor]*/
    public GameObject shop_theme_question_prefab = null;
    public Sprite shop_theme_question_icon = null;
    public string shop_theme_question_name = string.Empty;
    public string shop_theme_question_desc = string.Empty;

    /*[SetInEditor]*/
    public GameObject chs_task_element_prefab = null;
    /*[SetInEditor]*/
    public GameObject chs_letters_main_prefab = null;
    /*[SetInEditor]*/
    public GameObject chs_letters_note_prefab = null;
    /*[SetInEditor]*/
    public Sprite chs_work_icon = null;
    /*[SetInEditor]*/
    public Sprite chs_green_icon = null;
    /*[SetInEditor]*/
    public string chs_prog_top_text = string.Empty;
    public string chs_prog_note_text = string.Empty;
    /*[SetInEditor]*/
    public string chs_rand_top_text = string.Empty;
    public string chs_rand_note_text = string.Empty;
    /*[SetInEditor]*/
    public string chs_day_top_text = string.Empty;
    public string chs_day_note_text = string.Empty;
    /*[SetInEditor]*/
    public string chs_spec_top_text = string.Empty;
    public string chs_spec_note_text = string.Empty;
    /*[SetInEditor]*/
    public Dictionary<string, string> chs_letter_replace = null;
    /*[SetInEditor]*/
    public Dictionary<UserInvItemType, UserItemDesc> user_item_desc = null;
    /*[SetInEditor]*/
    public Dictionary<PlaycharLevelType, UserItemDesc> playchar_level_desc = null;
    /*[SetInEditor]*/
    public Dictionary<UserInvItemType, GameObject> reward_item_prefabs = null;

    /*[SetInEditor]*/
    public Sprite fb_not_logged_in_icon = null;
    /*[SetInEditor]*/
    public Sprite fb_logged_in_icon = null;
    /*[SetInEditor]*/
    public string fb_feed_link_caption = string.Empty;
    /*[SetInEditor]*/
    public string fb_feed_link_name = string.Empty;
    /*[SetInEditor]*/
    public GameObject fb_stat_user_icon_prefab = null;
    /*[SetInEditor]*/
    public GameObject fb_friend_element_prefab = null;
    /*[SetInEditor]*/
    public Texture2D fb_friend_default_texture = null;
    /*[SetInEditor]*/
    public string fb_toppanel_login_text = string.Empty;
    /*[SetInEditor]*/
    public string fb_toppanel_logout_text = string.Empty;
    /*[SetInEditor]*/
    public string fb_toppanel_stat_text = string.Empty;
    /*[SetInEditor]*/
    public string fb_toppanel_top_text = string.Empty;
    /*[SetInEditor]*/
    public string note_login_greeting = string.Empty;
    /*[SetInEditor]*/
    public string note_offline_greeting = string.Empty;
    /*[SetInEditor]*/
    public string note_offline = string.Empty;
    /*[SetInEditor]*/
    public GameObject ui_tutorial_prefab = null;
    /*[SetInEditor]*/
    public GameObject tutorial_prefab = null;
    /*[SetInEditor]*/
    public GameObject console_prefab = null;
#if UNITY_EDITOR
    [UnityEditor.MenuItem("Assets/Create/UI")]
    public static void CreateAsset()
    {
        var asset = BaseScriptableObject<FullSerializerSerializer>.CreateInstance<UIScriptableObject>();

        string path = UnityEditor.AssetDatabase.GetAssetPath(UnityEditor.Selection.activeObject);
        if (System.String.IsNullOrEmpty(path)) {
            path = "Assets";
        } else if (!System.String.IsNullOrEmpty(Path.GetExtension(path))) {
            path = Path.GetDirectoryName(path);
        }

        UnityEditor.AssetDatabase.CreateAsset(asset, path + "/ui.asset");
        UnityEditor.AssetDatabase.SaveAssets();

        UnityEditor.EditorUtility.FocusProjectWindow();

        UnityEditor.Selection.activeObject = asset;
    }
#endif
}
