/// Use <see cref="timer"/> in threads that process Windows messages. For example, if the thread has a window. The callback function runs in the same thread.

timer.after(1000, _ => { print.it("timer after 1000 ms"); });
timer.every(500, _ => { print.it("timer every 500 ms"); });
dialog.show("timer"); //the function processes Windows messages

/// Use <see cref="timer2"/> in threads that don't process Windows messages. The callback function runs in a thread pool thread.

timer2.after(1000, _ => { print.it("timer2 after 1000 ms"); });
timer2.every(500, _ => { print.it("timer2 every 500 ms"); });
5.s();
