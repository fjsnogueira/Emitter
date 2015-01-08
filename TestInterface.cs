
namespace Emitter
{
	public interface TestInterface
	{
		string Foo { get; set; }
		int Bar { get; }

		void DoFoo();
		int DoBar(int bar);
		int DoFoobar(string sVersion, int nVersion, long justALong);
	}
}
