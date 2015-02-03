using System;
using System.Collections.Generic;

namespace Emitter
{
	/// <summary>
	/// A test class used to figure out the correct IL to emit via ILDASM
	/// </summary>
	public class TestImplementor : TestInterface
	{
		public string Foo { get; set; }

		private IDictionary<string, Delegate> methodImplementations = new Dictionary<string, Delegate>();

		public TestImplementor()
		{
			Func<int, int> impl = i => i * 2;

			methodImplementations.Add("DoBar", impl);
		}

		public int Bar
		{
			get { return 1; }
		}

		public void DoFoo()
		{
			Delegate del = methodImplementations["DoFoo"];

			del.DynamicInvoke(null);
		}

		public void DoFooAgain(int i, int j)
		{
			Delegate del = methodImplementations["DoFooAgain"];

			object[] parms = new object[] { i, j };

			del.DynamicInvoke(parms);
		}

		public int DoBar(int bar)
		{
			return bar * 2;
		}

		public int DoFoobar(string sVersion, int nVersion, long l)
		{
			Delegate del = methodImplementations["DoFoobar"];

			object[] parms = new object[] { sVersion, nVersion };

			return (int)del.DynamicInvoke(parms);
		}

		public int DoFoobar2(int foobar)
		{
			Delegate del = methodImplementations["DoBar"];

			object[] parms = new object[] { foobar };

			return (int)del.DynamicInvoke(parms);
		}
	}
}
