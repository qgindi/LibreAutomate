using Microsoft.Win32;
using SpeechLib;

namespace Au {
	/// <summary>
	/// Plays short sounds and speaks text.
	/// </summary>
	public static class sound {
		/// <summary>
		/// Gets or sets the sound volume of this program. Percent 0-100 of the master volume.
		/// </summary>
		/// <exception cref="ArgumentException"></exception>
		/// <remarks>
		/// Used for speech and .wav files, but not for system sounds.
		/// Sets volume for each program seperately. The program remembers it after restarting. Note: all scripts with role miniProgram (default) run in the same program (Au.Task.exe).
		/// </remarks>
		public static int volume {
			get {
				Api.waveOutGetVolume(default, out var v);
				v &= 0xffff;
				return (int)Math.Round((double)v * 100 / 0xffff);
			}
			set {
				uint v = (uint)value;
				if (v > 100) throw new ArgumentException("Must be 0-100.");
				v = (uint)(0xffff * v / 100);
				Api.waveOutSetVolume(default, v << 16 | v);
			}
		}

		/// <summary>
		/// Plays a custom sound (.wav file).
		/// </summary>
		/// <param name="wavFile">.wav file.</param>
		/// <param name="async">Don't wait until the sound ends. Note: the sound ends when this process exits.</param>
		/// <param name="system">Use the sound volume channel "System Sounds". Then <see cref="volume"/> isn't used.</param>
		public static bool playWav(string wavFile, bool async = false, bool system = false) {
			var s = wavFile.NE() ? null : pathname.expand(wavFile);
			var f = Api.SND_FILENAME | Api.SND_NODEFAULT;
			if (async) f |= Api.SND_ASYNC;
			if (system) f |= Api.SND_SYSTEM;
			return Api.PlaySound(s, default, f);
		}

