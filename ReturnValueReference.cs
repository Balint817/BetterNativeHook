using MelonLoader;
using System.Collections.ObjectModel;
using System.Security;

namespace BetterNativeHook
{
    [SecurityCritical]
    [PatchShield]
    public sealed class ReturnValueReference: ValueReference
    {
        readonly FakeAssembly _assembly;
        readonly ReadOnlyCollection<ParameterReference> _parameters;

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
        internal ReturnValueReference(Type reflectedType, FakeAssembly assembly, ReadOnlyCollection<ParameterReference> parameters): base(reflectedType)
        {
            ReflectedType = reflectedType;
            _assembly = assembly;
            _parameters = parameters;
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
    }
}