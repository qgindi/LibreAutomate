using System.Drawing;
using System.Drawing.Imaging;

namespace Au.More {
	/// <summary>
	/// The default OCR engine. Available on Windows 10 and later.
	/// </summary>
	/// <remarks>
	/// Uses the Windows 10/11 OCR engine. It's the fastest. The accuracy is poor.
	/// If need better accuracy, use a cloud OCR engine (<see cref="OcrGoogleCloud"/>, <see cref="OcrMicrosoftAzure"/>).
	/// If need a non-cloud OCR for older Windows, install Tesseract and use <see cref="OcrTesseract"/>. Also it supports more languages.
	/// </remarks>
	public unsafe class OcrWin10 : IOcrEngine {
		/// <exception cref="NotSupportedException">OS version older than Windows 10.</exception>
		public OcrWin10() {
			if (!osVersion.minWin10) throw new NotSupportedException("This OCR engine is available only on Windows 10 and later. Use another engine.");
		}

		/// <summary>
		/// Language tag, like "en-US". See <see cref="AvailableLanguages"/>. If <c>null</c> (default), uses the default OCR language of this computer.
		/// You can install languages in Windows Settings -> Time and language -> Language and region. Not all languages are supported.
		/// </summary>
		public string Language { get; set; }

		/// <summary>
		/// Gets OCR languages that are installed on this computer and can be used for <see cref="Language"/>.
		/// </summary>
		public (string tag, string displayName)[] AvailableLanguages {
			get {
				using var oes = WinRT.IOcrEngineStatics.CreateStatics();
				using var a1 = oes.AvailableRecognizerLanguages;
				var a2 = new (string, string)[a1.Size];
				for (int i = 0; i < a2.Length; i++) {
					using var lang = a1[i];
					a2[i] = (lang.LanguageTag, lang.DisplayName);
				}
				return a2;
			}
		}

		/// <inheritdoc cref="IOcrEngine.DpiScale"/>
		public bool DpiScale { get; set; } = true;

		/// <inheritdoc cref="IOcrEngine.Recognize"/>
		public OcrWord[] Recognize(Bitmap b, bool dispose, double scale) {
			var b0 = b;
			b = IOcrEngine.PrepareBitmap(b, dispose, scale, 50, 50);
			using var sb = WinRT.ISoftwareBitmap.FromBitmap(b);
			if (dispose || b != b0) b.Dispose();

			using var engine = WinRT.IOcrEngine.CreateEngine(Language);
			using var result = engine.RecognizeAsync(sb).Await<WinRT.IOcrResult>();

			var a = new List<OcrWord>();
			string sep = null;
			using var lines = result.Lines;
			foreach (var line in lines.Items()) {
				using var words = line.Words;
				foreach (var word in words.Items()) {
					a.Add(new(sep, word.Text, word.BoundingRect, scale));
					sep = " ";
				}
				sep = "\r\n";
			}

			return a.ToArray();
		}
	}
}

namespace Au.Types {
#pragma warning disable 649, 169 //field never assigned/used
	static unsafe partial class WinRT {
		internal struct IOcrResult : IComPtr {
			IUnknown _u; public IUnknown U => _u;
			public void Dispose() => _u.Dispose();

			public IVectorView<IOcrLine> Lines => _u.GetPtr<IVectorView<IOcrLine>>(6);

			//public string Text => _u.GetString(8);
		}

		internal struct IOcrLine : IComPtr {
			IUnknown _u; public IUnknown U => _u;
			public void Dispose() => _u.Dispose();

			public IVectorView<IOcrWord> Words => _u.GetPtr<IVectorView<IOcrWord>>(6);

			//public string Text => _u.GetString(7);
		}

		internal struct IOcrWord : IComPtr {
			IUnknown _u; public IUnknown U => _u;
			public void Dispose() => _u.Dispose();

			public RECT BoundingRect {
				get {
					HR(((delegate* unmanaged[Stdcall]<IntPtr, out RectangleF, int>)_u[6])(_u, out var r));
					return RECT.From(r, true);
				}
			}

			public string Text => _u.GetString(7);
		}

		[Guid("5BFFA85A-3384-3540-9940-699120D428A8")]
		internal struct IOcrEngineStatics : IComPtr {
			IUnknown _u; public IUnknown U => _u;
			public void Dispose() => _u.Dispose();

			//public int MaxImageDimension { get; }

			public IVectorView<ILanguage> AvailableRecognizerLanguages => _u.GetPtr<IVectorView<ILanguage>>(7);

