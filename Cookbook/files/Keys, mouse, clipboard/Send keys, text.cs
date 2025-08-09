/// To quickly insert <see cref="keys.send"/> code, use snippet <.x>kkKeysSendSnippet<>: type <.c>kk<> and select from the list. Or click toolbar button <b>Keys<> or <b>Input recorder<>.

var w = wnd.find(1, "*- Notepad", "Notepad").Activate();
keys.send("Alt+E P");

/// To send text use <see cref="keys.sendt"/> or <.x>keys.send<>. To quickly insert code, use snippet <.x>ktKeysSendSnippet<>.

keys.sendt("Text."); //send text as characters, or paste if very long
keys.send("!Text."); //the same
keys.send("^Text."); //send text as hardware keys

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

/// <mono>Ctrl<>+click.

Action click = () => mouse.click();
keys.send("Ctrl+", click);

/// Send key raw/fast. <help keys.more.sendKey>More info<>.

keys.more.sendKey(KKey.A); //press A
keys.more.sendKey(KKey.Ctrl, true); //Ctrl down
keys.more.sendKey(KKey.Ctrl, false); //Ctrl up

/// Turn off <mono>CapsLock<>.

if (keys.isCapsLock) keys.more.sendKey(KKey.CapsLock);
