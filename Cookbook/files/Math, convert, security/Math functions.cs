/// For simple math operations use <+recipe>operators<>. Also there are math functions in classes <see cref="Math"/>, <see cref="Math2"/> and namespace <see cref="System.Numerics"/>.

/// Min, max.

int i = 10;
print.it(Math.Min(i, 5), Math.Max(i, 100), Math.Clamp(i, 0, 100));

/// Get absolute value (remove sign).

i = -5;
print.it(Math.Abs(i));

/// Get integer part, round.

double d = 4.56789;
print.it((int)d, d.ToInt(), Math.Round(d, 2));

/// Square root, hypotenuse.

d = 9;
print.it(Math.Sqrt(d));

int x = 4, y = 6;
print.it(Math.Sqrt(x*x + y*y));

/// Is power of 2?

int i1 = 8, i2 = 9;
print.it(System.Numerics.BitOperations.IsPow2(i1), System.Numerics.BitOperations.IsPow2(i2));

/// If even <see cref="decimal"/> isn't big enough, try <see cref="System.Numerics.BigInteger"/>.

var big = new System.Numerics.BigInteger(decimal.MaxValue);
print.it(big, System.Numerics.BigInteger.Pow(big, 10));
