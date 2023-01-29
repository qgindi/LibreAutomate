# Math functions
For simple math operations use <a href='Operators, expressions.md'>operators</a>. Also there are math functions in classes <a href='https://www.google.com/search?q=System.Math+class'>Math</a>, <a href='/api/Au.More.Math2.html'>Math2</a> and namespace <a href='https://www.google.com/search?q=System.Numerics+namespace'>System.Numerics</a>.

Min, max.

```csharp
int i = 10;
print.it(Math.Min(i, 5), Math.Max(i, 100), Math.Clamp(i, 0, 100));
```

Get absolute value (remove sign).

```csharp
i = -5;
print.it(Math.Abs(i));
```

Get integer part, round.

```csharp
double d = 4.56789;
print.it((int)d, d.ToInt(), Math.Round(d, 2));
```

Square root, hypotenuse.

```csharp
d = 9;
print.it(Math.Sqrt(d));

int x = 4, y = 6;
print.it(Math.Sqrt(x*x + y*y));
```

Is power of 2?

```csharp
int i1 = 8, i2 = 9;
print.it(System.Numerics.BitOperations.IsPow2(i1), System.Numerics.BitOperations.IsPow2(i2));
```

If even <a href='https://www.google.com/search?q=System.Decimal+structure'>decimal</a> isn't big enough, try <a href='https://www.google.com/search?q=System.Numerics.BigInteger+structure'>System.Numerics.BigInteger</a>.

```csharp
var big = new System.Numerics.BigInteger(decimal.MaxValue);
print.it(big, System.Numerics.BigInteger.Pow(big, 10));
```

