/// The first 6 statements in this script call <google C# functions>functions<>.

dialog.showInfo("example"); //call function (method) showInfo which is defined in type dialog. Pass 1 argument.
var w = wnd.find(1, "*- Notepad"); //call function (method) find which is defined in type wnd. Pass 2 arguments and assign its return value to variable w.
w.Activate(); //call function (method) Activate which is defined in type wnd
var s = w.Name; //call function (property-get) Name which is defined in type wnd. Assign the return value to variable s.
var b = new StringBuilder(1000); //create new object of type StringBuilder and call its constructor function with 1 argument. Assign the object to variable b.
b.Capacity = 10000; //call function (property-set) Capacity which is defined in type StringBuilder. Pass a value.
int x = 5; //this statement does not call functions

/// There are several kinds of functions.
/// - method. Can have parameters and return a value. Always called with <q>()<>, even if does not have parameters.
/// - property. Called without <q>()<>, like a field. Actually a property consists of 1 or 2 functions: get and/or set.
/// - indexer. Called with <q>[]<>, like accessing an array element. Does not have a name. Actually an indexer consists of 1 or 2 functions: get and/or set.
/// - constructor. Called with operator <.k>new<> to initialize new object. Its name is the type name.
/// - finalizer. Can't be called directly.
/// - local function. Like method, but defined inside a function or directly in script (most others are defined in classes and structs) and can use local variables of that function/script.
/// - lambda or anonymous function. It's a local callback function defined using a special syntax.
/// - event add/remove functions.
/// - extension methods.

/// A function contains code that performs some particular work/action or/and gets/sets a value.
/// This example shows how to define local and other functions and how to call them.

int localVariable = 1;

//call local functions
LocalFunction(1, "test 1");
LocalFunction(2, "test 2");
int r = Add(2, 2);
print.it(Add(r, 5));

void LocalFunction(int i, string s) {
	//i and s - parameters. They'll receive caller's argument values.
	//void - the function does not return a value.
	
	print.it(i, s);
	print.it(localVariable); //local functions can use local variables of the outer code
}
	
int Add(int a, int b) { //this local function returns a value of type int
	return a + b;
}


//call static functions defined in Class1
Class1.Func1(10, "test");
int k = Class1.Add(3, 4);

//create variable c of type Class1 and call non-static functions
var c = new Class1(1); //calls the constructor
c.Func2(20);
c.Func2(20, s: "optional argument"); //use parameter name
string m = c.Func4(100);
c.FuncParams(0, "text", 100);
if (c.FuncOut(1, out m)) print.it(m);
if (c.FuncOut(2, out var m2)) print.it(m2);
var t = c.FuncTuple(); print.it(t.ok, t.text);
int f = 0; c.FuncRef(ref f); print.it(f);


class Class1 {
	int _i; //private non-static field
	static string _s; //private static field
	
	public Class1(int i) { //constructor function
		_i = i;
	}
	
	public static void Func1(int i, string s) {
		//i and s - parameters. They'll receive caller's argument values.
		//void - the function does not return a value.
		//public - the function can be used outside of the class.
		//static - outside it is called like TypeName.FunctioName(arguments), not like variableName.FunctioName(arguments).
		
		print.it(i);
		print.it(s);
		//print.it(_i); //error, _i isn't static
		print.it(_s);
	}
	
	public static void Func1(int i) {
		//Methods, indexers and constructors can have several versions (overloads) with the same name but different parameters.
		
		print.it(i);
	}
	
	public static int Add(int a, int b) { //this function returns a value of type int
		return a + b;
	}
	
	public void Func2(int i, bool b = false, string s = null, POINT p = default) {
		//This function isn't static. It can use non-static fields. Outside it is called like variableName.FunctioName(arguments), not like TypeName.FunctioName(arguments).
		//Parameters b, s and p have a default value. Callers can omit arguments that have default values, they are optional.
		
		print.it(i, b, s, p, _i, _s, _Func3(i));
		
		//The class can call its static functions without the type name.
		Func1(7);
		
		//The this keyword represents the current class instance. Non-static functions and fields can be accessed with or without it.
		this._i = this._Func3(10);
	}
	
	int _Func3(int i) {
		//This function is private. It can't be used outside of the class.
		
		return i * 2;
	}
	
	public string Func4(int n) => "test " + n; //same as { return "test " + n; }
	
	public void FuncParams(int k, params object[] a) {
		//This function takes variable number of arguments.
		
		foreach (var v in a) print.it(v);
	}
	
	public bool FuncOut(int i, out string s) {
		//This function uses an out parameter to return more than one value.
		
		if (i < 0) { s = null; return false; }
		s = "TEXT";
		return true;
	}
	
	public (bool ok, string text) FuncTuple() {
		//This function uses a tuple to return more than one value.
		
		if (!dialog.showInput(out string s)) return default; //calls a function with an out parameter
		return (true, s);
	}
	
	public void FuncRef(ref int r) {
		//This function uses a ref parameter to access and modify a caller's variable.
		
		if (r == 0) r = 100;
	}
	
	//Sorry, no examples of properties and other kinds of functions. Look on the internet.
	//To add a property you can use propSnippet (inside a class type prop...). Also there is methodSnippet.
}

/// Extension methods (functions) can be attached to existing types without modifying the type. Define them in a static class.

public static class ExtensionMethods {
	//Add an extension method to the string type.
	//Then it can be called like this: int r = stringVariable.ExtensionMethod(10);
	public static int ExtensionMethod(this string t, int add) {
		return t.Length + add;
	}
}

/// Generic methods have names like <b><_>Name<T></_><>. When using the method, replace <b>T<> with a type name, like <b><_>Name<string></_><>.
