/// How to create/show a window and then use <see cref="ActionTriggers"/> in the same script? You may encounter these problems:
/// 1. You don't know where and in what order to call <b>ShowDialog</b>, <see cref="ActionTriggers.Run"/>, etc.
/// 2. <b>ShowDialog</b> and <b>ActionTriggers.Run</b> both run all the time, and therefore cannot run in the same thread simultaneously, unless <b>ShowDialog</b> is called from a trigger action.
/// 3. The script may not exit when the window closed, because <b>ActionTriggers.Run</b> is still running.
/// 4. In trigger actions you cannot use WPF window functions because they run in another thread.

using Au.Triggers;
using System.Windows;
using System.Windows.Controls;

/// A good and easy way - run triggers in another thread. And set option to execute trigger actions in the primary thread.

//build window
var b = new wpfBuilder("Window");
b.R.AddOkCancel();
b.End();

//set triggers
ActionTriggers Triggers = new();
Triggers.Options.ThreadThis(); //let trigger actions run in this thread
var hk = Triggers.Hotkey;
hk["Ctrl+Shift+L"] = o => { b.Window.WindowState = WindowState.Minimized; };
hk["Ctrl+Shift+K"] = o => { b.Window.WindowState = WindowState.Normal; };

//run triggers and show window
run.thread(() => { Triggers.Run(); });
if (!b.ShowDialog()) return;

/// Or you may want to use <b>Show</b> instead of <b>ShowDialog</b>. It does not wait. The "build window" and "set triggers" parts are the same. The "run triggers and show window" part can be like this:

b.OkApply += o => { print.it("OK"); };
b.Window.Closed += (_, _) => { Triggers.Stop(); };
b.Window.Show();
Triggers.RunThread();
