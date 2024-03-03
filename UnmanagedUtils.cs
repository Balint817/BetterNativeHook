using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using MelonLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace BetterNativeHook
{
    [SecurityCritical]
    [PatchShield]
    public class UnmanagedUtils
    {
        public static T CreateTypeValue<T>()
        {
            return (T)Activator.CreateInstance(typeof(T),
                IL2CPP.il2cpp_object_new(Il2CppClassPointerStore<T>.NativeClassPtr))!;
        }
        public static string? Il2CppStringPtrToString(IntPtr ptr)
        {
            return IL2CPP.Il2CppStringToManaged(ptr);
        }
        public static string? Il2CppStringPtrToString(ParameterInfo parameter)
        {
            return Il2CppStringPtrToString(parameter.Value);
        }


    }
}
