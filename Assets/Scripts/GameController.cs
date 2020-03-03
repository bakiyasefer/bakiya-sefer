//#define PHPDEBUG
//#define PHPLOCAL
#if CODEDEBUG
//#define DBG_TRACE_ROUTINES
//#define DBG_TRACE_PATTERNS
#endif

using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Advertisements;
using FullInspector;
using Facebook.MiniJSON;
using Facebook.Unity;
using DarkTonic.MasterAudio;
using BestHTTP;

public struct Pair<T1, T2>
{
    public T1 v1;
    public T2 v2;

    public Pair(T1 t1, T2 t2)
    {
        v1 = t1;
        v2 = t2;
    }
}
public interface IProbRandomItem
{
    float ProbValue();
    float ProbHalfSum { get; set; }
}
public class ProbRandom
{
    public static int Get<T>(IList<T> items) where T : IProbRandomItem
    {
        float sample = Random.value * items[items.Count - 1].ProbHalfSum;
        for (int i = 0, l = items.Count; i < l; ++i) {
            if (sample < items[i].ProbHalfSum) return i;
        }
        return 0;
    }
    public static void Update<T>(IList<T> items) where T : IProbRandomItem
    {
        IProbRandomItem item = items[0];
        item.ProbHalfSum = item.ProbValue();
        for (int i = 1, l = items.Count; i < l; ++i) {
            IProbRandomItem next_item = items[i];
            next_item.ProbHalfSum = item.ProbHalfSum + next_item.ProbValue();
            item = next_item;
        }
    }
    public static int UpdateAndGet<T>(IList<T> items) where T : IProbRandomItem
    {
        Update(items);
        return Get(items);
    }
}
#region [POOL]
public class PoolSharedData
{
    /*[SetInEditor]*/
    [InspectorStepRange(0, 45f, 1f)]
    public int size_in_cells = (int)GameController.CELL_SYNC_SIZE;
}
public class PrefabPool : IProbRandomItem
{
    /*[SetInEditor]*/
    public string name = string.Empty;
    /*[SetInEditor]*/
    public GameObject[] target_prefabs = null;
    /*[SetInEditor]*/
    public int num_objects = 0;

    /*[SetInEditor]*/
    [InspectorRange(0f, 1f)]
    public float prob = 1.0f;
    public float ProbValue() { return prob; }
    [NotSerialized, HideInInspector]
    public float ProbHalfSum { get; set; }

    /*[SetInEditor]*/
    public PoolSharedData shared_data = null;
    public T GetSharedData<T>() where T : PoolSharedData { return shared_data as T; }
    public int SizeInCells() { return shared_data.size_in_cells; }

    //instantiated game_objects
    Transform[] gos = null;
    //PoolObject script instances on game_objects
    IPoolObject[] pool_objects = null;
    public IPoolObject PoolObjectAt(int index) { return pool_objects[index]; }
    int index_cursor = 0;
    public int LastUsedIndex() { return index_cursor; }

    Transform parent_node = null;

    bool[] go_available = null;

    GameController gc = null;

#if CODEDEBUG
    string debug_data = string.Empty;
#endif

    public void Init(
#if CODEDEBUG
        string debugData,
#endif
        GameController.GetValue<GameObject, int> object_factory = null)
    {
        gc = GameController.Instance;
#if CODEDEBUG
        debug_data = debugData;
        string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
#endif
        if (num_objects < 1) {
            /*
            #if CODEDEBUG
                        GameController.LogWarning(METHOD_NAME, (debug_data + " reserved"));
            #endif*/
            return;
        }
#if CODEDEBUG
        if (object_factory == null && (target_prefabs == null || target_prefabs.Length == 0)) {
            GameController.LogError(METHOD_NAME, (debug_data + ".tarfet_prefabs is NULL"));
            return;
        }
#endif
        if (parent_node == null) {
#if CODEDEBUG
            var parent_go = new GameObject(debug_data);
            parent_go.SetActive(false);
            parent_node = parent_go.transform;
            parent_node.SetParent(gc.HiddenNode(), false);
#else
            parent_node = gc.HiddenNode();
#endif
        }

        if (gos == null) {
            go_available = new bool[num_objects];
            gos = new Transform[num_objects];
            pool_objects = new IPoolObject[num_objects];
            for (int i = 0; i < num_objects; ++i) {
                GameObject go = object_factory == null ? Object.Instantiate(target_prefabs[i % target_prefabs.Length]) : object_factory(i);
                gos[i] = go.transform;
                IPoolObject po = gos[i].GetComponent(typeof(IPoolObject)) as IPoolObject;
                if (po == null) po = go.AddComponent<PoolObject>();
                po.Pool_Init(this, i, shared_data
#if CODEDEBUG
                    , string.Format("{0}.pool_objects[{1}]", debug_data, i)
#endif
                    );
                pool_objects[i] = po;

                gos[i].SetParent(parent_node);
                gos[i].localPosition = GameController.POSITION_ZERO;
                go_available[i] = true;
            }
        } else {
            ReleaseAll();
        }
    }
    public static void PlaceCustom(Vector3 offset, float y_offset, Transform placeObject)
    {
        offset.y += y_offset;
        Transform parent = GameController.Instance.AttachNodeRoot();
        placeObject.SetParent(parent, false);
        placeObject.localPosition = offset - parent.localPosition;
    }
    public int PlaceAsThemeElement(Vector3 offset, bool mirror = false)
    {
        if (++index_cursor >= num_objects) index_cursor = 0;
#if CODEDEBUG
        if (!go_available[index_cursor]) {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogWarning(METHOD_NAME, (debug_data + ".gos[{0}] Not available"), index_cursor);
        }
#endif
        Transform attach_parent = gc.AttachNodeTheme();
        Transform offset_parent = gc.AttachNodeRoot();

        go_available[index_cursor] = false;
        Transform tr = gos[index_cursor];
        tr.SetParent(attach_parent, false);
        tr.localPosition = offset - offset_parent.localPosition;
        tr.localScale = GameController.SCALE_X[mirror ? 1 : 0];

        pool_objects[index_cursor].Pool_Placed();
        return shared_data.size_in_cells;
    }
    public void PlaceAsPatternElement(Vector3 offset, float y_offset, PatternBox.Element el, int lane_index)
    {
        //correct offset
        offset.z += el.num_cells_before * GameController.CELL_DEPTH;
        offset.y = y_offset;

        //place
        if (++index_cursor >= num_objects) index_cursor = 0;
#if CODEDEBUG
        if (!go_available[index_cursor]) {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogWarning(METHOD_NAME, (debug_data + ".gos[{0}] Not available"), index_cursor);
        }
#endif
        Transform attach_parent = gc.AttachNodePattern();
        Transform offset_parent = gc.AttachNodeRoot();

        go_available[index_cursor] = false;
        Transform tr = gos[index_cursor];
        tr.SetParent(attach_parent, false);
        tr.localPosition = offset - offset_parent.localPosition;

        pool_objects[index_cursor].Pool_Placed(el, lane_index);
    }
    public void PlaceAsCoin(Vector3 offset, Transform parent)
    {
        if (++index_cursor == num_objects) index_cursor = 0;
        go_available[index_cursor] = false;
        Transform tr = gos[index_cursor].transform;
        tr.SetParent(parent, false);
        tr.localPosition = offset;
        pool_objects[index_cursor].Pool_Placed();
    }
    public void PlaceAsArcTrigger(Vector3 offset, Vector3 scale, Transform parent)
    {
        if (++index_cursor == num_objects) index_cursor = 0;
        go_available[index_cursor] = false;
        Transform tr = gos[index_cursor].transform;
        tr.SetParent(parent, false);
        tr.localPosition = offset/* - parent.localPosition*/;
        tr.localScale = scale;
        pool_objects[index_cursor].Pool_Placed(scale.z);
    }

    public void Release(int index_in_pool)
    {
        if (!go_available[index_in_pool]) {
            pool_objects[index_in_pool].Pool_Releasing();
            //detach from parent and attach to rootnode.
            gos[index_in_pool].transform.SetParent(parent_node, false);
            go_available[index_in_pool] = true;
        }
    }
    public void ReleaseAll()
    {
        for (int i = 0; i < num_objects; ++i) Release(i);
    }
    public void Destroy()
    {
        if (gos == null) return;
        ReleaseAll();
        for (int i = 0; i < num_objects; ++i) {
            GameObject.Destroy(gos[i]);
        }
#if CODEDEBUG
        GameObject.Destroy(parent_node.gameObject);
#endif
        parent_node = null;
        gos = null;
        pool_objects = null;
        go_available = null;
    }
}
#endregion //[POOL]
#region [REF GROUP]
public interface IRefGroup<Item>
{
    void Update();
    Item Get();
    Item UpdateAndGet();
}
[System.Serializable]
public class RefGroup<Item> : IRefGroup<Item> where Item : IProbRandomItem
{
    /*[SetInEditor]*/
    public string name = string.Empty;
    /*[SetInEditor]*/
    public Item[] items = null;

    public void Update()
    {
        ProbRandom.Update(items);
    }
    public Item Get()
    {
        return items[ProbRandom.Get(items)];
    }
    public Item UpdateAndGet()
    {
        return items[ProbRandom.UpdateAndGet(items)];
    }
}
public class SelectorRefGroup<Item> : IRefGroup<Item> where Item : IProbRandomItem
{
    /*[SetInEditor]*/
    public string name = string.Empty;
    /*[SetInEditor]*/
    public int selector = 0;
    /*[SetInEditor]*/
    public Item[][] items = null;

    GameController.GetValue<int> selector_getter = null;

    public void Update()
    {
        if (selector_getter != null) { selector = selector_getter(); }
        for (int i = 0; i < items.Length; ++i) {
            ProbRandom.Update(items[i]);
        }
    }
    public Item Get()
    {
        return items[selector][ProbRandom.Get(items[selector])];
    }
    public Item UpdateAndGet()
    {
        if (selector_getter != null) { selector = selector_getter(); }
        return items[selector][ProbRandom.UpdateAndGet(items[selector])];
    }
    public void SetSelectorGetter(GameController.GetValue<int> getter)
    {
        selector_getter = getter;
    }
}
public class SimpleRefGroupItem : IProbRandomItem
{
    /*[SetInEditor]*/
    public int index = 0;

    /*[SetInEditor]*/
    [InspectorRange(0f, 1f)]
    public float prob = 1.0f;
    public float ProbValue() { return prob; }
    [NotSerialized, HideInInspector]
    public float ProbHalfSum { get; set; }
}
public class SimpleRefGroup : RefGroup<SimpleRefGroupItem> { }

#endregion //[REF GROUP]
#region [THEME]
public enum ObstaclePlaceType
{
    EMPTY,
    BY_INDEX,
    FROM_GROUP
}
public enum PlaceOnSide
{
    BOTH,
    LEFT,
    RIGHT
}
public class CutsceneZoneSet
{
    /*[SetInEditor]*/
    public GameObject cutscene_crash_obstacle_zone_prefab = null;
    /*[SetInEditor]*/
    public GameObject cutscene_crash_chaser_zone_prefab = null;
    /*[SetInEditor]*/
    public GameObject cutscene_continue_obstacle_zone_prefab = null;
    /*[SetInEditor]*/
    public GameObject cutscene_continue_chaser_zone_prefab = null;
    /*[SetInEditor]*/
    public GameObject cutscene_rest_zone_prefab = null;
    /*[SetInEditor]*/
    public GameController.RestMusicType rest_music_type = GameController.RestMusicType.OUTSIDE;
}
public abstract class ObstacleSet : IProbRandomItem
{
    /*[SetInEditor]*/
    public string name = string.Empty;

    /*[SetInEditor]*/
    [InspectorNullable]
    public PrefabPool begin = null;
    /*[SetInEditor]*/
    [InspectorNullable]
    public PrefabPool end = null;
    /*[SetInEditor]*/
    [InspectorCollectionRotorzFlags(ShowIndices = true)]
    public PrefabPool[] body = null;
    //[NotSerialized, HideInInspector]
    //public int last_body_index = -1;

    /*[SetInEditor]*/
    public int body_place_count = 16;

    /*[SetInEditor]*/
    [InspectorRange(0f, 1f)]
    public float prob = 1.0f;
    public float ProbValue() { return prob; }
    [NotSerialized, HideInInspector]
    public float ProbHalfSum { get; set; }

    public void Init(
#if CODEDEBUG
string debug_data
#endif
)
    {
#if CODEDEBUG
        string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
#endif
        if (begin != null) {
#if CODEDEBUG
            if (begin.SizeInCells() % GameController.CELL_SYNC_SIZE != 0) {
                GameController.LogError(METHOD_NAME, (debug_data + ".begin.size_in_cells == {0}, should divide by {1} without remainder"), begin.SizeInCells(), GameController.CELL_SYNC_SIZE);
            }
#endif
            begin.Init(
#if CODEDEBUG
string.Format("{0}.begin", debug_data)
#endif
);
        }
        if (end != null) {
#if CODEDEBUG
            if (end.SizeInCells() % GameController.CELL_SYNC_SIZE != 0) {
                GameController.LogError(METHOD_NAME, (debug_data + ".end.size_in_cells == {0}, should divide by {1} without remainder"), end.SizeInCells(), GameController.CELL_SYNC_SIZE);
            }
#endif
            end.Init(
#if CODEDEBUG
string.Format("{0}.end", debug_data)
#endif
);
        }

#if CODEDEBUG
        if (body == null || body.Length == 0) {
            GameController.LogError(METHOD_NAME, (debug_data + ".body is {0}"), 0);
        }
#endif
        for (int i = 0, l = body.Length; i < l; ++i) {
#if CODEDEBUG
            if (body[i].SizeInCells() % GameController.CELL_SYNC_SIZE != 0) {
                GameController.LogError(METHOD_NAME, (debug_data + ".body[{0}].size_in_cells == {1}, should divide by {2} without remainder"), i, body[i].SizeInCells(), GameController.CELL_SYNC_SIZE);
            }
#endif
            body[i].Init(
#if CODEDEBUG
string.Format("{0}.body[{1}]", debug_data, i)
#endif
);
        }
        ProbRandom.Update(body);

        //last_body_index = -1;
    }
    public void Destroy()
    {
        if (begin != null) { begin.Destroy(); }
        if (end != null) { end.Destroy(); }
        for (int i = 0; i < body.Length; ++i) { body[i].Destroy(); }
    }
}
public class RoadObstacleSet : ObstacleSet
{
#if UNITY_EDITOR
    [InspectorHeader("Road"), InspectorMargin(20), InspectorDivider, ShowInInspector, InspectorHidePrimary]
    bool __inspector_road;
#endif
    /*[SetInEditor]*/
    public bool road_override_sides = false;
    /*[SetInEditor]*/
    [InspectorNullable]
    public CutsceneZoneSet cutscene_zones = null;
}
public class SideObstacleSet : ObstacleSet
{
#if UNITY_EDITOR
    [InspectorHeader("Side"), InspectorMargin(20), InspectorDivider, ShowInInspector, InspectorHidePrimary]
    bool __inspector_side;
#endif
    /*[SetInEditor]*/
    public PlaceOnSide only_on_side = PlaceOnSide.BOTH;
}
public class PatternBox
{
    public class Element
    {
        /*[SetInEditor]*/
        public int index = -1;
        /*[SetInEditor]*/
        public bool write_serie = false;
        /*[SetInEditor]*/
        public int num_cells_before = 0;

        /*[SetInEditor]*/
        [InspectorRange(-0.1f, 1.1f)]
        public float coins_prob = -0.1f;
        /*[SetInEditor]*/
        [InspectorStepRange(0f, 6f, 0.5f)]
        public float coins_step_offset = 0f;
        /*[SetInEditor]*/
        [InspectorRange(-0.1f, 0.9f)]
        public float super_luck_threshold = -0.1f;
        /*[SetInEditor]*/
        public CoinPlaceFormMod coin_form_mod = CoinPlaceFormMod.NONE;
        /*[SetInEditor]*/
        public CoinPlaceAlignment coin_alignment = CoinPlaceAlignment.BOTH;
    }
    /*[SetInEditor]*/
    public string name = string.Empty;
    /*[SetInEditor]*/
    public bool random_mirror = false;
    /*[SetInEditor]*/
    [InspectorCollectionRotorzFlags(ShowIndices = true)]
    public Element[][] elements;

    public void Init(
#if CODEDEBUG
string debug_data
#endif
)
    {
#if CODEDEBUG
        string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
#endif
        if (elements.Length < GameController.NUM_RUN_LANES) {
            System.Array.Resize(ref elements, GameController.NUM_RUN_LANES);
#if CODEDEBUG
            GameController.LogWarning(METHOD_NAME, (debug_data + ".elements.Length == {0}, expected {1}"), elements.Length, GameController.NUM_RUN_LANES);
#endif
        }
        for (int i = 0, l = elements.Length; i < l; ++i) {
            if (elements[i] == null || elements[i].Length == 0) {
#if CODEDEBUG
                GameController.LogWarning(METHOD_NAME, (debug_data + ".elements[{0}] is NULL"), i);
#endif
                elements[i] = new Element[1];
            }
            if (elements[i][0] == null) {
#if CODEDEBUG
                GameController.LogWarning(METHOD_NAME, (debug_data + ".elements[{0}][0] is NULL"), i);
#endif
                elements[i][0] = new Element();
                elements[i][0].index = -1;
                elements[i][0].coins_prob = -1f;
                elements[i][0].super_luck_threshold = -1f;
            }
        }
    }
}
public class PatternSuperBox
{
    public class BoxReference
    {
        /*[SetInEditor]*/
        public int index = -1;
        /*[SetInEditor]*/
        public int num_cells_before = 0;
    }
    /*[SetInEditor]*/
    public string name = string.Empty;

    /*[SetInEditor]*/
    [InspectorCollectionRotorzFlags(ShowIndices = true)]
    public BoxReference[] boxes;

    /*[SetInEditor]*/
    public float y_offset = 0f;
    /*[SetInEditor]*/
    public int override_road_index = -1;
    /*[SetInEditor]*/
    public bool random_mirror = false;

    public void Init(
#if CODEDEBUG
        string debug_data
#endif
)
    {
#if CODEDEBUG
        string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
#endif
#if CODEDEBUG
        if (boxes == null) {
            GameController.LogWarning(METHOD_NAME, debug_data + ".boxes is NULL");
            boxes = new BoxReference[0];
        }
#endif
    }
}
public class ObstaclePattern
{
    public class SuperBoxReference
    {
        /*[SetInEditor]*/
        public int index = -1;
        /*[SetInEditor]*/
        public int num_cells_before = 0;
    }
    /*[SetInEditor]*/
    public string name = string.Empty;
    /*[SetInEditor]*/
    public SuperBoxReference[] super_boxes = null;
}
public class PatternProgressReference
{
    /*[SetInEditor]*/
    public int pattern_group_index = 0;
    /*[SetInEditor]*/
    [InspectorRange(0f, 1f)]
    public float from_run_speed = 0f;
}
public class Theme
{
    /*[SetInEditor]*/
    [InspectorCategory("Begin End")]
    public string name = string.Empty;

    /*[SetInEditor]*/
    [InspectorNullable, InspectorCategory("Begin End")]
    public PrefabPool begin = null;
    /*[SetInEditor]*/
    [InspectorNullable, InspectorCategory("Begin End")]
    public PrefabPool end = null;

    /*[SetInEditor]*/
    [InspectorCollectionRotorzFlags(ShowIndices = true), InspectorCategory("Sides")]
    public SideObstacleSet[] sides = null;

    /*[SetInEditor]*/
    [InspectorCollectionRotorzFlags(ShowIndices = true), InspectorCategory("Roads")]
    public RoadObstacleSet[] roads = null;

    /*[SetInEditor]*/
    [InspectorCategory("Patterns")]
    public PatternProgressReference[] patterns = null;

#if UNITY_EDITOR
    [InspectorHeader("Cutscene"), InspectorMargin(20), InspectorDivider, ShowInInspector, InspectorHidePrimary]
    bool __inspector_cutscene;
#endif
    /*[SetInEditor]*/
    [InspectorCategory("Begin End")]
    public GameObject cutscene_intro_zone_prefab = null;
    /*[SetInEditor]*/
    [InspectorCategory("Begin End")]
    public CutsceneZoneSet cutscene_zones = null;

    public void Init(
#if CODEDEBUG
        string debug_data
#endif
        )
    {
#if CODEDEBUG
        string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
#endif
        if (begin != null) {
#if CODEDEBUG
            if (begin.SizeInCells() % GameController.CELL_SYNC_SIZE != 0) {
                GameController.LogError(METHOD_NAME, (debug_data + ".begin.size_in_cells == {0}, should divide by {1} without remainder"), begin.SizeInCells(), GameController.CELL_SYNC_SIZE);
            }
#endif
            begin.Init(
#if CODEDEBUG
                string.Format("{0}.begin", debug_data)
#endif
                );
        }
        if (end != null) {
#if CODEDEBUG
            if (end.SizeInCells() % GameController.CELL_SYNC_SIZE != 0) {
                GameController.LogError(METHOD_NAME, (debug_data + ".begin.size_in_cells == {0}, should divide by {1} without remainder"), begin.SizeInCells(), GameController.CELL_SYNC_SIZE);
            }
#endif
            end.Init(
#if CODEDEBUG
                string.Format("{0}.end", debug_data)
#endif
                );
        }

        //sides
        if (sides == null) { sides = new SideObstacleSet[0]; }
#if CODEDEBUG
        if (sides.Length == 0) {
            GameController.LogWarning(METHOD_NAME, (debug_data + ".sides is {0}"), 0);
        }
#endif
        for (int i = 0, l = sides.Length; i < l; ++i) {
            sides[i].Init(
#if CODEDEBUG
string.Format("{0}.sides[{1}]", debug_data, i)
#endif
);
        }
        ProbRandom.Update(sides);
#if CODEDEBUG
        if (sides[0].only_on_side != PlaceOnSide.BOTH)
            GameController.LogError(METHOD_NAME, (debug_data + ".sides[0] must be for BOTH sides"));
        if (sides[0].begin != null && sides[0].begin.SizeInCells() != GameController.CELL_SYNC_SIZE)
            GameController.LogError(METHOD_NAME, (debug_data + ".sides[0].begin.size_in_cells is {0}, expected {1}"), sides[0].begin.SizeInCells(), GameController.CELL_SYNC_SIZE);
        if (sides[0].end != null && sides[0].end.SizeInCells() != GameController.CELL_SYNC_SIZE)
            GameController.LogError(METHOD_NAME, (debug_data + ".sides[0].end.size_in_cells is {0}, expected {1}"), sides[0].end.SizeInCells(), GameController.CELL_SYNC_SIZE);
        if (sides[0].body[0].SizeInCells() != GameController.CELL_SYNC_SIZE)
            GameController.LogError(METHOD_NAME, (debug_data + ".sides[0].body[0].size_in_cells is {0}, expected {1}"), sides[0].body[0].SizeInCells(), GameController.CELL_SYNC_SIZE);
#endif

        //roads
#if CODEDEBUG
        if (roads == null || roads.Length == 0) {
            GameController.LogError(METHOD_NAME, (debug_data + ".roads is {0}"), 0);
        }
#endif

        for (int i = 0, l = roads.Length; i < l; ++i) {
            roads[i].Init(
#if CODEDEBUG
string.Format("{0}.roads[{1}]", debug_data, i)
#endif
);
        }
        ProbRandom.Update(roads);
#if CODEDEBUG
        if (roads[0].begin != null && roads[0].begin.SizeInCells() != GameController.CELL_SYNC_SIZE)
            GameController.LogError(METHOD_NAME, (debug_data + ".roads[0].begin.size_in_cells is {0}, expected {1}"), roads[0].begin.SizeInCells(), GameController.CELL_SYNC_SIZE);
        if (roads[0].end != null && roads[0].end.SizeInCells() != GameController.CELL_SYNC_SIZE)
            GameController.LogError(METHOD_NAME, (debug_data + ".roads[0].end.size_in_cells is {0}, expected {1}"), roads[0].end.SizeInCells(), GameController.CELL_SYNC_SIZE);
        if (roads[0].body[0].SizeInCells() != GameController.CELL_SYNC_SIZE)
            GameController.LogError(METHOD_NAME, (debug_data + ".roads[0].body[0].size_in_cells is {0}, expected {1}"), roads[0].body[0].SizeInCells(), GameController.CELL_SYNC_SIZE);
        if (roads[0].road_override_sides)
            GameController.LogError(METHOD_NAME, (debug_data + ".roads[0] must not override sides"));
#endif

    }
    public void Destroy()
    {
        if (begin != null) { begin.Destroy(); }
        if (end != null) { end.Destroy(); }

        //sides
        for (int i = 0; i < sides.Length; ++i) sides[i].Destroy();

        //roads
        for (int i = 0; i < roads.Length; ++i) roads[i].Destroy();
    }
}
public class ThemeSlot
{
    public class ThemeInfo
    {
        /*[SetInEditor]*/
        public int theme_index;
        /*[SetInEditor]*/
        public int theme_cells;
        /*[SetInEditor]*/
        [InspectorRange(0.5f, 2.0f)]
        public float cell_width = 2.0f;
        /*[SetInEditor]*/
        /*[Range(1, 4)]
        public int num_run_lanes = 3;*/
    }
    /*[SetInEditor]*/
    public string name = string.Empty;
    /*[SetInEditor]*/
    public ThemeInfo[] theme_infos = null;

#if UNITY_EDITOR
    [InspectorHeader("UI"), InspectorMargin(20), InspectorDivider, ShowInInspector, InspectorHidePrimary]
    bool __inspector_shop;
#endif
    /*[SetInEditor]*/
    public GameObject ui_theme_prefab = null;
    [System.NonSerialized, NotSerialized, HideInInspector]
    public GameObject ui_theme_go = null;
    /*[SetInEditor]*/
    public Sprite ui_themeslot_icon = null;
    /*[SetInEditor]*/
    public string ui_themeslot_name = string.Empty;
    /*[SetInEditor]*/
    public string ui_themeslot_desc = string.Empty;

#if UNITY_EDITOR
    [InspectorHeader("Background"), InspectorMargin(20), InspectorDivider, ShowInInspector, InspectorHidePrimary]
    bool __inspector_bg;
#endif
    /*[SetInEditor]*/
    public GameObject bg_prefab = null;
}
#endregion //[THEME]

#region [PLAYCHAR]
public class Playchar
{
    /*[SetInEditor]*/
    public string name = string.Empty;
    /*[SetInEditor]*/
    public GameObject player_prefab = null;

#if UNITY_EDITOR
    [InspectorHeader("Coins"), InspectorMargin(20), InspectorDivider, ShowInInspector, InspectorHidePrimary]
    bool __inspector_coins;
#endif
    /*[SetInEditor]*/
    public Dictionary<CoinType, SuperCoinInitData> super_coin_override = null;

#if UNITY_EDITOR
    [InspectorHeader("Cutscene Character Animations"), InspectorMargin(20), InspectorDivider, ShowInInspector, InspectorHidePrimary]
    bool __inspector_cutscene;
#endif
    /*[SetInEditor]*/
    public GameObject anim_intro;
    /*[SetInEditor]*/
    public GameObject anim_crash_obstacle;
    /*[SetInEditor]*/
    public GameObject anim_crash_chaser;
    /*[SetInEditor]*/
    public GameObject anim_continue_obstacle;
    /*[SetInEditor]*/
    public GameObject anim_continue_chaser;
    /*[SetInEditor]*/
    public GameObject anim_rest;

#if UNITY_EDITOR
    [InspectorHeader("UI"), InspectorMargin(20), InspectorDivider, ShowInInspector, InspectorHidePrimary]
    bool __inspector_ui;
#endif
    /*[SetInEditor]*/
    public GameObject ui_playchar_prefab = null;
    [System.NonSerialized, NotSerialized, HideInInspector]
    public GameObject ui_playchar_go = null;

    /*[SetInEditor]*/
    public string ui_playchar_name = string.Empty;
    /*[SetInEditor]*/
    public string ui_playchar_desc = string.Empty;
    /*[SetInEditor]*/
    public string ui_playchar_icon_name_hs = string.Empty;
    /*[SetInEditor]*/
    public string ui_playchar_icon_name_pass = string.Empty;
}
public class PlaycharSlot
{
    public class PlaycharInfo
    {
        /*[SetInEditor]*/
        public int playchar_index = 0;
    }
    /*[SetInEditor]*/
    public string name = string.Empty;
    /*[SetInEditor]*/
    public PlaycharInfo[] playchars = null;
    /*[SetInEditor]*/
    public GameObject chaser_prefab = null;
#if UNITY_EDITOR
    [InspectorHeader("Cutscene Chaser Animations With Environment"), InspectorMargin(20), InspectorDivider, ShowInInspector, InspectorHidePrimary]
    bool __inspector_cutscene;
#endif
    /*[SetInEditor]*/
    public bool override_intro_zone = false;
    /*[SetInEditor]*/
    public GameObject anim_intro;
    /*[SetInEditor]*/
    public GameController.IntroMusicType intro_music_type = GameController.IntroMusicType.EJDER;
    /*[SetInEditor]*/
    public GameObject anim_crash_obstacle;
    /*[SetInEditor]*/
    public GameObject anim_crash_chaser;
    /*[SetInEditor]*/
    public GameObject anim_continue_obstacle;
    /*[SetInEditor]*/
    public GameObject anim_continue_chaser;
    /*[SetInEditor]*/
    public GameObject anim_rest;

#if UNITY_EDITOR
    [InspectorHeader("Levels"), InspectorMargin(20), InspectorDivider, ShowInInspector, InspectorHidePrimary]
    bool __inspector_levels;
#endif
    /*[SetInEditor]*/
    [InspectorCommentAttribute("Levels Max: 3. Value[0] is default\nMagnet work time\nValue in seconds")]
    public float[] magnet_time_levels = new float[PlaycharLevel.MaxLevelFor(PlaycharLevelType.MAG_TIME) + 1];
    /*[SetInEditor]*/
    [InspectorCommentAttribute("Levels Max: 3. Value[0] is default\nScoreX work time\nValue in seconds")]
    public float[] scorex_time_levels = new float[PlaycharLevel.MaxLevelFor(PlaycharLevelType.SCOREX_TIME) + 1];
    /*[SetInEditor]*/
    [InspectorCommentAttribute("Levels Max: 3. Value[0] is default\nCoinsX work time\nValue in seconds")]
    public float[] coinsx_time_levels = new float[PlaycharLevel.MaxLevelFor(PlaycharLevelType.COINSX_TIME) + 1];
    /*[SetInEditor]*/
    [InspectorCommentAttribute("Levels Max: 3. Value[0] is default\nLuckX work time\nValue in seconds")]
    public float[] luckx_time_levels = new float[PlaycharLevel.MaxLevelFor(PlaycharLevelType.LUCKX_TIME) + 1];
    /*[SetInEditor]*/
    [InspectorCommentAttribute("Levels Max: 3. Value[0] is default\nOne StaminaPoint decrease time\nValue in seconds")]
    public float[] stamina_decrease_time_levels = new float[PlaycharLevel.MaxLevelFor(PlaycharLevelType.STAMINA_DECTIME) + 1];
    /*[SetInEditor]*/
    [InspectorCommentAttribute("Levels Max: 2. Value[0] is default")]
    public float[] drop_power_levels = new float[PlaycharLevel.MaxLevelFor(PlaycharLevelType.DROP_POWER) + 1];

#if UNITY_EDITOR
    [InspectorHeader("Coins"), InspectorMargin(20), InspectorDivider, ShowInInspector, InspectorHidePrimary]
    bool __inspector_coins;
#endif
    /*[SetInEditor]*/
    public SuperCoinInitData drop_coin_data = null;

#if UNITY_EDITOR
    [InspectorHeader("UI"), InspectorMargin(20), InspectorDivider, ShowInInspector, InspectorHidePrimary]
    bool __inspector_ui;
#endif
    /*[SetInEditor]*/
    public GameObject ui_chaser_prefab = null;
    [System.NonSerialized, NotSerialized, HideInInspector]
    public GameObject ui_chaser_go = null;
    /*[SetInEditor]*/
    public string ui_chaser_name = string.Empty;
    /*[SetInEditor]*/
    public GameObject ui_devgui_prefab = null;
    /*[SetInEditor]*/
    public GameObject ui_reward_item_drop_prefab = null;
    /*[SetInEditor]*/
    public UserItemDesc ui_drop_desc = null;
    /*[SetInEditor]*/
    public UserItemDesc ui_drop_level_desc = null;
    /*[SetInEditor]*/
    public Sprite ui_playchar_icon = null;

#if UNITY_EDITOR
    [InspectorHeader("Particles"), InspectorMargin(20), InspectorDivider, ShowInInspector, InspectorHidePrimary]
    bool __inspector_particles;
#endif
    public GameObject par_playchar_drop_prefab = null;
    public GameObject par_chaser_slide_prefab = null;

#if UNITY_EDITOR
    [InspectorHeader("Audio"), InspectorMargin(20), InspectorDivider, ShowInInspector, InspectorHidePrimary]
    bool __inspector_audio;
#endif
    /*[SetInEditor]*/
    public AudioClip aud_chaser_drophit_clip = null;
    /*[SetInEditor]*/
    public AudioClip aud_chaser_dropslide_clip = null;
    /*[SetInEditor]*/
    public AudioClip aud_chaser_attack_clip = null;
}
#endregion //[PLAYCHAR]
public static class StringCipher
{
    // This constant string is used as a "salt" value for the PasswordDeriveBytes function calls.
    // This size of the IV (in bytes) must = (keysize / 8).  Default keysize is 256, so the IV must be
    // 32 bytes long.  Using a 16 character string here gives us 32 bytes when converted to a byte array.
    private static readonly byte[] initVectorBytes = System.Text.Encoding.ASCII.GetBytes("tu89geji340t89u2");
    private static readonly string passPhrase = (04051984).ToString();
    private static System.Security.Cryptography.Rfc2898DeriveBytes keygen = null;
    private static byte[] keyBytes = null;

    // This constant is used to determine the keysize of the encryption algorithm.
    private const int keysize = 256;

    public static string Encrypt(string plainText)
    {
        if (keygen == null) {
            keygen = new System.Security.Cryptography.Rfc2898DeriveBytes(passPhrase, initVectorBytes);
            keyBytes = keygen.GetBytes(keysize / 8);
        }
        byte[] plainTextBytes = System.Text.Encoding.Unicode.GetBytes(plainText);
        using (var cipher = new System.Security.Cryptography.RijndaelManaged()) {
            cipher.Mode = System.Security.Cryptography.CipherMode.CBC;
            using (var encryptor = cipher.CreateEncryptor(keyBytes, initVectorBytes)) {
                using (var memoryStream = new MemoryStream()) {
                    using (var cryptoStream = new System.Security.Cryptography.CryptoStream(memoryStream, encryptor, System.Security.Cryptography.CryptoStreamMode.Write)) {
                        cryptoStream.Write(plainTextBytes, 0, plainTextBytes.Length);
                        cryptoStream.FlushFinalBlock();
                        byte[] cipherTextBytes = memoryStream.ToArray();
                        return System.Convert.ToBase64String(cipherTextBytes);
                    }
                }
            }
        }
    }

    public static string Decrypt(string cipherText)
    {
        byte[] cipherTextBytes = System.Convert.FromBase64String(cipherText);
        if (keygen == null) {
            keygen = new System.Security.Cryptography.Rfc2898DeriveBytes(passPhrase, initVectorBytes);
            keyBytes = keygen.GetBytes(keysize / 8);
        }
        using (var cipher = new System.Security.Cryptography.RijndaelManaged()) {
            cipher.Mode = System.Security.Cryptography.CipherMode.CBC;
            using (var decryptor = cipher.CreateDecryptor(keyBytes, initVectorBytes)) {
                using (var memoryStream = new MemoryStream(cipherTextBytes)) {
                    using (var cryptoStream = new System.Security.Cryptography.CryptoStream(memoryStream, decryptor, System.Security.Cryptography.CryptoStreamMode.Read)) {
                        byte[] plainTextBytes = new byte[cipherTextBytes.Length];
                        int decryptedByteCount = cryptoStream.Read(plainTextBytes, 0, plainTextBytes.Length);
                        return System.Text.Encoding.Unicode.GetString(plainTextBytes, 0, decryptedByteCount);
                    }
                }
            }
        }
    }
}
public class ConfigSerializeData
{
    public string us_id = GameController.OFFLINE_ID;
    public bool show_tut = true;
    public int lang_index = 0;
    public System.DateTime daily_watch_date = default(System.DateTime);
}
public interface IParticles
{
    void Emit(int count);
    void Play();
    void Stop();
    bool IsAlive();
    Transform Root();
}
public class ParticleHolder : IParticles
{
    public ParticleSystem[] particles = null;
    public Transform root = null;
    public void Play()
    {
        for (int i = 0, l = particles.Length; i < l; ++i) {
            particles[i].Play(false);
        }
    }
    public void Emit(int count)
    {
        for (int i = 0, l = particles.Length; i < l; ++i) {
            particles[i].Emit(count);
        }
    }
    public void Stop()
    {
        for (int i = 0, l = particles.Length; i < l; ++i) {
            particles[i].Stop(false);
        }
    }
    public bool IsAlive()
    {
        for (int i = 0, l = particles.Length; i < l; ++i) {
            if (particles[i].IsAlive(false)) return true;
        }
        return false;
    }
    public Transform Root()
    {
        return root;
    }
    public bool IsEmpty()
    {
        return particles == null || particles.Length == 0;
    }
    public void Clear()
    {
        particles = null;
    }
    public void SetParticles(Transform rootTransform)
    {
        root = rootTransform;
        var pars = GameController.FindComponentsRecursive<ParticleSystem>(root);
        particles = pars.ToArray();
    }
}
public class GameController : Singleton<GameController>
{
    public delegate void Event();
    public delegate void Event<P1>(P1 p1);
    public delegate void Event<P1, P2>(P1 p1, P2 p2);
    public delegate void Event<P1, P2, P3>(P1 p1, P2 p2, P3 p3);
    public delegate R GetValue<R>();
    public delegate R GetValue<R, P1>(P1 p1);
    public delegate R GetValue<R, P1, P2>(P1 p1, P2 p2);

    public static void Stub() { }
    public static void Stub<P1>(P1 p1) { }
    public static void Stub<P1, P2>(P1 p1, P2 p2) { }
    public static void Stub<P1, P2, P3>(P1 p1, P2 p2, P3 p3) { }


    /*public static void CreateFloorMeshDense(Mesh mesh)
    {
        const int NUM_X_VERT = NUM_RUN_LANES + 1;
        const int NUM_Z_VERT = NUM_LANE_CELLS + 1;

        float cell_width = GameController.Instance.THEME_CurrentCellWidth();
        int num_faces = NUM_RUN_LANES * NUM_LANE_CELLS;
        int num_vertices = NUM_X_VERT * NUM_Z_VERT;
        Vector3[] vertices = new Vector3[num_vertices];
        Vector3[] normals = new Vector3[num_vertices];
        Vector2[] uvs = new Vector2[num_vertices];
        for (int z = 0; z < NUM_Z_VERT; ++z) {
            float zPos = (z * CELL_DEPTH) + LANE_NEAR_Z;
            for (int x = 0; x < NUM_X_VERT; ++x) {
                float xPos = (x * cell_width) + LANES_OFFSET_X;
                vertices[z * NUM_X_VERT + x] = new Vector3(xPos, 0.0f, zPos);
                normals[z * NUM_X_VERT + x] = Vector3.up;
                uvs[z * NUM_X_VERT + x] = new Vector2(x / NUM_X_VERT, z / NUM_Z_VERT);
            }
        }

        int[] triangles = new int[num_faces * 6];
        int t = 0;
        int row_index = 0;
        for (int f = 0; f < num_faces; ++f) {
            row_index = f / NUM_RUN_LANES;
            triangles[t++] = f + row_index;
            triangles[t++] = f + 1 + NUM_X_VERT + row_index;
            triangles[t++] = f + NUM_X_VERT + row_index;

            triangles[t++] = f + row_index;
            triangles[t++] = f + 1 + row_index;
            triangles[t++] = f + 1 + NUM_X_VERT + row_index;
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles;

        mesh.RecalculateBounds();
        mesh.Optimize();
    }
    public static void CreateFloorMeshQuad(Mesh mesh)
    {
        const int NUM_VERTICES = 4;
        float cell_width = GameController.Instance.THEME_CurrentCellWidth();
        Vector3[] vertices = new Vector3[NUM_VERTICES] { 
            new Vector3(LANES_OFFSET_X, 0f, LANE_NEAR_Z),
            new Vector3(LANES_OFFSET_X + (NUM_RUN_LANES * cell_width), 0f, LANE_NEAR_Z),
            new Vector3(LANES_OFFSET_X, 0f, LANE_FAR_Z),
            new Vector3(LANES_OFFSET_X + (NUM_RUN_LANES * cell_width), 0, LANE_FAR_Z)
        };
        Vector3[] normals = new Vector3[NUM_VERTICES] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
        Vector2[] uvs = new Vector2[NUM_VERTICES] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };

        int[] triangles = new int[6];
        triangles[0] = 0;
        triangles[1] = 3;
        triangles[2] = 2;
        triangles[3] = 0;
        triangles[4] = 1;
        triangles[5] = 3;

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles;

        mesh.RecalculateBounds();
        mesh.Optimize();
    }*/
    public static Pair<T1, T2> MakePair<T1, T2>(T1 t1, T2 t2)
    {
        return new Pair<T1, T2>(t1, t2);
    }
    public static List<T> FindComponentsRecursive<T>(Transform root) where T : Component
    {
        List<T> result = new List<T>();

        var par = root.GetComponent<T>();
        if (par != null) { result.Add(par); }

        if (root.childCount == 0) return result;
        foreach (Transform child in root) {
            result.AddRange(FindComponentsRecursive<T>(child));
        }
        return result;
    }
    public static Transform FindNodeWithName(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform child in root) {
            Transform node = null;
            if ((node = FindNodeWithName(child, name)) != null) { return node; }
        }
        return null;
    }
    public static void DestroyComponents<T>(IList<T> comps) where T : Component
    {
        for (int i = 0; i < comps.Count; ++i) {
            Component.Destroy(comps[i]);
        }
    }



    public const string TAG_FLOOR = "ObFloor";
    public const string TAG_SLOPE = "ObSlope";
    public const string TAG_OBSTACLE_WITHIN = "ObWithin";
    public const string TAG_OBSTACLE_STUMBLE = "ObStumble";
    public const string TAG_OBSTACLE_CRASH = "ObCrash";
    public const string TAG_OBSTACLE_SIDE = "ObSide";
    public const string TAG_OBSTACLE_FALL = "ObFall";
    public const string TAG_PLAYER = "Player";
    public const string TAG_CHASER = "Chaser";
    public const string TAG_MAGNET = "Magnet";
    public const string TAG_PICKER = "Picker";



#if UNITY_EDITOR
    /*[SetInEditor]*/
    [ShowInInspector, InspectorRange(0f, 2.0f)]
#endif
    float playing_time_scale = 1.0f;
    float frame_delta_time = 0f;
    float playing_delta_time = 0f;
    public float PlayingTime() { return playing_delta_time; }
    public float PlayingTimeScale() { return playing_time_scale; }
    public float FrameTime() { return frame_delta_time; }



    Event[] invoke_on_mt = new Event[2];
    int invoke_cursor = 0;
    public void InvokeOnMT(Event func) { invoke_on_mt[invoke_cursor] += func; }

    Event update_on_mt = null;
    Event update_on_mt_playing = null;
    public void AddUpdateOnMT(GameController.Event func) { update_on_mt -= func; update_on_mt += func; }
    public void RemoveUpdateOnMT(GameController.Event func) { update_on_mt -= func; }
    public void EnableUpdateOnMT(GameController.Event func, bool enable) { update_on_mt -= func; if (enable) update_on_mt += func; }
    public void AddUpdateOnMT_Playing(GameController.Event func) { update_on_mt_playing -= func; update_on_mt_playing += func; }
    public void RemoveUpdateOnMT_Playing(GameController.Event func) { update_on_mt_playing -= func; }
    public void EnableUpdateOnMT_Playing(GameController.Event func, bool enable) { update_on_mt_playing -= func; if (enable) update_on_mt_playing += func; }

    ParallelRoutine work_routine = new ParallelRoutine(64);
    public void AddWorkRoutine(IEnumerator routine, bool callOnce = false) { work_routine.Add(routine, callOnce); }
    SequentialRoutine game_state_routine = new SequentialRoutine();
    void PushStateRoutine(IEnumerator routine) { game_state_routine.Push(routine); }
    void OverlapStateRoutine(IEnumerator routine) { game_state_routine.Overlap(routine); }

    #region [Localize]
    const string LOCALIZE_ASSET = "localize";

    Event on_lang_changed = null;
    public void AddOnLangChanged(Event func) { on_lang_changed -= func; on_lang_changed += func; }
    public void RemoveOnLangChanged(Event func) { on_lang_changed -= func; }
    public void EnableOnLangChanged(Event func, bool enable) { on_lang_changed -= func; if (enable) on_lang_changed += func; }

    int lang_order_index = 0;
    int lang_current_index = 0;
    public LocalizeScriptableObject loc_so = null;

    public string Localize(string term)
    {
        if (string.IsNullOrEmpty(term)) return string.Empty;
        string[] term_data = null;
        if (!loc_so.localize_data.TryGetValue(term, out term_data) || lang_current_index >= term_data.Length) return term;

        return term_data[lang_current_index];
    }

    void LOCALIZE_NextLanguage()
    {
        //select language as ordered in order list
        lang_order_index = (++lang_order_index) % loc_so.langs_order.Length;
        //get actual language index from order list
        lang_current_index = loc_so.langs_order[lang_order_index];
        //save in config data
        config_data.lang_index = lang_order_index;
        if (on_lang_changed != null) on_lang_changed();
    }

    void LOCALIZE_Init()
    {
        //select order with -1
        lang_order_index = config_data.lang_index - 1;
        LOCALIZE_NextLanguage();
    }
    #endregion

    #region [Advertise]
/*#if UNITY_EDITOR
    string admob_id = "ca-app-pub-4571968159336967/8794603533";
#elif UNITY_ANDROID
    string admob_id = "ca-app-pub-4571968159336967/8794603533";
#elif UNITY_IPHONE
    string admob_id = "ca-app-pub-4571968159336967/8794603533";
#else
    string admob_id = "unexpected_platform";
#endif*/

    /*const int ADMOB_INTERSTITIAL_PREPARE_STEP = 2;
    const int ADMOB_INTERSTITIAL_SHOW_STEP = 3;
    int admob_interstitial_step = 0;*/

    bool ad_continue_allowed = true;
    //InterstitialAd admob_interstitial;
    //AdRequest admob_request;
    bool AD_IsReadyContinue()
    {
        return ad_continue_allowed && Advertisement.IsReady();
    }
    bool AD_IsReadyDaily()
    {
        var now = System.DateTime.Now;
        return (now.Day != config_data.daily_watch_date.Day || (config_data.daily_watch_date.Hour < 3 && now.Hour >= 3)) && Advertisement.IsReady();
    }
    void AD_OnWatchContinue()
    {
        AUD_UI_Sound(UiSoundType.BUTTON);
        if (Advertisement.IsReady()) {
            var opts = new ShowOptions() { resultCallback = AD_OnWatchContinueResult };
            Advertisement.Show(null, opts);
            game_state_routine.Overlap(AD_WaitForComplete());

            //Complete time waiter to continue UI_PLAYING_CRASH_ShowUi
            ui_time_waiter.Complete(true);

            ad_continue_allowed = false;
        }
    }
    void AD_OnWatchContinueResult(ShowResult res)
    {
        if (res == ShowResult.Finished) {
            //CONTINUE
            AUD_UI_Sound(UiSoundType.BUY);
            GAME_PLAYING_CutsceneContinue();
        } else {
            GAME_PLAYING_CutsceneComplete();
        }
    }
    void AD_OnWatchDaily()
    {
        AUD_UI_Sound(UiSoundType.BUTTON);
        if (Advertisement.IsReady()) {
            var opts = new ShowOptions() { resultCallback = AD_OnWatchDailyResult };
            Advertisement.Show(null, opts);
            game_state_routine.Overlap(AD_WaitForComplete());
        }
    }
    void AD_OnWatchDailyResult(ShowResult res)
    {
        if (res == ShowResult.Finished) {
            AUD_UI_Sound(UiSoundType.BUY);

            //reward
            user_rewards_pending.Add(new UserReward() { type = UserInvItemType.LUCK, amount = reward_data.daily_vid_luck });
            user_rewards_pending.AddRange(reward_data.daily_vid.items[reward_data.daily_vid.RandomIndex()].rewards);
            USER_CheckRewardsAdded(null);

            //save date
            config_data.daily_watch_date = System.DateTime.Now;
            //hide watch item
            ui_buy_vid.SetActive(false);
        }
    }
    IEnumerator AD_WaitForComplete()
    {
        while (Advertisement.isShowing)
            yield return null;
    }
    /*void AD_AdmobInterstitialStep()
    {
        ++admob_interstitial_step;
        if(admob_interstitial_step == ADMOB_INTERSTITIAL_PREPARE_STEP) {
            admob_interstitial.LoadAd(admob_request);
        } else if(admob_interstitial_step == ADMOB_INTERSTITIAL_SHOW_STEP) {
            if (admob_interstitial.IsLoaded()) {
                admob_interstitial.Show();
            }
        } else if(admob_interstitial_step > ADMOB_INTERSTITIAL_SHOW_STEP) {
            admob_interstitial_step = 0;
        }
    }*/
    void AD_Init()
    {
        onUserLuckSpend += (int val) => ad_continue_allowed = true;
        onGameStateChanged += () => {
            if (game_state == GameState.END) {
                ad_continue_allowed = true;
                //AD_AdmobInterstitialStep();
            }
        };

        //init admob
        /*admob_interstitial = new InterstitialAd(admob_id);
        admob_request = new AdRequest.Builder()
                .AddKeyword("game")
                .SetGender(Gender.Male)
                .SetBirthday(new System.DateTime(1995, 1, 1))
                .TagForChildDirectedTreatment(false)
                .AddExtra("color_bg", "9B30FF")
                .Build();*/
    }
    #endregion


    #region [INPUT]
    const float SWIPE_THRESHOLD = 5.0f;
    const float DROPBTN_PORTRAIT_HALFX = 0.3f;
    const float DROPBTN_LANDSCAPE_HALFX = 0.15f;
    const float DROPBTN_Y = 0.3f;
    public enum SwipeDir { Tap, Left, Right, Up, Down }
    public static SwipeDir GetSwipeDirection(Vector2 diff_pos)
    {
        SwipeDir current_dir = SwipeDir.Tap;

        if (diff_pos.x > SWIPE_THRESHOLD) { //Right half
            if (diff_pos.y > SWIPE_THRESHOLD) { //RightUpper half
                current_dir = (diff_pos.x > diff_pos.y) ? SwipeDir.Right : SwipeDir.Up;
            } else if (diff_pos.y < -SWIPE_THRESHOLD) { //RightLower half
                current_dir = (diff_pos.x > -diff_pos.y) ? SwipeDir.Right : SwipeDir.Down;
            } else current_dir = SwipeDir.Right;
        } else if (diff_pos.x < -SWIPE_THRESHOLD) { //Left half
            if (diff_pos.y > SWIPE_THRESHOLD) { //LeftUpper half
                current_dir = (diff_pos.x < -diff_pos.y) ? SwipeDir.Left : SwipeDir.Up;
            } else if (diff_pos.y < -SWIPE_THRESHOLD) { //LeftLower half
                current_dir = (diff_pos.x < diff_pos.y) ? SwipeDir.Left : SwipeDir.Down;
            } else current_dir = SwipeDir.Left;
        } else {
            if (diff_pos.y > SWIPE_THRESHOLD) current_dir = SwipeDir.Up;
            else if (diff_pos.y < -SWIPE_THRESHOLD) current_dir = SwipeDir.Down;
        }
        return current_dir;
    }
    bool CheckSwipeInput(SwipeDir dir)
    {
#if MOUSE_TOUCH
        if (Input.GetMouseButtonDown(0)) {
            gesture_start_pos = Input.mousePosition;

            if (!gesture_timer.Enabled) gesture_timer.Start();
        } else if (gesture_timer_elapsed) {
            gesture_timer.Stop();
            gesture_timer_elapsed = false;

            Vector2 mouse_pos = Input.mousePosition;
            Vector2 diff_pos = mouse_pos - gesture_start_pos;
            if (GetSwipeDirection(diff_pos) == dir) return true;
        }
#else
        if (Input.touchCount > 0) {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began) {
                gesture_stationary = true;
            } else if (gesture_stationary && touch.phase == TouchPhase.Moved) {
                gesture_stationary = false;
                if (GetSwipeDirection(touch.deltaPosition) == dir) return true;
            } else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) {
                if (gesture_stationary && dir == SwipeDir.Tap) return true;
            }
        }
#endif
        return false;
    }

    int screen_width = 0;
    int screen_height = 0;
    int screen_dropbtn_halfwidth_left = 0;
    int screen_dropbtn_halfwidth_right = 0;
    int screen_dropbtn_height = 0;
    void InitScreenValues()
    {
        screen_width = Screen.width;
        screen_height = Screen.height;
        int screen_halfwidth = screen_width / 2;
        int screen_halfwidth_offset = (int)(((Screen.orientation == ScreenOrientation.Portrait || Screen.orientation == ScreenOrientation.PortraitUpsideDown) ? DROPBTN_PORTRAIT_HALFX : DROPBTN_LANDSCAPE_HALFX) * screen_halfwidth);
        screen_dropbtn_halfwidth_left = screen_halfwidth - screen_halfwidth_offset;
        screen_dropbtn_halfwidth_right = screen_halfwidth + screen_halfwidth_offset;
        screen_dropbtn_height = (int)(DROPBTN_Y * screen_height);
    }
#if MOUSE_TOUCH
    //mouse control
    float gesture_time = 70f;
    float dbltap_time = 250f;
    int dbltap_threshold = 10;
    Vector2 gesture_start_pos;
    System.Timers.Timer gesture_timer = null, dbltap_timer = null;
    bool gesture_timer_elapsed = false;
#else
    bool gesture_stationary = true;
#endif
    #endregion //[INPUT]

    #region [UnitySlots]
    protected override void Awake()
    {
        base.Awake();

#if CODEDEBUG
        string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
#endif
        UI_Init();

        next_obstacle_pos = new Vector3[NUM_RUN_LANES];
        for (int i = 0; i < NUM_RUN_LANES; ++i)
            next_obstacle_pos[i] = Vector3.zero;
        next_side_pos = new Vector3[2];
        next_side_pos[LEFT_SIDE_INDEX] = Vector3.zero;
        next_side_pos[RIGHT_SIDE_INDEX] = Vector3.zero;
        next_road_pos = Vector3.zero;

#if CODEDEBUG
        if (root_attach_nodes != null) {
            LogError(METHOD_NAME, "root_attach_nodes is not NULL");
        }
        if (theme_attach_nodes != null) {
            LogError(METHOD_NAME, "theme_attach_nodes is not NULL");
        }
        if (pattern_attach_nodes != null) {
            LogError(METHOD_NAME, "pattern_attach_nodes is not NULL");
        }
#endif
        root_attach_nodes = new Transform[NUM_ATTACH_NODES];
        theme_attach_nodes = new Transform[NUM_ATTACH_NODES];
        pattern_attach_nodes = new Transform[NUM_ATTACH_NODES];
        for (int i = 0; i < NUM_ATTACH_NODES; ++i) {
            root_attach_nodes[i] = new GameObject(string.Format("attach_node_0{0}", i)).transform;
            Transform an_tr = root_attach_nodes[i].transform;
            an_tr.SetParent(transform, false);
            an_tr.localPosition = ATTACH_NODE_POSITION;

            //add theme and pattern nodes
            theme_attach_nodes[i] = new GameObject("thm").transform;
            theme_attach_nodes[i].SetParent(an_tr, false);
            theme_attach_nodes[i].localPosition = Vector3.zero;

            pattern_attach_nodes[i] = new GameObject("ptrn").transform;
            pattern_attach_nodes[i].SetParent(an_tr, false);
            pattern_attach_nodes[i].localPosition = Vector3.zero;
        }

        hidden_node = new GameObject("hidden_node").transform;
        hidden_node.gameObject.SetActive(false);
        hidden_node.transform.parent = transform;
        hidden_node.transform.localPosition = HIDDEN_NODE_POSITION;

        playchar_passes = new PlaycharPasses[PLAYCHAR_PASSES_LENGTH];
        for (int i = 0; i < PLAYCHAR_PASSES_LENGTH; ++i) {
            playchar_passes[i] = new PlaycharPasses() {
                distance = -1
            };
        }

        //Empty Obstacle
        if (empty_obst == null) {
            var obst_shared_data = new ObstacleSharedData();
            obst_shared_data.size_in_cells = 0;
            obst_shared_data.configuration = ObstacleConfiguration.WALL;

            empty_obst = new PrefabPool() {
                num_objects = 32,
                shared_data = obst_shared_data
            };
        }
        empty_obst.Init(
#if CODEDEBUG
"empty_obstacle",
#endif
 (value) => {
     GameObject target_obj = new GameObject(string.Format("empty_obst_{0}", value));
     target_obj.AddComponent<RoadObstacleController>();
     return target_obj;
 }
            );

        //OBSTACLES
#if CODEDEBUG
        if (obst_so.obstacles == null || obst_so.obstacles.Length == 0) {
            LogError(METHOD_NAME, " obstacles is NULL");
        }
#endif
        for (int i = 0, l = obst_so.obstacles.Length; i < l; ++i) {
            obst_so.obstacles[i].Init(
#if CODEDEBUG
string.Format("obstacles[{0}]", i)
#endif
);
        }
        for (int i = 0, l = obst_so.obstacle_groups.Length; i < l; ++i) {
            var group = obst_so.obstacle_groups[i];
            if (group.items == null || group.items.Length == 0) continue;
            group.Update();
        }

        //Empty PBox
        if (empty_pbox == null) {
            empty_pbox = new PatternBox();
            empty_pbox.name = "empty";
            empty_pbox.elements = new PatternBox.Element[NUM_RUN_LANES][];
            for (int i = 0; i < empty_pbox.elements.Length; ++i) {
                empty_pbox.elements[i] = new PatternBox.Element[1];
                empty_pbox.elements[i][0] = new PatternBox.Element();
                var el = empty_pbox.elements[i][0];
                el.num_cells_before = 5;
                el.index = -1;
            }
        }
        empty_pbox.Init(
#if CODEDEBUG
"empty_pbox"
#endif
);
        //Empty PSBox
        if (empty_psbox == null) {
            empty_psbox = new PatternSuperBox();
            empty_psbox.name = "empty";
            empty_psbox.boxes = new PatternSuperBox.BoxReference[1];
            empty_psbox.boxes[0] = new PatternSuperBox.BoxReference();
        }
        empty_psbox.Init(
#if CODEDEBUG
"empty_psbox"
#endif
);

        //OVERRIDE ROADS
        for (int i = 0; i < ptrn_so.roads.Length; ++i) {
            ptrn_so.roads[i].Init(
#if CODEDEBUG
string.Format("override_roads[{0}]", i)
#endif
);
        }

        //PBOXES
#if CODEDEBUG
        if (ptrn_so.pboxes == null || ptrn_so.pboxes.Length == 0) {
            LogError(METHOD_NAME, " pattern_boxes is NULL");
        }
#endif
        for (int i = 0; i < ptrn_so.pboxes.Length; ++i) {
            var pbox = ptrn_so.pboxes[i];
            if (pbox.elements == null || pbox.elements.Length == 0) continue;
            ptrn_so.pboxes[i].Init(
#if CODEDEBUG
string.Format("pattern_boxes[{0}]", i)
#endif
);
        }
        for (int i = 0, l = ptrn_so.pbox_groups.Length; i < l; ++i) {
            var group = ptrn_so.pbox_groups[i];
            if (group.items == null || group.items.Length == 0) continue;
            group.Update();
        }

        //PSBOXES
#if CODEDEBUG
        if (ptrn_so.psboxes == null || ptrn_so.psboxes.Length == 0) {
            LogError(METHOD_NAME, " pattern_super_boxes is NULL");
        }
#endif
        for (int i = 0; i < ptrn_so.psboxes.Length; ++i) {
            ptrn_so.psboxes[i].Init(
#if CODEDEBUG
string.Format("pattern_super_boxes[{0}]", i)
#endif
);
        }
        for (int i = 0, l = ptrn_so.psbox_groups.Length; i < l; ++i) {
            var group = ptrn_so.psbox_groups[i];
            if (group.items == null || group.items.Length == 0) continue;
            group.Update();
        }

        //PATTERNS
#if CODEDEBUG
        if (ptrn_so.patterns == null || ptrn_so.patterns.Length == 0) {
            LogError(METHOD_NAME, " patterns is NULL");
        }
#endif
        for (int i = 0; i < ptrn_so.pattern_groups.Length; ++i) {
            var group = ptrn_so.pattern_groups[i];
            if (group.items == null || group.items.Length == 0) continue;
            group.Update();
        }

        /*floor = new GameObject("floor");
        floor.transform.parent = gameObject.transform;
        floor.tag = "Floor";
        MeshFilter floor_filter = floor.AddComponent<MeshFilter>();
        Mesh floor_mesh = floor_filter.mesh;
        CreateFloorMeshQuad(floor_mesh);
        BoxCollider floor_mc = floor.AddComponent<BoxCollider>();
        PhysicMaterial pm = Resources.Load<PhysicMaterial>("Resources/PhysicMaterials/Floor");
        if(pm == null) pm = new PhysicMaterial("Floor");
        floor_mc.material = pm;*/

        //Input
#if MOUSE_TOUCH
        gesture_timer = new System.Timers.Timer(gesture_time);
        gesture_timer.Enabled = false;
        gesture_timer.AutoReset = false;
        gesture_timer.Elapsed += new System.Timers.ElapsedEventHandler((object a, System.Timers.ElapsedEventArgs b) => { gesture_timer_elapsed = true; });
        dbltap_timer = new System.Timers.Timer(dbltap_time);
        dbltap_timer.Enabled = false;
        dbltap_timer.AutoReset = false;
#endif
        //Playchar Node
        GameObject playchar_node_go = new GameObject("player_origin");
        playchar_node_go.SetActive(false);
        playchar_node = playchar_node_go.transform;
        playchar_node.SetParent(transform, false);
        //camera
        run_camera_go = Instantiate(pch_so.run_camera_prefab);
        run_camera_go.transform.SetParent(playchar_node, false);

        CS_Init();

    }
    void Start()
    {
        USER_Init();

        AUD_Init();

        GAME_SwitchTo(GameState.BEGIN);
    }
    void Update()
    {
        frame_delta_time = Time.deltaTime;

        if (invoke_on_mt[invoke_cursor] != null) {
            //store old cursor
            int old_cursor = invoke_cursor;
            //switch to next to prevent add to existing invoke delegate
            invoke_cursor = 1 - invoke_cursor;
            invoke_on_mt[old_cursor]();
            invoke_on_mt[old_cursor] = null;
        }

        game_state_routine.Update();
        work_routine.Update();

        if (update_on_mt != null) { update_on_mt(); }
    }
    #endregion

    #region [GAME]
    public enum GameState { NOT_DEFINED, BEGIN, PLAYING, END, TUTORIAL }
    GameState game_state = GameState.NOT_DEFINED;
    public GameState CurrentGameState() { return game_state; }

    public event Event onGameStateChanged;
    public event Event onPatternBoxPlaced;

    public enum GamePlayingState { NONE, MAIN, CUTSCENE, PAUSE }
    GamePlayingState target_playing_state = GamePlayingState.MAIN;
    GamePlayingState playing_state = GamePlayingState.MAIN;
    public GamePlayingState CurrentPlayingState() { return playing_state; }
    public event Event onPlayingStateChanged = null;

    const string _cut_intro_origin_name = "intro_origin";
    const string _cut_continue_chaser_origin_name = "continue_chaser_origin";
    const string _cut_continue_obstacle_origin_name = "continue_obstacle_origin";
    const string _cut_rest_origin_name = "rest_origin";
    const string _cut_crash_chaser_origin_name = "crash_chaser_origin";
    const string _cut_crash_obstacle_origin_name = "crash_obstacle_origin";
    public enum CutsceneState { NONE, INTRO, REST, CONTINUE_CHASER, CONTINUE_OBSTACLE, CRASH_CHASER, CRASH_OBSTACLE }
    CutsceneState target_cut_state = CutsceneState.NONE;
    CutsceneState cut_state = CutsceneState.NONE;
    bool cut_intro_show = true;
    public CutsceneState CurrentCutsceneState() { return cut_state; }
    GameObject cut_zone_go = null;
    GameObject cut_playchar_anim_go = null;
    GameObject cut_chaser_anim_go = null;

    void GAME_SwitchTo(GameState state)
    {
        if (game_state == state) return;

        //Leave PLAYING
        playing_state = GamePlayingState.NONE;
        target_playing_state = GamePlayingState.NONE;

        switch (state) {
        case GameState.BEGIN: game_state_routine.Push(GAME_BEGIN()); break;
        case GameState.PLAYING: game_state_routine.Push(GAME_PLAYING()); break;
        case GameState.END: game_state_routine.Push(GAME_END()); break;
        case GameState.TUTORIAL: game_state_routine.Push(GAME_TUTORIAL()); break;
        }
        game_state = GameState.NOT_DEFINED;
    }
    IEnumerator GAME_BEGIN()
    {
        game_state = GameState.BEGIN;

        //Show rewards
        IEnumerator current = null;
        if (user_rewards_pending.Count > 0) {
            current = Routine.Waiter(USER_ShowRewards(false, null));
            while (current.MoveNext()) { yield return null; }
        }

        //UI
        UI_STATE_SwitchTo(UiState.BEGIN, null);
        AUD_PlayMusic(MusicType.BEGIN);

        cut_intro_show = true;

        if (onGameStateChanged != null) { onGameStateChanged(); }

        //garbage collection
        System.GC.Collect();

        /*while (game_state == GameState.BEGIN) {
            yield return null;
        }*/
    }
    void GAME_PLAYING_ShowCutscene(CutsceneState state)
    {
        if (cut_state == state || state == CutsceneState.NONE) return;

        cut_state = CutsceneState.NONE;
        target_cut_state = state;
        playing_state = GamePlayingState.NONE;
        target_playing_state = GamePlayingState.CUTSCENE;
    }
    void GAME_PLAYING_CutsceneContinue()
    {
        GAME_PLAYING_ShowCutscene(cut_state == CutsceneState.CRASH_CHASER ? CutsceneState.CONTINUE_CHASER : CutsceneState.CONTINUE_OBSTACLE);
    }
    void GAME_PLAYING_CutsceneComplete()
    {
        cut_state = CutsceneState.NONE;
    }
    void GAME_PLAYING_SetPause(bool pause)
    {
        if (playing_state == GamePlayingState.PAUSE && pause) return;
        playing_state = GamePlayingState.NONE;
        target_playing_state = pause ? GamePlayingState.PAUSE : GamePlayingState.MAIN;
    }
    IEnumerator GAME_PLAYING()
    {
        game_state = GameState.PLAYING;
        if (onGameStateChanged != null) { onGameStateChanged(); }

        if (cut_intro_show) {
            //Enter CUTSCENE INTRO
            GAME_PLAYING_ShowCutscene(CutsceneState.INTRO);
            cut_intro_show = false;
        }
        THEME_SelectSlot(selected_theme_slot_index);
        PLAYCHAR_Prepare();

        bool first_playing_main_enter = true;
        IEnumerator current = null;
        while (game_state == GameState.PLAYING) {
            switch (target_playing_state) {
            case GamePlayingState.CUTSCENE: current = GAME_PLAYING_CUTSCENE(); break;
            case GamePlayingState.PAUSE: current = GAME_PLAYING_PAUSE(); break;
            case GamePlayingState.MAIN:
            default:
                if (first_playing_main_enter) {
                    CHALLENGE_OnPlayingBegin();
                    first_playing_main_enter = false;
                }
                current = GAME_PLAYING_MAIN();
                break;
            }
            target_playing_state = GamePlayingState.NONE;
            while (current.MoveNext()) { yield return null; }
            yield return null;
        }
        //Leave SubStates
        playing_state = GamePlayingState.NONE;
        if (onPlayingStateChanged != null) { onPlayingStateChanged(); }
        if (current != null) {
            while (current.MoveNext()) { yield return null; }
        }
        //Leave PLAYING
        COINS_CompleteAllActiveCoins();
        COINS_ArcTriggerExit();

        //CHALLENGE
        CHALLENGE_OnPlayingEnd();

        //Leave PLAYING
        PLAYCHAR_HideScene(true);
    }
    IEnumerator GAME_PLAYING_MAIN()
    {
        playing_state = GamePlayingState.MAIN;

        //Playchar
        PLAYCHAR_ShowScene();
        //UI
        UI_STATE_SwitchTo(UiState.PLAYING, UiPlayingPage.MAIN);
        //AUDIO
        AUD_PLAYING_UpdateMusic();

        if (onPlayingStateChanged != null) { onPlayingStateChanged(); }

        //MAIN cycle
        while (playing_state == GamePlayingState.MAIN) {
            THEME_PlaceObjects();

            playing_delta_time = frame_delta_time * playing_time_scale;

            float distance_traveled = playchar_ctr.CurrentSpeed() * playing_delta_time;

            //move attach nodes
            for (int i = 0; i < NUM_ATTACH_NODES; ++i) {
                if (theme_attach_nodes[i].childCount > 0 || pattern_attach_nodes[i].childCount > 0) {
                    root_attach_nodes[i].MoveLocalPositionZ(-distance_traveled);
                } else {
                    root_attach_nodes[i].localPosition = ATTACH_NODE_POSITION;
                }

                if (current_attach_node_index == i && root_attach_nodes[i].localPosition.z < LANE_NEAR_Z) {
                    //we have past behind attach node, so switch attach node
                    if (++current_attach_node_index >= NUM_ATTACH_NODES)
                        current_attach_node_index = 0;
                }
            }

            next_road_pos.z -= distance_traveled;
            next_side_pos[LEFT_SIDE_INDEX].z -= distance_traveled;
            next_side_pos[RIGHT_SIDE_INDEX].z -= distance_traveled;
            for (int i = 0; i < NUM_RUN_LANES; ++i) {
                next_obstacle_pos[i].z -= distance_traveled;
            }

            //pp
            if (pp_active) { PP_Travel(distance_traveled); }

            //approach obstacles
            if (appobst_serie_active) {
                appobst_serie_offset -= distance_traveled;
            }

            //score calculation
            pch_playing_score += distance_traveled * active_scorex_mult;

            if (update_on_mt_playing != null) { update_on_mt_playing(); }

            //INPUT
            if (Screen.width != screen_width) {
                InitScreenValues();
            }
#if MOUSE_TOUCH
            if (Input.GetMouseButtonDown(0)) {
                Vector2 mouse_pos = Input.mousePosition;
                Vector2 diff_pos = mouse_pos - gesture_start_pos;

                if (chaser_ctr.IsOnScreen() && mouse_pos.x > screen_dropbtn_halfwidth_left && mouse_pos.x < screen_dropbtn_halfwidth_right && mouse_pos.y < screen_dropbtn_height) {
                    PLAYCHAR_OnDropButton();
                } else if (dbltap_timer.Enabled && System.Math.Abs(diff_pos.x) < dbltap_threshold && System.Math.Abs(diff_pos.y) < dbltap_threshold) {
                    playchar_input.INPUT_DoubleTap();
                    dbltap_timer.Stop();
                } else {
                    gesture_start_pos = mouse_pos;
                    dbltap_timer.Start();

                    if (!gesture_timer.Enabled) gesture_timer.Start();
                }
            } else if (gesture_timer_elapsed) {
                gesture_timer.Stop();
                gesture_timer_elapsed = false;

                //Process Input
                Vector2 mouse_pos = Input.mousePosition;
                Vector2 diff_pos = mouse_pos - gesture_start_pos;
                switch (GetSwipeDirection(diff_pos)) {
                case SwipeDir.Left:
                    playchar_input.INPUT_SwipeLeft();
                    break;
                case SwipeDir.Right:
                    playchar_input.INPUT_SwipeRight();
                    break;
                case SwipeDir.Up:
                    playchar_input.INPUT_SwipeUp();
                    break;
                case SwipeDir.Down:
                    playchar_input.INPUT_SwipeDown();
                    break;
                }
            }
#else
            if (Input.touchCount > 0) {
                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began) {
                    gesture_stationary = true;
                } else if (gesture_stationary && touch.phase == TouchPhase.Moved) {
                    switch (GetSwipeDirection(touch.deltaPosition)) {
                    case SwipeDir.Left:
                        playchar_input.INPUT_SwipeLeft();
                        gesture_stationary = false;
                        break;
                    case SwipeDir.Right:
                        playchar_input.INPUT_SwipeRight();
                        gesture_stationary = false;
                        break;
                    case SwipeDir.Up:
                        playchar_input.INPUT_SwipeUp();
                        gesture_stationary = false;
                        break;
                    case SwipeDir.Down:
                        playchar_input.INPUT_SwipeDown();
                        gesture_stationary = false;
                        break;
                    }
                } else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) {
                    if (gesture_stationary) {
                        Vector2 mouse_pos = touch.position;
                        if (chaser_ctr.IsOnScreen() && mouse_pos.x > screen_dropbtn_halfwidth_left && mouse_pos.x < screen_dropbtn_halfwidth_right && mouse_pos.y < screen_dropbtn_height) {
                            PLAYCHAR_OnDropButton();
                        } else if (touch.tapCount > 1) {
                            playchar_input.INPUT_DoubleTap();
                        }
                    }
                }
            }
#endif
            yield return null;
        }

        //Leave MAIN
        playing_delta_time = 0f;
    }
    IEnumerator GAME_PLAYING_CUTSCENE()
    {
        playing_state = GamePlayingState.CUTSCENE;
        CutsceneState last_cut_state = cut_state = target_cut_state;
        target_cut_state = CutsceneState.NONE;

        PLAYCHAR_HideScene(false);
        switch (cut_state) {
        case CutsceneState.REST:
            COINS_ArcTriggerExit();
            UI_STATE_SwitchTo(UiState.PLAYING, UiPlayingPage.RESTCUT);
            AddWorkRoutine(Routine.Waiter(UI_PLAYING_REST_ShowResults()), true);
            USER_Playing_2_Rest();
            break;
        case CutsceneState.CRASH_CHASER:
        case CutsceneState.CRASH_OBSTACLE:
            COINS_CompleteAllActiveCoins();
            COINS_ArcTriggerExit();
            UI_STATE_SwitchTo(UiState.PLAYING, UiPlayingPage.CRASHCUT);
            AddWorkRoutine(Routine.Waiter(UI_PLAYING_CRASH_ShowUi()), true);
            break;
        default:
            UI_STATE_SwitchTo(UiState.PLAYING, UiPlayingPage.CUTSCENE);
            break;
        }
        AUD_CUTSCENE_PlayMusic();

        int current_theme_index = THEME_CurrentIndex();
        string cs_origin_name = _cut_intro_origin_name;
        GameObject cut_playchar_anim_prefab = null;
        GameObject cut_chaser_anim_prefab = null;
        GameObject cut_zone_prefab = null;
        switch (cut_state) {
        case CutsceneState.INTRO:
            cut_playchar_anim_prefab = SelectedPlaychar().anim_intro;
            cut_chaser_anim_prefab = SelectedPlaycharSlot().anim_intro;
            if (!SelectedPlaycharSlot().override_intro_zone) { cut_zone_prefab = thm_so.themes[current_theme_index].cutscene_intro_zone_prefab; }
            cs_origin_name = _cut_intro_origin_name;
            break;
        case CutsceneState.CONTINUE_OBSTACLE:
            cut_playchar_anim_prefab = SelectedPlaychar().anim_continue_obstacle;
            cut_chaser_anim_prefab = SelectedPlaycharSlot().anim_continue_obstacle;
            cut_zone_prefab = PP_GetCurrentZones().cutscene_continue_obstacle_zone_prefab;
            cs_origin_name = _cut_continue_obstacle_origin_name;
            break;
        case CutsceneState.CONTINUE_CHASER:
            cut_playchar_anim_prefab = SelectedPlaychar().anim_continue_chaser;
            cut_chaser_anim_prefab = SelectedPlaycharSlot().anim_continue_chaser;
            cut_zone_prefab = PP_GetCurrentZones().cutscene_continue_chaser_zone_prefab;
            cs_origin_name = _cut_continue_chaser_origin_name;
            break;
        case CutsceneState.REST:
            cut_playchar_anim_prefab = SelectedPlaychar().anim_rest;
            cut_chaser_anim_prefab = SelectedPlaycharSlot().anim_rest;
            cut_zone_prefab = PP_GetCurrentZones().cutscene_rest_zone_prefab;
            cs_origin_name = _cut_rest_origin_name;
            break;
        case CutsceneState.CRASH_CHASER:
            cut_playchar_anim_prefab = SelectedPlaychar().anim_crash_chaser;
            cut_chaser_anim_prefab = SelectedPlaycharSlot().anim_crash_chaser;
            cut_zone_prefab = PP_GetCurrentZones().cutscene_crash_chaser_zone_prefab;
            cs_origin_name = _cut_crash_chaser_origin_name;
            break;
        case CutsceneState.CRASH_OBSTACLE:
            cut_playchar_anim_prefab = SelectedPlaychar().anim_crash_obstacle;
            cut_chaser_anim_prefab = SelectedPlaycharSlot().anim_crash_obstacle;
            cut_zone_prefab = PP_GetCurrentZones().cutscene_crash_obstacle_zone_prefab;
            cs_origin_name = _cut_crash_obstacle_origin_name;
            break;
        }
        cut_playchar_anim_go = Instantiate(cut_playchar_anim_prefab);
        cut_chaser_anim_go = Instantiate(cut_chaser_anim_prefab);
        Transform cs_origin = null;
        if (cut_zone_prefab != null) {
            cut_zone_go = Instantiate(cut_zone_prefab);
            //find origin on cut_zone_go
            cs_origin = cut_zone_go.transform.Find(cs_origin_name);
#if CODEDEBUG
            if (cs_origin == null) {
                string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
                LogError(METHOD_NAME, "requested state is {0}, {1} not found in theme[{2}]", cut_state, cs_origin_name, current_theme_index);
            }
#endif
            //attach chaser to origin
            cut_chaser_anim_go.transform.SetParent(cs_origin, false);
        } else {
            //find origin on cut_chaser_anim_go
            cs_origin = cut_chaser_anim_go.transform.Find(cs_origin_name);
        }
        if (cs_origin != null) {
            //attach playchar to origin
            cut_playchar_anim_go.transform.SetParent(cs_origin, false);
        }
#if CODEDEBUG
 else {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            LogWarning(METHOD_NAME, "requested state is {0}, {1} not found", cut_state, cs_origin_name);
        }
#endif
        float anim_time = cut_playchar_anim_go.GetComponent<Animation>().StateAt(0).length + Time.realtimeSinceStartup + 0.1f;

        //report state changed
        if (onPlayingStateChanged != null) { onPlayingStateChanged(); }

        //wait cutscene
        while (cut_state != CutsceneState.NONE && Time.realtimeSinceStartup < anim_time) {
            yield return null;
        }
        cut_state = CutsceneState.NONE;

        //destroy cutscene_player_anim_go
        if (cut_playchar_anim_go != null) { GameObject.Destroy(cut_playchar_anim_go); cut_playchar_anim_go = null; }
        if (cut_chaser_anim_go != null) { GameObject.Destroy(cut_chaser_anim_go); cut_chaser_anim_go = null; }
        if (cut_zone_go != null) { GameObject.Destroy(cut_zone_go); cut_zone_go = null; }

        if (target_playing_state == GamePlayingState.NONE) {
            switch (last_cut_state) {
            case CutsceneState.CRASH_CHASER:
            case CutsceneState.CRASH_OBSTACLE:
                GAME_SwitchTo(GameState.END);
                break;
            default:
                target_playing_state = (target_cut_state != CutsceneState.NONE) ? GamePlayingState.CUTSCENE : GamePlayingState.MAIN;
                break;
            }
        }
    }
    IEnumerator GAME_PLAYING_PAUSE()
    {
        playing_state = GamePlayingState.PAUSE;

        UI_STATE_SwitchTo(UiState.PLAYING, UiPlayingPage.PAUSE);

        if (onPlayingStateChanged != null) { onPlayingStateChanged(); }

        while (playing_state == GamePlayingState.PAUSE) {
            yield return null;
        }

        //leave
        if (target_playing_state == GamePlayingState.MAIN) {
            //AUDIO
            AUD_UI_Sound(UiSoundType.UNPAUSE);

            AddWorkRoutine(GAME_PLAYING_PAUSE_UnpauseAnimation(), true);
        }
    }
    IEnumerator GAME_PLAYING_PAUSE_UnpauseAnimation()
    {
        //prepare
        var playchar_anim = playchar_go.GetComponentsInChildren<AdvancedAnimationController>(true)[0];
        var chaser_anim = chaser_go.GetComponentsInChildren<AdvancedAnimationController>(true)[0];

        float current_time = AdvancedAnimationController.MIN_SPEED;
        float duration = 2f;
        //tween
        while ((current_time += Time.deltaTime) < duration) {
            float time_norm_value = current_time / duration;
            playing_time_scale = time_norm_value;
            playchar_anim.layers[0].SetSequenceSpeedMultOverride(time_norm_value);
            chaser_anim.layers[0].SetSequenceSpeedMultOverride(time_norm_value);
            yield return null;
        }

        //leave
        playchar_anim.layers[0].SetSequenceSpeedMultOverride(0f);
        chaser_anim.layers[0].SetSequenceSpeedMultOverride(0f);
        playing_time_scale = 1f;
    }
    IEnumerator GAME_END()
    {
        game_state = GameState.END;
        if (onGameStateChanged != null) onGameStateChanged();

        PLAYCHAR_DeactivateAllStartItems();

        //calc values
        int total_score = (int)pch_playing_score + PLAYCHAR_ScoreByCoins(pch_coins_nb_collected);
        if (onUserScore != null) { onUserScore(total_score); }
        us.coins += pch_coins_b_collected;
        bool highscore_changed = false;
        if (total_score > us.highscore) {
            us.highscore = total_score;
            us_score_needs_save = highscore_changed = true;
        }
        USER_OnUserStateChanged();

        //Show rewards
        IEnumerator current = null;
        if (user_rewards_pending.Count > 0) {
            current = Routine.Waiter(USER_ShowRewards(false, null));
            while (current.MoveNext()) { yield return null; }
        }

        //show highscore scene
        if (highscore_changed) {
            current = Routine.Waiter(UI_RewardShowHighscore());
            while (current.MoveNext()) { yield return null; }
        }

        UI_MENU_SetInteractable(false);
        UI_STATE_SwitchTo(UiState.END, UiEndPage.MAIN);
        AUD_PlayMusic(MusicType.END);
        //Show results begin
        (current = Routine.Waiter(UI_END_ShowResults())).MoveNext();

        //begin saving after showing started
        USER_SaveState(true);

        //call after save - remote data update
        if (highscore_changed && onUserHighscoreChanged != null) { onUserHighscoreChanged(); }

        //clear values
        pch_coins_b_collected = 0;
        pch_coins_nb_collected = 0;
        pch_luck_collected = 0;
        pch_playing_score = 0;

        //continue showing results
        while (current.MoveNext()) { yield return null; }
        UI_MENU_SetInteractable(true);

#if UNITY_ANDROID
        //activate hardware back button
        AddUpdateOnMT(_UI_HardwareBtnBackUpdate);
#endif

        /*while (game_state == GameState.END) {
            yield return null;
        }*/
    }

    #endregion //[GAME]

    #region [PLAYCHAR]
    /*[SetInEditor]*/
    public PlaycharScriptableObject pch_so = null;
    int selected_playchar_slot_index = 0;
    int selected_playchar_index_in_slot = 0;
    GameObject playchar_go = null;
    GameObject chaser_go = null;
    PlayerController playchar_ctr = null;
    ChaserController chaser_ctr = null;
    Transform playchar_node = null;
    public Transform PlaycharAttachNode() { return playchar_node; }
    public int PlaycharIndexAt(int slot_index, int index_in_slot) { return pch_so.playchar_slots[slot_index].playchars[index_in_slot].playchar_index; }
    public int NumPlaycharsInSlot(int slot_index) { return pch_so.playchar_slots[slot_index].playchars.Length; }
    public bool IsPlaycharSelected() { return selected_playchar_slot_index >= 0 && selected_playchar_index_in_slot >= 0; }
    //public int SelectedPlaycharSlotIndex() { return selected_playchar_slot_index; }
    //public int SelectedPlaycharIndexInSlot() { return selected_playchar_index_in_slot; }
    public PlaycharSlot SelectedPlaycharSlot() { return pch_so.playchar_slots[selected_playchar_slot_index]; }
    public int SelectedPlaycharIndex() { return PlaycharIndexAt(selected_playchar_slot_index, selected_playchar_index_in_slot); }
    public Playchar SelectedPlaychar() { return pch_so.playchars[SelectedPlaycharIndex()]; }
    public int SelectedPlaycharLevel(PlaycharLevelType type) { return us.levels[selected_playchar_slot_index].LevelFor(type); }
    public bool IsPlaycharPlaced() { return playchar_ctr != null; }
    public PlayerController PlaycharCtrl() { return playchar_ctr; }
    public ChaserController ChaserCtrl() { return chaser_ctr; }
    public float SelectedPlaycharMagnetTime() { return SelectedPlaycharSlot().magnet_time_levels[SelectedPlaycharLevel(PlaycharLevelType.MAG_TIME)]; }
    public float SelectedPlaycharScorexTime() { return SelectedPlaycharSlot().scorex_time_levels[SelectedPlaycharLevel(PlaycharLevelType.SCOREX_TIME)]; }
    public float SelectedPlaycharCoinsxTime() { return SelectedPlaycharSlot().coinsx_time_levels[SelectedPlaycharLevel(PlaycharLevelType.COINSX_TIME)]; }
    public float SelectedPlaycharLuckxTime() { return SelectedPlaycharSlot().luckx_time_levels[SelectedPlaycharLevel(PlaycharLevelType.LUCKX_TIME)]; }
    public float SelectedPlaycharStaminaTime() { return SelectedPlaycharSlot().stamina_decrease_time_levels[SelectedPlaycharLevel(PlaycharLevelType.STAMINA_DECTIME)]; }
    public float SelectedPlaycharDropPower() { return SelectedPlaycharSlot().drop_power_levels[SelectedPlaycharLevel(PlaycharLevelType.DROP_POWER)]; }
    public GameObject SelectedUiPlaychar()
    {
        var playchar = SelectedPlaychar();
        if (playchar.ui_playchar_go == null) {
            playchar.ui_playchar_go = Instantiate(playchar.ui_playchar_prefab);
        }
        return playchar.ui_playchar_go;
    }

    public event Event onPlaycharReady;
    public event Event onPlaycharReleasing;
    public event Event onPlaycharReleased;

    public IPlaycharChallenge PlaycharChallengeEvents() { return playchar_ctr; }
    public IChaserChallengeEvents ChaserChallengeEvents() { return chaser_ctr; }
    IPlaycharInput playchar_input = null;

    GameObject run_camera_go = null;
    GameObject bg_go = null;

    //Particles
    const float PAR_BUMP_XOFFSET = 0.5f;
    const float PAR_DROP_YOFFSET = 0.75f;
    ParticleHolder par_playchar_lucky = null;
    ParticleHolder par_playchar_bump = null;
    ParticleHolder par_playchar_land = null;
    ParticleHolder par_playchar_crash_continue = null;
    ParticleHolder[] par_playchar_drop = null;
    IParticles par_playchar_drop_active = null;
    ParticleHolder par_chaser_land = null;
    ParticleHolder par_chaser_crash = null;
    ParticleHolder par_chaser_crashlose = null;
    ParticleHolder[] par_chaser_dropslide = null;
    IParticles par_chaser_dropslide_active = null;
    void PLAYCHAR_StopAllParticles()
    {
        par_playchar_lucky.Stop();
        par_playchar_bump.Stop();
        par_playchar_land.Stop();
        par_playchar_drop_active.Stop();
        par_playchar_crash_continue.Stop();
        par_chaser_land.Stop();
        par_chaser_crash.Stop();
        par_chaser_crashlose.Stop();
        par_chaser_dropslide_active.Stop();
    }

    void PLAYCHAR_Select(int slot_index, int index_in_slot)
    {
        selected_playchar_slot_index = slot_index;
        selected_playchar_index_in_slot = index_in_slot;
    }
    void PLAYCHAR_Prepare()
    {
        if (playchar_ctr != null) {
            return;
        }
        //instantiate player
        playchar_go = Instantiate(SelectedPlaychar().player_prefab);
        playchar_go.transform.SetParent(playchar_node, false);
        playchar_ctr = playchar_go.GetComponent<PlayerController>();
        CS_SetCharMaterial(0, playchar_go.GetComponentsInChildren<SkinnedMeshRenderer>(true)[0].material);
        //instantiate chaser
        chaser_go = Instantiate(SelectedPlaycharSlot().chaser_prefab);
        chaser_go.transform.SetParent(playchar_node, false);
        chaser_ctr = chaser_go.GetComponent<ChaserController>();
        CS_SetCharMaterial(1, chaser_go.GetComponentsInChildren<SkinnedMeshRenderer>(true)[0].material);

        //player input handler
        playchar_input = playchar_ctr;

#if CODEDEBUG
        if (SelectedPlaycharSlot().ui_devgui_prefab != null) {
            ui_playchar_devgui_go = Instantiate(SelectedPlaycharSlot().ui_devgui_prefab);
            ui_playchar_devgui_go.transform.SetParent(ui_playing_main_root_go.transform, false);
            playchar_ctr.SetDevGui(ui_playchar_devgui_go);
        }
#endif
        //events
        playchar_ctr.onPlayerCrash += PLAYCHAR_OnCrash;
        playchar_ctr.onRunSpeedChanged += PLAYCHAR_OnRunSpeedChanged;
        playchar_ctr.onLucky += PLAYCHAR_OnLuckyEffectDelayed;
        playchar_ctr.onSideBump += PLAYCHAR_OnSideBump;
        playchar_ctr.onLand += PLAYCHAR_OnPlaycharLand;
        playchar_ctr.onPosStateChanged += PLAYCHAR_OnPlaycharPosStateChanged;
        playchar_ctr.onStrafeStateChanged += PLAYCHAR_OnPlaycharStrafeStateChanged;
        playchar_ctr.onSlideStateChanged += PLAYCHAR_OnPlaycharSlideStateChanged;
        playchar_ctr.onStumble += PLAYCHAR_OnPlaycharStumble;
        playchar_ctr.onStaminaChanged += PLAYCHAR_OnPlaycharStaminaStateChanged;

        chaser_ctr.onLand += PLAYCHAR_OnChaserLand;
        chaser_ctr.onCrash += PLAYCHAR_OnChaserCrash;
        chaser_ctr.onCrashLose += PLAYCHAR_OnChaserCrashLose;
        chaser_ctr.onChaseStateChanged += PLAYCHAR_OnChaserStateChanged;
        chaser_ctr.onSideBump += PLAYCHAR_OnChaserBump;
        chaser_ctr.onDropHit += PLAYCHAR_OnChaserDropHit;

        //tell player individually
        playchar_ctr.GC_PlayerPlaced();

        //coins
        PLAYCHAR_PrepareCoins();
        //handitems
        PLAYCHAR_PrepareHandItems();

        PLAYCHAR_UpdateTotalLuck();
        PLAYCHAR_PrepareParticles();
        PLAYCHAR_PrepareAudio();

        //tell everyone
        if (onPlaycharReady != null) { onPlaycharReady(); }
    }
    void PLAYCHAR_PrepareCoins()
    {
        coins_supercoin_placers_active.Clear();

        Transform coins_node = playchar_ctr.CoinParticleNode();
#if CODEDEBUG
        if (coins_node == null) {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            LogError(METHOD_NAME, "coins_node is NULL");
            return;
        }
#endif

        //small coin
        coins_small_place = InitSmallCoin();
        coins_small_particles.Root().SetParent(coins_node, false);

        //super coins
        for (int i = 0, l = coins_so.super_coins.Length; i < l; ++i) {
            CoinType coin_type = coins_so.super_coins[i];
            SuperCoinPlacer coin_placer = InitSuperCoin(coin_type);
            if (coin_placer == null) {
#if CODEDEBUG
                string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
                LogError(METHOD_NAME, "placer for type {0} is NULL", coin_type);
#endif
                continue;
            }
            Transform coin_particle_tr = COINS_GetSupercoinParticles(coin_type).Root();

            coins_supercoin_placers_active.Add(coin_placer);
            coin_particle_tr.SetParent(coins_node, false);
        }
    }
    void PLAYCHAR_RemoveCoins()
    {
        //remove particles
        Transform coins_node = playchar_ctr.CoinParticleNode();
        for (int i = 0, l = coins_node.childCount; i < l; ++i) {
            coins_node.GetChild(0).SetParent(hidden_node, false);
        }

        coins_supercoin_placers_active.Clear();
    }
    void PLAYCHAR_PrepareHandItems()
    {
        if (handitem_magnet_tr == null) {
            handitem_magnet_tr = Instantiate(coins_so.hand_magnet_prefab).transform;
            handitem_magnet_tr.SetParent(hidden_node, false);
        }
    }
    void PLAYCHAR_RemoveHandItems()
    {
        handitem_magnet_tr.SetParent(hidden_node, false);
    }
    void PLAYCHAR_PrepareAudio()
    {
        var playchar_slot = SelectedPlaycharSlot();
        MasterAudio.GrabGroup(AUDIOGROUP_CHASER_DROPHIT).transform.GetChild(0).GetComponent<AudioSource>().clip = playchar_slot.aud_chaser_drophit_clip;
        MasterAudio.GrabGroup(AUDIOGROUP_CHASER_DROPSLIDE).transform.GetChild(0).GetComponent<AudioSource>().clip = playchar_slot.aud_chaser_dropslide_clip;
    }
    void PLAYCHAR_PrepareParticles()
    {
        Transform playchar_particles_root = playchar_ctr.ParticlesNode();
        //playchar lucky
        if (par_playchar_lucky == null) {
            par_playchar_lucky = new ParticleHolder();
            par_playchar_lucky.SetParticles(Instantiate(pch_so.par_playchar_lucky_prefab).transform);
        }
        par_playchar_lucky.root.SetParent(playchar_particles_root, false);
        //playchar bump
        if (par_playchar_bump == null) {
            par_playchar_bump = new ParticleHolder();
            par_playchar_bump.SetParticles(Instantiate(pch_so.par_playchar_bump_prefab).transform);
        }
        par_playchar_bump.root.SetParent(playchar_particles_root, false);
        //playchar land
        if (par_playchar_land == null) {
            par_playchar_land = new ParticleHolder();
            par_playchar_land.SetParticles(Instantiate(pch_so.par_playchar_land_prefab).transform);
        }
        par_playchar_land.root.SetParent(playchar_particles_root, false);
        //playchar drop
        if (par_playchar_drop == null) {
            par_playchar_drop = new ParticleHolder[pch_so.playchar_slots.Length];
        }
        if (par_playchar_drop[selected_playchar_slot_index] == null) {
            par_playchar_drop[selected_playchar_slot_index] = new ParticleHolder();
            par_playchar_drop[selected_playchar_slot_index].SetParticles(Instantiate(SelectedPlaycharSlot().par_playchar_drop_prefab).transform);
        }
        par_playchar_drop_active = par_playchar_drop[selected_playchar_slot_index];
        par_playchar_drop_active.Root().SetParent(playchar_particles_root, false);
        par_playchar_drop_active.Root().SetLocalPositionY(PAR_DROP_YOFFSET);
        //playchar crash continue
        if (par_playchar_crash_continue == null) {
            par_playchar_crash_continue = new ParticleHolder();
            par_playchar_crash_continue.SetParticles(Instantiate(pch_so.par_playchar_crash_continue_prefab).transform);
        }
        par_playchar_crash_continue.root.SetParent(playchar_particles_root, false);

        Transform chaser_particles_root = chaser_ctr.ParticlesNode();
        //chaser land
        if (par_chaser_land == null) {
            par_chaser_land = new ParticleHolder();
            par_chaser_land.SetParticles(Instantiate(pch_so.par_chaser_land_prefab).transform);
        }
        par_chaser_land.root.SetParent(chaser_particles_root, false);
        //chaser crash
        if (par_chaser_crash == null) {
            par_chaser_crash = new ParticleHolder();
            par_chaser_crash.SetParticles(Instantiate(pch_so.par_chaser_crash_prefab).transform);
        }
        par_chaser_crash.root.SetParent(chaser_particles_root, false);
        //chaser crash lose
        if (par_chaser_crashlose == null) {
            par_chaser_crashlose = new ParticleHolder();
            par_chaser_crashlose.SetParticles(Instantiate(pch_so.par_chaser_crashlose_prefab).transform);
        }
        par_chaser_crashlose.root.SetParent(playchar_ctr.ParticlesNode(), false);
        //chaser drop slide
        if (par_chaser_dropslide == null) {
            par_chaser_dropslide = new ParticleHolder[pch_so.playchar_slots.Length];
        }
        if (par_chaser_dropslide[selected_playchar_slot_index] == null) {
            par_chaser_dropslide[selected_playchar_slot_index] = new ParticleHolder();
            par_chaser_dropslide[selected_playchar_slot_index].SetParticles(Instantiate(SelectedPlaycharSlot().par_chaser_slide_prefab).transform);
        }
        par_chaser_dropslide_active = par_chaser_dropslide[selected_playchar_slot_index];
        par_chaser_dropslide_active.Root().SetParent(chaser_particles_root, false);
    }
    void PLAYCHAR_RemoveParticles()
    {
        par_playchar_lucky.root.SetParent(hidden_node, false);
        par_playchar_bump.root.SetParent(hidden_node, false);
        par_playchar_land.root.SetParent(hidden_node, false);
        par_playchar_drop_active.Root().SetParent(hidden_node, false);
        par_playchar_crash_continue.root.SetParent(hidden_node, false);

        par_chaser_land.root.SetParent(hidden_node, false);
        par_chaser_crash.root.SetParent(hidden_node, false);
        par_chaser_crashlose.root.SetParent(hidden_node, false);
        par_chaser_dropslide_active.Root().SetParent(hidden_node, false);
    }
    void PLAYCHAR_ShowScene()
    {
        playchar_node.gameObject.SetActive(true);

        //ATTACH NODES
        for (int i = 0; i < NUM_ATTACH_NODES; ++i) {
            root_attach_nodes[i].gameObject.SetActive(true);
        }

        //Background
        bg_go.SetActive(true);

        PP_WriteToCurrent(current_theme.cutscene_zones);
    }
    void PLAYCHAR_HideScene(bool destroyPlaychar)
    {
        //stop all particles
        PLAYCHAR_StopAllParticles();
        COINS_StopAllParticles();
        //stop all sounds
        MasterAudio.StopBus(AUDIOBUS_PLAYCHAR);
        MasterAudio.StopBus(AUDIOBUS_CHASER);
        MasterAudio.StopBus(AUDIOBUS_COINS);

        if (destroyPlaychar) {
            //tell everyone
            if (onPlaycharReleasing != null) onPlaycharReleasing();

            PLAYCHAR_RemoveParticles();
            if (playchar_ctr != null) {
                //coins
                PLAYCHAR_RemoveCoins();
                PLAYCHAR_RemoveHandItems();

                //events
                playchar_ctr.onPlayerCrash -= PLAYCHAR_OnCrash;
                playchar_ctr.onRunSpeedChanged -= PLAYCHAR_OnRunSpeedChanged;
                playchar_ctr.onLucky -= PLAYCHAR_OnLuckyEffectDelayed;
                playchar_ctr.onSideBump -= PLAYCHAR_OnSideBump;
                playchar_ctr.onLand -= PLAYCHAR_OnPlaycharLand;
                playchar_ctr.onPosStateChanged -= PLAYCHAR_OnPlaycharPosStateChanged;
                playchar_ctr.onStrafeStateChanged -= PLAYCHAR_OnPlaycharStrafeStateChanged;
                playchar_ctr.onSlideStateChanged -= PLAYCHAR_OnPlaycharSlideStateChanged;
                playchar_ctr.onStumble -= PLAYCHAR_OnPlaycharStumble;
                playchar_ctr.onStaminaChanged -= PLAYCHAR_OnPlaycharStaminaStateChanged;

                //tell player individually
                playchar_ctr.GC_PlayerRemoving();
                //player removes events automatically;
#if CODEDEBUG
                //remove playchar devgui
                if(ui_playchar_devgui_go != null) {
                    GameObject.Destroy(ui_playchar_devgui_go);
                    ui_playchar_devgui_go = null;
                }
#endif
                //remove player
                GameObject.Destroy(playchar_go);
            }
            playchar_go = null;
            playchar_ctr = null;
            playchar_input = null;

            if (chaser_ctr != null) {
                //events
                chaser_ctr.onLand -= PLAYCHAR_OnChaserLand;
                chaser_ctr.onCrash -= PLAYCHAR_OnChaserCrash;
                chaser_ctr.onCrashLose -= PLAYCHAR_OnChaserCrashLose;
                chaser_ctr.onChaseStateChanged -= PLAYCHAR_OnChaserStateChanged;
                chaser_ctr.onSideBump -= PLAYCHAR_OnChaserBump;
                chaser_ctr.onDropHit -= PLAYCHAR_OnChaserDropHit;

                GameObject.Destroy(chaser_go);
            }
            chaser_go = null;
            chaser_ctr = null;

            //tell everyone
            if (onPlaycharReleased != null) onPlaycharReleased();
        }
        playchar_node.gameObject.SetActive(false);

        //ATTACH NODES
        THEME_ReleaseAllThemeElements();
        THEME_ReleaseAllPatternElements();
        for (int i = 0; i < NUM_ATTACH_NODES; ++i) {
            root_attach_nodes[i].gameObject.SetActive(false);
            root_attach_nodes[i].localPosition = ATTACH_NODE_POSITION;
        }
        current_attach_node_index = 0;

        //reset NEXT Z values
        THEME_ResetThemeNextZValues();
        THEME_ResetPatternNextZValues();

        //Background
        if (bg_go != null) { bg_go.SetActive(false); }

        THEME_ClearTempValues();
    }
    void PLAYCHAR_OnCrash(bool crash_by_chaser)
    {
        if (crash_by_chaser || playchar_ctr.IsTired()) {
            //game over
            GAME_PLAYING_ShowCutscene(crash_by_chaser ? CutsceneState.CRASH_CHASER : CutsceneState.CRASH_OBSTACLE);
        } else {
            //remove pbox
            THEME_ReleaseAllPatternElements();
            //reset NEXT Z values
            //THEME_ResetPatternNextZValues();
            //select next box
            if (onPatternBoxPlaced != null) { onPatternBoxPlaced(); }
            THEME_PatternSelectNextBox();
            //exit arc
            COINS_ArcTriggerExit();
            //active coins
            COINS_CompleteAllActiveCoins();

            //particles
            par_playchar_crash_continue.Play();

            //AUDIO
            MasterAudio.PlaySound(AUDIOGROUP_CHASER_CRASH_LOSE);

            //Slow motion
            AddWorkRoutine(GAME_PLAYING_PAUSE_UnpauseAnimation(), true);
            //stamina
            AddWorkRoutine(UI_PLAYING_CRASHCONTINUE_ShowUi(), false);
        }
    }
    void PLAYCHAR_OnDropButton()
    {
        if (us.drops[selected_playchar_slot_index] > 0 && playchar_ctr.INPUT_DropButton()) {
            --us.drops[selected_playchar_slot_index];

            //UI
            ui_playing_main_drops_value_txt.text = us.drops[selected_playchar_slot_index].ToString();

            //particles
            par_playchar_drop_active.Play();

            //AUDIO
            MasterAudio.PlaySound(AUDIOGROUP_PLAYCHAR_DROP);
        }
    }
    void PLAYCHAR_OnRunSpeedChanged(float curveSample)
    {
        //coins
        coins_wait_time = coins_so.coins_show_time_begin + (curveSample * (coins_so.coins_show_time_end - coins_so.coins_show_time_begin));
    }
    void PLAYCHAR_OnLuckyEffectDelayed()
    {
        ui_timer.Complete(true);
        ui_timer.SetOnCompleteOnce(PLAYCHAR_OnLuckyEffectNow);
        ui_timer.Reset(1f);
    }
    void PLAYCHAR_OnLuckyEffectNow()
    {
        //particles
        par_playchar_lucky.Play();

        //AUDIO
        MasterAudio.PlaySound(AUDIOGROUP_PLAYCHAR_LUCKY);
    }
    void PLAYCHAR_OnSideBump(ObstacleController obst)
    {
        //particles
        par_playchar_bump.root.SetLocalPositionX((playchar_ctr.IsStrafingTo() == StrafeTo.LEFT) ? -PAR_BUMP_XOFFSET : PAR_BUMP_XOFFSET);
        par_playchar_bump.Play();

        //AUDIO
        MasterAudio.PlaySound(AUDIOGROUP_PLAYCHAR_BUMP);
    }
    void PLAYCHAR_OnPlaycharLand(float fall_height)
    {
        //particles
        par_playchar_land.Play();

        //AUDIO
        if (!chaser_ctr.IsOnScreen()) {
            MasterAudio.PlaySound(AUDIOGROUP_PLAYCHAR_LAND);
        }
    }
    void PLAYCHAR_OnPlaycharPosStateChanged(PosState state)
    {
        //AUDIO
        switch (state) {
        case PosState.JUMP_RISING:
            MasterAudio.PlaySound(AUDIOGROUP_PLAYCHAR_JUMP);
            break;
        }
    }
    void PLAYCHAR_OnPlaycharStrafeStateChanged(StrafeTo state)
    {
        //AUDIO
        if (state != StrafeTo.NONE) {
            MasterAudio.PlaySound(AUDIOGROUP_PLAYCHAR_STRAFE);
        }
    }
    void PLAYCHAR_OnPlaycharSlideStateChanged(bool sliding)
    {
        //AUDIO
        if (sliding) {
            MasterAudio.PlaySound(AUDIOGROUP_PLAYCHAR_PUSHTG);
        }
    }
    void PLAYCHAR_OnPlaycharStumble(ObstacleController obst)
    {
        //AUDIO
        MasterAudio.PlaySound(AUDIOGROUP_PLAYCHAR_BUMP);
    }
    void PLAYCHAR_OnPlaycharStaminaStateChanged()
    {
        //AUDIO
        if (playchar_ctr.IsTired()) {
            MasterAudio.PlaySound(AUDIOGROUP_PLAYCHAR_TIRED);
        }
    }
    void PLAYCHAR_OnChaserLand()
    {
        //particles
        par_chaser_land.Play();

        //AUDIO
        MasterAudio.PlaySound(AUDIOGROUP_CHASER_LAND);
    }
    void PLAYCHAR_OnChaserCrash(ObstacleController obst)
    {
        //particles
        par_chaser_crash.Play();

        //AUDIO
        MasterAudio.PlaySound(AUDIOGROUP_CHASER_CRASH);
    }
    void PLAYCHAR_OnChaserCrashLose()
    {
        //particles
        par_chaser_crashlose.Play();

        //AUDIO
        MasterAudio.PlaySound(AUDIOGROUP_CHASER_CRASH_LOSE);
    }
    void PLAYCHAR_OnChaserStateChanged(ChaseState state)
    {
        if (state == ChaseState.CHASE_SLIDE) {
            //particles
            par_chaser_dropslide_active.Play();

            //AUDIO
            MasterAudio.PlaySound(AUDIOGROUP_CHASER_DROPSLIDE);
        } else if (chaser_ctr.LastChaseState() == ChaseState.CHASE_SLIDE) {
            //particles
            par_chaser_dropslide_active.Stop();

            //AUDIO
            MasterAudio.StopAllOfSound(AUDIOGROUP_CHASER_DROPSLIDE);
        }
        if (playing_state == GamePlayingState.MAIN) {
            AUD_PLAYING_UpdateMusic();
        }
    }
    void PLAYCHAR_OnChaserBump(ObstacleController obst)
    {
        //AUDIO
        MasterAudio.PlaySound(AUDIOGROUP_CHASER_BUMP);
    }
    void PLAYCHAR_OnChaserDropHit()
    {
        //AUDIO
        MasterAudio.PlaySound(AUDIOGROUP_CHASER_DROPHIT);
    }

    float pch_luck_total = 0f;
    int pch_luck_collected = 0;
    float pch_playing_score = 0;
    int pch_coins_nb_collected = 0;
    int pch_coins_b_collected = 0;
    public int CoinsBanked() { return pch_coins_b_collected; }
    public int CoinsNotBanked() { return pch_coins_nb_collected; }

    const int SCORE_BY_COINS = 10;
    const float STARTITEM_ACTIVE_TIME = 3f;
    const float PAUSE_HIDDEN_TIME = 3f;
    const int STARTITEM_SCOREX = 2;
    const int STARTITEM_COINSX = 2;
    const int STARTITEM_LUCKX = 2;
    const int RUN_SCOREX = 2;
    const int RUN_COINSX = 2;
    const int RUN_LUCKX = 2;
    const int COINTINUE_LUCK_PAY_VALUE = 5;
    bool active_startitem_scorex = false;
    bool active_startitem_coinsx = false;
    bool active_startitem_luckx = false;
    bool active_run_scorex = false;
    bool active_run_coinsx = false;
    bool active_run_luckx = false;
    public bool StartItem_ScoreX_Active() { return active_startitem_scorex; }
    public bool StartItem_CoinsX_Active() { return active_startitem_coinsx; }
    public bool StartItem_LuckX_Active() { return active_startitem_luckx; }
    public bool Run_ScoreX_Active() { return active_run_scorex; }
    public bool Run_CoinsX_Active() { return active_run_coinsx; }
    public bool Run_LuckX_Active() { return active_run_luckx; }
    int active_scorex_mult = 1;
    int active_coinsx_mult = 1;
    int active_luckx_mult = 1;

    public event Event<UserInvItemType> onStartItemUsed;

    public float PLAYCHAR_LuckTotal() { return pch_luck_total; }
    public bool PLAYCHAR_IsLucky()
    {
        return active_run_luckx ? true : Random.value < pch_luck_total;
    }
    void PLAYCHAR_UpdateTotalLuck()
    {
        pch_luck_total = System.Math.Min((0.01f * us.luck) * active_luckx_mult, 1.0f);
    }
    void PLAYCHAR_UpdateScoreX()
    {
        active_scorex_mult = (active_run_scorex ? RUN_SCOREX : 1) * (active_startitem_scorex ? STARTITEM_SCOREX : 1);

        //UI
        UI_PLAYING_MAIN_OnScorexMultChanged();
    }
    void PLAYCHAR_UpdateCoinsX()
    {
        active_coinsx_mult = (active_run_coinsx ? RUN_COINSX : 1) * (active_startitem_coinsx ? STARTITEM_COINSX : 1);

        //UI
        UI_PLAYING_MAIN_OnCoinsxMultChanged();
    }
    void PLAYCHAR_UpdateLuckX()
    {
        active_luckx_mult = (active_run_luckx ? RUN_LUCKX : 1) * (active_startitem_luckx ? STARTITEM_LUCKX : 1);
        PLAYCHAR_UpdateTotalLuck();
    }
    int PLAYCHAR_ScoreByCoins(int coins)
    {
        return coins * SCORE_BY_COINS;
    }
    void PLAYCHAR_ActivateStartScorex()
    {
        if (us.score_x > 0) {
            --us.score_x;
            active_startitem_scorex = true;
            PLAYCHAR_UpdateScoreX();

            if (onStartItemUsed != null) { onStartItemUsed(UserInvItemType.SCORE_X); }
        }
        //UI
        ui_playing_main_startitems_scorex_go.SetActive(false);
        //AUDIO
        AUD_UI_Sound(UiSoundType.BUY);
    }
    void PLAYCHAR_ActivateStartCoinsx()
    {
        if (us.coins_x > 0) {
            --us.coins_x;
            active_startitem_coinsx = true;
            PLAYCHAR_UpdateCoinsX();

            if (onStartItemUsed != null) { onStartItemUsed(UserInvItemType.COINS_X); }
        }
        //UI
        ui_playing_main_startitems_coinsx_go.SetActive(false);
        //AUDIO
        AUD_UI_Sound(UiSoundType.BUY);
    }
    void PLAYCHAR_ActivateStartLuckx()
    {
        if (us.luck_x > 0) {
            --us.luck_x;
            active_startitem_luckx = true;
            PLAYCHAR_UpdateLuckX();

            if (onStartItemUsed != null) { onStartItemUsed(UserInvItemType.LUCK_X); }
        }
        //UI
        ui_playing_main_startitems_luckx_go.SetActive(false);
        //AUDIO
        AUD_UI_Sound(UiSoundType.BUY);
    }
    void PLAYCHAR_DeactivateAllStartItems()
    {
        active_startitem_scorex = false;
        PLAYCHAR_UpdateScoreX();

        active_startitem_coinsx = false;
        PLAYCHAR_UpdateCoinsX();

        active_startitem_luckx = false;
        PLAYCHAR_UpdateLuckX();
    }
    #endregion //[PLAYCHAR]

    #region [PP]
    const int PLAYCHAR_PASSES_LENGTH = 10;
    class PlaycharPasses
    {
        public float distance = 0f;
        public CutsceneZoneSet cutscene_zones = null;
    }
    PlaycharPasses[] playchar_passes = null;
    int pp_current_cursor = 1;
    int pp_next_cursor = 1;
    int pp_write_cursor = 0;
    bool pp_active = false;
    void PP_Travel(float distance)
    {
        if ((playchar_passes[pp_next_cursor].distance -= distance) < 0) {
            pp_current_cursor = pp_next_cursor;
            if (++pp_next_cursor >= PLAYCHAR_PASSES_LENGTH) { pp_next_cursor = 0; }
            pp_active = playchar_passes[pp_next_cursor].distance > 0;
        }
    }
    void PP_Write(float distance, CutsceneZoneSet cutsceneZones)
    {
        if (cutsceneZones == null) return;

        if (playchar_passes[pp_write_cursor].distance > 0) {
            distance -= playchar_passes[pp_write_cursor].distance;
        }
        if (++pp_write_cursor >= PLAYCHAR_PASSES_LENGTH) { pp_write_cursor = 0; }
        var pp = playchar_passes[pp_write_cursor];
        pp.distance = distance;
        pp.cutscene_zones = cutsceneZones;
        pp_active = true;
    }
    void PP_WriteToCurrent(CutsceneZoneSet cutsceneZones)
    {
        playchar_passes[pp_current_cursor].cutscene_zones = cutsceneZones;
    }
    CutsceneZoneSet PP_GetCurrentZones()
    {
        return playchar_passes[pp_current_cursor].cutscene_zones;
    }
    #endregion

    #region [THEME]
    public const int NUM_RUN_LANES = 3;
    public const int NUM_LANE_CELLS = 64;
    public const int LEFT_SIDE_INDEX = 0;
    public const int RIGHT_SIDE_INDEX = 1;
    /* Dimension in units
	 * for cell == 1meter where unitScale == 1inch then CELL_DIMENSION = 39.37f
	 * for cell == 1meter where unitScale == 1cm then CELL_DIMENSION = 100.0f
     * In unity 1unit == 1meter so I scale all my models on import
	 */
    public const float CELL_DEPTH = 1.0f;
    public const float CELL_HEIGHT = 1.0f;
    public const float CELL_DEPTH_HALF = CELL_DEPTH * 0.5f;
    public const float CELL_HEIGHT_HALF = CELL_HEIGHT * 0.5f;
    public const float LANE_NEAR_Z = 0f;
    public const float LANE_FAR_Z = NUM_LANE_CELLS * CELL_DEPTH;
    public const float MIN_DISTANCE_TO_BUILD_SIDES = NUM_LANE_CELLS * CELL_DEPTH;
    public const float MIN_DISTANCE_TO_BUILD_OBSTACLES = (NUM_LANE_CELLS) * CELL_DEPTH;
    public const float MIN_DISTANCE_TO_BUILD_COINS = (NUM_LANE_CELLS * 0.3f) * CELL_DEPTH;
    public const float MIN_DISTANCE_TO_ENABLE_PHYSX = NUM_LANE_CELLS * 0.2f * CELL_DEPTH;
    public const float CELL_SYNC_SIZE = 5.0f;
    const float CELL_SIZE_EQUALITY_ERROR = CELL_DEPTH * 0.2f;

    public static readonly Vector3 POSITION_ZERO = Vector3.zero;
    public static readonly Vector3[] SCALE_X = new Vector3[2] { new Vector3(1, 1, 1), new Vector3(-1, 1, 1) };

    Vector3[] next_obstacle_pos;
    Vector3[] next_side_pos;
    Vector3 next_road_pos;
    void THEME_ResetThemeNextZValues()
    {
        next_side_pos[GameController.LEFT_SIDE_INDEX].z = 0f;
        next_side_pos[GameController.RIGHT_SIDE_INDEX].z = 0f;
        next_road_pos.z = 0;
    }
    void THEME_ResetPatternNextZValues()
    {
        for (int i = 0; i < NUM_RUN_LANES; ++i) { next_obstacle_pos[i].z = 10f; }
    }

    //attach nodes
    static readonly Vector3 ATTACH_NODE_POSITION = new Vector3(0f, 0f, LANE_FAR_Z);
    const int NUM_ATTACH_NODES = 2;
    Transform[] root_attach_nodes;
    Transform[] theme_attach_nodes;
    Transform[] pattern_attach_nodes;
    int current_attach_node_index = 0;
    public Transform AttachNodeRoot() { return root_attach_nodes[current_attach_node_index]; }
    public Transform AttachNodeTheme() { return theme_attach_nodes[current_attach_node_index]; }
    public Transform AttachNodePattern() { return pattern_attach_nodes[current_attach_node_index]; }
    void THEME_ReleaseAllThemeElements()
    {
        for (int i = 0; i < NUM_ATTACH_NODES; ++i)
            while (theme_attach_nodes[i].childCount > 0)
                theme_attach_nodes[i].GetChild(0).GetComponent<PoolObject>().Release();
    }
    void THEME_ReleaseAllPatternElements()
    {
        for (int i = 0; i < NUM_ATTACH_NODES; ++i)
            while (pattern_attach_nodes[i].childCount > 0)
                pattern_attach_nodes[i].GetChild(0).GetComponent<PoolObject>().Release();
    }
    static readonly Vector3 HIDDEN_NODE_POSITION = new Vector3(-10.0f, 0.0f, 0.0f);
    Transform hidden_node;
    public Transform HiddenNode() { return hidden_node; }

    /*[SetInEditor]*/
    public ObstacleScriptableObject obst_so = null;
    /*[SetInEditor]*/
    public PatternScriptableObject ptrn_so = null;
    /*[SetInEditor]*/
    public ThemeScriptableObject thm_so = null;

    Theme current_theme = null;
    int selected_theme_slot_index = 0;
    int current_theme_index_in_slot = -1;

    SideObstacleSet[] current_sides = new SideObstacleSet[2];
    int[] current_side_bodyplace_count = new int[2];
    RoadObstacleSet current_road = null;
    int current_road_bodyplace_count = 0;
    RefGroup<SimpleRefGroupItem> pattern_progress_group = null;
    ObstaclePattern current_pattern = null;
    int current_psbox_ref_index = -1;
    int current_pbox_ref_index = -1;
    PatternSuperBox current_psbox = null;
    PatternBox current_pbox = null;
    PatternSuperBox empty_psbox = null;
    PatternBox empty_pbox = null;
    PrefabPool empty_obst = null;
    int[] current_pbox_element_index = new int[GameController.NUM_RUN_LANES];
    int num_pattern_completed_lanes = 0;
    bool current_pbox_mirror = false;
    bool current_psbox_mirror = false;
    public bool CurrentPBoxMirror() { return current_pbox_mirror; }

    //approach serie data
    int[] appobst_serie_cells = new int[GameController.NUM_RUN_LANES];
    public int AppObstSerieCells(int lane_index) { return appobst_serie_cells[lane_index]; }
    float appobst_serie_offset = 0f;
    public float ApproachObstSerieOffset() { return appobst_serie_offset; }
    bool appobst_serie_active = false;

    //This value is shared between themes
    int current_theme_cells = 0;
    //road is syncing with sides. Do not place sides
    bool road_overriding_sides = false;
    //pattern box is overriding theme sets. Dont place roads and sides
    bool psbox_overriding_theme = false;

    void THEME_ClearTempValues()
    {
        for (int i = 0; i < current_sides.Length; ++i) { current_sides[i] = null; }
        for (int i = 0; i < current_side_bodyplace_count.Length; ++i) { current_side_bodyplace_count[i] = 0; }
        current_road = null;
        current_road_bodyplace_count = 0;
        current_pattern = null;
        current_psbox_ref_index = -1;
        current_psbox = null;
        current_pbox_ref_index = -1;
        current_pbox = null;
        for (int i = 0; i < current_pbox_element_index.Length; ++i) { current_pbox_element_index[i] = 0; }
        num_pattern_completed_lanes = 0;
        road_overriding_sides = false;
        psbox_overriding_theme = false;
    }
    public bool IsThemeSlotSelected() { return selected_theme_slot_index >= 0; }
    public int THEME_CurrentIndex()
    {
        return thm_so.theme_slots[selected_theme_slot_index].theme_infos[current_theme_index_in_slot].theme_index;
    }
    public float THEME_LaneOffsetX(int laneIndex)
    {
        return next_obstacle_pos[laneIndex].x;
    }
    public float THEME_CurrentCellWidth()
    {
        return thm_so.theme_slots[selected_theme_slot_index].theme_infos[current_theme_index_in_slot].cell_width;
    }
    public void THEME_PlaceCustomObstacle(int laneIndex, int cellsBefore, int cellsAfter, Transform placeObject)
    {
        next_obstacle_pos[laneIndex].z += cellsBefore * CELL_DEPTH;
        PrefabPool.PlaceCustom(next_obstacle_pos[laneIndex], (current_psbox != null) ? current_psbox.y_offset : 0f, placeObject);
        next_obstacle_pos[laneIndex].z += cellsAfter * CELL_DEPTH;
    }
    void THEME_SelectSlot(int index)
    {
#if CODEDEBUG
        string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
        if (thm_so.theme_slots == null || thm_so.theme_slots.Length == 0) {
            LogError(METHOD_NAME, " theme_slots is {0}", 0);
            return;
        }
        if (index < 0 || index >= thm_so.theme_slots.Length) {
            LogError(METHOD_NAME, " index is {0}, expected range [{1}, {2})", index, 0, thm_so.theme_slots.Length);
            return;
        }
#endif

        if (selected_theme_slot_index != -1) {
            //iterate old theme set
            for (int i = 0, l = thm_so.theme_slots[selected_theme_slot_index].theme_infos.Length; i < l; ++i) {
                int theme_index = thm_so.theme_slots[selected_theme_slot_index].theme_infos[i].theme_index;
                bool found = false;
                //iterate new theme set
                for (int k = 0; k < thm_so.theme_slots[index].theme_infos.Length; ++k) {
                    if (thm_so.theme_slots[index].theme_infos[k].theme_index == theme_index)
                        found = true;
                }
                if (!found) { thm_so.themes[theme_index].Destroy(); }
            }
        }

        //BG
        if (index != selected_theme_slot_index && bg_go != null) {
            GameObject.Destroy(bg_go);
            bg_go = null;
        }
        if (bg_go == null) {
            bg_go = GameObject.Instantiate(thm_so.theme_slots[index].bg_prefab) as GameObject;
            bg_go.transform.parent = transform;
            bg_go.SetActive(false);
        }

        //init or reset selected themes
        selected_theme_slot_index = index;
        for (int i = 0, l = thm_so.theme_slots[selected_theme_slot_index].theme_infos.Length; i < l; ++i) {
            int theme_index = thm_so.theme_slots[selected_theme_slot_index].theme_infos[i].theme_index;
            thm_so.themes[theme_index].Init(
#if CODEDEBUG
string.Format("themes[{0}]", theme_index)
#endif
);
        }

        //reset NEXT Z values
        for (int i = 0; i < NUM_RUN_LANES; ++i) { next_obstacle_pos[i].z = 0f; }
        next_side_pos[GameController.LEFT_SIDE_INDEX].z = 0f;
        next_side_pos[GameController.RIGHT_SIDE_INDEX].z = 0f;
        next_road_pos.z = 0;

        current_theme_index_in_slot = -1;
        THEME_Reset();
    }
    void THEME_Reset()
    {
        if (++current_theme_index_in_slot >= thm_so.theme_slots[selected_theme_slot_index].theme_infos.Length) {
            current_theme_index_in_slot = 0;
        }
        current_theme = thm_so.themes[THEME_CurrentIndex()];

        //adjust NEXT X values
        float cell_width = THEME_CurrentCellWidth();
        for (int i = 0; i < NUM_RUN_LANES; ++i) { next_obstacle_pos[i].x = (i - 1) * cell_width; }
        next_side_pos[LEFT_SIDE_INDEX].x = cell_width * -1.5f;
        next_side_pos[RIGHT_SIDE_INDEX].x = cell_width * 1.5f;
        next_road_pos.x = 0;

        current_theme_cells = -1;
    }
    void THEME_PlaceBegin()
    {
        if (current_theme.begin != null) {
            next_road_pos.z += current_theme.begin.PlaceAsThemeElement(next_road_pos) * CELL_DEPTH;
            next_side_pos[LEFT_SIDE_INDEX].z = next_road_pos.z;
            next_side_pos[RIGHT_SIDE_INDEX].z = next_road_pos.z;
        }

        //obstacles
        for (int i = 0; i < NUM_RUN_LANES; ++i)
            next_obstacle_pos[i].z = next_road_pos.z;

        PP_Write(next_road_pos.z, current_theme.cutscene_zones);

        //increment theme cells to indicate that theme has started
        current_theme_cells = 0;

        current_sides[LEFT_SIDE_INDEX] = null;
        current_sides[RIGHT_SIDE_INDEX] = null;
        current_road = null;
        current_pattern = null;
    }
    void THEME_PlaceEnd()
    {
        THEME_SyncRoadWithSides();
        if (current_theme.end != null) {
            next_road_pos.z += current_theme.end.PlaceAsThemeElement(next_road_pos) * CELL_DEPTH;
            next_side_pos[LEFT_SIDE_INDEX].z = next_road_pos.z;
            next_side_pos[RIGHT_SIDE_INDEX].z = next_road_pos.z;
        }
    }
    void THEME_PlaceObjects()
    {
        if (current_theme_cells < 0) {
            THEME_PlaceBegin();
        } else if (current_theme_cells > thm_so.theme_slots[selected_theme_slot_index].theme_infos[current_theme_index_in_slot].theme_cells && current_pattern == null) {
            THEME_PlaceEnd();
            THEME_Reset();
        } else {
            //place patterns
            if (current_pattern == null) {
                //pattern progress group
                {
                    float speed_progress = (playchar_ctr != null) ? playchar_ctr.RunSpeedNorm() : 0f;
                    int selected_theme_group_index = current_theme.patterns.Length - 1;
                    for (int i = 0; i < current_theme.patterns.Length; ++i) {
                        if (speed_progress < current_theme.patterns[i].from_run_speed) break;
                        selected_theme_group_index = i;
                    }
                    pattern_progress_group = ptrn_so.pattern_groups[current_theme.patterns[selected_theme_group_index].pattern_group_index];
                }

                //select new pattern
                int pattern_index = pattern_progress_group.Get().index;
                current_pattern = ptrn_so.patterns[pattern_index];
#if DBG_TRACE_PATTERNS
                Log("Pattern", string.Format("I[{0}], {1}", pattern_index, current_pattern.name));
#endif
            }
            if (current_psbox == null) {
                //select new superbox
                THEME_PatternSelectNextSuperBox();
            }
            for (int i = 0; i < NUM_RUN_LANES; ++i)
                if (next_obstacle_pos[i].z < MIN_DISTANCE_TO_BUILD_OBSTACLES)
                    THEME_PlacePatternElementAtLane(i);

            if (!psbox_overriding_theme) {
                if (next_road_pos.z < MIN_DISTANCE_TO_BUILD_SIDES)
                    THEME_PlaceRoad();

                if (!road_overriding_sides) {
                    if (next_side_pos[LEFT_SIDE_INDEX].z < MIN_DISTANCE_TO_BUILD_SIDES)
                        THEME_PlaceSide(PlaceOnSide.LEFT);
                    if (next_side_pos[RIGHT_SIDE_INDEX].z < MIN_DISTANCE_TO_BUILD_SIDES)
                        THEME_PlaceSide(PlaceOnSide.RIGHT);
                }
            }
        }
    }
    void THEME_PlaceRoad(bool force_end = false)
    {
        int occupied_cells = 0;
        //Place Begin: if road_set ended last time
        if (current_road == null) {
            if (force_end) return;
            //select new road_set
            current_road = current_theme.roads[ProbRandom.Get(current_theme.roads)];

            if (current_road.road_override_sides) {
                var selected_road = current_road;
                //select sync road
                current_road = null;
                //make sync
                THEME_SyncRoadWithSides();
                current_road = selected_road;
                road_overriding_sides = true;
                PP_Write(next_road_pos.z, current_road.cutscene_zones);
            }

            if (current_road.begin != null) {
                occupied_cells = current_road.begin.PlaceAsThemeElement(next_road_pos);
            }

            next_road_pos.z += occupied_cells * CELL_DEPTH;
            if (road_overriding_sides) {
                next_side_pos[LEFT_SIDE_INDEX].z = next_road_pos.z;
                next_side_pos[RIGHT_SIDE_INDEX].z = next_road_pos.z;
            }

            current_road_bodyplace_count = 0;
        }
        //Place End: if we decide that road_set should be finished
        else if (current_road_bodyplace_count >= current_road.body_place_count || force_end) {
            if (current_road.end != null) {
                occupied_cells = current_road.end.PlaceAsThemeElement(next_road_pos);
            }

            next_road_pos.z += occupied_cells * CELL_DEPTH;
            if (road_overriding_sides) {
                next_side_pos[LEFT_SIDE_INDEX].z = next_road_pos.z;
                next_side_pos[RIGHT_SIDE_INDEX].z = next_road_pos.z;
            }

            current_road = null;
            if (road_overriding_sides) { PP_Write(next_road_pos.z, current_theme.cutscene_zones); }
            road_overriding_sides = false;
        }
        //Place Body: just place another body segment from road_set
        else {
            //select random body
            int current_body_index = ProbRandom.Get(current_road.body);
            occupied_cells = current_road.body[current_body_index].PlaceAsThemeElement(next_road_pos);

            next_road_pos.z += occupied_cells * CELL_DEPTH;
            if (road_overriding_sides) {
                next_side_pos[LEFT_SIDE_INDEX].z = next_road_pos.z;
                next_side_pos[RIGHT_SIDE_INDEX].z = next_road_pos.z;
            }

            ++current_road_bodyplace_count;
        }

        current_theme_cells += occupied_cells;
    }
    void THEME_PlaceSide(PlaceOnSide side_hand, bool force_end = false)
    {
        bool mirror = side_hand == PlaceOnSide.RIGHT;
        int left_or_right_side_index = mirror ? RIGHT_SIDE_INDEX : LEFT_SIDE_INDEX;
        var current_side = current_sides[left_or_right_side_index];

        //Place Begin: if side_set ended last time
        if (current_side == null) {
            if (force_end) return;
            //select new side_set
            current_side = current_theme.sides[ProbRandom.Get(current_theme.sides)];
            if (current_side.only_on_side != side_hand && current_side.only_on_side != PlaceOnSide.BOTH) {
                //sides[0] must be for BOTH sides
                current_side = current_theme.sides[0];
            }

            if (current_side.begin != null) {
                next_side_pos[left_or_right_side_index].z += current_side.begin.PlaceAsThemeElement(next_side_pos[left_or_right_side_index], mirror) * GameController.CELL_DEPTH;
            }
            current_side_bodyplace_count[left_or_right_side_index] = 0;
            //store selected side index
            current_sides[left_or_right_side_index] = current_side;
        }
        //Place End: if we decide that side_set should be finished
        else if (current_side_bodyplace_count[left_or_right_side_index] >= current_side.body_place_count || force_end) {
            if (current_side.end != null) {
                next_side_pos[left_or_right_side_index].z += current_side.end.PlaceAsThemeElement(next_side_pos[left_or_right_side_index], mirror) * GameController.CELL_DEPTH;
            }
            current_sides[left_or_right_side_index] = null;
        }
        //Place Body: just place another segment from side_set
        else {
            //select random body
            int current_body_index = ProbRandom.Get(current_side.body);
            next_side_pos[left_or_right_side_index].z += current_side.body[current_body_index].PlaceAsThemeElement(next_side_pos[left_or_right_side_index], mirror) * GameController.CELL_DEPTH;
            ++current_side_bodyplace_count[left_or_right_side_index];
        }
    }
    void THEME_PlacePatternElementAtLane(int lane_index)
    {
        if (current_pbox == null) return;

        int select_lane_index = current_pbox_mirror ? (NUM_RUN_LANES - lane_index - 1) : lane_index;
        //check if current lane is not completed
        int num_elements_in_lane = current_pbox.elements[select_lane_index].Length;
        if (current_pbox_element_index[select_lane_index] < num_elements_in_lane) {
            int occupied_cells = 0;
            var el = current_pbox.elements[select_lane_index][current_pbox_element_index[select_lane_index]];

            //prepare approach serie
            if (el.write_serie && !appobst_serie_active) {
                //first on serie
                appobst_serie_offset = next_obstacle_pos[lane_index].z;
                for (int i = 0; i < NUM_RUN_LANES; ++i) {
                    appobst_serie_cells[i] = 0;
                }
                appobst_serie_active = true;
            }

            if (el.index < 0) {
                //empty obstacle
                empty_obst.PlaceAsPatternElement(next_obstacle_pos[lane_index], current_psbox.y_offset, el, lane_index);
                occupied_cells = el.num_cells_before;
            } else {
                //select from group
                var group = obst_so.obstacle_groups[el.index];
                PrefabPool obst = obst_so.obstacles[group.Get().index];
                obst.PlaceAsPatternElement(next_obstacle_pos[lane_index], current_psbox.y_offset, el, lane_index);
                occupied_cells = el.num_cells_before + ((group.size_override >= 0) ? group.size_override : obst.shared_data.size_in_cells);
            }

            if (el.write_serie) {
                appobst_serie_cells[lane_index] += occupied_cells;
            } else {
                next_obstacle_pos[lane_index].z += occupied_cells * CELL_DEPTH;
            }

            if (psbox_overriding_theme) {
                while (next_road_pos.z < next_obstacle_pos[lane_index].z) {
                    var road_body = ptrn_so.roads[current_psbox.override_road_index].body;
                    next_road_pos.z += road_body[ProbRandom.Get(road_body)].PlaceAsThemeElement(next_road_pos, current_psbox_mirror) * GameController.CELL_DEPTH;
                }
            }

            //check if lane is now completed
            if (++current_pbox_element_index[select_lane_index] >= num_elements_in_lane) {
                //check if all lanes are completed
                if (++num_pattern_completed_lanes >= NUM_RUN_LANES) {
                    //box completed

                    //tell everyone
                    if (onPatternBoxPlaced != null) { onPatternBoxPlaced(); }
                    THEME_PatternSelectNextBox();
                }
            }
        }
    }
    void THEME_PatternSelectNextSuperBox()
    {
        if (++current_psbox_ref_index >= current_pattern.super_boxes.Length) {
            //pattern ended
            //reset pbox and index
#if DBG_TRACE_PATTERNS
            Log("Pattern Ended", current_pattern.name);
#endif
            current_pbox = null;
            current_pbox_ref_index = -1;
            //reset psbox and index
            current_psbox = null;
            current_psbox_ref_index = -1;
            //will select next pattern on next iteration
            current_pattern = null;
        } else {
            // new superbox selected
            var sbref = current_pattern.super_boxes[current_psbox_ref_index];
            if (sbref.index < 0) {
                current_psbox = empty_psbox;
#if DBG_TRACE_PATTERNS
                Log("SuperBox", current_psbox.name);
#endif
            } else {
                int psbox_index = ptrn_so.psbox_groups[sbref.index].Get().index;
                current_psbox = ptrn_so.psboxes[psbox_index];
#if DBG_TRACE_PATTERNS
                Log("SuperBox", string.Format("G[{0}], I[{1}], {2}", sbref.index, psbox_index, current_psbox.name));
#endif
            }

            //stop approach serie
            appobst_serie_active = false;

            float max_z = next_obstacle_pos[0].z;
            for (int i = 1; i < NUM_RUN_LANES; ++i) {
                if (next_obstacle_pos[i].z > max_z) { max_z = next_obstacle_pos[i].z; }
            }
            max_z += sbref.num_cells_before * CELL_DEPTH;

            psbox_overriding_theme = current_psbox.override_road_index >= 0;
            current_psbox_mirror = psbox_overriding_theme && current_psbox.random_mirror && Random.value > 0.5f;
            if (psbox_overriding_theme) {
                //Sync Road with Obstacle
                {
                    //place roads normally to reach max_z
                    while (next_road_pos.z < max_z) {
                        THEME_PlaceRoad();
                    }
                    if (!road_overriding_sides) {
                        while (next_side_pos[LEFT_SIDE_INDEX].z < max_z)
                            THEME_PlaceSide(PlaceOnSide.LEFT);
                        while (next_side_pos[RIGHT_SIDE_INDEX].z < max_z)
                            THEME_PlaceSide(PlaceOnSide.RIGHT);
                    }

                    THEME_SyncRoadWithSides();
                }
                //align obstacles
                for (int i = 0; i < NUM_RUN_LANES; ++i)
                    next_obstacle_pos[i].z = next_road_pos.z;

                var road = ptrn_so.roads[current_psbox.override_road_index];
                PP_Write(next_road_pos.z, road.cutscene_zones);
                if (road.begin != null) {
                    next_road_pos.z += road.begin.PlaceAsThemeElement(next_road_pos, current_psbox_mirror) * CELL_DEPTH;
                }
            } else {
                //align obstacles
                for (int i = 0; i < NUM_RUN_LANES; ++i)
                    next_obstacle_pos[i].z = max_z;
            }

            current_pbox = null;
            current_pbox_ref_index = -1;
            THEME_PatternSelectNextBox();
        }
    }
    void THEME_PatternSelectNextBox()
    {
        if (++current_pbox_ref_index >= current_psbox.boxes.Length) {
            //psbox ended
#if DBG_TRACE_PATTERNS
            Log("SuperBox Ended", current_psbox.name);
#endif
            if (psbox_overriding_theme) {
                //finish box
                var road = ptrn_so.roads[current_psbox.override_road_index];
                if (road.end != null) {
                    next_road_pos.z += road.end.PlaceAsThemeElement(next_road_pos, current_psbox_mirror) * GameController.CELL_DEPTH;
                }
                next_side_pos[LEFT_SIDE_INDEX].z = next_road_pos.z;
                next_side_pos[RIGHT_SIDE_INDEX].z = next_road_pos.z;
                for (int i = 0; i < NUM_RUN_LANES; ++i) {
                    next_obstacle_pos[i].z = next_road_pos.z;
                }
                PP_Write(next_road_pos.z, current_theme.cutscene_zones);
                psbox_overriding_theme = false;
            }
            //reset pbox and index
            current_pbox = null;
            current_pbox_ref_index = -1;
            //will select next psbox on next iteration
            current_psbox = null;
        } else {
            //new pattern box reference selected
            var bref = current_psbox.boxes[current_pbox_ref_index];
            if (bref.index < 0) {
                //place empty
                current_pbox = empty_pbox;
#if DBG_TRACE_PATTERNS
                Log("PBox", current_pbox.name);
#endif
            } else {
                //place from group
                int pbox_index = ptrn_so.pbox_groups[bref.index].Get().index;
                current_pbox = ptrn_so.pboxes[pbox_index];
#if DBG_TRACE_PATTERNS
                Log("PBox", string.Format("G[{0}], I[{1}], {2}", bref.index, pbox_index, current_pbox.name));
#endif
            }

            //align all next_obstacle_pos
            float max_z = next_obstacle_pos[0].z;
            for (int i = 1; i < NUM_RUN_LANES; ++i) {
                if (next_obstacle_pos[i].z > max_z) {
                    max_z = next_obstacle_pos[i].z;
                }
            }
            max_z += bref.num_cells_before * CELL_DEPTH;
            for (int i = 0; i < NUM_RUN_LANES; ++i) {
                next_obstacle_pos[i].z = max_z;
            }

            for (int i = 0; i < NUM_RUN_LANES; ++i) {
                current_pbox_element_index[i] = 0;
            }
            num_pattern_completed_lanes = 0;
            current_pbox_mirror = current_psbox_mirror || (current_pbox.random_mirror && Random.value > 0.5f);

            //approach serie
            if (appobst_serie_active) {
                int max_cells = appobst_serie_cells[0];
                for (int i = 1; i < NUM_RUN_LANES; ++i) {
                    if (appobst_serie_cells[i] > max_cells) max_cells = appobst_serie_cells[i];
                }
                //cells in a new box begin from start
                for (int i = 0; i < NUM_RUN_LANES; ++i) {
                    appobst_serie_cells[i] = max_cells;
                }
            }
        }
    }
    void THEME_SyncRoadWithSides()
    {
        float z_diff = 0f;
        int cell_diff = 0;

        //place left side end
        THEME_PlaceSide(PlaceOnSide.LEFT, true);

        //place right side end
        THEME_PlaceSide(PlaceOnSide.RIGHT, true);

        //sync sides
        z_diff = next_side_pos[LEFT_SIDE_INDEX].z - next_side_pos[RIGHT_SIDE_INDEX].z;
        cell_diff = (int)((Mathf.Abs(z_diff) + CELL_SIZE_EQUALITY_ERROR) / CELL_SYNC_SIZE);
        if (z_diff > 0f) {
            //place right side sync nodes
            for (int i = 0; i < cell_diff; ++i) {
                current_theme.sides[0].body[0].PlaceAsThemeElement(next_side_pos[RIGHT_SIDE_INDEX], true);
                next_side_pos[RIGHT_SIDE_INDEX].z += CELL_SYNC_SIZE;
            }
        } else {
            //place left side sync nodes
            for (int i = 0; i < cell_diff; ++i) {
                current_theme.sides[0].body[0].PlaceAsThemeElement(next_side_pos[LEFT_SIDE_INDEX], false);
                next_side_pos[LEFT_SIDE_INDEX].z += CELL_SYNC_SIZE;
            }
        }

        //place road end
        THEME_PlaceRoad(true);

        //sync road with sides
        z_diff = next_road_pos.z - next_side_pos[LEFT_SIDE_INDEX].z;
        cell_diff = (int)((Mathf.Abs(z_diff) + CELL_SIZE_EQUALITY_ERROR) / CELL_SYNC_SIZE);
        if (z_diff > 0f) {
            //place side waits
            for (int i = 0; i < cell_diff; ++i) {
                current_theme.sides[0].body[0].PlaceAsThemeElement(next_side_pos[LEFT_SIDE_INDEX], false);
                current_theme.sides[0].body[0].PlaceAsThemeElement(next_side_pos[RIGHT_SIDE_INDEX], true);
                next_side_pos[LEFT_SIDE_INDEX].z += CELL_SYNC_SIZE;
                next_side_pos[RIGHT_SIDE_INDEX].z += CELL_SYNC_SIZE;
            }
        } else {
            //place road
            for (int i = 0; i < cell_diff; ++i) {
                current_theme.roads[0].body[0].PlaceAsThemeElement(next_road_pos);
                next_road_pos.z += CELL_SYNC_SIZE;
            }
        }
    }
    #endregion //[THEME]

    #region [Coins]
    public CoinsScriptableObject coins_so = null;

    public event GameController.Event<CoinSharedData> onCoinCollected;
    public event GameController.Event<int> onCoinsBanked;

    const float COIN_PLACE_HEIGHT = 0.8f;

    float coins_in_cell = 0.25f;
    float coins_wait_time = 0.12f;

    List<SuperCoinPlacer> coins_supercoin_placers_active = new List<SuperCoinPlacer>(10);
    Dictionary<CoinType, SimpleSuperCoinPlacer> coins_supercoin_placers = new Dictionary<CoinType, SimpleSuperCoinPlacer>();

    //small coin
    GameController.Event<Vector3, Transform> coins_small_place = null;
    IParticles coins_small_particles = null;

    //super coin particles unwrapped collection
    IParticles coins_medium_particles = null;
    IParticles coins_magnet_particles = null;
    IParticles coins_stamina_particles = null;
    IParticles coins_scorex_particles = null;
    IParticles coins_coinsx_particles = null;
    IParticles coins_luckx_particles = null;
    IParticles coins_luck_particles = null;
    IParticles coins_drop_particles = null;
    IParticles coins_mbox_particles = null;
    IParticles coins_smbox_particles = null;
    IParticles coins_symbol_particles = null;
    IParticles coins_letter_particles = null;
    void COINS_SetSupercoinParticles(CoinType type, IParticles particles)
    {
        switch (type) {
        case CoinType.MEDIUM: coins_medium_particles = particles; break;
        case CoinType.MAGNET: coins_magnet_particles = particles; break;
        case CoinType.STAMINA: coins_stamina_particles = particles; break;
        case CoinType.SCOREX: coins_scorex_particles = particles; break;
        case CoinType.COINSX: coins_coinsx_particles = particles; break;
        case CoinType.LUCKX: coins_luckx_particles = particles; break;
        case CoinType.LUCK: coins_luck_particles = particles; break;
        case CoinType.DROP: coins_drop_particles = particles; break;
        case CoinType.MBOX: coins_mbox_particles = particles; break;
        case CoinType.SMBOX: coins_smbox_particles = particles; break;
        case CoinType.SYMBOL: coins_symbol_particles = particles; break;
        case CoinType.LETTER: coins_letter_particles = particles; break;
        }
    }
    IParticles COINS_GetSupercoinParticles(CoinType type)
    {
        switch (type) {
        case CoinType.MEDIUM: return coins_medium_particles;
        case CoinType.MAGNET: return coins_magnet_particles;
        case CoinType.STAMINA: return coins_stamina_particles;
        case CoinType.SCOREX: return coins_scorex_particles;
        case CoinType.COINSX: return coins_coinsx_particles;
        case CoinType.LUCKX: return coins_luckx_particles;
        case CoinType.LUCK: return coins_luck_particles;
        case CoinType.DROP: return coins_drop_particles;
        case CoinType.MBOX: return coins_mbox_particles;
        case CoinType.SMBOX: return coins_smbox_particles;
        default:
#if CODEDEBUG
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            LogError(METHOD_NAME, "type {0} is invalid", type);
#endif
            return null;
        }
    }
    void COINS_CoinCollectParticle(CoinType type)
    {
        switch (type) {
        case CoinType.SMALL: coins_small_particles.Emit(1); break;
        case CoinType.MEDIUM: coins_medium_particles.Play(); break;
        case CoinType.MAGNET: coins_magnet_particles.Play(); break;
        case CoinType.STAMINA: coins_stamina_particles.Play(); break;
        case CoinType.SCOREX: coins_scorex_particles.Play(); break;
        case CoinType.COINSX: coins_coinsx_particles.Play(); break;
        case CoinType.LUCKX: coins_luckx_particles.Play(); break;
        case CoinType.LUCK: coins_luck_particles.Play(); break;
        case CoinType.DROP: coins_drop_particles.Play(); break;
        case CoinType.MBOX: coins_mbox_particles.Play(); break;
        case CoinType.SMBOX: coins_smbox_particles.Play(); break;
        case CoinType.SYMBOL: coins_symbol_particles.Play(); break;
        case CoinType.LETTER: coins_letter_particles.Play(); break;
        }
    }
    void COINS_StopAllParticles()
    {
        coins_small_particles.Stop();
        coins_medium_particles.Stop();
        coins_magnet_particles.Stop();
        coins_stamina_particles.Stop();
        coins_scorex_particles.Stop();
        coins_coinsx_particles.Stop();
        coins_luckx_particles.Stop();
        coins_luck_particles.Stop();
        coins_drop_particles.Stop();
        coins_mbox_particles.Stop();
        coins_smbox_particles.Stop();
        if (coins_symbol_particles != null) coins_symbol_particles.Stop();
        if (coins_letter_particles != null) coins_letter_particles.Stop();
    }
    bool[] coins_special_available = null;

    Dictionary<string, PrefabPool> coins_letter = null;
    bool coins_letter_available = true;

    //hand items
    Transform handitem_magnet_tr = null;

    //arc trigger zone
    PrefabPool coins_arc_trigger_pool = null;
    Vector3 coins_arc_trigger_scale = Vector3.one;
    bool coins_arc_trigger_inside = false;

    public void AddSuperCoinPlacer(SuperCoinPlacer placer)
    {
        coins_supercoin_placers_active.Add(placer);
    }
    public void RemoveSuperCoinPlacer(SuperCoinPlacer placer)
    {
        coins_supercoin_placers_active.Remove(placer);
    }
    public IEnumerator PlaceCoins(RoadObstacleController obst)
    {
        PatternBox.Element el = obst.PatternElement();
        if (el.coins_prob < 0f || (el.coins_prob < 1f && Random.value > el.coins_prob)) yield break;

        int num_cells_before = obst.NumCellsBefore();
        Vector3 offset = Vector3.zero;
        offset.z += GameController.CELL_DEPTH_HALF;
        offset.y += COIN_PLACE_HEIGHT;
        Transform before_node = obst.CoinsBeforeNode();
        float time_mult = obst.CoinsWaitTimeMult();
        var coin_place_interval_waiter = obst.CoinPlaceWaiter();
        coin_place_interval_waiter.SetDuration(coins_wait_time * time_mult);
        IEnumerator current = null;

        if (el.index < 0) {
            //place stride
            current = PlaceStride(offset, num_cells_before, el, obst.PatternMirror(), before_node, coin_place_interval_waiter);
            while (current.MoveNext()) yield return current.Current;
        } else {
            if (obst.Configuration() == ObstacleConfiguration.WALL) {
                //before coins
                if (el.coin_form_mod == CoinPlaceFormMod.ARC && obst.FloorHeight() < 2f) {
                    //place arc to jump over obstacle
                    int num_coins = (int)((num_cells_before - (playchar_ctr.JumpCells() / 2)) * coins_in_cell) - (int)el.coins_step_offset;
                    offset.z += el.coins_step_offset * coins_so.coins_place_step;
                    if (num_coins > 0) {
                        current = PlaceStrideCoins(offset, num_coins, before_node, coin_place_interval_waiter);
                        while (current.MoveNext()) yield return current.Current;
                    }

                    coin_place_interval_waiter.SetDuration(coins_wait_time * time_mult * 0.4f);
                    offset.z += num_coins * coins_so.coins_place_step;
                    current = PlaceArc(offset, el, before_node, coin_place_interval_waiter);
                    while (current.MoveNext()) yield return current.Current;
                } else {
                    //place stride
                    current = PlaceStride(offset, num_cells_before, el, obst.PatternMirror(), before_node, coin_place_interval_waiter);
                    while (current.MoveNext()) yield return current.Current;
                }
            } else {
                //configuration is BOX
                //coins BEFORE
                if (el.coin_alignment == CoinPlaceAlignment.BEFORE || el.coin_alignment == CoinPlaceAlignment.BOTH) {
                    current = PlaceStride(offset, num_cells_before, el, obst.PatternMirror(), before_node, coin_place_interval_waiter);
                    while (current.MoveNext()) yield return current.Current;
                }
                //coins ON_TOP
                if (el.coin_alignment == CoinPlaceAlignment.ON_TOP || el.coin_alignment == CoinPlaceAlignment.BOTH) {
                    int num_cells_after = obst.SizeCells();
                    Transform after_node = obst.CoinsAfterNode();
                    offset.y += obst.FloorHeight();
                    if (el.coin_form_mod == CoinPlaceFormMod.ARC) {
                        //place stride before arc
                        int num_coins = (int)(num_cells_after * coins_in_cell) - (int)el.coins_step_offset;
                        offset.z += el.coins_step_offset * coins_so.coins_place_step;
                        if (num_coins > 0) {
                            current = PlaceStrideCoins(offset, num_coins, after_node, coin_place_interval_waiter);
                            while (current.MoveNext()) yield return current.Current;
                        }

                        //place arc at the end
                        coin_place_interval_waiter.SetDuration(coins_wait_time * time_mult * 0.4f);
                        offset.z += ((float)num_coins - 0.5f) * coins_so.coins_place_step;
                        current = PlaceArc(offset, el, after_node, coin_place_interval_waiter);
                        while (current.MoveNext()) yield return current.Current;
                    } else {
                        //place stride
                        current = PlaceStride(offset, num_cells_after, el, obst.PatternMirror(), after_node, coin_place_interval_waiter);
                        while (current.MoveNext()) yield return current.Current;
                    }
                }
            }
        }
    }
    IEnumerator PlaceStrideCoins(Vector3 offset, int num_coins, Transform parent, Routine.IntervalWait coin_place_interval_waiter)
    {
        for (int i = 0; i < num_coins; ++i) {
            coins_small_place(offset, parent);
            offset.z += coins_so.coins_place_step;
            yield return coin_place_interval_waiter.Reset();
        }
    }
    IEnumerator PlaceStride(Vector3 offset, int num_cells, PatternBox.Element el, bool pattern_mirrored, Transform parent, Routine.IntervalWait coin_place_interval_waiter)
    {
        int num_coins = (int)(num_cells * coins_in_cell) - (int)el.coins_step_offset;
        if (num_coins > 0) {
            //offset should be calculated before IF, but in this case it doesnt matter
            offset.z += el.coins_step_offset * coins_so.coins_place_step;

            --num_coins;
            if (num_coins > 0) {
                IEnumerator current = PlaceStrideCoins(offset, num_coins, parent, coin_place_interval_waiter);
                while (current.MoveNext()) yield return current.Current;
                offset.z += num_coins * coins_so.coins_place_step;
            }

            if (el.super_luck_threshold < 0f || !PlaceSuper(offset, parent, el.super_luck_threshold)) {
                switch (el.coin_form_mod) {
                case CoinPlaceFormMod.TURN_RIGHT:
                    offset.x += GameController.Instance.THEME_CurrentCellWidth() * (pattern_mirrored ? -0.5f : 0.5f);
                    break;
                case CoinPlaceFormMod.TURN_LEFT:
                    offset.x -= GameController.Instance.THEME_CurrentCellWidth() * (pattern_mirrored ? -0.5f : 0.5f);
                    break;
                }
                coins_small_place(offset, parent);
            }
        }
    }
    IEnumerator PlaceArc(Vector3 offset, PatternBox.Element el, Transform parent, Routine.IntervalWait coin_place_interval_waiter)
    {
        COINS_ArcPlacing(offset, parent);
        float base_height = offset.y;
        float delta_height = playchar_ctr.jump_height/* - COIN_PLACE_HEIGHT*/;
        int num_coins = (int)(playchar_ctr.JumpCells() / coins_so.coins_arc_place_step) + 2;
        int num_coins_rising = num_coins / 2;
        int num_coins_falling = num_coins - num_coins_rising;

        for (int i = 0; i < num_coins_rising; ++i) {
            offset.y = Easing.QuadOut(i, base_height, delta_height, num_coins_rising);
            coins_small_place(offset, parent);
            offset.z += coins_so.coins_arc_place_step/* * ((((float)num_coins / (i + 1)) * 0.5f) + 0.5f)*/;
            yield return coin_place_interval_waiter.Reset();
        }

        //arc top
        offset.y = base_height + delta_height;
        if (el.super_luck_threshold < 0f || !PlaceSuper(offset, parent, el.super_luck_threshold)) {
            coins_small_place(offset, parent);
        }
        offset.z += coins_so.coins_arc_place_step;
        yield return null;

        for (int i = 0; i < num_coins_falling; ++i) {
            offset.y = Easing.QuadIn(i, base_height + delta_height, -delta_height, num_coins_falling);
            coins_small_place(offset, parent);
            offset.z += coins_so.coins_arc_place_step/* * ((((float)num_coins / (i + 1)) * 0.5f) + 0.5f)*/;
            yield return coin_place_interval_waiter.Reset();
        }
    }
    bool PlaceSuper(Vector3 offset, Transform parent, float luck_threshold)
    {
        if (luck_threshold < 0.05f) {
            coins_supercoin_placers_active[ProbRandom.UpdateAndGet(coins_supercoin_placers_active)].PlaceCoin(offset, parent);
            return true;
        } else if (active_run_luckx || (pch_luck_total > luck_threshold && PLAYCHAR_IsLucky())) {
            coins_supercoin_placers_active[ProbRandom.UpdateAndGet(coins_supercoin_placers_active)].PlaceCoin(offset, parent);
            //lucky effect
            PLAYCHAR_OnLuckyEffectNow();
            return true;
        }
        return false;
    }
    GameController.Event<Vector3, Transform> InitSmallCoin()
    {
        if (coins_so.small_coin_data.coin_pool == null) {
            coins_so.small_coin_data.coin_pool = new PrefabPool() {
                num_objects = 48,
                target_prefabs = new GameObject[1] { coins_so.small_coin_data.coin_prefab },
                shared_data = new CoinSharedData() {
                    size_in_cells = 0,
                    coin_value = 1,
                    can_fly = true,
                    category = CoinCategory.COIN,
                    type = CoinType.SMALL,
                    pick_method = COINS_SmallCoinCollected
                }
            };
            coins_so.small_coin_data.coin_pool.Init(
#if CODEDEBUG
"small_coin"
#endif
);
        }

        //select default particle
        if (coins_so.small_coin_data.collect_particle_holder == null) {
            Transform particle_tr = Instantiate(coins_so.small_coin_data.collect_particle_prefab).transform;
            particle_tr.SetParent(hidden_node, false);
            coins_so.small_coin_data.collect_particle_holder = new ParticleHolder();
            coins_so.small_coin_data.collect_particle_holder.SetParticles(particle_tr);
        }
#if CODEDEBUG
        if (coins_so.small_coin_data.collect_particle_holder.IsEmpty()) {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            LogError(METHOD_NAME, "small_coin.default_particle_system is NULL");
        }
#endif
        //set particles
        coins_small_particles = coins_so.small_coin_data.collect_particle_holder;

        //arc trigger zone
        if (coins_arc_trigger_pool == null) {
            coins_arc_trigger_pool = new PrefabPool() {
                num_objects = 10,
                target_prefabs = new GameObject[1] { coins_so.coins_arc_trigger_prefab },
                shared_data = new TriggerZoneSharedData() {
                    tag = GameController.TAG_PLAYER,
                    enter_call = COINS_ArcTriggerEnter,
                    exit_call = COINS_ArcTriggerExit
                }
            };
            coins_arc_trigger_pool.Init(
#if CODEDEBUG
"arc_trigger"
#endif
);
        }
        audio_small_coins_pitch = coins_so.audio_small_coin_begin_pitch;
        coins_arc_trigger_inside = false;
        coins_in_cell = 1f / coins_so.coins_place_step;

        return coins_so.small_coin_data.coin_pool.PlaceAsCoin;
    }
    SimpleSuperCoinPlacer InitSuperCoin(CoinType coin_type)
    {
        SuperCoinInitData coin = null;
        switch (coin_type) {
        case CoinType.DROP:
            coin = SelectedPlaycharSlot().drop_coin_data;
            break;
        default:
            //get default coin data
            if (coins_so.super_coin_data.TryGetValue(coin_type, out coin) == false) { return null; }
            break;
        }
        //default pool
        if (coin.coin_pool == null) {
            coin.coin_pool = new PrefabPool() {
                num_objects = 2,
                target_prefabs = new GameObject[1] { coin.coin_prefab },
                shared_data = new CoinSharedData() {
                    size_in_cells = 0,
                    category = CoinSharedData.CoinCategoryForType(coin_type),
                    type = coin_type,
                    pick_method = COINS_PickMethodForType(coin_type)
                }
            };
            coin.coin_pool.Init(
#if CODEDEBUG
"medium_coin"
#endif
);
        }

        //select default particle
        if (coin.collect_particle_holder == null) {
            Transform particle_tr = Instantiate(coin.collect_particle_prefab).transform;
            particle_tr.SetParent(hidden_node, false);
            coin.collect_particle_holder = new ParticleHolder();
            coin.collect_particle_holder.SetParticles(particle_tr);
        }
#if CODEDEBUG
        if (coin.collect_particle_holder.IsEmpty()) {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            LogError(METHOD_NAME, "medium_coin.default_particle_system is NULL");
        }
#endif
        //select particles
        COINS_SetSupercoinParticles(coin_type, coin.collect_particle_holder);

        //select pool
        PrefabPool pool = coin.coin_pool;

        SuperCoinInitData coin_override = null;
        if (SelectedPlaychar().super_coin_override.TryGetValue(coin_type, out coin_override)) {
            if (coin_override.coin_prefab != null) {
                if (coin_override.coin_pool == null) {
                    coin_override.coin_pool = new PrefabPool() {
                        num_objects = 2,
                        target_prefabs = new GameObject[1],
                        shared_data = coin.coin_pool.shared_data
                    };
                }
                coin_override.coin_pool.target_prefabs[0] = coin_override.coin_prefab;
                coin_override.coin_pool.Init(
#if CODEDEBUG
"medium_coin_override"
#endif
);
                //set override pool
                pool = coin_override.coin_pool;
            }

            //particle override
            if (coin_override.collect_particle_prefab != null) {
                if (coin_override.collect_particle_holder == null) {
                    Transform particle_tr = Instantiate(coin_override.collect_particle_prefab).transform;
                    particle_tr.SetParent(hidden_node, false);
                    coin_override.collect_particle_holder = new ParticleHolder();
                    coin_override.collect_particle_holder.SetParticles(particle_tr);
                }
#if CODEDEBUG
                if (coin_override.collect_particle_holder.IsEmpty()) {
                    string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
                    LogError(METHOD_NAME, "playchar[{0}].medium_coin_override.particle_system is NULL", SelectedPlaycharIndex());
                }
#endif
                COINS_SetSupercoinParticles(coin_type, coin_override.collect_particle_holder);
            }
        }

        //coin placer
        SimpleSuperCoinPlacer placer = null;
        if (coins_supercoin_placers.TryGetValue(coin_type, out placer) == false) {
            placer = new SimpleSuperCoinPlacer();
            coins_supercoin_placers.Add(coin_type, placer);
        }

        //set values
        pool.GetSharedData<CoinSharedData>().coin_value = (coin_override != null && coin_override.coin_value > 0) ? coin_override.coin_value : coin.coin_value;
        pool.GetSharedData<CoinSharedData>().can_fly = PlaycharLevel.MagnetPullsCoin(coin_type, SelectedPlaycharLevel(PlaycharLevelType.MAG_POWER));
        placer.probability = (coin_override != null && coin_override.probability > 0f) ? coin_override.probability : coin.probability;
        placer.place_method = pool.PlaceAsCoin;
        //predicate
        int predicate_value = (coin_override != null && coin_override.predicate_value > 0) ? coin_override.predicate_value : coin.predicate_value;
        switch (coin_type) {
        case CoinType.MAGNET:
            placer.prob_predicate = () => !playchar_ctr.IsMagnetActive();
            break;
        case CoinType.LUCKX:
            placer.prob_predicate = () => !active_run_luckx;
            break;
        case CoinType.LUCK:
            placer.prob_predicate = () => us.luck < predicate_value;
            break;
        case CoinType.STAMINA:
            placer.prob_predicate = () => playchar_ctr.CurrentStamina() < predicate_value;
            break;
        case CoinType.DROP:
            placer.prob_predicate = () => us.drops[selected_playchar_slot_index] < predicate_value;
            break;
        }
        return placer;
    }
    public PrefabPool InitSpecialCoin(int index)
    {
#if CODEDEBUG
        if (coins_so.special_coins == null || coins_so.special_coins.Length == 0) {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            LogError(METHOD_NAME, "coins_so.special_coins is NULL");
        }
#endif
        if (coins_special_available == null) {
            coins_special_available = new bool[coins_so.special_coins.Length];
            for (int i = 0, l = coins_special_available.Length; i < l; ++i) {
                coins_special_available[i] = true;
            }
        }
        if (index < 0 || index >= coins_so.special_coins.Length) {
#if CODEDEBUG
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            LogError(METHOD_NAME, "requested index is {0}, expected range [{1}, {2})", index, 0, coins_so.special_coins.Length);
#endif
            return null;
        }
        if (!coins_special_available[index]) {
#if CODEDEBUG
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            LogError(METHOD_NAME, "special_coins[{0}] is not available", index);
#endif
            return null;
        }

        CoinInitData coin = coins_so.special_coins[index];

        if (coin.coin_pool == null) {
            coin.coin_pool = new PrefabPool() {
                num_objects = 1,
                target_prefabs = new GameObject[1] { coin.coin_prefab },
                shared_data = new CoinSharedData() {
                    size_in_cells = 0,
                    category = CoinCategory.SPECIAL,
                    type = CoinType.SYMBOL
                }
            };
            coin.coin_pool.Init(
#if CODEDEBUG
string.Format("special_coins[{0}]", index)
#endif
);
        }

        if (coin.collect_particle_holder == null) {
            Transform particle_tr = Instantiate(coin.collect_particle_prefab).transform;
            particle_tr.SetParent(hidden_node, false);
            coin.collect_particle_holder = new ParticleHolder();
            coin.collect_particle_holder.SetParticles(particle_tr);
        }
#if CODEDEBUG
        if (coin.collect_particle_holder.IsEmpty()) {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            LogError(METHOD_NAME, "medium_coin.default_particle_system is NULL");
        }
#endif
        //select particles
        COINS_SetSupercoinParticles(CoinType.SYMBOL, coin.collect_particle_holder);

        coins_special_available[index] = false;
        return coin.coin_pool;
    }
    public void ReleaseSpecialCoin(int index)
    {
        CoinInitData coin = coins_so.special_coins[index];
        coin.coin_pool.ReleaseAll();
        coin.collect_particle_holder.root.SetParent(hidden_node, false);

        coins_special_available[index] = true;
    }
    public PrefabPool[] InitLetterCoins(string[] letters)
    {
        if (!coins_letter_available) return null;

        if (letters == null || letters.Length == 0) {
#if CODEDEBUG
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            LogError(METHOD_NAME, "letters are NULL");
#endif
            return null;
        }

        if (coins_letter == null) {
            coins_letter = new Dictionary<string, PrefabPool>(32);
        }
        for (int i = 0, l = letters.Length; i < l; ++i) {
            string letter = letters[i];

            PrefabPool letter_pool = null;
            if (!coins_letter.TryGetValue(letter, out letter_pool)) {
                string path = string.Format("Items/Coins/Letters/Prefabs/{0}", letter);
                GameObject letter_prefab = Resources.Load<GameObject>(path);
#if CODEDEBUG
                if (letter_prefab == null) {
                    string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
                    LogError(METHOD_NAME, "{0}.prefab is NULL", path);
                    return null;
                }
#endif
                letter_pool = new PrefabPool() {
                    num_objects = 1,
                    target_prefabs = new GameObject[1] { letter_prefab },
                    shared_data = new CoinSharedData() {
                        size_in_cells = 0,
                        category = CoinCategory.SPECIAL,
                        type = CoinType.LETTER,
                        coin_value = 0,
                    }
                };
                coins_letter[letter] = letter_pool;
            }
        }

        PrefabPool[] out_coins = new PrefabPool[letters.Length];
        for (int i = 0, l = letters.Length; i < l; ++i) {
            if (!coins_letter.TryGetValue(letters[i], out out_coins[i])) {
#if CODEDEBUG
                string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
                LogError(METHOD_NAME, "letter[{0}] is NULL", letters[i]);
#endif
                return null;
            }
        }
        for (int i = 0, l = out_coins.Length; i < l; ++i) {
            out_coins[i].Init(
#if CODEDEBUG
string.Format("letter_coins[{0}]", letters[i])
#endif
);
        }

        if (coins_letter_particles == null) {
            Transform particle_tr = Instantiate(coins_so.letter_coin_particle_prefab).transform;
            particle_tr.SetParent(hidden_node, false);
            var coins_letter_particle_holder = new ParticleHolder();
            coins_letter_particle_holder.SetParticles(particle_tr);
            coins_letter_particles = coins_letter_particle_holder;
        }
        if (playchar_ctr != null) {
            coins_letter_particles.Root().SetParent(playchar_ctr.CoinParticleNode(), false);
        }

        coins_letter_available = false;
        return out_coins;
    }
    public void ReleaseLetterCoins()
    {
        if (coins_letter == null) return;

        foreach (var kv in coins_letter) {
            kv.Value.ReleaseAll();
        }
        coins_letter_particles.Root().SetParent(hidden_node, false);

        coins_letter_available = true;
    }
    public void ReportPlaycharCoinCollected(CoinSharedData sharedData)
    {
        //AUDIO
        AUD_COINS_Sound(sharedData.type);

        //particles
        COINS_CoinCollectParticle(sharedData.type);

        //challenge event
        if (onCoinCollected != null) { onCoinCollected(sharedData); }
    }
    GameController.Event<CoinSharedData> COINS_PickMethodForType(CoinType type)
    {
        switch (type) {
        case CoinType.SMALL: return COINS_SmallCoinCollected;
        case CoinType.MEDIUM: return COINS_MediumCoinCollected;
        case CoinType.MAGNET: return COINS_MagnetCoinCollected;
        case CoinType.STAMINA: return COINS_StaminaCoinCollected;
        case CoinType.LUCKX: return COINS_LuckxCoinCollected;
        case CoinType.SCOREX: return COINS_ScorexCoinCollected;
        case CoinType.COINSX: return COINS_CoinsxCoinCollected;
        case CoinType.MBOX: return COINS_MBoxCoinCollected;
        case CoinType.SMBOX: return COINS_SMBoxCoinCollected;
        case CoinType.DROP: return COINS_DropCoinCollected;
        case CoinType.LUCK: return COINS_LuckCollected;
        default:
#if CODEDEBUG
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            LogError(METHOD_NAME, "type {0} is invalid", type);
#endif
            return COINS_SmallCoinCollected;
        }
    }
    void COINS_SmallCoinCollected(CoinSharedData sharedData)
    {
        pch_coins_nb_collected += active_coinsx_mult;

        if (coins_arc_trigger_inside) {
            audio_small_coins_pitch *= 1.12246f;
        }

        ReportPlaycharCoinCollected(sharedData);
    }
    void COINS_MediumCoinCollected(CoinSharedData sharedData)
    {
        pch_coins_b_collected += sharedData.coin_value * active_coinsx_mult;

        ReportPlaycharCoinCollected(sharedData);
    }
    void COINS_MagnetCoinCollected(CoinSharedData sharedData)
    {
        COINS_MagnetCoin_SetActive(true);
        ReportPlaycharCoinCollected(sharedData);
    }
    void COINS_MagnetCoin_SetActive(bool active)
    {
        playchar_ctr.GC_SetMagnetActive(active);
        //handitem
        handitem_magnet_tr.SetParent(active ? playchar_ctr.HandSlot() : hidden_node, false);

        //UI
        ui_playing_main_items_magnet_go.SetActive(active);
        if (active) ui_playing_main_items_magnet_tween.Restart(SelectedPlaycharMagnetTime());
    }
    void COINS_MagnetCoinComplete()
    {
        COINS_MagnetCoin_SetActive(false);

        //AUDIO
        AUD_UI_Sound(UiSoundType.ITEM_ENDED);
    }
    void COINS_StaminaCoinCollected(CoinSharedData sharedData)
    {
        playchar_ctr.StaminaIncrease(sharedData.coin_value);

        ReportPlaycharCoinCollected(sharedData);
    }
    void COINS_LuckxCoinCollected(CoinSharedData sharedData)
    {
        COINS_LuckxCoin_SetActive(true);
        ReportPlaycharCoinCollected(sharedData);
    }
    void COINS_LuckxCoin_SetActive(bool active)
    {
        active_run_luckx = active;
        PLAYCHAR_UpdateLuckX();

        //UI
        ui_playing_main_items_luckx_go.SetActive(active);
        if (active) ui_playing_main_items_luckx_tween.Restart(SelectedPlaycharLuckxTime());
    }
    void COINS_LuckxCoinComplete()
    {
        COINS_LuckxCoin_SetActive(false);

        //AUDIO
        AUD_UI_Sound(UiSoundType.ITEM_ENDED);
    }
    void COINS_CoinsxCoinCollected(CoinSharedData sharedData)
    {
        COINS_CoinsxCoin_SetActive(true);
        ReportPlaycharCoinCollected(sharedData);
    }
    void COINS_CoinsxCoin_SetActive(bool active)
    {
        active_run_coinsx = active;
        PLAYCHAR_UpdateCoinsX();

        //UI
        ui_playing_main_items_coinsx_go.SetActive(active);
        if (active) ui_playing_main_items_coinsx_tween.Restart(SelectedPlaycharCoinsxTime());
    }
    void COINS_CoinsxCoinComplete()
    {
        COINS_CoinsxCoin_SetActive(false);

        //AUDIO
        AUD_UI_Sound(UiSoundType.ITEM_ENDED);
    }
    void COINS_ScorexCoinCollected(CoinSharedData sharedData)
    {
        COINS_ScorexCoin_SetActive(true);
        ReportPlaycharCoinCollected(sharedData);
    }
    void COINS_ScorexCoin_SetActive(bool active)
    {
        active_run_scorex = active;
        PLAYCHAR_UpdateScoreX();

        //UI
        ui_playing_main_items_scorex_go.SetActive(active);
        if (active) ui_playing_main_items_scorex_tween.Restart(SelectedPlaycharScorexTime());
    }
    void COINS_ScorexCoinComplete()
    {
        COINS_ScorexCoin_SetActive(false);

        //AUDIO
        AUD_UI_Sound(UiSoundType.ITEM_ENDED);
    }
    void COINS_MBoxCoinCollected(CoinSharedData sharedData)
    {
        user_rewards_pending.Add(new UserReward() { type = UserInvItemType.MBOX, amount = 1 });

        ReportPlaycharCoinCollected(sharedData);
    }
    void COINS_SMBoxCoinCollected(CoinSharedData sharedData)
    {
        user_rewards_pending.Add(new UserReward() { type = UserInvItemType.SMBOX, amount = 1 });

        ReportPlaycharCoinCollected(sharedData);
    }
    void COINS_DropCoinCollected(CoinSharedData sharedData)
    {
        us.drops[selected_playchar_slot_index] += sharedData.coin_value;

        ReportPlaycharCoinCollected(sharedData);
    }
    void COINS_LuckCollected(CoinSharedData sharedData)
    {
        pch_luck_collected += sharedData.coin_value;
        us.luck += sharedData.coin_value;
        PLAYCHAR_UpdateTotalLuck();

        ReportPlaycharCoinCollected(sharedData);
    }
    void COINS_CompleteAllActiveCoins()
    {
        ui_playing_main_items_coinsx_tween.Complete(false);
        COINS_CoinsxCoin_SetActive(false);
        ui_playing_main_items_scorex_tween.Complete(false);
        COINS_ScorexCoin_SetActive(false);
        ui_playing_main_items_luckx_tween.Complete(false);
        COINS_LuckxCoin_SetActive(false);
        ui_playing_main_items_magnet_tween.Complete(false);
        COINS_MagnetCoin_SetActive(false);
    }
    void COINS_ArcPlacing(Vector3 offset, Transform parent)
    {
        offset.z -= 0.5f;
        offset.y -= 0.5f;
        coins_arc_trigger_scale.y = offset.y + playchar_ctr.jump_height * 2f;
        coins_arc_trigger_scale.z = playchar_ctr.JumpLength() + 1f;

        coins_arc_trigger_pool.PlaceAsArcTrigger(offset, coins_arc_trigger_scale, parent);
    }
    void COINS_ArcTriggerEnter()
    {
        audio_small_coins_pitch = coins_so.audio_small_coin_begin_pitch;
        coins_arc_trigger_inside = true;
    }
    void COINS_ArcTriggerExit()
    {
        audio_small_coins_pitch = coins_so.audio_small_coin_begin_pitch;
        coins_arc_trigger_inside = false;
    }
    #endregion //[Coins]

    #region [Network]
#if PHPDEBUG
#if PHPLOCAL
    const string QURL = "localhost/runner/query.php?XDEBUG_SESSION_START=1";
#else
    //const string QURL = "http://leetcrowd.co.nf/query.php?XDEBUG_SESSION_START=1";
    //const string QURL = "http://fatehtkd.az/query.php?XDEBUG_SESSION_START=1";
    const string QURL = "http://bakiya-sefer.com/query.php?XDEBUG_SESSION_START=1";
#endif
#else
#if PHPLOCAL
    const string QURL = "localhost/runner/query.php";
#else
    //const string QURL = "http://leetcrowd.co.nf/query.php";
    //const string QURL = "http://fatehtkd.az/query.php";
    const string QURL = "http://bakiya-sefer.com/query.php";
    const string QURL_PULL = "http://bakiya-sefer.com/query_pull.php";
    const string QURL_PUSH = "http://bakiya-sefer.com/query_push.php";
    const string QURL_IDSCORE = "http://bakiya-sefer.com/query_idscore.php";
    const string QURL_TOPSCORE = "http://bakiya-sefer.com/query_topscore.php";
    const string QURL_ICON_PREFIX = "http://bakiya-sefer.com/";
    const string QURL_FBPAGE = "https://www.facebook.com/bakiyasefer";
#endif
#endif
    const float QTIMEOUT = 30f;
    const int QRETRY_MAX = 3;
    const int QNETID = 0;

    bool qonline = false;
    public bool IsOnline() { return qonline; }

    System.Uri quri_pull = null;
    System.Uri quri_push = null;
    System.Uri quri_idscore = null;
    System.Uri quri_topscore = null;

    NoteInfo qoffline_note = null;

    bool qpull_finished = false;
    bool qidscore_finished = false;
    bool qtopscore_finished = false;
    bool IsPullFinished() { return qpull_finished; }
    bool IsScoreFinished() { return qidscore_finished && qtopscore_finished; }

    const string QDO_PULL = "pull";
    const string QDO_PUSH = "push";
    const string QDO_IDSCORE = "idscore";
    const string QDO_TOPSCORE = "topscore";

    const string QF_DO = "do";
    const string QF_DATA = "data";
    const string QF_CODE = "code";
    const string QF_FBID = "fbid";
    const string QF_NETID = "netid";
    const string QF_CHPROG = "chp";
    const string QF_CHPROGID = "chpid";
    const string QF_CHDAY = "chd";
    const string QF_CHDAYID = "chdid";
    const string QF_CHSPEC = "chs";
    const string QF_CHSPECID = "chsid";
    const string QF_SHOP = "shop";
    const string QF_SHOPID = "shopid";
    const string QF_REWARDS = "rwds";
    const string QF_REWARDSID = "rwdsid";
    const string QF_SIGNUP = "signup";
    const string QF_EVENT = "event";
    const string QF_EVENTID = "evtid";
    const string QF_STATE = "state";
    const string QF_PADARKA = "pdrk";
    const string QF_IDS = "ids";
    const string QF_SCORE = "score";

    const int QCODE_SUCCESS = 200;
    const int QCODE_FAIL = 0;
    const int QCODE_QVERSION_DIFFER = 401;
    const int QCODE_INVALID_FIELDS = 402;
    const int QCODE_INVALID_DATA = 403;

    public event Event onOnlineStateChanged = null;

    void QUERY_Init()
    {
        HTTPManager.MaxConnectionPerServer = 1;
        HTTPManager.ConnectTimeout = System.TimeSpan.FromSeconds(1);
        HTTPManager.RequestTimeout = System.TimeSpan.FromSeconds(2);

        quri_pull = new System.Uri(QURL_PULL);
        quri_push = new System.Uri(QURL_PUSH);
        quri_idscore = new System.Uri(QURL_IDSCORE);
        quri_topscore = new System.Uri(QURL_TOPSCORE);

        if (qoffline_note == null) {
            qoffline_note = new NoteInfo() { text = Localize(ui_so.note_offline) };
        }
    }
    void QUERY_DoPull()
    {
#if CODEDEBUG
        string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
#endif
        if (!IsOnline()) {
#if CODEDEBUG
            LogError(METHOD_NAME, "is Offline");
#endif
            return;
        }
        if (!FB.IsLoggedIn) {
#if CODEDEBUG
            LogError(METHOD_NAME, " FB.IsLoggedIn returned FALSE");
#endif
            return;
        }
        //fill pull data
        IDictionary<string, object> qdata_out = new Dictionary<string, object>();
        qdata_out[QF_FBID] = AccessToken.CurrentAccessToken.UserId;
        qdata_out[QF_CHPROGID] = chprog_data.id;
        qdata_out[QF_CHDAYID] = chday_data.id;
        qdata_out[QF_CHSPECID] = chday_data.id;
        qdata_out[QF_SHOPID] = shop_data.id;
        qdata_out[QF_REWARDSID] = reward_data.id;

        //disable input
        UI_BEGIN_SetInputEnabled(false);
        var request = new HTTPRequest(quri_pull, HTTPMethods.Post, (req, resp) => {
            switch (req.State) {
            case HTTPRequestStates.Finished:
#if CODEDEBUG
                LogSuccess(METHOD_NAME, "{0} Request Success", QDO_PULL);
#endif
                qpull_finished = true;
                QUERY_PullSuccess(Json.Deserialize(resp.DataAsText) as IDictionary<string, object>);
                break;
            default:
#if CODEDEBUG
                QUERY_Error(req, QDO_PULL);
#endif
                QUERY_SetOnlineStatus(false);
                break;
            }

            //enable input
            UI_BEGIN_SetInputEnabled(true);
        });
        request.AddField(QF_DATA, Json.Serialize(qdata_out));
        request.Send();
#if CODEDEBUG
        Log(METHOD_NAME, "Pull Query Sent");
#endif
    }
    void QUERY_DoPush(string serializedState)
    {
#if CODEDEBUG
        string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
#endif
        if (!IsOnline()) {
#if CODEDEBUG
            LogError(METHOD_NAME, "is Offline");
#endif
            return;
        }
        if (!FB.IsLoggedIn) {
#if CODEDEBUG
            LogError(METHOD_NAME, "FB.IsLoggedIn returned FALSE");
#endif
            return;
        }
        //fill push data
        IDictionary<string, object> qdata_out = new Dictionary<string, object>();
        qdata_out[QF_FBID] = AccessToken.CurrentAccessToken.UserId;
        qdata_out[QF_STATE] = serializedState;
        if (us_score_needs_save) {
            qdata_out[QF_SCORE] = us.highscore;
        }
        if (user_event_id > 0) {
            qdata_out[QF_EVENT] = user_event_id;
        }

        var request = new HTTPRequest(quri_push, HTTPMethods.Post, (req, resp) => {
            switch (req.State) {
            case HTTPRequestStates.Finished:
#if CODEDEBUG
                LogSuccess(METHOD_NAME, "{0} Request Success", QDO_PUSH);
#endif
                QUERY_PushSuccess();
                break;
            default:
#if CODEDEBUG
                QUERY_Error(req, QDO_PUSH);
#endif
                QUERY_SetOnlineStatus(false);
                break;
            }
        });
        request.AddField(QF_DATA, Json.Serialize(qdata_out));
        request.Send();
#if CODEDEBUG
        Log(METHOD_NAME, "Push Query Sent");
#endif
    }
    void QUERY_DoIdscore(string serializedIds)
    {
#if CODEDEBUG
        string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
#endif
        if (!IsOnline()) {
#if CODEDEBUG
            LogError(METHOD_NAME, "is Offline");
#endif
            return;
        }
        if (!FB.IsLoggedIn) {
#if CODEDEBUG
            LogError(METHOD_NAME, " FB.IsLoggedIn returned FALSE");
#endif
            return;
        }
        //fill data
        IDictionary<string, object> qdata_out = new Dictionary<string, object>();
        qdata_out[QF_IDS] = serializedIds;

        var request = new HTTPRequest(quri_idscore, HTTPMethods.Post, (req, resp) => {
            switch (req.State) {
            case HTTPRequestStates.Finished:
#if CODEDEBUG
                LogSuccess(METHOD_NAME, "{0} Request Success", QDO_IDSCORE);
#endif
                qidscore_finished = true;
                QUERY_IdScoreSuccess(Json.Deserialize(resp.DataAsText) as IDictionary<string, object>);
                break;
            default:
#if CODEDEBUG
                QUERY_Error(req, QDO_IDSCORE);
#endif
                QUERY_SetOnlineStatus(false);
                break;
            }
        });
        request.AddField(QF_DATA, Json.Serialize(qdata_out));
        request.Send();
#if CODEDEBUG
        Log(METHOD_NAME, "IdScore Query Sent");
#endif
    }
    void QUERY_DoTopScores()
    {
#if CODEDEBUG
        string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
#endif
        if (!IsOnline()) {
#if CODEDEBUG
            LogError(METHOD_NAME, "is Offline");
#endif
            return;
        }
        if (!FB.IsLoggedIn) {
#if CODEDEBUG
            LogError(METHOD_NAME, " FB.IsLoggedIn returned FALSE");
#endif
            return;
        }
        var request = new HTTPRequest(quri_topscore, HTTPMethods.Post, (req, resp) => {
            switch (req.State) {
            case HTTPRequestStates.Finished:
#if CODEDEBUG
                LogSuccess(METHOD_NAME, "{0} Request Success", QDO_TOPSCORE);
#endif
                qtopscore_finished = true;
                QUERY_TopScoreSuccess(Json.Deserialize(resp.DataAsText) as IDictionary<string, object>);
                break;
            default:
#if CODEDEBUG
                QUERY_Error(req, QDO_TOPSCORE);
#endif
                QUERY_SetOnlineStatus(false);
                break;
            }
        });
        request.AddField(QF_DATA, string.Empty);
        request.Send();
#if CODEDEBUG
        Log(METHOD_NAME, "TopScore Request Sent");
#endif
    }
#if CODEDEBUG
    void QUERY_Error(HTTPRequest req, string reqType)
    {
        string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
        switch (req.State) {
        case HTTPRequestStates.Error:
            LogError(METHOD_NAME, "{0} Error: {1}", reqType, req.Exception != null ? req.Exception.Message : string.Empty);
            break;
        case HTTPRequestStates.Aborted:
            LogWarning(METHOD_NAME, "{0} Aborted", reqType);
            break;
        case HTTPRequestStates.ConnectionTimedOut:
            LogError(METHOD_NAME, "{0} Connection Timeout", reqType);
            break;
        case HTTPRequestStates.TimedOut:
            LogError(METHOD_NAME, "{0} Process Request Timeout", reqType);
            break;
        }
    }
#endif
    string QUERY_GetHtmlFromUri(string resource)
    {
        string html = string.Empty;
        var req = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(resource);
        req.Timeout = 2000;
        req.ReadWriteTimeout = 2000;
        try {
            using (var resp = (System.Net.HttpWebResponse)req.GetResponse()) {
                bool success = (int)resp.StatusCode >= 200 && (int)resp.StatusCode < 299;
                if (success) {
                    using (var reader = new System.IO.StreamReader(resp.GetResponseStream())) {
                        char[] cs = new char[80];
                        reader.Read(cs, 0, cs.Length);
                        html = new string(cs);
                    }
                }
            }
        } catch {
            return string.Empty;
        }
        return html;
    }
    public event Event onLoginStateChanged = null;
    const string QUERY_TEST_URI = "http://google.com";
    const string QUERY_TEXT_SUBSTRING = "schema.org";
    void QUERY_CheckOnlineStatus()
    {
        string html = QUERY_GetHtmlFromUri(QUERY_TEST_URI);
        QUERY_SetOnlineStatus(!string.IsNullOrEmpty(html) && html.Contains(QUERY_TEXT_SUBSTRING));
    }
    void QUERY_SetOnlineStatus(bool isOnline)
    {
        qonline = isOnline;
#if CODEDEBUG
        string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
        Log(METHOD_NAME, qonline ? "is Online" : "is Offline");
#endif
        if (onOnlineStateChanged != null) { onOnlineStateChanged(); }
    }
    void QUERY_PullSuccess(IDictionary<string, object> data)
    {
        //Code
        if (!QUERY_CheckCode(data)) return;

        //Progress Challenges
        //QUERY_Pull_ProgressChallenges();

        //Special Challenges
        //QUERY_Pull_SpecialChallenges();

        //State
        QUERY_Pull_State(data);

        //Event
        //QUERY_Pull_Event();
    }
    void QUERY_PushSuccess()
    {
        us_score_needs_save = false;
    }
    void QUERY_IdScoreSuccess(IDictionary<string, object> data)
    {
        //Code
        if (!QUERY_CheckCode(data)) return;

        object ids_obj;
        if (!data.TryGetValue(QF_IDS, out ids_obj)) {
#if CODEDEBUG
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            LogWarning(METHOD_NAME, "cannot find ids");
#endif
            return;
        }
        var ids = ids_obj as IDictionary<string, object>;
        if (ids == null) {
#if CODEDEBUG
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            LogWarning(METHOD_NAME, "invalid ids");
#endif
            return;
        }
        UI_FB_Query_FriendScores_Complete(ids);
    }
    void QUERY_TopScoreSuccess(IDictionary<string, object> data)
    {
        object topids_obj;
        if (!data.TryGetValue(QF_IDS, out topids_obj)) {
#if CODEDEBUG
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            LogWarning(METHOD_NAME, "cannot find topids");
#endif
            return;
        }
        var topids = topids_obj as IDictionary<string, object>;
        if (topids == null) {
#if CODEDEBUG
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            LogWarning(METHOD_NAME, "invalid topids");
#endif
            return;
        }
        UI_FB_Query_TopAllScores_Complete(topids);
    }
    bool QUERY_CheckCode(IDictionary<string, object> data)
    {
        object val;
        if (!data.TryGetValue(QF_CODE, out val)) return false;

        int code = System.Convert.ToInt32(val);
        switch (code) {
        case QCODE_SUCCESS:
#if CODEDEBUG
 {
                string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
                LogSuccess(METHOD_NAME, "Code: Success");
            }
#endif
            return true;
        case QCODE_QVERSION_DIFFER:
#if CODEDEBUG
 {
     string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
     LogWarning(METHOD_NAME, "Code: Version Differ");
 }
#endif
            return false;
        case QCODE_INVALID_FIELDS:
#if CODEDEBUG
 {
     string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
     LogWarning(METHOD_NAME, "Code: Invalid Fields");
 }
#endif
            return false;
        case QCODE_INVALID_DATA:
#if CODEDEBUG
 {
     string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
     LogWarning(METHOD_NAME, "Code: Invalid Data");
 }
#endif
            return false;
        case QCODE_FAIL:
#if CODEDEBUG
 {
     string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
     LogWarning(METHOD_NAME, "Code: Fail");
 }
#endif
            return false;
        default: return false;
        }
    }
    /*
        void QUERY_Pull_ProgressChallenges()
        {
            object val;
            if (!qdata_in.TryGetValue(QF_CHPROGID, out val)) {
                return;
            }
            int chprog_id = (int)val;

            if (!qdata_in.TryGetValue(QF_CHPROG, out val)) {
                return;
            }
            string chprog_text = val as string;
            if (!System.String.IsNullOrEmpty(chprog_text)) {
                var ch = USER_LoadChallenges(chprog_text);
                if (ch != null && ch.id == chprog_id) {
                    chprog_data = ch;
                }
            }
        }*/
    /*
        void QUERY_Pull_SpecialChallenges()
        {
            object val;
            if (!qdata_in.TryGetValue(QF_CHSPECID, out val)) {
                return;
            }
            int chspec_id = (int)val;

            if (!qdata_in.TryGetValue(QF_CHSPEC, out val)) {
                return;
            }
            string chspec_text = val as string;
            if (!System.String.IsNullOrEmpty(chspec_text)) {
                var ch = USER_LoadChallenges(chspec_text);
                if (ch != null && ch.id == chspec_id) {
                    chday_data = ch;
                }
            }
        }*/
    void QUERY_Pull_State(IDictionary<string, object> data)
    {
#if CODEDEBUG
        if (!FB.IsLoggedIn) {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            LogError(METHOD_NAME, "Not logged in");
            return;
        }
        if (string.IsNullOrEmpty(AccessToken.CurrentAccessToken.UserId)) {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            LogError(METHOD_NAME, "FBID is NULL");
            return;
        }
#endif
        object val;

        //SignUp
        int signup = 0;
        if (data.TryGetValue(QF_SIGNUP, out val)) {
            signup = System.Convert.ToInt32(val);
        }

        //State
        if (signup == 0) {
            /* User Logged In
               Remove offline progress, but dont touch OFFLINE_FILE
               Load state from PULL
               if PushFail - save FBID_FILE
            */
            if (data.TryGetValue(QF_STATE, out val)) {
                USER_LoadState(val as string, AccessToken.CurrentAccessToken.UserId, config_data.us_id == AccessToken.CurrentAccessToken.UserId);
            }
        } else {
            /* User Signed Up.
               Leave offline progress as User's progress
               Dont load state from PULL
               if PushSuccess - delete OFFLINE_FILE
               else delete OFFLINE_FILE, save FBID_FILE
            */
#if CODEDEBUG
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            LogSuccess(METHOD_NAME, "user signup");
#endif
            //add rewards. save state after
            user_rewards_pending.AddRange(reward_data.signup.rewards);
            USER_CheckRewardsAdded(USER_Signup_RewardsComplete);
        }
    }
    void QUERY_Pull_Event(IDictionary<string, object> data)
    {
        object val;

        if (!data.TryGetValue(QF_EVENT, out val)) {
            return;
        }
        IDictionary<string, object> evt = val as IDictionary<string, object>;
        if (evt == null) {
            return;
        }

        //Event ID
        if (!evt.TryGetValue(QF_EVENTID, out val)) {
            return;
        }
        user_event_id = (int)val;

        //Padarka
        if (evt.TryGetValue(QF_PADARKA, out val)) {
            string rewards_text = val as string;
            if (!System.String.IsNullOrEmpty(rewards_text)) {
                USER_LoadRewards(rewards_text);
            }
        }
    }


    #region [Facebook]
    readonly string[] FB_LOGIN_SCOPE = { "public_profile", "email", "user_friends" };
    static void FB_QUERY_UserFirstName(FacebookDelegate<IGraphResult> callback)
    {
        FB.API("/me?fields=name,first_name", HttpMethod.GET, callback);
    }
    static void FB_QUERY_UserFriends(FacebookDelegate<IGraphResult> callback)
    {
        FB.API("/me?fields=friends", HttpMethod.GET, callback);
    }
    static void FB_QUERY_IdName(string id, FacebookDelegate<IGraphResult> callback)
    {
        FB.API(string.Format("/{0}?fields=name", id), HttpMethod.GET, callback);
    }
    static void FB_QUERY_IdPicture(string id, FacebookDelegate<IGraphResult> callback)
    {
        FB.API(string.Format("{0}/picture", id), HttpMethod.GET, callback);
    }
    static void FB_QUERY_GetPermissions(FacebookDelegate<IGraphResult> callback)
    {
        FB.API("/me/permissions", HttpMethod.GET, callback);
    }
    static void FB_QUERY_UploadPhoto(WWWForm form, FacebookDelegate<IGraphResult> callback)
    {
        FB.API("/me/photos", HttpMethod.POST, callback, form);
    }
    void FB_Feed()
    {
#if CODEDEBUG
        string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
#endif
        if (!GameController.Instance.IsOnline()) {
#if CODEDEBUG
            GameController.LogWarning(METHOD_NAME, "Is Offline");
#endif
            return;
        }
        if (!FB.IsInitialized) {
#if CODEDEBUG
            GameController.LogError(METHOD_NAME, "FB isInitialized returned FALSE");
#endif
            return;
        }
        if (!FB.IsLoggedIn) {
#if CODEDEBUG
            GameController.LogError(METHOD_NAME, "FB isLoggedIn returned FALSE");
#endif
            return;
        }

        var gc = GameController.Instance;
        FB.ShareLink(
            contentURL: new System.Uri(QURL_FBPAGE),
            contentTitle: string.Format(gc.ui_so.fb_feed_link_caption, GameController.Instance.USER_Highscore()),
            photoURL: new System.Uri(QURL_ICON_PREFIX + gc.SelectedPlaychar().ui_playchar_icon_name_hs),
            callback: FB_OnFeedComplete
            );
    }
    void FB_OnFeedComplete(IShareResult res)
    {
        //release coroutine wait flag
        ui_menu_backbtn_coroutine_wait = false;
    }

    const float FB_INIT_TIMEOUT = 3f;
    bool fb_initializing = false;
    void FB_Init()
    {
#if CODEDEBUG
        string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
#endif
        if (!IsOnline()) {
#if CODEDEBUG
            LogWarning(METHOD_NAME, "Is Offline");
#endif
            return;
        }
        if (FB.IsInitialized) {
#if CODEDEBUG
            LogWarning(METHOD_NAME, "FB is initialized");
#endif
            return;
        }
        if (fb_initializing) {
#if CODEDEBUG
            LogWarning(METHOD_NAME, "FB Init already in progress");
#endif
            return;
        }

        //fb init timeout
        ui_timer.Complete(true);
        ui_timer.ClearEvents();
        ui_timer.SetOnCompleteOnce(FB_OnInitFailed);
        ui_timer.Reset(FB_INIT_TIMEOUT);

        //disable input
        UI_BEGIN_SetInputEnabled(false);
        fb_initializing = true;
        try {
            FB.Init(FB_OnInitComplete);
        } catch {
#if CODEDEBUG
            LogError(METHOD_NAME, "FB init exception");
#endif
            FB_OnInitFailed();
        }
    }
    void FB_OnInitComplete()
    {
#if CODEDEBUG
        string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
#endif
        //stop timeout timer
        if (ui_timer.IsEnabled()) { ui_timer.Complete(false); ui_timer.ClearEvents(); }

        if (!FB.IsInitialized) {
#if CODEDEBUG
            LogError(METHOD_NAME, "FB.IsInitialized returned FALSE");
#endif
            return;
        }
        //check Timeout
        if (!IsOnline()) {
#if CODEDEBUG
            LogWarning(METHOD_NAME, "FB Init Timeout");
#endif
            return;
        }

        string id = FB.IsLoggedIn ? AccessToken.CurrentAccessToken.UserId : config_data.us_id;
        if (us == null || id != config_data.us_id) {
#if CODEDEBUG
            Log(METHOD_NAME, "Loading Local State");
#endif
            USER_LoadState(USER_FileReadAllText(id, true), id, false);
        }
#if CODEDEBUG
 else {
     Log(METHOD_NAME, "State already loaded");
        }
#endif

        if (FB.IsLoggedIn) {
#if CODEDEBUG
            Log(METHOD_NAME, "Auto Login");
#endif
            FB_OnLoginSuccess();
        } else {
            //enable input
            UI_BEGIN_SetInputEnabled(true);
        }
    }
    void FB_OnInitFailed()
    {
#if CODEDEBUG
        string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
        LogError(METHOD_NAME, "FB Init Timeout");
#endif
        QUERY_SetOnlineStatus(false);
#if CODEDEBUG
        Log(METHOD_NAME, "Loading Local State");
#endif
        //load last played state
        USER_LoadState(USER_FileReadAllText(config_data.us_id, true), config_data.us_id, false);
        //enable input
        UI_BEGIN_SetInputEnabled(true);
    }
    void FB_Login()
    {
#if CODEDEBUG
        string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
#endif
        if (!IsOnline()) {
#if CODEDEBUG
            LogError(METHOD_NAME, "is Offline");
#endif
            return;
        }
        if (!FB.IsInitialized) {
#if CODEDEBUG
            LogError(METHOD_NAME, "FB.IsInitialized is FALSE");
#endif
            return;
        }
        if (FB.IsLoggedIn) {
#if CODEDEBUG
            LogError(METHOD_NAME, "FB.IsLoggedIn is TRUE");
#endif
            return;
        }
        FB.LogInWithReadPermissions(FB_LOGIN_SCOPE, FB_OnLoginComplete);
    }
    void FB_OnLoginComplete(ILoginResult result)
    {
#if CODEDEBUG
        string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
#endif
        if (result.Error != null) {
#if CODEDEBUG
            LogWarning(METHOD_NAME, "result.Error:\n{0}", result.Error);
#endif
        } else if (FB.IsLoggedIn) {
#if CODEDEBUG
            LogSuccess(METHOD_NAME, "FB.Login success");
#endif
            InvokeOnMT(FB_OnLoginSuccess);
        }
#if CODEDEBUG
 else {
            LogWarning(METHOD_NAME, "FB.IsLoggerIn is FALSE");
        }
#endif
    }
    void FB_OnLoginSuccess()
    {
        if (IsOnline()) {
            qpull_finished = qidscore_finished = qtopscore_finished = false;
            ui_menu_fb_top_go.SetActive(false);
            QUERY_DoPull();
            QUERY_DoTopScores();
        }
        if (onLoginStateChanged != null) { onLoginStateChanged(); }
    }
    void FB_Logout()
    {
#if CODEDEBUG
        string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
        if (!IsOnline()) {
            LogError(METHOD_NAME, "is_online is FALSE");
            return;
        }
        if (!FB.IsInitialized) {
            LogError(METHOD_NAME, "FB.IsInitialized is FALSE");
            return;
        }
        if (!FB.IsLoggedIn) {
            LogError(METHOD_NAME, "FB.IsLoggedIn is FALSE");
            return;
        }
        Log(METHOD_NAME, "fb logout");
#endif
        FB.LogOut();

        //delay logout handler
        ui_timer.Complete(true);
        ui_timer.ClearEvents();
        ui_timer.SetOnCompleteOnce(FB_LogoutSuccess);
        ui_timer.Reset(0.2f);
    }
    void FB_LogoutSuccess()
    {
#if CODEDEBUG
        if (FB.IsLoggedIn) {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            LogError(METHOD_NAME, "Still Logged In");
        }
#endif
        USER_LoadState(USER_FileReadAllText(OFFLINE_ID, true), OFFLINE_ID, false);
        if (onLoginStateChanged != null) { onLoginStateChanged(); }
    }
    void FB_FeedHighscore()
    {
#if CODEDEBUG
        string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
#endif
        if (!IsOnline()) {
#if CODEDEBUG
            LogWarning(METHOD_NAME, "Is Offline");
#endif
            return;
        }
        if (!FB.IsInitialized) {
#if CODEDEBUG
            LogError(METHOD_NAME, "FB isInitialized returned FALSE");
#endif
            return;
        }
        if (!FB.IsLoggedIn) {
#if CODEDEBUG
            LogError(METHOD_NAME, "FB isLoggedIn returned FALSE");
#endif
            return;
        }

        FB.ShareLink(
            contentURL: new System.Uri(QURL_FBPAGE),
            contentTitle: string.Format(ui_so.fb_feed_link_caption, GameController.Instance.USER_Highscore()),
            photoURL: new System.Uri(QURL_ICON_PREFIX + SelectedPlaychar().ui_playchar_icon_name_hs),
            callback: FB_OnFeedComplete
            );

        /*FB.FeedShare(
            link: new System.Uri(QURL_FBPAGE),
            linkName: string.Format(ui_so.fb_feed_link_name, GameController.Instance.USER_Highscore()),
            linkCaption: string.Format(ui_so.fb_feed_link_caption, GameController.Instance.USER_Highscore()),
            picture: new System.Uri(QURL_ICON_PREFIX + SelectedPlaychar().ui_playchar_icon_name_hs),
            callback: FB_OnFeedComplete
            );*/
    }
    #endregion //[Facebook]
    #endregion //[Network]

    #region [User]
    public const string OFFLINE_ID = "off";
    const string CHPROG_ASSET = "chprog";
    const string CHDAY_ASSET = "chday";
    const string CHSPEC_ASSET = "chspec";
    const string SHOP_ASSET = "shop";
    const string REWARD_ASSET = "reward";
    const string CONFIG_ASSET = "config";

    ChallengeSerializeData chprog_data = null;
    ChallengeSerializeData chday_data = null;
    ChallengeSerializeData chspec_data = null;
    RewardSerializeData reward_data = null;
    ShopSerializeData shop_data = null;
    ConfigSerializeData config_data = null;

    UserState us = null;
    bool us_needs_save = false;
    bool us_score_needs_save = false;
    bool user_rewards_showing = false;
    int user_event_id = 0;

    List<UserReward> user_rewards_pending = new List<UserReward>(10);

    public event Event onUserStateChanged = null;
    public event Event<int> onUserCoinSpend = null;
    public event Event<int> onUserLuckSpend = null;
    public event Event<UserInvItemType, int> onUserItemBuy = null;
    public event Event<PlaycharLevelType> onUserLevelBuy = null;
    public event Event onUserHighscoreChanged = null;
    public event Event<int> onUserScore = null;

    public int USER_Highscore()
    {
        return us.highscore;
    }
    bool USER_Buy(PriceData price)
    {
        switch (price.type) {
        case CurrencyType.LUCK: return USER_BuyForLuck(price.value);
        case CurrencyType.COINS: return USER_BuyForCoins(price.value);
        default: return false;
        }
    }
    bool USER_BuyForCoins(int value)
    {
        if (us.coins >= value) {
            us.coins -= value;
            //challenge event
            if (onUserCoinSpend != null) { onUserCoinSpend(value); }
            USER_OnUserStateChanged();
            return true;
        }
        return false;
    }
    bool USER_BuyForLuck(int value)
    {
        if (us.luck >= value) {
            us.luck -= value;
            //challenge event
            if (onUserLuckSpend != null) { onUserLuckSpend(value); }
            USER_OnUserStateChanged();
            return true;
        }
        return false;
    }
    int USER_NumItems(UserInvItemType type)
    {
        switch (type) {
        case UserInvItemType.LUCK: return us.luck;
        case UserInvItemType.COINS: return us.coins;
        case UserInvItemType.DROPS: return us.drops[selected_playchar_slot_index];
        case UserInvItemType.COINS_X: return us.coins_x;
        case UserInvItemType.SCORE_X: return us.score_x;
        case UserInvItemType.LUCK_X: return us.luck_x;
        default: return 0;
        }
    }
    UserReward[] USER_GetActiveChprogRewards()
    {
        if (us.chprog_random_index < 0) {
            return reward_data.chprog[us.chprog_prog_index].rewards;
        } else {
            return reward_data.chday.items[us.chprog_reward_index].rewards;
        }
    }
    UserReward[] USER_GetActiveChdayRewards()
    {
        return reward_data.chday.items[us.chday_reward_index].rewards;
    }
    UserReward[] USER_GetActiveChspecRewards()
    {
        return reward_data.chspec.items[us.chspec_reward_index].rewards;
    }
    void USER_Init()
    {
#if CODEDEBUG
        string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
#endif
        //Load config data
        config_data = null;
        string filetext = USER_FileReadAllText(CONFIG_ASSET);
        if (!string.IsNullOrEmpty(filetext)) { config_data = USER_DeserializeFromContent<ConfigSerializeData>(filetext); }
        if (config_data == null) {
#if CODEDEBUG
            Log(METHOD_NAME, "Creating New Config");
#endif
            config_data = USER_NewConfigData();
        }
        //init localize system
        LOCALIZE_Init();
        //init ad system
        AD_Init();

        //Load shop data
        shop_data = null;
        filetext = USER_FileReadAllText(SHOP_ASSET);
        if (!string.IsNullOrEmpty(filetext)) { shop_data = USER_DeserializeFromContent<ShopSerializeData>(filetext); }
        if (shop_data == null) { shop_data = Resources.Load<ShopScriptableObject>(SHOP_ASSET).shop_data; }
#if CODEDEBUG
        if (shop_data == null) { LogError(METHOD_NAME, "{0} asset is NULL", SHOP_ASSET); }
#endif
        //Load progress challenges data
        chprog_data = null;
        filetext = USER_FileReadAllText(CHPROG_ASSET);
        if (!string.IsNullOrEmpty(filetext)) { chprog_data = USER_DeserializeFromContent<ChallengeSerializeData>(filetext); }
        if (chprog_data == null) { chprog_data = Resources.Load<ChallengeScriptableObject>(CHPROG_ASSET).ch; }
#if CODEDEBUG
        if (chprog_data == null) { LogError(METHOD_NAME, "{0} asset is NULL", CHPROG_ASSET); }
#endif
        //Load daily challenges data
        chday_data = null;
        filetext = USER_FileReadAllText(CHDAY_ASSET);
        if (!string.IsNullOrEmpty(filetext)) { chday_data = USER_DeserializeFromContent<ChallengeSerializeData>(filetext); }
        if (chday_data == null) { chday_data = Resources.Load<ChallengeScriptableObject>(CHDAY_ASSET).ch; }
#if CODEDEBUG
        if (chday_data == null) { LogError(METHOD_NAME, "{0} asset is NULL", CHDAY_ASSET); }
#endif
        //Load special challenges data
        chspec_data = null;
        filetext = USER_FileReadAllText(CHSPEC_ASSET);
        if (!string.IsNullOrEmpty(filetext)) { chspec_data = USER_DeserializeFromContent<ChallengeSerializeData>(filetext); }
        if (chspec_data == null) { chspec_data = Resources.Load<ChallengeScriptableObject>(CHSPEC_ASSET).ch; }
#if CODEDEBUG
        if (chspec_data == null) { LogError(METHOD_NAME, "{0} asset is NULL", CHSPEC_ASSET); }
#endif
        //Load reward data
        reward_data = null;
        filetext = USER_FileReadAllText(REWARD_ASSET);
        if (!string.IsNullOrEmpty(filetext)) { reward_data = USER_DeserializeFromContent<RewardSerializeData>(filetext); }
        if (reward_data == null) { reward_data = Resources.Load<RewardScriptableObject>(REWARD_ASSET).reward; }
#if CODEDEBUG
        if (reward_data == null) { LogError(METHOD_NAME, "{0} asset is NULL", REWARD_ASSET); }
#endif

        QUERY_Init();
        QUERY_CheckOnlineStatus();
        if (IsOnline()) {
            FB_Init();
        } else {
#if CODEDEBUG
            Log(METHOD_NAME, "Loading Local State");
#endif
            //load last played state
            USER_LoadState(USER_FileReadAllText(config_data.us_id, true), config_data.us_id, false);

            //Joy button is disabled from start, so enable it
            UI_BEGIN_SetInputEnabled(true);
        }
    }
    void USER_LoadState(string text, string id, bool checkTimestamp)
    {
#if CODEDEBUG
        string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
#endif

        UserState state = null;
        if (!string.IsNullOrEmpty(text)) {
            state = USER_DeserializeFromContent<UserState>(text);
        }
        if (state == null) {
#if CODEDEBUG
            LogWarning(METHOD_NAME, "state is NULL. Creating New");
#endif
            state = USER_NewState();
        }

        if (checkTimestamp && id == config_data.us_id && state.timestamp < us.timestamp) {
#if CODEDEBUG
            LogError(METHOD_NAME, "current timestamp: {0}, trying to load: {1}", us.timestamp, state.timestamp);
#endif
            return;
        }

        config_data.us_id = id;
        us = state;
        if (us.levels.Length != pch_so.playchar_slots.Length) {
            System.Array.Resize(ref us.levels, pch_so.playchar_slots.Length);
        }
        if (us.drops.Length != pch_so.playchar_slots.Length) {
            System.Array.Resize(ref us.drops, pch_so.playchar_slots.Length);
        }
        for (int i = 0, l = us.levels.Length; i < l; ++i) {
            if (us.levels[i] == null) {
                us.levels[i] = new PlaycharLevel();
            }
        }
        if (us.avail_playchars.Length != pch_so.playchars.Length) {
            System.Array.Resize(ref us.avail_playchars, pch_so.playchars.Length);
        }
        if (us.avail_themeslots.Length != thm_so.theme_slots.Length) {
            System.Array.Resize(ref us.avail_themeslots, thm_so.theme_slots.Length);
        }

        //selection check
        if (us.playchar_slot_index >= pch_so.playchar_slots.Length) {
            us.playchar_slot_index = 0;
            us.playchar_index_in_slot = 0;
        }
        if (us.playchar_index_in_slot >= pch_so.playchar_slots[us.playchar_slot_index].playchars.Length) {
            us.playchar_index_in_slot = 0;
        }
        if (us.theme_slot_index >= thm_so.theme_slots.Length) {
            us.theme_slot_index = 0;
        }

        //restore selection
        selected_playchar_slot_index = us.playchar_slot_index;
        selected_playchar_index_in_slot = us.playchar_index_in_slot;
        selected_theme_slot_index = us.theme_slot_index;

        //highscore
        _UI_FB_STAT_UpdateUserDisplayData();

        USER_OnUserStateChanged(false);

        CHALLENGE_Init();

        USER_SaveConfig();
#if CODEDEBUG
        Log(METHOD_NAME, "state loaded for {0}", config_data.us_id);
#endif
    }
    void USER_SaveState(bool doPush)
    {
        if (!us_needs_save) return;

        //timestamp
        us.timestamp = System.DateTime.UtcNow;
        //selection
        us.playchar_slot_index = selected_playchar_slot_index;
        us.playchar_index_in_slot = selected_playchar_index_in_slot;
        us.theme_slot_index = selected_theme_slot_index;
        //challenges
        us.chprog_state = (chprog_active != null) ? chprog_active.SaveState() : null;
        us.chday_state = (chday_active != null) ? chday_active.SaveState() : null;
        us.chspec_state = (chspec_active != null) ? chspec_active.SaveState() : null;
        //serialize
        string state_str = SerializationHelpers.SerializeToContent<UserState, FullSerializerSerializer>(us);

        if (doPush && IsOnline() && FB.IsLoggedIn) {
            QUERY_DoPush(state_str);
        }

        USER_FileWriteAllText(config_data.us_id, state_str, true);
        us_needs_save = false;

#if CODEDEBUG
        string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
        Log(METHOD_NAME, "state saved for {0}", config_data.us_id);
#endif
    }
    void USER_SaveConfig()
    {
        USER_FileWriteAllText(CONFIG_ASSET, SerializationHelpers.SerializeToContent<ConfigSerializeData, FullSerializerSerializer>(config_data), false);
    }
    string USER_FileReadAllText(string filename, bool encrypted = false)
    {
#if CODEDEBUG
        string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
#endif
        try {
            string path = Path.Combine(Application.persistentDataPath, filename);
            if (File.Exists(path)) {
                string text = File.ReadAllText(path);
                if (encrypted) { text = StringCipher.Decrypt(text); }
                return text;
            }
#if CODEDEBUG
 else {
                LogWarning(METHOD_NAME, "file not found:\n{0}", path);
            }
#endif
        } catch
#if CODEDEBUG
            (System.Exception e)
#endif
        {
#if CODEDEBUG
            LogError(METHOD_NAME, "exception:\n{0}", e.Message);
#endif
        }
        return null;
    }
    void USER_FileWriteAllText(string filename, string text, bool encrypted)
    {
#if CODEDEBUG
        string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
#endif
        try {
            if (encrypted) { text = StringCipher.Encrypt(text); }
            File.WriteAllText(Path.Combine(Application.persistentDataPath, filename), text);
        } catch
#if CODEDEBUG
 (System.Exception e)
#endif
 {
#if CODEDEBUG
            LogError(METHOD_NAME, "exception:\n{0}", e.Message);
#endif
        }
    }
    T USER_DeserializeFromContent<T>(string content)
    {
        if (string.IsNullOrEmpty(content)) {
#if CODEDEBUG
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            LogError(METHOD_NAME, "content is NULL");
#endif
            return default(T);
        }

        var result = SerializationHelpers.DeserializeFromContent<T, FullSerializerSerializer>(content);
#if CODEDEBUG
        if (result == null) {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            LogError(METHOD_NAME, "cannot deserialize content");
        }
#endif
        return result;
    }
    void USER_DeleteFile(string filename)
    {
#if CODEDEBUG
        string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
#endif
        try {
            string path = Path.Combine(Application.persistentDataPath, filename);
            if (File.Exists(path)) {
                File.Delete(path);
            }
#if CODEDEBUG
 else {
                LogWarning(METHOD_NAME, "file not found:\n{0}", path);
            }
#endif
        } catch
#if CODEDEBUG
            (System.Exception e)
#endif
        {
#if CODEDEBUG
            LogError(METHOD_NAME, "exception:\n{0}", e.Message);
#endif
        }
    }
    void USER_LoadRewards(string text)
    {
        List<UserReward> rewards = USER_DeserializeFromContent<List<UserReward>>(text);
        if (rewards == null) return;

        user_rewards_pending.AddRange(rewards);
        USER_CheckRewardsAdded(null);
    }
    UserState USER_NewState()
    {
        UserState state = new UserState() {
            name = string.Empty,
            luck = reward_data.new_luck_value,
            coins = reward_data.new_coins_value,
            luck_x = 0,
            coins_x = 0,
            score_x = 0,
            levels = new PlaycharLevel[pch_so.playchar_slots.Length],
            drops = new int[pch_so.playchar_slots.Length],
            avail_playchars = new int[pch_so.playchars.Length],
            avail_themeslots = new int[thm_so.theme_slots.Length],
            playchar_slot_index = 0,
            playchar_index_in_slot = 0,
            theme_slot_index = 0
        };
        state.avail_playchars[0] = 1;
        state.avail_themeslots[0] = 1;

        return state;
    }
    ConfigSerializeData USER_NewConfigData()
    {
        var config_data = new ConfigSerializeData() {
            us_id = OFFLINE_ID,
            show_tut = true
        };
        return config_data;
    }
    void USER_Signup_RewardsComplete()
    {
        //save local fbid
        us_needs_save = true;
        USER_SaveState(false);
        //save config
        USER_SaveConfig();
        //delete offline
        USER_DeleteFile(OFFLINE_ID);
    }
    void USER_OnUserStateChanged(bool stateNeedSave = true)
    {
        us_needs_save = stateNeedSave;
        if (onUserStateChanged != null) { onUserStateChanged(); }
    }
    void USER_CheckRewardsAdded(Event onComplete)
    {
        if (!user_rewards_showing && user_rewards_pending.Count > 0 && game_state == GameState.BEGIN) {
            user_rewards_showing = true;
            game_state_routine.Overlap(Routine.Waiter(USER_ShowRewards(true, onComplete)));
        }
    }
    IEnumerator USER_ShowRewards(bool restoreState, Event onComplete)
    {
#if DBG_TRACE_ROUTINES
        Log("Routine", "Enter: {0}", "USER_ShowRewards");
#endif
        UI_STATE_SwitchTo(UiState.HIDDEN);
        AUD_PlayMusic(MusicType.UI_REWARD);

        //show rewards
        while (user_rewards_pending.Count > 0) {
            IEnumerator current = UI_RewardShowItem(user_rewards_pending[0]);
            while (current.MoveNext()) { yield return current.Current; }
            user_rewards_pending.RemoveAt(0);
        }

        if (restoreState) {
            UI_STATE_SwitchTo(ui_last_state, ui_target_page);
            //AUD_PlayMusic(last_music_state);
        }

        user_rewards_showing = false;
        if (onComplete != null) { onComplete(); }

#if DBG_TRACE_ROUTINES
        Log("Routine", "Exit: {0}", "USER_ShowRewards");
#endif
    }
    void USER_AddItem(UserInvItemType type, int value)
    {
        switch (type) {
        case UserInvItemType.LUCK:
            if ((us.luck += value) > 100) us.luck = 100;
            break;
        case UserInvItemType.COINS:
            us.coins += value;
            break;
        case UserInvItemType.DROPS:
            us.drops[selected_playchar_slot_index] += value;
            break;
        case UserInvItemType.COINS_X:
            us.coins_x += value;
            break;
        case UserInvItemType.SCORE_X:
            us.score_x += value;
            break;
        case UserInvItemType.LUCK_X:
            us.luck_x += value;
            break;
        case UserInvItemType.MBOX:
            user_rewards_pending.Add(new UserReward() { type = UserInvItemType.MBOX, amount = 1 });
            USER_CheckRewardsAdded(null);
            break;
        }

        USER_OnUserStateChanged();
    }
    void USER_AddLevel(PlaycharLevelType type)
    {
        us.levels[selected_playchar_slot_index].AddLevelFor(type);

        USER_OnUserStateChanged();
    }
    void USER_Playing_2_Rest()
    {
        //calc values
        pch_coins_b_collected += pch_coins_nb_collected;
        if (onCoinsBanked != null) { onCoinsBanked(pch_coins_nb_collected); }
        pch_coins_nb_collected = 0;
    }
    #endregion //[User]

    #region [Challenge]
    const int NUM_AUTO_CHALLENGES = 0;
    Challenge chprog_active = null;
    Challenge chday_active = null;
    Challenge chspec_active = null;

    NoteInfo chs_note = null;

    void CHALLENGE_Init()
    {
        if (chprog_active != null) { chprog_active.Destroy(); chprog_active = null; }
        if (chday_active != null) { chday_active.Destroy(); chday_active = null; }
        if (chspec_active != null) { chspec_active.Destroy(); chspec_active = null; }

        if (chs_note == null) {
            chs_note = new NoteInfo() {
                icon = ui_so.chs_green_icon,
                sound_type = UiSoundType.NOTEWIN
            };
        }

        //Progress challenges
        //check if state version differs from challenges version
        if (us.chprog_id != chprog_data.id) {
            us.chprog_id = chprog_data.id;
            CHALLENGE_InitNextProgress();
        } else {
            if (us.chprog_prog_index < chprog_data.ch.Length) {
                //Load Progress
                chprog_active = chprog_data.ch[us.chprog_prog_index];
                chprog_active.Enable(
#if CODEDEBUG
string.Format("chprog_data[{0}]", us.chprog_prog_index)
#endif
);
                if (us.chprog_state != null) { chprog_active.LoadState(us.chprog_state); }
                chprog_active.SetOnGreen(CHALLENGE_CheckDone);
            } else {
                CHALLENGE_InitNextProgress();
            }
        }

        var now = System.DateTime.UtcNow;
        //Daily challenges
        if (us.chday_expire > now) {
            //check if state version differs from challenges version
            if (us.chday_id != chday_data.id || us.chday_random_index >= chday_data.ch.Length || us.chday_reward_index >= reward_data.chday.items.Length) {
                us.chday_id = chday_data.id;
                CHALLENGE_InitNextDaily();
            } else {
                //Load Daily
                chday_active = chday_data.ch[us.chday_random_index];
                chday_active.Enable(
#if CODEDEBUG
string.Format("chday_data[{0}]", us.chday_random_index)
#endif
);
                if (us.chday_state != null) { chday_active.LoadState(us.chday_state); }
                chday_active.SetOnGreen(CHALLENGE_CheckDone);
            }
        } else {
            //get new daily
            CHALLENGE_InitNextDaily();
        }

        //Special Challenges
        if (us.chspec_expire > now) {
            //check if state version differs from challenges version
            if (us.chspec_id != chspec_data.id || us.chspec_random_index >= chspec_data.ch.Length || us.chspec_reward_index >= reward_data.chspec.items.Length) {
                us.chspec_id = chspec_data.id;
                CHALLENGE_InitNextSpecial();
            } else {
                //Load Spec
                chspec_active = chspec_data.ch[us.chspec_random_index];
                chspec_active.Enable(
#if CODEDEBUG
string.Format("chspec_data[{0}]", us.chspec_random_index)
#endif
);
                if (us.chspec_state != null) { chspec_active.LoadState(us.chspec_state); }
                chspec_active.SetOnGreen(CHALLENGE_CheckDone);
            }
        } else {
            //get new spec
            CHALLENGE_InitNextSpecial();
        }
    }
    void CHALLENGE_OnPlayingBegin()
    {
        //Progress challenges
        chprog_active.OnPlayingBegin();

        //Daily challenges
        CHALLENGE_CheckDailyExpired();
        chday_active.OnPlayingBegin();

        //Special challenges
        CHALLENGE_CheckSpecialExpired();
        chspec_active.OnPlayingBegin();
    }
    void CHALLENGE_OnPlayingEnd()
    {
        //Progress challenges
        chprog_active.OnPlayingEnd();

        //Daily challenges
        chday_active.OnPlayingEnd();

        //Special challenges
        chspec_active.OnPlayingEnd();
    }
    void CHALLENGE_CheckDone()
    {
        if (chprog_active.IsGreen()) {
            if (us.chprog_random_index < 0) {
                chs_note.text = string.Format(Localize(ui_so.chs_prog_note_text), us.chprog_prog_index + 1);
            } else {
                chs_note.text = Localize(ui_so.chs_rand_note_text);
            }
            Notify(chs_note);

            //add rewards
            user_rewards_pending.AddRange(USER_GetActiveChprogRewards());

            //destroy
            if (game_state == GameState.PLAYING) { chprog_active.OnPlayingEnd(); }
            chprog_active.Destroy();
            //move progress level
            if (us.chprog_prog_index < chprog_data.ch.Length) { ++us.chprog_prog_index; }
            //generate new progress
            CHALLENGE_InitNextProgress();

        } else if (chday_active.IsGreen()) {
            chs_note.text = Localize(ui_so.chs_day_note_text);
            Notify(chs_note);

            //add rewards
            user_rewards_pending.AddRange(USER_GetActiveChdayRewards());

            //destroy
            if (game_state == GameState.PLAYING) { chday_active.OnPlayingEnd(); }
            //chday_active.Destroy();

        } else if (chspec_active.IsGreen()) {
            chs_note.text = Localize(ui_so.chs_spec_note_text);
            Notify(chs_note);

            //add rewards
            user_rewards_pending.AddRange(USER_GetActiveChspecRewards());

            //destroy
            if (game_state == GameState.PLAYING) { chspec_active.OnPlayingEnd(); }
        }
        USER_CheckRewardsAdded(null);
    }
    void CHALLENGE_InitNextProgress()
    {
        us.chprog_state = null;
        if (us.chprog_prog_index < chprog_data.ch.Length) {
            //add from progress list
            us.chprog_random_index = -1;
            us.chprog_reward_index = -1;
            chprog_active = chprog_data.ch[us.chprog_prog_index];
            chprog_active.Enable(
#if CODEDEBUG
string.Format("init next chprog_data[{0}]", us.chprog_prog_index)
#endif
);
        } else {
            //add from day list
            us.chprog_random_index = CHALLENGE_RandomDailyIndex();
            us.chprog_reward_index = CHALLENGE_RandomDailyReward();
            chprog_active = chday_data.ch[us.chprog_random_index];
            chprog_active.Enable(
#if CODEDEBUG
string.Format("chday_data[{0}]", us.chprog_random_index)
#endif
);
        }
        chprog_active.SetOnGreen(CHALLENGE_CheckDone);
        if (game_state == GameState.PLAYING) {
            chprog_active.OnPlayingBegin();
        }
    }
    void CHALLENGE_InitNextDaily()
    {
        us.chday_state = null;
        us.chday_expire = System.DateTime.UtcNow.AddHours(UserState.CHDAY_EXPIRE_HOURS);
        us.chday_random_index = CHALLENGE_RandomDailyIndex();
        us.chday_reward_index = CHALLENGE_RandomDailyReward();
        chday_active = chday_data.ch[us.chday_random_index];
        chday_active.Enable(
#if CODEDEBUG
            string.Format("chday_data[{0}]", us.chday_random_index)
#endif
);
        chday_active.SetOnGreen(CHALLENGE_CheckDone);
    }
    void CHALLENGE_InitNextSpecial()
    {
        us.chspec_state = null;
        us.chspec_expire = System.DateTime.UtcNow.AddHours(UserState.CHSPEC_EXPIRE_HOURS);
        us.chspec_random_index = CHALLENGE_RandomSpecialIndex();
        us.chspec_reward_index = CHALLENGE_RandomSpecialReward();
        chspec_active = chspec_data.ch[us.chspec_random_index];
        chspec_active.Enable(
#if CODEDEBUG
string.Format("chspec_data[{0}]", us.chspec_random_index)
#endif
);
        chspec_active.SetOnGreen(CHALLENGE_CheckDone);
    }
    int CHALLENGE_RandomDailyIndex()
    {
        int index = Random.Range(0, chday_data.ch.Length);
        if (index == us.chprog_random_index) {
            if (++index >= chday_data.ch.Length) index = 0;
        }
        if (index == us.chday_random_index) {
            if (++index >= chday_data.ch.Length) index = 0;
        }
        return index;
    }
    int CHALLENGE_RandomSpecialIndex()
    {
        int index = Random.Range(0, chspec_data.ch.Length);
        if (index == us.chspec_random_index) {
            if (++index >= chspec_data.ch.Length) index = 0;
        }
        return index;
    }
    int CHALLENGE_RandomDailyReward()
    {
        int index = reward_data.chday.RandomIndex();
        if (index == us.chday_reward_index) {
            if (++index >= reward_data.chday.items.Length) index = 0;
        }
        return index;
    }
    int CHALLENGE_RandomSpecialReward()
    {
        int index = reward_data.chspec.RandomIndex();
        if (index == us.chspec_reward_index) {
            if (++index >= reward_data.chspec.items.Length) index = 0;
        }
        return index;
    }
    void CHALLENGE_CheckDailyExpired()
    {
        if (us.chday_expire < System.DateTime.UtcNow) {
            CHALLENGE_InitNextDaily();
        }
    }
    void CHALLENGE_CheckSpecialExpired()
    {
        if (us.chspec_expire < System.DateTime.UtcNow) {
            CHALLENGE_InitNextSpecial();
        }
    }
    #endregion //[Challenge]

    #region [UI]
    abstract class UI_BuyElement
    {
        static UI_BuyElement expanded_item = null;

        GameObject item_root_go = null;
        Image item_root_bg_img = null;
        public Image item_icon = null;
        GameObject expand_icon_go = null;
        GameObject expand_content_go = null;
        GameObject price_go = null;
        GameObject price_coins_go = null;
        GameObject price_luck_go = null;
        protected Transform youhave_tr = null;
        Text item_name_txt = null;
        public LocalizeText item_name_loc = null;
        public LocalizeText item_desc_loc = null;
        Text price_coins_value_txt = null;
        Text price_luck_value_txt = null;
        GameObject buybtn_go = null;
        public Button buybtn = null;
        public int shopdata_item_index = 0;

        protected UI_BuyElement(GameObject item_root)
        {
            item_root_go = item_root;
            Transform item_root_tr = item_root_go.transform;

            item_root_bg_img = item_root_go.GetComponent<Image>();
            item_root_go.GetComponent<Button>().onClick.AddListener(OnExpandButton);

            //content
            Transform content_tr = item_root_tr.Find("Content");
            item_icon = content_tr.Find("Icon").GetComponent<Image>();
            expand_icon_go = content_tr.Find("ExpandIcon").gameObject;
            price_go = content_tr.Find("Price").gameObject;
            price_coins_go = price_go.transform.Find("Coins").gameObject;
            price_luck_go = price_go.transform.Find("Luck").gameObject;
            price_coins_value_txt = price_coins_go.transform.Find("Value").GetComponent<Text>();
            price_luck_value_txt = price_luck_go.transform.Find("Value").GetComponent<Text>();
            //NameGroup
            {
                Transform namegroup_tr = content_tr.Find("NameGroup");
                youhave_tr = namegroup_tr.Find("YouHave");
                Transform name_tr = namegroup_tr.Find("Name");
                if (name_tr != null) {
                    item_name_loc = name_tr.GetComponent<LocalizeText>();
                    item_name_txt = name_tr.GetComponent<Text>();
                }
            }
            //expand content
            expand_content_go = item_root_tr.Find("ExpandContent").gameObject;
            buybtn_go = expand_content_go.transform.Find("BuyBtn").gameObject;
            buybtn = buybtn_go.GetComponent<Button>();
            item_desc_loc = expand_content_go.transform.Find("Desc").GetComponent<LocalizeText>();
        }
        public void Collapse()
        {
            expand_content_go.SetActive(false);
            expand_icon_go.SetActive(true);
            item_root_bg_img.sprite = GameController.Instance.ui_so.shop_buyitemlevel_element_collapsed_img;
        }
        public void Expand()
        {
            expand_content_go.SetActive(true);
            expand_icon_go.SetActive(false);
            item_root_bg_img.sprite = GameController.Instance.ui_so.shop_buyitemlevel_element_expanded_img;
        }
        public void SetBuyEnabled(bool enabled)
        {
            price_go.SetActive(enabled);
            buybtn_go.SetActive(enabled);
        }
        public void SetYouhaveEnabled(bool enabled)
        {
            youhave_tr.gameObject.SetActive(enabled);
            item_name_txt.horizontalOverflow = enabled ? HorizontalWrapMode.Overflow : HorizontalWrapMode.Wrap;
            item_name_txt.alignment = enabled ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
        }
        public void SetPrice(PriceData price)
        {
            switch (price.type) {
            case CurrencyType.COINS:
                price_luck_go.SetActive(false);
                price_coins_go.SetActive(true);
                price_coins_value_txt.text = price.value.ToString();
                break;
            case CurrencyType.LUCK:
                price_coins_go.SetActive(false);
                price_luck_go.SetActive(true);
                price_luck_value_txt.text = price.value.ToString();
                break;
            }
        }
        public void SetActive(bool active)
        {
            item_root_go.SetActive(active);
        }
        void OnExpandButton()
        {
            if (expand_content_go.activeSelf) {
                Collapse();
                //AUDIO
                GameController.Instance.AUD_UI_Sound(UiSoundType.BACK);
            } else {
                if (expanded_item != null) { expanded_item.Collapse(); }
                expanded_item = this;

                Expand();
                //AUDIO
                GameController.Instance.AUD_UI_Sound(UiSoundType.BUTTON);
            }
        }
    }
    class UI_BuyItemElement : UI_BuyElement
    {
        public Text youhave_value_txt = null;

        public UI_BuyItemElement(GameObject item_root, Transform parent, Transform groupNameSibling, int siblingIndexInGroup)
            : base(item_root)
        {
            youhave_value_txt = youhave_tr.Find("Value").GetComponent<Text>();

            item_root.transform.SetParent(parent, false);
            item_root.transform.SetSiblingIndex(groupNameSibling.GetSiblingIndex() + siblingIndexInGroup + 1);
        }
    }
    class UI_BuyLevelElement : UI_BuyElement
    {
        public UI_BuyLevelElement(GameObject item_root, Transform parent, Transform groupNameSibling, int siblingIndexInGroup)
            : base(item_root)
        {
            item_root.transform.SetParent(parent, false);
            item_root.transform.SetSiblingIndex(groupNameSibling.GetSiblingIndex() + siblingIndexInGroup + 1);
        }
        public void SetNumLevels(int value)
        {
#if CODEDEBUG
            if (value < 1) {
                return;
            }
            if (youhave_tr.childCount < 1) {
                return;
            }
#endif
            int diff = value - youhave_tr.childCount;
            int diffabs = System.Math.Abs(diff);
            if (diffabs > 0) {
                GameObject youhave_value_go = youhave_tr.GetChild(0).gameObject;
                if (diff > 0) {
                    //add child elements
                    for (int i = 0; i < diffabs; ++i) {
                        Instantiate(youhave_value_go).transform.SetParent(youhave_tr, false);
                    }
                } else {
                    //remove child elements
                    for (int i = 0; i < diffabs; ++i) {
                        GameObject.Destroy(youhave_tr.GetChild(0).gameObject);
                    }
                }
            }
        }
        public void FillLevels(int num_filled)
        {
            GameController gc = GameController.Instance;

            if (num_filled > youhave_tr.childCount) {
                num_filled = youhave_tr.childCount;
            }

            int i = 0;
            for (; i < num_filled; ++i) {
                youhave_tr.GetChild(i).GetComponent<Image>().sprite = gc.ui_so.shop_level_icon_full;
            }
            for (int l = youhave_tr.childCount; i < l; ++i) {
                youhave_tr.GetChild(i).GetComponent<Image>().sprite = gc.ui_so.shop_level_icon_empty;
            }
        }
    }
    class UI_BuyChallengeElement : UI_BuyElement
    {
        public Text ch_index_txt = null;

        public UI_BuyChallengeElement(GameObject item_root, Transform parent, Transform groupNameSibling, int siblingIndexInGroup)
            : base(item_root)
        {
            ch_index_txt = item_root.transform.Find("Content/Icon/Value").GetComponent<Text>();

            item_root.transform.SetParent(parent, false);
            item_root.transform.SetSiblingIndex(groupNameSibling.GetSiblingIndex() + siblingIndexInGroup + 1);
        }
    }
    class UI_BuyDailyVideoElement : UI_BuyElement
    {
        public UI_BuyDailyVideoElement(GameObject item_root, Transform parent, Transform groupNameSibling, int siblingIndexInGroup)
            : base(item_root)
        {
            item_root.transform.SetParent(parent, false);
            item_root.transform.SetSiblingIndex(groupNameSibling.GetSiblingIndex() + siblingIndexInGroup + 1);
        }
    }
    class UI_FbFriendElement
    {
        GameObject item_root_go = null;
        //data
        Text name_txt = null;
        Text score_value_txt = null;
        //index icon
        Text index_txt = null;
        //picture
        Material picture_material = null;

        public UI_FbFriendElement(GameObject root_go, Transform parent, int placeIndex)
        {
            item_root_go = root_go;
            Transform item_root_tr = item_root_go.transform;

            //picture
            Transform item_picture_tr = item_root_tr.Find("PicHolder");
            GameObject item_picture = item_picture_tr.Find("Pic").gameObject;
            picture_material = item_picture.GetComponent<Renderer>().material;
            picture_material.mainTexture = GameController.Instance.ui_so.fb_friend_default_texture;
            //index icon
            index_txt = item_root_tr.Find("Index").GetComponent<Text>();
            index_txt.text = placeIndex.ToString();

            //data
            Transform item_data = item_root_tr.Find("Data");
            name_txt = item_data.Find("Name").GetComponent<Text>();
            name_txt.text = string.Empty;
            score_value_txt = item_data.Find("Score").GetComponent<Text>();
            score_value_txt.text = string.Empty;

            //position
            item_root_tr.SetParent(parent, false);
        }
        public void SetInfo(UI_FbFriendInfo info, int index)
        {
            SetInfo(info.name, info.score, info.pic, index);
        }
        public void SetInfo(string name, int score, Texture2D pic, int index)
        {
            name_txt.text = name;
            score_value_txt.text = score.ToString();
            picture_material.mainTexture = pic != null ? pic : GameController.Instance.ui_so.fb_friend_default_texture;
            index_txt.text = index.ToString();
        }
        public void SetName(string name)
        {
            name_txt.text = name;
        }
        public void SetScore(int score)
        {
            score_value_txt.text = score.ToString();
        }
        public void SetPicture(Texture2D pic)
        {
            picture_material.mainTexture = pic != null ? pic : GameController.Instance.ui_so.fb_friend_default_texture;
        }
        public void SetIndex(int index)
        {
            index_txt.text = index.ToString();
        }
    }
    class UI_FbFriendInfo
    {
        public string id = string.Empty;
        public string name = string.Empty;
        public int score = 0;
        public Texture2D pic = null;
    }

    const int UI_PLAYCHAR_ANIM_GREETING = 0;
    const int UI_PLAYCHAR_ANIM_HIGHSCORE = 1;
    const int UI_PLAYCHAR_ANIM_STAT = 2;

    const string UI_ROOT_NAME = "UiRoot";

    public UIScriptableObject ui_so = null;

    enum UiState { HIDDEN, BEGIN, SHOP, CHS, USER, PLAYING, END, FB }
    UiState ui_state = UiState.HIDDEN;
    UiState ui_last_state = UiState.HIDDEN;
    object ui_target_page = null;
    enum UiShowHideAnimState { NONE, SHOW, HIDE }

    GameTween<float> ui_value_tween = null;
    GameTween<float> ui_value_rev_tween = null;
    GameTimer ui_timer = null;
    Routine.TimeWait ui_time_waiter = null;
    //Used for display results
    int ui_score_value = 0;
    int ui_nbcoins_value = 0;
    int ui_coins_value = 0;
    int ui_luck_value = 0;

#if CODEDEBUG
    //Player DevUI
    GameObject ui_playchar_devgui_go = null;
    GameObject ui_playchar_devgui_controls_go = null;
    Text ui_playchar_devgui_luck_txt = null;

    //Console UI
    GameObject ui_console_root_go = null;
    ScrollRect ui_console_scroll = null;
    GameObject ui_console_body_go = null;
    RectTransform ui_console_body_rtr = null;
    Transform ui_console_content_tr = null;
    bool ui_console_expanded = false;

    GameTween<float> ui_console_scroll_tween = null;
    GameTimer ui_console_timer = null;
#endif

    //Note UI
    public class NoteInfo
    {
        public string text = string.Empty;
        public Sprite icon = null;
        public GameObject custom_ui = null;
        public UiSoundType sound_type = UiSoundType.NOTE;
        public GameController.Event onComplete = null;
    }
    Transform ui_note_root_tr = null;
    GameObject ui_note_msg_go = null;
    Text ui_note_msg_txt = null;
    GameObject ui_note_msg_icon_go = null;
    Image ui_note_msg_icon_img = null;
    List<NoteInfo> ui_note_msg_list = null;
    GameTimer ui_note_timer = null;
    GameObject ui_note_active_go = null;
    NoteInfo ui_note_active_info = null;

    //Begin UI
    enum UiBeginPage { HIDDEN, MAIN }
    UiBeginPage ui_begin_page = UiBeginPage.HIDDEN;
    UiBeginPage ui_begin_last_page = UiBeginPage.HIDDEN;
    GameObject ui_begin_root_go = null;
    GameObject ui_begin_joybtn_go = null;
    //GameObject ui_begin_joybtn_text_go = null; //Tutorial is disabled

    //Playing UI
    enum UiPlayingPage
    {
        HIDDEN,
        MAIN,
        CUTSCENE, //INTRO, CONTINUE. Displays ScreenButton
        CRASHCUT, //Displays ContinueButton, ScreenButton
        RESTCUT, //Displays CoinCount, ScreenButton
        PAUSE
    }
    UiPlayingPage ui_playing_page = UiPlayingPage.HIDDEN;
    UiPlayingPage ui_playing_last_page = UiPlayingPage.HIDDEN;
    GameObject ui_playing_root_go = null;
    //
    //Playing UI Main Page
    GameObject ui_playing_main_root_go = null;
    GameObject ui_playing_main_pausebtn_go = null;
    GameObject ui_playing_main_restbtn_go = null;
    Text ui_playing_main_coins_value_txt = null;
    Text ui_playing_main_score_value_txt = null;
    GameObject ui_playing_main_scorex_mult_go = null;
    Text ui_playing_main_scorex_mult_value_txt = null;
    GameObject ui_playing_main_coinsx_mult_go = null;
    Text ui_playing_main_coinsx_mult_value_txt = null;
    GameObject ui_playing_main_stamina_icon_go = null;
    RectTransform ui_playing_main_stamina_rect = null;
    GameTween<float> ui_playing_main_stamina_tween = null;
    GameObject ui_playing_main_drops_go = null;
    Text ui_playing_main_drops_value_txt = null;
    Image ui_playing_main_drops_img = null;
    Transform ui_playing_main_staminacrash_tr = null;
    RectTransform ui_playing_main_staminacrash_rect = null;
    float ui_playing_main_staminacrash_rect_width = 0f;
    GameTween<float> ui_playing_main_staminacrash_tween = null;
    //GameObject ui_playing_main_dropbtn_go = null;
    //Items
    float ui_playing_main_stamina_rect_step_width = 0f;
    GameObject ui_playing_main_items_magnet_go = null;
    RectTransform ui_playing_main_items_magnet_rect = null;
    GameTween<float> ui_playing_main_items_magnet_tween = null;
    GameObject ui_playing_main_items_scorex_go = null;
    RectTransform ui_playing_main_items_scorex_rect = null;
    GameTween<float> ui_playing_main_items_scorex_tween = null;
    GameObject ui_playing_main_items_coinsx_go = null;
    RectTransform ui_playing_main_items_coinsx_rect = null;
    GameTween<float> ui_playing_main_items_coinsx_tween = null;
    GameObject ui_playing_main_items_luckx_go = null;
    RectTransform ui_playing_main_items_luckx_rect = null;
    GameTween<float> ui_playing_main_items_luckx_tween = null;
    //Start Items
    GameObject ui_playing_main_startitems_root_go = null;
    GameObject ui_playing_main_startitems_scorexholder_go = null;
    GameObject ui_playing_main_startitems_scorex_go = null;
    GameObject ui_playing_main_startitems_coinsxholder_go = null;
    GameObject ui_playing_main_startitems_coinsx_go = null;
    GameObject ui_playing_main_startitems_luckxholder_go = null;
    GameObject ui_playing_main_startitems_luckx_go = null;

    //
    //Playing UI Pause Page
    GameObject ui_playing_pause_root_go = null;
    Text ui_playing_pause_top_luck_value_txt = null;
    Text ui_playing_pause_top_coins_value_txt = null;
    //
    //Playing UI Cutscene Page
    GameObject ui_playing_cutscene_root_go = null;
    //contains GameObject ui_playing_cutdefault_screenbtn_go = null;
    //
    //Playing UI Crash Cutscene Page
    GameObject ui_playing_crash_root_go = null;
    GameObject ui_playing_crash_continuebtn_go = null;
    GameObject ui_playing_crash_watchbtn_go = null;
    Button ui_playing_crash_screen_btn = null;
    //
    //Playing UI Rest Cutscene Page
    GameObject ui_playing_rest_root_go = null;
    Button ui_playing_rest_screenbtn = null;
    GameObject ui_playing_rest_nbcoins_go = null;
    Text ui_playing_rest_nbcoins_value_txt = null;
    GameObject ui_playing_rest_coins_go = null;
    Text ui_playing_rest_coins_value_txt = null;
    Transform ui_playing_rest_stamina_tr = null;
    RectTransform ui_playing_rest_stamina_rect = null;
    float ui_playing_rest_stamina_rect_width = 0f;
    GameTween<float> ui_playing_rest_stamina_tween = null;


    //Shop UI
    enum UiShopPage { HIDDEN, CHAR, THEME, BUY }
    enum UiShopConfirmState { ACTIVE, SELECT, BUY, NOT_AVAILABLE }
    UiShopPage ui_shop_page = UiShopPage.HIDDEN;
    UiShopPage ui_shop_last_page = UiShopPage.HIDDEN;
    UI_BuyItemElement[] ui_buy_items;
    UI_BuyLevelElement[] ui_buy_levels;
    UI_BuyChallengeElement[] ui_buy_chs;
    UI_BuyDailyVideoElement ui_buy_vid;
    GameObject ui_shop_root_go = null;
    GameTween<float> ui_shop_scroll_tween = null;
    //Shop UI Top Panel
    Text ui_shop_top_luck_value_txt = null;
    Text ui_shop_top_coins_value_txt = null;
    int ui_shop_top_coins_value = -1;
    int ui_shop_top_luck_value = -1;
    //
    //
    //Shop UI Char Page
    GameObject ui_shop_char_root_go = null;
    LocalizeText ui_shop_char_name_loc = null;
    LocalizeText ui_shop_char_desc_loc = null;
    int ui_shop_char_slotindex = 0;
    int ui_shop_char_indexinslot = 0;
    Transform ui_shop_char_node = null;
    GameObject ui_shop_char_question_go = null;
    GameObject ui_shop_char_node_active_go = null;
    //  Confirm
    UiShopConfirmState ui_shop_char_confirm_state = UiShopConfirmState.BUY;
    UiShopConfirmState ui_shop_char_confirm_last_state = UiShopConfirmState.BUY;
    GameObject ui_shop_char_confirm_active_go = null;
    GameObject ui_shop_char_confirm_select_go = null;
    GameObject ui_shop_char_confirm_buy_go = null;
    GameObject ui_shop_char_confirm_na_go = null;
    GameObject ui_shop_char_confirm_price_go = null;
    GameObject ui_shop_char_confirm_price_coins_go = null;
    Text ui_shop_char_confirm_price_coins_value_txt = null;
    GameObject ui_shop_char_confirm_price_luck_go = null;
    Text ui_shop_char_confirm_price_luck_value_txt = null;
    //  Scroll
    ScrollRect ui_shop_char_scroll = null;
    Transform ui_shop_char_scroll_content_tr = null;
    Transform ui_shop_char_scroll_selitem_icon_tr = null;
    float[] ui_shop_char_scroll_points = null;
    // Skin
    GameObject ui_shop_char_skin_go = null;
    int ui_shop_char_skin_activeindex = 0;
    //
    //
    //Shop UI Theme Page
    GameObject ui_shop_theme_root_go = null;
    LocalizeText ui_shop_theme_name_loc = null;
    LocalizeText ui_shop_theme_desc_loc = null;
    int ui_shop_themeslot_index = 0;
    Transform ui_shop_theme_node = null;
    GameObject ui_shop_theme_question_go = null;
    GameObject ui_shop_theme_node_active_go = null;
    // Confirm
    UiShopConfirmState ui_shop_theme_confirm_state = UiShopConfirmState.BUY;
    UiShopConfirmState ui_shop_theme_confirm_last_state = UiShopConfirmState.BUY;
    GameObject ui_shop_theme_confirm_active_go = null;
    GameObject ui_shop_theme_confirm_select_go = null;
    GameObject ui_shop_theme_confirm_buy_go = null;
    GameObject ui_shop_theme_confirm_na_go = null;
    GameObject ui_shop_theme_confirm_price_go = null;
    GameObject ui_shop_theme_confirm_price_coins_go = null;
    Text ui_shop_theme_confirm_price_coins_value_txt = null;
    GameObject ui_shop_theme_confirm_price_luck_go = null;
    Text ui_shop_theme_confirm_price_luck_value_txt = null;
    // Scroll
    ScrollRect ui_shop_theme_scroll = null;
    Transform ui_shop_theme_scroll_content_tr = null;
    Transform ui_shop_theme_scroll_selitem_icon_tr = null;
    float[] ui_shop_theme_scroll_points = null;
    //
    //
    //Shop UI BuyItem Page
    GameObject ui_shop_buy_root_go = null;
    Transform ui_shop_buy_content_tr = null;

    //Challenge UI
    enum UiChsPage { HIDDEN, PROG, DAILY, SPEC }
    UiChsPage ui_chs_page = UiChsPage.HIDDEN;
    UiChsPage ui_chs_last_page = UiChsPage.HIDDEN;
    GameObject ui_chs_root_go = null;
    LocalizeText ui_chs_top_loc = null;
    //MainPage
    GameObject ui_chs_main_root_go = null;
    //  Reward
    Image ui_chs_reward_icon = null;
    LocalizeText ui_chs_reward_name_loc = null;
    Text ui_chs_reward_value_txt = null;
    GameObject ui_chs_reward_bonus_go = null;
    Text ui_chs_reward_bonus_value_txt = null;
    //  Content
    Transform ui_chs_tasks_tr = null;

    //End UI
    enum UiEndPage { HIDDEN, MAIN }
    UiEndPage ui_end_page = UiEndPage.HIDDEN;
    UiEndPage ui_end_last_page = UiEndPage.HIDDEN;
    GameObject ui_end_root_go = null;
    GameObject ui_end_result_root_go = null;
    GameObject ui_end_result_score_go = null;
    Text ui_end_result_score_value_txt = null;
    GameObject ui_end_result_nbcoins_go = null;
    Text ui_end_result_nbcoins_value_txt = null;
    GameObject ui_end_result_coins_go = null;
    Text ui_end_result_coins_value_txt = null;
    GameObject ui_end_result_luck_go = null;
    Text ui_end_result_luck_sign_txt = null;
    Text ui_end_result_luck_value_txt = null;
    Transform ui_end_result_char_tr = null;
    GameObject ui_end_joystick_btn_go = null;

    //FB UI
    enum UiFbPage { HIDDEN, LOGIN, STAT, TOP, LOGOUT }
    UiFbPage ui_fb_page = UiFbPage.HIDDEN;
    UiFbPage ui_fb_last_page = UiFbPage.HIDDEN;
    GameObject ui_fb_root_go = null;
    Text ui_fb_toppanel_txt = null;
    LocalizeText ui_fb_toppanel_loc = null;
    //FB Login Page
    GameObject ui_fb_login_root_go = null;
    GameObject ui_fb_login_button_go = null;
    Text ui_fb_login_reward_coins_value_txt = null;
    Text ui_fb_login_reward_luck_value_txt = null;
    //FB Logout Page
    GameObject ui_fb_logout_root_go = null;
    GameObject ui_fb_logout_button_go = null;
    //FB Stat Page
    GameObject ui_fb_stat_root_go = null;
    Text ui_fb_stat_score_value_txt = null;
    Text ui_fb_stat_coins_value_txt = null;
    Text ui_fb_stat_luck_value_txt = null;
    Transform ui_fb_stat_char_node = null;
    GameObject ui_fb_stat_char_active_go = null;
    GameObject ui_fb_stat_user_icon_go = null;
    Texture2D ui_fb_stat_user_pic = null;
    string ui_fb_stat_user_first_name = string.Empty;
    //FB Top Page
    GameObject ui_fb_top_root_go = null;
    //Top Friends
    GameObject ui_fb_top_friends_go = null;
    GameObject ui_fb_top_friends_delimiter_go = null;
    Transform ui_fb_top_topfriends_tr = null;
    UI_FbFriendElement[] ui_fb_top_topfriend_elements = null;
    Transform ui_fb_top_bottomfriends_tr = null;
    UI_FbFriendElement[] ui_fb_top_bottomfriend_elements = null;
    UI_FbFriendInfo[] ui_fb_top_friend_infos = null;
    List<Pair<UI_FbFriendInfo, UI_FbFriendElement>> ui_fb_top_friends_to_query = null;
    int ui_fb_top_friends_pic_qindex = 0;
    //Top All
    Transform ui_fb_top_all_tr = null;
    UI_FbFriendElement[] ui_fb_top_all_elements = null;
    UI_FbFriendInfo[] ui_fb_top_all_infos = null;
    int ui_fb_top_all_name_qindex = 0;
    int ui_fb_top_all_pic_qindex = 0;
    //Top Weekly
    //Transform ui_fb_top_weekly_tr = null;
    //UI_FbFriendElement[] ui_fb_top_weekly_elements = null;
    //UI_FbFriendInfo[] ui_fb_top_weekly_infos = null;
    //int ui_fb_top_weekly_name_qindex = 0;
    //int ui_fb_top_weekly_pic_qindex = 0;

    //Reward UI
    GameObject ui_reward_root_go = null;
    GameObject ui_reward_mbox_root_go = null;
    GameObject ui_reward_mbox_go = null;
    GameObject ui_reward_smbox_go = null;
    GameObject ui_reward_item_root_go = null;
    Transform ui_reward_item_node_tr = null;
    GameObject ui_reward_desc_root_go = null;
    LocalizeText ui_reward_desc_name_loc = null;
    GameObject ui_reward_desc_desc_go = null;
    LocalizeText ui_reward_desc_desc_loc = null;
    Text ui_reward_desc_value_txt = null;
    Button ui_reward_scrbtn = null;
    ParticleSystem ui_reward_mbox_emitter = null;
    GameObject ui_reward_mbox_active_go = null;

    //Highscore
    GameObject ui_hs_root_go = null;
    GameObject ui_hs_score_go = null;
    Text ui_hs_score_value_txt = null;
    GameObject ui_hs_desc_go = null;
    Transform ui_hs_char_tr = null;
    GameObject ui_hs_sharebtn_go = null;

    //Menu
    enum UiMenuPage { HIDDEN, BEGIN, SHOP, CHS, USER, PLAYING_PAUSE, END, FB_LOGINOUT, FB_MAIN, HIGHSCORE }
    UiMenuPage ui_menu_page = UiMenuPage.HIDDEN;
    UiMenuPage ui_menu_last_page = UiMenuPage.HIDDEN;
    UiShowHideAnimState ui_menu_anim_state = UiShowHideAnimState.NONE;
    GameObject ui_menu_root_go = null;
    Transform[] ui_menu_btn_tr = null;
    GameObject ui_menu_back_go = null;
    GameObject ui_menu_restart_go = null;
    bool ui_menu_backbtn_coroutine_wait = true; //true is wait, false is continue
    //BEGIN Menu
    GameObject ui_menu_begin_fb_go = null;
    GameObject ui_menu_begin_shop_go = null;
    GameObject ui_menu_begin_chs_go = null;
    GameObject ui_menu_begin_quit_go = null;
    //
    //FB Menu
    GameObject ui_menu_fb_stat_go = null;
    GameObject ui_menu_fb_top_go = null;
    GameObject ui_menu_fb_logout_go = null;
    //
    //Shop Menu
    GameObject ui_menu_shop_char_go = null;
    GameObject ui_menu_shop_theme_go = null;
    GameObject ui_menu_shop_buyitem_go = null;
    //contains ui_menu_shop_back_go
    //
    //Chs Menu
    GameObject ui_menu_chs_prog_go = null;
    GameObject ui_menu_chs_day_go = null;
    GameObject ui_menu_chs_spec_go = null;
    //contains ui_menu_chs_back_go
    //
    //End Menu
    //contains ui_menu_chs_back_go
    //
    //Pause Menu
    //contains ui_menu_chs_restart_go
    //

    void UI_Init()
    {
        Transform ui_root = transform.Find(UI_ROOT_NAME);
        ui_root.gameObject.SetActive(true);

        ui_value_tween = new FloatTween();
        ui_value_tween.SetAutoUpdate(TweenAutoUpdateMode.APPLY);
        ui_value_rev_tween = new FloatTween();
        ui_value_rev_tween.SetAutoUpdate(TweenAutoUpdateMode.APPLY);
        ui_timer = new GameTimer();
        ui_timer.SetAutoUpdate(true);
        ui_time_waiter = new Routine.TimeWait();

#if CODEDEBUG
        //Console
        {
            ui_console_root_go = Instantiate(ui_so.console_prefab);
            ui_console_root_go.transform.SetParent(ui_root, false);
            Transform ui_console_body_tr = ui_console_root_go.transform.FindChild("Body");
            ui_console_body_go = ui_console_body_tr.gameObject;
            ui_console_body_rtr = ui_console_body_go.GetComponent<RectTransform>();
            ui_console_content_tr = ui_console_body_tr.FindChild("Content");
            ui_console_root_go.transform.FindChild("ShowBtn").GetComponent<Button>().onClick.AddListener(_UI_CONSOLE_OnButton);
            //Scroll
            ui_console_scroll = ui_console_body_go.GetComponent<ScrollRect>();
            var trigger = ui_console_body_go.AddComponent<EventTrigger>();
            //begin drag
            var entry = new EventTrigger.Entry();
            entry.eventID = EventTriggerType.BeginDrag;
            entry.callback.AddListener(_UI_CONSOLE_OnBeginDrag);
            if (trigger.triggers == null) { trigger.triggers = new List<EventTrigger.Entry>(1); }
            trigger.triggers.Add(entry);

            if (ui_console_timer == null) {
                ui_console_timer = new GameTimer();
                ui_console_timer.SetAutoUpdate(true);
                ui_console_timer.SetDuration(3f);
                ui_console_timer.SetOnComplete(_UI_CONSOLE_OnTimer);
            }
            if (ui_console_scroll_tween == null) {
                ui_console_scroll_tween = new FloatTween();
                ui_console_scroll_tween.SetBeginGetter(_UI_CONSOLE_ScrollPosGetter, false);
                ui_console_scroll_tween.SetSetter(_UI_CONSOLE_ScrollPosSetter);
                ui_console_scroll_tween.SetAutoUpdate(TweenAutoUpdateMode.APPLY);
                ui_console_scroll_tween.SetEase(Easing.QuadInOut);
                ui_console_scroll_tween.SetEndValue(0f);
            }

            //set state
            ui_console_expanded = true;
            _UI_CONSOLE_OnButton();
        }
#endif

        //Menu
        Transform ui_menu_root_tr = ui_root.Find("Menu");
        ui_menu_root_go = ui_menu_root_tr.gameObject;
        ui_menu_root_go.SetActive(false);
        ui_menu_btn_tr = new Transform[4];
        ui_menu_btn_tr[0] = ui_menu_root_tr.Find("Btn1");
        ui_menu_btn_tr[1] = ui_menu_root_tr.Find("Btn2");
        ui_menu_btn_tr[2] = ui_menu_root_tr.Find("Btn3");
        ui_menu_btn_tr[3] = ui_menu_root_tr.Find("Btn4");

        ui_menu_back_go = ui_menu_btn_tr[3].Find("Back").gameObject;
        ui_menu_back_go.SetActive(false);
        ui_menu_back_go.GetComponent<Button>().onClick.AddListener(_UI_OnBtnBack);
        ui_menu_restart_go = ui_menu_btn_tr[3].Find("Restart").gameObject;
        ui_menu_restart_go.SetActive(false);
        ui_menu_restart_go.GetComponent<Button>().onClick.AddListener(_UI_OnBtnRestart);
        //BeginMenu
        {
            //FB Button
            ui_menu_begin_fb_go = ui_menu_btn_tr[0].Find("Begin_Fb").gameObject;
            ui_menu_begin_fb_go.SetActive(false);
            ui_menu_begin_fb_go.GetComponent<Button>().onClick.AddListener(UI_FB_OnFbButon);
            //Shop button
            ui_menu_begin_shop_go = ui_menu_btn_tr[1].Find("Begin_Shop").gameObject;
            ui_menu_begin_shop_go.SetActive(false);
            ui_menu_begin_shop_go.GetComponent<Button>().onClick.AddListener(() => _UI_OnBtnStateTo(UiState.SHOP, UiShopPage.CHAR));
            //Chs button
            ui_menu_begin_chs_go = ui_menu_btn_tr[2].Find("Begin_Chs").gameObject;
            ui_menu_begin_chs_go.SetActive(false);
            ui_menu_begin_chs_go.GetComponent<Button>().onClick.AddListener(() => _UI_OnBtnStateTo(UiState.CHS, UiChsPage.PROG));
            //Quit button
            ui_menu_begin_quit_go = ui_menu_btn_tr[3].Find("Begin_Quit").gameObject;
            ui_menu_begin_quit_go.SetActive(false);
            ui_menu_begin_quit_go.GetComponent<Button>().onClick.AddListener(_UI_OnBtnQuit);
        }
        //Shop Menu
        {
            //Char button
            ui_menu_shop_char_go = ui_menu_btn_tr[0].Find("Shop_Char").gameObject;
            ui_menu_shop_char_go.SetActive(false);
            ui_menu_shop_char_go.GetComponent<Button>().onClick.AddListener(() => _UI_OnBtnStateTo(UiState.SHOP, UiShopPage.CHAR));
            //Theme button
            ui_menu_shop_theme_go = ui_menu_btn_tr[1].Find("Shop_Theme").gameObject;
            ui_menu_shop_theme_go.SetActive(false);
            ui_menu_shop_theme_go.GetComponent<Button>().onClick.AddListener(() => _UI_OnBtnStateTo(UiState.SHOP, UiShopPage.THEME));
            //BuyItem button
            ui_menu_shop_buyitem_go = ui_menu_btn_tr[2].Find("Shop_Buy").gameObject;
            ui_menu_shop_buyitem_go.SetActive(false);
            ui_menu_shop_buyitem_go.GetComponent<Button>().onClick.AddListener(() => _UI_OnBtnStateTo(UiState.SHOP, UiShopPage.BUY));
        }
        //Chs Menu
        {
            //Prog button
            ui_menu_chs_prog_go = ui_menu_btn_tr[0].Find("Chs_Prog").gameObject;
            ui_menu_chs_prog_go.SetActive(false);
            ui_menu_chs_prog_go.GetComponent<Button>().onClick.AddListener(() => _UI_OnBtnStateTo(UiState.CHS, UiChsPage.PROG));
            //Day button
            ui_menu_chs_day_go = ui_menu_btn_tr[1].Find("Chs_Day").gameObject;
            ui_menu_chs_day_go.SetActive(false);
            ui_menu_chs_day_go.GetComponent<Button>().onClick.AddListener(() => _UI_OnBtnStateTo(UiState.CHS, UiChsPage.DAILY));
            //Spec button
            ui_menu_chs_spec_go = ui_menu_btn_tr[2].Find("Chs_Spec").gameObject;
            ui_menu_chs_spec_go.SetActive(false);
            ui_menu_chs_spec_go.GetComponent<Button>().onClick.AddListener(() => _UI_OnBtnStateTo(UiState.CHS, UiChsPage.SPEC));
        }
        //End Menu
        {
        }
        //Pause Menu
        {
            //Challenges button
        }
        //FB_MAIN Menu
        {
            //Stat button
            ui_menu_fb_stat_go = ui_menu_btn_tr[0].Find("Fb_Stat").gameObject;
            ui_menu_fb_stat_go.SetActive(false);
            ui_menu_fb_stat_go.GetComponent<Button>().onClick.AddListener(() => _UI_OnBtnStateTo(UiState.FB, UiFbPage.STAT));
            //Top button
            ui_menu_fb_top_go = ui_menu_btn_tr[1].Find("Fb_Top").gameObject;
            ui_menu_fb_top_go.SetActive(false);
            ui_menu_fb_top_go.GetComponent<Button>().onClick.AddListener(UI_FB_OnTopButton);
            //Logout button
            ui_menu_fb_logout_go = ui_menu_btn_tr[2].Find("Fb_Logout").gameObject;
            ui_menu_fb_logout_go.SetActive(false);
            ui_menu_fb_logout_go.GetComponent<Button>().onClick.AddListener(() => _UI_OnBtnStateTo(UiState.FB, UiFbPage.LOGOUT));
        }

        //Note UI
        {
            ui_note_root_tr = ui_root.Find("Note");
            ui_note_root_tr.gameObject.SetActive(true);
            //animation
            ui_note_root_tr.GetComponent<Animation>().SampleAt(0);

            Transform ui_note_msg_tr = ui_note_root_tr.Find("Message");
            ui_note_msg_go = ui_note_msg_tr.gameObject;
            ui_note_msg_go.SetActive(false);
            ui_note_msg_txt = ui_note_msg_tr.Find("Text").GetComponent<Text>();
            ui_note_msg_icon_go = ui_note_msg_tr.Find("Icon").gameObject;
            ui_note_msg_icon_img = ui_note_msg_icon_go.GetComponent<Image>();

            if (ui_note_msg_list == null) { ui_note_msg_list = new List<NoteInfo>(3); }
            if (ui_note_timer == null) {
                ui_note_timer = new GameTimer();
                ui_note_timer.SetDuration(ui_note_root_tr.GetComponent<Animation>().clip.length * 2);
                ui_note_timer.SetAutoUpdate(true);
                ui_note_timer.SetOnComplete(_UI_NOTE_OnTimer);
            }
        }

        //Begin UI
        {
            Transform ui_begin_root_tr = ui_root.Find("Begin");
            ui_begin_root_go = ui_begin_root_tr.gameObject;
            ui_begin_root_go.SetActive(false);
            ui_begin_joybtn_go = ui_begin_root_tr.Find("JoystickBtn").gameObject;
            ui_begin_joybtn_go.GetComponent<Button>().onClick.AddListener(_UI_BEGIN_OnJoystickBtn);
            ui_begin_joybtn_go.SetActive(false);
            Transform ui_begin_langbtn_tr = ui_begin_root_tr.Find("Lang");
            ui_begin_langbtn_tr.GetComponent<Button>().onClick.AddListener(_UI_BEGIN_OnLangButton);

            //Tutorial is disabled
            /*ui_begin_joybtn_text_go = ui_begin_joybtn_go.transform.FindChild("Text").gameObject;
            ui_begin_joybtn_text_go.SetActive(false);*/
        }

        //Playing UI
        {
            Transform ui_playing_root_tr = ui_root.Find("Playing");
            ui_playing_root_go = ui_playing_root_tr.gameObject;
            ui_playing_root_go.SetActive(false);
            //Playing UI Main Page
            {
                Transform ui_playing_main_root_tr = ui_playing_root_tr.Find("Main");
                ui_playing_main_root_go = ui_playing_main_root_tr.gameObject;
                ui_playing_main_root_go.SetActive(false);
                //Pause button
                ui_playing_main_pausebtn_go = ui_playing_main_root_tr.Find("PauseBtn").gameObject;
                ui_playing_main_pausebtn_go.GetComponent<Button>().onClick.AddListener(_UI_PLAYING_MAIN_OnPauserBtn);
                //Rest button
                ui_playing_main_restbtn_go = ui_playing_main_root_tr.Find("RestBtn").gameObject;
                ui_playing_main_restbtn_go.SetActive(false);
                ui_playing_main_restbtn_go.GetComponent<Button>().onClick.AddListener(_UI_PLAYING_MAIN_OnRestBtn);
                //Coins value text
                Transform ui_playing_main_coins_tr = ui_playing_main_root_tr.Find("Coins");
                ui_playing_main_coins_value_txt = ui_playing_main_coins_tr.Find("Value").GetComponent<Text>();
                ui_playing_main_coinsx_mult_go = ui_playing_main_coins_tr.Find("Mult").gameObject;
                ui_playing_main_coinsx_mult_value_txt = ui_playing_main_coins_tr.Find("Mult/Value").GetComponent<Text>();
                //Score value text
                Transform ui_playing_main_score_tr = ui_playing_main_root_tr.Find("Score");
                ui_playing_main_score_value_txt = ui_playing_main_score_tr.Find("Value").GetComponent<Text>();
                ui_playing_main_scorex_mult_go = ui_playing_main_score_tr.Find("Mult").gameObject;
                ui_playing_main_scorex_mult_value_txt = ui_playing_main_score_tr.Find("Mult/Value").GetComponent<Text>();
                //stamina icon
                Transform ui_playing_main_stamina_tr = ui_playing_main_root_tr.Find("Stamina");
                ui_playing_main_stamina_icon_go = ui_playing_main_stamina_tr.Find("Icon").gameObject;
                ui_playing_main_stamina_rect = ui_playing_main_stamina_tr.Find("Bar").GetComponent<RectTransform>();
                ui_playing_main_stamina_rect_step_width = ui_playing_main_stamina_rect.rect.width / PlayerController.STAMINA_MAX_VALUE;
                ui_playing_main_stamina_tween = new FloatTween();
                ui_playing_main_stamina_tween.SetSetter((value) => ui_playing_main_stamina_rect.SetWidth(value));
                ui_playing_main_stamina_tween.SetAutoUpdate(TweenAutoUpdateMode.APPLY);
                //Drop button
                /*ui_playing_main_dropbtn_go = ui_playing_main_root_tr.FindChild("DropBtn").gameObject;
                ui_playing_main_dropbtn_go.SetActive(false);
                ui_playing_main_dropbtn_go.GetComponent<Button>().onClick.AddListener(PLAYCHAR_OnDropButton);*/
                //drops
                Transform ui_playing_main_drops_tr = ui_playing_main_root_tr.Find("Drops");
                ui_playing_main_drops_go = ui_playing_main_drops_tr.gameObject;
                ui_playing_main_drops_value_txt = ui_playing_main_drops_tr.Find("Value").GetComponent<Text>();
                ui_playing_main_drops_img = ui_playing_main_drops_tr.Find("Icon").GetComponent<Image>();
                //crash stamina
                //stamina icon
                ui_playing_main_staminacrash_tr = ui_playing_main_root_tr.Find("StaminaCrash");
                ui_playing_main_staminacrash_tr.gameObject.SetActive(false);
                ui_playing_main_staminacrash_rect = ui_playing_main_staminacrash_tr.Find("Bar").GetComponent<RectTransform>();
                ui_playing_main_staminacrash_rect_width = ui_playing_main_staminacrash_rect.rect.width;

                ui_playing_main_staminacrash_tween = new FloatTween();
                ui_playing_main_staminacrash_tween.SetAutoUpdate(TweenAutoUpdateMode.APPLY);
                ui_playing_main_staminacrash_tween.SetSetter((value) => ui_playing_main_staminacrash_rect.SetWidth(value));
                ui_playing_main_staminacrash_tween.SetEase(Easing.QuadOut);
                ui_playing_main_staminacrash_tween.SetValues(ui_playing_main_staminacrash_rect_width, 0f);
                ui_playing_main_staminacrash_tween.SetDuration(1.5f);


                //items
                Transform ui_playing_main_items_tr = ui_playing_main_root_tr.Find("Items");
                ui_playing_main_items_scorex_go = ui_playing_main_items_tr.Find("Scorex").gameObject;
                ui_playing_main_items_scorex_go.SetActive(false);
                ui_playing_main_items_scorex_rect = ui_playing_main_items_scorex_go.transform.Find("Bar").GetComponent<RectTransform>();
                ui_playing_main_items_coinsx_go = ui_playing_main_items_tr.Find("Coinsx").gameObject;
                ui_playing_main_items_coinsx_go.SetActive(false);
                ui_playing_main_items_coinsx_rect = ui_playing_main_items_coinsx_go.transform.Find("Bar").GetComponent<RectTransform>();
                ui_playing_main_items_luckx_go = ui_playing_main_items_tr.Find("Luckx").gameObject;
                ui_playing_main_items_luckx_go.SetActive(false);
                ui_playing_main_items_luckx_rect = ui_playing_main_items_luckx_go.transform.Find("Bar").GetComponent<RectTransform>();
                ui_playing_main_items_magnet_go = ui_playing_main_items_tr.Find("Magnet").gameObject;
                ui_playing_main_items_magnet_go.SetActive(false);
                ui_playing_main_items_magnet_rect = ui_playing_main_items_magnet_go.transform.Find("Bar").GetComponent<RectTransform>();
                float ui_playing_main_items_bar_full_width = ui_playing_main_items_magnet_rect.rect.width;
                //tweens
                ui_playing_main_items_scorex_tween = new FloatTween();
                ui_playing_main_items_scorex_tween.SetValues(ui_playing_main_items_bar_full_width, 0f);
                ui_playing_main_items_scorex_tween.SetSetter((value) => ui_playing_main_items_scorex_rect.SetWidth(value));
                ui_playing_main_items_scorex_tween.SetTimeStepGetter(PlayingTime);
                ui_playing_main_items_scorex_tween.SetAutoUpdate(TweenAutoUpdateMode.APPLY);
                ui_playing_main_items_scorex_tween.SetOnComplete(COINS_ScorexCoinComplete);

                ui_playing_main_items_coinsx_tween = new FloatTween();
                ui_playing_main_items_coinsx_tween.SetValues(ui_playing_main_items_bar_full_width, 0f);
                ui_playing_main_items_coinsx_tween.SetSetter((value) => ui_playing_main_items_coinsx_rect.SetWidth(value));
                ui_playing_main_items_coinsx_tween.SetTimeStepGetter(PlayingTime);
                ui_playing_main_items_coinsx_tween.SetAutoUpdate(TweenAutoUpdateMode.APPLY);
                ui_playing_main_items_coinsx_tween.SetOnComplete(COINS_CoinsxCoinComplete);

                ui_playing_main_items_luckx_tween = new FloatTween();
                ui_playing_main_items_luckx_tween.SetValues(ui_playing_main_items_bar_full_width, 0f);
                ui_playing_main_items_luckx_tween.SetSetter((value) => ui_playing_main_items_luckx_rect.SetWidth(value));
                ui_playing_main_items_luckx_tween.SetTimeStepGetter(PlayingTime);
                ui_playing_main_items_luckx_tween.SetAutoUpdate(TweenAutoUpdateMode.APPLY);
                ui_playing_main_items_luckx_tween.SetOnComplete(COINS_LuckxCoinComplete);

                ui_playing_main_items_magnet_tween = new FloatTween();
                ui_playing_main_items_magnet_tween.SetValues(ui_playing_main_items_bar_full_width, 0f);
                ui_playing_main_items_magnet_tween.SetSetter((value) => ui_playing_main_items_magnet_rect.SetWidth(value));
                ui_playing_main_items_magnet_tween.SetTimeStepGetter(PlayingTime);
                ui_playing_main_items_magnet_tween.SetAutoUpdate(TweenAutoUpdateMode.APPLY);
                ui_playing_main_items_magnet_tween.SetOnComplete(COINS_MagnetCoinComplete);

                //Start Items
                Transform ui_playing_main_startitems_root_tr = ui_playing_main_root_tr.Find("StartItems");
                ui_playing_main_startitems_root_go = ui_playing_main_startitems_root_tr.gameObject;
                ui_playing_main_startitems_root_go.SetActive(false);
                ui_playing_main_startitems_scorexholder_go = ui_playing_main_startitems_root_tr.Find("ScorexHolder").gameObject;
                ui_playing_main_startitems_scorex_go = ui_playing_main_startitems_scorexholder_go.transform.Find("Scorex").gameObject;
                ui_playing_main_startitems_scorex_go.SetActive(false);
                ui_playing_main_startitems_scorex_go.GetComponent<Button>().onClick.AddListener(PLAYCHAR_ActivateStartScorex);
                ui_playing_main_startitems_coinsxholder_go = ui_playing_main_startitems_root_tr.Find("CoinsxHolder").gameObject;
                ui_playing_main_startitems_coinsx_go = ui_playing_main_startitems_coinsxholder_go.transform.Find("Coinsx").gameObject;
                ui_playing_main_startitems_coinsx_go.SetActive(false);
                ui_playing_main_startitems_coinsx_go.GetComponent<Button>().onClick.AddListener(PLAYCHAR_ActivateStartCoinsx);
                ui_playing_main_startitems_luckxholder_go = ui_playing_main_startitems_root_tr.Find("LuckxHolder").gameObject;
                ui_playing_main_startitems_luckx_go = ui_playing_main_startitems_luckxholder_go.transform.Find("Luckx").gameObject;
                ui_playing_main_startitems_luckx_go.SetActive(false);
                ui_playing_main_startitems_luckx_go.GetComponent<Button>().onClick.AddListener(PLAYCHAR_ActivateStartLuckx);
            }
            //Playing UI Pause Page
            {
                Transform ui_playing_pause_root_tr = ui_playing_root_tr.Find("Pause");
                ui_playing_pause_root_go = ui_playing_pause_root_tr.gameObject;
                ui_playing_pause_root_go.SetActive(false);
                ui_playing_pause_root_tr.Find("JoystickBtn").GetComponent<Button>().onClick.AddListener(_UI_PLAYING_PAUSE_OnResumeBtn);
                //Top Panel
                {
                    Transform ui_pause_top_tr = ui_playing_pause_root_tr.Find("TopPanel");
                    ui_playing_pause_top_luck_value_txt = ui_pause_top_tr.Find("Layout/Luck/Value").GetComponent<Text>();
                    ui_playing_pause_top_coins_value_txt = ui_pause_top_tr.Find("Layout/Coins/Value").GetComponent<Text>();
                }
            }
            //Playing UI Cutscene Page
            {
                Transform ui_playing_cutscene_root_tr = ui_playing_root_tr.Find("Cutscene");
                ui_playing_cutscene_root_go = ui_playing_cutscene_root_tr.gameObject;
                ui_playing_cutscene_root_go.SetActive(false);
                //Screen button
                ui_playing_cutscene_root_tr.Find("ScreenBtn").GetComponent<Button>().onClick.AddListener(GAME_PLAYING_CutsceneComplete);
            }
            //Playing UI Crash Cutscene Page
            {
                Transform ui_playing_crash_root_tr = ui_playing_root_tr.Find("Crash");
                ui_playing_crash_root_go = ui_playing_crash_root_tr.gameObject;
                ui_playing_crash_root_go.SetActive(false);
                //Continue button
                ui_playing_crash_continuebtn_go = ui_playing_crash_root_tr.Find("ContinueBtn").gameObject;
                ui_playing_crash_continuebtn_go.SetActive(false);
                ui_playing_crash_continuebtn_go.GetComponent<Button>().onClick.AddListener(_UI_PLAYING_CRASH_OnContinueBtn);
                ui_playing_crash_watchbtn_go = ui_playing_crash_root_tr.Find("WatchBtn").gameObject;
                ui_playing_crash_watchbtn_go.GetComponent<Button>().onClick.AddListener(AD_OnWatchContinue);
                //Screen button
                ui_playing_crash_screen_btn = ui_playing_crash_root_tr.Find("ScreenBtn").GetComponent<Button>();
                ui_playing_crash_screen_btn.onClick.AddListener(_UI_PLAYING_CRASH_OnScreenBtn);
            }
            //Playing UI Rest Cutscene Page
            {
                Transform ui_playing_rest_root_tr = ui_playing_root_tr.Find("Rest");
                ui_playing_rest_root_go = ui_playing_rest_root_tr.gameObject;
                ui_playing_rest_root_go.SetActive(false);
                //Screen button
                ui_playing_rest_screenbtn = ui_playing_rest_root_tr.Find("ScreenBtn").GetComponent<Button>();

                //stamina icon
                ui_playing_rest_stamina_tr = ui_playing_rest_root_tr.Find("Stamina");
                ui_playing_rest_stamina_rect = ui_playing_rest_stamina_tr.Find("Bar").GetComponent<RectTransform>();
                ui_playing_rest_stamina_rect_width = ui_playing_rest_stamina_rect.rect.width;

                ui_playing_rest_stamina_tween = new FloatTween();
                ui_playing_rest_stamina_tween.SetAutoUpdate(TweenAutoUpdateMode.APPLY);
                ui_playing_rest_stamina_tween.SetSetter((value) => ui_playing_rest_stamina_rect.SetWidth(value));
                ui_playing_rest_stamina_tween.SetEase(Easing.QuadOut);
                ui_playing_rest_stamina_tween.SetValues(0f, ui_playing_rest_stamina_rect_width);
                ui_playing_rest_stamina_tween.SetDuration(2f);

                //results
                Transform rest_results_tr = ui_playing_rest_root_tr.Find("Results");
                ui_playing_rest_coins_go = rest_results_tr.Find("Coins").gameObject;
                ui_playing_rest_coins_go.SetActive(false);
                ui_playing_rest_coins_value_txt = ui_playing_rest_coins_go.transform.Find("Value").GetComponent<Text>();
                ui_playing_rest_nbcoins_go = rest_results_tr.Find("NBCoins").gameObject;
                ui_playing_rest_nbcoins_go.SetActive(false);
                ui_playing_rest_nbcoins_value_txt = ui_playing_rest_nbcoins_go.transform.Find("Value").GetComponent<Text>();
            }
        }

        //Shop UI
        {
            Transform ui_shop_root_tr = ui_root.Find("Shop");
            ui_shop_root_go = ui_shop_root_tr.gameObject;
            ui_shop_root_go.SetActive(false);
            //Top Panel
            {
                Transform ui_shop_top_tr = ui_shop_root_tr.Find("TopPanel");
                ui_shop_top_luck_value_txt = ui_shop_top_tr.Find("Layout/Luck/Value").GetComponent<Text>();
                ui_shop_top_coins_value_txt = ui_shop_top_tr.Find("Layout/Coins/Value").GetComponent<Text>();
            }
            //Char page
            {
                Transform ui_shop_char_root_tr = ui_shop_root_tr.Find("Char");
                ui_shop_char_root_go = ui_shop_char_root_tr.gameObject;
                ui_shop_char_root_go.SetActive(false);
                ui_shop_char_name_loc = ui_shop_char_root_tr.Find("Name").GetComponent<LocalizeText>();
                ui_shop_char_desc_loc = ui_shop_char_root_tr.Find("Desc").GetComponent<LocalizeText>();
                ui_shop_char_node = ui_shop_char_root_tr.Find("CharNode");
                //confirm button
                {
                    Transform ui_shop_char_confirm_tr = Instantiate(ui_so.shop_confirm_prefab).transform;
                    ui_shop_char_confirm_tr.SetParent(ui_shop_char_root_tr, false);
                    ui_shop_char_confirm_active_go = ui_shop_char_confirm_tr.Find("Active").gameObject;
                    ui_shop_char_confirm_select_go = ui_shop_char_confirm_tr.Find("Select").gameObject;
                    ui_shop_char_confirm_select_go.GetComponent<Button>().onClick.AddListener(_UI_SHOP_CHAR_CONFIRM_OnSelectBtn);
                    ui_shop_char_confirm_buy_go = ui_shop_char_confirm_tr.Find("Buy").gameObject;
                    ui_shop_char_confirm_buy_go.GetComponent<Button>().onClick.AddListener(_UI_SHOP_CHAR_CONFIRM_OnBuyBtn);
                    ui_shop_char_confirm_na_go = ui_shop_char_confirm_tr.Find("Na").gameObject;
                    Transform ui_shop_char_confirm_price_tr = ui_shop_char_confirm_tr.Find("Price");
                    ui_shop_char_confirm_price_go = ui_shop_char_confirm_price_tr.gameObject;
                    ui_shop_char_confirm_price_coins_go = ui_shop_char_confirm_price_tr.Find("Coins").gameObject;
                    ui_shop_char_confirm_price_coins_value_txt = ui_shop_char_confirm_price_coins_go.transform.Find("Value").GetComponent<Text>();
                    ui_shop_char_confirm_price_luck_go = ui_shop_char_confirm_price_tr.Find("Luck").gameObject;
                    ui_shop_char_confirm_price_luck_value_txt = ui_shop_char_confirm_price_luck_go.transform.Find("Value").GetComponent<Text>();
                }
                //scroll
                {
                    Transform ui_shop_char_scroll_tr = ui_shop_char_root_tr.Find("Scroll");
                    ui_shop_char_scroll = ui_shop_char_scroll_tr.GetComponent<ScrollRect>();
                    {
                        var trigger = ui_shop_char_scroll_tr.gameObject.AddComponent<EventTrigger>();
                        if (trigger.triggers == null) { trigger.triggers = new List<EventTrigger.Entry>(2); }
                        //begin drag
                        var entry = new EventTrigger.Entry();
                        entry.eventID = EventTriggerType.BeginDrag;
                        entry.callback.AddListener(_UI_SHOP_SCROLL_OnBeginDrag);
                        trigger.triggers.Add(entry);
                        //end drag
                        entry = new EventTrigger.Entry();
                        entry.eventID = EventTriggerType.EndDrag;
                        entry.callback.AddListener(_UI_SHOP_CHAR_SCROLL_OnEndDrag);
                        trigger.triggers.Add(entry);
                    }
                    ui_shop_char_scroll_content_tr = ui_shop_char_scroll_tr.Find("Viewport/Content");
                    //scroll points
                    ui_shop_char_scroll_points = new float[pch_so.playchar_slots.Length + 1];
                    float scrollpoint_step = 1f / (pch_so.playchar_slots.Length + 4 + 1);
                    //fill char data
                    for (int i = 0, l = pch_so.playchar_slots.Length + 1; i < l; ++i) {
                        Transform el = Instantiate(ui_so.shop_chartheme_scrollitem_prefab).transform;
                        el.SetParent(ui_shop_char_scroll_content_tr, false);
                        if (i < pch_so.playchar_slots.Length) {
                            var slot = pch_so.playchar_slots[i];
                            el.GetComponent<Image>().sprite = slot.ui_playchar_icon;
                        } else {
                            el.GetComponent<Image>().sprite = ui_so.shop_char_question_icon;
                        }

                        _UI_SHOP_CHAR_SCROLL_AddListener(el.GetComponent<Button>(), i);

                        //scroll point
                        ui_shop_char_scroll_points[i] = scrollpoint_step * (2.5f + i);
                    }

                    //item selected icon
                    ui_shop_char_scroll_selitem_icon_tr = Instantiate(ui_so.shop_chartheme_scrollitem_selected_prefab).transform;
                    ui_shop_char_scroll_selitem_icon_tr.SetParent(ui_shop_char_scroll_content_tr.GetChild(0), false);

                    if (ui_shop_scroll_tween == null) {
                        ui_shop_scroll_tween = new FloatTween();
                        ui_shop_scroll_tween.SetBeginGetter(_UI_SHOP_CHAR_SCROLL_ScrollPosGetter, false);
                        ui_shop_scroll_tween.SetSetter(_UI_SHOP_CHAR_SCROLL_ScrollPosSetter);
                        ui_shop_scroll_tween.SetAutoUpdate(TweenAutoUpdateMode.APPLY);
                        ui_shop_scroll_tween.SetEase(Easing.QuadInOut);
                    }
                }
                //skin
                {
                    ui_shop_char_skin_go = ui_shop_char_root_tr.Find("Skin").gameObject;
                    for (int i = 0, l = ui_shop_char_skin_go.transform.childCount; i < l; ++i) {
                        Transform child = ui_shop_char_skin_go.transform.GetChild(i);
                        child.GetComponent<Image>().sprite = ui_so.shop_level_icon_empty;
                        _UI_SHOP_CHAR_SKIN_ItemAddListener(child.GetComponent<Button>(), i);
                    }
                    _UI_SHOP_CHAR_SKIN_SetNumItems(0);
                }

            }
            //Theme Page
            {
                Transform ui_shop_theme_root_tr = ui_shop_root_tr.Find("Theme");
                ui_shop_theme_root_go = ui_shop_theme_root_tr.gameObject;
                ui_shop_theme_root_go.SetActive(false);
                ui_shop_theme_name_loc = ui_shop_theme_root_tr.Find("Name").GetComponent<LocalizeText>();
                ui_shop_theme_desc_loc = ui_shop_theme_root_tr.Find("Desc").GetComponent<LocalizeText>();
                ui_shop_theme_node = ui_shop_theme_root_tr.Find("CharNode").Find("AnimatedNode");
                //confirm button
                {
                    Transform ui_shop_theme_confirm_tr = Instantiate(ui_so.shop_confirm_prefab).transform;
                    ui_shop_theme_confirm_tr.SetParent(ui_shop_theme_root_tr, false);
                    ui_shop_theme_confirm_active_go = ui_shop_theme_confirm_tr.Find("Active").gameObject;
                    ui_shop_theme_confirm_select_go = ui_shop_theme_confirm_tr.Find("Select").gameObject;
                    ui_shop_theme_confirm_select_go.GetComponent<Button>().onClick.AddListener(_UI_SHOP_THEME_CONFIRM_OnSelectBtn);
                    ui_shop_theme_confirm_buy_go = ui_shop_theme_confirm_tr.Find("Buy").gameObject;
                    ui_shop_theme_confirm_buy_go.GetComponent<Button>().onClick.AddListener(_UI_SHOP_THEME_CONFIRM_OnBuyBtn);
                    ui_shop_theme_confirm_na_go = ui_shop_theme_confirm_tr.Find("Na").gameObject;
                    Transform ui_shop_theme_confirm_price_tr = ui_shop_theme_confirm_tr.Find("Price");
                    ui_shop_theme_confirm_price_go = ui_shop_theme_confirm_price_tr.gameObject;
                    ui_shop_theme_confirm_price_coins_go = ui_shop_theme_confirm_price_tr.Find("Coins").gameObject;
                    ui_shop_theme_confirm_price_coins_value_txt = ui_shop_theme_confirm_price_coins_go.transform.Find("Value").GetComponent<Text>();
                    ui_shop_theme_confirm_price_luck_go = ui_shop_theme_confirm_price_tr.Find("Luck").gameObject;
                    ui_shop_theme_confirm_price_luck_value_txt = ui_shop_theme_confirm_price_luck_go.transform.Find("Value").GetComponent<Text>();
                }
                //scroll
                {
                    Transform ui_shop_theme_scroll_tr = ui_shop_theme_root_tr.Find("Scroll");
                    ui_shop_theme_scroll = ui_shop_theme_scroll_tr.GetComponent<ScrollRect>();
                    {
                        var trigger = ui_shop_theme_scroll_tr.gameObject.AddComponent<EventTrigger>();
                        if (trigger.triggers == null) { trigger.triggers = new List<EventTrigger.Entry>(2); }
                        //begin drag
                        var entry = new EventTrigger.Entry();
                        entry.eventID = EventTriggerType.BeginDrag;
                        entry.callback.AddListener(_UI_SHOP_SCROLL_OnBeginDrag);
                        trigger.triggers.Add(entry);
                        //end drag
                        entry = new EventTrigger.Entry();
                        entry.eventID = EventTriggerType.EndDrag;
                        entry.callback.AddListener(_UI_SHOP_THEME_SCROLL_OnEndDrag);
                        trigger.triggers.Add(entry);
                    }
                    ui_shop_theme_scroll_content_tr = ui_shop_theme_scroll_tr.Find("Viewport/Content");
                    //scroll points
                    ui_shop_theme_scroll_points = new float[thm_so.theme_slots.Length + 1];
                    float scrollpoint_step = 1f / (thm_so.theme_slots.Length + 4 + 1);
                    //fill theme data
                    for (int i = 0, l = thm_so.theme_slots.Length + 1; i < l; ++i) {
                        Transform el = Instantiate(ui_so.shop_chartheme_scrollitem_prefab).transform;
                        el.SetParent(ui_shop_theme_scroll_content_tr, false);
                        if (i < thm_so.theme_slots.Length) {
                            var slot = thm_so.theme_slots[i];
                            el.GetComponent<Image>().sprite = slot.ui_themeslot_icon;
                        } else {
                            el.GetComponent<Image>().sprite = ui_so.shop_theme_question_icon;
                        }

                        _UI_SHOP_THEME_SCROLL_AddListener(el.GetComponent<Button>(), i);

                        //scroll point
                        ui_shop_theme_scroll_points[i] = scrollpoint_step * (2.5f + i);
                    }
                    //item selected icon
                    ui_shop_theme_scroll_selitem_icon_tr = Instantiate(ui_so.shop_chartheme_scrollitem_selected_prefab).transform;
                    ui_shop_theme_scroll_selitem_icon_tr.SetParent(ui_shop_theme_scroll_content_tr.GetChild(0), false);
                }
            }
            //Buy Page
            {
                ui_shop_buy_root_go = ui_shop_root_tr.Find("Buy").gameObject;
                ui_shop_buy_root_go.SetActive(false);
                ui_shop_buy_content_tr = ui_shop_buy_root_go.transform.Find("Scroll/Viewport/Content");
            }
        }

        //Chs Ui
        {
            Transform ui_chs_root_tr = ui_root.Find("Chs");
            ui_chs_root_go = ui_chs_root_tr.gameObject;
            ui_chs_root_go.SetActive(false);
            //Top
            Transform ui_chs_top_root_tr = ui_chs_root_tr.Find("TopPanel");
            ui_chs_top_loc = ui_chs_top_root_tr.Find("Text").GetComponent<LocalizeText>();
            //Main
            Transform ui_chs_main_root_tr = ui_chs_root_tr.Find("Main");
            ui_chs_main_root_go = ui_chs_main_root_tr.gameObject;
            ui_chs_main_root_go.SetActive(false);
            //Reward
            Transform ui_chs_reward_root_tr = ui_chs_main_root_tr.Find("Reward");
            ui_chs_reward_icon = ui_chs_reward_root_tr.Find("Icon").GetComponent<Image>();
            Transform ui_chs_reward_main_root_tr = ui_chs_reward_root_tr.Find("Main");
            ui_chs_reward_name_loc = ui_chs_reward_main_root_tr.Find("Name").GetComponent<LocalizeText>();
            ui_chs_reward_value_txt = ui_chs_reward_main_root_tr.Find("Value").GetComponent<Text>();
            ui_chs_reward_bonus_go = ui_chs_reward_root_tr.Find("Bonus").gameObject;
            ui_chs_reward_bonus_go.SetActive(false);
            ui_chs_reward_bonus_value_txt = ui_chs_reward_root_tr.Find("Bonus/Value").GetComponent<Text>();
            //Content
            ui_chs_tasks_tr = ui_chs_main_root_tr.Find("Tasks/Content");
        }

        //End UI
        {
            Transform ui_end_root_tr = ui_root.Find("End");
            ui_end_root_go = ui_end_root_tr.gameObject;
            ui_end_root_go.SetActive(false);

            Transform ui_end_result_tr = ui_end_root_tr.Find("Results");
            ui_end_result_root_go = ui_end_result_tr.gameObject;
            ui_end_result_score_go = ui_end_result_tr.Find("Score").gameObject;
            ui_end_result_score_value_txt = ui_end_result_score_go.transform.Find("Value").GetComponent<Text>();
            ui_end_result_nbcoins_go = ui_end_result_tr.Find("NBCoins").gameObject;
            ui_end_result_nbcoins_value_txt = ui_end_result_nbcoins_go.transform.Find("Value").GetComponent<Text>();
            ui_end_result_coins_go = ui_end_result_tr.Find("Coins").gameObject;
            ui_end_result_coins_value_txt = ui_end_result_coins_go.transform.Find("Value").GetComponent<Text>();
            Transform ui_end_result_luck_tr = ui_end_result_tr.Find("Luck");
            ui_end_result_luck_go = ui_end_result_luck_tr.gameObject;
            ui_end_result_luck_sign_txt = ui_end_result_luck_tr.Find("Text").GetComponent<Text>();
            ui_end_result_luck_value_txt = ui_end_result_luck_tr.Find("Value").GetComponent<Text>();
            ui_end_result_char_tr = ui_end_result_tr.Find("CharNode");

            //joystick btn
            ui_end_joystick_btn_go = ui_end_root_tr.Find("JoystickBtn").gameObject;
            ui_end_joystick_btn_go.SetActive(false);
            ui_end_joystick_btn_go.GetComponent<Button>().onClick.AddListener(_UI_END_OnJoystickBtn);
        }

        //FB UI
        {
            Transform ui_fb_root_tr = ui_root.Find("Fb");
            ui_fb_root_go = ui_fb_root_tr.gameObject;
            ui_fb_root_go.SetActive(false);
            //TopPanel
            ui_fb_toppanel_loc = ui_fb_root_tr.Find("TopPanel").Find("Text").GetComponent<LocalizeText>();
            ui_fb_toppanel_txt = ui_fb_root_tr.Find("TopPanel").Find("Text").GetComponent<Text>();

            //Login Page
            Transform ui_fb_login_root_tr = ui_fb_root_tr.Find("Login");
            ui_fb_login_root_go = ui_fb_login_root_tr.gameObject;
            ui_fb_login_root_go.SetActive(false);
            Transform ui_fb_login_content_tr = ui_fb_login_root_tr.Find("Content");
            ui_fb_login_button_go = ui_fb_login_content_tr.Find("LoginBtn").gameObject;
            Button login_btn = ui_fb_login_button_go.GetComponent<Button>();
            login_btn.interactable = false;
            login_btn.onClick.AddListener(_UI_FB_LOGIN_OnLoginBtn);
            Transform ui_fb_login_reward_tr = ui_fb_login_content_tr.Find("Reward");
            ui_fb_login_reward_coins_value_txt = ui_fb_login_reward_tr.Find("Coins/Value").GetComponent<Text>();
            ui_fb_login_reward_luck_value_txt = ui_fb_login_reward_tr.Find("Luck/Value").GetComponent<Text>();

            //Logout Page
            Transform ui_fb_logout_root_tr = ui_fb_root_tr.Find("Logout");
            ui_fb_logout_root_go = ui_fb_logout_root_tr.gameObject;
            ui_fb_logout_root_go.SetActive(false);
            Transform ui_fb_logout_content_tr = ui_fb_logout_root_tr.Find("Content");
            ui_fb_logout_button_go = ui_fb_logout_content_tr.Find("LogoutBtn").gameObject;
            ui_fb_logout_button_go.GetComponent<Button>().onClick.AddListener(_UI_FB_LOGOUT_OnLogoutBtn);

            //Stat Page
            Transform ui_fb_stat_root_tr = ui_fb_root_tr.Find("Stat");
            ui_fb_stat_root_go = ui_fb_stat_root_tr.gameObject;
            ui_fb_stat_root_go.SetActive(false);
            ui_fb_stat_char_node = ui_fb_stat_root_tr.Find("CharNode");
            Transform ui_fb_stats_root_tr = ui_fb_stat_root_tr.Find("Stats");
            ui_fb_stat_score_value_txt = ui_fb_stats_root_tr.Find("Score/Value").GetComponent<Text>();
            ui_fb_stat_coins_value_txt = ui_fb_stats_root_tr.Find("Coins/Value").GetComponent<Text>();
            ui_fb_stat_luck_value_txt = ui_fb_stats_root_tr.Find("Luck/Value").GetComponent<Text>();

            //Top Page
            Transform ui_fb_top_root_tr = ui_fb_root_tr.Find("Top");
            ui_fb_top_root_go = ui_fb_top_root_tr.gameObject;
            ui_fb_top_root_go.SetActive(false);
            Transform ui_fb_top_content_root_tr = ui_fb_top_root_tr.Find("Scroll/Viewport/Content");
            //Friends
            Transform ui_fb_top_friends_root_tr = ui_fb_top_content_root_tr.Find("Friends");
            ui_fb_top_friends_delimiter_go = ui_fb_top_friends_root_tr.Find("Delimiter").gameObject;
            ui_fb_top_friends_go = ui_fb_top_friends_root_tr.gameObject;
            ui_fb_top_topfriends_tr = ui_fb_top_friends_root_tr.Find("TopGroup");
            ui_fb_top_bottomfriends_tr = ui_fb_top_friends_root_tr.Find("BottomGroup");
            //All
            Transform ui_fb_top_all_root_tr = ui_fb_top_content_root_tr.Find("All");
            ui_fb_top_all_tr = ui_fb_top_all_root_tr.Find("Group");
            //Weekly
            //Transform ui_fb_top_weekly_root_tr = ui_fb_top_content_root_tr.FindChild("Weekly");
            //ui_fb_top_weekly_tr = ui_fb_top_weekly_root_tr.FindChild("Group");

            onOnlineStateChanged += _UI_FB_OnOnlineStateChanged;
            onLoginStateChanged += _UI_FB_OnLoginStateChanged;
            onUserHighscoreChanged += _UI_FB_OnUserHighscoreChanged;
        }

        //Reward UI
        {
            Transform ui_reward_root_tr = ui_root.Find("Reward");
            ui_reward_root_go = ui_reward_root_tr.gameObject;
            ui_reward_root_go.SetActive(false);
            //mbox
            ui_reward_mbox_root_go = ui_reward_root_tr.Find("BoxRoot").gameObject;
            ui_reward_mbox_root_go.SetActive(false);
            Transform ui_reward_mbox_node_tr = ui_reward_mbox_root_go.transform.Find("BoxAnimNode/Scale");
            ui_reward_mbox_go = ui_reward_mbox_node_tr.Find("MBox").gameObject;
            ui_reward_mbox_go.SetActive(false);
            ui_reward_smbox_go = ui_reward_mbox_node_tr.Find("SMBox").gameObject;
            ui_reward_smbox_go.SetActive(false);
            ui_reward_mbox_emitter = ui_reward_mbox_root_go.transform.Find("Particles").GetComponent<ParticleSystem>();
            //item
            ui_reward_item_root_go = ui_reward_root_tr.Find("ItemRoot").gameObject;
            ui_reward_item_root_go.SetActive(false);
            ui_reward_item_node_tr = ui_reward_item_root_go.transform.Find("ItemAnimNode/Scale");
            //screen btn
            ui_reward_scrbtn = ui_reward_root_tr.Find("ScreenBtn").GetComponent<Button>();

            //desc
            Transform ui_reward_desc_root_tr = ui_reward_root_tr.Find("ItemDesc");
            ui_reward_desc_root_go = ui_reward_desc_root_tr.gameObject;
            Transform ui_reward_desc_itemname_tr = ui_reward_desc_root_tr.Find("ItemName");
            ui_reward_desc_value_txt = ui_reward_desc_itemname_tr.Find("Value").GetComponent<Text>();
            ui_reward_desc_name_loc = ui_reward_desc_itemname_tr.Find("Name").GetComponent<LocalizeText>();
            ui_reward_desc_desc_go = ui_reward_desc_root_tr.Find("Desc").gameObject;
            ui_reward_desc_desc_loc = ui_reward_desc_desc_go.GetComponent<LocalizeText>();
        }

        //Highscore UI
        {
            Transform ui_hs_root_tr = ui_root.Find("Highscore");
            ui_hs_root_go = ui_hs_root_tr.gameObject;
            ui_hs_root_go.SetActive(false);

            ui_hs_score_go = ui_hs_root_tr.Find("Score").gameObject;
            ui_hs_score_value_txt = ui_hs_score_go.GetComponent<Text>();
            ui_hs_desc_go = ui_hs_root_tr.Find("Desc").gameObject;
            ui_hs_char_tr = ui_hs_root_tr.Find("CharNode/AnimNode");
            ui_hs_sharebtn_go = ui_hs_root_tr.Find("ShareBtn").gameObject;
            ui_hs_sharebtn_go.SetActive(false);
            ui_hs_sharebtn_go.GetComponent<Button>().onClick.AddListener(FB_FeedHighscore);
        }

        onPlaycharReady += UI_OnPlaycharReady;
        onPlaycharReleasing += UI_OnPlaycharReleasing;
        onGameStateChanged += UI_OnGameStateChanged;
        onPlayingStateChanged += UI_OnPlayingStateChanged;
    }

    #region [UI STATE]
    void UI_STATE_SwitchTo(UiState state, object page = null)
    {
        if (ui_state == UiState.HIDDEN && state == UiState.HIDDEN) return;

        if (state != UiState.HIDDEN) { ui_target_page = page; }

        if (ui_state == state) {
            //same state. change page
            _UI_STATE_Show();
        } else {
            //different state
            ui_last_state = ui_state;
            ui_state = state;
            if (ui_last_state == UiState.HIDDEN) {
                //show ui
                _UI_STATE_Show();
            } else {
                //hide or switch
                _UI_STATE_Hide();
            }
        }
    }
    void _UI_STATE_Show()
    {
        switch (ui_state) {
        case UiState.BEGIN:
            UiBeginPage begin_page = ui_target_page is UiBeginPage ? (UiBeginPage)ui_target_page : UiBeginPage.MAIN;
            UI_BEGIN_PageTo(begin_page);
            AUD_PlayMusic(MusicType.BEGIN);
            break;
        case UiState.SHOP:
            UiShopPage shop_page = ui_target_page is UiShopPage ? (UiShopPage)ui_target_page : UiShopPage.CHAR;
            UI_SHOP_PageTo(shop_page);
            AUD_PlayMusic(MusicType.UI_SHOP);
            break;
        case UiState.CHS:
            UiChsPage chs_page = ui_target_page is UiChsPage ? (UiChsPage)ui_target_page : UiChsPage.PROG;
            UI_CHS_PageTo(chs_page);
            AUD_PlayMusic(MusicType.UI_SHOP);
            break;
        case UiState.FB:
            UiFbPage fb_page = ui_target_page is UiFbPage ? (UiFbPage)ui_target_page : UiFbPage.LOGIN;
            UI_FB_PageTo(fb_page);
            AUD_PlayMusic(MusicType.UI_SHOP);
            break;
        case UiState.PLAYING:
            UiPlayingPage playing_page = ui_target_page is UiPlayingPage ? (UiPlayingPage)ui_target_page : UiPlayingPage.MAIN;
            UI_PLAYING_PageTo(playing_page);
            break;
        case UiState.END:
            UiEndPage end_page = ui_target_page is UiEndPage ? (UiEndPage)ui_target_page : UiEndPage.MAIN;
            UI_END_PageTo(end_page);
            break;
        }
    }
    void _UI_STATE_Hide()
    {
        switch (ui_last_state) {
        case UiState.BEGIN:
            UI_BEGIN_PageTo(UiBeginPage.HIDDEN);
            break;
        case UiState.SHOP:
            UI_SHOP_PageTo(UiShopPage.HIDDEN);
            break;
        case UiState.CHS:
            UI_CHS_PageTo(UiChsPage.HIDDEN);
            break;
        case UiState.USER:
            break;
        case UiState.PLAYING:
            UI_PLAYING_PageTo(UiPlayingPage.HIDDEN);
            break;
        case UiState.END:
            UI_END_PageTo(UiEndPage.HIDDEN);
            break;
        case UiState.FB:
            UI_FB_PageTo(UiFbPage.HIDDEN);
            break;
        }
    }
    void _UI_STATE_OnHideComplete()
    {
        if (ui_state != UiState.HIDDEN) {
            _UI_STATE_Show();
        } else {
            //hide menu
            UI_MENU_SwitchTo(UiMenuPage.HIDDEN);
        }
    }
    #endregion
#if UNITY_ANDROID
    void _UI_HardwareBtnBackUpdate()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) {
            switch (ui_state) {
            case UiState.END:
                _UI_OnBtnRestart();
                break;
            case UiState.PLAYING:
                if (ui_playing_page == UiPlayingPage.PAUSE) {
                    _UI_PLAYING_PAUSE_OnResumeBtn();
                }
                break;
            case UiState.CHS:
            case UiState.SHOP:
            case UiState.FB:
                _UI_OnBtnBack();
                break;
            }
            RemoveUpdateOnMT(_UI_HardwareBtnBackUpdate);
        }
    }
#endif
    void _UI_OnBtnBack()
    {
        AUD_UI_Sound(UiSoundType.BACK);

        switch (ui_state) {
        case UiState.SHOP:
            UI_STATE_SwitchTo(UiState.BEGIN, UiBeginPage.MAIN);
            //returning from shop. Save point
            USER_SaveState(false);
            break;
        case UiState.CHS:
            switch (game_state) {
            case GameState.BEGIN: UI_STATE_SwitchTo(UiState.BEGIN, UiBeginPage.MAIN); break;
            case GameState.PLAYING: UI_STATE_SwitchTo(UiState.PLAYING, UiPlayingPage.PAUSE); break;
            }
            break;
        case UiState.FB:
            switch (ui_fb_page) {
            case UiFbPage.LOGOUT: UI_STATE_SwitchTo(UiState.FB, UiFbPage.STAT); break;
            default: UI_STATE_SwitchTo(UiState.BEGIN, UiBeginPage.MAIN); break;
            }
            break;
        default:
            switch (ui_menu_page) {
            case UiMenuPage.HIGHSCORE:
                //Highscore page
                //continue showing end page
                ui_menu_backbtn_coroutine_wait = false;
                break;
            }
            break;
        }
    }
    void _UI_OnBtnRestart()
    {
        AUD_UI_Sound(UiSoundType.BACK);

        if (ui_state == UiState.PLAYING && ui_playing_page == UiPlayingPage.PAUSE) {
            GAME_SwitchTo(GameState.END);
        } else {
            GAME_SwitchTo(GameState.BEGIN);
        }
    }
    void _UI_OnBtnQuit()
    {
        AUD_UI_Sound(UiSoundType.BACK);
        ui_timer.Complete(true);
        ui_timer.SetOnCompleteOnce(() => Application.Quit());
        ui_timer.Reset(0.25f);
    }
    void _UI_OnBtnStateTo(UiState state, object page)
    {
        AUD_UI_Sound(UiSoundType.BUTTON);

        UI_STATE_SwitchTo(state, page);
    }

#if CODEDEBUG
    #region [Console]
    const string CONSOLE_FORMAT_CLASS_FUNC = "{0}.{1}";
    const string CONSOLE_FORMAT_MODULE_DEBUG = "{0}: {1}";
    const string CONSOLE_FORMAT_ITALIC = "<i>{0}</i>";
    const string CONSOLE_FORMAT_BOLD = "<b>{0}</b>";
    const string CONSOLE_FORMAT_COLOR = "<color={0}>{1}</color>";
    const string CONSOLE_COLOR_BLACK = "black";
    const string CONSOLE_COLOR_WHITE = "white";
    const string CONSOLE_COLOR_RED = "brown";
    const string CONSOLE_COLOR_GREEN = "green";
    const string CONSOLE_COLOR_BLUE = "blue";
    const string CONSOLE_COLOR_GRAY = "gray";
    const string CONSOLE_COLOR_SILVER = "silver";
    const string CONSOLE_COLOR_ORANGE = "orange";
    const string CONSOLE_COLOR_YELLOW = "yellow";
    const string CONSOLE_COLOR_TEAL = "teal";
    public enum ConsoleColorType { BLACK, WHITE, RED, GREEN, BLUE, GRAY, SILVER, ORANGE, YELLOW, TEAL }
    static string GetConsoleColorStr(ConsoleColorType color)
    {
        switch (color) {
        case ConsoleColorType.BLACK: return CONSOLE_COLOR_BLACK;
        case ConsoleColorType.WHITE: return CONSOLE_COLOR_WHITE;
        case ConsoleColorType.RED: return CONSOLE_COLOR_RED;
        case ConsoleColorType.GREEN: return CONSOLE_COLOR_GREEN;
        case ConsoleColorType.BLUE: return CONSOLE_COLOR_BLUE;
        case ConsoleColorType.GRAY: return CONSOLE_COLOR_GRAY;
        case ConsoleColorType.SILVER: return CONSOLE_COLOR_SILVER;
        case ConsoleColorType.ORANGE: return CONSOLE_COLOR_ORANGE;
        case ConsoleColorType.YELLOW: return CONSOLE_COLOR_YELLOW;
        case ConsoleColorType.TEAL: return CONSOLE_COLOR_TEAL;
        default: return CONSOLE_COLOR_WHITE;
        }
    }
    public static string ConsoleFormatModule(string className, string methodName)
    {
        return string.Format(CONSOLE_FORMAT_CLASS_FUNC, className, methodName);
    }
    public static string ConsoleColor(string txt, ConsoleColorType color)
    {
        return string.Format(CONSOLE_FORMAT_COLOR, GetConsoleColorStr(color), txt);
    }
    public static string ConsoleBold(string txt)
    {
        return string.Format(CONSOLE_FORMAT_BOLD, txt);
    }
    public static string ConsoleItalic(string txt)
    {
        return string.Format(CONSOLE_FORMAT_ITALIC, txt);
    }
    public static void LogSuccess(string module, string debug_str, params object[] values)
    {
        debug_str = ConsoleApplyDecoration(ConsoleColorType.GREEN, ConsoleColorType.SILVER, module, debug_str, values);
        GameController.Instance.UI_CONSOLE_Log(debug_str);

#if UNITY_EDITOR
        Debug.Log(debug_str);
#endif
    }
    public static void LogWarning(string module, string debug_str, params object[] values)
    {
        debug_str = ConsoleApplyDecoration(ConsoleColorType.ORANGE, ConsoleColorType.SILVER, module, debug_str, values);
        GameController.Instance.UI_CONSOLE_Log(debug_str);

#if UNITY_EDITOR
        Debug.Log(debug_str);
#endif
    }
    public static void LogError(string module, string debug_str, params object[] values)
    {
        debug_str = ConsoleApplyDecoration(ConsoleColorType.RED, ConsoleColorType.SILVER, module, debug_str, values);
        GameController.Instance.UI_CONSOLE_Log(debug_str);

#if UNITY_EDITOR
        Debug.Log(debug_str);
#endif
    }
    public static void Log(string module, string debug_str, params object[] values)
    {
        debug_str = ConsoleApplyDecoration(ConsoleColorType.TEAL, ConsoleColorType.SILVER, module, debug_str, values);
        GameController.Instance.UI_CONSOLE_Log(debug_str);

#if UNITY_EDITOR
        Debug.Log(debug_str);
#endif
    }
    static string ConsoleApplyDecoration(ConsoleColorType moduleColor, ConsoleColorType debugColor, string module, string debug_str, params object[] values)
    {
        if (values != null && values.Length > 0) {
            for (int i = 0; i < values.Length; ++i) {
                values[i] = ConsoleColor(ConsoleBold(values[i].ToString()), ConsoleColorType.WHITE);
            }
            debug_str = string.Format(debug_str, values);
        }
        return string.Format(CONSOLE_FORMAT_MODULE_DEBUG, ConsoleColor(module, moduleColor), ConsoleColor(debug_str, debugColor));
    }
    const int CONSOLE_MAX_ITEMS = 10;
    void UI_CONSOLE_Log(string txt)
    {
        var item = Instantiate(ui_console_content_tr.GetChild(0).gameObject);
        item.transform.SetParent(ui_console_content_tr, false);
        item.transform.SetAsLastSibling();
        item.GetComponent<Text>().text = txt;
        item.SetActive(true);

        if (ui_console_content_tr.childCount > CONSOLE_MAX_ITEMS) {
            GameObject.Destroy(ui_console_content_tr.GetChild(1).gameObject);
        }

        ui_console_scroll.verticalNormalizedPosition = 0.2f;
        ui_console_scroll_tween.Restart(0.4f);
        ui_console_body_go.SetActive(true);
        if (!ui_console_expanded) {
            ui_console_timer.Reset();
        }
    }
    void _UI_CONSOLE_OnButton()
    {
        if (ui_console_expanded) {
            //collapse
            ui_console_body_rtr.SetHeight(100f);
            //timer
            ui_console_timer.Reset();
        } else {
            //show
            ui_console_body_go.SetActive(true);
            //expand
            ui_console_body_rtr.SetHeight(1000f);
            //timer
            ui_console_timer.SetEnabled(false);
        }
        //tween
        ui_console_scroll.verticalNormalizedPosition = 0.2f;
        ui_console_scroll_tween.Restart(0.4f);

        //state
        ui_console_expanded = !ui_console_expanded;
    }
    void _UI_CONSOLE_OnBeginDrag(BaseEventData data)
    {
        if (ui_console_timer.IsEnabled()) {
            ui_console_timer.Reset();
        }
        ui_console_scroll_tween.SetEnabled(false);
    }
    void _UI_CONSOLE_OnTimer()
    {
        ui_console_body_go.SetActive(false);
    }
    float _UI_CONSOLE_ScrollPosGetter()
    {
        return ui_console_scroll.verticalNormalizedPosition;
    }
    void _UI_CONSOLE_ScrollPosSetter(float value)
    {
        ui_console_scroll.verticalNormalizedPosition = value;
    }
    #endregion //[Console]
#endif

    //Note UI
    #region [NOTE]
    public void Notify(NoteInfo note)
    {
        if (ui_note_active_go == null) {
            _UI_NOTE_Show(note);
        } else {
            if (!ui_note_msg_list.Contains(note)) { ui_note_msg_list.Add(note); }
        }
    }
    void _UI_NOTE_OnTimer()
    {
        if (ui_note_active_info.onComplete != null) { InvokeOnMT(ui_note_active_info.onComplete); }
        //hide last active
        if (ui_note_active_go != null) {
            ui_note_active_go.SetActive(false);
            ui_note_active_go = null;
        }
        if (ui_note_msg_list.Count > 0) {
            _UI_NOTE_Show(ui_note_msg_list[0]);
            ui_note_msg_list.RemoveAt(0);
        }
    }
    void _UI_NOTE_Show(NoteInfo note)
    {
        if (note.custom_ui != null) {
            Transform note_tr = note.custom_ui.transform;
            if (note_tr.parent != ui_note_root_tr) {
                note_tr.SetParent(ui_note_root_tr, false);
            }
            (ui_note_active_go = note.custom_ui).SetActive(true);
        } else {
            ui_note_msg_txt.text = note.text;
            if (note.icon != null) {
                ui_note_msg_icon_img.sprite = note.icon;
                ui_note_msg_icon_go.SetActive(true);
            } else {
                ui_note_msg_icon_go.SetActive(false);
            }
            (ui_note_active_go = ui_note_msg_go).SetActive(true);
        }

        ui_note_active_info = note;

        //AUDIO
        AUD_UI_Sound(note.sound_type);

        //animation
        ui_note_root_tr.GetComponent<Animation>().Play();
        ui_note_timer.Reset();
    }
    #endregion //[NOTE]

    //Begin UI
    #region [BEGIN PAGE]
    void UI_BEGIN_PageTo(UiBeginPage page)
    {
        if (ui_begin_page == page) return;
        ui_begin_last_page = ui_begin_page;
        ui_begin_page = page;
        if (ui_begin_last_page == UiBeginPage.HIDDEN) {
            //show
            _UI_BEGIN_ShowPage();
        } else {
            //hide or switch
            _UI_BEGIN_HidePage();
        }
    }
    void _UI_BEGIN_ShowPage()
    {
        //enable page
        _UI_BEGIN_SetPageEnabled(ui_begin_page, true);

        //show menu
        UI_MENU_SwitchTo(UiMenuPage.BEGIN);

        //set interactable = true
    }
    void _UI_BEGIN_HidePage()
    {
        //set interactable = false
        //disable last page
        _UI_BEGIN_SetPageEnabled(ui_begin_last_page, false);
        if (ui_begin_page != UiBeginPage.HIDDEN) {
            _UI_BEGIN_ShowPage();
        } else {
            //continue with
            _UI_STATE_OnHideComplete();
        }
    }
    void _UI_BEGIN_SetPageEnabled(UiBeginPage page, bool enabled)
    {
        switch (page) {
        case UiBeginPage.MAIN:
            ui_begin_root_go.SetActive(enabled);
            //Tutorial is disabled
            /*if (enabled) {
                ui_begin_joybtn_text_go.SetActive(config_data != null ? config_data.show_tut : false);
            }*/
            break;
        }
    }
    #endregion //[BEGIN Page]
    #region [BEGIN]
    void UI_BEGIN_SetInputEnabled(bool enabled)
    {
        ui_begin_joybtn_go.SetActive(enabled);
        UI_MENU_SetInteractable(enabled);
    }
    void _UI_BEGIN_OnJoystickBtn()
    {
        AUD_UI_Sound(UiSoundType.JOYSTICK);

        //Tutorial is disabled
        GAME_SwitchTo(GameState.PLAYING);
        //GAME_SwitchTo((config_data != null && config_data.show_tut) ? GameState.TUTORIAL : GameState.PLAYING);
    }
    void _UI_BEGIN_OnLangButton()
    {
        AUD_UI_Sound(UiSoundType.BUTTON);

        LOCALIZE_NextLanguage();
    }
    #endregion //[BEGIN]

    //Playing UI
    #region [PLAYING]
    #region [PLAYING PAGE]
    void UI_PLAYING_PageTo(UiPlayingPage page)
    {
        if (ui_playing_page == page) return;
        ui_playing_last_page = ui_playing_page;
        ui_playing_page = page;
        if (ui_playing_last_page == UiPlayingPage.HIDDEN) {
            //show
            _UI_PLAYING_ShowPage();
        } else {
            //hide root
            ui_playing_root_go.SetActive(false);
            //hide or switch
            _UI_PLAYING_HidePage();
        }
    }
    void _UI_PLAYING_SetPageEnabled(UiPlayingPage page, bool enabled)
    {
        switch (page) {
        case UiPlayingPage.MAIN:
            ui_playing_main_root_go.SetActive(enabled);
            EnableUpdateOnMT_Playing(_UI_PLAYING_MAIN_OnUpdate, enabled);
            if (enabled) {
                _UI_PLAYING_MAIN_UpdateWholePage();
            }
            break;
        case UiPlayingPage.CRASHCUT: ui_playing_crash_root_go.SetActive(enabled); break;
        case UiPlayingPage.CUTSCENE: ui_playing_cutscene_root_go.SetActive(enabled); break;
        case UiPlayingPage.RESTCUT: ui_playing_rest_root_go.SetActive(enabled); break;
        case UiPlayingPage.PAUSE:
            ui_playing_pause_root_go.SetActive(enabled);
            if (enabled) { _UI_PLAYING_PAUSE_TOP_UpdateUi(); }
#if UNITY_ANDROID
            EnableUpdateOnMT(_UI_HardwareBtnBackUpdate, enabled);
#endif
            break;
        }
    }
    void _UI_PLAYING_ShowPage()
    {
        //check root active
        if (!ui_playing_root_go.activeSelf) { ui_playing_root_go.SetActive(true); }

        //enable page
        _UI_PLAYING_SetPageEnabled(ui_playing_page, true);
        //set interactable = true

        //switch menu
        UI_MENU_SwitchTo((ui_playing_page == UiPlayingPage.PAUSE) ? UiMenuPage.PLAYING_PAUSE : UiMenuPage.HIDDEN);

        //continue with
    }
    void _UI_PLAYING_HidePage()
    {
        //set interactable = false
        //disable last page
        _UI_PLAYING_SetPageEnabled(ui_playing_last_page, false);
        if (ui_playing_page != UiPlayingPage.HIDDEN) {
            _UI_PLAYING_ShowPage();
        } else {
            //hide root
            ui_playing_root_go.SetActive(false);
            //continue with
            _UI_STATE_OnHideComplete();
        }
    }
    #endregion //[PLAYING PAGE]
    void _UI_PLAYING_MAIN_OnUpdate()
    {
        ui_playing_main_score_value_txt.text = ((int)pch_playing_score).ToString();
    }
    void UI_OnPlaycharReady()
    {
        onCoinCollected += UI_PLAYING_OnCoinCollected;
        playchar_ctr.onStaminaChanged += UI_PLAYING_OnPlaycharStaminaChanged;
        playchar_ctr.onRestStateChanged += UI_PLAYING_OnPlaycharRestStateChanged;
        chaser_ctr.onChaseStateChanged += UI_PLAYING_OnChaseStateChanged;

#if CODEDEBUG
        //devgui
        if (ui_playchar_devgui_go != null) {
            ui_playchar_devgui_controls_go = ui_playchar_devgui_go.transform.FindChild("Controls").gameObject;

            //luck
            Transform ui_luck_tr = ui_playchar_devgui_controls_go.transform.Find("Luck/Controls");
            ui_playchar_devgui_luck_txt = ui_luck_tr.Find("Value/Text").GetComponent<Text>();
            ui_luck_tr.FindChild("IncreaseBtn").GetComponent<Button>().onClick.AddListener(() => {
                pch_luck_total = System.Math.Min(pch_luck_total + 0.2f, 1f);
                ui_playchar_devgui_luck_txt.text = pch_luck_total.ToString("n2");
            });
            ui_luck_tr.FindChild("DecreaseBtn").GetComponent<Button>().onClick.AddListener(() => {
                pch_luck_total = System.Math.Max(pch_luck_total - 0.2f, 0f);
                ui_playchar_devgui_luck_txt.text = pch_luck_total.ToString("n2");
            });

            //expand
            ui_playchar_devgui_go.transform.FindChild("ExpandBtn").GetComponent<Button>().onClick.AddListener(() => ui_playchar_devgui_controls_go.SetActive(!ui_playchar_devgui_controls_go.activeSelf));
        }
#endif
    }
    void UI_OnPlaycharReleasing()
    {
        onCoinCollected -= UI_PLAYING_OnCoinCollected;
        playchar_ctr.onStaminaChanged -= UI_PLAYING_OnPlaycharStaminaChanged;
        playchar_ctr.onRestStateChanged -= UI_PLAYING_OnPlaycharRestStateChanged;
        chaser_ctr.onChaseStateChanged -= UI_PLAYING_OnChaseStateChanged;

#if CODEDEBUG
        //devgui
        ui_playchar_devgui_controls_go = null;
        ui_playchar_devgui_luck_txt = null;
#endif
    }
    void UI_OnGameStateChanged()
    {
        switch (game_state) {
        case GameState.PLAYING:
            //PLAYING UI
            bool activate_scorex = us.score_x > 0;
            bool activate_coinsx = us.coins_x > 0;
            bool activate_luckx = us.luck_x > 0;
            ui_playing_main_startitems_scorexholder_go.SetActive(activate_scorex);
            ui_playing_main_startitems_scorex_go.SetActive(true);
            ui_playing_main_startitems_coinsxholder_go.SetActive(activate_coinsx);
            ui_playing_main_startitems_coinsx_go.SetActive(true);
            ui_playing_main_startitems_luckxholder_go.SetActive(activate_luckx);
            ui_playing_main_startitems_luckx_go.SetActive(true);
            ui_playing_main_startitems_root_go.SetActive(activate_scorex || activate_coinsx || activate_luckx);
            break;
        }
    }
    void UI_OnPlayingStateChanged()
    {
        switch (playing_state) {
        case GamePlayingState.MAIN:
            //PLAYING UI
            if (ui_playing_main_startitems_root_go.activeSelf) {
                ui_timer.Complete(true);
                ui_timer.SetOnCompleteOnce(UI_PLAYING_MAIN_StartItems_OnTimeComplete);
                ui_timer.Reset(STARTITEM_ACTIVE_TIME);
            }
            break;
        }
    }
    void UI_PLAYING_MAIN_StartItems_OnTimeComplete()
    {
        if (!active_startitem_coinsx) { ui_playing_main_startitems_coinsx_go.SetActive(false); }
        if (!active_startitem_scorex) { ui_playing_main_startitems_scorex_go.SetActive(false); }
        if (!active_startitem_luckx) { ui_playing_main_startitems_luckx_go.SetActive(false); }
        //share ui_timer with hidden pause button
        if (!ui_playing_main_pausebtn_go.activeSelf) { ui_playing_main_pausebtn_go.SetActive(true); }
    }
    void _UI_PLAYING_PAUSE_TOP_UpdateUi()
    {
        ui_playing_pause_top_coins_value_txt.text = pch_coins_b_collected.ToString();
        ui_playing_pause_top_luck_value_txt.text = us.luck.ToString();
    }
    void UI_PLAYING_OnCoinCollected(CoinSharedData data)
    {
        ui_playing_main_coins_value_txt.text = pch_coins_nb_collected.ToString();
    }
    void UI_PLAYING_OnPlaycharStaminaChanged()
    {
        int stamina_value = playchar_ctr.CurrentStamina();
        //icon blink animation
        if (stamina_value <= 0) {
            ui_playing_main_stamina_icon_go.GetComponent<Animation>().Play();
        } else {
            ui_playing_main_stamina_icon_go.GetComponent<Animation>().SampleAt(0);
        }

        ui_playing_main_stamina_tween.SetValues(ui_playing_main_stamina_rect.GetWidth(), System.Math.Min(stamina_value, PlayerController.STAMINA_MAX_VALUE) * ui_playing_main_stamina_rect_step_width);
        ui_playing_main_stamina_tween.Restart(0.5f);
    }
    void UI_PLAYING_OnPlaycharRestStateChanged()
    {
        ui_playing_main_restbtn_go.SetActive(playchar_ctr.PlayerCanRest());
    }
    void UI_PLAYING_OnChaseStateChanged(ChaseState state)
    {
        bool chaser_on_screen = chaser_ctr.IsOnScreen();
        //ui_playing_main_dropbtn_go.SetActive(chaser_on_screen);
        ui_playing_main_drops_go.SetActive(chaser_on_screen);
        ui_playing_main_drops_value_txt.text = us.drops[selected_playchar_slot_index].ToString();
    }
    //Main
    void UI_PLAYING_MAIN_OnScorexMultChanged()
    {
        bool show = active_scorex_mult > 1;
        ui_playing_main_scorex_mult_go.SetActive(show);
        if (show) {
            ui_playing_main_scorex_mult_go.GetComponent<Animation>().Play();
            ui_playing_main_scorex_mult_value_txt.text = active_scorex_mult.ToString();
        }
    }
    void UI_PLAYING_MAIN_OnCoinsxMultChanged()
    {
        bool show = active_coinsx_mult > 1;
        ui_playing_main_coinsx_mult_go.SetActive(show);
        if (show) {
            ui_playing_main_coinsx_mult_go.GetComponent<Animation>().Play();
            ui_playing_main_coinsx_mult_value_txt.text = active_coinsx_mult.ToString();
        }
    }
    void _UI_PLAYING_MAIN_UpdateWholePage()
    {
        //score
        ui_playing_main_score_value_txt.text = string.Empty;
        //coins
        ui_playing_main_coins_value_txt.text = pch_coins_nb_collected.ToString();

#if CODEDEBUG
        if (playchar_ctr == null) {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            LogError(METHOD_NAME, "playchar is NULL");
            return;
        }
        if(chaser_ctr == null) {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            LogError(METHOD_NAME, "chaser is NULL");
            return;
        }
#endif
        //pause btn
        ui_playing_main_pausebtn_go.SetActive(false);
        //if startitems are displayed ui_timer is used to hide them
        if (!ui_playing_main_startitems_root_go.activeSelf) {
            ui_timer.Complete(true);
            ui_timer.SetOnCompleteOnce(() => ui_playing_main_pausebtn_go.SetActive(true));
            ui_timer.Reset(PAUSE_HIDDEN_TIME);
        }
        //rest btn
        ui_playing_main_restbtn_go.SetActive(playchar_ctr.PlayerCanRest());
        //drops
        //ui_playing_main_dropbtn_go.SetActive(chaser_ctr.IsOnScreen());
        ui_playing_main_drops_go.SetActive(chaser_ctr.IsOnScreen());
        ui_playing_main_drops_img.sprite = SelectedPlaycharSlot().ui_drop_desc.icon;

        //mult
        UI_PLAYING_MAIN_OnScorexMultChanged();
        UI_PLAYING_MAIN_OnCoinsxMultChanged();

        UI_PLAYING_OnPlaycharStaminaChanged();
    }
    void _UI_PLAYING_MAIN_OnPauserBtn()
    {
        AUD_UI_Sound(UiSoundType.BUTTON);
        GAME_PLAYING_SetPause(true);
    }
    void _UI_PLAYING_MAIN_OnRestBtn()
    {
        AUD_UI_Sound(UiSoundType.BUTTON);
        GAME_PLAYING_ShowCutscene(CutsceneState.REST);
    }
    //Pause
    void _UI_PLAYING_PAUSE_OnResumeBtn()
    {
        AUD_UI_Sound(UiSoundType.JOYSTICK);
        GAME_PLAYING_SetPause(false);
    }
    #region [PLAYING CRASH]
    void _UI_PLAYING_CRASH_OnScreenBtn()
    {
        //Complete time waiter to continue UI_PLAYING_CRASH_ShowUi
        ui_time_waiter.Complete(true);
        GAME_PLAYING_CutsceneComplete();
    }
    void _UI_PLAYING_CRASH_OnContinueBtn()
    {
        if (USER_BuyForLuck(COINTINUE_LUCK_PAY_VALUE)) {
            pch_luck_collected -= COINTINUE_LUCK_PAY_VALUE;
            //Complete time waiter to continue UI_PLAYING_CRASH_ShowUi
            ui_time_waiter.Complete(true);
            AUD_UI_Sound(UiSoundType.BUY);
            GAME_PLAYING_CutsceneContinue();
        } else {
            AUD_UI_Sound(UiSoundType.BUTTON);
        }

        //Test No luck payment
        /*ui_time_waiter.Complete(true);
        AUD_UI_Sound(UiSoundType.BUY);
        GAME_PLAYING_CutsceneContinue();*/
    }
    IEnumerator UI_PLAYING_CRASH_ShowUi()
    {
#if DBG_TRACE_ROUTINES
        Log("Routine", "==>>: {0}", "CRASH_ShowUi");
#endif
        ui_playing_crash_screen_btn.interactable = false;
        ui_playing_crash_continuebtn_go.SetActive(false);
        ui_playing_crash_watchbtn_go.SetActive(false);

        ui_time_waiter.ResetBreak();
        yield return ui_time_waiter.Reset(0.5f);

        ui_playing_crash_screen_btn.interactable = true;

        if (us.luck >= COINTINUE_LUCK_PAY_VALUE) {
            ui_playing_crash_continuebtn_go.SetActive(true);
        }
        //check if allowed to watch
        //check if ready to show
        if (AD_IsReadyContinue()) {
            ui_playing_crash_watchbtn_go.SetActive(true);
        }

        //WAIT
        yield return ui_time_waiter.Reset(3f);

        ui_playing_crash_continuebtn_go.SetActive(false);
        ui_playing_crash_watchbtn_go.SetActive(false);
#if DBG_TRACE_ROUTINES
        Log("Routine", "<<==: {0}", "CRASH_ShowUi");
#endif
    }
    IEnumerator UI_PLAYING_CRASHCONTINUE_ShowUi()
    {
#if DBG_TRACE_ROUTINES
        Log("Routine", "==>>: {0}", "CRASHCONTINUE_ShowUi");
#endif
        //show stamina animation
        ui_playing_main_staminacrash_tr.gameObject.SetActive(true);
        ui_playing_main_staminacrash_tr.GetComponent<AnimationController>().PlaySequence(0);

        //stamina
        ui_playing_main_staminacrash_tween.Restart();
        while (ui_playing_main_staminacrash_tween.IsEnabled()) {
            yield return null;
        }

        //hide stamina animation
        ui_playing_main_staminacrash_tr.GetComponent<AnimationController>().ProceedState();

        ui_time_waiter.ResetBreak();
        yield return ui_time_waiter.Reset(2f);

        ui_playing_main_staminacrash_tr.gameObject.SetActive(false);

#if DBG_TRACE_ROUTINES
        Log("Routine", "<<==: {0}", "CRASHCONTINUE_ShowUi");
#endif
    }
    #endregion //[PLAYING CRASH]
    #region [PLAYING REST]
    IEnumerator UI_PLAYING_REST_ShowResults()
    {
        //capture
        ui_nbcoins_value = pch_coins_nb_collected;
        ui_coins_value = pch_coins_b_collected;

        //prepare
        ui_playing_rest_coins_go.SetActive(true);
        ui_playing_rest_nbcoins_go.SetActive(false);
        ui_playing_rest_coins_value_txt.text = ui_coins_value.ToString();

        //animation
        ui_playing_rest_stamina_tr.GetComponent<Animation>().SampleAt(0);
        var anim_coins = ui_playing_rest_coins_go.GetComponent<AnimationController>();
        anim_coins.PlaySequence(0);

        //stamina
        ui_playing_rest_stamina_tween.Restart();

        //screenbtn
        ui_playing_rest_screenbtn.interactable = false;

        ui_time_waiter.ResetBreak();
        yield return ui_time_waiter.Reset(0.5f);

        if (ui_nbcoins_value == 0) goto Part4;

        // PART 2 ----------------------------------

        //show nbcoins
        ui_playing_rest_nbcoins_go.SetActive(true);

        //animation
        var anim_nb_coins = ui_playing_rest_nbcoins_go.GetComponent<AnimationController>();
        anim_nb_coins.PlaySequence(0);

        if (ui_nbcoins_value > 0) {
            //tween_rev
            ui_value_rev_tween.CompleteAndClear(true);
            ui_value_rev_tween.SetValues(ui_nbcoins_value, 0f);
            ui_value_rev_tween.SetSetter((value) => ui_playing_rest_nbcoins_value_txt.text = ((int)value).ToString());
            ui_value_rev_tween.Restart(0.2f, 1f);

            //tween
            ui_value_tween.CompleteAndClear(true);
            ui_value_tween.SetValues(ui_coins_value, ui_coins_value + ui_nbcoins_value);
            ui_value_tween.SetSetter((value) => ui_playing_rest_coins_value_txt.text = ((int)value).ToString());
            ui_value_tween.Restart(0.2f, 1f);
        }

        while (ui_value_tween.IsEnabled()) {
            yield return null;
        }

        // PART 3 ----------------------------------

        //hide nbcoins animation
        ui_playing_rest_nbcoins_go.GetComponent<AnimationController>().ProceedState();

        yield return ui_time_waiter.Reset(0.4f);

        // PART 4 ----------------------------------
        Part4:

        //hide coins animation
        ui_playing_rest_coins_go.GetComponent<AnimationController>().ProceedState();

        yield return ui_time_waiter.Reset(0.4f);

        //hide stamina animation
        ui_playing_rest_stamina_tr.GetComponent<Animation>().Play();

        yield return ui_time_waiter.Reset(0.4f);

        // PART 5 ----------------------------------

        ui_playing_rest_coins_go.SetActive(false);
        ui_playing_rest_nbcoins_go.SetActive(false);

        //screenbtn
        ui_playing_rest_screenbtn.interactable = true;
        ui_playing_rest_screenbtn.onClick.RemoveAllListeners();
        ui_playing_rest_screenbtn.onClick.AddListener(GAME_PLAYING_CutsceneComplete);
    }
    #endregion //[Playing Rest]
    #endregion //[PLAYING]

    //Shop UI
    #region [SHOP PAGE]
    void UI_SHOP_PageTo(UiShopPage page)
    {
        if (ui_shop_page == page) return;
        ui_shop_last_page = ui_shop_page;
        ui_shop_page = page;
        if (ui_shop_last_page == UiShopPage.HIDDEN) {
            //show
            _UI_SHOP_ShowPage();
        } else {
            //hide or switch
            _UI_SHOP_HidePage();
        }
    }
    void _UI_SHOP_ShowPage()
    {
        //show root
        ui_shop_root_go.SetActive(true);
        //_UI_SHOP_SetPageUpdateFunc();

        //enable page
        _UI_SHOP_SetPageEnabled(ui_shop_page, true);

        //show menu
        UI_MENU_SwitchTo(UiMenuPage.SHOP);

        //set interactable = true
    }
    void _UI_SHOP_HidePage()
    {
        //set interactable = false
        //disable last page
        _UI_SHOP_SetPageEnabled(ui_shop_last_page, false);
        if (ui_shop_page != UiShopPage.HIDDEN) {
            _UI_SHOP_ShowPage();
        } else {
            //hide root
            ui_shop_root_go.SetActive(false);
            //continue with
            _UI_STATE_OnHideComplete();
        }
    }
    void _UI_SHOP_SetPageEnabled(UiShopPage page, bool enabled)
    {
        if (enabled) {
            _UI_SHOP_TOP_UpdateUi();
        }
#if UNITY_ANDROID
        EnableUpdateOnMT(_UI_HardwareBtnBackUpdate, enabled);
#endif

        switch (page) {
        case UiShopPage.CHAR:
            ui_shop_char_root_go.SetActive(enabled);
            if (enabled) {
                _UI_SHOP_CHAR_SCROLL_OnElementSelected(selected_playchar_slot_index);
            }
            break;
        case UiShopPage.THEME:
            ui_shop_theme_root_go.SetActive(enabled);
            if (enabled) {
                _UI_SHOP_THEME_SCROLL_OnElementSelected(selected_theme_slot_index);
            }
            break;
        case UiShopPage.BUY:
            ui_shop_buy_root_go.SetActive(enabled);
            if (enabled) {
                _UI_SHOP_BUY_UpdateUi();
            }
            break;
        }
    }
    #endregion //[SHOP PAGE]
    #region [SHOP BUY]
    void _UI_SHOP_BUY_UpdateUi()
    {
        //Items
        {
            if (ui_buy_items == null) { ui_buy_items = new UI_BuyItemElement[shop_data.items.Length]; }
            if (ui_buy_items.Length != shop_data.items.Length) {
                System.Array.Resize(ref ui_buy_items, shop_data.items.Length);
            }
            Transform items_group_name_sibling = ui_shop_buy_content_tr.Find("Items");
            int i = 0;
            for (int l = ui_buy_items.Length; i < l; ++i) {
                var ui = ui_buy_items[i];
                var data = shop_data.items[i];
                if (ui == null) {
                    ui_buy_items[i] = new UI_BuyItemElement(Instantiate(ui_so.shop_buyitem_element_prefab), ui_shop_buy_content_tr, items_group_name_sibling, i);
                    ui = ui_buy_items[i];

                    //fill static data
                    ui.SetYouhaveEnabled(data.type != UserInvItemType.MBOX && data.type != UserInvItemType.SMBOX);
                    _UI_SHOP_BUY_BuyItemAddListener(ui.buybtn, data.type);

                    //Collapse
                    ui.Collapse();
                }

                //fill data
                if (data.type == UserInvItemType.DROPS) {
                    var playchar_slot = SelectedPlaycharSlot();
                    ui.item_icon.sprite = playchar_slot.ui_drop_desc.icon;
                    ui.item_name_loc.SetTerm(playchar_slot.ui_drop_desc.name);
                    ui.item_desc_loc.SetTerm(playchar_slot.ui_drop_desc.desc);
                } else {
                    UserItemDesc idesc;
                    if (ui_so.user_item_desc.TryGetValue(data.type, out idesc)) {
                        ui.item_icon.sprite = idesc.icon;
                        ui.item_name_loc.SetTerm(idesc.name);
                        ui.item_desc_loc.SetTerm(idesc.desc);
                    }
#if CODEDEBUG
         else {
                            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
                            LogError(METHOD_NAME, string.Format("UserItemDesc for {0} not found"), data.type);
                        }
#endif
                }


                int cur_val = USER_NumItems(data.type);
                ui.youhave_value_txt.text = cur_val.ToString();
                ui.shopdata_item_index = _UI_SHOP_BUY_ShopdataItemIndexFor(cur_val, i);
                if (ui.shopdata_item_index >= 0) {
                    ui.SetBuyEnabled(true);
                    var item = data.items[ui.shopdata_item_index];
                    ui.SetPrice(item.price);
                    //ui.buybtn_value_txt.text = string.Format("+{0}", item.item_value);
                } else {
                    ui.SetBuyEnabled(false);
                }
            }

            //Daily Video
            if (ui_buy_vid == null) {
                ui_buy_vid = new UI_BuyDailyVideoElement(Instantiate(ui_so.shop_buyvid_element_prefab), ui_shop_buy_content_tr, items_group_name_sibling, i);
                ui_buy_vid.buybtn.onClick.RemoveAllListeners();
                ui_buy_vid.buybtn.onClick.AddListener(AD_OnWatchDaily);

                ui_buy_vid.SetPrice(new PriceData() { type = CurrencyType.LUCK, value = reward_data.daily_vid_luck });

                //Collapse
                ui_buy_vid.Collapse();
            }
            ui_buy_vid.SetActive(AD_IsReadyDaily());
        }

        //Levels
        if (ui_buy_levels == null) { ui_buy_levels = new UI_BuyLevelElement[shop_data.levels.Length]; }
        if (ui_buy_levels.Length != shop_data.levels.Length) {
            System.Array.Resize(ref ui_buy_levels, shop_data.levels.Length);
        }
        Transform levels_group_name_sibling = ui_shop_buy_content_tr.Find("Levels");
        levels_group_name_sibling.GetChild(0).GetComponent<Image>().sprite = SelectedPlaycharSlot().ui_playchar_icon;
        for (int i = 0, l = ui_buy_levels.Length; i < l; ++i) {
            var ui = ui_buy_levels[i];
            var data = shop_data.levels[i];
            if (ui == null) {
                ui_buy_levels[i] = new UI_BuyLevelElement(Instantiate(ui_so.shop_buylevel_element_prefab), ui_shop_buy_content_tr, levels_group_name_sibling, i);
                ui = ui_buy_levels[i];
                ui.SetNumLevels(PlaycharLevel.MaxLevelFor(data.type));

                _UI_SHOP_BUY_BuyLevelAddListener(ui.buybtn, data.type);

                //collapse
                ui.Collapse();
            }
            //fill data
            if (data.type == PlaycharLevelType.DROP_POWER) {
                var playchar_slot = SelectedPlaycharSlot();
                ui.item_icon.sprite = playchar_slot.ui_drop_level_desc.icon;
                ui.item_name_loc.SetTerm(playchar_slot.ui_drop_level_desc.name);
                ui.item_desc_loc.SetTerm(playchar_slot.ui_drop_level_desc.desc);
            } else {
                UserItemDesc idesc;
                if (ui_so.playchar_level_desc.TryGetValue(data.type, out idesc)) {
                    ui.item_icon.sprite = idesc.icon;
                    ui.item_name_loc.SetTerm(idesc.name);
                    ui.item_desc_loc.SetTerm(idesc.desc);
                }
#if CODEDEBUG
 else {
                    string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
                    LogError(METHOD_NAME, string.Format("UserItemDesc for {0} not found"), data.type);
                }
#endif
            }

            int cur_val = SelectedPlaycharLevel(data.type);
            ui.FillLevels(cur_val);
            ui.shopdata_item_index = _UI_SHOP_BUY_ShopdataLevelIndexFor(cur_val, i);
            if (ui.shopdata_item_index >= 0) {
                ui.SetBuyEnabled(true);
                ui.SetPrice(data.items[ui.shopdata_item_index].price);
            } else {
                ui.SetBuyEnabled(false);
            }
        }

        //Challenges
        if (ui_buy_chs == null) {
            ui_buy_chs = new UI_BuyChallengeElement[10];
            Transform chs_group_name_sibling = ui_shop_buy_content_tr.Find("Chs");
            for (int i = 0; i < ui_buy_chs.Length; ++i) {
                ui_buy_chs[i] = new UI_BuyChallengeElement(Instantiate(ui_so.shop_buychs_element_prefab), ui_shop_buy_content_tr, chs_group_name_sibling, i);
                ui_buy_chs[i].SetBuyEnabled(true);
                ui_buy_chs[i].SetPrice(shop_data.chs);
            }
        }
        int chs_cursor = 0;
        //Progress
        if (chprog_active != null) {
            for (int i = 0, l = chprog_active.NumTasks(); i < l; ++i) {
                if (chprog_active.IsTaskGreen(i)) continue;

                var el = ui_buy_chs[chs_cursor];
                el.ch_index_txt.text = (i + 1).ToString();
                if (us.chprog_random_index < 0) {
                    el.item_name_loc.SetTerm(ui_so.chs_prog_top_text, () => new object[] { us.chprog_prog_index + 1 });
                } else {
                    el.item_name_loc.SetTerm(ui_so.chs_rand_top_text);
                }
                el.item_desc_loc.SetTerm(chprog_active.TaskDesc(i));
                el.SetActive(true);
                el.Collapse();

                _UI_SHOP_BUY_BuyChsAddListener(el.buybtn, chprog_active, i);

                ++chs_cursor;
            }
        }
        //Daily
        /*if (chday_active != null) {
            for (int i = 0, l = chday_active.NumTasks(); i < l; ++i) {
                if (chday_active.IsTaskGreen(i)) continue;

                var el = ui_buy_chs[chs_cursor];
                el.ch_index_txt.text = (i + 1).ToString();
                el.item_name_txt.text = ui_so.chs_day_top_text;
                el.item_desc_txt.text = chday_active.TaskDesc(i);
                el.SetActive(true);
                el.Collapse();

                _UI_SHOP_BUY_BuyChsAddListener(el.buybtn, chday_active, i);

                ++chs_cursor;
            }
        }*/


        //disable remaining
        for (; chs_cursor < ui_buy_chs.Length; ++chs_cursor) {
            ui_buy_chs[chs_cursor].SetActive(false);
        }
    }
    void _UI_SHOP_BUY_BuyItemAddListener(Button b, UserInvItemType type)
    {
        b.onClick.RemoveAllListeners();
        b.onClick.AddListener(() => _UI_SHOP_BUY_OnItemBuy(type));
    }
    void _UI_SHOP_BUY_BuyLevelAddListener(Button b, PlaycharLevelType type)
    {
        b.onClick.RemoveAllListeners();
        b.onClick.AddListener(() => _UI_SHOP_BUY_OnLevelBuy(type));
    }
    void _UI_SHOP_BUY_BuyChsAddListener(Button b, Challenge ch, int task_index)
    {
        b.onClick.RemoveAllListeners();
        b.onClick.AddListener(() => _UI_SHOP_BUY_OnChsBuy(ch, task_index));
    }
    int _UI_SHOP_BUY_ShopdataItemIndexFor(int currentValue, int itemIndex)
    {
        //index for BuyDisable
        int index = -1;
        var data = shop_data.items[itemIndex];
        if (data.limit_threshold <= 0 || currentValue < data.limit_threshold) {
            if (currentValue >= data.items[data.items.Length - 1].item_threshold) {
                //current value reached last threshold
                index = data.items.Length - 1;
            } else {
                for (int k = 0, m = data.items.Length; k < m; ++k) {
                    if (currentValue < data.items[k].item_threshold) {
                        index = k;
                        break;
                    }
                }
            }
        }
        return index;
    }
    int _UI_SHOP_BUY_ShopdataLevelIndexFor(int currentValue, int itemIndex)
    {
        //index for BuyDisable
        int index = -1;
        var data = shop_data.levels[itemIndex];
        if (currentValue < PlaycharLevel.MaxLevelFor(data.type) && (data.limit_threshold <= 0 || currentValue < data.limit_threshold)) {
            if (currentValue >= data.items[data.items.Length - 1].item_threshold) {
                //current value reached last threshold
                index = data.items.Length - 1;
            } else {
                for (int k = 0, m = data.items.Length; k < m; ++k) {
                    if (currentValue < data.items[k].item_threshold) {
                        index = k;
                        break;
                    }
                }
            }
        }
        return index;
    }
    void _UI_SHOP_BUY_OnItemBuy(UserInvItemType type)
    {
        for (int i = 0, l = shop_data.items.Length; i < l; ++i) {
            var data = shop_data.items[i];
            if (data.type == type) {
                var item = data.items[ui_buy_items[i].shopdata_item_index];
                if (USER_Buy(item.price)) {
                    USER_AddItem(type, item.item_value);
                    AUD_UI_Sound(UiSoundType.BUY);
                    _UI_SHOP_TOP_UpdateUi();
                    if (onUserItemBuy != null) { onUserItemBuy(type, item.item_value); }
                } else {
                    //not enough
                }
                break;
            }
        }
        _UI_SHOP_BUY_UpdateUi();
    }
    void _UI_SHOP_BUY_OnLevelBuy(PlaycharLevelType type)
    {
        for (int i = 0, l = shop_data.levels.Length; i < l; ++i) {
            var data = shop_data.levels[i];
            if (data.type == type) {
                var item = data.items[ui_buy_levels[i].shopdata_item_index];
                if (USER_Buy(item.price)) {
                    USER_AddLevel(type);
                    AUD_UI_Sound(UiSoundType.BUY);
                    _UI_SHOP_TOP_UpdateUi();
                    if (onUserLevelBuy != null) { onUserLevelBuy(type); }
                } else {
                    //not enough
                }
                break;
            }
        }
        _UI_SHOP_BUY_UpdateUi();
    }
    void _UI_SHOP_BUY_OnChsBuy(Challenge ch, int task_index)
    {
        if (USER_Buy(shop_data.chs)) {
            ch.CompleteTask(task_index);
            AUD_UI_Sound(UiSoundType.BUY);
            _UI_SHOP_TOP_UpdateUi();
            _UI_SHOP_BUY_UpdateUi();
        } else {
            //not enough
        }
    }
    #endregion
    #region [SHOP CHAR]
    void _UI_SHOP_CHAR_UpdateUi()
    {
        if (ui_shop_char_slotindex < pch_so.playchar_slots.Length) {
            int playchar_index = PlaycharIndexAt(ui_shop_char_slotindex, ui_shop_char_indexinslot);
            Playchar playchar = pch_so.playchars[playchar_index];

            ui_shop_char_name_loc.SetTerm(playchar.ui_playchar_name);
            ui_shop_char_desc_loc.SetTerm(playchar.ui_playchar_desc);

            //update skin widget
            _UI_SHOP_CHAR_SKIN_UpdateUi();

            //show playchar 3d
            if (ui_shop_char_node_active_go == null || ui_shop_char_node_active_go != playchar.ui_playchar_go) {
                if (ui_shop_char_node_active_go != null) { ui_shop_char_node_active_go.SetActive(false); }
                if (playchar.ui_playchar_go == null) { playchar.ui_playchar_go = Instantiate(playchar.ui_playchar_prefab); }
                ui_shop_char_node_active_go = playchar.ui_playchar_go;
                ui_shop_char_node_active_go.SetActive(true);
            }
            //animation
            var char_anim = ui_shop_char_node_active_go.GetComponent<AnimationController>();
            if (char_anim.CurrentSequenceIndex() != UI_PLAYCHAR_ANIM_GREETING) {
                char_anim.PlaySequence(UI_PLAYCHAR_ANIM_GREETING);
            }
        } else {
            ui_shop_char_name_loc.SetTerm(ui_so.shop_char_question_name);
            ui_shop_char_desc_loc.SetTerm(ui_so.shop_char_question_desc);

            //show question 3d
            if (ui_shop_char_node_active_go == null || ui_shop_char_node_active_go != ui_shop_char_question_go) {
                if (ui_shop_char_node_active_go != null) { ui_shop_char_node_active_go.SetActive(false); }
                if (ui_shop_char_question_go == null) { ui_shop_char_question_go = Instantiate(ui_so.shop_char_question_prefab); }
                ui_shop_char_node_active_go = ui_shop_char_question_go;
                ui_shop_char_node_active_go.SetActive(true);
            }

            _UI_SHOP_CHAR_SKIN_SetNumItems(0);
        }

        if (ui_shop_char_node_active_go.transform.parent != ui_shop_char_node) {
            ui_shop_char_node_active_go.transform.SetParent(ui_shop_char_node, false);
        }

        //selected icon
        ui_shop_char_scroll_selitem_icon_tr.SetParent(ui_shop_char_scroll_content_tr.GetChild(selected_playchar_slot_index), false);

        //update confirm button
        _UI_SHOP_CHAR_CONFIRM_UpdateUi();
    }
    #region [CHAR SCROLL]
    int _UI_SHOP_SCROLL_FindNearPoint(float value, float[] points)
    {
        float distance = 1.0f;
        int index = 0;
        for (int i = 0, l = points.Length; i < l; ++i) {
            float test = 1.0f;
            if ((test = System.Math.Abs(points[i] - value)) < distance) {
                distance = test;
                index = i;
            }
        }
        return index;
    }
    void _UI_SHOP_SCROLL_OnBeginDrag(BaseEventData data)
    {
        //stop snap animation
        ui_shop_scroll_tween.SetEnabled(false);
    }
    void _UI_SHOP_CHAR_SCROLL_AddListener(Button b, int index)
    {
        b.onClick.RemoveAllListeners();
        b.onClick.AddListener(() => _UI_SHOP_CHAR_SCROLL_OnElementSelected(index));
    }
    void _UI_SHOP_CHAR_SCROLL_OnEndDrag(BaseEventData data)
    {
        //select playchar
        int index = _UI_SHOP_SCROLL_FindNearPoint(ui_shop_char_scroll.horizontalNormalizedPosition, ui_shop_char_scroll_points);
        _UI_SHOP_CHAR_SCROLL_OnElementSelected(index);
    }
    void _UI_SHOP_CHAR_SCROLL_OnElementSelected(int slot_index)
    {
        ui_shop_char_slotindex = slot_index;
        ui_shop_char_indexinslot = 0;
        if (selected_playchar_slot_index == ui_shop_char_slotindex) {
            //playchar already selected
            //show skin selection properly
            ui_shop_char_indexinslot = selected_playchar_index_in_slot;
        }

        //snap with animation
        float scroll_target_pos = ui_shop_char_scroll_points[slot_index];
        ui_shop_scroll_tween.SetBeginGetter(_UI_SHOP_CHAR_SCROLL_ScrollPosGetter, false);
        ui_shop_scroll_tween.SetSetter(_UI_SHOP_CHAR_SCROLL_ScrollPosSetter);
        ui_shop_scroll_tween.SetEndValue(scroll_target_pos);
        ui_shop_scroll_tween.Restart(0.5f);

        _UI_SHOP_CHAR_UpdateUi();
    }
    float _UI_SHOP_CHAR_SCROLL_ScrollPosGetter()
    {
        return ui_shop_char_scroll.horizontalNormalizedPosition;
    }
    void _UI_SHOP_CHAR_SCROLL_ScrollPosSetter(float value)
    {
        ui_shop_char_scroll.horizontalNormalizedPosition = value;
    }
    #endregion //[CHAR SCROLL]
    #region [CHAR CONFIRM]
    void _UI_SHOP_CHAR_CONFIRM_UpdateUi()
    {
        if (ui_shop_char_slotindex < pch_so.playchar_slots.Length) {
            int playchar_index = PlaycharIndexAt(ui_shop_char_slotindex, ui_shop_char_indexinslot);
            if (playchar_index == SelectedPlaycharIndex()) {
                //show active state
                _UI_SHOP_CHAR_CONFIRM_StateTo(UiShopConfirmState.ACTIVE);
            } else if (us.IsPlaycharAvailable(playchar_index)) {
                //show select state
                _UI_SHOP_CHAR_CONFIRM_StateTo(UiShopConfirmState.SELECT);
            } else {
                //show buy state
                _UI_SHOP_CHAR_CONFIRM_StateTo(UiShopConfirmState.BUY);
                //fill buy data
                _UI_SHOP_CHAR_CONFIRM_SetPrice(shop_data.playchars[playchar_index]);
            }
        } else {
            //not available
            _UI_SHOP_CHAR_CONFIRM_StateTo(UiShopConfirmState.NOT_AVAILABLE);
        }
    }
    void _UI_SHOP_CHAR_CONFIRM_OnSelectBtn()
    {
        //select playchar
        PLAYCHAR_Select(ui_shop_char_slotindex, ui_shop_char_indexinslot);
        AUD_UI_Sound(UiSoundType.BUTTON);
        //update confirm ui
        _UI_SHOP_CHAR_UpdateUi();
    }
    void _UI_SHOP_CHAR_CONFIRM_OnBuyBtn()
    {
        //buy playchar
        int playchar_index = PlaycharIndexAt(ui_shop_char_slotindex, ui_shop_char_indexinslot);
        if (USER_Buy(shop_data.playchars[playchar_index])) {
            us.SetPlaycharAvailable(playchar_index);
            AUD_UI_Sound(UiSoundType.BUY);
            _UI_SHOP_TOP_UpdateUi();
        }
        //update confirm ui
        _UI_SHOP_CHAR_UpdateUi();
    }
    void _UI_SHOP_CHAR_CONFIRM_SetPrice(PriceData price)
    {
        switch (price.type) {
        case CurrencyType.LUCK:
            ui_shop_char_confirm_price_coins_go.SetActive(false);
            ui_shop_char_confirm_price_luck_go.SetActive(true);
            ui_shop_char_confirm_price_luck_value_txt.text = price.value.ToString();
            break;
        case CurrencyType.COINS:
            ui_shop_char_confirm_price_luck_go.SetActive(false);
            ui_shop_char_confirm_price_coins_go.SetActive(true);
            ui_shop_char_confirm_price_coins_value_txt.text = price.value.ToString();
            break;
        }
    }
    void _UI_SHOP_CHAR_CONFIRM_StateTo(UiShopConfirmState state)
    {
        if (ui_shop_char_confirm_state == state) return;
        ui_shop_char_confirm_last_state = ui_shop_char_confirm_state;
        ui_shop_char_confirm_state = state;
        _UI_SHOP_CHAR_CONFIRM_Hide();
    }
    void _UI_SHOP_CHAR_CONFIRM_Hide()
    {
        //set interactable = false
        //start hide animation
        //set animation state
        //continue with
        _UI_SHOP_CHAR_CONFIRM_OnHideComplete();
    }
    void _UI_SHOP_CHAR_CONFIRM_OnHideComplete()
    {
        //set animation state
        //disable last state
        _UI_SHOP_CHAR_CONFIRM_SetEnabled(ui_shop_char_confirm_last_state, false);
        //show target state
        _UI_SHOP_CHAR_CONFIRM_Show();
    }
    void _UI_SHOP_CHAR_CONFIRM_Show()
    {
        //enable state
        _UI_SHOP_CHAR_CONFIRM_SetEnabled(ui_shop_char_confirm_state, true);
        //start show animation
        //set animation state
        //continue with
        _UI_SHOP_CHAR_CONFIRM_OnShowComplete();
    }
    void _UI_SHOP_CHAR_CONFIRM_OnShowComplete()
    {
        //set animation state
        //set interactable = true
    }
    void _UI_SHOP_CHAR_CONFIRM_SetEnabled(UiShopConfirmState state, bool enabled)
    {
        switch (state) {
        case UiShopConfirmState.ACTIVE:
            ui_shop_char_confirm_price_go.SetActive(false);
            ui_shop_char_confirm_active_go.SetActive(enabled);
            break;
        case UiShopConfirmState.SELECT:
            ui_shop_char_confirm_price_go.SetActive(false);
            ui_shop_char_confirm_select_go.SetActive(enabled);
            break;
        case UiShopConfirmState.BUY:
            ui_shop_char_confirm_price_go.SetActive(true);
            ui_shop_char_confirm_buy_go.SetActive(enabled);
            break;
        case UiShopConfirmState.NOT_AVAILABLE:
            ui_shop_char_confirm_price_go.SetActive(false);
            ui_shop_char_confirm_na_go.SetActive(enabled);
            break;
        }
    }
    #endregion
    #region [CHAR SKIN]
    void _UI_SHOP_CHAR_SKIN_UpdateUi()
    {
        int num_skins = NumPlaycharsInSlot(ui_shop_char_slotindex);
        _UI_SHOP_CHAR_SKIN_SetNumItems(num_skins > 1 ? num_skins : 0);
        _UI_SHOP_CHAR_SKIN_SetItemActive(ui_shop_char_indexinslot);
    }
    void _UI_SHOP_CHAR_SKIN_OnItemSelected(int index)
    {
        ui_shop_char_indexinslot = index;

        _UI_SHOP_CHAR_UpdateUi();
    }
    void _UI_SHOP_CHAR_SKIN_SetNumItems(int num)
    {
        foreach (Transform child in ui_shop_char_skin_go.transform) {
            if (num > 0) { child.gameObject.SetActive(true); --num; } else { child.gameObject.SetActive(false); }
        }
    }
    void _UI_SHOP_CHAR_SKIN_ItemAddListener(Button b, int index)
    {
        b.onClick.RemoveAllListeners();
        b.onClick.AddListener(() => _UI_SHOP_CHAR_SKIN_OnItemSelected(index));
    }
    void _UI_SHOP_CHAR_SKIN_SetItemActive(int index)
    {
        //if (index == ui_shop_char_skin_activeindex) return;

        //hide animation
        //ui_shop_char_skin_go.transform.GetChild(ui_shop_char_skin_activeindex).GetChild(0).animation.Play("UICharSkinItemHide");
        //use temporary
        ui_shop_char_skin_go.transform.GetChild(ui_shop_char_skin_activeindex).GetComponent<Image>().sprite = ui_so.shop_level_icon_empty;

        //set new index
        ui_shop_char_skin_activeindex = index;

        //show animation
        //ui_shop_char_skin_go.transform.GetChild(ui_shop_char_skin_activeindex).GetChild(0).animation.Play("UICharSkinItemShow");
        //use temporary
        ui_shop_char_skin_go.transform.GetChild(ui_shop_char_skin_activeindex).GetComponent<Image>().sprite = ui_so.shop_level_icon_full;
    }
    #endregion //[CHAR SKIN]
    #endregion //[SHOP CHAR]
    #region [SHOP THEME]
    void _UI_SHOP_THEME_UpdateUi()
    {
        if (ui_shop_themeslot_index < thm_so.theme_slots.Length) {
            ThemeSlot theme_slot = thm_so.theme_slots[ui_shop_themeslot_index];

            ui_shop_theme_name_loc.SetTerm(theme_slot.ui_themeslot_name);
            ui_shop_theme_desc_loc.SetTerm(theme_slot.ui_themeslot_desc);

            //show theme 3d
            if (ui_shop_theme_node_active_go == null || ui_shop_theme_node_active_go != theme_slot.ui_theme_go) {
                if (ui_shop_theme_node_active_go != null) { ui_shop_theme_node_active_go.SetActive(false); }
                if (theme_slot.ui_theme_go == null) { theme_slot.ui_theme_go = Instantiate(theme_slot.ui_theme_prefab); }
                ui_shop_theme_node_active_go = theme_slot.ui_theme_go;
                ui_shop_theme_node_active_go.SetActive(true);
            }
        } else {
            //question
            ui_shop_theme_name_loc.SetTerm(ui_so.shop_theme_question_name);
            ui_shop_theme_desc_loc.SetTerm(ui_so.shop_theme_question_desc);

            //show theme 3d
            if (ui_shop_theme_node_active_go == null || ui_shop_theme_node_active_go != ui_shop_theme_question_go) {
                if (ui_shop_theme_node_active_go != null) { ui_shop_theme_node_active_go.SetActive(false); }
                if (ui_shop_theme_question_go == null) { ui_shop_theme_question_go = Instantiate(ui_so.shop_theme_question_prefab); }
                ui_shop_theme_node_active_go = ui_shop_theme_question_go;
                ui_shop_theme_node_active_go.SetActive(true);
            }
        }

        if (ui_shop_theme_node_active_go.transform.parent != ui_shop_theme_node) {
            ui_shop_theme_node_active_go.transform.SetParent(ui_shop_theme_node, false);
        }

        //selected icon
        ui_shop_theme_scroll_selitem_icon_tr.SetParent(ui_shop_theme_scroll_content_tr.GetChild(selected_theme_slot_index), false);

        //update confirm button
        _UI_SHOP_THEME_CONFIRM_UpdateUi();
    }
    #region [THEME CONFIRM]
    void _UI_SHOP_THEME_CONFIRM_UpdateUi()
    {
        if (ui_shop_themeslot_index < thm_so.theme_slots.Length) {
            if (ui_shop_themeslot_index == selected_theme_slot_index) {
                //show active state
                _UI_SHOP_THEME_CONFIRM_StateTo(UiShopConfirmState.ACTIVE);
            } else if (us.IsThemeSlotAvailable(ui_shop_themeslot_index)) {
                //show select state
                _UI_SHOP_THEME_CONFIRM_StateTo(UiShopConfirmState.SELECT);
            } else {
                //show buy state
                _UI_SHOP_THEME_CONFIRM_StateTo(UiShopConfirmState.BUY);
                //fill buy data
                _UI_SHOP_THEME_CONFIRM_SetPrice(shop_data.themes[ui_shop_themeslot_index]);
            }
        } else {
            //not available
            _UI_SHOP_THEME_CONFIRM_StateTo(UiShopConfirmState.NOT_AVAILABLE);
        }
    }
    void _UI_SHOP_THEME_CONFIRM_OnSelectBtn()
    {
        //select theme
        selected_theme_slot_index = ui_shop_themeslot_index;
        AUD_UI_Sound(UiSoundType.BUTTON);
        //update confirm ui
        _UI_SHOP_THEME_UpdateUi();
    }
    void _UI_SHOP_THEME_CONFIRM_OnBuyBtn()
    {
        if (USER_Buy(shop_data.themes[ui_shop_themeslot_index])) {
            us.SetThemeSlotAvailable(ui_shop_themeslot_index);
            AUD_UI_Sound(UiSoundType.BUY);
            _UI_SHOP_TOP_UpdateUi();
        }
        //update confirm ui
        _UI_SHOP_THEME_UpdateUi();
    }
    void _UI_SHOP_THEME_CONFIRM_SetPrice(PriceData price)
    {
        switch (price.type) {
        case CurrencyType.LUCK:
            ui_shop_theme_confirm_price_coins_go.SetActive(false);
            ui_shop_theme_confirm_price_luck_go.SetActive(true);
            ui_shop_theme_confirm_price_luck_value_txt.text = price.value.ToString();
            break;
        case CurrencyType.COINS:
            ui_shop_theme_confirm_price_luck_go.SetActive(false);
            ui_shop_theme_confirm_price_coins_go.SetActive(true);
            ui_shop_theme_confirm_price_coins_value_txt.text = price.value.ToString();
            break;
        }
    }
    void _UI_SHOP_THEME_CONFIRM_StateTo(UiShopConfirmState state)
    {
        if (ui_shop_theme_confirm_state == state) return;
        ui_shop_theme_confirm_last_state = ui_shop_theme_confirm_state;
        ui_shop_theme_confirm_state = state;
        _UI_SHOP_THEME_CONFIRM_Hide();
    }
    void _UI_SHOP_THEME_CONFIRM_Hide()
    {
        //set interactable = false
        //start hide animation
        //set animation state
        //continue with
        _UI_SHOP_THEME_CONFIRM_OnHideComplete();
    }
    void _UI_SHOP_THEME_CONFIRM_OnHideComplete()
    {
        //set animation state
        //disable last state
        _UI_SHOP_THEME_CONFIRM_SetEnabled(ui_shop_theme_confirm_last_state, false);
        //show target state
        _UI_SHOP_THEME_CONFIRM_Show();
    }
    void _UI_SHOP_THEME_CONFIRM_Show()
    {
        //enable state
        _UI_SHOP_THEME_CONFIRM_SetEnabled(ui_shop_theme_confirm_state, true);
        //start show animation
        //set animation state
        //continue with
        _UI_SHOP_THEME_CONFIRM_OnShowComplete();
    }
    void _UI_SHOP_THEME_CONFIRM_OnShowComplete()
    {
        //set animation state
        //set interactable = true
    }
    void _UI_SHOP_THEME_CONFIRM_SetEnabled(UiShopConfirmState state, bool enabled)
    {
        switch (state) {
        case UiShopConfirmState.ACTIVE:
            ui_shop_theme_confirm_price_go.SetActive(false);
            ui_shop_theme_confirm_active_go.SetActive(enabled);
            break;
        case UiShopConfirmState.SELECT:
            ui_shop_theme_confirm_price_go.SetActive(false);
            ui_shop_theme_confirm_select_go.SetActive(enabled);
            break;
        case UiShopConfirmState.BUY:
            ui_shop_theme_confirm_price_go.SetActive(true);
            ui_shop_theme_confirm_buy_go.SetActive(enabled);
            break;
        case UiShopConfirmState.NOT_AVAILABLE:
            ui_shop_theme_confirm_price_go.SetActive(false);
            ui_shop_theme_confirm_na_go.SetActive(enabled);
            break;
        }
    }
    #endregion //[THEME CONFIRM]
    #region [THEME SCROLL]
    void _UI_SHOP_THEME_SCROLL_AddListener(Button b, int index)
    {
        b.onClick.RemoveAllListeners();
        b.onClick.AddListener(() => _UI_SHOP_THEME_SCROLL_OnElementSelected(index));
    }
    void _UI_SHOP_THEME_SCROLL_OnEndDrag(BaseEventData data)
    {
        //select playchar
        int index = _UI_SHOP_SCROLL_FindNearPoint(ui_shop_theme_scroll.horizontalNormalizedPosition, ui_shop_theme_scroll_points);
        _UI_SHOP_THEME_SCROLL_OnElementSelected(index);
    }
    void _UI_SHOP_THEME_SCROLL_OnElementSelected(int slot_index)
    {
        ui_shop_themeslot_index = slot_index;

        //snap with animation
        float scroll_target_pos = ui_shop_theme_scroll_points[slot_index];
        ui_shop_scroll_tween.SetBeginGetter(_UI_SHOP_THEME_SCROLL_ScrollPosGetter, false);
        ui_shop_scroll_tween.SetSetter(_UI_SHOP_THEME_SCROLL_ScrollPosSetter);
        ui_shop_scroll_tween.SetEndValue(scroll_target_pos);
        ui_shop_scroll_tween.Restart(0.5f);

        _UI_SHOP_THEME_UpdateUi();
    }
    float _UI_SHOP_THEME_SCROLL_ScrollPosGetter()
    {
        return ui_shop_theme_scroll.horizontalNormalizedPosition;
    }
    void _UI_SHOP_THEME_SCROLL_ScrollPosSetter(float value)
    {
        ui_shop_theme_scroll.horizontalNormalizedPosition = value;
    }
    #endregion //[CHAR SCROLL]
    #endregion //[SHOP THEME]
    #region [SHOP TOP]
    void _UI_SHOP_TOP_UpdateUi()
    {
        //store old values
        int old_coins = ui_shop_top_coins_value;
        int old_luck = ui_shop_top_luck_value;
        //get new values
        ui_shop_top_coins_value = us.coins;
        ui_shop_top_luck_value = us.luck;

        //check coins changed
        if (old_coins != ui_shop_top_coins_value) {
            //tween
            ui_value_tween.CompleteAndClear(true);
            ui_value_tween.SetValues(old_coins, ui_shop_top_coins_value);
            ui_value_tween.SetSetter((float value) => ui_shop_top_coins_value_txt.text = ((int)value).ToString());
            ui_value_tween.Restart(1f);
        }

        //check luck changed
        if (old_luck != ui_shop_top_luck_value) {
            //tween
            ui_value_rev_tween.CompleteAndClear(true);
            ui_value_rev_tween.SetValues(old_luck, ui_shop_top_luck_value);
            ui_value_rev_tween.SetSetter((float value) => ui_shop_top_luck_value_txt.text = ((int)value).ToString());
            ui_value_rev_tween.Restart(1f);
        }
    }
    #endregion //[SHOP TOP]

    //Chs UI
    #region [CHS]
    #region [CHS PAGE]
    void UI_CHS_PageTo(UiChsPage page)
    {
        if (ui_chs_page == page) return;
        ui_chs_last_page = ui_chs_page;
        ui_chs_page = page;
        if (ui_chs_last_page == UiChsPage.HIDDEN) {
            //show page
            _UI_CHS_ShowPage();
        } else {
            //hide or switch
            _UI_CHS_HidePage();
        }
    }
    void _UI_CHS_ShowPage()
    {
        //check root active
        if (!ui_chs_root_go.activeSelf) { ui_chs_root_go.SetActive(true); }
        //enable page
        _UI_CHS_SetPageEnabled(ui_chs_page, true);

        //show menu
        UI_MENU_SwitchTo(UiMenuPage.CHS);

        //set interactable = true
    }
    void _UI_CHS_HidePage()
    {
        //set interactable = false
        //disable last page
        _UI_CHS_SetPageEnabled(ui_chs_last_page, false);
        if (ui_chs_page != UiChsPage.HIDDEN) {
            _UI_CHS_ShowPage();
        } else {
            //hide root
            ui_chs_root_go.SetActive(false);
            //continue with
            _UI_STATE_OnHideComplete();
        }
    }
    void _UI_CHS_SetPageEnabled(UiChsPage page, bool enabled)
    {
        switch (page) {
        case UiChsPage.PROG:
            ui_chs_main_root_go.SetActive(enabled);
            if (enabled) {
                if (us.chprog_random_index < 0) {
                    ui_chs_top_loc.SetTerm(ui_so.chs_prog_top_text, () => new object[] { us.chprog_prog_index + 1 });
                } else {
                    ui_chs_top_loc.SetTerm(ui_so.chs_rand_top_text);
                }
                _UI_CHS_UpdateMainUi(chprog_active, USER_GetActiveChprogRewards());
            }
            break;
        case UiChsPage.DAILY:
            ui_chs_main_root_go.SetActive(enabled);
            if (enabled) {
                ui_chs_top_loc.SetTerm(ui_so.chs_day_top_text);
                CHALLENGE_CheckDailyExpired();
                _UI_CHS_UpdateMainUi(chday_active, USER_GetActiveChdayRewards());
            }
            break;
        case UiChsPage.SPEC:
            ui_chs_main_root_go.SetActive(enabled);
            if (enabled) {
                ui_chs_top_loc.SetTerm(ui_so.chs_spec_top_text);
                CHALLENGE_CheckSpecialExpired();
                _UI_CHS_UpdateMainUi(chspec_active, USER_GetActiveChspecRewards());
            }
            break;
        }
#if UNITY_ANDROID
        EnableUpdateOnMT(_UI_HardwareBtnBackUpdate, enabled);
#endif
    }
    #endregion //[CHS PAGE]
    void _UI_CHS_UpdateMainUi(Challenge ch_active, UserReward[] rewards)
    {
        //Main reward
        UserReward main_rew = rewards[0];
        UserItemDesc idesc = null;
        switch (main_rew.type) {
        case UserInvItemType.DROPS:
            idesc = SelectedPlaycharSlot().ui_drop_desc;
            break;
        default:
            if (!ui_so.user_item_desc.TryGetValue(main_rew.type, out idesc)) {
#if CODEDEBUG
                string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
                LogError(METHOD_NAME, string.Format("UserItemDesc for {0} not found", main_rew.type));
#endif
                return;
            }
            break;
        }
        ui_chs_reward_icon.sprite = idesc.icon;
        ui_chs_reward_name_loc.SetTerm(idesc.name);
        if (main_rew.amount > 1) { ui_chs_reward_value_txt.text = main_rew.amount.ToString(); } else { ui_chs_reward_value_txt.text = string.Empty; }

        int num_bonus = rewards.Length - 1;
        if (num_bonus > 0) {
            ui_chs_reward_bonus_value_txt.text = num_bonus.ToString();
            ui_chs_reward_bonus_go.SetActive(true);
        } else {
            ui_chs_reward_bonus_go.SetActive(false);
        }

        //tasks
        _UI_CHS_SetNumTaskElements(ch_active.NumTasks());
        for (int i = 0, l = ch_active.NumTasks(); i < l; ++i) {
            Transform task_ui_tr = ui_chs_tasks_tr.GetChild(i);

            bool is_green = ch_active.IsTaskGreen(i);
            //icon
            Transform icon_tr = task_ui_tr.GetChild(0);
            icon_tr.GetComponent<Image>().sprite = is_green ? ui_so.chs_green_icon : ui_so.chs_work_icon;
            icon_tr.GetChild(0).gameObject.SetActive(!is_green);

            var custom_ui = ch_active.TaskMainUi(i);
            if (custom_ui != null) {
                //hide desc node
                task_ui_tr.GetChild(1).gameObject.SetActive(false);

                //show custom ui
                if (custom_ui.transform.parent != task_ui_tr) {
                    custom_ui.transform.SetParent(task_ui_tr, false);
                }
                custom_ui.SetActive(true);
            } else {
                //hide custom ui. Start from third
                for (int ch = 2, num_ch = task_ui_tr.childCount; ch < num_ch; ++ch) {
                    task_ui_tr.GetChild(ch).gameObject.SetActive(false);
                }

                //show desc node
                Transform desc_ui_tr = task_ui_tr.GetChild(1);
                desc_ui_tr.gameObject.SetActive(true);
                //fill task desc text
                desc_ui_tr.GetChild(0).GetComponent<LocalizeText>().SetTerm(ch_active.TaskDesc(i));
                //fill tip desc
                Transform task_tip_desc_tr = desc_ui_tr.GetChild(2);
                string task_tip_desc = ch_active.TaskTipDesc(i);
                task_tip_desc_tr.GetComponent<LocalizeText>().SetTerm(task_tip_desc);
                task_tip_desc_tr.gameObject.SetActive(!is_green && !string.IsNullOrEmpty(task_tip_desc));
                //fill progress text
                Transform task_progress_desc_tr = desc_ui_tr.GetChild(1);
                string task_progress_desc = ch_active.TaskProgressDesc(i);
                task_progress_desc_tr.GetComponent<LocalizeText>().SetTerm(task_progress_desc);
                task_progress_desc_tr.gameObject.SetActive(!is_green && !string.IsNullOrEmpty(task_progress_desc));
            }
        }
    }
    void _UI_CHS_SetNumTaskElements(int num)
    {
        if (ui_chs_tasks_tr.childCount < num) {
            int num_child_to_add = num - ui_chs_tasks_tr.childCount;
            for (int i = 0; i < num_child_to_add; ++i) {
                Instantiate(ui_so.chs_task_element_prefab).transform.SetParent(ui_chs_tasks_tr, false);
                ui_chs_tasks_tr.GetChild(ui_chs_tasks_tr.childCount - 1).Find("Icon/Value").GetComponent<Text>().text = ui_chs_tasks_tr.childCount.ToString();
            }
        }
        foreach (Transform child in ui_chs_tasks_tr) {
            if (num > 0) { child.gameObject.SetActive(true); --num; } else { child.gameObject.SetActive(false); }
        }
    }
    #endregion //[CHS]

    //End UI
    #region [END PAGE]
    void UI_END_PageTo(UiEndPage page)
    {
        if (ui_end_page == page) return;
        ui_end_last_page = ui_end_page;
        ui_end_page = page;
        if (ui_end_last_page == UiEndPage.HIDDEN) {
            //show
            _UI_END_ShowPage();
        } else {
            //hide or switch
            _UI_END_HidePage();
        }
    }
    void _UI_END_ShowPage()
    {
        //show root
        ui_end_root_go.SetActive(true);

        //enable page
        _UI_END_SetPageEnabled(ui_end_page, true);

        //show menu
        UI_MENU_SwitchTo(UiMenuPage.END);

        //set interactable = true
    }
    void _UI_END_HidePage()
    {
        //set interactable = false
        //disable last page
        _UI_END_SetPageEnabled(ui_end_last_page, false);
        if (ui_end_page != UiEndPage.HIDDEN) {
            _UI_END_ShowPage();
        } else {
            //hide root
            ui_end_root_go.SetActive(false);
            //continue with
            _UI_STATE_OnHideComplete();
        }
    }
    void _UI_END_SetPageEnabled(UiEndPage page, bool enabled)
    {
        switch (page) {
        case UiEndPage.MAIN:
            ui_end_root_go.SetActive(enabled);
            break;
        }

#if UNITY_ANDROID
        //button activated in GAME_END after showing results
        if (!enabled) { RemoveUpdateOnMT(_UI_HardwareBtnBackUpdate); }
#endif
    }
    #endregion //[END Page]
    #region [END]
    const string RESULT_PLUS = "+";
    const string RESULT_MINUS = "-";
    IEnumerator UI_END_ShowResults()
    {
#if DBG_TRACE_ROUTINES
        Log("Routine", "==>>: {0}", "END_ShowResults");
#endif
        //prepare
        ui_end_result_root_go.SetActive(true);
        ui_end_result_score_go.SetActive(true);
        ui_end_result_nbcoins_go.SetActive(false);
        ui_end_result_coins_go.SetActive(false);
        ui_end_result_luck_go.SetActive(false);
        ui_end_joystick_btn_go.SetActive(false);
        string zero_txt = (0).ToString();
        ui_end_result_score_value_txt.text = zero_txt;
        ui_end_result_nbcoins_value_txt.text = zero_txt;
        ui_end_result_coins_value_txt.text = zero_txt;
        ui_end_result_luck_value_txt.text = zero_txt;
        ui_end_result_luck_sign_txt.text = (pch_luck_collected < 0) ? RESULT_MINUS : RESULT_PLUS;

        //root pos
        RectTransform tr = ui_end_result_root_go.GetComponent<RectTransform>();
        Vector3 pos = tr.localPosition;
        pos.y = 0;
        tr.localPosition = pos;

        //capture values
        ui_score_value = (int)pch_playing_score;
        ui_nbcoins_value = pch_coins_nb_collected;
        ui_coins_value = pch_coins_b_collected;
        ui_luck_value = System.Math.Abs(pch_luck_collected);

        //show playchar 3d
        var playchar = SelectedUiPlaychar();
        playchar.transform.SetParent(ui_end_result_char_tr, false);
        playchar.SetActive(true);
        var playchar_anim = playchar.GetComponent<AnimationController>();

        //animation
        ui_end_result_root_go.GetComponent<Animation>().SampleAt(0);
        playchar_anim.PlaySequence(UI_PLAYCHAR_ANIM_GREETING);
        ui_end_result_score_go.GetComponent<Animation>().StateAt(0).speed = 2f;
        ui_end_result_score_go.GetComponent<Animation>().Play();

        //clear tweens
        ui_value_tween.CompleteAndClear(true);
        ui_value_rev_tween.CompleteAndClear(true);

        //tween
        ui_value_tween.SetValues(0f, ui_score_value);
        ui_value_tween.SetSetter((value) => ui_end_result_score_value_txt.text = ((int)value).ToString());
        ui_value_tween.Restart(0.2f, 0.5f);

        while (ui_value_tween.IsEnabled()) {
            yield return null;
        }

        // PART 2 ----------------------------------

        //show nbcoins
        ui_end_result_nbcoins_go.SetActive(true);

        //animation
        ui_end_result_nbcoins_go.GetComponent<AnimationController>().PlaySequence(0);

        if (ui_nbcoins_value > 0) {
            //tween rev
            ui_value_rev_tween.SetValues(ui_nbcoins_value, 0f);
            ui_value_rev_tween.SetSetter((value) => ui_end_result_nbcoins_value_txt.text = ((int)value).ToString());
            ui_value_rev_tween.Restart(0.2f, 0.5f);

            //tween
            ui_score_value += PLAYCHAR_ScoreByCoins(ui_nbcoins_value);
            ui_value_tween.SetValues(ui_value_tween.CurrentValue(), ui_score_value);
            ui_value_tween.Restart(0.2f, 0.5f);
        }

        while (ui_value_tween.IsEnabled()) {
            yield return null;
        }

        // PART 3 ----------------------------------

        //hide nbcoins animation
        ui_end_result_nbcoins_go.GetComponent<AnimationController>().ProceedState();

        ui_end_result_coins_go.SetActive(true);
        //animation
        ui_end_result_coins_go.GetComponent<Animation>().StateAt(0).speed = 2f;
        ui_end_result_coins_go.GetComponent<Animation>().Play();

        if (ui_coins_value > 0) {
            //tween
            ui_value_tween.CompleteAndClear(false);
            ui_value_tween.SetValues(0f, ui_coins_value);
            ui_value_tween.SetSetter((value) => ui_end_result_coins_value_txt.text = ((int)value).ToString());
            ui_value_tween.Restart(0.2f, 0.5f);
        }

        while (ui_value_tween.IsEnabled()) {
            yield return null;
        }

        // PART 4 ----------------------------------

        ui_end_result_luck_go.SetActive(true);

        //animation
        ui_end_result_luck_go.GetComponent<Animation>().StateAt(0).speed = 2f;
        ui_end_result_luck_go.GetComponent<Animation>().Play();

        if (ui_luck_value > 0) {
            //tween
            ui_value_tween.CompleteAndClear(false);
            ui_value_tween.SetValues(0f, ui_luck_value);
            ui_value_tween.SetSetter((value) => ui_end_result_luck_value_txt.text = ((int)value).ToString());
            ui_value_tween.Restart(0.2f, 0.5f);
        }

        while (ui_value_tween.IsEnabled()) {
            yield return null;
        }

        // PART 5 ----------------------------------

        //clear tweens
        /*ui_value_tween.CompleteAndClear(false);
        ui_value_rev_tween.CompleteAndClear(false);*/

        //result root animation
        ui_end_result_root_go.GetComponent<Animation>().Play();
        ui_time_waiter.ResetBreak();
        yield return ui_time_waiter.Reset(0.5f);

        // PART 6 ----------------------------------

        //screen btn
        ui_end_joystick_btn_go.SetActive(true);

#if DBG_TRACE_ROUTINES
        Log("Routine", "<<==: {0}", "END_ShowResults");
#endif
    }
    void _UI_END_OnJoystickBtn()
    {
        AUD_UI_Sound(UiSoundType.JOYSTICK);

        GAME_SwitchTo(GameState.PLAYING);
    }
    void _UI_END_OnFeedBtn()
    {
        FB_Feed();
    }
    #endregion //[END]

    #region [FB PAGE]
    void UI_FB_PageTo(UiFbPage page)
    {
        if (ui_fb_page == page) return;
        ui_fb_last_page = ui_fb_page;
        ui_fb_page = page;
        if (ui_fb_last_page == UiFbPage.HIDDEN) {
            //show page
            _UI_FB_ShowPage();
        } else {
            //hide or switch
            _UI_FB_HidePage();
        }
    }
    void _UI_FB_ShowPage()
    {
        //check root active
        if (!ui_fb_root_go.activeSelf) { ui_fb_root_go.SetActive(true); }
        //enable page
        _UI_FB_SetPageEnabled(ui_fb_page, true);

        //set interactable = true
    }
    void _UI_FB_HidePage()
    {
        //set interactable = false
        //disable last page
        _UI_FB_SetPageEnabled(ui_fb_last_page, false);
        if (ui_fb_page != UiFbPage.HIDDEN) {
            _UI_FB_ShowPage();
        } else {
            //hide root
            ui_fb_root_go.SetActive(false);
            //continue with
            _UI_STATE_OnHideComplete();
        }
    }
    void _UI_FB_SetPageEnabled(UiFbPage page, bool enabled)
    {
        switch (page) {
        case UiFbPage.LOGIN:
            ui_fb_login_root_go.SetActive(enabled);
            if (enabled) {
                ui_fb_toppanel_loc.SetTerm(ui_so.fb_toppanel_login_text);
                ui_fb_toppanel_loc.enabled = true;
                ui_fb_login_reward_coins_value_txt.text = reward_data.signup.rewards[0].amount.ToString();
                ui_fb_login_reward_luck_value_txt.text = reward_data.signup.rewards[1].amount.ToString();
                UI_MENU_SwitchTo(UiMenuPage.FB_LOGINOUT);
            }
            break;
        case UiFbPage.LOGOUT:
            ui_fb_logout_root_go.SetActive(enabled);
            if (enabled) {
                ui_fb_toppanel_loc.SetTerm(ui_so.fb_toppanel_logout_text);
                ui_fb_toppanel_loc.enabled = true;
                UI_MENU_SwitchTo(UiMenuPage.FB_LOGINOUT);
            }
            break;
        case UiFbPage.STAT:
            ui_fb_stat_root_go.SetActive(enabled);
            if (enabled) {
                ui_fb_toppanel_loc.enabled = false;
                ui_fb_toppanel_txt.text = ui_fb_stat_user_first_name;
                UI_MENU_SwitchTo(UiMenuPage.FB_MAIN);
                _UI_FB_STAT_UpdateUi();
            }
            break;
        case UiFbPage.TOP:
            ui_fb_top_root_go.SetActive(enabled);
            if (enabled) {
                ui_fb_toppanel_loc.SetTerm(ui_so.fb_toppanel_top_text);
                ui_fb_toppanel_loc.enabled = true;
                UI_MENU_SwitchTo(UiMenuPage.FB_MAIN);
            }
            break;
        }
#if UNITY_ANDROID
        EnableUpdateOnMT(_UI_HardwareBtnBackUpdate, enabled);
#endif
    }
    #endregion //[FB PAGE]
    #region [FB]
    void UI_FB_OnFbButon()
    {
        if (IsOnline()) {
            _UI_FB_OnFbButtonOnline();
        } else {
            QUERY_CheckOnlineStatus();
            if (IsOnline()) {
                _UI_FB_OnFbButtonOnline();
            }
        }
    }
    void _UI_FB_OnFbButtonOnline()
    {
        if (FB.IsInitialized) {
            if (FB.IsLoggedIn) {
                if (IsPullFinished()) {
                    _UI_OnBtnStateTo(UiState.FB, UiFbPage.STAT);
                } else {
                    FB_OnLoginSuccess();
                }
            } else {
                _UI_OnBtnStateTo(UiState.FB, UiFbPage.LOGIN);
            }
        } else {
            FB_Init();
        }
    }
    void UI_FB_OnTopButton()
    {
#if CODEDEBUG
        string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
        if (!IsOnline()) {
            LogError(METHOD_NAME, "is Offline");
            return;
        }
        if (!FB.IsInitialized) {
            LogError(METHOD_NAME, "FB isInitialized returned FALSE");
            return;
        }
        if (!FB.IsLoggedIn) {
            LogError(METHOD_NAME, "FB isLoggedIn returned FALSE");
            return;
        }
#endif
        if (IsScoreFinished()) {
            UI_STATE_SwitchTo(UiState.FB, UiFbPage.TOP);
        } else {
            if (!qidscore_finished) { FB_QUERY_UserFriends(UI_FB_Query_UserFriends_Complete); }
            if (!qtopscore_finished) { QUERY_DoTopScores(); }
        }

        AUD_UI_Sound(UiSoundType.BUTTON);
    }
    void _UI_FB_OnLoginStateChanged()
    {
        //Begin Menu Fb Button
        var login_button = ui_menu_begin_fb_go.GetComponent<Button>();
        var login_icon = ui_menu_begin_fb_go.GetComponent<Image>();
        var login_sprites = login_button.spriteState;
        Button top_button = ui_menu_fb_top_go.GetComponent<Button>();
        if (IsOnline() && FB.IsInitialized && FB.IsLoggedIn) {
            //begin menu
            login_icon.sprite = ui_so.fb_logged_in_icon;
            login_sprites.highlightedSprite = ui_so.fb_logged_in_icon;
            login_button.spriteState = login_sprites;

            //top
            top_button.interactable = true;
            FB_QUERY_UserFirstName(UI_FB_Query_UserName_Complete);
            FB_QUERY_UserFriends(UI_FB_Query_UserFriends_Complete);
            FB_QUERY_IdPicture(AccessToken.CurrentAccessToken.UserId, UI_FB_Query_UserPicture_Complete);
        } else {
            //begin menu
            login_icon.sprite = ui_so.fb_not_logged_in_icon;
            login_sprites.highlightedSprite = ui_so.fb_not_logged_in_icon;
            login_button.spriteState = login_sprites;

            //top
            top_button.interactable = false;
            ui_fb_top_friend_infos = null;
            _UI_FB_STAT_UpdateUserDisplayData();
        }

        switch (ui_fb_page) {
        case UiFbPage.LOGIN:
            UI_STATE_SwitchTo(UiState.FB, UiFbPage.STAT);
            break;
        case UiFbPage.LOGOUT:
            UI_STATE_SwitchTo(UiState.BEGIN, UiBeginPage.MAIN);
            break;
        }
    }
    void _UI_FB_OnUserHighscoreChanged()
    {
        if (!FB.IsLoggedIn) return;

        //query new top situation
        QUERY_DoTopScores();

        _UI_FB_STAT_UpdateUserDisplayData();
        _UI_FB_TOP_FriendsPrepare();
    }
    void _UI_FB_OnOnlineStateChanged()
    {
        ui_fb_login_button_go.GetComponent<Button>().interactable = IsOnline();
        if (!IsOnline()) {
            if (ui_state == UiState.FB) { UI_STATE_SwitchTo(UiState.BEGIN); }
            Notify(qoffline_note);
        }
    }
    void _UI_FB_LOGIN_OnLoginBtn()
    {
        FB_Login();

        AUD_UI_Sound(UiSoundType.BUTTON);
    }
    void _UI_FB_LOGOUT_OnLogoutBtn()
    {
        FB_Logout();

        AUD_UI_Sound(UiSoundType.BACK);
    }
    void _UI_FB_STAT_UpdateUi()
    {
        //update display data
        ui_fb_stat_score_value_txt.text = us.highscore.ToString();
        ui_fb_stat_coins_value_txt.text = us.coins.ToString();
        ui_fb_stat_luck_value_txt.text = us.luck.ToString();

        //3d playchar
        if (ui_fb_stat_char_active_go != null) { ui_fb_stat_char_active_go.SetActive(false); }
        ui_fb_stat_char_active_go = SelectedUiPlaychar();
        ui_fb_stat_char_active_go.transform.SetParent(ui_fb_stat_char_node, false);
        ui_fb_stat_char_active_go.SetActive(true);
        //3d user icon
        var user_icon_go = _UI_FB_STAT_UserIconGo();
        user_icon_go.transform.SetParent(ui_fb_stat_char_node, false);
        //animation
        ui_fb_stat_char_active_go.GetComponent<AnimationController>().PlaySequence(UI_PLAYCHAR_ANIM_STAT);
        user_icon_go.GetComponent<AnimationController>().PlaySequence(0);
    }
    GameObject _UI_FB_STAT_UserIconGo()
    {
        if (ui_fb_stat_user_icon_go == null) {
            ui_fb_stat_user_icon_go = Instantiate(ui_so.fb_stat_user_icon_prefab);
            ui_fb_stat_user_icon_go.transform.SetParent(ui_fb_stat_char_node, false);
        }
        return ui_fb_stat_user_icon_go;
    }
    void _UI_FB_STAT_UpdateUserDisplayData()
    {
        if (ui_fb_page == UiFbPage.STAT) {
            ui_fb_toppanel_loc.enabled = false;
            ui_fb_toppanel_txt.text = ui_fb_stat_user_first_name;
        }
        ui_fb_stat_score_value_txt.text = us.highscore.ToString();
        ui_fb_stat_coins_value_txt.text = us.coins.ToString();
        ui_fb_stat_luck_value_txt.text = us.luck.ToString();
        _UI_FB_STAT_UserIconGo().GetComponent<Renderer>().materials[1].mainTexture = (ui_fb_stat_user_pic != null) ? ui_fb_stat_user_pic : ui_so.fb_friend_default_texture;
    }
    void _UI_FB_TOP_Friends_SetNumElements(Transform root, int num)
    {
        num = System.Math.Min(System.Math.Max(num, 0), root.childCount);

        foreach (Transform child in root) {
            if (num > 0) { child.gameObject.SetActive(true); --num; } else { child.gameObject.SetActive(false); }
        }
    }
    void _UI_FB_TOP_FriendsPrepare()
    {
        if (ui_fb_top_friends_to_query == null) { ui_fb_top_friends_to_query = new List<Pair<UI_FbFriendInfo, UI_FbFriendElement>>(10); }
        ui_fb_top_friends_to_query.Clear();

#if CODEDEBUG
        if (ui_fb_top_friend_infos.Length == 0) {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            LogError(METHOD_NAME, "ui_fb_top_friend_infos is NULL");
            return;
        }
#endif

        //find user pos
        int user_pos = System.Array.FindIndex(ui_fb_top_friend_infos, (item) => item.score <= us.highscore);
        if (user_pos < 0) { user_pos = ui_fb_top_friend_infos.Length; }
        if (user_pos <= 4) {
            //top friends
            int num_friend_elements = user_pos + 1;
            //above user
            for (int i = 0; i < user_pos; ++i) {
                ui_fb_top_topfriend_elements[i].SetInfo(ui_fb_top_friend_infos[i], i + 1);
                ui_fb_top_friends_to_query.Add(MakePair(ui_fb_top_friend_infos[i], ui_fb_top_topfriend_elements[i]));
            }
            //set user info
            ui_fb_top_topfriend_elements[user_pos].SetInfo(us.name, us.highscore, ui_fb_stat_user_pic, user_pos + 1);
            //below user
            if (user_pos < ui_fb_top_friend_infos.Length) {
                ui_fb_top_topfriend_elements[user_pos + 1].SetInfo(ui_fb_top_friend_infos[user_pos], user_pos + 2);
                ui_fb_top_friends_to_query.Add(MakePair(ui_fb_top_friend_infos[user_pos], ui_fb_top_topfriend_elements[user_pos + 1]));
                ++num_friend_elements;
            }
            if (user_pos == 0 && user_pos < ui_fb_top_friend_infos.Length - 1) {
                ui_fb_top_topfriend_elements[user_pos + 2].SetInfo(ui_fb_top_friend_infos[user_pos + 1], user_pos + 3);
                ui_fb_top_friends_to_query.Add(MakePair(ui_fb_top_friend_infos[user_pos + 1], ui_fb_top_topfriend_elements[user_pos + 2]));
                ++num_friend_elements;
            }
            _UI_FB_TOP_Friends_SetNumElements(ui_fb_top_topfriends_tr, num_friend_elements);
            //bottom friends
            ui_fb_top_bottomfriends_tr.gameObject.SetActive(false);
            ui_fb_top_friends_delimiter_go.SetActive(false);
        } else {
            //top friends
            _UI_FB_TOP_Friends_SetNumElements(ui_fb_top_topfriends_tr, 3);
            for (int i = 0; i < 3; ++i) {
                ui_fb_top_topfriend_elements[i].SetInfo(ui_fb_top_friend_infos[i], i + 1);
                ui_fb_top_friends_to_query.Add(MakePair(ui_fb_top_friend_infos[i], ui_fb_top_topfriend_elements[i]));
            }
            //bottom friends
            _UI_FB_TOP_Friends_SetNumElements(ui_fb_top_bottomfriends_tr, (user_pos < ui_fb_top_friend_infos.Length) ? 3 : 2);
            ui_fb_top_bottomfriends_tr.gameObject.SetActive(true);
            ui_fb_top_friends_delimiter_go.SetActive(true);
            //above user
            ui_fb_top_bottomfriend_elements[0].SetInfo(ui_fb_top_friend_infos[user_pos - 1], user_pos);
            ui_fb_top_friends_to_query.Add(MakePair(ui_fb_top_friend_infos[user_pos - 1], ui_fb_top_bottomfriend_elements[0]));
            //user
            ui_fb_top_bottomfriend_elements[1].SetInfo(us.name, us.highscore, ui_fb_stat_user_pic, user_pos + 1);
            //below user
            if (user_pos < ui_fb_top_friend_infos.Length) {
                ui_fb_top_bottomfriend_elements[2].SetInfo(ui_fb_top_friend_infos[user_pos], user_pos + 2);
                ui_fb_top_friends_to_query.Add(MakePair(ui_fb_top_friend_infos[user_pos], ui_fb_top_bottomfriend_elements[2]));
            }
        }
        _UI_FB_StartQuery_TopFriends_Pics();
    }
    void UI_FB_Query_UserName_Complete(IGraphResult result)
    {
        var data = result.ResultDictionary as IDictionary<string, object>;

        object name;
        object first_name;
        if (data.TryGetValue("name", out name)) {
            us.name = name as string;
        }
#if CODEDEBUG
 else {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            LogWarning(METHOD_NAME, "cannot find name");
        }
#endif
        if (data.TryGetValue("first_name", out first_name)) {
            ui_fb_stat_user_first_name = first_name as string;
        }
#if CODEDEBUG
        else {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            LogWarning(METHOD_NAME, "cannot find first_name");
        }
#endif
        _UI_FB_STAT_UpdateUserDisplayData();
    }
    void UI_FB_Query_UserFriends_Complete(IGraphResult result)
    {
        var data = result.ResultDictionary as IDictionary<string, object>;

        //get friends
        object friends_data_obj;
        if (!data.TryGetValue("friends", out friends_data_obj)) {
#if CODEDEBUG
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            LogWarning(METHOD_NAME, "cannot find friends");
#endif
            return;
        }
        var friends_data = friends_data_obj as IDictionary<string, object>;
        if (friends_data == null) {
#if CODEDEBUG
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            LogWarning(METHOD_NAME, "friends data invalid");
#endif
            return;
        }
        object friends_array_obj;
        if (!friends_data.TryGetValue("data", out friends_array_obj)) {
#if CODEDEBUG
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            LogWarning(METHOD_NAME, "cannot get friends");
#endif
            return;
        }
        var friends_list = friends_array_obj as IList<object>;
        if (friends_list == null) {
#if CODEDEBUG
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            LogWarning(METHOD_NAME, "friends_array data invalid");
#endif
            return;
        }

        //fill friends list
        if (ui_fb_top_friend_infos == null) {
            ui_fb_top_friend_infos = new UI_FbFriendInfo[friends_list.Count];
        }
        if (ui_fb_top_friend_infos.Length != friends_list.Count) {
            System.Array.Resize(ref ui_fb_top_friend_infos, friends_list.Count);
        }
        for (int i = 0; i < ui_fb_top_friend_infos.Length; ++i) {
            if (ui_fb_top_friend_infos[i] == null) {
                ui_fb_top_friend_infos[i] = new UI_FbFriendInfo();
            }

            var friend_record_obj = friends_list[i];
            var friend_record = friend_record_obj as IDictionary<string, object>;
            if (friend_record == null) {
#if CODEDEBUG
                string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
                LogWarning(METHOD_NAME, "friend_list[{0}] is NULL", i);
#endif
                continue;
            }
            object id_obj;
            if (!friend_record.TryGetValue("id", out id_obj)) {
#if CODEDEBUG
                string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
                LogWarning(METHOD_NAME, "cannot find friend_list[{0}].id", i);
#endif
                continue;
            }
            string id = id_obj as string;
            if (string.IsNullOrEmpty(id)) {
#if CODEDEBUG
                string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
                LogWarning(METHOD_NAME, "friend_list[{0}].id is NULL", i);
#endif
                continue;
            }
            ui_fb_top_friend_infos[i].id = id;

            object name_obj;
            if (!friend_record.TryGetValue("name", out name_obj)) {
#if CODEDEBUG
                string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
                LogWarning(METHOD_NAME, "cannot find friend_list[{0}].name", i);
#endif
                continue;
            }
            string name = name_obj as string;
            if (string.IsNullOrEmpty(name)) {
#if CODEDEBUG
                string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
                LogWarning(METHOD_NAME, "friend_list[{0}].name is NULL", i);
#endif
                continue;
            }
            ui_fb_top_friend_infos[i].name = name;
        }
        ui_fb_top_friends_go.SetActive(ui_fb_top_friend_infos.Length != 0);

        //topfriends ui
        if (ui_fb_top_topfriend_elements == null) { ui_fb_top_topfriend_elements = new UI_FbFriendElement[6]; }
        for (int i = 0; i < ui_fb_top_topfriend_elements.Length; ++i) {
            if (ui_fb_top_topfriend_elements[i] == null) {
                ui_fb_top_topfriend_elements[i] = new UI_FbFriendElement(Instantiate(ui_so.fb_friend_element_prefab), ui_fb_top_topfriends_tr, i + 1);
            }
        }
        //bottomfriends ui
        if (ui_fb_top_bottomfriend_elements == null) { ui_fb_top_bottomfriend_elements = new UI_FbFriendElement[3]; }
        for (int i = 0; i < ui_fb_top_bottomfriend_elements.Length; ++i) {
            if (ui_fb_top_bottomfriend_elements[i] == null) {
                ui_fb_top_bottomfriend_elements[i] = new UI_FbFriendElement(Instantiate(ui_so.fb_friend_element_prefab), ui_fb_top_bottomfriends_tr, i + 1);
            }
        }

        //query friends score
        string[] ids = new string[ui_fb_top_friend_infos.Length];
        for (int i = 0; i < ids.Length; ++i) {
            ids[i] = ui_fb_top_friend_infos[i].id;
        }
        string serializedTopIds = SerializationHelpers.SerializeToContent<string[], FullSerializerSerializer>(ids);
        QUERY_DoIdscore(serializedTopIds);
    }
    void UI_FB_Query_UserPicture_Complete(IGraphResult result)
    {
        Texture2D tex = null;
        if (string.IsNullOrEmpty(result.Error)) {
            tex = result.Texture;
        }
#if CODEDEBUG
        else {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            LogWarning(METHOD_NAME, "invalid pic for user. error: {0}", result.Error);
        }
#endif
        ui_fb_stat_user_pic = tex;
        _UI_FB_STAT_UpdateUserDisplayData();
    }
    void UI_FB_Query_FriendScores_Complete(IDictionary<string, object> scores)
    {
        if (scores == null || scores.Count == 0) {
#if CODEDEBUG
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            LogWarning(METHOD_NAME, "scores is NULL");
#endif
            return;
        }

        //fill scores
        foreach (var kv in scores) {
            int index = System.Array.FindIndex(ui_fb_top_friend_infos, (item) => item.id == kv.Key);
            if (index >= 0) {
                ui_fb_top_friend_infos[index].score = System.Convert.ToInt32(kv.Value);
            }
#if CODEDEBUG
 else {
                string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
                LogWarning(METHOD_NAME, "id {0} not found", kv.Key);
            }
#endif
        }

        //sort by score
        System.Array.Sort(ui_fb_top_friend_infos, (item1, item2) => item2.score.CompareTo(item1.score));

        _UI_FB_TOP_FriendsPrepare();
    }
    void UI_FB_Query_TopAllScores_Complete(IDictionary<string, object> scores)
    {
        if (scores == null || scores.Count == 0) {
#if CODEDEBUG
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            LogWarning(METHOD_NAME, "scores is NULL");
#endif
            return;
        }

        //infos
        if (ui_fb_top_all_infos == null) { ui_fb_top_all_infos = new UI_FbFriendInfo[scores.Count]; }
        if (ui_fb_top_all_infos.Length != scores.Count) { System.Array.Resize(ref ui_fb_top_all_infos, scores.Count); }
        int top_index = 0;
        foreach (var kv in scores) {
            if (ui_fb_top_all_infos[top_index] == null) { ui_fb_top_all_infos[top_index] = new UI_FbFriendInfo(); }
            ui_fb_top_all_infos[top_index].id = kv.Key;
            ui_fb_top_all_infos[top_index].score = System.Convert.ToInt32(kv.Value);

            ++top_index;
        }
        //ui
        if (ui_fb_top_all_elements == null) { ui_fb_top_all_elements = new UI_FbFriendElement[scores.Count]; }
        if (ui_fb_top_all_elements.Length != scores.Count) { System.Array.Resize(ref ui_fb_top_all_elements, scores.Count); }
        for (int i = 0; i < ui_fb_top_all_elements.Length; ++i) {
            if (ui_fb_top_all_elements[i] == null) {
                ui_fb_top_all_elements[i] = new UI_FbFriendElement(Instantiate(ui_so.fb_friend_element_prefab), ui_fb_top_all_tr, i + 1);
            }
            ui_fb_top_all_elements[i].SetName(string.Empty);
            ui_fb_top_all_elements[i].SetPicture(null);
            ui_fb_top_all_elements[i].SetScore(ui_fb_top_all_infos[i].score);
        }

        _UI_FB_StartQuery_TopAll_Names();
        _UI_FB_StartQuery_TopAll_Pics();
    }
    void _UI_FB_StartQuery_TopAll_Names()
    {
        ui_fb_top_all_name_qindex = 0;
        if (ui_fb_top_all_infos == null || ui_fb_top_all_infos.Length == 0) {
#if CODEDEBUG
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            LogWarning(METHOD_NAME, "ui_fb_top_all_infos is NULL");
#endif
            return;
        }
        FB_QUERY_IdName(ui_fb_top_all_infos[ui_fb_top_all_name_qindex].id, _UI_FB_Query_TopAll_Name_Complete);
    }
    void _UI_FB_Query_TopAll_Name_Complete(IGraphResult result)
    {
        if (!string.IsNullOrEmpty(result.Error)) {
#if CODEDEBUG
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            LogWarning(METHOD_NAME, "query for topid[{0}] .{1}", ui_fb_top_all_name_qindex, result.Error);
#endif
            goto Next;
        }
        var data = result.ResultDictionary as IDictionary<string, object>;
        if (data == null) {
#if CODEDEBUG
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            LogWarning(METHOD_NAME, "invalid data for topid[{0}]", ui_fb_top_all_name_qindex);
#endif
            goto Next;
        }
        object name_obj;
        if (!data.TryGetValue("name", out name_obj)) {
#if CODEDEBUG
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            LogWarning(METHOD_NAME, "cannot find name for topid[{0}]", ui_fb_top_all_name_qindex);
#endif
            goto Next;
        }
        string name = name_obj as string;
#if CODEDEBUG
        if (string.IsNullOrEmpty(name)) {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            LogWarning(METHOD_NAME, "name is NULL");
        }
#endif
        ui_fb_top_all_infos[ui_fb_top_all_name_qindex].name = name;
        ui_fb_top_all_elements[ui_fb_top_all_name_qindex].SetName(ui_fb_top_all_infos[ui_fb_top_all_name_qindex].name);

        Next:
        //move next
        if (++ui_fb_top_all_name_qindex < ui_fb_top_all_infos.Length) {
            FB_QUERY_IdName(ui_fb_top_all_infos[ui_fb_top_all_name_qindex].id, _UI_FB_Query_TopAll_Name_Complete);
        }
    }
    void _UI_FB_StartQuery_TopAll_Pics()
    {
        //begin -1 so increment will start from 0
        ui_fb_top_all_pic_qindex = -1;
        if (ui_fb_top_all_infos == null || ui_fb_top_all_infos.Length == 0) {
#if CODEDEBUG
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            LogWarning(METHOD_NAME, "ui_fb_top_all_infos is NULL");
#endif
            return;
        }

        //move next
        _UI_FB_Query_TopAll_Pic_QueryNext();
    }
    void _UI_FB_Query_TopAll_Pic_QueryNext()
    {
        if (++ui_fb_top_all_pic_qindex < ui_fb_top_all_infos.Length) {
            //pictures should be updated all without checking already exits
            FB_QUERY_IdPicture(ui_fb_top_all_infos[ui_fb_top_all_pic_qindex].id, _UI_FB_Query_TopAll_Pic_Complete);
        }
    }
    void _UI_FB_Query_TopAll_Pic_Complete(IGraphResult result)
    {
        Texture2D tex = null;
        if (string.IsNullOrEmpty(result.Error)) {
            tex = result.Texture;
        }
#if CODEDEBUG
        else {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            LogWarning(METHOD_NAME, "invalid pic for topid[{0}]. {1}", ui_fb_top_all_pic_qindex, result.Error);
        }
#endif
        ui_fb_top_all_infos[ui_fb_top_all_pic_qindex].pic = tex;
        ui_fb_top_all_elements[ui_fb_top_all_pic_qindex].SetPicture(tex);

        //move next
        _UI_FB_Query_TopAll_Pic_QueryNext();
    }
    void _UI_FB_StartQuery_TopFriends_Pics()
    {
        //start from -1
        ui_fb_top_friends_pic_qindex = -1;
        if (ui_fb_top_friends_to_query == null || ui_fb_top_friends_to_query.Count == 0) {
#if CODEDEBUG
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            LogWarning(METHOD_NAME, "ui_fb_top_friends_to_query is NULL");
#endif
            return;
        }

        //move next
        _UI_FB_Query_TopFriends_Pic_QueryNext();
    }
    void _UI_FB_Query_TopFriends_Pic_QueryNext()
    {
        if (++ui_fb_top_friends_pic_qindex < ui_fb_top_friends_to_query.Count) {
            //check if picture already exists
            if (ui_fb_top_friends_to_query[ui_fb_top_friends_pic_qindex].v1.pic == null) {
                FB_QUERY_IdPicture(ui_fb_top_friends_to_query[ui_fb_top_friends_pic_qindex].v1.id, _UI_FB_Query_TopFriends_Pic_Complete);
            } else {
                InvokeOnMT(_UI_FB_Query_TopFriends_Pic_QueryNext);
            }
        }
    }
    void _UI_FB_Query_TopFriends_Pic_Complete(IGraphResult result)
    {
        Texture2D tex = null;
        if (string.IsNullOrEmpty(result.Error)) {
            tex = result.Texture;
        }
#if CODEDEBUG
        else {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            LogWarning(METHOD_NAME, "invalid pic for topid[{0}]. {1}", ui_fb_top_all_pic_qindex, result.Error);
        }
#endif
        ui_fb_top_friends_to_query[ui_fb_top_friends_pic_qindex].v1.pic = tex;
        ui_fb_top_friends_to_query[ui_fb_top_friends_pic_qindex].v2.SetPicture(tex);

        //move next
        _UI_FB_Query_TopFriends_Pic_QueryNext();
    }
    #endregion //[FB]

    //Menu
    #region [Menu]
    void UI_MENU_SwitchTo(UiMenuPage page)
    {
        if (ui_menu_page == page) return;
        if (ui_menu_anim_state != UiShowHideAnimState.NONE) {
            _UI_MENU_CompleteAnimations();
        }
        ui_menu_last_page = ui_menu_page;
        ui_menu_page = page;
        if (ui_menu_last_page == UiMenuPage.HIDDEN) {
            //show
            _UI_MENU_Show();
        } else {
            //hide or switch
            _UI_MENU_Hide();
        }
    }
    void UI_MENU_SetInteractable(bool enable)
    {
        float alphaValue = enable ? 1.0f : 0.5f;
        for (int i = 0; i < 4; ++i) {
            var group = ui_menu_btn_tr[i].GetComponent<CanvasGroup>();
            group.alpha = alphaValue;
            group.interactable = enable;
        }
    }
    void _UI_MENU_SetPageEnabled(UiMenuPage page, bool enabled)
    {
        switch (page) {
        case UiMenuPage.BEGIN:
            ui_menu_begin_fb_go.SetActive(enabled);
            ui_menu_begin_shop_go.SetActive(enabled);
            ui_menu_begin_chs_go.SetActive(enabled);
            ui_menu_begin_quit_go.SetActive(enabled);
            break;
        case UiMenuPage.SHOP:
            ui_menu_shop_char_go.SetActive(enabled);
            ui_menu_shop_theme_go.SetActive(enabled);
            ui_menu_shop_buyitem_go.SetActive(enabled);
            ui_menu_back_go.SetActive(enabled);
            break;
        case UiMenuPage.CHS:
            ui_menu_chs_prog_go.SetActive(enabled);
            ui_menu_chs_day_go.SetActive(enabled);
            ui_menu_chs_spec_go.SetActive(enabled);
            ui_menu_back_go.SetActive(enabled);
            break;
        case UiMenuPage.PLAYING_PAUSE:
            ui_menu_begin_chs_go.SetActive(enabled);
            ui_menu_restart_go.SetActive(enabled);
            break;
        case UiMenuPage.END:
            ui_menu_restart_go.SetActive(enabled);
            break;
        case UiMenuPage.FB_MAIN:
            ui_menu_fb_stat_go.SetActive(enabled);
            ui_menu_fb_top_go.SetActive(enabled);
            ui_menu_fb_logout_go.SetActive(enabled);
            ui_menu_back_go.SetActive(enabled);
            break;
        case UiMenuPage.FB_LOGINOUT:
            ui_menu_back_go.SetActive(enabled);
            break;
        case UiMenuPage.HIGHSCORE:
            ui_menu_back_go.SetActive(enabled);
            break;
        }
    }
    void _UI_MENU_Show()
    {
        _UI_MENU_SetPageEnabled(ui_menu_page, true);
        //start show animation
        ui_menu_anim_state = UiShowHideAnimState.SHOW;
        if (ui_menu_last_page == UiMenuPage.HIDDEN) {
            //start full show animation
            ui_menu_root_go.SetActive(true);
        } else {
            //start short show animation
        }
        //continue with
        _UI_MENU_OnShowComplete();
    }
    void _UI_MENU_Hide()
    {
        //set interactable = false
        //start hide animation
        ui_menu_anim_state = UiShowHideAnimState.HIDE;
        if (ui_menu_page == UiMenuPage.HIDDEN) {
            //start full hide animation
            ui_menu_root_go.SetActive(false);
        } else {
            //start short hide animation
        }
        //continue with
        _UI_MENU_OnHideComplete();
    }
    void _UI_MENU_OnShowComplete()
    {
        ui_menu_anim_state = UiShowHideAnimState.NONE;
        //set interactable = true
    }
    void _UI_MENU_OnHideComplete()
    {
        ui_menu_anim_state = UiShowHideAnimState.NONE;
        //disable last buttons
        _UI_MENU_SetPageEnabled(ui_menu_last_page, false);
        if (ui_menu_page != UiMenuPage.HIDDEN) { _UI_MENU_Show(); }
    }
    void _UI_MENU_CompleteAnimations()
    {
        if (ui_menu_anim_state == UiShowHideAnimState.SHOW) {
            //complete animation
            _UI_MENU_OnShowComplete();
        } else if (ui_menu_anim_state == UiShowHideAnimState.HIDE) {
            //complete hide animation
            _UI_MENU_OnHideComplete();
            if (ui_menu_anim_state == UiShowHideAnimState.SHOW) {
                //complete show animation
                _UI_MENU_OnShowComplete();
            }
        }
    }
    #endregion //[MENU]

    //Reward UI
    #region [REWARD]
    IEnumerator UI_RewardShowHighscore()
    {
#if DBG_TRACE_ROUTINES
        Log("Routine", "==>>: {0}", "ShowHighscore");
#endif
        UI_STATE_SwitchTo(UiState.HIDDEN);
        UI_MENU_SwitchTo(FB.IsLoggedIn ? UiMenuPage.HIGHSCORE : UiMenuPage.HIDDEN);
        UI_MENU_SetInteractable(false);
        ui_hs_sharebtn_go.SetActive(false);
        //audio
        AUD_PlayMusic(MusicType.UI_REWARD);
        AUD_UI_Sound(UiSoundType.BOX_OPEN);
        AUD_UI_Sound(UiSoundType.REWARD_ITEM);

        //capture
        ui_score_value = us.highscore;

        //show root
        ui_hs_root_go.SetActive(true);

        //show playchar 3d
        var playchar = SelectedUiPlaychar();
        playchar.transform.SetParent(ui_hs_char_tr, false);
        playchar.SetActive(true);
        var playchar_animation = playchar.GetComponent<AnimationController>();

        //animation
        //playchar_animation.enabled = true;
        playchar_animation.PlaySequence(UI_PLAYCHAR_ANIM_HIGHSCORE);
        ui_hs_score_go.GetComponent<Animation>().StateAt(0).speed = 1f;
        ui_hs_score_go.GetComponent<Animation>().Play();
        ui_hs_desc_go.GetComponent<Animation>().Play();
        ui_hs_char_tr.GetComponent<Animation>().Play();

        //tween
        ui_value_tween.CompleteAndClear(true);
        ui_value_tween.SetValues(0f, ui_score_value);
        ui_value_tween.SetSetter((value) => ui_hs_score_value_txt.text = ((int)value).ToString());
        ui_value_tween.Restart(0.5f, 1.0f);
        //animation tween
        ui_value_rev_tween.CompleteAndClear(true);
        ui_value_rev_tween.SetValues(1.5f, AnimationController.MIN_SPEED * 1.2f);
        ui_value_rev_tween.SetSetter((value) => playchar_animation.SetSequenceSpeedMultOverride(value));
        ui_value_rev_tween.Restart(1.8f);

        while (ui_value_tween.IsEnabled()) {
            yield return null;
        }

        ui_time_waiter.ResetBreak();
        yield return ui_time_waiter.Reset(1.0f);

        //show share button
        if (FB.IsLoggedIn) {
            ui_hs_sharebtn_go.SetActive(true);
            UI_MENU_SetInteractable(true);
        }
        ui_menu_backbtn_coroutine_wait = FB.IsLoggedIn;

        //wait for leave
        while (ui_menu_backbtn_coroutine_wait) {
            yield return null;
        }

        //leave
        //reset playchar animation
        ui_value_tween.CompleteAndClear(true);
        ui_value_rev_tween.CompleteAndClear(true);
        SelectedUiPlaychar().GetComponent<AnimationController>().SetSequenceSpeedMultOverride(0);

        //hide root
        ui_hs_root_go.SetActive(false);

#if DBG_TRACE_ROUTINES
        Log("Routine", "<<==: {0}", "ShowHighscore");
#endif
    }
    IEnumerator UI_RewardShowItem(UserReward rew)
    {
#if DBG_TRACE_ROUTINES
        Log("Routine", "==>>: {0}", "RewardShowItem");
#endif
        //show root
        ui_reward_root_go.SetActive(true);

        if (rew.type == UserInvItemType.MBOX || rew.type == UserInvItemType.SMBOX) {
            //show box
            ui_reward_mbox_root_go.SetActive(true);
            ui_reward_mbox_active_go = (rew.type == UserInvItemType.MBOX) ? ui_reward_mbox_go : ui_reward_smbox_go;
            ui_reward_mbox_active_go.SetActive(true);
            //set animation to closed state
            ui_reward_mbox_active_go.GetComponent<Animation>().SampleAt(0);
            //hide item node
            ui_reward_item_root_go.SetActive(false);
            ui_reward_desc_root_go.SetActive(false);

            //screen btn
            bool box_opened = false;
            ui_reward_scrbtn.onClick.RemoveAllListeners();
            ui_reward_scrbtn.onClick.AddListener(() => box_opened = true);
            ui_reward_scrbtn.interactable = true;

            //wait for box open
            while (!box_opened) {
                yield return null;
            }

            //screen btn
            ui_reward_scrbtn.interactable = false;

            //AUDIO
            AUD_UI_Sound(UiSoundType.BOX_OPEN);

            //animation
            ui_reward_mbox_active_go.GetComponent<Animation>().Play();
            ui_reward_mbox_root_go.GetComponent<AnimationController>().ProceedState();

            //particles
            ui_reward_mbox_emitter.Emit(50);

            //select item
            rew = rew.type == UserInvItemType.MBOX ? reward_data.mystery_box.items[reward_data.mystery_box.RandomIndex()].rewards[0] : reward_data.super_mystery_box.items[reward_data.super_mystery_box.RandomIndex()].rewards[0];
            //possible wait to show item
        }

        //show item
        GameObject item_prefab = null;
        UserItemDesc item_desc = null;
        switch (rew.type) {
        case UserInvItemType.DROPS:
            item_prefab = SelectedPlaycharSlot().ui_reward_item_drop_prefab;
            item_desc = SelectedPlaycharSlot().ui_drop_desc;
            break;
        default:
            item_prefab = ui_so.reward_item_prefabs[rew.type];
            item_desc = ui_so.user_item_desc[rew.type];
            break;
        }
        GameObject item_go = Instantiate(item_prefab);
        item_go.transform.SetParent(ui_reward_item_node_tr, false);

        //item node
        ui_reward_item_root_go.SetActive(true);
        ui_reward_desc_root_go.SetActive(true);
        ui_reward_item_root_go.GetComponent<Animation>().StateAt(0).speed = 1f;
        ui_reward_desc_root_go.GetComponent<Animation>().StateAt(0).speed = 1f;
        ui_reward_item_root_go.GetComponent<Animation>().Play();
        ui_reward_desc_root_go.GetComponent<Animation>().Play();

        //item desc
        ui_reward_desc_name_loc.SetTerm(item_desc.name);
        ui_reward_desc_desc_loc.SetTerm(item_desc.desc);

        //tween
        ui_value_tween.CompleteAndClear(true);
        ui_value_tween.SetValues(0f, rew.amount);
        ui_value_tween.SetSetter((value) => ui_reward_desc_value_txt.text = ((int)value).ToString());
        ui_value_tween.Restart(0.8f, 1f);

        //screen btn
        ui_reward_scrbtn.onClick.RemoveAllListeners();
        ui_reward_scrbtn.onClick.AddListener(() => {
            //screenbtn
            ui_reward_scrbtn.interactable = false;
            ui_reward_item_root_go.GetComponent<Animation>().StateAt(0).speed = 2.5f;
            ui_reward_desc_root_go.GetComponent<Animation>().StateAt(0).speed = 2.5f;
        });
        ui_reward_scrbtn.interactable = true;

        //AUDIO
        AUD_UI_Sound(UiSoundType.REWARD_ITEM);

        //wait tween
        ui_time_waiter.ResetBreak();
        do {
            yield return ui_time_waiter.Reset(0.5f);
        } while (ui_value_tween.IsEnabled());

        //force value (shows 0 sometimes)
        ui_reward_desc_value_txt.text = rew.amount.ToString();
        //hide mbox root
        ui_reward_mbox_root_go.SetActive(false);

        //possible wait here

        //Allowed to close
        bool close = false;
        //screen btn
        ui_reward_scrbtn.onClick.RemoveAllListeners();
        ui_reward_scrbtn.onClick.AddListener(() => close = true);
        ui_reward_scrbtn.interactable = true;

        while (!close) {
            yield return null;
        }

        //destroy item
        GameObject.Destroy(item_go);

        //hide box
        if (ui_reward_mbox_active_go != null) {
            ui_reward_mbox_active_go.SetActive(false);
        }

        //hide root
        ui_reward_root_go.SetActive(false);

        //add item
        USER_AddItem(rew.type, rew.amount);

#if DBG_TRACE_ROUTINES
        Log("Routine", "<<==: {0}", "RewardShowItem");
#endif
    }
    #endregion //[REWARD]
    #endregion //[UI]

    #region [CurvedShader]
    /*[SetInEditor]*/
    public CurvedShaderScriptableObject cs_so = null;
    Material[] cs_char_materials = new Material[2];
    //GameTween<float> cs_x_tween = null;
    public void CS_Init()
    {
        for (int i = 0; i < cs_so.main_materials.Length; ++i) {
            cs_so.main_materials[i].SetVector("_QOffset", cs_so.z_offset);
            cs_so.main_materials[i].SetVector("_Sphere", cs_so.sphere_offset);
        }

        /*if (cs_x_tween == null) {
            cs_x_tween = new FloatTween();
            cs_x_tween.SetValues(-cs_so.max_x, cs_so.max_x);
            cs_x_tween.SetDelay(cs_so.x_wait);
            cs_x_tween.SetDuration(cs_so.x_time);
            cs_x_tween.SetTimeStepGetter(PlayingTime);
            cs_x_tween.SetOnComplete(CS_OnCompleteX);
        }
        cs_x_tween.Restart();*/

        //AddUpdateOnMT_Playing(CS_Update);
    }
    /*public void CS_Update()
    {
        cs_so.z_offset.x = cs_x_tween.UpdateAndGet();
        //inject shader values
        for (int i = 0; i < cs_so.main_materials.Length; ++i) {
            cs_so.main_materials[i].SetVector("_QOffset", cs_so.z_offset);
        }
        for (int i = 0; i < cs_char_materials.Length; ++i) {
            cs_char_materials[i].SetVector("_QOffset", cs_so.z_offset);
        }
    }
    void CS_OnCompleteX()
    {
        cs_x_tween.Reverse();
        cs_x_tween.Restart();
    }*/
    void CS_SetCharMaterial(int index, Material mat)
    {
        mat.SetVector("_QOffset", cs_so.z_offset);
        mat.SetVector("_Sphere", cs_so.sphere_offset);
        cs_char_materials[index] = mat;
    }
    #endregion

    #region [Audio]
    public const string AUDIOBUS_PLAYCHAR = "Playchar";
    public const string AUDIOBUS_CHASER = "Chaser";
    public const string AUDIOBUS_COINS = "Coins";

    public const string AUDIOGROUP_PLAYCHAR_ACTION = "PlAction";
    public const string AUDIOGROUP_PLAYCHAR_DROP = "PlDrop";
    public const string AUDIOGROUP_PLAYCHAR_STRAFE = "PlStrafe";
    public const string AUDIOGROUP_PLAYCHAR_JUMP = "PlJump";
    public const string AUDIOGROUP_PLAYCHAR_PUSHTG = "PlPushtg";
    public const string AUDIOGROUP_PLAYCHAR_LAND = "PlLand";
    public const string AUDIOGROUP_PLAYCHAR_BUMP = "PlBump";
    public const string AUDIOGROUP_PLAYCHAR_CRASH = "PlCrash";
    public const string AUDIOGROUP_PLAYCHAR_TIRED = "PlTired";
    public const string AUDIOGROUP_PLAYCHAR_LUCKY = "PlLucky";

    public const string AUDIOGROUP_CHASER_ACTION = "ChsrAction";
    public const string AUDIOGROUP_CHASER_SHOUT = "ChsrShout";
    public const string AUDIOGROUP_CHASER_BUMP = "ChsrBump";
    public const string AUDIOGROUP_CHASER_CRASH = "ChsrCrash";
    public const string AUDIOGROUP_CHASER_CRASH_LOSE = "ChsrCrashLose";
    public const string AUDIOGROUP_CHASER_LAND = "ChsrLand";
    public const string AUDIOGROUP_CHASER_ATTACK = "ChsrAttack";
    public const string AUDIOGROUP_CHASER_DROPHIT = "ChsrDropHit";
    public const string AUDIOGROUP_CHASER_DROPSLIDE = "ChsrDropSlide";

    public const string AUDIOGROUP_COINS_SMALL = "CoinSmall";
    public const string AUDIOGROUP_COINS_MEDIUM = "CoinMedium";
    public const string AUDIOGROUP_COINS_ITEM = "CoinItem";
    public const string AUDIOGROUP_COINS_POWERUP = "CoinPwrup";
    public const string AUDIOGROUP_COINS_SPECIAL = "CoinSpec";

    public const string AUDIOGROUP_UI_BUTTON = "UiBtn";
    public const string AUDIOGROUP_UI_BACK = "UiBack";
    public const string AUDIOGROUP_UI_BUY = "UiBuy";
    public const string AUDIOGROUP_UI_JOYSTICK = "UiJoystick";
    public const string AUDIOGROUP_UI_NOTE = "UiNote";
    public const string AUDIOGROUP_UI_NOTEWIN = "UiNoteWin";
    public const string AUDIOGROUP_UI_BOXOPEN = "UiBoxOpen";
    public const string AUDIOGROUP_UI_REWARDITEM = "UiRewardItem";
    public const string AUDIOGROUP_UI_ITEMENDED = "UiItemEnded";
    public const string AUDIOGROUP_UI_UNPAUSE = "UiUnpause";

    public const string MUSIC_CONTROLLER = "MusicController";
    public const string MUSIC_MAIN = "mus_main";
    public const string MUSIC_UI_REWARD = "mus_ui_reward";
    public const string MUSIC_UI_SHOP = "mus_ui_shop";
    public const string MUSIC_PLAYING_NORMAL = "mus_play_normal";
    public const string MUSIC_PLAYING_CHASER = "mus_play_chaser";
    public const string MUSIC_PLAYING_REST = "mus_play_rest";
    public const string MUSIC_CUTSCENE_INTRO_EJDER = "mus_cut_intro_ejder";
    public const string MUSIC_CUTSCENE_INTRO_MEHRIBAN = "mus_cut_intro_mehriban";
    public const string MUSIC_CUTSCENE_CRASH_CHASER = "mus_cut_crash_chaser";
    public const string MUSIC_CUTSCENE_CRASH_OBSTACLE = "mus_cut_crash_obstacle";
    public const string MUSIC_CUTSCENE_REST_OUTSIDE = "mus_cut_rest_outside";
    public const string MUSIC_CUTSCENE_REST_INSIDE = "mus_cut_rest_inside";
    public const string MUSIC_CUTSCENE_CONTINUE_CHASER = "mus_cut_continue_chaser";
    public const string MUSIC_CUTSCENE_CONTINUE_OBSTACLE = "mus_cut_continue_obstacle";

    public enum UiSoundType
    {
        BUTTON,
        BACK,
        JOYSTICK,
        BUY,
        NOTE,
        NOTEWIN,
        BOX_OPEN,
        REWARD_ITEM,
        ITEM_ENDED,
        UNPAUSE
    }
    enum MusicType
    {
        NONE,

        BEGIN,
        END,

        PLAY_NORMAL,
        PLAY_CHASER,
        PLAY_REST,

        CUT_INTRO,
        CUT_CRASH_OBSTACLE,
        CUT_CRASH_CHASER,
        CUT_REST,
        CUT_CONTINUE_OBSTACLE,
        CUT_CONTINUE_CHASER,

        UI_REWARD,
        UI_SHOP,
    }
    public enum IntroMusicType
    {
        EJDER,
        MEHRIBAN
    }
    public enum RestMusicType
    {
        OUTSIDE,
        INSIDE
    }

    PlaylistController music_playlist = null;
    MusicType music_state = MusicType.NONE;
    //MusicType last_music_state = MusicType.NONE;
    void AUD_Init()
    {
        music_playlist = PlaylistController.InstanceByName(MUSIC_CONTROLLER);
#if CODEDEBUG
        if (music_playlist == null) {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogError(METHOD_NAME, "music_controller is NULL");
        }
#endif
    }

    float audio_small_coins_pitch = 1f;
    void AUD_COINS_Sound(CoinType coin)
    {
        switch (coin) {
        case CoinType.SMALL: MasterAudio.PlaySound(AUDIOGROUP_COINS_SMALL, pitch: audio_small_coins_pitch); break;
        case CoinType.MEDIUM: MasterAudio.PlaySound(AUDIOGROUP_COINS_MEDIUM); break;
        case CoinType.DROP:
        case CoinType.COINSX:
        case CoinType.LUCKX:
        case CoinType.SCOREX:
        case CoinType.MBOX:
        case CoinType.SMBOX: MasterAudio.PlaySound(AUDIOGROUP_COINS_ITEM); break;
        case CoinType.MAGNET:
        case CoinType.STAMINA:
        case CoinType.LUCK: MasterAudio.PlaySound(AUDIOGROUP_COINS_POWERUP); break;
        case CoinType.LETTER:
        case CoinType.SYMBOL: MasterAudio.PlaySound(AUDIOGROUP_COINS_SPECIAL); break;
        }
    }
    public void AUD_UI_Sound(UiSoundType type)
    {
        switch (type) {
        case UiSoundType.BUTTON: MasterAudio.PlaySound(AUDIOGROUP_UI_BUTTON); break;
        case UiSoundType.BACK: MasterAudio.PlaySound(AUDIOGROUP_UI_BACK); break;
        case UiSoundType.BUY: MasterAudio.PlaySound(AUDIOGROUP_UI_BUY); break;
        case UiSoundType.JOYSTICK: MasterAudio.PlaySound(AUDIOGROUP_UI_JOYSTICK); break;
        case UiSoundType.NOTE: MasterAudio.PlaySound(AUDIOGROUP_UI_NOTE); break;
        case UiSoundType.NOTEWIN: MasterAudio.PlaySound(AUDIOGROUP_UI_NOTEWIN); break;
        case UiSoundType.REWARD_ITEM: MasterAudio.PlaySound(AUDIOGROUP_UI_REWARDITEM); break;
        case UiSoundType.BOX_OPEN: MasterAudio.PlaySound(AUDIOGROUP_UI_BOXOPEN); break;
        case UiSoundType.ITEM_ENDED: MasterAudio.PlaySound(AUDIOGROUP_UI_ITEMENDED); break;
        case UiSoundType.UNPAUSE: MasterAudio.PlaySound(AUDIOGROUP_UI_UNPAUSE); break;
        }
    }

    void AUD_PLAYING_UpdateMusic()
    {
        switch (chaser_ctr.CurrentChaseState()) {
        case ChaseState.CHASE_ONSCREEN: AUD_PlayMusic(MusicType.PLAY_CHASER); break;
        case ChaseState.CHASE_OFFSCREEN: AUD_PlayMusic(MusicType.PLAY_NORMAL); break;
        case ChaseState.DISABLED: AUD_PlayMusic(MusicType.PLAY_REST); break;
        }
    }
    void AUD_PlayMusic(MusicType type)
    {
        if (music_state == type) return;
        if (music_state == MusicType.UI_SHOP && type == MusicType.BEGIN) return;
        if (music_state == MusicType.END && type == MusicType.BEGIN) return;

        switch (type) {
        case MusicType.BEGIN:
        case MusicType.END: music_playlist.TriggerPlaylistClip(MUSIC_MAIN); break;
        case MusicType.PLAY_CHASER: music_playlist.TriggerPlaylistClip(MUSIC_PLAYING_CHASER); break;
        case MusicType.PLAY_NORMAL: music_playlist.TriggerPlaylistClip(MUSIC_PLAYING_NORMAL); break;
        case MusicType.PLAY_REST: music_playlist.TriggerPlaylistClip(MUSIC_PLAYING_REST); break;
        case MusicType.CUT_INTRO: music_playlist.TriggerPlaylistClip(AUD_IntroMusicName()); break;
        case MusicType.CUT_CRASH_OBSTACLE: music_playlist.TriggerPlaylistClip(MUSIC_CUTSCENE_CRASH_OBSTACLE); break;
        case MusicType.CUT_CRASH_CHASER: music_playlist.TriggerPlaylistClip(MUSIC_CUTSCENE_CRASH_CHASER); break;
        case MusicType.CUT_CONTINUE_OBSTACLE: music_playlist.TriggerPlaylistClip(MUSIC_CUTSCENE_CONTINUE_OBSTACLE); break;
        case MusicType.CUT_CONTINUE_CHASER: music_playlist.TriggerPlaylistClip(MUSIC_CUTSCENE_CONTINUE_CHASER); break;
        case MusicType.CUT_REST: music_playlist.TriggerPlaylistClip(AUD_RestMusicName()); break;
        case MusicType.UI_REWARD: music_playlist.TriggerPlaylistClip(MUSIC_UI_REWARD); break;
        case MusicType.UI_SHOP: music_playlist.TriggerPlaylistClip(MUSIC_UI_SHOP); break;
        default: music_playlist.StopPlaylist(); break;
        }
        //last_music_state = music_state;
        music_state = type;
    }
    string AUD_IntroMusicName()
    {
        switch (SelectedPlaycharSlot().intro_music_type) {
        case IntroMusicType.EJDER: return MUSIC_CUTSCENE_INTRO_EJDER;
        case IntroMusicType.MEHRIBAN:
        default: return MUSIC_CUTSCENE_INTRO_MEHRIBAN;
        }
    }
    string AUD_RestMusicName()
    {
        switch (PP_GetCurrentZones().rest_music_type) {
        case RestMusicType.OUTSIDE: return MUSIC_CUTSCENE_REST_OUTSIDE;
        case RestMusicType.INSIDE:
        default: return MUSIC_CUTSCENE_REST_INSIDE;
        }
    }
    void AUD_CUTSCENE_PlayMusic()
    {
        switch (cut_state) {
        case CutsceneState.INTRO: AUD_PlayMusic(MusicType.CUT_INTRO); break;
        case CutsceneState.CRASH_CHASER: AUD_PlayMusic(MusicType.CUT_CRASH_CHASER); break;
        case CutsceneState.CRASH_OBSTACLE: AUD_PlayMusic(MusicType.CUT_CRASH_OBSTACLE); break;
        case CutsceneState.CONTINUE_CHASER: AUD_PlayMusic(MusicType.CUT_CONTINUE_CHASER); break;
        case CutsceneState.CONTINUE_OBSTACLE: AUD_PlayMusic(MusicType.CUT_CONTINUE_OBSTACLE); break;
        case CutsceneState.REST: AUD_PlayMusic(MusicType.CUT_REST); break;
        }
    }
    #endregion

    #region [Tutorial]
    IEnumerator GAME_TUTORIAL()
    {
        const float SWIPE_TIMEOUT = 1f;

        float tut_time_scale = 1f;
        bool tut_within_collider = false;

        //UI
        Transform ui_root = transform.Find(UI_ROOT_NAME);
        //UI tut root
        var ui_tut_root_go = GameObject.Instantiate(ui_so.ui_tutorial_prefab) as GameObject;
        Transform ui_tut_root_tr = ui_tut_root_go.transform;
        ui_tut_root_tr.SetParent(ui_root, false);
        //coins
        var ui_tut_coins_go = ui_tut_root_tr.Find("Coins").gameObject;
        var ui_tut_coins_txt = ui_tut_coins_go.transform.Find("Value").GetComponent<Text>();
        ui_tut_coins_txt.text = (297).ToString();
        //rest btn
        var ui_tut_rest_btn_go = ui_tut_root_tr.Find("RestBtn").gameObject;
        ui_tut_rest_btn_go.SetActive(false);
        //hands
        var ui_tut_hands = ui_tut_root_tr.Find("Hands");
        ui_tut_hands.gameObject.SetActive(true);
        for (int i = 0; i < ui_tut_hands.childCount; ++i) {
            ui_tut_hands.GetChild(i).gameObject.SetActive(false);
        }
        GameObject swipe_right_hand_go = ui_tut_hands.Find("SwipeRight").gameObject;
        GameObject swipe_left_hand_go = ui_tut_hands.Find("SwipeLeft").gameObject;
        GameObject swipe_up_hand_go = ui_tut_hands.Find("SwipeUp").gameObject;
        GameObject swipe_down_hand_go = ui_tut_hands.Find("SwipeDown").gameObject;
        GameObject swipe_tap_hand_go = ui_tut_hands.Find("SwipeTap").gameObject;
        //stamina
        GameObject ui_tut_stamina_go = ui_tut_root_tr.Find("Stamina").gameObject;
        ui_tut_stamina_go.SetActive(false);
        RectTransform ui_tut_stamina_rect = ui_tut_stamina_go.transform.Find("Bar").GetComponent<RectTransform>();
        float ui_tut_stamina_rect_width = ui_tut_stamina_rect.GetWidth();
        GameTween<float> ui_tut_stamina_tween = new FloatTween();
        ui_tut_stamina_tween.SetAutoUpdate(TweenAutoUpdateMode.APPLY);
        ui_tut_stamina_tween.SetSetter((value) => ui_tut_stamina_rect.SetWidth(value));
        //drops item
        GameObject ui_tut_drops_go = ui_tut_root_tr.Find("Drops").gameObject;
        ui_tut_drops_go.SetActive(false);
        Text ui_tut_drops_txt = ui_tut_drops_go.transform.Find("Value").GetComponent<Text>();
        ui_tut_drops_txt.text = (0).ToString();
        //fader
        GameObject ui_tut_fader_go = ui_tut_root_tr.Find("Fader").gameObject;
        ui_tut_fader_go.SetActive(true);
        CanvasGroup ui_tut_fader_canvas = ui_tut_fader_go.GetComponent<CanvasGroup>();
        ui_tut_fader_canvas.alpha = 1f;
        GameTween<float> ui_tut_fader_tween = new FloatTween();
        ui_tut_fader_tween.SetAutoUpdate(TweenAutoUpdateMode.APPLY);
        ui_tut_fader_tween.SetSetter((value) => ui_tut_fader_canvas.alpha = value);
        ui_tut_fader_tween.SetDuration(1f);
        //rest
        Transform ui_tut_rest_root_tr = ui_tut_root_tr.Find("Rest");
        //rest stamina
        Transform ui_tut_rest_stamina_tr = ui_tut_rest_root_tr.Find("Stamina");
        ui_tut_rest_stamina_tr.gameObject.SetActive(false);
        RectTransform ui_tut_rest_stamina_rect = ui_tut_rest_stamina_tr.Find("Bar").GetComponent<RectTransform>();
        float ui_tut_rest_stamina_rect_width = ui_tut_rest_stamina_rect.GetWidth();
        GameTween<float> ui_tut_rest_stamina_tween = new FloatTween();
        ui_tut_rest_stamina_tween.SetAutoUpdate(TweenAutoUpdateMode.APPLY);
        ui_tut_rest_stamina_tween.SetSetter((value) => ui_tut_rest_stamina_rect.SetWidth(value));
        ui_tut_rest_stamina_tween.SetEase(Easing.QuadOut);
        ui_tut_rest_stamina_tween.SetValues(0f, ui_tut_rest_stamina_rect_width);
        ui_tut_rest_stamina_tween.SetDuration(2f);
        //rest result
        Transform ui_tut_rest_result_tr = ui_tut_rest_root_tr.Find("RestResults");
        GameObject ui_tut_rest_coins_go = ui_tut_rest_result_tr.Find("Coins").gameObject;
        ui_tut_rest_coins_go.SetActive(false);
        var ui_tut_rest_coins_anim = ui_tut_rest_coins_go.GetComponent<AnimationController>();
        var ui_tut_rest_coins_value_txt = ui_tut_rest_coins_go.transform.Find("Value").GetComponent<Text>();
        ui_tut_rest_coins_value_txt.text = (0).ToString();
        GameObject ui_tut_rest_nbcoins_go = ui_tut_rest_result_tr.Find("NBCoins").gameObject;
        ui_tut_rest_nbcoins_go.SetActive(false);
        var ui_tut_rest_nbcoins_anim = ui_tut_rest_nbcoins_go.GetComponent<AnimationController>();
        var ui_tut_rest_nbcoins_value_txt = ui_tut_rest_nbcoins_go.transform.Find("Value").GetComponent<Text>();
        ui_tut_rest_nbcoins_value_txt.text = (0).ToString();
        GameTween<float> tut_rest_coins_tween = new FloatTween();
        tut_rest_coins_tween.SetAutoUpdate(TweenAutoUpdateMode.APPLY);
        tut_rest_coins_tween.SetSetter((value) => ui_tut_rest_coins_value_txt.text = ((int)value).ToString());
        GameTween<float> tut_rest_nbcoins_tween = new FloatTween();
        tut_rest_nbcoins_tween.SetAutoUpdate(TweenAutoUpdateMode.APPLY);
        tut_rest_nbcoins_tween.SetSetter((value) => ui_tut_rest_nbcoins_value_txt.text = ((int)value).ToString());


        GameObject tut_root_go = GameObject.Instantiate(ui_so.tutorial_prefab) as GameObject;
        Transform tut_root_tr = tut_root_go.transform;

        //playchar
        var tut_playchar_tr = tut_root_tr.Find("playchar");
        var tut_playchar_pos_anim = tut_playchar_tr.GetComponent<AnimationController>();
        var tut_playchar_anim = tut_playchar_tr.Find("player_node").GetComponent<AnimationController>();
        var tut_playchar_shadow_go = tut_playchar_tr.Find("shadow").gameObject;
        Transform tut_playchar_particles_tr = tut_playchar_tr.Find("particles");
        ParticleHolder tut_par_playchar_drop = new ParticleHolder();
        tut_par_playchar_drop.SetParticles(tut_playchar_particles_tr.Find("drop"));
        ParticleHolder tut_par_chaser_crash = new ParticleHolder();
        tut_par_chaser_crash.SetParticles(tut_playchar_particles_tr.Find("chaser_crash"));
        ParticleHolder tut_par_chaser_crash_lose = new ParticleHolder();
        tut_par_chaser_crash_lose.SetParticles(tut_playchar_particles_tr.Find("chaser_crash_lose"));
        ParticleHolder tut_par_coin_collect = new ParticleHolder();
        tut_par_coin_collect.SetParticles(tut_playchar_particles_tr.Find("coin_collect_small"));
        ParticleHolder tut_par_super_collect = new ParticleHolder();
        tut_par_super_collect.SetParticles(tut_playchar_particles_tr.Find("coin_collect_yellow"));

        //chaser
        var tut_chaser_tr = tut_root_tr.Find("chaser");
        tut_chaser_tr.gameObject.SetActive(false);
        var tut_chaser_pos_anim = tut_chaser_tr.GetComponent<AnimationController>();
        var tut_chaser_anim = tut_chaser_tr.Find("chaser_node").GetComponent<AnimationController>();
        Transform tut_chaser_particles_tr = tut_chaser_tr.Find("particles");
        ParticleHolder tut_par_chaser_slide = new ParticleHolder();
        tut_par_chaser_slide.SetParticles(tut_chaser_particles_tr.Find("slide"));

        //camera
        var tut_camera_tr = tut_root_tr.Find("camera");

        //theme
        Transform tut_theme_root_tr = tut_root_tr.Find("theme_root");
        tut_theme_root_tr.SetLocalPositionZ(-2f);
        //coins
        var tut_coins_tr = tut_theme_root_tr.Find("coins");
        for (int i = 0; i < tut_coins_tr.childCount; ++i) {
            tut_coins_tr.GetChild(i).gameObject.SetActive(false);
        }
        //rest
        var tut_rest_root_go = tut_theme_root_tr.Find("rest_root").gameObject;
        tut_rest_root_go.SetActive(false);

        //time scale tween
        GameTween<float> tut_time_tween = new FloatTween();
        tut_time_tween.SetAutoUpdate(TweenAutoUpdateMode.APPLY);
        tut_time_tween.SetSetter((value) => tut_time_scale = value);
        tut_time_tween.SetValues(1f, 0f);
        tut_time_tween.SetEase(Easing.QuadIn);
        tut_time_scale = 1f;

        //delagates
        Event move_forward = () => {
            float tut_delta_time = frame_delta_time * tut_time_scale;
            float distance_traveled = 10 * tut_delta_time;
            tut_theme_root_tr.MoveLocalPositionZ(-distance_traveled);

            float playchar_posx = tut_playchar_tr.localPosition.x;
            float cam_posx = tut_camera_tr.localPosition.x;
            //cam_pos.x += (camera_offset_x_sensivity * (playchar_pos.x * camera_offset_x_mult - cam_pos.x));
            tut_camera_tr.MoveLocalPositionX(0.1f * (playchar_posx * 0.85f - cam_posx));
        };
        Event<Collider> within_obstacle = (value) => tut_within_collider = true;
        tut_playchar_tr.GetComponent<TutorialPlaycharController>().SetTriggerEnterEvent(within_obstacle);


        //TUTORIAL BEGIN
        UI_STATE_SwitchTo(UiState.HIDDEN);

        game_state = GameState.TUTORIAL;
        if (onGameStateChanged != null) { onGameStateChanged(); }

        //music
        AUD_PlayMusic(MusicType.PLAY_NORMAL);

        //fadein
        ui_tut_fader_go.SetActive(true);
        ui_tut_fader_tween.SetValues(1f, 0f);
        ui_tut_fader_tween.Restart();

        //move to first obstacle
        while (!tut_within_collider) {
            move_forward();
            yield return null;
        }

        //within first obstacle
        tut_within_collider = false;
        //start slowing down
        tut_time_tween.Restart();
        //show swipe right hand        
        swipe_right_hand_go.SetActive(true);
        //fadeout
        ui_tut_fader_tween.SetValues(0f, 0.3f);
        ui_tut_fader_tween.Restart(1f);
        //wait for swipe
        float time_wait = Time.realtimeSinceStartup + SWIPE_TIMEOUT;
        while (!CheckSwipeInput(SwipeDir.Right) && Time.realtimeSinceStartup < time_wait) {
            move_forward();
            tut_playchar_anim.SetSequenceSpeedMultOverride(System.Math.Max(tut_time_scale, AnimationController.MIN_SPEED + 0.01f));
            yield return null;
        }
        //swipe right
        swipe_right_hand_go.SetActive(false);
        //sound
        MasterAudio.PlaySound(AUDIOGROUP_PLAYCHAR_STRAFE);
        //fadein
        ui_tut_fader_go.SetActive(false);
        //playchar animation
        tut_playchar_pos_anim.PlaySequence(0);
        tut_playchar_anim.PlaySequence(1);
        //restore time scale
        tut_time_tween.SetEnabled(false);
        tut_time_scale = 1f;
        tut_playchar_anim.SetSequenceSpeedMultOverride(0);

        //move to second obstacle
        while (!tut_within_collider) {
            move_forward();
            yield return null;
        }

        //within second obstacle
        tut_within_collider = false;
        //start slowing down
        tut_time_tween.Restart();
        //show swipe left hand
        swipe_left_hand_go.SetActive(true);
        //fadeout
        ui_tut_fader_go.SetActive(true);
        ui_tut_fader_tween.Restart(0.5f);
        //wait for swipe
        time_wait = Time.realtimeSinceStartup + SWIPE_TIMEOUT;
        while (!CheckSwipeInput(SwipeDir.Left) && Time.realtimeSinceStartup < time_wait) {
            move_forward();
            tut_playchar_anim.SetSequenceSpeedMultOverride(System.Math.Max(tut_time_scale, AnimationController.MIN_SPEED + 0.01f));
            yield return null;
        }
        //swipe left
        swipe_left_hand_go.SetActive(false);
        //sound
        MasterAudio.PlaySound(AUDIOGROUP_PLAYCHAR_STRAFE);
        //fadein
        ui_tut_fader_go.SetActive(false);
        //playchar_animation
        tut_playchar_pos_anim.PlaySequence(1);
        tut_playchar_anim.PlaySequence(2);
        //restore time scale
        tut_time_tween.SetEnabled(false);
        tut_time_scale = 1f;
        tut_playchar_anim.SetSequenceSpeedMultOverride(0);

        //move to third obstacle
        while (!tut_within_collider) {
            move_forward();
            yield return null;
        }

        //within third obstacle
        tut_within_collider = false;
        //start slowing down
        tut_time_tween.Restart();
        //show jump hand
        swipe_up_hand_go.SetActive(true);
        //fadeout
        ui_tut_fader_go.SetActive(true);
        ui_tut_fader_tween.Restart(0.5f);
        //wait for swipe
        time_wait = Time.realtimeSinceStartup + SWIPE_TIMEOUT;
        while (!CheckSwipeInput(SwipeDir.Up) && Time.realtimeSinceStartup < time_wait) {
            move_forward();
            tut_playchar_anim.SetSequenceSpeedMultOverride(System.Math.Max(tut_time_scale, AnimationController.MIN_SPEED + 0.01f));
            yield return null;
        }
        //swipe up
        swipe_up_hand_go.SetActive(false);
        //shadow
        tut_playchar_shadow_go.SetActive(false);
        //sound
        MasterAudio.PlaySound(AUDIOGROUP_PLAYCHAR_STRAFE);
        //fadein
        ui_tut_fader_go.SetActive(false);
        //playchar animation
        tut_playchar_pos_anim.PlaySequence(2);
        tut_playchar_anim.PlaySequence(3);
        //restore time scale
        tut_time_tween.SetEnabled(false);
        tut_time_scale = 1f;
        tut_playchar_anim.SetSequenceSpeedMultOverride(0);

        //wait to land
        time_wait = Time.realtimeSinceStartup + tut_playchar_pos_anim.GetClipLength(2);
        while (Time.realtimeSinceStartup < time_wait) {
            move_forward();
            yield return null;
        }
        //land
        //shadow
        tut_playchar_shadow_go.SetActive(true);
        //playchar animation
        tut_playchar_anim.ProceedState();

        //move to stamina end collider
        while (!tut_within_collider) {
            move_forward();
            yield return null;
        }
        //within stamina end
        tut_within_collider = false;
        ui_tut_stamina_go.SetActive(true);
        ui_tut_stamina_tween.SetValues(ui_tut_stamina_rect_width, 0f);
        ui_tut_stamina_tween.Restart(2f);
        //time scale
        /*tut_time_tween.SetValues(1f, 0.5f);
        tut_time_tween.Restart();*/
        //wait stamina to complete
        time_wait = Time.realtimeSinceStartup + 1f;
        while (Time.realtimeSinceStartup < time_wait) {
            move_forward();
            yield return null;
        }
        //chaser enter screen
        tut_chaser_tr.gameObject.SetActive(true);
        tut_chaser_pos_anim.PlaySequence(0);
        tut_chaser_anim.PlaySequence(0);
        //music
        AUD_PlayMusic(MusicType.PLAY_CHASER);
        //wait chaser to appear
        time_wait = Time.realtimeSinceStartup + 2f;
        while (Time.realtimeSinceStartup < time_wait) {
            move_forward();
            yield return null;
        }

        //move to fourth obstacle
        while (!tut_within_collider) {
            move_forward();
            yield return null;
        }
        //within fourth obstacle
        tut_within_collider = false;
        //start slowing down
        //tut_time_tween.SetValues(tut_time_tween.CurrentValue(), 0f);
        tut_time_tween.Restart();
        //show slide hand
        swipe_down_hand_go.SetActive(true);
        //fadeout
        ui_tut_fader_go.SetActive(true);
        ui_tut_fader_tween.Restart();
        //wait for swipe
        time_wait = Time.realtimeSinceStartup + SWIPE_TIMEOUT;
        while (!CheckSwipeInput(SwipeDir.Down) && Time.realtimeSinceStartup < time_wait) {
            move_forward();
            float anim_speed_scale = System.Math.Max(tut_time_scale, AnimationController.MIN_SPEED + 0.01f);
            tut_playchar_anim.SetSequenceSpeedMultOverride(anim_speed_scale);
            tut_chaser_anim.SetSequenceSpeedMultOverride(anim_speed_scale);
            yield return null;
        }
        //swipe down
        swipe_down_hand_go.SetActive(false);
        //sound
        MasterAudio.PlaySound(AUDIOGROUP_PLAYCHAR_STRAFE);
        //fadein
        ui_tut_fader_go.SetActive(false);
        //playchar animation
        tut_playchar_anim.PlaySequence(4);
        //restore time scale
        tut_time_tween.SetEnabled(false);
        tut_time_scale = 1f;
        tut_playchar_anim.SetSequenceSpeedMultOverride(0);
        tut_chaser_anim.SetSequenceSpeedMultOverride(0);


        //wait for chaser crash
        time_wait = Time.realtimeSinceStartup + 0.45f;
        while (Time.realtimeSinceStartup < time_wait) {
            move_forward();
            yield return null;
        }
        //chaser crash
        tut_chaser_pos_anim.PlaySequence(2);
        tut_par_chaser_crash.Play();
        //music
        AUD_PlayMusic(MusicType.PLAY_NORMAL);
        //sound
        MasterAudio.PlaySound(AUDIOGROUP_CHASER_CRASH);


        //wait to show coins
        time_wait = Time.realtimeSinceStartup + 1f;
        while (Time.realtimeSinceStartup < time_wait) {
            move_forward();
            yield return null;
        }
        //show coins
        int current_coin = 0;
        while (current_coin < 4) {
            tut_coins_tr.GetChild(current_coin).gameObject.SetActive(true);
            time_wait = Time.realtimeSinceStartup + 0.2f;
            while (Time.realtimeSinceStartup < time_wait) {
                move_forward();
                yield return null;
            }
            ++current_coin;
        }
        //pick coins
        current_coin = 0;
        while (current_coin < 3) {
            //wait for coin pick
            while (!tut_within_collider) {
                move_forward();
                yield return null;
            }
            //coin pick
            tut_within_collider = false;
            tut_coins_tr.GetChild(current_coin).gameObject.SetActive(false);
            ui_tut_coins_txt.text = ((++current_coin) + 297).ToString();
            //particle
            tut_par_coin_collect.Play();
            //sound
            MasterAudio.PlaySound(AUDIOGROUP_COINS_SMALL);
        }
        //wait for luck coin pick
        while (!tut_within_collider) {
            move_forward();
            yield return null;
        }
        //luck drop pick
        tut_within_collider = false;
        tut_coins_tr.GetChild(3).gameObject.SetActive(false);
        ui_tut_drops_go.SetActive(true);
        ui_tut_drops_txt.text = (10).ToString();
        //particles
        tut_par_super_collect.Play();
        //sound
        MasterAudio.PlaySound(AUDIOGROUP_COINS_POWERUP);

        //chaser enter screen
        //music
        AUD_PlayMusic(MusicType.PLAY_CHASER);
        //wait for chaser enter
        tut_chaser_pos_anim.PlaySequence(0);
        time_wait = Time.realtimeSinceStartup + 2f;
        while (Time.realtimeSinceStartup < time_wait) {
            move_forward();
            yield return null;
        }
        //start slowing down
        tut_time_tween.Restart();
        //show tap hand
        swipe_tap_hand_go.SetActive(true);
        //fadeout
        ui_tut_fader_go.SetActive(true);
        ui_tut_fader_tween.Restart(0.5f);
        //wait for swipe
        time_wait = Time.realtimeSinceStartup + SWIPE_TIMEOUT;
        while (!CheckSwipeInput(SwipeDir.Tap) && Time.realtimeSinceStartup < time_wait) {
            move_forward();
            float speed_scale = System.Math.Max(tut_time_scale, AnimationController.MIN_SPEED + 0.01f);
            tut_playchar_anim.SetSequenceSpeedMultOverride(speed_scale);
            tut_chaser_anim.SetSequenceSpeedMultOverride(speed_scale);
            yield return null;
        }
        //swipe
        swipe_tap_hand_go.SetActive(false);
        //fadein
        ui_tut_fader_go.SetActive(false);
        //restore time scale
        tut_time_tween.SetEnabled(false);
        tut_time_scale = 1f;
        tut_playchar_anim.SetSequenceSpeedMultOverride(0);
        tut_chaser_anim.SetSequenceSpeedMultOverride(0);

        //chaser animation
        tut_chaser_anim.PlaySequence(1);
        tut_par_chaser_slide.Play();
        //playchar animation
        for (int i = 0; i < 1; ++i) {
            //playchar_animation
            tut_playchar_anim.PlaySequence(5);
            tut_par_playchar_drop.Play();
            //sound
            MasterAudio.PlaySound(AUDIOGROUP_PLAYCHAR_DROP);
            //wait
            time_wait = Time.realtimeSinceStartup + 0.25f;
            while (Time.realtimeSinceStartup < time_wait) {
                move_forward();
                yield return null;
            }
        }
        //playchar run
        tut_playchar_anim.PlaySequence(0);

        //wait chaser slide
        time_wait = Time.realtimeSinceStartup + 1f;
        while (Time.realtimeSinceStartup < time_wait) {
            move_forward();
            yield return null;
        }
        //chaser leave screen
        tut_chaser_pos_anim.PlaySequence(1);
        //wait chaser leave screen
        time_wait = Time.realtimeSinceStartup + 1f;
        while (Time.realtimeSinceStartup < time_wait) {
            move_forward();
            yield return null;
        }
        //chaser crash particles and sound
        MasterAudio.PlaySound(AUDIOGROUP_CHASER_CRASH_LOSE);
        tut_par_chaser_crash_lose.Play();
        tut_par_chaser_slide.Stop();

        //show rest
        ui_tut_rest_btn_go.SetActive(true);
        //slowdown
        tut_time_tween.Restart();
        //wait for tap
        time_wait = Time.realtimeSinceStartup + SWIPE_TIMEOUT;
        while (!CheckSwipeInput(SwipeDir.Tap) && Time.realtimeSinceStartup < time_wait) {
            move_forward();
            float anim_speed_scale = System.Math.Max(tut_time_scale, AnimationController.MIN_SPEED + 0.01f);
            tut_playchar_anim.SetSequenceSpeedMultOverride(anim_speed_scale);
            yield return null;
        }
        //rest tap
        ui_tut_rest_btn_go.SetActive(false);
        ui_tut_coins_go.SetActive(false);
        ui_tut_drops_go.SetActive(false);
        //move scene
        tut_theme_root_tr.SetLocalPositionZ(-196f);
        //hide playchar
        tut_playchar_tr.gameObject.SetActive(false);
        tut_chaser_tr.gameObject.SetActive(false);
        //activate rest scene
        tut_rest_root_go.SetActive(true);
        //restore stamina
        ui_tut_rest_stamina_tr.gameObject.SetActive(true);
        ui_tut_rest_stamina_tr.GetComponent<Animation>().SampleAt(0);
        ui_tut_rest_stamina_tween.Restart();
        ui_tut_stamina_go.SetActive(false);
        //music sound
        AUD_PlayMusic(MusicType.UI_REWARD);
        MasterAudio.PlaySound(AUDIOGROUP_UI_BUTTON);

        //wait before results
        time_wait = Time.realtimeSinceStartup + 2f;
        while (Time.realtimeSinceStartup < time_wait) { yield return null; }

        //show rest results
        ui_tut_rest_coins_go.SetActive(true);
        ui_tut_rest_coins_anim.PlaySequence(0);
        //wait animation
        time_wait = Time.realtimeSinceStartup + 1f;
        while (Time.realtimeSinceStartup < time_wait) { yield return null; }
        //show nb coins
        ui_tut_rest_nbcoins_go.SetActive(true);
        ui_tut_rest_nbcoins_anim.PlaySequence(0);
        ui_tut_rest_nbcoins_value_txt.text = (300).ToString();
        //wait animation
        time_wait = Time.realtimeSinceStartup + 0.5f;
        while (Time.realtimeSinceStartup < time_wait) { yield return null; }
        //tween coins
        tut_rest_nbcoins_tween.SetValues(300f, 0);
        tut_rest_nbcoins_tween.Restart(1f);
        tut_rest_coins_tween.SetValues(0, 300f);
        tut_rest_coins_tween.Restart(1f);
        //wait tween
        time_wait = Time.realtimeSinceStartup + 2f;
        while (Time.realtimeSinceStartup < time_wait) { yield return null; }
        //animation
        ui_tut_rest_nbcoins_anim.ProceedState();
        //wait animation
        time_wait = Time.realtimeSinceStartup + 0.5f;
        while (Time.realtimeSinceStartup < time_wait) { yield return null; }
        //animation
        ui_tut_rest_coins_anim.ProceedState();
        //wait animation
        time_wait = Time.realtimeSinceStartup + 0.5f;
        while (Time.realtimeSinceStartup < time_wait) { yield return null; }
        //animation
        ui_tut_rest_stamina_tr.GetComponent<Animation>().Play();
        //wait animation
        time_wait = Time.realtimeSinceStartup + 0.5f;
        while (Time.realtimeSinceStartup < time_wait) { yield return null; }

        //TUTORIAL END

        //Leave
        //fadeout
        ui_tut_fader_go.SetActive(true);
        ui_tut_fader_tween.SetValues(0f, 1f);
        ui_tut_fader_tween.Restart(1f);
        //wait fader
        while (ui_tut_fader_tween.IsEnabled()) {
            yield return null;
        }

        GameObject.Destroy(ui_tut_root_go);
        GameObject.Destroy(tut_root_go);

        //disable tutorial
        config_data.show_tut = false;
        USER_SaveConfig();

        GAME_SwitchTo(GameState.BEGIN);
    }

    #endregion //[Tutorial]
}