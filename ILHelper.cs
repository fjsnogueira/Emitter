using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;

namespace Emitter
{
	public static class ILHelper
	{
		#region Constants & Readonly's

		internal const string METHOD_DICTIONARY = "_methodImplementations";

		private static readonly Type OBJ_TYPE = typeof(System.Object);
		private static readonly Type OBJ_ARR_TYPE = typeof(object[]);
		private static readonly Type DELEGATE_TYPE = typeof(Delegate);
		private static readonly Type DEL_DICT_TYPE = typeof(IDictionary<string, Delegate>);

		#endregion

		internal static Tuple<AssemblyBuilder, ModuleBuilder> GetAssemblyAndModuleBuilders(string assemblyName = "DynamicAssembly")
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

		internal static TypeBuilder GetTypeBuilder(Type type, ModuleBuilder mBuilder)
		{
			return mBuilder.DefineType(type.Name + "Impl", TypeAttributes.Public | TypeAttributes.Class);
		}

		internal static void AddDefaultConstructor(TypeCreationContext context, bool hasImplementations)
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

		internal static void AddProperty(PropertyInfo pi, TypeCreationContext context)
		{
			var backingFieldName = "_" + pi.Name;

			var fieldBuilder = context.TypeBuilder.DefineField(backingFieldName, pi.PropertyType, FieldAttributes.Private);

			context.Fields.Add(backingFieldName, fieldBuilder);

			var propertyBuilder = context.TypeBuilder.DefineProperty(pi.Name, pi.Attributes, pi.PropertyType, null);

			context.Properties.Add(pi.Name, propertyBuilder);

			var methodAttributes = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig;

			var getter = pi.GetGetMethod();
			if (getter != null)
			{				
				if (getter.IsFinal)
					methodAttributes |= MethodAttributes.Final;

				var gmMethodBuilder = context.TypeBuilder.DefineMethod("get_" + pi.Name, methodAttributes | MethodAttributes.Virtual, pi.PropertyType, Type.EmptyTypes);

				context.Methods.Add(gmMethodBuilder.Name, gmMethodBuilder);

				var gmGenerator = gmMethodBuilder.GetILGenerator();

				gmGenerator.Emit(OpCodes.Ldarg_0);
				gmGenerator.Emit(OpCodes.Ldfld, fieldBuilder);
				gmGenerator.Emit(OpCodes.Ret);

				propertyBuilder.SetGetMethod(gmMethodBuilder);

				context.TypeBuilder.DefineMethodOverride(gmMethodBuilder, getter);
			}

			var setter = pi.GetSetMethod();
			if (setter != null)
			{
				if (setter.IsVirtual)
					methodAttributes |= MethodAttributes.Virtual;

				var smMethodBuilder = context.TypeBuilder.DefineMethod("set_" + pi.Name, methodAttributes, null, new Type[] { pi.PropertyType });

				context.Methods.Add(smMethodBuilder.Name, smMethodBuilder);

				var smGenerator = smMethodBuilder.GetILGenerator();

				smGenerator.Emit(OpCodes.Ldarg_0); // This is equivalent to "this"
				smGenerator.Emit(OpCodes.Ldarg_1); // This is the typical "value" parameter
				smGenerator.Emit(OpCodes.Stfld, fieldBuilder);
				smGenerator.Emit(OpCodes.Ret);

				propertyBuilder.SetSetMethod(smMethodBuilder);

				context.TypeBuilder.DefineMethodOverride(smMethodBuilder, setter);
			}
		}

		internal static void AddMethod(MethodInfo mi, TypeCreationContext context, bool hasImplementation)
		{
			// Get all the parameters to the method
			var parameterTypes = mi.GetParameters().Select(pi => pi.ParameterType).ToArray();

			var mBuilder = context.TypeBuilder.DefineMethod(mi.Name, MethodAttributes.Public | MethodAttributes.Virtual, mi.ReturnType, parameterTypes);

			context.Methods.Add(mi.Name, mBuilder);

			var mGenerator = mBuilder.GetILGenerator();

			if (hasImplementation)
			{
				if (mi.ReturnType == typeof(void))
				{
					AddVoidDelegateInvocation(mi, context, parameterTypes, mGenerator);
				}
				else
				{
					AddDelegateInvocation(mi, context, parameterTypes, mGenerator);
				}
			}
			else if (mi.ReturnType != typeof(void))
			{
				var localBuilder = mGenerator.DeclareLocal(mi.ReturnType);

				mGenerator.Emit(OpCodes.Ldloc, localBuilder);
			}

			mGenerator.Emit(OpCodes.Ret);
		}

