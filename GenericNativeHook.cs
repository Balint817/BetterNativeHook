using MelonLoader;
using System.Reflection;
using System.Security;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace BetterNativeHook
{

    [SecurityCritical]
    [PatchShield]
    public sealed class GenericNativeHook
    {
        List<MelonHookInfo> HookInfos = new();
        public void Subscribe(MelonHookInfo method)
        {
            HookInfos.Add(method);
        }
        TargetMethodData TargetMethod { get; }

        static Dictionary<TargetMethodData, GenericNativeHook> MethodDataToInstance { get; } = new();
        public static GenericNativeHook CreateInstance(MelonHookInfo hookInfo)
        {
            if (MethodDataToInstance.TryGetValue(hookInfo.TargetMethodData, out var hook))
            {
                hook.HookInfos.Add(hookInfo);
                return hook;
            }
            return new GenericNativeHook(hookInfo);
        }
        public static GenericNativeHook CreateInstance(out MelonHookInfo hookInfo, MethodInfo targetMethod, int hookPriority = 0, string[]? runBefore = null, string[]? runAfter = null)
        {
            return CreateInstance(hookInfo = new MelonHookInfo(targetMethod, hookPriority, runBefore, runAfter));
        }
        private GenericNativeHook(MelonHookInfo hookInfo)
        {
            TargetMethod = hookInfo.TargetMethodData;
            MethodDataToInstance.Add(TargetMethod, this);
            HookInfos.Add(hookInfo);
        }

        internal static void InitParams(IntPtr returnValue, IntPtr[] args, FakeAssembly assembly, out ParameterInfo modifiedReturnValue, out ReadOnlyCollection<ParameterInfo> parameters)
        {
            var boundMethod = assembly.BoundMethodData;
            var methodParams = boundMethod.Parameters;
            modifiedReturnValue = new ParameterInfo(-1, null!, boundMethod.ReturnType, returnValue);

            parameters = methodParams
                .Select((x) => new ParameterInfo(x.Position + 1, x.Name, x.ParameterType, args[x.Position + 1]))
                .Prepend(new ParameterInfo(0, FakeAssembly.instanceParamName, boundMethod.TargetType, args[0]))
                .Append(new ParameterInfo(methodParams.Length + 1, FakeAssembly.nativeMethodPtrName, typeof(MethodInfo), args[methodParams.Length + 1]))
                .ToList()
                .AsReadOnly();
        }
        public static unsafe object HandleCallback(int fakeAssemblyIndex, IntPtr[] args)
        {
            var fakeAssembly = FakeAssembly.GetAssemblyByIndex(fakeAssemblyIndex);
            var returnValue = fakeAssembly.InvokeTrampoline(args);
            if (!MethodDataToInstance.TryGetValue(fakeAssembly.BoundMethodData, out var hook))
            {
                return returnValue;
            }
            InitParams(returnValue, args, fakeAssembly, out var modifiedReturnValue, out var parameters);

            foreach (var hookInfo in hook.HookInfos)
            {
                hookInfo.InvokeCallback(returnValue, modifiedReturnValue, parameters);
            }
            return modifiedReturnValue.Value;
        }
        public unsafe void AttachHook()
        {
            var trace = MelonTrace.GetMelonFromStackTrace();
            MelonLogger.Msg(ConsoleColor.Cyan, $"[{trace?.MelonInfo.Name ?? "<unknown melon>"}] requested AttachHook for {TargetMethod.Name}<{string.Join(", ", TargetMethod.GenericTypes.Select(x => x.Name))}>({string.Join(", ", TargetMethod.Parameters.Select(x => x.ParameterType.Name))})");
            TargetMethod.FakeAssembly.AttachHook();
        }
        public unsafe void DetachHook()
        {
            var trace = MelonTrace.GetMelonFromStackTrace();
            MelonLogger.Msg(ConsoleColor.Cyan, $"[{trace?.MelonInfo.Name ?? "<unknown melon>"}] requested DetachHook for {TargetMethod.Name}<{string.Join(", ", TargetMethod.GenericTypes.Select(x => x.Name))}>({string.Join(", ", TargetMethod.Parameters.Select(x => x.ParameterType.Name))})");
            TargetMethod.FakeAssembly.DetachHook();
        }
    }
}