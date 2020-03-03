using System;

namespace FullInspector {
    /// <summary>
    /// Display this field, property, or method inside of the given tab group / category within
    /// the inspector. Each member can be part of multiple categories - simply apply this attribute
    /// multiple times.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = true)]
    public sealed class InspectorCategoryAttribute : Attribute {
        /// <summary>
        /// The category to display this member in.
        /// </summary>
        public string Category;

        public InspectorCategoryAttribute(string category) {
            Category = category;
        }
    }
}