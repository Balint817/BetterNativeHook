using MelonLoader;
using System.Reflection;
using System.Diagnostics;
using System.Security;
using BetterNativeHook.Properties;

namespace BetterNativeHook
{
    //[SecurityCritical]
    //[PatchShield]
    //internal static class PrivateUtils
    //{
    //}

    [SecurityCritical]
    [PatchShield]
    public sealed class MelonTrace
    {
        public static string GetName(MelonTrace? trace)
        {
            return trace?.MelonInfo.Name ?? "<unknown melon>";
        }
        public MelonInfoAttribute MelonInfo { get; }
        public Assembly Assembly { get; }
        private MelonTrace(Assembly asm, MelonInfoAttribute melonInfo)
        {
            Assembly = asm;
            MelonInfo = melonInfo;
        }
        internal static MelonTrace? GetMelonFromStackTrace()
        {
            var stackTrace = new StackTrace();
            var stackFrames = stackTrace.GetFrames();
            foreach (var frame in stackFrames)
            {
                var currentMethod = frame.GetMethod()!;
                var currentType = currentMethod.ReflectedType!;
                var currentAssembly = currentType.Assembly;
                var melonInfoAttribute = currentAssembly.GetCustomAttribute<MelonInfoAttribute>();
                if (melonInfoAttribute is null || melonInfoAttribute.Name == MelonModInfo.Name)
                {
                    continue;
                }
                return new MelonTrace(currentAssembly, melonInfoAttribute);
            }
            return null;
        }
    }
}