using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetterNativeHook
{
    public abstract class ValueReference
    {
        internal ValueReference(Type reflectedType)
        {
            ReflectedType = reflectedType;
        }
        /// <summary>
        /// The original pointer of the value.
        /// If this is the return value, this will be <see langword="null"/> until the trampoline is called.
        /// </summary>
        public IntPtr? OriginalValue { get; protected set; }
        /// <summary>
        /// The modified pointer of the value.
        /// If this is the return value, this will be <see langword="null"/> until the trampoline is called, or an <see cref="Override"/> is specified.
        /// </summary>
        public IntPtr? CurrentValue { get; protected set; }
        /// <summary>
        /// The type of the value (as shown in the MethodInfo)
        /// </summary>
        public Type ReflectedType { get; protected init; }
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
            var sb = new StringBuilder($"<{ReflectedType.Name}> value = ");
            if (OriginalValue is { } originalValue)
            {
                sb.Append(originalValue.ToInt64());
            }
            else
            {
                sb.Append("<NA>");
            }

            if (CurrentValue is { } currentValue)
            {
                sb.Append("->");
                sb.Append(currentValue.ToInt64());
            }

            if (Override is { } overrideValue)
            {
                sb.Append("->");
                sb.Append(overrideValue.ToInt64());
            }

            return sb.ToString();
        }
        /// <returns>
        /// <br/>
        /// - <see cref="Override"/>, if not <see langword="null"/>
        /// <br/>
        /// - <see cref="CurrentValue"/>, if not <see langword="null"/>
        /// <br/>
        /// - <see cref="OriginalValue"/>, even if it is <see langword="null"/>
        /// </returns>
        public IntPtr? GetOverrideOrValue()
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
