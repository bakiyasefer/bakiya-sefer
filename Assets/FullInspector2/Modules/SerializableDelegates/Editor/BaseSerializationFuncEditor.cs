using System;
using System.Reflection;

namespace FullInspector.Modules.SerializableDelegates {
    [CustomPropertyEditor(typeof(BaseSerializedFunc), Inherit = true)]
    public class BaseSerializationFuncEditor<TSerializationFunc> : BaseSerialiationInvokableEditor<TSerializationFunc>
        where TSerializationFunc : BaseSerializedFunc, new() {

        protected override bool IsValidMethod(MethodInfo method) {
            var methodParameters = method.GetParameters();

            var funcGenerics = typeof(TSerializationFunc).GetGenericArguments();

            // Verify the parameters
            if (methodParameters.Length != (funcGenerics.Length - 1)) {
                return false;
            }
            for (int i = 0; i < methodParameters.Length; ++i) {
                Type genericType = funcGenerics[i];
                Type methodParam = methodParameters[i].ParameterType;

                if (methodParam.IsAssignableFrom(genericType) == false) {
                    return false;
                }
            }

            // Verify the return type
            Type funcReturnType = funcGenerics[funcGenerics.Length - 1];
            Type methodReturnType = method.ReturnType;
            if (funcReturnType.IsAssignableFrom(methodReturnType) == false) {
                return false;
            }

            return true;
        }
    }


}