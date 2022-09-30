namespace SdkConverter;

unsafe partial class Converter {
	enum _KeywordT : byte {
		Any,
		Normal,
		TypeDecl, //enum, struct, union, class, __interface, typedef
		CallConv, //__stdcall etc
		PubPrivProt, //public, private, protected
		IgnoreFuncEtc, //inline, template, operator, static_assert
		
		//Declspec, //__declspec(...) //removed in script
		//Ignore, //volatile etc //removed in script
	}
	
	class _Namespace {
		/// <summary>
		/// Symbols of current namespace. Types, typedef etc. Not C++ keywords/types.
		/// </summary>
		public Dictionary<_Token, _Symbol> sym = new Dictionary<_Token, _Symbol>();
		
		/// <summary>
		/// Formats members and nested types of current namespace. Cleared/reused for each type.
		/// </summary>
		public StringBuilder sb = new StringBuilder();
		
		/// <summary>
		/// Clears sym and sb.
		/// </summary>
		public void Clear() {
			sym.Clear();
			sb.Clear();
		}
	}
	
	class _Symbol {
		public string csTypename;
		public bool forwardDecl;
	}
	
	class _Keyword : _Symbol {
		public _KeywordT kwType;
		public bool cannotStartStatement;
		
		public _Keyword(_KeywordT kwType = _KeywordT.Normal, bool cannotStartStatement = false) {
			this.kwType = kwType;
			this.cannotStartStatement = cannotStartStatement;
		}
	}
	
	class _CppType : _Symbol {
		public byte sizeBytesCpp;
		public bool isUnsigned;
		
		public _CppType(string csTypename, int sizeBytesCpp, bool isUnsigned) {
			this.csTypename = csTypename;
			this.sizeBytesCpp = (byte)sizeBytesCpp;
			this.isUnsigned = isUnsigned;
		}
	}
	
	class _Enum : _Symbol {
		public char[] defAfterName;
		public bool isFlags;
		
		public _Enum(string csTypename, bool forwardDecl) {
			this.csTypename = csTypename;
			this.forwardDecl = forwardDecl;
		}
		
		public static _Enum Copy(_Enum x, string name) {
			var r = new _Enum(name, x.forwardDecl);
			r.defAfterName = x.defAfterName;
			r.isFlags = x.isFlags;
			return r;
		}
	}
	
	class _Struct : _Symbol {
		public bool isInterface, isDualInterface, isClass;
		public char[] attributes, members;
		
		public _Struct(string csTypename, bool forwardDecl) {
			this.csTypename = csTypename;
			this.forwardDecl = forwardDecl;
		}
		
		public static _Struct Copy(_Struct x, string name) {
			var r = new _Struct(name, x.forwardDecl);
			r.isInterface = x.isInterface;
			r.isDualInterface = x.isDualInterface;
			r.attributes = x.attributes;
			r.members = x.members;
			return r;
		}
	}
	
	class _Callback : _Symbol {
		public _Callback(string csTypename) {
			this.csTypename = csTypename;
		}
	}
	
	class _Typedef : _Symbol {
		/// <summary>
		/// pointer (1 if *, 2 if ** and so on)
		/// </summary>
		public byte ptr;
		/// <summary>
		/// Is const.
		/// </summary>
		public bool isConst;
		/// <summary>
		/// Used when swapping tagX with X in 'typedef tagX{...}X'. Then true if tagX was forward-declared. Later used mostly to make conversion faster.
		/// </summary>
		public bool wasForwardDecl;
		/// <summary>
		/// struct etc for which this typedef is alias.
		/// </summary>
		public _Symbol aliasOf;
		
		public _Typedef(_Symbol aliasOf, int ptr, bool isConst, bool wasForwardDecl = false) {
			this.aliasOf = aliasOf;
			this.ptr = (byte)ptr;
			this.isConst = isConst;
			this.wasForwardDecl = wasForwardDecl;
		}
	}
	
	//class _Func
	//{
	
	//}
	
	struct _Token : IEquatable<_Token> {
		public char* s { get; private set; }
		public int len { get; private set; }
		
		public _Token(char* S, int length) {
			s = S;
			len = length;
		}
		
		public bool Equals(_Token t) {
			if (t.len != len) return false;
			for (int i = 0; i < len; i++) if (t.s[i] != s[i]) return false;
			return true;
		}
		
		public bool Equals(string s) {
			if (s.Length != len) return false;
			for (int i = 0; i < len; i++) if (s[i] != this.s[i]) return false;
			return true;
		}
		
		public bool StartsWith(string s) {
			int n = s.Length;
			if (n > len) return false;
			for (int i = 0; i < n; i++) if (s[i] != this.s[i]) return false;
			return true;
		}
		
		public override int GetHashCode() {
			return (int)_HashFnv1(s, len);
		}
		
		public override string ToString() {
			return new string(s, 0, len);
		}
		
		public static implicit operator char*(_Token t) { return t.s; }
		
		//FNV-1 hash.
		//2 times faster than Crc, but less accurate.
		//If length<0, data must be \0-terminated string. Then same speed (does not call Len).
		static uint _HashFnv1(char* data, int length = -1) {
			uint hash = 2166136261;
			
			if (length >= 0) {
				for (int i = 0; i < length; i++)
					hash = (hash * 16777619) ^ data[i];
				//hash = (hash ^ p[i]) * 16777619; //FNV-1a, slower
			} else {
				char ch;
				while ((ch = *data++) != '\0') hash = (hash * 16777619) ^ ch;
			}
			
			return hash;
		}
	}
}
