using System;

namespace Emitter
{
	class Program
	{
		static void Main(string[] args)
		{
			var context = TypeCreationContext.ImplementInterface<TestInterface>();

			context.AddImplementation<int, int>("DoBar", i => i * 2);
			context.AddImplementation("DoFoo", () => Console.WriteLine("Doing this from a void implementation"));

			var obj = context.CreateInstance() as TestInterface;

			var obj2 = TypeCreationContext.New<TestInterface>();

			if (obj != null)
			{
				obj.Foo = "Foo";

				Console.WriteLine(obj.Foo);
				Console.WriteLine(obj.Bar);

				obj.DoFoo();
				obj.DoFooAgain(1, 2);

				Console.WriteLine(obj.DoBar(3));
				Console.WriteLine(obj.DoFoobar("5", 3, 1));
			}

			Console.ReadLine();
		}
	}
}
