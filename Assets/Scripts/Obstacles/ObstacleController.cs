using System.Collections;
using UnityEngine;
using FullInspector;

public enum ObstacleType
{
    THEME_OBSTACLE,
    //Vehicles
    VEHICLE_BUS,
    VEHICLE_CAR,
    VEHICLE_APPBUS,
    VEHICLE_APPCAR,
    //Barriers
    BARRIER,
    PLATFORM
}
public enum ObstacleConfiguration
{
    WALL,
    BOX
}
public class ObstacleSharedData : PoolSharedData
{
    /*[SetInEditor]*/
    [InspectorRange(0f, 6.0f)]
    public float floor_height = 0f;
    /*[SetInEditor]*/
    [InspectorRange(0.1f, 1.0f)]
    public float slope_factor = 1f;
    /*[SetInEditor]*/
    public ObstacleConfiguration configuration;
    /*[SetInEditor]*/
    public ObstacleType type;
}
public class ObstacleController : PoolObject
{
    ObstacleSharedData shared_data = null;
    public float FloorHeight() { return shared_data.floor_height; }
    public float SlopeFactor() { return shared_data.slope_factor; }
    public ObstacleConfiguration Configuration() { return shared_data.configuration; }
    public ObstacleType ObstType() { return shared_data.type; }

    public override void Pool_Init(PrefabPool parent, int index, PoolSharedData sharedData
#if CODEDEBUG
        , string debug_data
#endif
        )
    {
        base.Pool_Init(parent, index, sharedData
#if CODEDEBUG
            , debug_data
#endif
            );

        shared_data = sharedData as ObstacleSharedData;
#if CODEDEBUG
        if (shared_data == null) {
            string METHOD_NAME = GameController.ConsoleFormatModule(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name, System.Reflection.MethodBase.GetCurrentMethod().Name);
            GameController.LogError(METHOD_NAME, (debug_data + ".shared_data is NULL"));
        }
#endif
    }

    public virtual float TotalHeight()
    {
        return shared_data.floor_height;
    }
    public virtual void OnWithin()
    {
    }
    public virtual void OnStumble()
    {
    }
    public virtual void OnCrash()
    {
    }
    public virtual void OnSideBump()
    {
    }
    public virtual void OnFloor()
    {
    }
    public virtual void OnSlope()
    {
    }
}