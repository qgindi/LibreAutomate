# Operators, expressions
Examples of expressions with <a href='https://www.google.com/search?q=C%23+operators'>operators</a>. An expression is anything that produces a value.

```csharp
int x = 10;
x = x + 1; //11
x += 4; //15 (same as x = x + 4)
x++; //16 (same as x = x + 1)
x--; //15
x -= 4; //11
x = x - 1; //10

x = x * 2; //20
x *= 2; //40
x /= 2; //20
x = x / 2; //10
x = x % 3; //1 (division remainder)

x = 1;
int y = x++; //x 2, y 1 (same as y = x; x = x + 1;)
y = ++x; //x 3, y 3 (same as x = x + 1; y = x;)

x = -5;
x = -x; //5

if (x == 5) print.it("x is 5");
if (x != 5) print.it("x is not 5");
if (x > 0 && x <= 10) print.it("x is greater than 0 and less than or equal 10");
if (x < 0 || x >= 10) print.it("x is less than 0 or greater than or equal 10");

//operators && (AND) and || (OR) have lower precedence, therefore at first are calculated subexpressions with other operators.
//or you can use parentheses for subexpressions that must be calculated first:
if ((x > 0) && (x <= 10)) print.it("x is greater than 0 and less than or equal 10");

if (!(x > 0)) print.it("x is not greater than 0");
//operator ! (NOT) has higher precedence, therefore we use ( ) to calculate x > 0 first

x = 1;
x = x | 2; //3 (add bit with value 2)
x = x & ~1; //2 (remove bit with value 1); ~1 is calculated first, it reverses all bits of value 1
x = x ^ 4; //6 (reverse bit with value 4)
x = x ^ 4; //again 2
//to reverse a bit means to toggle between 0 and 1
x = x << 1; //4 (left-shift all bits 1 time)
x = x >> 1; //2 (right-shift all bits 1 time)

y = (x > 0) ? 1 : 2; //if x > 0, set y = 1, else set y = 2

x = y = 3; //y = 3; x = y;

//some non-numeric types support some operators too
string s = "ab";
s = s + "cd"; //abcd
s += "ef"; //abcdef
if (s == "abcdef") print.it("s is abcdef"); //case-sensitive
if (s != "abcdef") print.it("s is not abcdef"); //case-sensitive

s = null;
print.it(s ?? "if s is null, use this string instead");
s ??= "default text"; //if (s != null) s = "default text";

string s2 = s.Trim(); //if (s != null) s2 = s.Trim(); else throw new NullReferenceException();
s2 = s?.Trim(); //if (s != null) s2 = s.Trim(); else s2 = null;
int k = s.Length; //if (s != null) k = s.Length; else throw new NullReferenceException();
k = s?.Length ?? 0; //if (s != null) k = s.Length; else k = 0;
char c = s[0]; //if (s != null) c = s[0]; else throw new NullReferenceException();
c = s?[0] ?? default; //if (s != null) c = s[0]; else c = default;

object g = "string"; //object can hold almost any type
if (g is string) print.it("g is string"); //use operator 'is' to ask whether g holds a string
if (g is string s3) print.it("g is string " + s3); //if g holds a string, create variable s3 and assign the string
```

