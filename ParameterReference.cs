using MelonLoader;
using System.Security;

namespace BetterNativeHook
{
    [SecurityCritical]
    [PatchShield]
    public sealed record class ParameterReference
    {
        /// <summary>
        /// The original pointer of the parameter
        /// </summary>
        public readonly IntPtr OriginalValue;
        /// <summary>
        /// The modified pointer of the parameter
        /// </summary>
        public IntPtr CurrentValue;

        /// <summary>
        /// The name of the parameter.
        /// <para>May be null if the method was generated and the names weren't specified</para>
        /// </summary>
        public readonly string? Name;
        /// <summary>
        /// The index of the parameter.
        /// </summary>
        public readonly int Index;
        /// <summary>
        /// The type of the parameter (from it's MethodInfo)
        /// </summary>
        public readonly Type ReflectedType;
        internal ParameterReference(int parameterIndex, string? parameterName, Type reflectedType, IntPtr initialValue = default)
        {
            Index = parameterIndex;
            Name = parameterName;
            ReflectedType = reflectedType;
            CurrentValue = OriginalValue = initialValue;
        }
        public override string ToString()
        {
            return $"{ReflectedType.Name??"<type>"} {Name??"<null>"} = {OriginalValue.ToInt64()}" + (CurrentValue != OriginalValue ? "" : $"->{CurrentValue.ToInt64()}");
        }
    }
}