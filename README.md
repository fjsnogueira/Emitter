Emitter
=======

A small project that allows you to implement any interface and selectively add method implementations. Useful for mocking objects for testing.

#Simple Case

If you just want a raw object that implements the bare bones of the interface add the following line of code

	var obj = TypeCreationContext.New<TestInterface>();

BAM! You have a working instance of TestInterface