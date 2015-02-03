Emitter
=======

A small project that allows you to implement any interface and selectively add method implementations. Useful for mocking objects for testing.

#Simple Case

If you just want a raw object that implements the bare bones of the interface add the following line of code

	var obj = TypeCreationContext.New<TestInterface>();

BAM! You have a working instance of TestInterface

#Let's Get Complex

If you want to provide an implementation of any method on your interface we need to create the type in a different manner.
	
	var context = TypeCreationContext.ImplementInterface<TestInterface>();

Doing so returns a context object that allows you to work with your type before you create it. Let's add a Hello World to a void method.

	context.AddImplementation("DoFoo", () => Console.WriteLine("Hello World"));

If the interface and implementation signatures do not line up you will receive an ArgumentException.
Now we create the type and call the method

	var obj = context.CreateType() as TestInterface;
	obj.DoFoo(); // Writes out "Hello World" to the console

#Desired Features

Emitter is still a small working prototype, but I'd like to be able to add a much cleaner way to add method implementations instead of having
to add method signatures for all permutations of Func<T> and Action/Action<T>. I'd also like to add the ability to add your own properties, or methods
to the type you're constructing.
