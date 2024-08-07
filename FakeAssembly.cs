﻿using HarmonyLib;
using MelonLoader;
using MelonLoader.NativeUtils;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;


namespace BetterNativeHook
{
    /// <summary>
    /// Instances of this class generate and manage the IL required for generalized hooking (e.g. for this mod to work)
    /// <para>It was only made public to prevent MethodAccessExceptions in the generated IL</para>
    /// <para>It is for this reason that you really shouldn't touch any of this if you aren't 100% sure about what you are doing</para>
    /// <para>If you have to ask, then it's probably worth neither the time or trouble.</para>
    /// </summary>
    [SecurityCritical]
    [PatchShield]
    public sealed class FakeAssembly
    {
        private delegate IntPtr TrampolineInvoker(IntPtr[] args);
        internal IntPtr InvokeTrampoline(ReadOnlyCollection<ParameterReference> args)
        {
            return _trampolineInvoker!(args.Select(x => x.GetOverrideOrValue() ?? IntPtr.Zero).ToArray());
        }
        internal IntPtr InvokeTrampolineDirect(IntPtr[] args)
        {
            return _trampolineInvoker!(args);
        }

        internal static IntPtr InvokeTrampoline(int assemblyIdx, ReadOnlyCollection<ParameterReference> args)
        {
            return _instances[assemblyIdx].InvokeTrampoline(args);
        }
        TrampolineInvoker? _trampolineInvoker;
        internal object Hook = null!;
        MethodInfo Hook_Method_Attach = null!;
        MethodInfo Hook_Method_Detach = null!;
        PropertyInfo Hook_Property_Detour = null!;
        PropertyInfo Hook_Property_Target = null!;
        PropertyInfo Hook_Property_Trampoline = null!;
        internal bool IsAttached { get; private set; }
        Delegate? _trampoline;
        Delegate? Trampoline
        {
            get
            {
                return IsAttached ? _trampoline : throw new InvalidOperationException("cannot get trampoline from detached hook");
            }
            set
            {
                _trampoline = value;
            }
        }
        /// <summary>
        /// Builds the assembly, attaches the hook, and initializes <see cref="Trampoline"/>
        /// </summary>
        internal void AttachHook()
        {
            if (IsAttached)
            {
                return;
            }

            BuildAssembly();
            Hook_Method_Attach.Invoke(Hook, null);
            IsAttached = true;
            _trampoline = (Delegate?)Hook_Property_Trampoline.GetValue(Hook)!;
        }
        /// <summary>
        /// Detaches the hook, and deinitializes <see cref="Trampoline"/>
        /// </summary>
        internal void DetachHook()
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

        static readonly List<FakeAssembly> _instances = new();
        internal static FakeAssembly GetAssemblyByIndex(int index)
        {
            return _instances[index];
        }
        /// <summary>
        /// This method was made public to prevent MethodAccessException in MSIL, but you shouldn't call this method (not even internally).
        /// <para></para>
        /// It's sole caller should be the generated unmanaged method.
        /// </summary>
        public static Delegate GetTrampolineByIndex(int index)
        {
            var asm = _instances[index];
            return asm._trampoline ?? throw new NullReferenceException("failed to get trampoline (returned null)");
        }
        /// <summary>
        /// Searches for a <see cref="FakeAssembly"/> instance via it's <see cref="FakeAssembly.BoundMethodData"/> and the given <see cref="TargetMethodData"/> to compare to.
        /// <para>If an instance is not found, a new instance is created.</para>
        /// </summary>
        /// <param name="boundMethodData">The <see cref="TargetMethodData"/> that will be searched for</param>
        /// <returns></returns>
        internal static FakeAssembly GetInstance(TargetMethodData boundMethodData)
        {
            return _instances.FirstOrDefault(x => x.BoundMethodData == boundMethodData) ?? new FakeAssembly(boundMethodData);
        }
        public TargetMethodData BoundMethodData { get; }
        private FakeAssembly(TargetMethodData boundMethodData)
        {
            BoundMethodData = boundMethodData ?? throw new ArgumentNullException(nameof(boundMethodData));
            _instances.Add(this);
        }
        public bool IsBuilt { get; private set; }

