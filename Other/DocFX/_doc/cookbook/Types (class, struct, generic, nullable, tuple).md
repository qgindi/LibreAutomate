# Types (class, struct, generic, nullable, tuple)
In scripts we use <a href='https://www.google.com/search?q=C%23+types'>types</a> to create variables and to call functions.

```csharp
string s = "text"; //variable s of type string
int k = 5; //variable k of type int
dialog.showInfo(s, k.ToString()); //call function showInfo which is defined in type dialog. Also call function ToString which is defined in type int.
```

<span style='color:#00f;font-weight:bold'>int</span>, <span style='color:#00f;font-weight:bold'>string</span> and some other types are built into the C# language; more info in recipe <a href='Variables, fields, built-in types.md'>Variables</a>. Many other types are defined in .NET and other libraries, and <b>dialog</b> is an example. More examples:

```csharp
DateTime dt = DateTime.Now; //variable dt of type DateTime. Also call function Now which is defined in type DateTime.
RECT r = new RECT(1, 2, 3, 4); //variable r of type RECT. Also call a constructor function defined in type RECT.
print.it(dt, r.left); //call function it which is defined in type print. Pass dt and r field left which is defined in type RECT.
```

There are 5 kinds of types:
- <a href='https://www.google.com/search?q=C%23+class'>class</a>. Also known as <i>reference type</i>. Can contain data fields (variables, constants), functions (methods, properties, etc), events and inner types.
- <a href='https://www.google.com/search?q=C%23+struct'>struct</a>. Also known as <i>value type</i>. Same as class, but variables are stored differently: the value is directly in the variable. A class variable is a reference (pointer) to its value that is stored separately, and the value isn't copied when copying the variable.
- <a href='https://www.google.com/search?q=C%23+enum'>enum</a>. Defines several integer constants, and nothing else.
- <a href='https://www.google.com/search?q=C%23+delegate'>delegate</a>. Defines a function type (parameters, etc).
- <a href='https://www.google.com/search?q=C%23+interface'>interface</a>. Defines multiple function types.

You can define new types, with functions etc inside. Example in recipe <a href='Functions (methods, properties).md'>Functions</a>.

Also C# supports arrays, <a href='https://www.google.com/search?q=C%23+generic+types'>generic types</a>, <a href='https://www.google.com/search?q=C%23+nullable+value+types'>nullable value types</a>, <a href='https://www.google.com/search?q=C%23+value+tuple+types'>tuples</a>, <a href='https://www.google.com/search?q=C%23+anonymous+types'>anonymous types</a> and <a href='https://www.google.com/search?q=C%23+unsafe+pointers'>pointers</a>.

Generic types have names like <b>List\<T\></b>. They can be used in several ways:
- Replace <b>T</b> with a type name, like `List<string>`. Examples in recipe <a href='Collections - array, List, Stack, Queue.md'>collections</a>.
- If a parameter is of type <b>T</b>, can be used argument of any supported type.
- If an <span style='color:#00f;font-weight:bold'>out</span> parameter is of type <b>T</b>, argument code can be like `out string s`.

If a value-type variable is declared like `int? i`, you can assign it <span style='color:#00f;font-weight:bold'>null</span>, which could mean "no value". Often used for optional parameters.

```csharp
void Func1(bool? b = null) {
	if (b == null) print.it("null");
	else if (b == true) print.it("true");
	else print.it(b.Value);
}
```

Variables of reference types always can have value <span style='color:#00f;font-weight:bold'>null</span>. If a function parameter or return type is like `string?`, it is just a hint that the function supports <span style='color:#00f;font-weight:bold'>null</span>.

Tuples contain several fields (variables) of possibly different types. Often used to returns multiple values from a function, like in the <a href='Functions (methods, properties).md'>Functions</a> recipe.

```csharp
(int i, string s) t = (10, "text"); //create a tuple variable t
print.it(t.i, t.s);
```

