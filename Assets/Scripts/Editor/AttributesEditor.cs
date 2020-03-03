using UnityEngine;
using UnityEditor;
using System.Collections;
using FullInspector;

[CustomPropertyEditor(typeof(MinMax<>))]
public class MinMaxEditor<TElement> : PropertyEditor<MinMax<TElement>>
{
    /// <summary>
    /// Formats a float so that it shows up to two decimal places if they are non-zero.
    /// </summary>
    public static string FormatFloat(float num)
    {
        var s = string.Format("{0:0.00}", num).TrimEnd('0');
        if (s.EndsWith(".")) {
            return s.TrimEnd('.');
        }
        return s;
    }

    private static float ToFloat(TElement element)
    {
        return (float)System.Convert.ChangeType(element, typeof(float));
    }

    private static TElement FromFloat(float f)
    {
        return (TElement)System.Convert.ChangeType(f, typeof(TElement));
    }

    public override MinMax<TElement> Edit(Rect region, GUIContent label, MinMax<TElement> element, fiGraphMetadata metadata)
    {
        float min = ToFloat(element.Min);
        float max = ToFloat(element.Max);
        float minLimit = ToFloat(element.MinLimit);
        float maxLimit = ToFloat(element.MaxLimit);

        string labelText = label.text + string.Format(" ({0}/{2} - {1}/{3})",
            FormatFloat(min), FormatFloat(max),
            FormatFloat(minLimit), FormatFloat(maxLimit));
        var updatedLabel = new GUIContent(labelText, label.image, label.tooltip);

        EditorGUI.MinMaxSlider(region, updatedLabel, ref min, ref max, minLimit, maxLimit);

        return new MinMax<TElement>() {
            Min = FromFloat(min),
            Max = FromFloat(max),
            MinLimit = FromFloat(minLimit),
            MaxLimit = FromFloat(maxLimit)
        };
    }

    public override float GetElementHeight(GUIContent label, MinMax<TElement> element, fiGraphMetadata metadata)
    {
        return EditorStyles.largeLabel.CalcHeight(label, 100);
    }
}
namespace FullInspector
{
    [CustomAttributePropertyEditor(typeof(InspectorStepRangeAttribute), ReplaceOthers = true)]
    public class InspectorStepRangeAttributeEditor<TElement> : AttributePropertyEditor<TElement, InspectorStepRangeAttribute>
    {
        private static T Cast<T>(object o)
        {
            return (T)System.Convert.ChangeType(o, typeof(T));
        }

        protected override TElement Edit(Rect region, GUIContent label, TElement element, InspectorStepRangeAttribute attribute, fiGraphMetadata metadata)
        {
            return Cast<TElement>((int)(EditorGUI.Slider(region, label, Cast<float>(element), attribute.Min, attribute.Max) / attribute.Step) * attribute.Step);
        }

        protected override float GetElementHeight(GUIContent label, TElement element, InspectorStepRangeAttribute attribute, fiGraphMetadata metadata)
        {
            return EditorStyles.label.CalcHeight(label, 100);
        }

        public override bool CanEdit(System.Type type)
        {
            return type == typeof(int) || type == typeof(double) || type == typeof(float) || type == typeof(decimal);
        }
    }
}