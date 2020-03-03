using UnityEngine;
using System.Collections;
using FullInspector;

public class CameraController : BaseBehavior<FullSerializerSerializer>
{
    /*[SetInEditor]*/
    [InspectorRange(0.1f, 1.0f)]
    public float camera_offset_x_mult = 1.0f;
    /*[SetInEditor]*/
    [InspectorRange(0.1f, 1.0f)]
    public float camera_playchar_offset_y_mult = 1.0f;
    [InspectorRange(0.1f, 1.0f)]
    public float camera_floor_offset_y_mult = 1.0f;
    /*[SetInEditor]*/
    [InspectorRange(0.05f, 0.5f)]
    public float camera_offset_x_sensivity = 0.1f;
    /*[SetInEditor]*/
    [InspectorRange(0.05f, 0.5f)]
    public float camera_offset_y_sensivity = 0.1f;
    /*[SetInEditor]*/
    [InspectorRange(0.05f, 0.5f)]
    public float camera_offset_z_sensivity = 0.03f;
    /*[SetInEditor]*/
    [InspectorRange(0.0f, 20.0f)]
    public float camera_lookat_offset_z = 10.0f;
    Transform camera_offset_node = null;
    /*[SetInEditor]*/
    public float[] camera_offset_y = new float[2];
    /*[SetInEditor]*/
    public float[] camera_offset_z = new float[2];
    int camera_offset_index = 0;

    float floor_height = 0f;

#if UNITY_EDITOR
    [InspectorHeader("Animation Control"), InspectorMargin(20), InspectorDivider, ShowInInspector, InspectorHidePrimary]
    bool __inspector_animation;
#endif
    /*[SetInEditor]*/
    public int run_sequence_index = 0;
    /*[SetInEditor]*/
    public int jump_sequence_index = 0;
    /*[SetInEditor]*/
    public int land_sequence_index = 0;
    /*[SetInEditor]*/
    public int bump_sequence_index = 0;
    /*[SetInEditor]*/
    public int stumble_sequence_index = 0;

    AnimationController anim = null;
    GameController gc = null;
    Transform playchar = null;

    public void Start()
    {
        gc = GameController.Instance;
        gc.onPlaycharReady += OnPlaycharReady;
        gc.onPlaycharReleasing += OnPlaycharReleasing;
        gc.onPlayingStateChanged += OnPlayingStateChanged;
        gc.AddUpdateOnMT_Playing(UpdateOnPlaying);

        if (gc.CurrentPlayingState() == GameController.GamePlayingState.MAIN) {
            OnPlaycharReady();
        }

        camera_offset_node = transform.Find("camera_node");
        anim = camera_offset_node.GetComponent<AnimationController>();
        camera_offset_node = transform;
    }
    void UpdateOnPlaying()
    {
        Vector3 cam_pos = camera_offset_node.localPosition;
        Vector3 playchar_pos = playchar.localPosition;
        //smooth camera movement
        cam_pos.x += (camera_offset_x_sensivity * (playchar_pos.x * camera_offset_x_mult - cam_pos.x));
        cam_pos.y += (camera_offset_y_sensivity * ((playchar_pos.y * camera_playchar_offset_y_mult) + (floor_height * camera_floor_offset_y_mult) - cam_pos.y + camera_offset_y[camera_offset_index]));
        cam_pos.z += (camera_offset_z_sensivity * (playchar_pos.z - cam_pos.z + camera_offset_z[camera_offset_index]));
        camera_offset_node.localPosition = cam_pos;

        //look at (Caution! Modifying cam_pos)
        cam_pos = playchar_pos;
        cam_pos.z += camera_lookat_offset_z;
        camera_offset_node.LookAt(cam_pos);
    }
    void OnPlaycharReady()
    {
        PlayerController pl = gc.PlaycharCtrl();
        pl.onLand += OnPlaycharLand;
        pl.onSideBump += OnPlaycharBump;
        pl.onPosStateChanged += OnPlaycharPosStateChanged;
        gc.ChaserCtrl().onChaseStateChanged += OnChaseStateChanged;

        playchar = gc.PlaycharCtrl().transform;

        camera_offset_index = 0;
    }
    void OnPlaycharReleasing()
    {
        PlayerController pl = gc.PlaycharCtrl();
        pl.onLand -= OnPlaycharLand;
        pl.onSideBump -= OnPlaycharBump;
        pl.onPosStateChanged -= OnPlaycharPosStateChanged;

        gc.ChaserCtrl().onChaseStateChanged -= OnChaseStateChanged;

        playchar = null;
    }
    void OnPlaycharLand(float fall_height)
    {
        if (fall_height > gc.pch_so.playchar_roll_height_threshold) {
            //animation
            anim.PlaySequence(land_sequence_index);
        }
    }
    void OnPlaycharBump(ObstacleController obst)
    {
        //animation
        anim.PlaySequence(bump_sequence_index);
    }
    void OnPlaycharPosStateChanged(PosState state)
    {
        switch (state) {
        case PosState.JUMP_RISING:
            //animation jump
            anim.PlaySequence(jump_sequence_index);
            break;
        case PosState.GROUNDED_FLOOR:
        //case PosState.GROUNDED_SLOPE:
            floor_height = gc.PlaycharCtrl().FloorHeight();
            break;
        }
    }
    void OnChaseStateChanged(ChaseState state)
    {
        camera_offset_index = gc.ChaserCtrl().IsOnScreen() ? 1 : 0;
    }
    void OnPlayingStateChanged()
    {
        bool enable = gc.CurrentPlayingState() == GameController.GamePlayingState.MAIN;
        anim.enabled = enable;
        if (enable) {
            floor_height = gc.PlaycharCtrl().FloorHeight();
        }
    }
}