		/// <summary>
		/// Plays a system event sound.
		/// </summary>
		/// <param name="name">Sound event name. If null, displays all available names.</param>
		/// <param name="async">Don't wait until the sound ends. Note: the sound ends when this process exits.</param>
		/// <param name="system">Use the sound volume channel "System Sounds". Then <see cref="volume"/> isn't used.</param>
		/// <param name="orDefault">Play default sound if the specified sound not found or does not have a .wav file assigned.</param>
		/// <remarks>
		///	Sounds can be changed in the Control Panel's Sound dialog.
		/// </remarks>
		public static bool playEvent(string name, bool async = false, bool system = false, bool orDefault = false) {
			if (name == null) {
				using var k1 = Registry.CurrentUser.OpenSubKey(@"AppEvents\Schemes\Apps\.Default");
				foreach (var s in k1.GetSubKeyNames()) {
					using var k2 = k1.OpenSubKey(s + @"\.Current");
					if (k2?.GetValue("") is string file && file.Length > 0) {
						var label = Registry.GetValue(@"HKEY_CURRENT_USER\AppEvents\EventLabels\" + s, "", null) as string;
						print.it($"{s,-30}   {label,-30}   {file}");
					}
				}
				return false;
			} else {
				uint f = Api.SND_ALIAS;
				//f |= Api.SND_APPLICATION; //doesn't work. Plays only sounds from the ".Default" key, with or without this flag.
				if (!orDefault) f |= Api.SND_NODEFAULT;
				if (async) f |= Api.SND_ASYNC;
				if (system) f |= Api.SND_SYSTEM;
				return Api.PlaySound(name, default, f);
			}
		}

		/// <summary>
		/// Plays the system default sound.
		/// </summary>
		/// <remarks>
		/// Does not wait until the sound ends. The sound can continue even when this process ends.
		/// </remarks>
		public static void playDefault() {
			Api.MessageBeep(0x40);
		}

		/// <summary>
		/// Plays the system error sound.
		/// </summary>
		/// <remarks>
		/// Does not wait until the sound ends. The sound can continue even when this process ends.
		/// </remarks>
		public static void playError() {
			Api.MessageBeep(0x10);
		}

		//other system sounds now are silent or same as default.

		/// <summary>
		/// Generates sound of specified frequency and duration. Waits until it ends.
		/// </summary>
		/// <param name="freq">Frequency, 37-32767 hertz.</param>
		/// <param name="duration">Duration, in milliseconds.</param>
		/// <param name="async">Don't wait. Note: the sound ends when this process exits.</param>
		public static void beep(int freq, int duration, bool async = false) {
			if (async) {
				Task.Run(() => Api.Beep(freq, duration));
			} else {
				Api.Beep(freq, duration);
			}
		}

		/// <summary>
		/// Speaks text.
		/// </summary>
		/// <param name="text">Text to speak. If null, stops speaking.</param>
		/// <param name="async">Don't wait. Note: the sound ends when this process exits.</param>
		/// <param name="voice">A voice name from Control Panel -> Speech -> Text to speech. Can be partial, case-insensitive. Example: "Zira". If null, uses default voice.</param>
		/// <param name="rate">Speed adjustment, +- 10.</param>
		/// <param name="volume">Volume, 0-100. See also <see cref="volume"/>.</param>
		/// <seealso cref="SpeakVoice"/>
#if true
		public static void speak(string text, bool async = false, string voice = null, int rate = 0, int volume = 100) {
			lock (s_lock) {
				s_voice?.Stop();
				if (text.NE()) return;

				s_voice ??= new SpeakVoice();
				if (voice != s_sVoice) s_voice.SetVoice_(s_sVoice = voice);
				s_voice.Rate = rate;
				s_voice.Volume = volume;
			}
			s_voice.Speak(text, async);
		}
		static string s_sVoice;
#else //use new SpVoice each time
	public static void speak(string text, bool async = false, string voice = null, int rate = 0, int volume = 100) {
		SpeakVoice v = null;
		lock (s_lock) {
			if (s_voice!=null) {
				s_voice.Dispose();
				s_voice=null;
			}
			if (text.NE()) return;
			
			v = new SpeakVoice(voice);
			if (rate != 0) v.Rate = rate;
			if (volume != 100) v.Volume = volume;
			
			s_voice=v;
		}
		v.Speak(text, async);
		if(!async) {
			lock (s_lock) { if (s_voice==v) { s_voice=null; v.Dispose(); } }
		}
		//else v.EndStream+=(o, e) => print.it("end"); //need to process messages
	}
#endif
		static SpeakVoice s_voice;
		static readonly object s_lock = new();
	}
}

namespace Au.More {
	/// <summary>
	/// Speaks text.
	/// </summary>
	/// <seealso cref="sound.speak"/>
	public class SpeakVoice : IDisposable {
		SpVoice _v;

		/// <summary>
		/// Creates a text-to-speech (speech synthesis) voice instance.
		/// </summary>
		/// <param name="voice">A voice name from Control Panel -> Speech -> Text to speech. Can be partial, case-insensitive. Example: "Zira". If null, uses default voice.</param>
		public SpeakVoice(string voice = null) {
			//PROBLEM: slow. Ctor first time ~130 ms, sometimes 200; then ~70.
			//	Native code cocreateinstance fast, but Speak starts slower. Total time until the real sound comes is similar. Tested with hot CPU too.

			_v = new SpeechLib.SpVoice();
			GC.AddMemoryPressure(250_000);
			if (voice != null) SetVoice_(voice);
		}

		internal void SetVoice_(string voice) {
			if (!voice.NE()) {
				foreach (SpObjectToken v in _v.GetVoices()) {
					var sd = v.GetDescription();
					//print.it(sd);
					if (sd.Find(voice, true) >= 0) { _v.Voice = v; break; }
				}
			} else _v.Voice = null;
		}

