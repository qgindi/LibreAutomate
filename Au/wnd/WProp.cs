namespace Au.Types
{
	/// <summary>
	/// Sets, gets, removes and lists window properties using API <msdn>SetProp</msdn> and co.
	/// </summary>
	public struct WProp
	{
		readonly wnd _w;

		internal WProp(wnd w) => _w = w;

		/// <summary>
		/// Gets a window property.
		/// Calls API <msdn>GetProp</msdn> and returns its return value.
		/// </summary>
		/// <param name="name">Property name.</param>
		/// <remarks>Supports <see cref="lastError"/>.</remarks>
		public nint this[string name] => Api.GetProp(_w, name);

		/// <summary>
		/// Gets a window property.
		/// Calls API <msdn>GetProp</msdn> and returns its return value.
		/// </summary>
		/// <param name="atom">Property name atom in the global atom table.</param>
		/// <remarks>
		/// This overload uses atom instead of string. It's about 3 times faster. See API <msdn>GlobalAddAtom</msdn>, <msdn>GlobalDeleteAtom</msdn>.
		/// </remarks>
		public nint this[ushort atom] => Api.GetProp(_w, atom);

		/// <summary>
		/// Sets a window property.
		/// Calls API <msdn>SetProp</msdn> and returns its return value.
		/// </summary>
		/// <param name="name">Property name.</param>
		/// <param name="value">Property value.</param>
		/// <remarks>
		/// Supports <see cref="lastError"/>.
		/// 
		/// Later call <see cref="Remove(string)"/> to remove the property. If you use many unique property names and don't remove the properties, the property name strings can fill the global atom table which is of a fixed size (about 48000) and which is used by all processes for various purposes.
		/// </remarks>
		public bool Set(string name, nint value) {
			return Api.SetProp(_w, name, value);
		}

		/// <summary>
		/// Sets a window property.
		/// Calls API <msdn>SetProp</msdn> and returns its return value.
		/// </summary>
		/// <param name="atom">Property name atom in the global atom table.</param>
		/// <param name="value">Property value.</param>
		/// <remarks>
		/// This overload uses atom instead of string. It's about 3 times faster. See API <msdn>GlobalAddAtom</msdn>, <msdn>GlobalDeleteAtom</msdn>.
		/// </remarks>
		public bool Set(ushort atom, nint value) {
			return Api.SetProp(_w, atom, value);
		}

		/// <summary>
		/// Removes a window property.
		/// Calls API <msdn>RemoveProp</msdn> and returns its return value.
		/// </summary>
		/// <param name="name">Property name. Other overload allows to use global atom instead, which is faster.</param>
		/// <remarks>Supports <see cref="lastError"/>.</remarks>
		public nint Remove(string name) {
			return Api.RemoveProp(_w, name);
		}

		/// <summary>
		/// Removes a window property.
		/// Calls API <msdn>RemoveProp</msdn> and returns its return value.
		/// </summary>
		/// <param name="atom">Property name atom in the global atom table.</param>
		public nint Remove(ushort atom) {
			return Api.RemoveProp(_w, atom);
		}

		/// <summary>
		/// Gets list of window properties.
		/// Uses API <msdn>EnumPropsEx</msdn>.
		/// </summary>
		/// <returns>0-length list if failed. Fails if invalid window or access denied ([](xref:uac)). Supports <see cref="lastError"/>.</returns>
		public Dictionary<string, nint> GetList() {
			var a = new Dictionary<string, nint>();
			Api.EnumPropsEx(_w, (w, name, data, p) => {
				string s;
				if ((long)name < 0x10000) s = "#" + (int)name; else s = Marshal.PtrToStringUni(name);
				a.Add(s, data);
				return true;
			}, default);
			return a;
		}

		/// <summary>
		/// Calls <see cref="GetList"/> and converts to string.
		/// </summary>
		public override string ToString() {
			return string.Join("\r\n", GetList());
		}
	}
}
