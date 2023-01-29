# Constants, enum
Constants are like variables with a value that can't be changed. Also they can be used where variables can't, for example with <span style='color:#00f;font-weight:bold'>case</span> and as default parameter values. <a href='https://www.google.com/search?q=C%23+constants'>More info</a>.

```csharp
print.it(Math.PI); //PI is a constant, and it is declared in class Math
```

Constants can be declared with the <span style='color:#00f;font-weight:bold'>const</span> keyword. The type can be any C# built-in numeric type or string or an enum type.

```csharp
const double speedOfLight = 299792458; //constant number
const string c_sun = "Sunday", c_mon = "Monday"; //constant strings
```

Usually constants are declared as class fields.

```csharp
print.it(Class1.IntConstant, Class1.StringConstant);

class Class1 {
	public const int IntConstant = 100;
	public const string StringConstant = "example";
	const double c_privateConstant = 2.5;
	
	void Test() {
		print.it(IntConstant, c_privateConstant);
	}
}
```

The <b><a href='https://www.google.com/search?q=enum+enumeration+types%2C+C%23+reference'>enum</a></b> keyword declares multiple integer constants and gives them a type. Physically they are like <span style='color:#00f;font-weight:bold'>int</span> by default.

```csharp
enum MyColors {
	Black, //0
	White, //1 (previous + 1)
	Blue, //2
	Green = 10,
	Red, //11
}

[Flags] //recommended for enums that are used as flags
enum MyFlags {
	Bold = 1,
	Italic = 2,
	Underline = 4,
}

enum Small : byte { One, Two, Three } //byte-sized enum
```

Examples of how to use enums.

```csharp
MyColors colors = MyColors.Green;
if (colors == MyColors.Green) print.it("Green");

MyFlags flags = MyFlags.Bold | MyFlags.Italic;
if (flags.Has(MyFlags.Italic)) print.it("has flag Italic");

int i = (int)flags; //convert to int
flags = (MyFlags)i; //convert from int

const MyColors violet = (MyColors)100; //this is how to declare additional constants of an enum type when you can't add them to the enum
```

