# Send keys, text
To quickly insert <a href='/api/Au.keys.send.html'>keys.send</a> code, use snippet kkKeysSendSnippet: type kk and select from the list. Or click toolbar button "Keys" or "Input recorder".

```csharp
var w = wnd.find(1, "*- Notepad", "Notepad").Activate();
keys.send("Alt+E P");
```

To send text use <a href='/api/Au.keys.sendt.html'>keys.sendt</a> or <b>keys.send</b>. To quickly insert code, use snippet ktKeysSendSnippet.

```csharp
keys.sendt("Text."); //send text as characters, or paste if very long
keys.send("!Text."); //the same
keys.send("^Text."); //send text as hardware keys
```

Send keys and text.

```csharp
keys.send("Ctrl+A Del", "!Text", "Ctrl+S", "!filename.txt");
```

Change speed and other options. To quickly insert code can be used speedSnippet.

```csharp
opt.key.KeySpeed = 50;
opt.key.TextSpeed = 20;
opt.key.SleepFinally = 100;
opt.key.TextHow = OKeyText.KeysOrChar;
keys.send("Ctrl+End Enter", "!Looooooooooooooooooooooooooooooooooooooooooooooong text.");
```

Repeat key or character.

```csharp
keys.send("Tab*4"); //Tab 4 times
keys.send("_**20"); //character * 20 times
```

Key down and up.

```csharp
keys.send("Alt*down E P Alt*up");
keys.send("Alt+(E P)"); //the same
```

Ctrl+click.

```csharp
Action click = () => mouse.click();
keys.send("Ctrl+", click);
```

Send key raw/fast. <a href='/api/Au.keys.more.sendKey.html'>More info</a>.

```csharp
keys.more.sendKey(KKey.A); //press A
keys.more.sendKey(KKey.Ctrl, true); //Ctrl down
keys.more.sendKey(KKey.Ctrl, false); //Ctrl up
```

Turn off CapsLock.

```csharp
if (keys.isCapsLock) keys.more.sendKey(KKey.CapsLock);
```

