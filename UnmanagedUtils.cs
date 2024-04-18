using Il2CppInterop.Runtime;
using MelonLoader;
using System.Security;

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
        public static string? Il2CppStringToManaged(IntPtr? pointer)
        {
            if (pointer is not { } ptr)
            {
                return null;
            }
            return IL2CPP.Il2CppStringToManaged(ptr);
        }
        public static string? Il2CppStringToManaged(ParameterReference parameter)
        {
            return Il2CppStringToManaged(parameter.CurrentValue);
        }


    }
}
