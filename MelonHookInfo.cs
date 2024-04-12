using MelonLoader;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Security;

namespace BetterNativeHook
{
    /// <param name="returnValue">The pointer initially returned by the trampoline</param>
    /// <param name="modifiedReturnValue">The return value modified by prior methods in the invocation list</param>
    /// <param name="parameters">The parameters of the method. Can be overridden and sent to the next method in the invocation list</param>
    [SecurityCritical]
    public delegate void HookDelegate(ReturnValueReference returnValue, ReadOnlyCollection<ParameterReference> parameters);

    [SecurityCritical]
    [PatchShield]
    public sealed class MelonHookInfo: IComparable<MelonHookInfo>
    {
        event HookDelegate? HookCallback;
        internal void InvokeCallback(ReturnValueReference returnValue, ReadOnlyCollection<ParameterReference> parameters)
        {
            if (HookCallback is null)
            {
                return;
            }
            foreach (HookDelegate del in HookCallback.GetInvocationList())
            {
                try
                {
                    if (del is null)
                    {
                        continue;                        
                    }
                    del.Invoke(returnValue, parameters);
                    returnValue.SetOverride();
                }
                catch (Exception ex)
                {
                    MelonLogger.Error(ex.ToString());
                    if (ex is CriticalPatchException)
                    {
                        MelonLogger.Error("The program will now exit to prevent further issues or corruption.");
                        Thread.Sleep(5000);
                        Environment.FailFast(null);
                    }
                }
            }
        }
        /// <summary>
        /// The callback for the current hook. The method being added/removed must be defined by the assembly of the Melon that created this instance.
        /// </summary>
        /// <exception cref="ArgumentNullException">If the method being added/removed is null</exception>
        /// <exception cref="ArgumentException">If the assembly of the method doesn't match the assembly of the Melon that created this instance</exception>
        /// <exception cref="InvalidOperationException">If the caller isn't a MelonMod instance</exception>
        /// <exception cref="InvalidOperationException">If the caller doesn't match the MelonMod that created this instance</exception>
        public event HookDelegate HookCallbackEvent
        {
            add
            {
                var trace = MelonTrace.GetMelonFromStackTrace() ?? throw new InvalidOperationException("callbacks must be modified from a MelonMod instance");
                if (trace.MelonInfo.Name != CallerMelon.MelonInfo.Name)
                {
                    throw new InvalidOperationException("only the MelonMod that created this instance may modify the callbacks");
                }
                if (value is null)
                {
                    throw new ArgumentNullException(nameof(value));
                }
                if (value.Method.Module.Assembly != CallerMelon.Assembly)
                {
                    throw new ArgumentException("you must implement your own callback (instance and method assembly mismatch)", nameof(value));
                }
                HookCallback += value;
            }
            remove
            {
                var trace = MelonTrace.GetMelonFromStackTrace() ?? throw new InvalidOperationException("callbacks must be modified from a MelonMod instance");
                if (trace.MelonInfo.Name != CallerMelon.MelonInfo.Name)
                {
                    throw new InvalidOperationException("only the MelonMod that created this instance may modify the callbacks");
                }
                if (value is null)
                {
                    throw new ArgumentNullException(nameof(value));
                }
                if (value.Method.Module.Assembly != CallerMelon.Assembly)
                {
                    throw new ArgumentException("you must implement your own callback (instance and method assembly mismatch)", nameof(value));
                }
                HookCallback -= value;
            }
        }
        /// <summary>
        /// The melon that called this method represented by a <see cref="MelonTrace"/> instance.
        /// </summary>
        public MelonTrace CallerMelon { get; }
        /// <summary>
        /// The metadata of the target method represented by a <see cref="TargetMethodData"/> instance.
        /// </summary>
        public TargetMethodData TargetMethodData { get; }
        /// <summary>
        /// Initializes a new instance of MelonHookInfo.
        /// </summary>
        /// <param name="targetMethod">
        /// 
        /// </param>
        /// <param name="hookPriority">
        /// The priority of the hook (lower means higher priority). Should be assigned in a multiples of 100, so that values can be inserted into the middle if needed.
        /// <para>For example:</para>
        /// <example>0, 100 => 0, 50, 100 => 0, 50, 75, 100</example>
        /// <para>You can't change this later.</para>
        /// </param>
        /// <param name="runBefore">
        /// MelonMod names, as defined in the MelonInfoAttribute. Regardless of priority, your hook will always run before this mod's hook
        /// <para>You can't change this later.</para>
        /// </param>
        /// <param name="runAfter">
        /// MelonMod names, as defined in the MelonInfoAttribute. Regardless of priority, your hook will always run after this mod's hook
        /// <para>You can't change this later.</para>
        /// </param>
        public MelonHookInfo(MethodInfo targetMethod, int hookPriority = 0, string[]? runBefore = null, string[]? runAfter = null)
        {
            TargetMethodData = TargetMethodData.GetInstance(targetMethod);
            CallerMelon = MelonTrace.GetMelonFromStackTrace() ?? throw new InvalidOperationException("MelonHookInfo must be instantiated from a MelonMod instance");
            HookPriority = hookPriority;
            _runBefore = runBefore?.ToHashSet() ?? new();
            _runAfter = runAfter?.ToHashSet() ?? new();
            if (_runBefore.Any(x => _runAfter.Contains(x)))
            {
                throw new ArgumentException($"A melon's name can't be included in both '{nameof(runBefore)}' and '{nameof(runAfter)}'.", $"{nameof(runBefore)} & {nameof(runAfter)}");
            }
        }
        /// <summary>
        /// The priority of this hook. Lower value means higher priority
        /// </summary>
        public int HookPriority { get; }
        /// <summary>
        /// Name of MelonMods that this instance will be guaranteed to run before, regardless of <see cref="HookPriority"/>
        /// <para></para>
        /// If both mods attempt to run before each other, the priority decides the order. If they match, the behavior is undefined.
        /// </summary>
        HashSet<string> _runBefore { get; }
        /// <inheritdoc cref="_runBefore"/>
        public ReadOnlyCollection<string> RunBefore => _runBefore.ToList().AsReadOnly();
        /// <summary>
        /// Name of MelonMods that this instance will be guaranteed to run after, regardless of <see cref="HookPriority"/>
        /// <para></para>
        /// If both mods attempt to run after each other, the priority decides the order. If they match, the behavior is undefined.
        /// </summary>
        HashSet<string> _runAfter { get; }
        /// <inheritdoc cref="_runAfter"/>
        public ReadOnlyCollection<string> RunAfter => _runBefore.ToList().AsReadOnly();

