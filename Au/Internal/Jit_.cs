namespace Au.More;

/// <summary>
/// JIT-compiles methods.
/// </summary>
static class Jit_ {
	const BindingFlags c_bindingFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
	
	/// <summary>
	/// JIT-compiles method.
	/// Uses <b>RuntimeHelpers.PrepareMethod</b>.
	/// </summary>
	/// <param name="type">Type containing the method.</param>
	/// <param name="method">Method name.</param>
	/// <exception cref="ArgumentException">Method does not exist.</exception>
	/// <exception cref="AmbiguousMatchException">Multiple overloads exist.</exception>
	public static void Compile(Type type, string method) {
		var m = type.GetMethod(method, c_bindingFlags);
		if (m == null) throw new ArgumentException($"Method {type.Name}.{method} does not exist.");
		RuntimeHelpers.PrepareMethod(m.MethodHandle);
		//tested: maybe MethodHandle.GetFunctionPointer can be used to detect whether the method is jited and assembly ngened.
		//	Call GetFunctionPointer before and after PrepareMethod. If was not jited, the second call returns a different value.
		//	Undocumented, therefore unreliable.
	}
	
	//rejected. Don't JIT-compile overloads.
	///// <summary>
	///// JIT-compiles a method overload.
	///// Uses <b>RuntimeHelpers.PrepareMethod</b>.
	///// </summary>
	///// <param name="type">Type containing the method.</param>
	///// <param name="method">Method name.</param>
	///// <param name="paramTypes">Types of parameters of this overload.</param>
	///// <exception cref="ArgumentException">Method does not exist.</exception>
	///// <exception cref="AmbiguousMatchException">Multiple overloads exist that match <i>paramTypes</i>.</exception>
	//public static void Compile(Type type, string method, params Type[] paramTypes)
	//{
	//	var m = type.GetMethod(method, c_bindingFlags, null, paramTypes, null);
	//	if(m == null) throw new ArgumentException($"Method {type.Name}.{method} does not exist.");
	//	RuntimeHelpers.PrepareMethod(m.MethodHandle);
	//	//tested: MethodHandle.GetFunctionPointer cannot be used to detect whether the method is jited.
	//	//	Tried to find a faster way to detect whether the assembly is ngened.
	//}
	
	/// <summary>
	/// JIT-compiles multiple methods of same type.
	/// Uses <b>RuntimeHelpers.PrepareMethod</b>.
	/// </summary>
	/// <param name="type">Type containing the methods.</param>
	/// <param name="methods">Method names.</param>
	/// <exception cref="ArgumentException">Method does not exist.</exception>
	public static void Compile(Type type, params string[] methods) {
		foreach (var v in methods) Compile(type, v);
	}
}
