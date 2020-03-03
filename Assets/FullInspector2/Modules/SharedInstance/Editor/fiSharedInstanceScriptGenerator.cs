using System;
using System.IO;
using System.Linq;
using FullInspector.Internal;
using FullSerializer;
using UnityEditor;
using UnityEngine;

namespace FullInspector.Modules.SharedInstance {
    /// <summary>
    /// Generates derived types for SharedInstance{T}.
    /// </summary>
    public static class fiSharedInstanceScriptGenerator {
        public static void GenerateScript(Type instanceType, Type serializerType) {
            // The name of the class, ie, SharedInstance_SystemInt32
            string className = instanceType.CSharpName();
            if (instanceType.Namespace != null && instanceType.Namespace != "System") {
                className = RemoveAll(instanceType.Namespace, '.') + className;
            }
            className = "SharedInstance_" + className;

            // The name of the type, ie, System.Int32
            // We do a number of replacements so that it will be a valid filename
            string typeName = instanceType.CSharpName().Replace(" ", "_").Replace("<", "_").Replace(">", "_").Replace(",", "_");
            if (instanceType.Namespace != null && instanceType.Namespace != "System") {
                typeName = instanceType.Namespace + "." + typeName;
            }

            // The name of the serializer
            string serializerName = null;
            if (serializerType != null) {
                serializerName = serializerType.CSharpName();
            }

            Emit(className, typeName, serializerName);
        }

        private static void Emit(string className, string typeName, string serializerName) {
            // Get the file path we will generate. If there is already a file there, it is assumed that
            // we are the ones who generated it and so we don't need to do anything.
            String directory = fiUtility.CombinePaths(fiSettings.RootGeneratedDirectory, "SharedInstance");
            Directory.CreateDirectory(directory);
            String path = fiUtility.CombinePaths(directory, className + ".cs");
            if (File.Exists(path)) return;

            string script = "";
            script += "// This is an automatically generated script that is used to remove the generic " + Environment.NewLine;
            script += "// parameter from SharedInstance<T, TSerializer> so that Unity can properly serialize it." + Environment.NewLine;
            script += Environment.NewLine;
            script += "using System;" + Environment.NewLine;
            script += Environment.NewLine;
            script += "namespace FullInspector.Generated.SharedInstance {" + Environment.NewLine;
            if (serializerName != null) {
                script += "    public class " + className + " : SharedInstance<" + typeName + ", " + serializerName + "> {}" + Environment.NewLine;
            }
            else {
                script += "    public class " + className + " : SharedInstance<" + typeName + "> {}" + Environment.NewLine;
            }
            script += "}" + Environment.NewLine;

            Debug.Log("Writing derived SharedInstance<" + typeName + ", " + serializerName + "> type (" + className + ") to " + path + "; click to see script below." +
                Environment.NewLine + Environment.NewLine + script);
            File.WriteAllText(path, script);
            AssetDatabase.Refresh();
        }

        private static string RemoveAll(string str, char c) {
            return str.Split(c).Aggregate((a, b) => a + b);
        }
    }
}