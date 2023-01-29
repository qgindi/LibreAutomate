# Threads
Multiple threads are used to execute codes simultaneously. A process can have many threads.

Create 2 threads. See <a href='/api/Au.run.thread.html'>run.thread</a>.

```csharp
run.thread(() => {
	for (int i = 0; i < 10; i++) { 300.ms(); print.it(Environment.CurrentManagedThreadId); } //this code runs in a new thread
});
run.thread(() => {
	for (int i = 0; i < 10; i++) { 200.ms(); print.it(Environment.CurrentManagedThreadId); } //this code runs in another new thread
});
for (int i = 0; i < 10; i++) { 100.ms(); print.it(Environment.CurrentManagedThreadId); } //this code runs in the primary thread
```

Use thread pool threads.

```csharp
Task.Run(() => {
	for (int i = 0; i < 10; i++) { 300.ms(); print.it(Environment.CurrentManagedThreadId); }
});
Task.Run(() => {
	for (int i = 0; i < 10; i++) { 300.ms(); print.it(Environment.CurrentManagedThreadId); }
});
for (int i = 0; i < 10; i++) { 100.ms(); print.it(Environment.CurrentManagedThreadId); }
```

Not all codes are safe to run in multiple threads simultaneously. Objects that aren't thread-safe may be corrupted. Use <b><a href='https://www.google.com/search?q=lock+statement%2C+C%23+reference'>lock</a></b> to prevent it.

```csharp
var a = new List<int>();

Task.Run(() => {
	for (int i = 0; i < 100; i++) {
		10.ms();
		lock (a) {
			a.Add(i);
		}
	}
});
for (int i = 0; i < 100; i++) {
	11.ms();
	lock (a) {
		a.Add(i*100);
	}
}
```

