/// Can be used <google>Windows keyboard shortcuts</google>.

keys.send("Win+D"); //show desktop; or undo it if possible
keys.send("Win+M"); //minimize all windows
keys.send("Win+Shift+M"); //restore all windows
keys.send("Win+Tab"); //task view
keys.send("Win+Left"); //dock the active window at the left side of the screen
keys.send("Win+Right"); //dock the active window at the right side of the screen

/// Switch the active window like with Alt+Tab.

wnd.switchActiveWindow();
