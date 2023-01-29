# Collections - LINQ functions
<a href='https://www.google.com/search?q=LINQ'>LINQ</a> adds many extension methods to arrays, lists, strings and other <a href='Collections - array, List, Stack, Queue.md'>collections</a>. This recipe shows how to use some of them.

```csharp
//create a collection (array) to use with other examples
var a = new string[] { "One", "Two", "Three", "Four", "Five" };
```

Get the first matching item. The <a href='Callback functions, lambda, delegate, event.md'>callback function</a> decides what items match; it can compare any property/condition/etc. This example looks for a string that starts with "F".

```csharp
string s1 = a.FirstOrDefault(o => o.Starts("F"));
print.it(s1);
```

How it works: function <b>FirstOrDefault</b> calls the callback function (lambda) for each item until it returns true. Then <b>FirstOrDefault</b> returns that item.

Function <b>FirstOrDefault</b> returns null if not found. Function <b>First</b> throws exception instead. Function <b>Any</b> returns true/false. Function <b>Count</b> tells how many.

```csharp
if (a.Any(o => o.Eqi("three"))) print.it("found"); else print.it("not found");
if (!a.Any()) print.it("the collection is empty");
int n1 = a.Count(), n2 = a.Count(o => o.Starts("F"));
print.it(n1, n2);
```

Function <b>Where</b> returns all matching items.

```csharp
foreach (var v in a.Where(o => o.Starts("F"))) {
	print.it(v);
}
```

Many functions return a lazy <b>IEnumerable</b> object that may retrieve items later. Convert it to array or <b>List</b> if need.

```csharp
string[] a2 = a
	.Where(o => o.Starts("F"))
	.ToArray(); //or ToList
```

Remove duplicate or similar elements.

```csharp
var ai = new List<int> { 3, 4, 3, 5, 5, 1, 2, 1 };
print.it("Dictinct", ai.Distinct());
print.it("DictinctBy", a.DistinctBy(o => o[0])); //get strings with unique first character
```

Remove specified elements.

```csharp
print.it("Except", ai.Except(new int[] { 1, 5 }));
```

Reverse.

```csharp
print.it("reverse array", a.Reverse());
print.it("reverse string", "abc".Reverse());
```

Compare two collections.

```csharp
var c1 = new int[] { 5, 2, 9 };
var c2 = new int[] { 5, 2, 9 };
var c3 = new int[] { 5, 3, 9 };
print.it(c1.SequenceEqual(c2), c1.SequenceEqual(c3));
```

Get elements that have/inherit the specified type.

```csharp
var ao = new object[] { 3, "blue", 6.2, "yellow" };
print.it("strings", ao.OfType<string>());
```

Sorting.

```csharp
print.it("sort", a.OrderBy(o => o));
print.it("sort descending, case-insensitive", a.OrderByDescending(o => o, StringComparer.OrdinalIgnoreCase));
print.it("sort by property", a.OrderBy(o => o.Length));

var at = new (int i, string s)[] { (5, "five"), (8, "eight"), (2, "eight") }; //array of tuples
print.it("sort by member", at.OrderBy(o => o.s));
print.it("sort by two members", at.OrderBy(o => o.s).ThenBy(o => o.i));
```

Transform elements from one type to other type.

```csharp
print.it("Select: get a property", a.Select(o => o.Length)); //create ints from strings
print.it("Select: create objects of other type", a.Select(o => (o.Length, o))); //create tuples from strings
```

