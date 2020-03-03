using UnityEngine;
using System.Collections;

public static class RectTransformExtensions
{
    public static void SetDefaultScale(this RectTransform trans)
    {
        trans.localScale = new Vector3(1, 1, 1);
    }
    public static void SetPivotAndAnchors(this RectTransform trans, Vector2 aVec)
    {
        trans.pivot = aVec;
        trans.anchorMin = aVec;
        trans.anchorMax = aVec;
    }

    public static Vector2 GetSize(this RectTransform trans)
    {
        return trans.rect.size;
    }
    public static float GetWidth(this RectTransform trans)
    {
        return trans.rect.width;
    }
    public static float GetHeight(this RectTransform trans)
    {
        return trans.rect.height;
    }

    public static void SetPositionOfPivot(this RectTransform trans, Vector2 newPos)
    {
        trans.localPosition = new Vector3(newPos.x, newPos.y, trans.localPosition.z);
    }

    public static void SetLeftBottomPosition(this RectTransform trans, Vector2 newPos)
    {
        trans.localPosition = new Vector3(newPos.x + (trans.pivot.x * trans.rect.width), newPos.y + (trans.pivot.y * trans.rect.height), trans.localPosition.z);
    }
    public static void SetLeftTopPosition(this RectTransform trans, Vector2 newPos)
    {
        trans.localPosition = new Vector3(newPos.x + (trans.pivot.x * trans.rect.width), newPos.y - ((1f - trans.pivot.y) * trans.rect.height), trans.localPosition.z);
    }
    public static void SetRightBottomPosition(this RectTransform trans, Vector2 newPos)
    {
        trans.localPosition = new Vector3(newPos.x - ((1f - trans.pivot.x) * trans.rect.width), newPos.y + (trans.pivot.y * trans.rect.height), trans.localPosition.z);
    }
    public static void SetRightTopPosition(this RectTransform trans, Vector2 newPos)
    {
        trans.localPosition = new Vector3(newPos.x - ((1f - trans.pivot.x) * trans.rect.width), newPos.y - ((1f - trans.pivot.y) * trans.rect.height), trans.localPosition.z);
    }

    public static void SetSize(this RectTransform trans, Vector2 newSize)
    {
        Vector2 oldSize = trans.rect.size;
        Vector2 deltaSize = newSize - oldSize;
        trans.offsetMin = trans.offsetMin - new Vector2(deltaSize.x * trans.pivot.x, deltaSize.y * trans.pivot.y);
        trans.offsetMax = trans.offsetMax + new Vector2(deltaSize.x * (1f - trans.pivot.x), deltaSize.y * (1f - trans.pivot.y));
    }
    public static void SetWidth(this RectTransform trans, float newSize)
    {
        SetSize(trans, new Vector2(newSize, trans.rect.size.y));
    }
    public static void SetHeight(this RectTransform trans, float newSize)
    {
        SetSize(trans, new Vector2(trans.rect.size.x, newSize));
    }
}
public static class TransformExtensions
{
    public static void SetPositionX(this Transform tr, float value)
    {
        Vector3 pos = tr.position;
        pos.x = value;
        tr.position = pos;
    }
    public static void SetPositionY(this Transform tr, float value)
    {
        Vector3 pos = tr.position;
        pos.y = value;
        tr.position = pos;
    }
    public static void SetPositionZ(this Transform tr, float value)
    {
        Vector3 pos = tr.position;
        pos.z = value;
        tr.position = pos;
    }
    public static void SetLocalPositionX(this Transform tr, float value)
    {
        Vector3 pos = tr.localPosition;
        pos.x = value;
        tr.localPosition = pos;
    }
    public static void SetLocalPositionY(this Transform tr, float value)
    {
        Vector3 pos = tr.localPosition;
        pos.y = value;
        tr.localPosition = pos;
    }
    public static void SetLocalPositionZ(this Transform tr, float value)
    {
        Vector3 pos = tr.localPosition;
        pos.z = value;
        tr.localPosition = pos;
    }
    public static void MovePositionX(this Transform tr, float value)
    {
        Vector3 pos = tr.position;
        pos.x += value;
        tr.position = pos;
    }
    public static void MovePositionY(this Transform tr, float value)
    {
        Vector3 pos = tr.position;
        pos.y += value;
        tr.position = pos;
    }
    public static void MovePositionZ(this Transform tr, float value)
    {
        Vector3 pos = tr.position;
        pos.z += value;
        tr.position = pos;
    }
    public static void MoveLocalPositionX(this Transform tr, float value)
    {
        Vector3 pos = tr.localPosition;
        pos.x += value;
        tr.localPosition = pos;
    }
    public static void MoveLocalPositionY(this Transform tr, float value)
    {
        Vector3 pos = tr.localPosition;
        pos.y += value;
        tr.localPosition = pos;
    }
    public static void MoveLocalPositionZ(this Transform tr, float value)
    {
        Vector3 pos = tr.localPosition;
        pos.z += value;
        tr.localPosition = pos;
    }
}

public struct MinMax<TElement>
{
    public TElement Min;
    public TElement Max;

    public TElement MinLimit;
    public TElement MaxLimit;

    /// <summary>
    /// Resets the Min and Max values to the MinLimit.
    /// </summary>
    public void ResetMin()
    {
        Min = MinLimit;
        Max = MinLimit;
    }
}

namespace FullInspector
{
    [System.AttributeUsage(System.AttributeTargets.Field | System.AttributeTargets.Property)]
    public sealed class InspectorStepRangeAttribute : System.Attribute
    {
        /// <summary>
        /// The minimum value.
        /// </summary>
        public float Min;

        /// <summary>
        /// The maximum value.
        /// </summary>
        public float Max;

        public float Step;

        public InspectorStepRangeAttribute(float min, float max, float step)
        {
            Min = min;
            Max = max;
            Step = step;
            if (step < 0.01f) step = 0.01f;
        }
    }

    public class MySettings : fiSettingsProcessor
    {
        public void Process()
        {
            fiSettings.DefaultPageMinimumCollectionLength = -1;
        }
    }
}
