/// Event <see cref="process.thisProcessExit"/> can be used to execute some code when current script ends, either normally or on unhandles exception.

process.thisProcessExit += e => {
	if (e != null) print.it("FAILED. " + e.ToStringWithoutStack());
	else print.it("DONE");
};

//example script code
script.setup(exception: 0); //disables printing the standard exception message, because this script code prints it itself
if (keys.isCapsLock) throw new InvalidOperationException("CapsLock");
print.it("script");

/// To execute some code when any script part ends, use <.k>try<>/<.k>finally<>.

try {
	if (keys.isScrollLock) throw new InvalidOperationException("ScrollLock");
	print.it("code");
}
finally { print.it("finally"); }
