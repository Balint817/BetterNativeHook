using MelonLoader;
using System.Reflection;
using System.Reflection.Emit;
using System.Security;
using Il2CppInterop.Common;

namespace BetterNativeHook
{
    [SecurityCritical]
    [PatchShield]
    public class TargetMethodData
    {
        public Type ReturnType => Method.ReturnType;
        public Type TargetType => Method.ReflectedType ?? Method.DeclaringType!;
        public string Name => Method.Name;
        public System.Reflection.ParameterInfo[] Parameters => Method.GetParameters();
        public Type[] GenericTypes => Method.GetGenericArguments();
        public MethodInfo Method { get; }
        internal IntPtr HookableMethodPointer { get; }
        internal IntPtr NativeMethodPointer { get; }
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
        internal static TargetMethodData GetInstance(MethodInfo targetMethod)
        {
            var newMethodData = new TargetMethodData(targetMethod);
            if (_instances.FirstOrDefault(newMethodData) is TargetMethodData existingData)
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