/// Create a string variable.

string s = "Text example";

/// Most string functions are called (executed) like <mono>variable.Function(arguments)<>.

string uppercase = s.Upper();
string lowercase = s.Lower();
print.it(uppercase, lowercase);

/// Other functions are called like <mono>type.Function(arguments)<>.

string joined = string.Join(" + ", "one", "two", "three");
print.it(joined);

/// You can find all functions in the popup list that appears when you type dot (.) after a variable name or type name. Just several examples.

/// Get string length (number of characters).

int len = s.Length;
int lenSafe = s.Lenn(); //if s is null, returns 0; s.Lenght would throw exception

/// Is string empty?

if (s.NE()) print.it("s is null or \"\"");

/// Remove white-space charactars (spaces, tabs, newlines) from the start and/or end.

string ss = " text ";
print.it($"'{ss.Trim()}', '{ss.TrimStart()}', '{ss.TrimEnd()}'");

/// Append, prepend. See also recipe <+recipe>String formatting<>.

var sa = s + " appended";
var sp = "prepended " + s;
print.it(sa, sp);

/// Get substring (part of string).

var middle = s.Substring(5, 4); //get 4 characters starting from index 5
var end = s.Substring(5); //get from index 5 until the end
var start = s.Remove(4); //get 4 characters at the start
print.it(middle, end, start);

/// Another way to get substring.

middle = s[5..9]; //get from index 5 to 9 (not including s[9])
end = s[5..]; //get from index 5 until the end
start = s[..4]; //get 4 characters at the start
var end2 = s[^3..]; //get 3 characters at the end
var middle2 = s[2..^2]; //get all except 2 characters at the start and end
print.it(middle, end, start, end2, middle2);

/// Insert, remove, replace.

var si = s.Insert(4, " inserted");
var r1 = s.Remove(5, 2);
var r2 = s.ReplaceAt(5, 3, "te");
print.it(si, r1, r2);

/// Compare string.

if (s == "Text example") print.it("equals");
if (s.Eq("text example", ignoreCase: true)) print.it("equals"); //case-insensitive
if (s.Eqi("text example")) print.it("equals"); //the same
print.it(string.Compare(s, "text example", StringComparison.OrdinalIgnoreCase)); //0 if equal; useful in sorting

/// Compare start, end, middle.

var sc = "one two THREE";
if (sc.Starts("one")) print.it("sc starts with \"one\"");
print.it(sc.Ends("ee"), sc.Ends("ee", ignoreCase: true), sc.Eq(4, "Two"), sc.Eq(4, "two", ignoreCase: true));

/// Find character or substring. See also recipe <+recipe>Regular expressions<>.

var sf = "one, twoo, two, three, Four";
int i1 = sf.IndexOf(',');
int i2 = sf.Find("two");
int i3 = sf.FindWord("two");
int i4 = sf.Find("four", ignoreCase: true);
print.it(i1, i2, i3, i4);

/// Find and replace. See also recipe <+recipe>Regular expressions<>.

var sr = "/one/two/one/two";
var sr1 = sr.Replace('/', '\\'); //info: \\ is an escape sequence for character \
var sr2 = sr.Replace("two", "three");
print.it(sr1, sr2);

/// Split into lines.

var sl = @"one
two
three";
string[] lines = sl.Lines();
print.it("lines", lines);

/// Split using a separator character or substring. See also recipe <+recipe>Regular expressions<>.

var st = "one; two; three";
string[] tokens = st.Split(';', StringSplitOptions.TrimEntries);
print.it("tokens", tokens);

/// Split using several separator characters.

st = @"C:\folder\file";
tokens = st.Split(new char[] { ':', '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
print.it("tokens", tokens);
