namespace Au.More;

/// <summary>
/// Binary-serializes and deserializes multiple values of types <b>int</b>, <b>string</b>, <b>string[]</b>, <b>byte[]</b> and <c>null</c>.
/// Used mostly for sending parameters for IPC through pipe etc.
/// Similar to <b>BinaryWriter</b>, but faster and less garbage. Much faster than <b>BinaryFormatter</b>, CSV, etc.
/// Serializes all values into a <b>byte[]</b> in single call. If need to append, use <b>BinaryWriter</b> instead.
/// </summary>
internal static unsafe class Serializer_ {
	/// <summary>
	/// Type of input and output values of <see cref="Serializer_"/> functions.
	/// Has implicit conversions from/to <b>int</b> and <b>string</b>.
	/// </summary>
	public struct Value {
		public object Obj;
		public int Int;
		public VType Type;
		
		Value(int i) { Obj = null; Int = i; Type = VType.Int; }
		Value(object o, VType type) { Obj = o; Int = 0; Type = o != null ? type : VType.Null; }
		
		public static implicit operator Value(int i) => new Value(i);
		public static implicit operator Value(string s) => new Value(s, VType.String);
		public static implicit operator Value(string[] a) => new Value(a, VType.StringArray);
		public static implicit operator Value(byte[] a) => new Value(a, VType.ByteArray);
		
		public static implicit operator int(Value a) => a.Int;
		public static implicit operator string(Value a) => a.Obj as string;
		public static implicit operator string[](Value a) => a.Obj as string[];
		public static implicit operator byte[](Value a) => a.Obj as byte[];
	}
	
	public enum VType { Null, Int, String, StringArray, ByteArray }
	
	/// <summary>
	/// Serializes multiple values of types <b>int</b>, <b>string</b>, <b>string[]</b> and <c>null</c>.
	/// The returned array can be passed to <see cref="Deserialize"/>.
	/// </summary>
	public static byte[] Serialize(params Value[] a) => _Serialize(false, a);
	
	/// <summary>
	/// Serializes multiple values of types <b>int</b>, <b>string</b>, <b>string[]</b> and <c>null</c>.
	/// Unlike <see cref="Serialize"/>, in the first 4 bytes writes the size of data that follows.
	/// Can be used with pipes or other streams where data size is initially unknown: read 4 bytes as <c>int dataSize</c>; <c>var b=new byte[dataSize]</c>, read it, pass <c>b</c> to <see cref="Deserialize"/>. 
	/// </summary>
	public static byte[] SerializeWithSize(params Value[] a) => _Serialize(true, a);
	
	static byte[] _Serialize(bool withSize, Value[] a) {
		int size = 4;
		if (withSize) size += 4;
		for (int i = 0; i < a.Length; i++) {
			size++;
			switch (a[i].Type) {
			case VType.Int: size += 4; break;
			case VType.String: size += 4 + (a[i].Obj as string).Length * 2; break;
			case VType.StringArray:
				int z = 4;
				foreach (var v in a[i].Obj as string[]) z += 4 + v.Lenn() * 2;
				size += z;
				break;
			case VType.ByteArray: size += 4 + (a[i].Obj as byte[]).Length; break;
			}
		}
		var ab = new byte[size];
		fixed (byte* b0 = ab) {
			byte* b = b0;
			if (withSize) { *(int*)b = ab.Length - 4; b += 4; }
			*(int*)b = a.Length; b += 4;
			for (int i = 0; i < a.Length; i++) {
				var ty = a[i].Type;
				*b++ = (byte)ty;
				switch (ty) {
				case VType.Int:
					*(int*)b = a[i].Int;
					b += 4;
					break;
				case VType.String:
					var s = a[i].Obj as string;
					_AddString(s);
					break;
				case VType.StringArray:
					var k = a[i].Obj as string[];
					*(int*)b = k.Length; b += 4;
					foreach (var v in k) {
						if (v != null) _AddString(v);
						else { *(int*)b = -1; b += 4; }
					}
					break;
				case VType.ByteArray:
					var u = a[i].Obj as byte[];
					*(int*)b = u.Length; b += 4;
					u.CopyTo(ab, b - b0);
					b += u.Length;
					break;
				}
			}
			Debug.Assert((b - b0) == size);
			
			void _AddString(string s) {
				*(int*)b = s.Length; b += 4;
				var c = (char*)b;
				for (int j = 0; j < s.Length; j++) c[j] = s[j];
				b += s.Length * 2;
			}
		}
		return ab;
	}
	
	/// <summary>
	/// Deserializes values serialized by <see cref="Serialize"/>.
	/// Returns array of values passed to <b>Serialize</b>.
	/// </summary>
	public static Value[] Deserialize(RByte serialized) {
		fixed (byte* b0 = serialized) {
			byte* b = b0;
			int n = *(int*)b; b += 4;
			var a = new Value[n];
			for (int i = 0; i < n; i++) {
				switch ((VType)(*b++)) {
				case VType.Null:
					break;
				case VType.Int:
					a[i] = *(int*)b; b += 4;
					break;
				case VType.String:
					a[i] = _GetString();
					break;
				case VType.StringArray:
					var k = new string[*(int*)b]; b += 4;
					for (int j = 0; j < k.Length; j++) k[j] = _GetString();
					a[i] = k;
					break;
				case VType.ByteArray:
					int len = *(int*)b; b += 4;
					a[i] = serialized.Slice((int)(b - b0), len).ToArray();
					b += len;
					break;
				default: throw new ArgumentException();
				}
			}
			return a;
			
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			string _GetString() {
				int len = *(int*)b; b += 4;
				if (len == -1) return null;
				var R = new string((char*)b, 0, len);
				b += len * 2;
				return R;
			}
		}
	}
}
