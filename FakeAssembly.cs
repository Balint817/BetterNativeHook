using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MelonLoader.NativeUtils;
using MelonLoader;
using System.Reflection;
using System.Reflection.Emit;
using System.Security;
using HarmonyLib;

namespace BetterNativeHook
{
    [SecurityCritical]
    [PatchShield]
    internal class FakeAssembly
    {
        public object Hook = null!;
        MethodInfo Hook_Method_Attach = null!;
        MethodInfo Hook_Method_Detach = null!;
        PropertyInfo Hook_Property_Detour = null!;
        PropertyInfo Hook_Property_Target = null!;
        PropertyInfo Hook_Property_Trampoline = null!;
        public bool IsAttached { get; private set; }
        dynamic _trampoline;
        public dynamic Trampoline
        {
            get
            {
                return IsAttached ? _trampoline : throw new InvalidOperationException("cannot get trampoline from detached hook");
            }
            private set
            {
                _trampoline = value;
            }
        }

        public IntPtr InvokeTrampoline(IntPtr[] args)
        {
            if (!IsAttached)
            {
                throw new InvalidOperationException("Attempted to invoke trampoline on detached hook");
            }
            return (IntPtr)_fakeTrampolineMethod(Trampoline, args)!;
        }

        public void AttachHook()
        {
            if (IsAttached)
            {
                return;
            }
            BuildAssembly();
            Hook_Method_Attach.Invoke(Hook, null);
            IsAttached = true;
            _trampoline = Hook_Property_Trampoline.GetValue(Hook)!;
        }
        public void DetachHook()
        {
            if (!IsAttached)
            {
                return;
            }
            Hook_Method_Detach.Invoke(Hook, null);
            IsAttached = false;
            _trampoline = null!;
        }
        IntPtr Detour
        {
            get
            {
                return (IntPtr)Hook_Property_Detour.GetValue(Hook)!;
            }
            set
            {
                Hook_Property_Detour.SetValue(Hook, value);
            }
        }
        IntPtr Target
        {
            get
            {
                return (IntPtr)Hook_Property_Target.GetValue(Hook)!;
            }
            set
            {
                Hook_Property_Target.SetValue(Hook, value);
            }
        }


        static List<FakeAssembly> _instances = new();

        static MethodInfo GenericNativeHookCallback = AccessTools.Method(typeof(GenericNativeHook), nameof(GenericNativeHook.HandleCallback))!;
        public static FakeAssembly GetAssemblyByIndex(int index)
        {
            return _instances[index];
        }

        public static FakeAssembly GetInstance(TargetMethodData targetMethodInfo)
        {
            return _instances.FirstOrDefault(x => x.BoundMethodData == targetMethodInfo) ?? new FakeAssembly(targetMethodInfo);
        }
        public TargetMethodData BoundMethodData { get; }
        private FakeAssembly(TargetMethodData boundMethodInfo)
        {
            BoundMethodData = boundMethodInfo;
            _instances.Add(this);
        }
        public bool IsBuilt { get; private set; }

