/// In scripts we use <google C# types>types<> to create variables and to call functions.

string s = "text"; //variable s of type string
int k = 5; //variable k of type int
dialog.showInfo(s, k.ToString()); //call function showInfo which is defined in type dialog. Also call function ToString which is defined in type int.

/// <.k>int<>, <.k>string<> and some other types are built into the C# language; more info in recipe <+recipe>Variables<>. Many other types are defined in .NET and other libraries, and <b>dialog<> is an example. More examples:

DateTime dt = DateTime.Now; //variable dt of type DateTime. Also call function Now which is defined in type DateTime.
RECT r = new RECT(1, 2, 3, 4); //variable r of type RECT. Also call a constructor function defined in type RECT.
print.it(dt, r.left); //call function it which is defined in type print. Pass dt and r field left which is defined in type RECT.

/// There are 5 kinds of types:
/// - <google C# class>class<>. Also known as <i>reference type<>. Can contain data fields (variables, constants), functions (methods, properties, etc), events and inner types.
/// - <google C# struct>struct<>. Also known as <i>value type<>. Same as class, but variables are stored differently: the value is directly in the variable. A class variable is a reference (<i>pointer<>) to its value that is stored separately, and the value isn't copied when copying the variable.
/// - <google C# enum>enum<>. Defines several integer constants, and nothing else.
/// - <google C# delegate>delegate<>. Defines a function type (parameters, etc).
/// - <google C# interface>interface<>. Defines multiple function types.

/// You can define new types, with functions etc inside. Example in recipe <+recipe>Functions<>.

/// Also C# supports arrays, <google C# generic types>generic types<>, <google C# nullable value types>nullable value types<>, <google C# value tuple types>tuples<>, <google C# anonymous types>anonymous types<> and <google C# unsafe pointers>pointers<>.
///
/// Generic types have names like <b><_>List<T></_><>. They can be used in several ways:
/// - Replace <b>T<> with a type name, like <b><_>List<string></_><>. Examples in recipe <+recipe Collections - array>collections<>.
/// - If a parameter is of type <b>T<>, can be used argument of any supported type.
/// - If an <.k>out<> parameter is of type <b>T<>, argument code can be like <.c>out string s<>.
///
/// If a value-type variable is declared like <.c>int? i<>, you can assign it <.k>null<>, which could mean "no value". Often used for optional parameters.

void Func1(bool? b = null) {
	if (b == null) print.it("null");
	else if (b == true) print.it("true");
	else print.it(b.Value);
}

/// Variables of <_>reference</_> types always can have value <.k>null<>. If a function parameter or return type is like <.c>string?<>, it is just a hint that the function supports <.k>null<>.
///
/// Tuples contain several fields (variables) of possibly different types. Often used to returns multiple values from a function, like in the <+recipe>Functions<> recipe.

(int i, string s) t = (10, "text"); //create a tuple variable t
print.it(t.i, t.s);
