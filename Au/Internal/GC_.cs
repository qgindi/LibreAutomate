namespace Au.More {
	/// <summary>
	/// <see cref="GC"/> extensions.
	/// </summary>
	static class GC_ {
		static readonly ConditionalWeakTable<object, _Remover> s_table = new();

		/// <summary>
		/// Calls <see cref="GC.AddMemoryPressure"/>. Later, when object <i>obj</i> is garbage-collected, will call <see cref="GC.RemoveMemoryPressure"/>.
		/// </summary>
		/// <param name="obj">An object of any type.</param>
		/// <param name="size">Unmanaged memory size. It is passed to <b>GC.AddMemoryPressure</b> and <b>GC.RemoveMemoryPressure</b>.</param>
		public static void AddObjectMemoryPressure(object obj, long size) {
			GC.AddMemoryPressure(size);
			s_table.Add(obj, new _Remover(size));
		}

		class _Remover {
			readonly long _size;

			public _Remover(long size) {
				_size = size;
			}

			~_Remover() {
				//print.it("removed " + _size);
				GC.RemoveMemoryPressure(_size);
			}
		}

		/// <summary>
		/// Provides a static <see cref="HandleCollector"/> for USER handles, created with this code: <c>new("Au.User", 300, 3000);</c>.
		/// </summary>
		/// <remarks>
		/// OS refuses to create more USER objects than the process quota. Normally it is 10000, but can be min 200. It is set in registry.
		/// </remarks>
		public static HandleCollector UserHandleCollector { get; } = new("Au.User", 300, 3000);

		/// <summary>
		/// Provides a static <see cref="HandleCollector"/> for GDI handles, created with this code: <c>new("Au.GDI", 300, 3000);</c>.
		/// </summary>
		/// <remarks>
		/// OS refuses to create more GDI objects than the process quota. Normally it is 10000, but can be min 200. It is set in registry.
		/// </remarks>
		public static HandleCollector GdiHandleCollector { get; } = new("Au.GDI", 300, 3000);

		//The above HandleCollectors are used by icon.
		//Tested, works well. But if threshold 100 or 200, GC become too frequent sometimes.
		//Default limit for USER and GDI objects is 10000, min 200.
		//Alternatively could use GC.AddMemoryPressure/GC.RemoveMemoryPressure.
		//	If we add 40000 bytes/icon and GC starts working when pressure is 4 MB, then the number of icons is ~100 and GDI handles ~300.
		//	Problem: the GC threshold for unmanaged memory pressure is unknown, undocumented and changing.
		//		Test results: used to be ~100 KB (why so small?), but now in .NET5 4MB (3MB in 32-bit process).
		//		For managed memory it is 2 MB.
		//		Could instead allocate managed array of this size and keep in a field.
		//			Info: System.Drawing.Icon objects loaded from file or stream keep icon memory in private readonly byte[]? _iconData.
		//				That is why it triggers GC quite soon. But eg Clone doesn't clone that memory and it can be dangerous.
		//Don't care about icon memory size.
	}
}
