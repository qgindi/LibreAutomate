/// Activate a window before sending keys or text to it.

var w = wnd.find(1, "*- Notepad++").Activate();
//or: wnd.switchActiveWindow();

/// To send keys, use <see cref="keys.send"/>. To quickly insert code, use snippet <.x>kkKeysSendSnippet<>: type `kk` and select from the list. Or click toolbar button <b>Keys<> or <b>Input recorder<>.

keys.send("Alt+E P Enter"); //note: the string contains key names, not any text

/// To send text, use prefix `!`. Or <see cref="keys.sendt"/> (snippet <.x>ktKeysSendSnippet<>).

keys.send("!Text.");
keys.sendt("Text."); //the same
keys.send("^Text."); //send text using keys if possible, eg keys Shift+T for uppercase T

/// Send keys and text.

keys.send("Ctrl+A Del", "!Text", "Ctrl+S", "!filename.txt");

/// Change speed and other options. To insert code can be used <.x>speedOptSnippet<>.

opt.key.KeySpeed = 50;
opt.key.TextSpeed = 20;
opt.key.SleepFinally = 100;
opt.key.TextHow = OKeyText.KeysOrChar;
keys.send("Ctrl+End Enter", "!Looooooooooooooooooooooooooooooooooooooooooooooong text.");

/// Repeat key or character.

keys.send("Tab*4"); //Tab 4 times
keys.send("_**20"); //character * 20 times

/// Key down and up.

keys.send("Alt*down E P Alt*up");
keys.send("Alt+(E P)"); //the same

/// The best way to send menu access characters:

keys.send("Alt+^ep");

/// `Ctrl`+click.

Action click = () => mouse.click();
keys.send("Ctrl+", click);

/// Send key raw/fast. <see cref="keys.more.sendKey">More info<>.

keys.more.sendKey(KKey.A); //press A
keys.more.sendKey(KKey.Ctrl, true); //Ctrl down
keys.more.sendKey(KKey.Ctrl, false); //Ctrl up

/// Turn off `CapsLock`.

if (keys.isCapsLock) keys.more.sendKey(KKey.CapsLock);
