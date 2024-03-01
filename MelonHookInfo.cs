using MelonLoader;
using System.Reflection;
using System.Security;
using System.Collections.ObjectModel;

namespace BetterNativeHook
{
    /// <param name="originalReturnValue">The pointer initially returned by the trampoline</param>
    /// <param name="modifiedReturnValue">The return value modified by prior methods in the invocation list</param>
    /// <param name="parameters">The parameters of the method. Can be overridden and sent to the next method in the invocation list</param>
    [SecurityCritical]
    public delegate void HookDelegate(IntPtr originalReturnValue, ParameterInfo modifiedReturnValue, ReadOnlyCollection<ParameterInfo> parameters);

    [SecurityCritical]
    [PatchShield]
    public sealed class MelonHookInfo
    {
        event HookDelegate HookCallback;
        protected internal void InvokeCallback(IntPtr originalReturnValue, ParameterInfo modifiedReturnValue, ReadOnlyCollection<ParameterInfo> parameters)
        {
            if (HookCallback is null)
            {
                return;
            }
            foreach (HookDelegate del in HookCallback.GetInvocationList())
            {
                try
                {
                    del.Invoke(originalReturnValue, modifiedReturnValue, parameters);
                    modifiedReturnValue.SetOverrides();
                    foreach (var parameter in parameters)
                    {
                        parameter.SetOverrides();
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Error(ex.ToString());
                    if (ex is CriticalPatchException)
                    {
                        Environment.FailFast(null);
                    }
                }
            }
        }

        public event HookDelegate HookCallbackEvent
        {
            add
            {
                if (value is null)
                {
                    throw new ArgumentNullException(nameof(value));
                }
                var reflectedType = value.Method.ReflectedType ?? throw new NotSupportedException("Method.ReflectedType returned null");
                if (reflectedType.Assembly != CallerMelon.Assembly)
                {
                    throw new ArgumentException("you must implement your own callback (instance and method assembly mismatch)", nameof(value));
                }
                HookCallback += value ?? throw new ArgumentNullException(nameof(value));
            }
            remove
            {
                HookCallback -= value;
            }
        }
        public MelonTrace CallerMelon { get; }
        public TargetMethodData TargetMethodData { get; }
        /// <summary>
        /// Initializes a new instance of MelonHookInfo.
        /// </summary>
        /// <param name="targetMethod"></param>
        /// <param name="hookPriority">Not implemented</param>
        /// <param name="runBefore">Not implemented</param>
        /// <param name="runAfter">Not implemented</param>
        public MelonHookInfo(MethodInfo targetMethod, int hookPriority = 0, string[]? runBefore = null, string[]? runAfter = null)
        {
            TargetMethodData = TargetMethodData.GetInstance(targetMethod);
            CallerMelon = MelonTrace.GetMelonFromStackTrace() ?? throw new InvalidOperationException("MelonHookInfo must be instantiated from a MelonMod instance");
        }
    }
}