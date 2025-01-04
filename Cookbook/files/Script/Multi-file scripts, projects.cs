/// A <help editor/Class files, projects>script project<> folder can contain one script and multiple class files that contain classes, structs, etc used in the script. Also you can place resource files there.
/// 
/// Script example:

Class1.Function1("example");

/// Class file example:

class Class1 {
	public static void Function1(string s) {
		print.it(s);
	}
}

/// Also in a script project you can split the <+recipe>script class<> into multiple files: one script and several <google C# partial class>partial class<> files.

/// Script example (note the <.k>partial<>):

partial class Program {
	static void Main(string[] a) => new Program(a);
	Program(string[] args) {
		
		Function2("example");
		
	}
}

/// Partial class file example:

partial class Program {
	void Function2(string s) {
		print.it(s);
	}
}


/// See also recipe <+recipe>Shared classes<>.