		private static void AddVoidDelegateInvocation(MethodInfo mi, TypeCreationContext context, Type[] parameterTypes, ILGenerator mGenerator)
		{
			mGenerator.DeclareLocal(DELEGATE_TYPE);

			bool hasParameters = parameterTypes != null && parameterTypes.Length > 0;

			if (hasParameters)
			{
				mGenerator.DeclareLocal(OBJ_ARR_TYPE);
				mGenerator.DeclareLocal(OBJ_ARR_TYPE);
			}

			mGenerator.Emit(OpCodes.Nop);
			mGenerator.Emit(OpCodes.Ldarg_0);
			mGenerator.Emit(OpCodes.Ldfld, context.Fields[METHOD_DICTIONARY]);
			mGenerator.Emit(OpCodes.Ldstr, mi.Name);

			var dictGetMethod = DEL_DICT_TYPE.GetMethod("get_Item", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

			mGenerator.Emit(OpCodes.Callvirt, dictGetMethod);
			mGenerator.Emit(OpCodes.Stloc_0);

			// Now if we need to deal with parameters lets do this now
			if (hasParameters)
			{
				OpCode arraySize = GetSizeOpCode(parameterTypes.Length);

				mGenerator.Emit(arraySize);

				mGenerator.Emit(OpCodes.Newarr, OBJ_TYPE);
				mGenerator.Emit(OpCodes.Stloc_2);
				mGenerator.Emit(OpCodes.Ldloc_2);

				for (int i = 0; i < parameterTypes.Length; i++)
				{
					mGenerator.Emit(GetSizeOpCode(i));
					mGenerator.Emit(GetArgumentOpCode(i + 1));

					if (parameterTypes[i].IsValueType)
						mGenerator.Emit(OpCodes.Box, GetBoxedType(parameterTypes[i]));

					mGenerator.Emit(OpCodes.Stelem_Ref);
					mGenerator.Emit(OpCodes.Ldloc_2);
				}

				mGenerator.Emit(OpCodes.Stloc_1);
			}

			// Generate the IL to call the delegate properly
			mGenerator.Emit(OpCodes.Ldloc_0);
			mGenerator.Emit(hasParameters ? OpCodes.Ldloc_1 : OpCodes.Ldnull);
			mGenerator.Emit(OpCodes.Callvirt, DELEGATE_TYPE.GetMethod("DynamicInvoke"));

			// Return from the method properly
			mGenerator.Emit(OpCodes.Pop);
			mGenerator.Emit(OpCodes.Ret);
		}

		private static void AddDelegateInvocation(MethodInfo mi, TypeCreationContext context, Type[] parameterTypes, ILGenerator mGenerator)
		{
			mGenerator.DeclareLocal(DELEGATE_TYPE); // reference to delegate to call
			mGenerator.DeclareLocal(OBJ_ARR_TYPE); // Delegate parameter array
			mGenerator.DeclareLocal(mi.ReturnType); // Return type
			mGenerator.DeclareLocal(OBJ_ARR_TYPE); // ??? I think its the parameter array that goes on the stack to call the delegate

			// Generate the IL to get the delegate out of the private dictionary
			mGenerator.Emit(OpCodes.Nop);
			mGenerator.Emit(OpCodes.Ldarg_0);
			mGenerator.Emit(OpCodes.Ldfld, context.Fields[METHOD_DICTIONARY]);
			mGenerator.Emit(OpCodes.Ldstr, mi.Name);

			var dictGetMethod = DEL_DICT_TYPE.GetMethod("get_Item", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

			mGenerator.Emit(OpCodes.Callvirt, dictGetMethod);
			mGenerator.Emit(OpCodes.Stloc_0);

			// Create the parameter array
			OpCode arraySize = GetSizeOpCode(parameterTypes.Length);

			mGenerator.Emit(arraySize);

			mGenerator.Emit(OpCodes.Newarr, OBJ_TYPE);
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
			mGenerator.Emit(OpCodes.Callvirt, DELEGATE_TYPE.GetMethod("DynamicInvoke"));

			mGenerator.Emit(OpCodes.Unbox_Any, mi.ReturnType);
			mGenerator.Emit(OpCodes.Stloc_2);

			// Load the value for return purposes
			mGenerator.Emit(OpCodes.Nop);
			mGenerator.Emit(OpCodes.Ldloc_2);
		}

		internal static void AddImplementationsDictionary(TypeCreationContext context)
		{
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
