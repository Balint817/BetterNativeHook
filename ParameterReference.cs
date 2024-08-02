using MelonLoader;
using System.Security;

namespace BetterNativeHook
{
    [SecurityCritical]
    [PatchShield]
    public sealed class ParameterReference: ValueReference
    {
        /// <summary>
        /// The name of the parameter.
        /// <para>May be null if the method was generated and the names weren't specified</para>
        /// </summary>
        public readonly string? Name;
        /// <summary>
        /// The index of the parameter.
        /// </summary>
        public readonly int Index;

        public IntPtr GetNotNull()
        {
            return GetOverrideOrValue() ?? throw new NullReferenceException();
        }
        internal ParameterReference(int parameterIndex, string? parameterName, Type reflectedType, IntPtr initialValue = default): base(reflectedType)
        {
            Index = parameterIndex;
            Name = parameterName;
            ReflectedType = reflectedType;
            CurrentValue = OriginalValue = initialValue;
        }
    }
}