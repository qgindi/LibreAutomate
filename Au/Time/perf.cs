//#define PREPAREMETHOD //now slow anyway. Does not JIT everything.

namespace Au {
	/// <summary>
	/// Code execution time measurement with high precision (API <msdn>QueryPerformanceCounter</msdn>).
	/// </summary>
	public static unsafe class perf {
		//info: we don't use Stopwatch. It used to load System.dll (before .NET Core, now don't know), which is slow and can make speed measurement incorrect and confusing in some cases.
		
		static readonly double s_freqMCS = Api.QueryPerformanceFrequency(out long f) ? 1_000_000d / f : 0d, s_freqMS = s_freqMCS / 1000d;
		//note: don't use static ctor. Makes some simplest functions much slower.
		
		/// <summary>
		/// Gets the number of microseconds elapsed since Windows startup. Uses the high-resolution system timer (API <msdn>QueryPerformanceCounter</msdn>).
		/// </summary>
		/// <remarks>
		/// This function is used to measure time differences with 1 microsecond precision, like <c>var t1=perf.mcs; ... var t2=perf.mcs; var diff=t2-t1;</c>.
		/// Independent of computer clock time changes.
		/// See also: <msdn>Acquiring high-resolution time stamps</msdn>.
		/// </remarks>
		/// <seealso cref="perf"/>
		public static long mcs { get { Api.QueryPerformanceCounter(out var t); return (long)(t * s_freqMCS); } }
		//On current OS and hardware QueryPerformanceCounter is reliable and fast enough.
		//Speed of 1000 calls when cold CPU: PerfMilliseconds 97, WinMillisecondsWithoutSleep 76, WinMilliseconds64/GetTickCount64 12.
		//The double cast/multiply/cast is fast. The API is much much slower. And Math.BigMul is much slower.
		//rejected: make corrections based on GetTickCount64. It makes slower and is not necessary.
		
		/// <summary>
		/// Gets the number of milliseconds elapsed since Windows startup. Uses the high-resolution system timer (API <msdn>QueryPerformanceCounter</msdn>).
		/// </summary>
		/// <remarks>
		/// This function is used to measure time differences with 1 ms precision, like <c>var t1=perf.ms; ... var t2=perf.ms; var diff=t2-t1;</c>.
		/// The return value equals <see cref="mcs"/>/1000 but is slightly different than of <see cref="Environment.TickCount64"/> and most Windows API. Never compare times returned by different functions.
		/// Independent of computer clock time changes.
		/// </remarks>
		public static long ms { get { Api.QueryPerformanceCounter(out var t); return (long)(t * s_freqMS); } }
		
		/// <summary>
		/// Performs time measurements and stores measurement data.
		/// </summary>
		/// <remarks>
		/// Static functions of <see cref="perf"/> class use a single static variable of this type to perform measurements.
		/// Use a variable of this type instead when you want to have multiple independent measurements. See <see cref="perf.local"/>.
		/// Variables of this type usually are used as local (in single function), but also can be used anywhere (class fields, unmanaged memory).
		/// This type is a struct (value type), and not small (184 bytes). Don't use it as a function parameter without ref. To share a variable between functions use a field in your class.
		/// Don't need to dispose variables of this type. The <see cref="Dispose"/> function just calls <see cref="NW"/>.
		/// </remarks>
		public unsafe struct Instance : IDisposable {
			static Instance() {
				//Prevent JIT delay when calling Next etc
#if PREPAREMETHOD
				Jit_.Compile(typeof(Instance), "Next", "NW", "Dispose");
#if DEBUG //else these methods are inlined
				Jit_.Compile(typeof(perf), "Next", "NW");
#endif
#else
				perf.next(); perf.nw(); perf.first(); //JIT-compiles everything we need. s_enabled prevents calling print.it etc.
				s_enabled = true;
#endif
				
				//JIT speed: 1 ms.
			}
			
#if !PREPAREMETHOD
			readonly static bool s_enabled;
#endif
			const int _nElem = 16;
			
