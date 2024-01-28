using System.Text.Json;
using System.Text.Json.Nodes;

partial class PanelDebug {
	record class _MiRecord {
		public readonly int token;
		public readonly char kind;
		public readonly string name;
		public readonly JsonObject data;
		
		public _MiRecord(RStr s) {
			if (char.IsAsciiDigit(s[0])) {
				if (!s.ToInt_(out token, out int e)) throw new ArgumentException();
				s = s[e..];
			}
			kind = s[0];
			if (kind is not ('^' or '*' or '+' or '=' or '~' or '@' or '&')) throw new ArgumentException();
			s = s[1..];
			int i = s.IndexOf(',');
			if (i < 0) {
				name = s.ToString();
			} else {
				name = s[..i].ToString();
				s = s[++i..];
				data = _ReadObject(ref s, true, name);
			}
		}
		
		static JsonObject _ReadObject(ref RStr s, bool root, string fieldName) {
			if (!root) s = s[1..];
			var r = new JsonObject();
			for (bool once = false; ;) {
				if (root) { if (s.Length == 0) break; } else if (s[0] == '}') { s = s[1..]; break; }
				if (!once) once = true; else if (s[0] == ',') s = s[1..]; else throw new ArgumentException(s.ToString());
				
				int i = s.IndexOf('=');
				string name = s[..i].ToString().Replace('-', '_');
				s = s[++i..];
				r[name] = _ReadValue(ref s, name);
			}
#if DEBUG
			if (fieldName != null) _PrintType(r, fieldName);
#endif
			return r;
		}
		
		static JsonArray _ReadList(ref RStr s, string fieldName) {
#if DEBUG
			fieldName = fieldName?.TrimEnd('s'); //eg threads -> thread
#endif
			s = s[1..];
			var r = new JsonArray();
			for (bool once = false; ;) {
				if (s[0] == ']') { s = s[1..]; break; }
				if (!once) once = true; else if (s[0] == ',') s = s[1..]; else throw new ArgumentException();
				
				if (s[0] is not ('"' or '{' or '[')) { //name=value
					int i = s.IndexOf('=');
					string name = s[..i].ToString();
					s = s[++i..];
#if DEBUG
					if (fieldName != null) fieldName = name;
#endif
				}
				
				r.Add(_ReadValue(ref s, fieldName));
				fieldName = null; //print type once
			}
			return r;
		}
		
		static JsonNode _ReadValue(ref RStr s, string fieldName) {
			return s[0] switch {
				'"' => _ReadString(ref s),
				'{' => _ReadObject(ref s, false, fieldName),
				'[' => _ReadList(ref s, fieldName),
				_ => throw new ArgumentException()
			};
		}
		
		static string _ReadString(ref RStr s) {
			for (int i = 1; ; i++) {
				i = s.IndexOf(i, '"');
				bool esc = false; for (int j = i; s[--j] == '\\';) esc ^= true;
				if (!esc) {
					var r = s[1..i].ToString().Unescape();
					s = s[++i..];
					return r;
				}
			}
		}
		
#if DEBUG
		static void prt(RStr s) { print.it(s.ToString()); }
#endif
		
		public T Data<T>() {
			return data.Deserialize<T>(s_jso);
		}
		
		static JsonSerializerOptions s_jso = new(JsonSerializerDefaults.General) {
			IncludeFields = true,
			NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
#if DEBUG
			UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow
#endif
		};
		
#if DEBUG
		static void _PrintType(JsonObject x, string fieldName) {
			//var b = new StringBuilder();
			//foreach (var (k, v) in x) {
			//	if (b.Length > 0) b.Append(", "); else b.Append("record ").Append('_').Append(fieldName.Replace('-', '_').Upper()).Append('(');
			//	switch (v.GetValueKind()) {
			//	case JsonValueKind.String:
			//		var s = (string)v;
			//		if (s.ToInt(out int i1, 0, out int e, STIFlags.DontSkipSpaces | STIFlags.NoHex) && e == s.Length) b.Append("int"); else b.Append("string");
			//		break;
			//	case JsonValueKind.Object:
			//		b.Append('_').Append(k.Upper());
			//		break;
			//	case JsonValueKind.Array:
			//		b.Append('_').Append(k.Upper()).Append("[]");
			//		break;
			//	}
			//	b.Append(' ').Append(k);
			//}
			//b.Append(");");
			//print.it($"<><c green>{b.ToString()}<>");
		}
#endif
	}
}
