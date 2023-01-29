# Play sound, speak
Play a system sound.

```csharp
sound.playDefault();
sound.playError();
sound.playEvent("DeviceConnect");
```

Play a sound file.

```csharp
sound.playWav(folders.Windows + @"Media\Alarm01.wav");
```

Speak text.

```csharp
sound.speak("Today is " + DateTime.Now.ToLongDateString());
```

