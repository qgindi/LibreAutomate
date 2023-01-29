# Multi-file scripts, projects
A <a href='/editor/Class files, projects.html'>script project</a> folder can contain one script and multiple class files that contain classes, structs, etc used in the script. Also you can place resource files there.

Script example:

```csharp
Class1.Function1("example");
```

Class file example:

```csharp
class Class1 {
	public static void Function1(string s) {
		print.it(s);
	}
}
```

Also in a script project you can split the <a href='Script class with Main().md'>script class</a> into multiple files: one script and several <a href='https://www.google.com/search?q=C%23+partial+class'>partial class</a> files. To add a partial class file: menu File -> New -> More -> Partial.cs.

Script example (note the <span style='color:#00f;font-weight:bold'>partial</span>):

```csharp
partial class Program {
	static void Main(string[] a) => new Program(a);
	Program(string[] args) {
		
		Function2("example");
		
	}
}
```

Partial class file example:

```csharp
partial class Program {
	void Function2(string s) {
		print.it(s);
	}
}
```

See also recipe <a href='Shared classes and functions, libraries.md'>Shared classes</a>.
