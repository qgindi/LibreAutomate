/// The default script syntax is known as <google>C# top-level statements<>. You can instead use the classic C# syntax with class <b>Program<> and function <b>Main<>. To insert code can be used menu <b>Code > Simple > Add function Main<>.

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

/// Why to use such code? Because the default syntax has some limitations. The classic syntax allows to:
/// - Add fields (class-level variables) and class-level functions to the main class.
/// - Add class/method-level modifiers and attributes, for example <.k>partial<>, <.k>unsafe<>, <.c>[MTAThread]<>.
/// - Split the main class into partial files. See recipe <+recipe>Multi-file scripts<>.
/// - Avoid <google>intellisense<> failures that sometimes happen when typing code not in a { } block.

/// You can remove code <.c>=> new Program(a); Program(string[] args)<>. It just makes writing code a bit easier, because then don't need to use static functions and static fields. It creates a class instance, and the script code is executed in the constructor.
