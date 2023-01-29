# String formatting with variables
There are many ways to create strings from multiple substrings and variables.

Operator +.

```csharp
int n = 99;
string s = "There are " + n + " ways.";
print.it(s);
```

Interpolated string.

```csharp
string c = "create";
s = $"There are {n} ways to {c} strings.";
```

Function <a href='https://www.google.com/search?q=System.String.Format+method'>string.Format</a> now is almost obsolete. Use interpolated strings instead.

```csharp
s = string.Format("There are {0} ways to {1} strings.", n, c);
```

When need to append many times, use <a href='https://www.google.com/search?q=System.Text.StringBuilder+class'>StringBuilder</a> to avoid creating many intermediate strings (garbage).

```csharp
var b = new StringBuilder();
for (int i = 0; i < 10; i++) {
	b.Append(i).AppendFormat(" {0,5}", i * i);
	b.AppendLine();
}
s = b.ToString();
print.it(s);
```

String constructors.

```csharp
s = new string('*', 20);
print.it(s);
```

Various functions.

```csharp
string[] a = { "one", "two", "three" };
s = string.Join("; ", a);
print.it(s);
```

Function <b>ToString</b> of various variable types.

```csharp
print.it("0x" + n.ToS("X8")); //hexadecimal number format with 8 digits

var r = new RECT(1, 2, 3, 4);
print.it(r.ToString());
```

String interpolation also can be used with verbatim strings and raw strings.

```csharp
s = $@"There are {n} ways
to {c} strings.";

s = $$"""
There are {{n}} ways
to {{c}} strings.
""";
```

<a href='https://www.google.com/search?q=C%23+string+interpolation'>Interpolations</a> can be <a href='https://www.google.com/search?q=C%23+composite+formatting'>formatted</a> in various ways.

```csharp
for (int i = 8; i < 15; i++) {
	print.it($"{i,-5} 0x{i:X8} {Random.Shared.NextDouble(),8:F3}");
	//-5: left-align the value within a field of spaces of minimum 5 characters width.
	//X8: hexadecimal number format; minimum 8 characters in the number.
	//8: right-align the value within a field of spaces of minimum 8 characters width.
	//F3: only 3 digits after dot.
}
```

For literal characters { and } in interpolated strings use escape sequences {{ and }}. Or use raw string with multiple $ in prefix; then enclose interpolations in the same number of { and }.

```csharp
print.it($"Ab {{cd}} {n}");
print.it($$"""Ab {cd} {{n}}""");
```

If an interpolation expression contains special characters, enclose it in ().

```csharp
print.it($"Ab {(n > 0 ? n : 0)}");
```

Localization with string interpolation.

```csharp
double d = 1.5;
var dt = DateTime.Now;
FormattableString f = $"{d} {dt}";
print.it(f.ToString());
print.it(f.ToString(CultureInfo.InstalledUICulture));
print.it(f.ToString(CultureInfo.GetCultureInfo("nl-NL")));
```