			fixed long _a[_nElem];
			fixed char _aMark[_nElem];
			volatile int _counter;
			bool _incremental;
			int _nMeasurements; //used with incremental to display n measurements and average times
			long _time0;
			
			/// <summary>See <see cref="perf.incremental"/>.</summary>
			/// <example>
			/// <code><![CDATA[
			/// var p1 = new perf.Instance { Incremental = true };
			/// for(int i = 0; i < 5; i++) {
			/// 	Thread.Sleep(100); //not included in the measurement
			/// 	p1.First();
			/// 	Thread.Sleep(30); //will make sum ~150000
			/// 	p1.Next();
			/// 	Thread.Sleep(10); //will make sum ~50000
			/// 	p1.Next();
			/// 	Thread.Sleep(100); //not included in the measurement
			/// }
			/// p1.Write(); //speed:  154317  51060  (205377)
			/// p1.Incremental = false;
			/// ]]></code>
			/// </example>
			public bool Incremental {
				get => _incremental;
				set {
					if (_incremental = value) {
						fixed (long* p = _a) { for (int i = 0; i < _nElem; i++) p[i] = 0; }
						_nMeasurements = 0;
					}
				}
			}
			
			/// <summary><see cref="perf.first()"/></summary>
			public void First() {
#if !PREPAREMETHOD
				if (!s_enabled) return; //called by the static ctor
#endif
				Api.QueryPerformanceCounter(out _time0);
				_counter = 0;
				_nMeasurements++;
			}
			
			//rejected. See comments in static.
			///// <summary>
			///// Calls <see cref="Cpu"/> and <see cref="First()"/>.
			///// </summary>
			//public void First(int timeSpeedUpCPU)
			//{
			//	Cpu(timeSpeedUpCPU);
			//	First();
			//}
			
			/// <summary><see cref="perf.next"/></summary>
			public void Next(char cMark = '\0') {
#if !PREPAREMETHOD
				if (!s_enabled) return; //called by the static ctor
#endif
				int n = _counter; if (n >= _nElem) return;
				_counter++;
				fixed (long* p = _a) {
					Api.QueryPerformanceCounter(out long pc); long t = pc - _time0;
					if (_incremental) p[n] += t; else p[n] = t;
					//fixed (char* c = _aMark) c[n] = cMark;
					char* c = (char*)(p + _nElem); c[n] = cMark;
				}
			}
			
			/// <summary>
			/// Calls <see cref="Next"/> and <see cref="Write"/>.
			/// </summary>
			/// <param name="cMark">A character to add to the results string like <c>"A=150"</c>.</param>
			[MethodImpl(MethodImplOptions.NoInlining)]
			public void NW(char cMark = '\0') { Next(cMark); Write(); }
			
			/// <summary>
			/// Calls <see cref="NW"/>, which calls <see cref="Next"/> and <see cref="Write"/>.
			/// </summary>
			/// <remarks>
			/// Don't need to dispose variables of this type. This function just allows to use <c>using</c> instead of <b>NW</b>. See example.
			/// 
			/// If <see cref="Incremental"/>, calls just <see cref="Write"/>.
			/// </remarks>
			/// <example>
			/// <code><![CDATA[
			/// using(var p1 = perf.local()) { //p1.First();
			/// 	1.ms();
			/// 	p1.Next();
			/// 	1.ms();
			/// } //p1.NW();
			/// ]]></code>
			/// </example>
			public void Dispose() {
				if (!_incremental) Next();
				Write();
			}
			//public void Dispose() => NW();
			
			/// <summary>
			/// Formats a string from time values collected by calling <see cref="First"/> and <see cref="Next"/>, and shows it in the output.
			/// The string contains the number of microseconds of each code execution between calling <b>First</b> and each <b>Next</b>.
			/// </summary>
			public void Write() {
#if !PREPAREMETHOD
				if (!s_enabled) return; //called by the static ctor
#endif
				print.it(ToString());
			}
			
