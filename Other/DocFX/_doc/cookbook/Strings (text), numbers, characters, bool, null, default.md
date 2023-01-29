# Strings (text), numbers, characters, bool, null, default
Strings contain text. More info: <a href='https://www.google.com/search?q=C%23+strings'>C# strings</a>, recipe <a href='Strings and characters.md'>strings and characters</a>.

```csharp
string s1 = "text";
string s2 = ""; //empty string
string s3 = null; //no string. Not the same as "".
print.it("this is a \"string\"\r\n\twith \\ escape sequences");
```

Frequently used escape sequences:
`\"` - `"`
`\\` - `\`
`\t` - tab
`\n` - newline
`\r\n` - Windows newline (2 characters)
`\0` - character code 0

To avoid many escape sequences, use verbatim strings or raw strings.

```csharp
string path = @"C:\folder\file"; //instead of "C:\\folder\\file"
string verbatim1 = @"multiline
string";
string verbatim2 = @"abc ""def"" gh"; //for " use ""

string raw1 = """raw single-line string without any escape sequences""";
string raw2 = """
raw multi-line string
without any escape sequences
""";
string raw3 = """"another """raw""" string """";
```

To easily create strings with variables, can be used <a href='https://www.google.com/search?q=interpolated+strings%2C+C%23+reference'>interpolated strings</a>, operator + and other ways. More info in recipe <a href='String formatting with variables.md'>string formatting</a>.

```csharp
string s4 = $"ab {s1} cd {path} ef";
string s5 = @$"ab {s1}
cd {path} ef";
string s6 = $"""ab {s1} cd""";
string s7 = $$"""ab {{s1}} {cd}""";

string s8 = "ab " + s1 + " cd";
```

Strings consist of characters. Characters also can be used alone.

```csharp
char c1 = 'A';
char c2 = ' '; //space
char[] a1 = { '\t', '\r', '\n', '\"', '\'', '\\', '\0' }; //escape sequences like in strings
```

<a href='https://www.google.com/search?q=integral+numeric+types%2C+C%23+reference'>Integer numbers</a> are whole numbers like 10 but not like 1.5.

```csharp
int i1 = 10, i2 = -20;
int i3 = 2000000000; //or 2_000_000_000
int i4 = 0x10; //hexadecimal 16
int i5 = unchecked((int)0xFFFFFFFF); //sorry, simple values 0x80000000...0xFFFFFFFF are considered too big for int
uint u1 = 0xFFFFFFFF; //unsigned int
var u2 = 5u; //uint (suffix u)
var k1 = 5L; //long (suffix L)
long k2 = 9_223_372_036_854_775_807; //max long integer value
print.it(int.MaxValue, int.MinValue); //for max/min values it's better to use these constants
```

<a href='https://www.google.com/search?q=floating+point+numeric+types%2C+C%23+reference'>Floating-point numbers</a> can be non-integer and can hold larger values.

```csharp
double d1 = 3.14;
double d2 = 0.5; //or .5
double d4 = -2e6; //-2000000
double d5 = 3.5e-3; //0.0035
var d6 = 5d; //double because of prefix d
print.it(double.MaxValue, Math.PI); //use constants
float f1 = 0.5f; //float is smaller (32-bit instead of 64-bit) and less precise
decimal g1 = 10.1234m; //the largest built-in type
```

Boolean values can be either <span style='color:#00f;font-weight:bold'>true</span> or <span style='color:#00f;font-weight:bold'>false</span>.

```csharp
bool b1 = true;
bool b2 = false;
bool? b3 = null; //nullable bool can be true, false or null
```

To specify default value of that type, use keyword <span style='color:#00f;font-weight:bold'>default</span>. For reference types, nullables and pointers also can be used <span style='color:#00f;font-weight:bold'>null</span>.

```csharp
string sn = null; //string is a reference type (class)
DateTime dt = default; //value type (struct)
bool? bn = null; //nullable value type
```

