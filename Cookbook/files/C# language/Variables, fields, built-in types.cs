/// <google C# variables>Variables<> are containers for storing numbers, text and other data. A variable has a type, name and value.

int i = 0; //declare (create) variable i of type int, and assign value 0
string s = "text"; //variable s of type string
i = 10; //can change value
print.it(i, s); //display variable values
int j; if (i == 1) j = 1; else j = 2; //can assign value later. Can't use unassigned.
int one = 10, two, three = one, four = i; //several variables in single statement
var k = 0; //var means the type of the assigned value. The same as int k = 0;
bool b1 = keys.isCtrl; //call a function and assign its return value
if (!dialog.showInput(out string s3, s)) return; //declare with the out keyword. The function assigns a value.

/// Names are case-sensitive and can contain letters, digits (except at the start) and <q>_<>.

/// Frequently used <+lang built-in types>C# built-in types<>:
/// <.k>int<> - integer (whole) number like <q>10<> or <q>-10<>. Max about +- 2 billions.
/// <.k>double<> - can hold non-integer numbers like <q>3.14<> and large numbers like <q>1e15<>.
/// <.k>string<> - text. Strings are Unicode UTF-16.
/// <.k>char<> - single character like <q>'A'<>. Unicode UTF-16.
/// <.k>bool<> - <.k>true<> or <.k>false<>.
/// <.k>byte<> - small integer, 0-255. Usually used in binary data arrays.
/// <.k>long<> - 64-bit integer, about 4 billion times larger than int.
/// <.k>object<> - can hold values of almost any type.

/// A variable can hold values only of its type (unless it's <.k>object<> or <.k>dynamic<>).

string s4 = "4";
//s4 = 5; //error, 5 is not string

/// To assign a value of another type, need to convert it to the variable's type if possible.

double d4 = i; //i is int, but it is implicitly converted to double
//int i1 = d4; //error, can't convert implicitly
int i1 = (int)d4; //sometimes can convert explicitly, with the type cast operator ()
string s5 = i1.ToString(); //or with a function
i1 = s5.ToInt();

/// Variables declared in functions or directly in script are <i>local variables<>. Function <i>parameters<> too.
/// Variables declared directly in a class are known as <i>fields<>. <google C# fields>More info<>.

Example v1 = new Example(); //create new instance of class Example and assign to variable v1
//var v1 = new Example(); //the same, but shorter code
//Example v1 = new(); //the same too
v1.f2 = "text"; //fields are accessed through the variable, except inside the class
//v1._f1 = 6; //error, it's private
int m = Example.f4; //static fields are accessed through class name, not through variable

class Example {
	int _f1; //private field; can't be used outside of the class
	public string f2; //public field; can be used anywhere
	int _f3 = 1; //assign a value; else fields have the default value of the type (0, null, false, empty)
	public static int f4; //static field
	[ThreadStatic] static int _f5; //thread-static field. Note: don't assign a value now; assign in each thread.
	
	//also classes can have fields with other kinds of storage and access: const, readonly, protected, etc
	
	//function Test with 2 parameters
	public void Test(int p1, string p2) {
		int k = 10; //local variable
		_f1 = 7;
	}
	
	//examples of variables in inner { } code blocks
	void _Test2() {
		int m = 7;
		if (true) {
			int k = 1;
			print.it(k, m);
		} else {
			int k = 2; //it's another variable k
		}
		//print.it(k); //error, k is unavailable outside of its { } block
		
		for (int i = 0; i < 3; i++) {
			int k = 3; //it's yet another variable k. Actually this code creates/destroys new variable k in each loop (3 times).
		}
		//print.it(i); //error, i is unavailable outside of the for { } block
	}
}

/// Local variables live until the function exits. If declared in inner <q>{ }<> code blocks - until leaving the block. New variables are created each time the function or <q>{ }<> block is executed.
/// Non-static fields live while the class instance variable lives. Each class instance variable has its own fields.
/// Static fields live until the script process exits. All class instance variables share them.
/// Thread-static fields live until the thread exits. Each thread has its own variables. All class instance variables share them.
/// When the script process ends, all variables are gone. Multiple processes don't share variables.