			/// <summary>
			/// Formats a string from time values collected by calling <see cref="First"/> and <see cref="Next"/>.
			/// The string contains the number of microseconds of each code execution between calling <b>First</b> and each <b>Next</b>.
			/// </summary>
			public override string ToString() {
				using (new StringBuilder_(out var b)) {
					b.Append("speed:");
					_Results(Math.Min(_counter, _nElem), b, null);
					return b.ToString();
				}
			}
			
			/// <summary>
			/// Return array of time values collected by calling <see cref="First"/> and <see cref="Next"/>.
			/// Each element is the number of microseconds of each code execution between calling <b>First</b> and each <b>Next</b>.
			/// </summary>
			public long[] ToArray() {
				int n = Math.Min(_counter, _nElem);
				var a = new long[n];
				_Results(n, null, a);
				return a;
			}
			
			void _Results(int n, StringBuilder b, long[] a) {
				if (n == 0) return;
				bool average = false; int nMeasurements = 1;
				
				fixed (long* p = _a) fixed (char* c = _aMark) {
					g1:
					double t = 0d, tPrev = 0d;
					for (int i = 0; i < n; i++) {
						t = s_freqMCS * p[i];
						double d = t - tPrev; tPrev = t; //could add 0.5 to round up, but assume that QueryPerformanceCounter call time is 0 - 0.5.
						if (average) d /= nMeasurements;
						long dLong = (long)d;
						if (b != null) {
							b.Append("  ");
							if (c[i] != '\0') b.Append(c[i]).Append('=');
							b.Append(dLong.ToString());
						} else {
							a[i] = dLong;
						}
					}
					if (b == null) return;
					
					if (n > 1) {
						if (average) t /= nMeasurements;
						b.Append("  (").Append((long)t).Append(")");
					}
					
					if (!average && _incremental && (nMeasurements = _nMeasurements) > 1) {
						average = true;
						b.Append(";  measured ").Append(nMeasurements).Append(" times, average");
						goto g1;
					}
				}
			}
			
			/// <summary>
			/// Gets the number of microseconds between <see cref="First"/> and the last <see cref="Next"/>.
			/// </summary>
			public long TimeTotal {
				get {
					int n = _counter;
					if (n == 0) return 0;
					if (n > _nElem) n = _nElem;
					fixed (long* p = _a) { return (long)(s_freqMCS * p[n - 1]); }
				}
			}
		}
		
		/// <summary>
		/// This static variable is used by the static functions.
		/// </summary>
		static Instance s_static;
		//rejected: public. Needed for Serialize. Maybe in the future will be implemented somehow differently.
		
		/// <summary>
		/// Creates and returns new <see cref="Instance"/> variable and calls its <see cref="Instance.First"/>.
		/// </summary>
		public static Instance local() {
			var R = new Instance();
			R.First();
			return R;
		}
		
		/// <summary>
		/// If <c>true</c>, times of each new <b>First</b>/<b>Next</b>/<b>Next</b>... measurement are added to previous measurement times.
		/// Finally you can call <see cref="write"/> or <see cref="toString"/> to get the sums.
		/// Usually used to measure code in loops. See example.
		/// </summary>
		/// <example>
		/// <code><![CDATA[
		/// perf.incremental = true;
		/// for(int i = 0; i < 5; i++) {
		/// 	Thread.Sleep(100); //not included in the measurement
		/// 	perf.first();
		/// 	Thread.Sleep(30); //will make sum ~150000
		/// 	perf.next();
		/// 	Thread.Sleep(10); //will make sum ~50000
		/// 	perf.next();
		/// 	Thread.Sleep(100); //not included in the measurement
		/// }
		/// perf.write(); //speed:  154317  51060  (205377)
		/// perf.incremental = false;
		/// ]]></code>
		/// </example>
		public static bool incremental {
			get => s_static.Incremental;
			set => s_static.Incremental = value;
		}
		