			//public bool IsLanguageSupported(ILanguage language);

			public IOcrEngine TryCreateFromLanguage(ILanguage language) {
				HR(((delegate* unmanaged[Stdcall]<IntPtr, ILanguage, out IOcrEngine, int>)_u[9])(_u, language, out var r));
				return r;
			}

			public IOcrEngine TryCreateFromUserProfileLanguages() => _u.GetPtr<IOcrEngine>(10);

			public static IOcrEngineStatics CreateStatics() => Create<IOcrEngineStatics>("Windows.Media.Ocr.OcrEngine");
		}

		internal struct IOcrEngine : IComPtr {
			IUnknown _u; public IUnknown U => _u;
			public void Dispose() => _u.Dispose();

			public IAsyncOperation RecognizeAsync(ISoftwareBitmap bitmap) {
				HR(((delegate* unmanaged[Stdcall]<IntPtr, ISoftwareBitmap, out IAsyncOperation, int>)_u[6])(_u, bitmap, out var r));
				return r;
			}

			//public ILanguage RecognizerLanguage { get; }

			public static IOcrEngine CreateEngine(string language = null) {
				using var oes = IOcrEngineStatics.CreateStatics();
				if (language.NE()) return oes.TryCreateFromUserProfileLanguages();
				using var lf = Create<ILanguageFactory>("Windows.Globalization.Language");
				using var lang = lf.CreateLanguage(language);
				return oes.TryCreateFromLanguage(lang);
			}
		}

		[Guid("DF0385DB-672F-4A9D-806E-C2442F343E86")]
		struct ISoftwareBitmapStatics : IComPtr {
			IUnknown _u; public IUnknown U => _u;
			public void Dispose() => _u.Dispose();

			public ISoftwareBitmap CreateCopyFromBuffer(IBuffer source, int width, int height) {
				HR(((delegate* unmanaged[Stdcall]<IntPtr, IBuffer, int, int, int, out ISoftwareBitmap, int>)_u[9])(_u, source, 87, width, height, out var r)); //Bgra8 = 87
				return r;
			}
		}

		internal struct ISoftwareBitmap : IComPtr {
			IUnknown _u; public IUnknown U => _u;
			public void Dispose() => _u.Dispose();

			public static ISoftwareBitmap FromBitmap(Bitmap b) {
				using var d = b.Data(ImageLockMode.ReadOnly, b.PixelFormat == PixelFormat.Format32bppRgb ? PixelFormat.Format32bppRgb : PixelFormat.Format32bppArgb);
				if (d.Stride < 0) throw new ArgumentException();
				using var cbs = Create<ICryptographicBufferStatics>("Windows.Security.Cryptography.CryptographicBuffer");
				using var sbs = Create<ISoftwareBitmapStatics>("Windows.Graphics.Imaging.SoftwareBitmap");
				using var buffer = cbs.CreateFromByteArray(d.Height * d.Stride, (byte*)d.Scan0);
				return sbs.CreateCopyFromBuffer(buffer, d.Width, d.Height);
			}
		}

		[Guid("320B7E22-3CB0-4CDF-8663-1D28910065EB")]
		struct ICryptographicBufferStatics : IComPtr {
			IUnknown _u; public IUnknown U => _u;
			public void Dispose() => _u.Dispose();

			//the easiest way to create IBuffer which is required to create ISoftwareBitmap
			public IBuffer CreateFromByteArray(int size, byte* value) {
				HR(((delegate* unmanaged[Stdcall]<IntPtr, int, byte*, out IBuffer, int>)_u[9])(_u, size, value, out var r));
				return r;
			}
		}

		struct IBuffer : IComPtr {
			IUnknown _u; public IUnknown U => _u;
			public void Dispose() => _u.Dispose();
		}

		internal struct ILanguage : IComPtr {
			IUnknown _u; public IUnknown U => _u;
			public void Dispose() => _u.Dispose();

			public string LanguageTag => _u.GetString(6);
			public string DisplayName => _u.GetString(7);
			//public string NativeName => _u.GetString(8);
			//public string Script => _u.GetString(9);
		}

		[Guid("9B0252AC-0C27-44F8-B792-9793FB66C63E")]
		internal struct ILanguageFactory : IComPtr {
			IUnknown _u; public IUnknown U => _u;
			public void Dispose() => _u.Dispose();

			public ILanguage CreateLanguage(string languageTag) {
				using var s1 = new _Hstring(languageTag);
				HR(((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, out ILanguage, int>)_u[6])(_u, s1, out var r));
				return r;
			}
		}
	}
}
