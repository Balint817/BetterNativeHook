using MelonLoader;
using System.Collections.ObjectModel;
using System.Security;

namespace BetterNativeHook
{
    [SecurityCritical]
    [PatchShield]
    public sealed record class ReturnValueReference
    {
        FakeAssembly _assembly;
        ReadOnlyCollection<ParameterReference> _parameters;

        /// <returns>
        /// <br/>
        /// - <see cref="OriginalValue"/>, if not <see langword="null"/>
        /// <br/>
        /// - Invokes the trampoline, sets <see cref="OriginalValue"/> to the result, and returns it
        /// </returns>
        public IntPtr InvokeTrampoline()
        {
            if (OriginalValue is { } ptr)
            {
                return ptr;
            }
            OriginalValue = ptr = _assembly.InvokeTrampoline(_parameters);
            CurrentValue ??= OriginalValue;
            return ptr;
        }
        /// <summary>
        /// The original pointer of the return value.
        /// This will be <see langword="null"/> until the trampoline is called.
        /// </summary>
        public IntPtr? OriginalValue { get; private set; }
        /// <summary>
        /// The modified pointer of the return value.
        /// This will be <see langword="null"/> until the trampoline is called, or an <see cref="Override"/> is specified.
        /// </summary>
        public IntPtr? CurrentValue { get; private set; }
        /// <summary>
        /// The type of the return value (from it's MethodInfo)
        /// </summary>
        public readonly Type ReflectedType;
        internal ReturnValueReference(Type reflectedType, FakeAssembly assembly, ReadOnlyCollection<ParameterReference> parameters)
        {
            ReflectedType = reflectedType;
            _assembly = assembly;
            _parameters = parameters;
        }
        /// <summary>
        /// Set to override the pointer. Modifications are applied once the event loses control.
        /// <para></para>
        /// Leave on the default value of <c>null</c> to not modify.
        /// </summary>
        public IntPtr? Override { get; set; }
        internal void SetOverrides()
        {
            if (!Override.HasValue)
            {
                return;
            }
            CurrentValue = Override.Value;
            Override = null;
        }
        public override string ToString()
        {
            return $"{ReflectedType.Name ?? "<type>"} = {(!OriginalValue.HasValue ? "<NA>" : OriginalValue.Value.ToInt64())}" + (!Override.HasValue ? (!CurrentValue.HasValue ? "" : CurrentValue.Value.ToInt64()) : $"->{Override.Value.ToInt64()}");
        }
        /// <returns>
        /// <br/>
        /// - <see cref="Override"/>, if not <see langword="null"/>
        /// <br/>
        /// - <see cref="CurrentValue"/>, if not <see langword="null"/>
        /// <br/>
        /// - <see cref="InvokeTrampoline()"/>
        /// </returns>
        public IntPtr GetValueOrInvokeTrampoline()
        {
            if (Override is { } v1)
            {
                return v1;
            }
            if (CurrentValue is { } v2)
            {
                return v2;
            }
            return InvokeTrampoline();
        }
        /// <returns>
        /// <br/>
        /// - <see cref="Override"/>, if not <see langword="null"/>
        /// <br/>
        /// - <see cref="CurrentValue"/>, if not <see langword="null"/>
        /// <br/>
        /// - <see cref="OriginalValue"/>, even if it is <see langword="null"/>
        /// </returns>
        public IntPtr? GetValueWithoutInvoke()
        {
            if (Override is { } v1)
            {
                return v1;
            }
            if (CurrentValue is { } v2)
            {
                return v2;
            }
            return OriginalValue;
        }
    }
}