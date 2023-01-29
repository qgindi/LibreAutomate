# Run code when script ends
Event <a href='/api/Au.process.thisProcessExit.html'>process.thisProcessExit</a> can be used to execute some code when current script ends, either normally or on unhandles exception.

```csharp
process.thisProcessExit += e => {
	if (e != null) print.it("FAILED. " + e.ToStringWithoutStack());
	else print.it("DONE");
};

//example script code
script.setup(exception: 0); //disables printing the standard exception message, because this script code prints it itself
if (keys.isCapsLock) throw new InvalidOperationException("CapsLock");
print.it("script");
```

To execute some code when any script part ends, use <span style='color:#00f;font-weight:bold'>try</span>/<span style='color:#00f;font-weight:bold'>finally</span>.

```csharp
try {
	if (keys.isScrollLock) throw new InvalidOperationException("ScrollLock");
	print.it("code");
}
finally { print.it("finally"); }
```

