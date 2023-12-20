/// Use functions of class <see cref="wait"/>.

wait.ms(10); //10 milliseconds
wait.s(2); //2 seconds

/// The above functions also can be called in this way:

10.ms();
2.s();

/// To quickly insert te above statements you can type 10ms and 2s.

/// The sleep time precision is 1-2 ms. The .NET function <b>Thread.Sleep</b> is less precise (15-16 ms). 

/// Most wait functions should not be used in threads with windows, hooks, triggers, COM. While waiting, the thread cannot process Windows messages and other events, and may hang. If need to wait and process messages, the simplest (but often not the best) way is <see cref="wait.doEvents"/>.
