using System;

namespace Emitter
{
	class Program
	{
		static void Main(string[] args)
		{
			Func<int, int> doBarBody = i => i * 2;
			Func<string, int, long, int> doFoobarBody = (s, i, l) => Int32.Parse(s) * i;

			Action doFooBody = () => Console.WriteLine("Woohoo!");
			Action<int, int> doFooAgainBody = (i, j) => Console.WriteLine("This is a test impl");

			var doFooBodyImpl = new MethodImpl { MethodName = "DoFooAgain", Implementation = doFooAgainBody };
			var doFooBodyImple = new MethodImpl { MethodName = "DoFoo", Implementation = doFooBody };
			
			var obj = ILEmitter.New<TestInterface>(new [] { doFooBodyImpl, doFooBodyImple });

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
