
namespace Emitter
{
	public interface TestInterface
	{
		string Foo { get; set; }
		int Bar { get; }

		void DoFoo();
		void DoFooAgain(int i, int j);
		int DoBar(int bar);
		int DoFoobar(string sVersion, int nVersion, long justALong);
	}
}
