/// Play a system sound.

sound.playDefault();
sound.playError();
sound.playEvent("DeviceConnect");

/// Play a sound file.

sound.playWav(folders.Windows + @"Media\Alarm01.wav");

/// Speak text.

sound.speak("Today is " + DateTime.Now.ToLongDateString());