		///
		protected void Dispose(bool disposing) {
			if (_v != null) {
				if (disposing) Marshal.ReleaseComObject(_v); //stops speaking if async
				_v = null;
				GC.RemoveMemoryPressure(250_000);
			}
		}

		///
		public void Dispose() {
			Stop();
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		///
		~SpeakVoice() {
			//print.it("~");
			Dispose(false);
		}

		/// <summary>
		/// Gets or sets the speed adjustment, +- 10.
		/// </summary>
		public int Rate {
			get => _v.Rate;
			set { _v.Rate = value; }
		}

		/// <summary>
		/// Gets or sets the volume, 0-100. See also <see cref="sound.volume"/>.
		/// </summary>
		public int Volume {
			get => _v.Volume;
			set { _v.Volume = value; }
		}

		/// <summary>
		/// Pauses speaking.
		/// </summary>
		public void Pause() => _v.Pause();

		/// <summary>
		/// Resumes speaking.
		/// </summary>
		public void Resume() => _v.Resume();

		/// <summary>
		/// Skips <i>count</i> milliseconds of speech.
		/// </summary>
		/// <param name="count">Forward if positive, else backward.</param>
		public void SkipMilliseconds(int count) => _v.Skip("MILLISECOND", count);

		/// <summary>
		/// Skips <i>count</i> sentences of speech.
		/// </summary>
		/// <param name="count">Forward if positive, else backward. If 0, repeats current sentence.</param>
		public void SkipSentence(int count) => _v.Skip("SENTENCE", count);

		/// <summary>
		/// Stops speaking.
		/// </summary>
		public void Stop() => SkipSentence(int.MaxValue);

		/// <summary>
		/// Returns true if currently is speaking. Returns false if finished or not started.
		/// </summary>
		public bool IsSpeaking => _v.Status.RunningState == SpeechRunState.SRSEIsSpeaking;

		/// <summary>
		/// Returns true if finished speaking.
		/// </summary>
		public bool IsDone => _v.Status.RunningState == SpeechRunState.SRSEDone;

		/// <summary>
		/// Waits until the async speech ends.
		/// </summary>
		/// <param name="msTimeout">Timeout milliseconds, or -1.</param>
		public bool WaitUntilDone(int msTimeout) => _v.WaitUntilDone(msTimeout);

		/// <summary>
		/// Speaks the specified text.
		/// </summary>
		/// <param name="text">Text to speak.</param>
		/// <param name="async">Don't wait. Note: the sound ends when this process exits.</param>
		public void Speak(string text, bool async = false) {
			Speak(text, async ? SVFlags.ASYNC : 0);
		}

		/// <summary>
		/// Speaks the specified text.
		/// </summary>
		/// <param name="text">Text to speak.</param>
		/// <param name="flags"></param>
		public void Speak(string text, SVFlags flags) {
			if (flags.Has(SVFlags.IS_FILENAME)) text = pathname.expand(text);
			_v.Speak(text, (SpeechVoiceSpeakFlags)flags);
			GC.KeepAlive(this);
			if (flags.Has(SVFlags.ASYNC)) { //protect from GC while speaking
				Task.Run(() => { _v.WaitUntilDone(-1); GC.KeepAlive(this); });
			}
		}
	}
}

namespace Au.Types {
	/// <summary>
	/// Flags for <see cref="sound.speak"/>. See <msdn>SPEAKFLAGS</msdn>.
	/// </summary>
	[Flags]
	public enum SVFlags {
#pragma warning disable 1591 //XML doc
		ASYNC = 0x0001,
		PURGEBEFORESPEAK = 0x0002,
		IS_FILENAME = 0x0004,
		IS_XML = 0x0008,
		IS_NOT_XML = 0x0010,
		PERSIST_XML = 0x0020,
		NLP_SPEAK_PUNC = 0x0040,
		PARSE_SAPI = 0x0080,
		PARSE_SSML = 0x0100,
#pragma warning restore
	}
}
