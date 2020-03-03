using System;
using System.Collections.Generic;
using System.Linq;
using FullSerializer;
using FullSerializer.Internal;
using UnityEngine;

namespace FullInspector.Internal {
    /// <summary>
    /// Manages the options that are displayed to the user in the instance selection drop-down.
    /// </summary>
    internal class AbstractTypeInstanceOptionManager {
        #region Derived Type Fetching and Sorting
        struct DisplayedType {
            public Type RawType;
            public string TypeName;
        }
        private static Dictionary<Type, DisplayedType[]> TypeCache = new Dictionary<Type, DisplayedType[]>();
        private static DisplayedType[] GetDisplayedTypes(Type baseType) {
            DisplayedType[] result;

            if (TypeCache.TryGetValue(baseType, out result) == false) {
                List<DisplayedType> types = new List<DisplayedType>();

                foreach (var type in fiReflectionUtility.GetCreatableTypesDeriving(baseType)) {
                    string typeName = "";

                    var attr = fsPortableReflection.GetAttribute<InspectorDropdownNameAttribute>(type);
                    if (attr != null) { 
                        typeName = attr.DisplayName;
                    }
                    if (string.IsNullOrEmpty(typeName)) {
                        typeName = type.CSharpName();
                    }

                    types.Add(new DisplayedType {
                        RawType = type,
                        TypeName = typeName
                    });
                }

                result = types.OrderBy(dt => dt.TypeName).ToArray();
                TypeCache[baseType] = result;
            }

            return result;
        }
        #endregion

        private DisplayedType[] _options;
        private List<GUIContent> _displayedOptions;

        /// <summary>
        /// Setup the instance option manager for the given type.
        /// </summary>
        public AbstractTypeInstanceOptionManager(Type baseType) {
            _options = GetDisplayedTypes(baseType);

            _displayedOptions = new List<GUIContent>();
            _displayedOptions.Add(new GUIContent("null (" + baseType.CSharpName() + ")"));
            _displayedOptions.AddRange(from option in _options
                                       select new GUIContent(option.TypeName));
        }

        private static string GetOptionName(Type type) {
            string baseName = type.CSharpName();

            if (type.IsValueType == false &&
                type.GetConstructor(fsPortableReflection.EmptyTypes) == null) {

                baseName += " (skips ctor)";
            }

            return baseName;
        }

        /// <summary>
        /// Returns an array of options that should be displayed.
        /// </summary>
        public GUIContent[] GetDisplayOptions() {
            return _displayedOptions.ToArray();
        }

        /// <summary>
        /// Remove any options from the set of display options that are not permanently visible.
        /// </summary>
        public void RemoveExtraneousOptions() {
            while (_displayedOptions.Count > (_options.Length + 1)) {
                _displayedOptions.RemoveAt(_displayedOptions.Count - 1);
            }
        }

        /// <summary>
        /// Returns the index of the option that should be displayed (from GetDisplayOptions())
        /// based on the current object instance.
        /// </summary>
        public int GetDisplayOptionIndex(object instance) {
            if (instance == null) {
                return 0;
            }

            Type instanceType = instance.GetType();
            for (int i = 0; i < _options.Length; ++i) {
                Type option = _options[i].RawType;
                if (instanceType == option) {
                    return i + 1;
                }
            }

            // we need a new display option
            _displayedOptions.Add(new GUIContent(instance.GetType().CSharpName() + " (cannot reconstruct)"));
            return _displayedOptions.Count - 1;
        }

        /// <summary>
        /// Changes the instance of the given object, if necessary.
        /// </summary>
        public object UpdateObjectInstance(object current, int currentIndex, int updatedIndex) {
            // the index has not changed - there will be no change in object instance
            if (currentIndex == updatedIndex) {
                return current;
            }

            // index 0 is always null
            if (updatedIndex == 0) {
                return null;
            }

            // create an instance of the object
            Type type = _options[updatedIndex - 1].RawType;
            return InspectedType.Get(type).CreateInstance();
        }
    }
}