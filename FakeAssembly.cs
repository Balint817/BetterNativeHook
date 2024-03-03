using HarmonyLib;
using MelonLoader;
using MelonLoader.NativeUtils;
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
            var asmBuilder = AssemblyBuilder.DefineDynamicAssembly(asmName, AssemblyBuilderAccess.Run);


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

            // declare the variables
            var pointerArrayVariable = ilgenerator1.DeclareLocal(typeof(IntPtr[]));
            var result = ilgenerator1.DeclareLocal(typeof(IntPtr));
            var exceptionVariable = ilgenerator1.DeclareLocal(typeof(TrampolineInvocationException));

            // start a try-catch block
            ilgenerator1.BeginExceptionBlock();

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

            // pushes the fake assembly's index on top of the stack
            ilgenerator1.Emit(OpCodes.Ldc_I4, assemblyIndex);

            /// pops off the fake assembly's index from the stack,
            /// calls <see cref="GetTrampolineByIndex(int)"/>,
            /// and pushes the return value (the trampoline delegate object) on top of the stack
            ilgenerator1.Emit(OpCodes.Call, AccessTools.Method(typeof(FakeAssembly), nameof(GetTrampolineByIndex)));


            for (int i = 0; i < arraySize; i++)
            {
                // pushes the current argument onto the stack
                ilgenerator1.Emit(OpCodes.Ldarg, i);
            }

            // pops off the previously returned value and all the arguments from the stack,
            // invokes the returned delegate using the generated Invoke method (see above),
            // and pushes the return value on top of the stack
            ilgenerator1.Emit(OpCodes.Callvirt, delegateTypeInvokeMethod);


            // pushes the fake assembly's index on top of the stack again
            ilgenerator1.Emit(OpCodes.Ldc_I4, assemblyIndex);
            // pushes the array on top of the stack
            ilgenerator1.Emit(OpCodes.Ldloc, pointerArrayVariable);
            // pops the index and array from the stack,
            /// calls <see cref="GenericNativeHook.HandleCallback(IntPtr, int, IntPtr[])"/>,
            // and pushes the return value onto the stack
            ilgenerator1.Emit(OpCodes.Call, AccessTools.Method(typeof(GenericNativeHook), nameof(GenericNativeHook.HandleCallback)));
            //pops the return value from the stack
            //and stores it in a local variable (result)
            ilgenerator1.Emit(OpCodes.Stloc, result);

            // starts catch block (any Exception),
            // and pushes the exception onto the stack
            ilgenerator1.BeginCatchBlock(typeof(Exception));

            // pushes the assembly's index onto the stack
            ilgenerator1.Emit(OpCodes.Ldc_I4, assemblyIndex);
            // pops the exception and the index from the stack,
            /// instantiates an exception using <see cref="TrampolineInvocationException(Exception, int)"/>
            // and pushes the instance onto the stack
            ilgenerator1.Emit(OpCodes.Newobj, AccessTools.Constructor(typeof(TrampolineInvocationException), new Type[] { typeof(Exception), typeof(int) }));

            // pops the created exception from the stack,
            /// and calls <see cref="MelonLogger.Error(object)"/>
            ilgenerator1.Emit(OpCodes.Call, AccessTools.Method(typeof(MelonLogger), nameof(MelonLogger.Error), new Type[] { typeof(object) }));

            ilgenerator1.EmitWriteLine("The program will exit to prevent further issues and/or corruption");

            // pushes an int value (wait time) onto the stack
            ilgenerator1.Emit(OpCodes.Ldc_I4, 5000);
            // pops the wait time from the stack,
            /// and calls <see cref="Thread.Sleep(int)"/>
            ilgenerator1.Emit(OpCodes.Call, AccessTools.Method(typeof(Thread), nameof(Thread.Sleep), new Type[] { typeof(int) }));
            // pushes a null reference onto the stack
            ilgenerator1.Emit(OpCodes.Ldnull);
            ilgenerator1.Emit(OpCodes.Castclass, typeof(string));
            /// calls <see cref="Environment.FailFast(string?)"/>
            ilgenerator1.Emit(OpCodes.Call, AccessTools.Method(typeof(Environment), nameof(Environment.FailFast), new Type[] { typeof(string) }));

            // the code below is only here to make the compiler happy,
            // since there is a FailFast immediately above which will halt the program anyway

            // pushes 0 onto the stack
            ilgenerator1.Emit(OpCodes.Ldc_I4, 0);
            // pops the value off the stack,
            // casts it to an IntPtr,
            // then pushes it back onto the stack
            ilgenerator1.Emit(OpCodes.Castclass, typeof(IntPtr));
            // stores the value in the result,
            // essentially returning (IntPtr)0
            ilgenerator1.Emit(OpCodes.Stloc, result);

            // ends the catch block
            ilgenerator1.EndExceptionBlock();

            // pushes the result from the variable onto the stack
            ilgenerator1.Emit(OpCodes.Ldloc, result);
            // pops the result from the stack and returns it to the caller
            ilgenerator1.Emit(OpCodes.Ret);

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

            // The assembly is built and the hook can be attached
            IsBuilt = true;
        }
        Type? _delegateType;
        Type? _cachedType;
    }
}