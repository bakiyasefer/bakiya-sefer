using System;
using System.Reflection;

namespace FullInspector.Modules.SerializableDelegates {
    [CustomPropertyEditor(typeof(BaseSerializedAction), Inherit = true)]
    public class BaseSerializationActionEditor<TSerializationAction> : BaseSerialiationInvokableEditor<TSerializationAction>
        where TSerializationAction : BaseSerializedAction, new() {

        protected override bool IsValidMethod(MethodInfo method) {
            var methodParameters = method.GetParameters();

            // Non-generic actions take no parameters
            if (typeof(TSerializationAction).IsGenericType == false) {
                return methodParameters.Length == 0;
            }

            // Verify the generic parameters
            else {
                var actionGenerics = typeof(TSerializationAction).GetGenericArguments();

                if (methodParameters.Length != actionGenerics.Length) {
                    return false;
                }

                for (int i = 0; i < methodParameters.Length; ++i) {
                    Type genericType = actionGenerics[i];
                    Type methodParam = methodParameters[i].ParameterType;

                    if (methodParam.IsAssignableFrom(genericType) == false) {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}