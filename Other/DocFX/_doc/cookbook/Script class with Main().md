# Script class with Main()
The default script syntax is known as <a href='https://www.google.com/search?q=C%23+top-level+statements'>C# top-level statements</a>. You can instead use the classic C# syntax with class Program and function Main. To insert code can be used menu Edit -> Document -> Add class Program.

```csharp
class Program {
	static void Main(string[] a) => new Program(a);
	Program(string[] args) {
		script.setup(trayIcon: true, sleepExit: true);
		
		print.it("script code");
		_field1 = 1;
		Function1();
		
	}
	
	int _field1; //this is a field, not a local variable
	void Function1() { } //this is a class function, not a local function
	
}
```

Why to use such code? Because the default syntax has some limitations. The classic syntax allows to:
- Add fields (class-level variables) and class-level functions to the main class.
- Add class/method-level modifiers and attributes, for example <span style='color:#00f;font-weight:bold'>partial</span>, <span style='color:#00f;font-weight:bold'>unsafe</span>.
- Split the main class into partial files. See recipe <a href='Multi-file scripts, projects.md'>Multi-file scripts</a>.
- Avoid <a href='https://www.google.com/search?q=intellisense'>intellisense</a> failures that sometimes happen when typing code not in a { } block.

You can remove code `=> new Program(a); Program(string[] args)`. It just makes writing code a bit easier, because then don't need to use static functions and static fields. It creates a class instance, and the script code is executed in the constructor.
