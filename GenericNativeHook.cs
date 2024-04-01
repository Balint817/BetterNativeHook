using MelonLoader;
using MelonLoader.NativeUtils;
using System.Reflection;
using System.Security;

namespace BetterNativeHook
{
    /// <summary>
    /// This class handles communication with the <see cref="FakeAssembly"/> instance.
    /// <para></para>
    /// It also manages the <see cref="MelonHookInfo"/> instances that wish to patch the same method, described by a given <see cref="TargetMethodData"/> instance
    /// <para></para>
    /// </summary>
    [SecurityCritical]
    [PatchShield]
    public sealed class GenericNativeHook
    {
        List<MelonHookInfo> HookInfos = new();
        TargetMethodData TargetMethod { get; }

        static Dictionary<TargetMethodData, GenericNativeHook> MethodDataToInstance { get; } = new();
        /// <summary>
        /// Creates a new instance of the class, and subscribes the given MelonHookInfo to it.
        /// <para></para>
        /// If an instance that points to the method described by <see cref="MelonHookInfo.TargetMethodData"/> already exists, it will subscribe to existing instance instead.
        /// </summary>
        /// <param name="hookInfo">The <see cref="MelonHookInfo"/> instance to create a hook from</param>
        /// <returns>A new <see cref="GenericNativeHook"/> instance</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="hookInfo"/> is null</exception>
        public static GenericNativeHook CreateInstance(MelonHookInfo hookInfo)
        {
            if (hookInfo is null)
            {
                throw new ArgumentNullException(nameof(hookInfo));
            }
            if (MethodDataToInstance.TryGetValue(hookInfo.TargetMethodData, out var hook))
            {
                hook.AttachHookInfo(hookInfo);
                return hook;
            }
            return new GenericNativeHook(hookInfo);
        }
        /// <summary>
        /// Creates a new instance of <see cref="MelonHookInfo"/> with the given parameters, then calls <see cref="CreateInstance(MelonHookInfo)"/>
        /// </summary>
        /// <param name="hookInfo">The resulting <see cref="MelonHookInfo"/> instance.</param>
        /// <param name="targetMethod">The <see cref="MethodInfo"/> instance to create a <see cref="MelonHookInfo"/> instance from.</param>
        /// <returns>A new <see cref="GenericNativeHook"/> instance</returns>
        public static GenericNativeHook CreateInstance(out MelonHookInfo hookInfo, MethodInfo targetMethod, int hookPriority = 0, string[]? runBefore = null, string[]? runAfter = null)
        {
            return CreateInstance(hookInfo = new MelonHookInfo(targetMethod, hookPriority, runBefore, runAfter));
        }
        private GenericNativeHook(MelonHookInfo hookInfo)
        {
            TargetMethod = hookInfo.TargetMethodData;
            this.AttachHookInfo(hookInfo);
            MethodDataToInstance.Add(TargetMethod, this);
        }

