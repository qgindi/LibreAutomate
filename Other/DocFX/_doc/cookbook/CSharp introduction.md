# C# introduction
The <a href='https://learn.microsoft.com/en-us/dotnet/csharp/'>C# programming language</a> is used to create scripts in this program. It is one of the <a href='https://www.google.com/search?q=programming+language+popularity'>most popular</a> languages.

Don't need to learn C# to start creating automation scripts. Use the input recorder and other tools in the Code menu. But with some C# knowledge you can do much more.

This script displays string "example" in the program's Output panel. It calls function <b>it</b> of class <b>print</b>.

```csharp
print.it("example");
```

This script contains 2 statements. The //text is comments.

```csharp
var s = "Some text."; //create variable s
dialog.show("Example", s); //show message box
```

Example with statements <span style='color:#00f;font-weight:bold'>if</span>, <span style='color:#00f;font-weight:bold'>return</span> (exit) and operator <span style='color:#00f;font-weight:bold'>!</span> (NOT).

```csharp
if (!dialog.showOkCancel("Example", "Continue?")) {
	print.it("Cancel");
	return;
}
print.it("OK, let's continue.");
```

Use the <span style='color:#00f;font-weight:bold'>for</span> statement to execute code more than once.

```csharp
for (int i = 0; i < 3; i++) {
	print.it(i);
}
```

Another way to execute code more than once - user-defined functions.

```csharp
//call function Example 2 times
Example("one", 1);
Example("two",2);

//this is the function
void Example(string s, int i) {
	print.it(s.Upper() + " " + i);
}
```

In the above examples you also can see:
- The blue words are <a href='https://www.google.com/search?q=C%23+keywords'>C# keywords</a>.
- Other words are <a href='https://www.google.com/search?q=C%23+identifiers'>identifiers</a> (names of types, functions, variables, etc).
- Keywords and identifiers are case-sensitive.
- Every statement ends with a semicolon (;). Unless it starts a block of code enclosed in { }.
- Function arguments are enclosed in ( ) and separated with comma (,).
- Blocks of related code are enclosed in {  }.

C# does not care about the type and amount of whitespace (spaces, tabs, newlines) between statements, arguments, etc. Example:

```csharp
Example("one",1);Example("two",
							2);
```

