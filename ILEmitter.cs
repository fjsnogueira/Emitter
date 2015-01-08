using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;

namespace Emitter
{
	public static class ILEmitter
	{
		private const string METHOD_DICTIONARY = "_methodImplementations";

		public static T New<T>(MethodImpl[] implementations = null) where T : class
		{
			var type = typeof(T);

			if (type.IsInterface)
				return ImplementInterface<T>(implementations);

			return null;
		}

		public static T ImplementInterface<T>(MethodImpl[] implementations = null) where T : class
		{
			var interfaceType = typeof(T);

			if (!interfaceType.IsInterface)
				throw new ArgumentException("Given type is not an interface");

			// Get the assembly and module builders. These are required to start defining these types
			var builders = GetAssemblyAndModuleBuilders();

			// Create the builder for the new type
			var typeBuilder = GetTypeBuilder(interfaceType, builders.Item2);

			var context = new TypeCreationContext(typeBuilder);

			// Now lets add the interface implementation
			typeBuilder.AddInterfaceImplementation(interfaceType);

			// Add implementation dictionary. This dictionary holds the delegates we call when a method on this dynamic implementation is invoked
			if (implementations != null)
				AddImplementationsDictionary(implementations, context);

			// First lets add the default constructor
			AddDefaultConstructor(context, implementations != null);

			// Now lets add each property in the interface to our new type
			var properties = interfaceType.GetProperties();
			foreach (var pi in properties)
				AddProperty(pi, context);

			// Finally lets add each method in the interface to our new type.
			// We have to explicitly exclude the getter and setter methods for the properties within the interface
			var methods = interfaceType.GetMethods().Where(mi => !mi.Name.StartsWith("get_") && !mi.Name.StartsWith("set_"));
			foreach (var mi in methods)
				AddMethod(mi, context, implementations != null && implementations.Any(i => i.MethodName == mi.Name));

			try
			{
				var actualType = typeBuilder.CreateType();

#if DEBUG
				builders.Item1.Save("TestAsm.dll");
#endif

				T instance = (T)Activator.CreateInstance(actualType);

				if (implementations != null)
				{
					var dictField = actualType.GetField(METHOD_DICTIONARY, BindingFlags.NonPublic | BindingFlags.Instance);

					var dict = (IDictionary<string, Delegate>)dictField.GetValue(instance);

					foreach (var impl in implementations)
						dict.Add(impl.MethodName, impl.Implementation);
				}

				return instance;
			}
			catch (Exception e)
			{
				Console.WriteLine(e.Message);
			}

			return null;
		}

		private static Tuple<AssemblyBuilder, ModuleBuilder> GetAssemblyAndModuleBuilders(string assemblyName = "DynamicAssembly")
		{
			var aName = new AssemblyName(assemblyName);

#if DEBUG
			var aBuilder = Thread.GetDomain().DefineDynamicAssembly(aName, AssemblyBuilderAccess.RunAndSave);

			return Tuple.Create(aBuilder, aBuilder.DefineDynamicModule(aName.Name, "TestAsm.dll"));
#else
			var aBuilder = Thread.GetDomain().DefineDynamicAssembly(aName, AssemblyBuilderAccess.Run);

			return Tuple.Create(aBuilder, aBuilder.DefineDynamicModule(aName.Name));
#endif
		}

		private static TypeBuilder GetTypeBuilder(Type type, ModuleBuilder mBuilder)
		{
			return mBuilder.DefineType(type.Name + "Impl", TypeAttributes.Public | TypeAttributes.Class);
		}

		private static void AddDefaultConstructor(TypeCreationContext context, bool hasImplementations)
		{
			var ci = typeof(object).GetConstructor(Type.EmptyTypes);

			var cBuilder = context.TypeBuilder.DefineConstructor(MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, CallingConventions.Standard, Type.EmptyTypes);

			var generator = cBuilder.GetILGenerator();

			generator.Emit(OpCodes.Ldarg_0);

			if (hasImplementations)
			{
				generator.Emit(OpCodes.Newobj, typeof(Dictionary<string, Delegate>).GetConstructor(Type.EmptyTypes));

				var fieldBuilder = context.Fields[METHOD_DICTIONARY];

				generator.Emit(OpCodes.Stfld, fieldBuilder);

				generator.Emit(OpCodes.Ldarg_0);
			}

			generator.Emit(OpCodes.Call, ci);
			generator.Emit(OpCodes.Ret);
		}

		private static void AddProperty(PropertyInfo pi, TypeCreationContext context)
		{
			var backingFieldName = "_" + pi.Name;

			var fieldBuilder = context.TypeBuilder.DefineField(backingFieldName, pi.PropertyType, FieldAttributes.Private);

			context.Fields.Add(backingFieldName, fieldBuilder);

			var getter = pi.GetGetMethod();
			if (getter != null)
			{
				var methodAttributes = MethodAttributes.Public | MethodAttributes.NewSlot | MethodAttributes.SpecialName | MethodAttributes.Virtual;
				if (getter.IsFinal)
					methodAttributes |= MethodAttributes.Final;

				var gmMethodBuilder = context.TypeBuilder.DefineMethod("get_" + pi.Name, methodAttributes, pi.PropertyType, Type.EmptyTypes);

				context.Methods.Add(gmMethodBuilder.Name, gmMethodBuilder);

				var gmGenerator = gmMethodBuilder.GetILGenerator();

				gmGenerator.Emit(OpCodes.Ldarg_0);
				gmGenerator.Emit(OpCodes.Ldfld, fieldBuilder);
				gmGenerator.Emit(OpCodes.Ret);

				context.TypeBuilder.DefineMethodOverride(gmMethodBuilder, getter);
			}

			var setter = pi.GetSetMethod();
			if (setter != null)
			{
				var methodAttributes = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName;
				if (setter.IsVirtual)
					methodAttributes |= MethodAttributes.Virtual;

				var smMethodBuilder = context.TypeBuilder.DefineMethod("set_" + pi.Name, methodAttributes, null, new Type[] { pi.PropertyType });

				context.Methods.Add(smMethodBuilder.Name, smMethodBuilder);

				var smGenerator = smMethodBuilder.GetILGenerator();

				smGenerator.Emit(OpCodes.Ldarg_0); // This is equivalent to "this"
				smGenerator.Emit(OpCodes.Ldarg_1); // This is the typical "value" parameter
				smGenerator.Emit(OpCodes.Stfld, fieldBuilder);
				smGenerator.Emit(OpCodes.Ret);

				context.TypeBuilder.DefineMethodOverride(smMethodBuilder, setter);
			}
		}

