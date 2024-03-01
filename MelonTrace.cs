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
        public MelonInfoAttribute MelonInfo { get; }
        public Assembly Assembly { get; }
        private MelonTrace(Assembly asm, MelonInfoAttribute melonInfo)
        {
            Assembly = asm;
            MelonInfo = melonInfo;
        }
        internal static MelonTrace? GetMelonFromStackTrace()
        {
            MelonLogger.Msg(1);
            var stackTrace = new StackTrace();
            MelonLogger.Msg(2);
            var stackFrames = stackTrace.GetFrames();
            MelonLogger.Msg(3);
            foreach (var frame in stackFrames)
            {
                MelonLogger.Msg("3.1");
                var currentMethod = frame.GetMethod()!;
                MelonLogger.Msg("3.2");
                var currentType = currentMethod.ReflectedType!;
                MelonLogger.Msg("3.3");
                var currentAssembly = currentType.Assembly;
                MelonLogger.Msg("3.4");
                var melonInfoAttribute = currentAssembly.GetCustomAttribute<MelonInfoAttribute>();
                MelonLogger.Msg("3.5");
                if (melonInfoAttribute is null || melonInfoAttribute.Name == MelonModInfo.Name)
                {
                    MelonLogger.Msg("3.6");
                    MelonLogger.Msg("-----------------------------");
                    continue;
                }
                MelonLogger.Msg(4);
                return new MelonTrace(currentAssembly, melonInfoAttribute);
            }
            MelonLogger.Msg(5);
            return null;
        }
    }
}