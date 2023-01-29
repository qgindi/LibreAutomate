# Date and time
For date-time use <a href='https://www.google.com/search?q=C%23+struct+DateTime'>DateTime</a>. For time interval use <a href='https://www.google.com/search?q=C%23+struct+TimeSpan'>TimeSpan</a>.

Get current date and time-of-day.

```csharp
var d = DateTime.Now;
```

Add, subtract.

```csharp
d = d.AddDays(1); //add 1 day
d = d.AddDays(-1.5); //subtract 1 day and 12 hours
d = d.Add(new TimeSpan(1, 2, 0, 0)); //add 1 day and 2 hours
d = d.Subtract(new TimeSpan(1, 30, 0)); //subtract 1 hour and 30 minutes
TimeSpan ts = d.Subtract(new DateTime(2022, 1, 10)); //get the difference between two dates
print.it(ts, ts.Days);
```

Compare two dates.

```csharp
var d2 = new DateTime(2022, 1, 20);
if (d > d2) print.it("d is later than 2022-01-20");
```

Convert to string.

```csharp
print.it(d.ToString());
print.it(d.ToLongDateString() + "; " + d.ToShortTimeString());
```

Convert to localized string.

```csharp
var c = CultureInfo.InstalledUICulture; //current user culture
//var c = new CultureInfo("de-DE"); //some other culture
print.it(d.ToString(c));
print.it(d.ToString("D", c) + "; " + d.ToString("t", c));
```

To use localized text everywhere in the script by default, add this at the start of the script.

```csharp
process.thisProcessCultureIsInvariant = false;
```

Convert string to DateTime.

```csharp
var d3 = DateTime.Parse("2022-01-29");
var d4 = DateTime.Parse("29.01.2022", new CultureInfo("de-DE"));
print.it(d3, d4);
```

Get only the date or time-of-day part.

```csharp
d = DateTime.Now; //get date and time-of-day
DateTime dd = d.Date; //get date
TimeSpan tt = d.TimeOfDay; //get time-of-day
print.it(d, dd, tt);
d = DateTime.Today; //get date
```

UTC.

```csharp
d = DateTime.UtcNow;
d = d.ToLocalTime();
d = d.ToUniversalTime();
```