        /// <summary>
        /// This method was made public to prevent MethodAccessException in MSIL, but this method should never be called anywhere (not even internally)
        /// <para></para>
        /// It's sole caller should be the generated unmanaged method.
        /// </summary>
        /// <param name="trampolineReturnValue">The value returned by the trampoline</param>
        /// <param name="fakeAssemblyIndex">The index of the FakeAssembly that generated the unmanaged caller</param>
        /// <param name="args">The arguments of the unmanaged function packed into an array</param>
        /// <returns>The pointer modified by the subscribed <see cref="MelonHookInfo"/> instances, or the original if left untouched</returns>
        public static IntPtr HandleCallback(int fakeAssemblyIndex, IntPtr[] args)
        {
            var fakeAssembly = FakeAssembly.GetAssemblyByIndex(fakeAssemblyIndex);
            if (!MethodDataToInstance.TryGetValue(fakeAssembly.BoundMethodData, out var hook))
            {
                return fakeAssembly.InvokeTrampolineDirect(args);
            }
            var boundMethod = fakeAssembly.BoundMethodData;
            var methodParams = boundMethod.Parameters;

            var parameters = methodParams
                .Select((x) => new ParameterReference(x.Position + 1, x.Name, x.ParameterType, args[x.Position + 1]))
                .Prepend(new ParameterReference(0, FakeAssembly.instanceParamName, boundMethod.TargetType, args[0]))
                .Append(new ParameterReference(methodParams.Length + 1, FakeAssembly.nativeMethodPtrName, typeof(MethodInfo), args[methodParams.Length + 1]))
                .ToList()
                .AsReadOnly();

            var returnValue = new ReturnValueReference(boundMethod.ReturnType, fakeAssembly, parameters);

            foreach (var hookInfo in hook.HookInfos)
            {
                hookInfo.InvokeCallback(returnValue, parameters);
            }
            return returnValue.GetValueOrInvokeTrampoline();
        }
        /// <summary>
        /// Attaches the <see cref="NativeHook{T}"/> represented by this instance.
        /// </summary>
        public void AttachHook()
        {
            var trace = MelonTrace.GetMelonFromStackTrace();
            MelonLogger.Msg(ConsoleColor.Cyan, $"[{MelonTrace.GetName(trace)}] requested AttachHook for {TargetMethod.GetFullName()}");
            TargetMethod.FakeAssembly.AttachHook();
        }
        /// <summary>
        /// Detaches the <see cref="NativeHook{T}"/> represented by this instance.
        /// </summary>
        public void DetachHook()
        {
            var trace = MelonTrace.GetMelonFromStackTrace();
            MelonLogger.Msg(ConsoleColor.Cyan, $"[{MelonTrace.GetName(trace)}] requested DetachHook for {TargetMethod.GetFullName()}");
            TargetMethod.FakeAssembly.DetachHook();
        }
        /// <summary>
        /// Adds the specified <see cref="MelonHookInfo"/> to the invocation list.
        /// <para></para>
        /// The caller must be a MelonMod instance that matches the Melon that instantiated the given <see cref="MelonHookInfo"/> instance.
        /// </summary>
        /// <param name="hookInfo"></param>
        /// <exception cref="ArgumentNullException">If <paramref name="hookInfo"/> is null</exception>
        /// <exception cref="InvalidOperationException">If the caller isn't a MelonMod instance</exception>
        /// <exception cref="InvalidOperationException">If the calling MelonMod instance doesn't match the instance tied to <paramref name="hookInfo"/></exception>
        public void AttachHookInfo(MelonHookInfo hookInfo)
        {
            if (hookInfo is null)
            {
                throw new ArgumentNullException(nameof(hookInfo));
            }
            var trace = MelonTrace.GetMelonFromStackTrace() ?? throw new InvalidOperationException($"{nameof(AttachHookInfo)} must be called from a MelonMod instance");
            if (hookInfo.CallerMelon.Assembly != trace.Assembly)
            {
                throw new InvalidOperationException($"you cannot attach a {nameof(MelonHookInfo)} instance that belongs to a different mod");
            }
            MelonLogger.Msg($"[{MelonTrace.GetName(trace)}] requested AttachHookInfo for {TargetMethod.Name}<{string.Join(", ", TargetMethod.GenericTypes.Select(x => x.Name))}>({string.Join(", ", TargetMethod.Parameters.Select(x => x.ParameterType.Name))})");
            HookInfos.Add(hookInfo);
            SortHookInfos();
        }
        /// <summary>
        /// Removes the specified <see cref="MelonHookInfo"/> from the invocation list.
        /// <para></para>
        /// The caller must be a MelonMod instance that matches the Melon that instantiated the given <see cref="MelonHookInfo"/> instance.
        /// </summary>
        /// <param name="hookInfo"></param>
        /// <exception cref="ArgumentNullException">If <paramref name="hookInfo"/> is null</exception>
        /// <exception cref="InvalidOperationException">If the caller isn't a MelonMod instance</exception>
        /// <exception cref="InvalidOperationException">If the calling MelonMod instance doesn't match the instance tied to <paramref name="hookInfo"/></exception>
        public void DetachHookInfo(MelonHookInfo hookInfo)
        {
            if (hookInfo is null)
            {
                throw new ArgumentNullException(nameof(hookInfo));
            }
            var trace = MelonTrace.GetMelonFromStackTrace() ?? throw new InvalidOperationException($"{nameof(AttachHookInfo)} must be called from a MelonMod instance");
            if (hookInfo.CallerMelon.Assembly != trace.Assembly)
            {
                throw new InvalidOperationException($"you cannot detach a {nameof(MelonHookInfo)} instance that belongs to a different mod");
            }
            MelonLogger.Msg($"[{MelonTrace.GetName(trace)}] requested DetachHookInfo for {TargetMethod.Name}<{string.Join(", ", TargetMethod.GenericTypes.Select(x => x.Name))}>({string.Join(", ", TargetMethod.Parameters.Select(x => x.ParameterType.Name))})");
            HookInfos.Remove(hookInfo);
            SortHookInfos();
        }
        void SortHookInfos()
        {
            HookInfos.Sort();
        }
    }
}