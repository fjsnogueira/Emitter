using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Emitter
{
	public class TypeCreationContext
	{
		public string TypeName { get; private set; }
		public Type Type { get; private set; }

		internal IDictionary<string, FieldBuilder> Fields { get; private set; }
		internal IDictionary<string, PropertyBuilder> Properties { get; private set; }
		internal IDictionary<string, MethodBuilder> Methods { get; private set; }

		internal TypeBuilder TypeBuilder { get; private set; }

		private IDictionary<string, Delegate> methodImplementations;		

		private TypeCreationContext(Type type)
		{
			this.Type = type;
			this.TypeName = type.Name;

			Fields = new Dictionary<string, FieldBuilder>();
			Properties = new Dictionary<string, PropertyBuilder>();
			Methods = new Dictionary<string, MethodBuilder>();

			methodImplementations = new Dictionary<string, Delegate>();
		}

		#region Method Implementation Handlers

		public void AddImplementation(string methodName, Action implementation)
		{
			ValidateDelegateCall(methodName, implementation);

			methodImplementations.Add(methodName, implementation);
		}

		public void AddImplementation<T, R>(string methodName, Func<T, R> implementation)
		{
			ValidateDelegateCall(methodName, implementation);

			methodImplementations.Add(methodName, implementation);
		}

		public void AddImplementation<T1, T2, R>(string methodName, Func<T1, T2, R> implementation)
		{
			ValidateDelegateCall(methodName, implementation);

			methodImplementations.Add(methodName, implementation);
		}

		public void AddImplementation<T1, T2, T3, R>(string methodName, Func<T1, T2, T3, R> implementation)
		{
			ValidateDelegateCall(methodName, implementation);

			methodImplementations.Add(methodName, implementation);
		}

		private void ValidateDelegateCall(string methodName, Delegate implementation)
		{
			var methodToInvoke = Type.GetMethod(methodName);

			if (methodToInvoke == null)
				throw new ArgumentException(string.Format("Context type {0} does not contain method {1}", TypeName, methodName));

			var delType = implementation.GetType();

			var publicMethod = delType.GetMethod("Invoke");

			if (publicMethod == null)
				throw new ArgumentException("Supplied implementation is not a proper delegate type.");

			if (methodToInvoke.ReturnType != publicMethod.ReturnType)
				throw new ArgumentException(string.Format("Given delegate does not return the proper type: {0}", methodToInvoke.ReturnType.Name));

			ValidateParameters(methodToInvoke.GetParameters(), publicMethod.GetParameters());
		}

		private void ValidateParameters(ParameterInfo[] typeMethodParams, ParameterInfo[] suppliedMethodParams)
		{
			int typeNumParams = typeMethodParams != null ? typeMethodParams.Length : 0;
			int suppliedNumParams = suppliedMethodParams != null ? suppliedMethodParams.Length : 0;

			if (typeNumParams != suppliedNumParams)
				throw new ArgumentException("Given delegate does not have the correct number of parameters.");

			for (int i = 0; i < typeNumParams; i++)
			{
				var tmType = typeMethodParams[i].ParameterType;
				var smType = suppliedMethodParams[i].ParameterType;

				if (tmType != smType)
					throw new ArgumentException(string.Format("Given delegate does not match parameter {0}. Expecting {1} have {2}", i, tmType.Name, smType.Name));
			}
		}

		#endregion

		#region Public Interface

		public static T New<T>() where T : class
		{
			return ImplementInterface<T>().CreateInstance() as T;
		}

		public static TypeCreationContext ImplementInterface<T>() where T : class
		{
			return ImplementInterface(typeof(T));
		}

		public static TypeCreationContext ImplementInterface(Type type)
		{
			if (!type.IsInterface)
				throw new ArgumentException("Given type is not an interface");

			return new TypeCreationContext(type);
		}

		public object CreateInstance()
		{
			var builders = ILHelper.GetAssemblyAndModuleBuilders();

			TypeBuilder = ILHelper.GetTypeBuilder(this.Type, builders.Item2);

			// Now lets add the interface implementation
			TypeBuilder.AddInterfaceImplementation(Type);

			bool hasImplementations = methodImplementations.Any();

			if (hasImplementations)
				ILHelper.AddImplementationsDictionary(this);

			ILHelper.AddDefaultConstructor(this, hasImplementations);

			var properties = Type.GetProperties();
			foreach (var pi in properties)
				ILHelper.AddProperty(pi, this);

			// Finally lets add each method in the interface to our new type.
			// We have to explicitly exclude the get and setter methods for the properties within the interface
			var methods = Type.GetMethods().Where(mi => !mi.Name.StartsWith("get_") && !mi.Name.StartsWith("set_"));
			foreach (var mi in methods)
				ILHelper.AddMethod(mi, this, hasImplementations && methodImplementations.ContainsKey(mi.Name));

			try
			{
				var actualType = TypeBuilder.CreateType();

#if DEBUG
				builders.Item1.Save("TestAsm.dll");
#endif
				var instance = Activator.CreateInstance(actualType);

				if (hasImplementations)
				{
					var dictField = actualType.GetField(ILHelper.METHOD_DICTIONARY, BindingFlags.NonPublic | BindingFlags.Instance);

					var dict = (IDictionary<string, Delegate>)dictField.GetValue(instance);

					foreach (var impl in methodImplementations)
						dict.Add(impl.Key, impl.Value);
				}

				return instance;
			}
			catch (Exception e)
			{
				Console.WriteLine(e.Message);
			}

			return null;
		}

		#endregion
	}
}