        /// <summary>
        /// Two hooks compare equal if:
        /// <para></para>
        ///  - Their priorities match
        /// <para></para>
        ///  - If they reference each other <see cref="RunBefore"/>, but their priorities match
        /// <para></para>
        ///  - If they reference each other <see cref="RunAfter"/>, but their priorities match
        /// <para></para>
        /// <para></para>
        /// This hook comes first if:
        /// <para></para>
        ///  - The other is null
        /// <para></para>
        ///  - The other is in this instance's <see cref="RunBefore"/>, but not the other way around
        /// <para></para>
        ///  - This instance is in other's <see cref="RunAfter"/>, but not the other way around
        /// <para></para>
        ///  - If they reference each other <see cref="RunBefore"/>, but this instance has higher priority
        /// <para></para>
        ///  - If they reference each other <see cref="RunAfter"/>, but this instance has higher priority
        /// <para></para>
        /// The other hook comes first if:
        /// <para></para>
        ///  - The other is in this instance's <see cref="RunAfter"/>, but not the other way around
        /// <para></para>
        ///  - This instance is in other's <see cref="RunBefore"/>, but not the other way around
        /// <para></para>
        ///  - If they reference each other <see cref="RunBefore"/>, but this instance has lower priority
        /// <para></para>
        ///  - If they reference each other <see cref="RunAfter"/>, but this instance has lower priority
        /// </summary>
        int IComparable<MelonHookInfo>.CompareTo(MelonHookInfo? other)
        {
            if (other is null)
            {
                return -1;
            }


            if (_runBefore.Contains(other.CallerMelon.MelonInfo.Name))
            {
                if (!other._runBefore.Contains(this.CallerMelon.MelonInfo.Name))
                {
                    return -1;
                }
                if (other.HookPriority == this.HookPriority)
                {
                    MelonLogger.Error($"Both '{other.CallerMelon.MelonInfo.Name}' and '{this.CallerMelon.MelonInfo.Name}' requested to run before each other with a matching priority ({HookPriority}) in hook of {TargetMethodData.GetFullName()}. This may cause unexpected behavior!");
                    return 0;
                }
            }
            else if (other._runBefore.Contains(this.CallerMelon.MelonInfo.Name))
            {
                return 1;
            }


            if (_runAfter.Contains(other.CallerMelon.MelonInfo.Name))
            {
                if (!other._runAfter.Contains(this.CallerMelon.MelonInfo.Name))
                {
                    return 1;
                }
                if (other.HookPriority == this.HookPriority)
                {
                    MelonLogger.Error($"Both '{other.CallerMelon.MelonInfo.Name}' and '{this.CallerMelon.MelonInfo.Name}' requested to run after each other with a matching priority ({HookPriority}) in hook of {TargetMethodData.GetFullName()}. This may cause unexpected behavior!");
                    return 0;
                }
            }
            else if (other._runAfter.Contains(this.CallerMelon.MelonInfo.Name))
            {
                return -1;
            }


            return HookPriority.CompareTo(other.HookPriority);
        }
    }
}