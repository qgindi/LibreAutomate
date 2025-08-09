/// Strings contain text. More info: <google>C# strings<>, recipe <+recipe>strings and characters<>.

string s1 = "text";
string s2 = ""; //empty string
string s3 = null; //no string. Not the same as "".
print.it("this is a \"string\"\r\n\twith \\ escape sequences");

/// Frequently used escape sequences:
/// <.c>\"<> - <.c>"<>
/// <.c>\\<> - <.c>\<>
/// <.c>\t<> - tab
/// <.c>\n<> - newline
/// <.c>\r\n<> - Windows newline (2 characters)
/// <.c>\0<> - character code 0

/// To avoid writing many escape sequences, use verbatim strings or raw strings.

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

/// To easily create strings with variables, can be used <+lang interpolated strings>interpolated strings<>, operator <.k>+<> and other ways. More info in recipe <+recipe>string formatting<>.

string s4 = $"ab {s1} cd {path} ef";
string s5 = @$"ab {s1}
cd {path} ef";
string s6 = $"""ab {s1} cd""";
string s7 = $$"""ab {{s1}} {cd}""";

string s8 = "ab " + s1 + " cd";

/// Strings consist of characters. Characters also can be used alone.

char c1 = 'A';
char c2 = ' '; //space
char[] a1 = { '\t', '\r', '\n', '\"', '\'', '\\', '\0' }; //escape sequences like in strings

/// <+lang integral numeric types>Integer numbers<> are numbers like <.c>10<> but not like <.c>1.5<>.

int i1 = 10, i2 = -20;
int i3 = 2000000000; //or 2_000_000_000
int i4 = 0x10; //hexadecimal 16
int i5 = unchecked((int)0xFFFFFFFF); //sorry, simple values 0x80000000...0xFFFFFFFF are considered too big for int
uint u1 = 0xFFFFFFFF; //unsigned int
var u2 = 5u; //uint (suffix u)
var k1 = 5L; //long (suffix L)
long k2 = 9_223_372_036_854_775_807; //max long integer value
print.it(int.MaxValue, int.MinValue); //for max/min values it's better to use these constants

/// <+lang floating point numeric types>Floating-point numbers<> can be non-integer and can hold larger values.

double d1 = 3.14;
double d2 = 0.5; //or .5
double d4 = -2e6; //-2000000
double d5 = 3.5e-3; //0.0035
var d6 = 5d; //double (use suffix d)
print.it(double.MaxValue, Math.PI); //use constants
float f1 = 0.5f; //float is smaller (32-bit instead of 64-bit) and less precise
decimal g1 = 10.1234m; //the largest numeric type

/// Boolean values can be either <.k>true<> or <.k>false<>.

bool b1 = true;
bool b2 = false;
bool? b3 = null; //nullable bool can be true, false or null

/// To specify default value of that type, use keyword <.k>default<>. For reference types, nullables and pointers also can be used <.k>null<>.

string sn = null; //string is a reference type (class)
DateTime dt = default; //value type (struct)
bool? bn = null; //nullable value type
