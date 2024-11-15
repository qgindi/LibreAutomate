## Version 1.7.0 (2024-)

### Editor
New tools:
- .

New cookbook recipes:
- .

Improved:
- .

Fixed bugs:
- .

### Library
New classes:
- .

New members:
- .

New parameters:
- .

Improved:
- **opt.key/mouse/warnings** now are managed differently, and work well with `await` and thread pool tasks. Each new thread and task inherits a copy of **opt.key/mouse/warnings** of the parent thread/task, which then is isolated from parent and other threads/tasks. Now **opt.init** is obsolete and is the same as **opt**.
- **mouse.save/restore/lastXY** now works well with `await`. Now the saved data is shared by all threads.

Fixed bugs:
- .

### Breaking changes
Because of the **opt.key/mouse/warnings** management change, initial options in threads and tasks now may be different.