		private static void AddMethod(MethodInfo mi, TypeCreationContext context, bool hasImplementation)
		{
			// Get all the parameters to the method
			var parameterTypes = mi.GetParameters().Select(pi => pi.ParameterType).ToArray();

			var mBuilder = context.TypeBuilder.DefineMethod(mi.Name, MethodAttributes.Public | MethodAttributes.Virtual, mi.ReturnType, parameterTypes);

			context.Methods.Add(mi.Name, mBuilder);

			var mGenerator = mBuilder.GetILGenerator();

			if (mi.ReturnType != typeof(void))
			{
				// If no implementation has been provided, then generate the IL to return the default value of the return type
				if (!hasImplementation)
				{
					var localBuilder = mGenerator.DeclareLocal(mi.ReturnType);

					mGenerator.Emit(OpCodes.Ldloc, localBuilder);
				}
				// Otherwise things are going to get tricky here
				else
				{
					AddDelegateInvocation(mi, context, parameterTypes, mGenerator);
				}
			}

			mGenerator.Emit(OpCodes.Ret);
		}

		private static void AddDelegateInvocation(MethodInfo mi, TypeCreationContext context, Type[] parameterTypes, ILGenerator mGenerator)
		{
			var objArrType = typeof(object[]);
			var delegateType = typeof(Delegate);

			mGenerator.DeclareLocal(delegateType); // reference to delegate to call
			mGenerator.DeclareLocal(objArrType); // Delegate parameter array
			mGenerator.DeclareLocal(mi.ReturnType); // Return type
			mGenerator.DeclareLocal(objArrType); // ??? I think its the parameter array that goes on the stack to call the delegate

			// Generate the IL to get the delegate out of the private dictionary
			mGenerator.Emit(OpCodes.Nop);
			mGenerator.Emit(OpCodes.Ldarg_0);
			mGenerator.Emit(OpCodes.Ldfld, context.Fields[METHOD_DICTIONARY]);
			mGenerator.Emit(OpCodes.Ldstr, mi.Name);

			var dictGetMethod = typeof(IDictionary<string, Delegate>).GetMethod("get_Item", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

			mGenerator.Emit(OpCodes.Callvirt, dictGetMethod);
			mGenerator.Emit(OpCodes.Stloc_0);

			// Create the parameter array
			OpCode arraySize = GetSizeOpCode(parameterTypes.Length);

			mGenerator.Emit(arraySize);

			mGenerator.Emit(OpCodes.Newarr, typeof(System.Object));
			mGenerator.Emit(OpCodes.Stloc_3);
			mGenerator.Emit(OpCodes.Ldloc_3);

			// Add each parameter to the array
			for (int i = 0; i < parameterTypes.Length; i++)
			{
				mGenerator.Emit(GetSizeOpCode(i));
				mGenerator.Emit(GetArgumentOpCode(i + 1));

				if (parameterTypes[i].IsValueType)
					mGenerator.Emit(OpCodes.Box, GetBoxedType(parameterTypes[i]));

				mGenerator.Emit(OpCodes.Stelem_Ref);
				mGenerator.Emit(OpCodes.Ldloc_3);
			}

			// Copy the array over to the location 1
			mGenerator.Emit(OpCodes.Stloc_1);

			// Generate the IL to call the delegate
			mGenerator.Emit(OpCodes.Ldloc_0);
			mGenerator.Emit(OpCodes.Ldloc_1);
			mGenerator.Emit(OpCodes.Callvirt, delegateType.GetMethod("DynamicInvoke"));

			mGenerator.Emit(OpCodes.Unbox_Any, mi.ReturnType);
			mGenerator.Emit(OpCodes.Stloc_2);

			// Load the value for return purposes
			mGenerator.Emit(OpCodes.Nop);
			mGenerator.Emit(OpCodes.Ldloc_2);
		}

		private static void AddImplementationsDictionary(IEnumerable<MethodImpl> implementations, TypeCreationContext context)
		{
			if (implementations == null)
				return;

			var dictionaryField = context.TypeBuilder.DefineField(METHOD_DICTIONARY, typeof(IDictionary<string, Delegate>), FieldAttributes.Private);

			context.Fields.Add(METHOD_DICTIONARY, dictionaryField);
		}

		private static OpCode GetSizeOpCode(int size)
		{
			return (OpCode)typeof(OpCodes).GetField("Ldc_I4_" + size).GetValue(null);
		}

		private static OpCode GetArgumentOpCode(int arg)
		{
			return (OpCode)typeof(OpCodes).GetField("Ldarg_" + arg).GetValue(null);
		}

		private static Type GetBoxedType(Type unboxedType)
		{
			if (unboxedType == typeof(int))
				return typeof(System.Int32);
			else if (unboxedType == typeof(long))
				return typeof(System.Int64);

			return null;
		}
	}
}