        internal const string assemblyName = "GeneratedFakeAssembly";
        internal const string namespaceName = "GeneratedFakeNamespace";
        internal const string typeName = "FakeType";
        internal const string unmanagedMethodName = "UnmanagedMethod";
        internal const string trampolineInvokerMethodName = "InvokeTrampoline";
        internal const string trampolineDelegateName = "DynamicTrampolineDelegate";
        internal const string instanceParamName = "instance";
        internal const string nativeMethodPtrName = "nativeMethodInfo";

        /// <summary>
        /// Wraps <see cref="BuildAssemblyPrivate"/> in a try-catch.
        /// <para></para>
        /// If the build terminates halfway, raises an <see cref="AssemblyBuildFailedException"/>, and calls <see cref="Environment.FailFast(string?)"/>
        /// </summary>
        public void BuildAssembly()
        {
            try
            {
                BuildAssemblyPrivate();
            }
            catch (Exception ex)
            {
                MelonLogger.Error(new AssemblyBuildFailedException(this, ex).ToString());
                Thread.Sleep(5000);
                Environment.FailFast(null);
                throw;
            }
        }
        /// <summary>
        /// Builds the assembly if it hasn't already been built.
        /// <para>This includes initializing relevant fields (type fields, hook property/method fields)</para>
        /// </summary>
        void BuildAssemblyPrivate()
        {
            // we don't need to do anything if the assembly has already been built
            if (IsBuilt)
            {
                return;
            }

            // the "IntPtr instance" additional parameter,
            // all parameters as <IntPtr> type,
            // and "IntPtr nativeMethodInfo" additional parameter
            int arraySize = BoundMethodData.Parameters.Length + 2;
            var returnType = typeof(IntPtr);
            var parameterTypes = Enumerable.Repeat(returnType, arraySize).ToArray();

            var assemblyIndex = _instances.IndexOf(this);

            
            // creates the assembly
            var asmName = new AssemblyName(assemblyName + assemblyIndex);
            var asmBuilder = AssemblyBuilder.DefineDynamicAssembly(asmName, AssemblyBuilderAccess.RunAndCollect);


            // defines the namespace
            var modBuilder = asmBuilder.DefineDynamicModule(namespaceName + assemblyIndex);


            // Define the delegate type for the trampoline
            TypeBuilder trampolineDelegateBuilder = modBuilder.DefineType(
                trampolineDelegateName + assemblyIndex,
                TypeAttributes.Public | TypeAttributes.Sealed,
                typeof(MulticastDelegate)
            );

            // Define the constructor
            ConstructorBuilder trampolineDelegateConstructorBuilder = trampolineDelegateBuilder.DefineConstructor(
                MethodAttributes.RTSpecialName | MethodAttributes.HideBySig | MethodAttributes.Public,
                CallingConventions.Standard,
                new Type[] { typeof(object), typeof(IntPtr) }
            );
            trampolineDelegateConstructorBuilder.SetImplementationFlags(MethodImplAttributes.CodeTypeMask);

            // Define the Invoke method
            MethodBuilder trampolineDelegateInvokeMethodBuilder = trampolineDelegateBuilder.DefineMethod(
                "Invoke",
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual,
                returnType,
                parameterTypes
            );
            trampolineDelegateInvokeMethodBuilder.SetImplementationFlags(MethodImplAttributes.CodeTypeMask);

            var parameters = BoundMethodData.Parameters;

            // define the parameters
            for (int i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                trampolineDelegateInvokeMethodBuilder.DefineParameter(i+1, ParameterAttributes.None, parameter.Name);
            }

            // Create and store the delegate type
            _delegateType = trampolineDelegateBuilder.CreateType()!;
            // Store the Invoke method
            var delegateTypeInvokeMethod = AccessTools.Method(_delegateType, "Invoke");

            // defines an internal type 
            var typeBuilder = modBuilder.DefineType($"{namespaceName}.{typeName}" + assemblyIndex, TypeAttributes.Class | TypeAttributes.NotPublic);

            // defines a private static method, with IntPtr return type and <arraySize> number of parameters
            var unmanagedMethodBuilder = typeBuilder.DefineMethod(unmanagedMethodName, MethodAttributes.Public | MethodAttributes.Static, CallingConventions.Standard, returnType, parameterTypes);


            // set parameter names
            int idx = 0;
            // set first parameter to "instance" if not static
            unmanagedMethodBuilder.DefineParameter(0, ParameterAttributes.None, instanceParamName);
            for (idx = 1; idx < arraySize - 1; idx++)
            {
                unmanagedMethodBuilder.DefineParameter(idx, ParameterAttributes.None, parameters[idx - 1].Name + "ptr");
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

            // declare the variables
            var pointerArrayVariable = ilgenerator1.DeclareLocal(typeof(IntPtr[]));
            var result = ilgenerator1.DeclareLocal(typeof(IntPtr));

            // pushes the desired 'arraySize' on the stack
            ilgenerator1.Emit(OpCodes.Ldc_I4, arraySize);
            // this actually creates the array instance, and pushes it onto the stack
            ilgenerator1.Emit(OpCodes.Newarr, typeof(IntPtr));
            // pops off the top of the stack (the array) and stores it in our variable
            ilgenerator1.Emit(OpCodes.Stloc, pointerArrayVariable);

            for (int i = 0; i < arraySize; i++)
            {
                // pushes the array onto the stack
                ilgenerator1.Emit(OpCodes.Ldloc, pointerArrayVariable);
                // pushes the index onto the stack
                ilgenerator1.Emit(OpCodes.Ldc_I4, i);
                // pushes the argument onto the stack
                ilgenerator1.Emit(OpCodes.Ldarg, i);
                // pops off the value, then the index, then the array from the stack,
                // and the value is inserted into the array at the given index
                ilgenerator1.Emit(OpCodes.Stelem, typeof(IntPtr));
            }


            // pushes the fake assembly's index on top of the stack again
            ilgenerator1.Emit(OpCodes.Ldc_I4, assemblyIndex);
            // pushes the array on top of the stack
            ilgenerator1.Emit(OpCodes.Ldloc, pointerArrayVariable);
            // pops the index and array from the stack,
            /// calls <see cref="GenericNativeHook.HandleCallback(int, IntPtr[])"/>,
            // and pushes the return value onto the stack
            ilgenerator1.Emit(OpCodes.Call, AccessTools.Method(typeof(GenericNativeHook), nameof(GenericNativeHook.HandleCallback)));
            //pops the return value from the stack
            //and stores it in a local variable (result)
            ilgenerator1.Emit(OpCodes.Stloc, result);

            // pushes the result from the variable onto the stack
            ilgenerator1.Emit(OpCodes.Ldloc, result);
            // pops the result from the stack and returns it to the caller
            ilgenerator1.Emit(OpCodes.Ret);



            // defines a private static method, with IntPtr return type and an IntPtr[] argument
            var trampolineInvokeMethodBuilder = typeBuilder.DefineMethod(trampolineInvokerMethodName, MethodAttributes.Public | MethodAttributes.Static, CallingConventions.Standard, returnType, new Type[] { typeof(IntPtr[]) });
            trampolineInvokeMethodBuilder.DefineParameter(0, ParameterAttributes.None, "args");

            var ilgenerator2 = trampolineInvokeMethodBuilder.GetILGenerator();
            var exceptionLabel = ilgenerator2.DefineLabel();

            // pushes the first argument (IntPtr[] args) onto the stack
            ilgenerator2.Emit(OpCodes.Ldarg, 0);
            // pops off the array from stack,
            // and pushes the length of the array on top of the stack
            ilgenerator2.Emit(OpCodes.Ldlen);
            ilgenerator2.Emit(OpCodes.Conv_I4);
            // pushes the expected length onto the stack
            ilgenerator2.Emit(OpCodes.Ldc_I4, arraySize);
            // pops off the 2 numbers from the stack, and compares them.
            // if they are not equal, the branch operation is performed
            ilgenerator2.Emit(OpCodes.Bne_Un, exceptionLabel);


            // pushes the fake assembly's index on top of the stack
            ilgenerator2.Emit(OpCodes.Ldc_I4, assemblyIndex);

            // pops off the fake assembly's index from the stack,
            /// calls <see cref="GetTrampolineByIndex(int)"/>,
            // and pushes the return value (the trampoline delegate object) on top of the stack
            ilgenerator2.Emit(OpCodes.Call, AccessTools.Method(typeof(FakeAssembly), nameof(GetTrampolineByIndex)));

            for (int i = 0; i < arraySize; i++)
            {
                // pushes the array onto the stack
                ilgenerator2.Emit(OpCodes.Ldarg, 0);
                // pushes the index onto the stack
                ilgenerator2.Emit(OpCodes.Ldc_I4, i);
                // pops off the index and the array from the stack,
                // and the value at the given index is retrieved from the array
                ilgenerator2.Emit(OpCodes.Ldelem_I);
            }

            // pops off the previously returned value and all the arguments from the stack,
            // invokes the returned delegate using the generated Invoke method (see above),
            // and pushes the return value (IntPtr) on top of the stack
            ilgenerator2.Emit(OpCodes.Callvirt, delegateTypeInvokeMethod);

            // pops off the return value from the stack,
            // and returns it
            ilgenerator2.Emit(OpCodes.Ret);

            ilgenerator2.MarkLabel(exceptionLabel);

            /// gets the constructor <see cref="ArgumentException(string, string)"/>
            var argExCtor = typeof(ArgumentException).GetConstructor(new Type[] { typeof(string), typeof(string) })!;
            // pushes the specified string onto the stack (the exception message)
            ilgenerator2.Emit(OpCodes.Ldstr, $"expected array of length {arraySize}");
            // pushes the specified string onto the stack (the parameter's name)
            ilgenerator2.Emit(OpCodes.Ldstr, $"args");
            // pops the arguments from the stack,
            // creates a new object instance using the specified constructor,
            // and pushes the result on top of the stack.
            ilgenerator2.Emit(OpCodes.Newobj, argExCtor);

            // pops the exception from the top of the stack,
            // then throws the exception.
            ilgenerator2.Emit(OpCodes.Throw);


            // create and store the type
            _cachedType = typeBuilder.CreateType()!;

            /// get the <see cref="NativeHook{T}"/> type with the generated delegate type as T
            var hookType = typeof(NativeHook<>).MakeGenericType(_delegateType);
            // create an instance of the type
            Hook = Activator.CreateInstance(hookType)!;
            /// Get the properties and methods of <see cref="NativeHook{T}"/>
            /// Keep in mind that we're only using <see cref="NativeHook{Delegate}"/> with the <c>nameof()</c> operator,
            /// therefore, the type mismatch doesn't matter here
            Hook_Method_Attach = hookType.GetMethod(nameof(NativeHook<Delegate>.Attach))!;
            Hook_Method_Detach = hookType.GetMethod(nameof(NativeHook<Delegate>.Detach))!;
            Hook_Property_Detour = hookType.GetProperty(nameof(NativeHook<Delegate>.Detour))!;
            Hook_Property_Target = hookType.GetProperty(nameof(NativeHook<Delegate>.Target))!;
            Hook_Property_Trampoline = hookType.GetProperty(nameof(NativeHook<Delegate>.Trampoline))!;

            // Set the hook's target to the Il2Cpp method pointer via a property
            Target = BoundMethodData.HookableMethodPointer;

            // Get the unmanaged method from the generated type,
            // and set the hook's detour method to the generated method's pointer via a property
            Detour = _cachedType.GetMethod(unmanagedMethodName)!.MethodHandle.GetFunctionPointer();

            
            var trampolineInvokerMethodInfo = _cachedType.GetMethod(trampolineInvokerMethodName)!;
            _trampolineInvoker = (TrampolineInvoker)Delegate.CreateDelegate(typeof(TrampolineInvoker), trampolineInvokerMethodInfo);

            // The assembly is built and the hook can be attached
            IsBuilt = true;
        }
        Type? _delegateType;
        Type? _cachedType;
    }
}