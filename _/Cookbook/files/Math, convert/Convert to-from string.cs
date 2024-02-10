/// Convert number to string.

int i = 20;
string s = i.ToString(); //use use ToS to avoid localization
print.it(s);

string hex = $"0x{i:X08}";
print.it(hex);

/// Convert string to number.

int i2 = s.ToInt();
print.it(i2);

s = "10.5";
double d = s.ToNumber();
print.it(d);

/// Also there are <b>ToInt<> and <b>ToNumber<> overloads with various options.

if (s.ToInt(out int r1)) print.it(r1); else print.it("not a number"); //ToInt can parse part of string
if (s.ToNumber(out int r2)) print.it(r2); else print.it("not a number"); //ToNumber always parses entire string
if (s.ToNumber(out double r3)) print.it(r3); else print.it("not a number");

string s2 = "#A4"; //hex 164 at offset 1
print.it(s2.ToInt(1, flags: STIFlags.IsHexWithout0x));

string s3 = "N: 10 20";
if (s3.ToInt(out int r4, 3, out int end1)) print.it(r4, s3.ToInt(end1));

/// Throw exception if entire string is not a number of that type.

print.it(double.Parse(s)); //ok ("10.5" is a double number)
print.it(int.Parse(s)); //exception ("10.5" is not an int number)
