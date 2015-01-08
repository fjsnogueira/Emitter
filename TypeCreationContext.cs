using System.Collections.Generic;
using System.Reflection.Emit;

namespace Emitter
{
	public class TypeCreationContext
	{
		public string TypeName { get; private set; }

		public TypeBuilder TypeBuilder { get; private set; }

		public IDictionary<string, FieldBuilder> Fields { get; private set; }
		public IDictionary<string, PropertyBuilder> Properties { get; private set; }
		public IDictionary<string, MethodBuilder> Methods { get; private set; }

		public TypeCreationContext(TypeBuilder typeBuilder)
		{
			this.TypeBuilder = typeBuilder;
			this.TypeName = typeBuilder.Name;

			Fields = new Dictionary<string, FieldBuilder>();
			Properties = new Dictionary<string, PropertyBuilder>();
			Methods = new Dictionary<string, MethodBuilder>();
		}
	}
}