        const string assemblyName = "FakeAssembly";
        const string namespaceName = "FakeNamespace";
        const string typeName = "FakeType";
        const string unmanagedMethodName = "UnmanagedMethod";
        public static readonly string instanceParamName = "instance";
        public static readonly string nativeMethodPtrName = "nativeMethodInfo";
        public static readonly string trampolineDelegateName = "DynamicTrampolineDelegate";
        public static readonly string trampolineDelegateInvokerName = "DynamicTrampolineDelegateInvoker";
        public static readonly string trampolineInvokerMethodName = "DynamicTrampolineInvoker";
        public void BuildAssembly()
        {
            if (IsBuilt)
            {
                return;
            }


            // all parameters as <IntPtr> type and "IntPtr nativeMethodInfo" parameter
            int arraySize = BoundMethodData.Parameters.Length + 2;
            var returnType = typeof(IntPtr);
            var parameterTypes = Enumerable.Repeat(returnType, arraySize).ToArray();

            var assemblyIndex = _instances.IndexOf(this);

            // creates the assembly
            var asmName = new AssemblyName(assemblyName + assemblyIndex);
            var asmBuilder = AssemblyBuilder.DefineDynamicAssembly(asmName, AssemblyBuilderAccess.Run);


            // defines the namespace
            var modBuilder = asmBuilder.DefineDynamicModule(namespaceName + assemblyIndex);

            // defines an internal type 
            var typeBuilder = modBuilder.DefineType($"{namespaceName}.{typeName}" + assemblyIndex, TypeAttributes.Class | TypeAttributes.NotPublic);

            // defines a private static method, with IntPtr return type and <arraySize> number of parameters
            var unmanagedMethodBuilder = typeBuilder.DefineMethod(unmanagedMethodName, MethodAttributes.Public | MethodAttributes.Static, CallingConventions.Standard, returnType, parameterTypes);


            // set parameter names
            var parameters = BoundMethodData.Parameters;
            int idx = 0;
            // set first parameter to "instance" if not static
            unmanagedMethodBuilder.DefineParameter(0, ParameterAttributes.None, instanceParamName);
            for (idx = 1; idx < arraySize - 1; idx++)
            {
                unmanagedMethodBuilder.DefineParameter(idx, ParameterAttributes.None, parameters[idx - 1].Name);
            }
            // set last parameter to "nativeMethodInfo"
            unmanagedMethodBuilder.DefineParameter(arraySize - 1, ParameterAttributes.None, nativeMethodPtrName);
            // creates an [UnmanagedCallersOnly] attribute with 'Cdecl' calling convention
            var constructorInfo = typeof(UnmanagedCallersOnlyAttribute).GetConstructors().First();
            var fieldInfo = typeof(UnmanagedCallersOnlyAttribute).GetField(nameof(UnmanagedCallersOnlyAttribute.CallConvs))!;
            var attributeBuilder = new CustomAttributeBuilder(constructorInfo, Array.Empty<object>(), new FieldInfo[] { fieldInfo }, new object[] { new[] { typeof(CallConvCdecl) } });
            // attaches [UnmanagedCallersOnly] to the method
            unmanagedMethodBuilder.SetCustomAttribute(attributeBuilder);
            // starts building the method body
            var ilgenerator1 = unmanagedMethodBuilder.GetILGenerator();
            // declares an 'IntPtr[]' variable
            var pointerArray = ilgenerator1.DeclareLocal(typeof(IntPtr[]));
            // pushes the desired 'arraySize' on the stack
            ilgenerator1.Emit(OpCodes.Ldc_I4, arraySize);
            // this actually creates the array instance, and pushes it onto the stack
            ilgenerator1.Emit(OpCodes.Newarr, typeof(IntPtr));
            // pops off the top of the stack (the array) and stores it in our variable
            ilgenerator1.Emit(OpCodes.Stloc, pointerArray);
            for (int i = 0; i < arraySize; i++)
            {
                // pushes the array onto the stack
                ilgenerator1.Emit(OpCodes.Ldloc, pointerArray);
                // pushes the index onto the stack
                ilgenerator1.Emit(OpCodes.Ldc_I4, i);
                // pushes the argument onto the stack
                ilgenerator1.Emit(OpCodes.Ldarg, i);
                // pops off the value, then the index, then the array from the stack,
                // and the value is inserted into the array at the given index
                ilgenerator1.Emit(OpCodes.Stelem, typeof(IntPtr));
            }

            // pushes the fake assembly's index on top of the stack
            ilgenerator1.Emit(OpCodes.Ldc_I4, assemblyIndex);
            // pushes the array on top of the stack
            ilgenerator1.Emit(OpCodes.Ldloc, pointerArray);
            // pop the index and array from the stack,
            // call GenericNativeHook.HandleCallback(index, array),
            // and push the return value onto the stack
            ilgenerator1.Emit(OpCodes.Call, GenericNativeHookCallback);
            // pop the return value from the stack
            // and return it to the caller
            ilgenerator1.Emit(OpCodes.Ret);



            // Define the delegate type
            TypeBuilder trampolineDelegateBuilder = modBuilder.DefineType(
                trampolineDelegateName + assemblyIndex,
                TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AutoClass | TypeAttributes.AnsiClass,
                typeof(MulticastDelegate)
            );

            // Define the constructor
            ConstructorBuilder trampolineDelegateConstructorBuilder = trampolineDelegateBuilder.DefineConstructor(
                MethodAttributes.RTSpecialName | MethodAttributes.HideBySig | MethodAttributes.Public,
                CallingConventions.Standard,
                new Type[] { typeof(object), typeof(IntPtr) }
            );
            trampolineDelegateConstructorBuilder.SetImplementationFlags(MethodImplAttributes.Runtime | MethodImplAttributes.Managed);

            // Define the Invoke method
            MethodBuilder trampolineDelegateInvokeMethodBuilder = trampolineDelegateBuilder.DefineMethod(
                "Invoke",
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual,
                returnType,
                parameterTypes
            );
            trampolineDelegateInvokeMethodBuilder.SetImplementationFlags(MethodImplAttributes.Runtime | MethodImplAttributes.Managed);

            // Create and store the delegate type
            _delegateType = trampolineDelegateBuilder.CreateType()!;
            _delegateTypeInvoke = AccessTools.Method(_delegateType, "Invoke");





            // Define the delegate type
            TypeBuilder fakeTrampolineDelegateBuilder = modBuilder.DefineType(
                trampolineDelegateInvokerName + assemblyIndex,
                TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AutoClass | TypeAttributes.AnsiClass,
                typeof(MulticastDelegate)
            );

            // Define the constructor
            ConstructorBuilder fakeTrampolineDelegateConstructorBuilder = fakeTrampolineDelegateBuilder.DefineConstructor(
                MethodAttributes.RTSpecialName | MethodAttributes.HideBySig | MethodAttributes.Public,
                CallingConventions.Standard,
                new Type[] { typeof(object), typeof(IntPtr) }
            );
            fakeTrampolineDelegateConstructorBuilder.SetImplementationFlags(MethodImplAttributes.Runtime | MethodImplAttributes.Managed);

            // Define the Invoke method
            MethodBuilder fakeTrampolineDelegateInvokeMethodBuilder = fakeTrampolineDelegateBuilder.DefineMethod(
                "Invoke",
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual,
                typeof(IntPtr),
                new Type[] { _delegateType, typeof(IntPtr[]) }
            );
            fakeTrampolineDelegateInvokeMethodBuilder.SetImplementationFlags(MethodImplAttributes.Runtime | MethodImplAttributes.Managed);

            // Create and store the delegate type
            _fakeDelegateType = fakeTrampolineDelegateBuilder.CreateType()!;



            // defines a private static method, with IntPtr return type and <arraySize> number of parameters
            var trampolineMethodBuilder = typeBuilder.DefineMethod(trampolineInvokerMethodName,
                MethodAttributes.Public | MethodAttributes.Static,
                CallingConventions.Standard, typeof(IntPtr), new Type[] { _delegateType, typeof(IntPtr[]) });

            var ilgenerator2 = trampolineMethodBuilder.GetILGenerator();
            // pushes the delegate onto the stack
            ilgenerator2.Emit(OpCodes.Ldarg_0);

            for (int i = 0; i < arraySize; i++)
            {
                // pushes the array onto the stack
                ilgenerator2.Emit(OpCodes.Ldarg_1);
                // pushes the index onto the stack
                ilgenerator2.Emit(OpCodes.Ldc_I4, i);
                // pops off the index and the array from the stack,
                // and pushes the element onto the stack
                ilgenerator2.Emit(OpCodes.Ldelem, typeof(IntPtr));
            }
            // pops off the delegate (instance in 'Invoke') from the stack
            // pops off all arguments from the stack,
            // virtual calls the function,
            // and pushes the result onto the stack
            ilgenerator2.Emit(OpCodes.Callvirt, _delegateTypeInvoke);
            // pops off the result from the stack,
            // casts it to IntPtr,
            // and pushes the result to the stack
            //ilgenerator2.Emit(OpCodes.Castclass, typeof(IntPtr));
            // pops off the IntPtr from the stack
            // and returns it to the caller
            ilgenerator2.Emit(OpCodes.Ret);

            _cachedType = typeBuilder.CreateType()!;
            _cachedUnmanagedMethodInfo = _cachedType.GetMethod(unmanagedMethodName)!;

            var fakeTrampolineMi = _cachedType.GetMethod(trampolineInvokerMethodName)!;
            _fakeTrampolineMethod = Delegate.CreateDelegate(_fakeDelegateType, fakeTrampolineMi);

            var hookType = typeof(NativeHook<>).MakeGenericType(_delegateType);
            Hook = Activator.CreateInstance(hookType)!;
            Hook_Method_Attach = hookType.GetMethod(nameof(NativeHook<Delegate>.Attach))!;
            Hook_Method_Detach = hookType.GetMethod(nameof(NativeHook<Delegate>.Detach))!;
            Hook_Property_Detour = hookType.GetProperty(nameof(NativeHook<Delegate>.Detour))!;
            Hook_Property_Target = hookType.GetProperty(nameof(NativeHook<Delegate>.Target))!;
            Hook_Property_Trampoline = hookType.GetProperty(nameof(NativeHook<Delegate>.Trampoline))!;

            Target = BoundMethodData.HookableMethodPointer;

            Detour = _cachedDetour = _cachedUnmanagedMethodInfo.MethodHandle.GetFunctionPointer();

            IsBuilt = true;
        }
        dynamic _fakeTrampolineMethod { get; set; }
        Type _delegateType { get; set; }
        Type _fakeDelegateType { get; set; }
        MethodInfo _delegateTypeInvoke { get; set; }
        IntPtr _cachedDetour { get; set; }
        MethodInfo _cachedUnmanagedMethodInfo { get; set; }
        Type _cachedType { get; set; }
    }
}