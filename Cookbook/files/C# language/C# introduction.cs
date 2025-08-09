/// <link https://learn.microsoft.com/en-us/dotnet/csharp/>C#<> is used to create script code in this program. It is one of the <google programming language popularity>most popular<> programming languages.
/// 
/// You don't need to learn C# to start creating automation scripts. Use the input recorder and other tools in the <b>Code<> menu. But with some C# knowledge you can do much more.
/// 
/// This script displays string <.c>"example"<> in the program's <b>Output<> panel. It calls function <.x>it<> of class <.x>print<>.

print.it("example");

/// This script contains 2 statements. The <.c>//green text<> is comments.

var s = "Some text."; //create variable s
dialog.show("Example", s); //show message box

/// Example with statements <.k>if<>, <.k>return<> (exit) and operator <.k>!<> (NOT).

if (!dialog.showOkCancel("Example", "Continue?")) {
	print.it("Cancel");
	return;
}
print.it("OK, let's continue.");

/// Use the <.k>for<> statement to execute code more than once.

for (int i = 0; i < 3; i++) {
	print.it(i);
}

/// Another way to execute code more than once - user-defined functions.

//call function Example 2 times
Example("one", 1);
Example("two",2);

//this is the function
void Example(string s, int i) {
	print.it(s.Upper() + " " + i);
}

/// In the above examples you also can see:
/// - The blue words are <google>C# keywords<>.
/// - Other words are <google C# identifiers>identifiers<> (names of types, functions, variables, etc).
/// - Keywords and identifiers are case-sensitive.
/// - Every statement ends with <.c>;<> (semicolon). Unless it starts a block of code enclosed in <.c>{ }<>.
/// - Function arguments are enclosed in <.c>( )<> and separated with <.c>,<> (comma).
/// - Blocks of code are enclosed in <.c>{ }<>.
/// 
/// C# does not care about the type and amount of whitespace (spaces, tabs, newlines) between statements, arguments, etc. Example:

Example("one",1);Example("two",
							2);
