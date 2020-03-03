using System;
using System.Collections.Generic;
using System.Linq;
using FullInspector.Internal;
using FullSerializer;
using UnityEditor;
using UnityEngine;

namespace FullInspector.Modules.TypeSpecifierImpl {
    [CustomPropertyEditor(typeof(TypeSpecifier<>))]
    public class TypeSpecifierPropertyEditor<TType> : PropertyEditor<TypeSpecifier<TType>> {
        private static Type[] DropdownTypes;
        private static GUIContent[] DropdownLabels;

        static TypeSpecifierPropertyEditor() {
            var dropdownTypes = new List<Type>();
            var dropdownLabels = new List<GUIContent>();

            dropdownTypes.Add(null);
            dropdownLabels.Add(new GUIContent("<no type>"));

            var types = fiReflectionUtility.GetCreatableTypesDeriving(typeof(TType)).ToList();
            dropdownTypes.AddRange(types);
            dropdownLabels.AddRange(types.Select(t => new GUIContent(t.CSharpName())));

            DropdownTypes = dropdownTypes.ToArray();
            DropdownLabels = dropdownLabels.ToArray();
        }

        public override TypeSpecifier<TType> Edit(Rect region, GUIContent label, TypeSpecifier<TType> element, fiGraphMetadata metadata) {
            if (element == null) element = new TypeSpecifier<TType>();

            int index = Array.IndexOf(DropdownTypes, element.Type);
            if (index == -1) index = 0;

            int newIndex = EditorGUI.Popup(region, label, index, DropdownLabels);

            if (index != newIndex && newIndex >= 0 && newIndex < DropdownTypes.Length) {
                element.Type = DropdownTypes[newIndex];
            }

            return element;
        }

        public override float GetElementHeight(GUIContent label, TypeSpecifier<TType> element, fiGraphMetadata metadata) {
            return EditorGUIUtility.singleLineHeight;
        }
    }
}