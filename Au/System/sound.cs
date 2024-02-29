using Microsoft.Win32;

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
		/// <param name="name">Sound event name. If <c>null</c>, displays all available names.</param>
		/// <param name="async">Don't wait until the sound ends. Note: the sound ends when this process exits.</param>
		/// <param name="system">Use the sound volume channel "System Sounds". Then <see cref="volume"/> isn't used.</param>
		/// <param name="orDefault">Play default sound if the specified sound not found or does not have a .wav file assigned.</param>
		/// <remarks>
		/// Sounds can be changed in the Control Panel's Sound dialog.
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
		/// <param name="text">Text to speak. If <c>null</c>, stops speaking.</param>
		/// <param name="async">Don't wait. Note: the sound ends when this process exits.</param>
		/// <param name="voice">
		/// A voice name from Control Panel -> Speech -> Text to speech. Can be partial, case-insensitive. Example: <c>"Zira"</c>.
		/// If <c>null</c>, uses default voice.
		/// Voice attributes can be specified using string format <c>"voice|reqAttr"</c> or <c>"voice|reqAttr|optAttr"</c>. Here <i>reqAttr</i> and <i>optAttr</i> are arguments for <google>ISpObjectTokenCategory.EnumTokens</google>. Each part can be empty. Example: <c>"|Gender=Female"</c>.
		/// </param>
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
		SAPI.ISpVoice _v;
		
		/// <summary>
		/// Creates a text-to-speech (speech synthesis) voice instance.
		/// </summary>
		/// <param name="voice">A voice name from Control Panel -> Speech -> Text to speech. Can be partial, case-insensitive. Example: <c>"Zira"</c>. If <c>null</c>, uses default voice.</param>
		public SpeakVoice(string voice = null) {
			_v = new SAPI.SpVoice() as SAPI.ISpVoice;
			GC.AddMemoryPressure(250_000);
			if (voice != null) SetVoice_(voice);
		}
		
		internal void SetVoice_(string voice) {
			if (!voice.NE()) {
				var cat = new SAPI.SpObjectTokenCategory() as SAPI.ISpObjectTokenCategory;
				cat.SetId(SAPI.SPCAT_VOICES, false);
				var a = voice.Split('|');
				voice = a[0];
				var et = cat.EnumTokens(a.Length > 1 && !a[1].NE() ? a[1] : null, a.Length > 2 && !a[2].NE() ? a[2] : null);
				for (int i = 0, n = et.GetCount(); i < n; i++) {
					var v = et.Item(i);
					if (!voice.NE()) {
						if (0 != v.OpenKey("Attributes", out var k)) continue;
						if (0 != k.GetStringValue("Name", out var s)) continue;
						if (s.Find(voice, true) < 0) continue;
					}
					_v.SetVoice(v);
					break;
				}
			} else _v.SetVoice(null);
		}
		
		///
		protected virtual void Dispose(bool disposing) {
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
			get => _v.GetRate();
			set { _v.SetRate(value); }
		}
		
		/// <summary>
		/// Gets or sets the volume, 0-100. See also <see cref="sound.volume"/>.
		/// </summary>
		public int Volume {
			get => _v.GetVolume();
			set { _v.SetVolume((ushort)value); }
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
		/// Returns <c>true</c> if currently is speaking. Returns <c>false</c> if finished or not started.
		/// </summary>
		public bool IsSpeaking => _RunningState() == SAPI.SpeechRunState.SRSEIsSpeaking;
		
		/// <summary>
		/// Returns <c>true</c> if finished speaking.
		/// </summary>
		public bool IsDone => _RunningState() == SAPI.SpeechRunState.SRSEDone;
		
		SAPI.SpeechRunState _RunningState() {
			_v.GetStatus(out var r);
			return r.RunningState;
		}
		
		/// <summary>
		/// Waits until the async speech ends.
		/// </summary>
		/// <param name="msTimeout">Timeout milliseconds, or -1.</param>
		public bool WaitUntilDone(int msTimeout) => 0 == _v.WaitUntilDone(msTimeout);
		
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
			_v.Speak(text, (uint)flags);
			GC.KeepAlive(this);
			if (flags.Has(SVFlags.ASYNC)) { //protect from GC while speaking
				Task.Run(() => { _v.WaitUntilDone(-1); GC.KeepAlive(this); });
			}
		}
	}
	
	//Easier would be to use the SAPI type library, but it creates problems, eg dotnet pack fails.
	unsafe class SAPI : NativeApi {
		[ComImport, Guid("6C44DF74-72B9-4992-A1EC-EF996E0422D4"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
		internal interface ISpVoice {
			void _0();
			void _1();
			void _2();
			void _3();
			void _4();
			void _5();
			void _6();
			void _7();
			void _8();
			void _9();
			void _a();
			void _b();
			void _c();
			void Pause();
			void Resume();
			void SetVoice(ISpObjectToken pToken);
			void _d();
			int Speak([MarshalAs(UnmanagedType.LPWStr)] string pwcs, uint dwFlags);
			void _e();
			void GetStatus(out SPVOICESTATUS pStatus, nint ppszLastBookmark = 0);
			int Skip([MarshalAs(UnmanagedType.LPWStr)] string pItemType, int lNumItems);
			void _f();
			void _g();
			void _h();
			void _i();
			void SetRate(int RateAdjust);
			int GetRate();
			void SetVolume(ushort usVolume);
			ushort GetVolume();
			[PreserveSig] int WaitUntilDone(int msTimeout);
		}
		
		internal struct SPVOICESTATUS {
			fixed uint _1[3];
			public SpeechRunState RunningState;
			fixed uint _2[9];
		}
		
		[ComImport, Guid("14056589-E16C-11D2-BB90-00C04F8EE6C0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
		internal interface ISpObjectToken {
			void _0();
			void _1();
			void _2();
			void _3();
			void _4();
			void _5();
			[PreserveSig] int OpenKey([MarshalAs(UnmanagedType.LPWStr)] string pszSubKeyName, out ISpDataKey ppSubKey);
		}
		
		[ComImport, Guid("2D3D3845-39AF-4850-BBF9-40B49780011D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
		internal interface ISpObjectTokenCategory {
			void _0();
			void _1();
			void _2();
			void _3();
			void _4();
			void _5();
			void _6();
			void _7();
			void _8();
			void _9();
			void _a();
			void _b();
			void SetId([MarshalAs(UnmanagedType.LPWStr)] string pszCategoryId, [MarshalAs(UnmanagedType.Bool)] bool fCreateIfNotExist);
			void _c();
			void _d();
			IEnumSpObjectTokens EnumTokens([MarshalAs(UnmanagedType.LPWStr)] string pzsReqAttribs, [MarshalAs(UnmanagedType.LPWStr)] string pszOptAttribs);
		}
		
		[ComImport, Guid("14056581-E16C-11D2-BB90-00C04F8EE6C0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
		internal interface ISpDataKey {
			void _0();
			void _1();
			void _2();
			[PreserveSig] int GetStringValue([MarshalAs(UnmanagedType.LPWStr)] string pszValueName, [MarshalAs(UnmanagedType.LPWStr)] out string s);
		}
		
		[ComImport, Guid("06B64F9E-7FDA-11D2-B4F2-00C04F797396"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
		internal interface IEnumSpObjectTokens {
			void _0();
			void _1();
			void _2();
			void _3();
			ISpObjectToken Item(int Index);
			int GetCount();
		}
		
		[ComImport, Guid("96749377-3391-11D2-9EE3-00C04F797396"), ClassInterface(ClassInterfaceType.None)]
		internal class SpVoice { }
		
		internal enum SpeechRunState {
			SRSEDone = 1,
			SRSEIsSpeaking
		}
		
		[ComImport, Guid("A910187F-0C7A-45AC-92CC-59EDAFB77B53"), ClassInterface(ClassInterfaceType.None)]
		internal class SpObjectTokenCategory { }
		
		internal const string SPCAT_VOICES = "HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Speech\\Voices";
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
