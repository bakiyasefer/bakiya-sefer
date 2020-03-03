using System;
using UnityEngine;

namespace FullInspector.Internal {
    [CustomPropertyEditor(typeof(Gradient))]
    public class GradientPropertyEditor : fiGenericPropertyDrawerPropertyEditor<GradientMonoBehaviourStorage, Gradient> {
        public override bool CanEdit(Type type) {
            return typeof(Gradient).IsAssignableFrom(type);
        }
    }
}
