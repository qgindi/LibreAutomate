# Computer time, code speed
Get the number of milliseconds that have elapsed since the system was started.

```csharp
long t1 = Environment.TickCount64; //with the sleep time
long t2 = computer.tickCountWithoutSleep; //without the sleep time
```

The above functions have low precision (resolution). Usually 15-16 ms. When need high precision, use <a href='/api/Au.perf.html'>perf</a> functions.

```csharp
long t3 = perf.ms; //milliseconds
long t4 = perf.mcs; //microseconds
```

To measure code speed, use <a href='/api/Au.perf.html'>perf</a> functions.

```csharp
perf.first();
50.ms();
perf.next();
5.ms();
perf.next();
for (int i = 0; i < 1000; i++) { var v = Environment.GetEnvironmentVariable("TEMP"); }
perf.next();
perf.write();
```

Or use a local <b>perf</b> variable.

```csharp
MyFunction();
void MyFunction() {
	using var p1 = perf.local(); //creates a local perf variable and calls First. If with 'using', will automatically print the times on exit.
	5.ms();
	p1.Next();
	50.ms();
}
```