		/// <summary>
		/// Stores current time in the first element of an internal array.
		/// </summary>
		public static void first() => s_static.First();
		
		//rejected. Unclear. Can confuse int timeSpeedUpCPU with char cMark.
		///// <summary>
		///// Calls <see cref="Cpu"/> and <see cref="First()"/>.
		///// </summary>
		//public static void first(int timeSpeedUpCPU) => s_static.First(timeSpeedUpCPU);
		
		/// <summary>
		/// Stores current time in next element of an internal array.
		/// </summary>
		/// <remarks>
		/// Don't call <b>Next</b> more than 16 times after <b>First</b>, because the array size is fixed.
		/// </remarks>
		/// <param name="cMark">A character to mark this time in the results string, like <c>"A=150"</c>.</param>
		public static void next(char cMark = '\0') => s_static.Next(cMark);
		
		/// <summary>
		/// Calls <see cref="next"/> and <see cref="write"/>.
		/// </summary>
		/// <param name="cMark">A character to add to the results string like <c>"A=150"</c>.</param>
		public static void nw(char cMark = '\0') => s_static.NW(cMark);
		
		/// <summary>
		/// Formats a string from time values collected by calling <see cref="first"/> and <see cref="next"/>, and shows it in the output.
		/// The string contains the number of microseconds of each code execution between calling <b>First</b> and each <b>Next</b>.
		/// </summary>
		/// <example>
		/// <code><![CDATA[
		/// perf.first(100);
		/// CODE1;
		/// perf.next();
		/// CODE2;
		/// perf.next();
		/// perf.write(); //speed:  timeOfCODE1  timeOfCODE2  (totalTime)
		/// ]]></code>
		/// </example>
		public static void write() => s_static.Write();
		
		/// <summary>
		/// Formats a string from time values collected by calling <see cref="first"/> and <see cref="next"/>.
		/// The string contains the number of microseconds of each code execution between calling <b>First</b> and each <b>Next</b>.
		/// </summary>
		public static string toString() => s_static.ToString();
		
		/// <summary>
		/// Return array of time values collected by calling <see cref="first"/> and <see cref="next"/>.
		/// Each element is the number of microseconds of each code execution between calling <b>First</b> and each <b>Next</b>.
		/// </summary>
		public static long[] toArray() => s_static.ToArray();
		
		/// <summary>
		/// Gets the number of microseconds between <see cref="first"/> and the last <see cref="next"/>.
		/// </summary>
		public static long timeTotal => s_static.TimeTotal;
		
		/// <summary>
		/// Executes some code in loop for the specified amount of time. It should make CPU to run at full speed.
		/// </summary>
		/// <param name="timeMilliseconds">How long to speed up CPU, milliseconds. The minimal required time probably is about 100 ms, but depends on CPU.</param>
		/// <remarks>
		/// Code speed measurements often are misleading because of variable CPU speed. Most CPU don't run at full speed when not actively used.
		/// 
		/// You can make CPU speed constant in <b>Control Panel > Power Options > ... Advanced > Processor power management > Minimum or maximum power state</b>.
		/// There are programs that show current CPU speed. For example HWMonitor.
		/// </remarks>
		public static void cpu(int timeMilliseconds = 200) {
			int n = 0;
			for (long t0 = mcs; mcs - t0 < timeMilliseconds * 1000L; n++) { }
			//print.it(n);
		}
		
		/// <summary>
		/// Gets a reference to a <see cref="Instance"/> variable in shared memory.
		/// </summary>
		/// <remarks>
		/// The variable can be used by multiple processes, for example to measure process startup time.
		/// Note: slow first time in process, eg 3 ms. It's because need to JIT-compile functions and open shared memory.
		/// </remarks>
		/// <example>
		/// <code><![CDATA[
		/// ref var p = ref perf.shared;
		/// p.First();
		/// //or
		/// perf.shared.First();
		/// ]]></code>
		/// </example>
		public static ref Instance shared => ref SharedMemory_.Ptr->perf;
	}
}
