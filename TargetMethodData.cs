using Il2CppInterop.Common;
using MelonLoader;
using MelonLoader.NativeUtils;
using System.Reflection;
using System.Reflection.Emit;
using System.Security;

namespace BetterNativeHook
{
    [SecurityCritical]
    [PatchShield]
    public class TargetMethodData
    {
        /// <summary>
        /// Gets the formatted name of the method represented by this instance.
        /// </summary>
        /// <returns>The method's info formatted as: Name&lt;T1, ..., Tn&gt;(type1, ..., typeN)</returns>
        public string GetFullName() => $"{Name}{(GenericTypes.Length == 0 ? "" : $"<{string.Join(", ", GenericTypes.Select(x => x.Name))}>")}({string.Join(", ", Parameters.Select(x => x.ParameterType.Name))})";
        /// <summary>
        /// Gets the return type of the method represented by this instance.
        /// </summary>
        public Type ReturnType => Method.ReturnType;
        /// <summary>
        /// Gets the type that declares the method represented by this instance.
        /// </summary>
        public Type TargetType => Method.ReflectedType ?? Method.DeclaringType!;
        /// <summary>
        /// Gets the type that declares the method represented by this instance.
        /// </summary>
        public string Name => Method.Name;
        /// <summary>
        /// Gets the parameters of the method represented by this instance.
        /// </summary>
        public System.Reflection.ParameterInfo[] Parameters => Method.GetParameters();
        /// <summary>
        /// Gets the generic arguments of the method represented by this instance.
        /// </summary>
        public Type[] GenericTypes => Method.GetGenericArguments();
        /// <summary>
        /// The <see cref="MethodInfo"/> that this instance represents.
        /// </summary>
        public MethodInfo Method { get; }
        /// <summary>
        /// A pointer that <see cref="NativeHook{T}"/> can hook into.
        /// </summary>
        internal IntPtr HookableMethodPointer { get; }
        /// <summary>
        /// Directly resulting pointer from IL2CPP.
        /// </summary>
        internal IntPtr NativeMethodPointer { get; }

        /// Won't be null once control falls back to the caller of <see cref="GetInstance(MethodInfo)"/>
        internal FakeAssembly FakeAssembly { get; }
        public override bool Equals(object? obj)
        {
            if (obj is not TargetMethodData hookInfo)
            {
                return base.Equals(obj);
            }
            if (hookInfo.Name != Name || hookInfo.TargetType != TargetType)
            {
                return false;
            }
            var parameters1 = Parameters;
            var parameters2 = hookInfo.Parameters;
            if (parameters1.Length != parameters2.Length)
            {
                return false;
            }
            var genericTypes1 = GenericTypes;
            var genericTypes2 = hookInfo.GenericTypes;
            if (genericTypes1.Length != genericTypes2.Length)
            {
                return false;
            }
            for (int i = 0; i < genericTypes1.Length; i++)
            {
                if (genericTypes1[i] != genericTypes2[i])
                {
                    return false;
                }
            }
            for (int i = 0; i < parameters1.Length; i++)
            {
                if (parameters1[i].ParameterType != parameters2[i].ParameterType)
                {
                    return false;
                }
            }
            return true;
        }
        public override int GetHashCode()
        {
            return TargetType.GetHashCode()
                + GenericTypes.Select(x => x.GetHashCode()).Aggregate((a, b) => unchecked(a + b))
                + Parameters.Select(x => x.ParameterType.GetHashCode()).Aggregate((a, b) => unchecked(a + b))
                + Name.GetHashCode();
        }

        private static HashSet<TargetMethodData> _instances = new();
        /// <summary>
        /// Returns a new instance with the given <see cref="MethodInfo"/>.
        /// <para></para>
        /// If an instance with the given <see cref="MethodInfo"/> already exists, it is returned instead.
        /// </summary>
        /// <param name="targetMethod"></param>
        /// <returns>A <see cref="TargetMethodData"/> instance</returns>
        internal static TargetMethodData GetInstance(MethodInfo targetMethod)
        {
            var newMethodData = new TargetMethodData(targetMethod);
            if (_instances.TryGetValue(newMethodData, out TargetMethodData? existingData))
            {
                return existingData;
            }
            _instances.Add(newMethodData);
            return newMethodData;

        }
        private unsafe TargetMethodData(MethodInfo targetMethod)
        {
            if (targetMethod is null)
            {
                throw new ArgumentNullException(nameof(targetMethod));
            }
            if (targetMethod.ContainsGenericParameters)
            {
                throw new ArgumentException("can't patch method where 'ContainsGenericParameters' is true", nameof(targetMethod));
            }
            if (targetMethod is MethodBuilder)
            {
                throw new NotSupportedException("MethodBuilder can't be invoked");
            }
            if (targetMethod is DynamicMethod)
            {
                throw new NotSupportedException("DynamicMethod can't be invoked");
            }
            FieldInfo fi;
            try
            {
                fi = Il2CppInteropUtils.GetIl2CppMethodInfoPointerFieldForGeneratedMethod(targetMethod);
            }
            catch (Exception)
            {
                throw new MemberAccessException("failed to get IL2CPP method for target");
            }
            if (fi is null)
            {
                throw new ArgumentException("target must be an IL2CPP method", nameof(targetMethod));
            }
            var getField = fi.GetValue(null);
            if (getField is null || getField is not IntPtr nativeMethodPtr || nativeMethodPtr == IntPtr.Zero)
            {
                throw new NullReferenceException("IL2CPP methodInfo field returned an invalid value");
            }
            this.Method = targetMethod;
            NativeMethodPointer = nativeMethodPtr;
            HookableMethodPointer = *(IntPtr*)nativeMethodPtr;
            FakeAssembly = FakeAssembly.GetInstance(this);
        }
    }
}