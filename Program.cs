using System;

namespace Emitter
{
	class Program
	{
		static void Main(string[] args)
		{
			Func<int, int> doBarBody = i => i * 2;
			Func<string, int, long, int> doFoobarBody = (s, i, l) => Int32.Parse(s) * i;
			Action doFooBody = () => Console.WriteLine("This is a test impl");			

			var doFooBodyImpl = new MethodImpl { MethodName = "DoFoo", Implementation = doFooBody };
			var doBarImpl = new MethodImpl { MethodName = "DoBar", Implementation = doBarBody };
			var doFoobarImpl = new MethodImpl { MethodName = "DoFoobar", Implementation = doFoobarBody };

			var obj = ILEmitter.New<TestInterface>(new [] { doFooBodyImpl, doBarImpl, doFoobarImpl });

			if (obj != null)
			{
				obj.Foo = "Foo";

				Console.WriteLine(obj.Foo);
				Console.WriteLine(obj.Bar);

				obj.DoFoo();

				Console.WriteLine(obj.DoBar(3));
				Console.WriteLine(obj.DoFoobar("5", 3, 1));
			}

			Console.ReadLine();
		}
	}
}
