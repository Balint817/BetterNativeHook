﻿using MelonLoader;
using System.Security;

namespace BetterNativeHook
{
    [SecurityCritical]
    [PatchShield]
    public sealed record class ParameterInfo
    {
        /// <summary>
        /// The underlying pointer of the parameter
        /// </summary>
        public IntPtr Value { get; private set; }

        /// <summary>
        /// The name of the parameter. Null if the ParameterInfo points to the modifed return value (see ParameterIndex)
        /// </summary>
        public string Name { get; }
        /// <summary>
        /// The index of the parameter. -1 if the ParameterInfo points to the modified return value
        /// </summary>
        public int Index { get; }
        /// <summary>
        /// The type of the parameter (from it's MethodInfo)
        /// </summary>
        public Type ReflectedType { get; }
        internal ParameterInfo(int parameterIndex, string parameterName, Type reflectedType, IntPtr originalPointer = default)
        {
            Index = parameterIndex;
            Name = parameterName;
            ReflectedType = reflectedType;
            Value = originalPointer;
        }
        /// <summary>
        /// Set to override the the parameter's pointer. Modifications are applied once control is lost.
        /// <para></para>
        /// Leave on the default value of <c>null</c> to not modify.
        /// </summary>
        public IntPtr? Override { get; set; }

        protected internal void SetOverrides()
        {
            if (Override is null)
            {
                return;
            }
            Value = Override.Value;
            Override = null;
        }
    }
}