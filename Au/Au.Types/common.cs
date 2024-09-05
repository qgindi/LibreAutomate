using System.Diagnostics.CodeAnalysis;
using static Au.More.Serializer_;

namespace Au.Types {
	/// <summary>
	/// In DocFX-generated help files removes documentation and auto-generated links in TOC and class pages.
	/// </summary>
	[EditorBrowsable(EditorBrowsableState.Never), AttributeUsage(AttributeTargets.All)]
	public sealed class NoDoc : Attribute { }

	/// <summary>
	/// If a class is derived from this class, editor adds undeclared Windows API to its completion list.
	/// </summary>
	public abstract class NativeApi {
		//Or for it could use an attribute. But this base class easily solves 2 problems:
		//	1. In 'new' expression does not show completion list (with types from winapi DB) if the winapi class still does not have types inside. Because the completion service then returns null. Now it is solved, because this class has nested types.
		//	2. If class with attributes is after top-level statements, code info often does not work when typing directly above it. Works better if without attributes.
		//FUTURE: Also add attribute. Then can specify an alternative DB or text file with declarations. Maybe also some settings.
		//	But use this class as base too, like now. Eg could add protected util functions. Could use this class as both (base and attribute), but Attribute has static members.

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
		/// <summary>
		/// Can be used in structures as flexible array member (the last field, defined like <c>Type[1] name;</c> in C).
		/// </summary>
		public struct FlexibleArray<T> where T : unmanaged {
			T _0;

			public ref T this[int index] {
				[UnscopedRef]
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => ref Unsafe.Add(ref _0, index);
			}

			[UnscopedRef]
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public Span<T> AsSpan(int length) {
				return MemoryMarshal.CreateSpan(ref _0, length);
			}
		}

		/// <summary>
		/// Windows API <b>BOOL</b>, with implicit conversions to/from C# <c>bool</c>.
		/// </summary>
		public readonly record struct BOOL(bool b) {
			public readonly int IntValue = b ? 1 : 0;

			public static implicit operator bool(BOOL b) => b.IntValue != 0;
			public static implicit operator BOOL(bool b) => new(b);
			public override string ToString() => IntValue == 0 ? "False" : "True";
		}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
	}

	/// <summary>
	/// Invokes specified action (calls callback function) at the end of <c>using(...) { ... }</c>.
	/// Usually returned by functions. Example: <see cref="opt.scope.mouse"/>.
	/// </summary>
	public struct UsingEndAction : IDisposable {
		readonly Action _a;

		/// <summary>Sets action to be invoked when disposing this variable.</summary>
		public UsingEndAction(Action a) => _a = a;

		/// <summary>Invokes the action if not <c>null</c>.</summary>
		public void Dispose() => _a?.Invoke();
	}

	/// <summary>
	/// Used with <see cref="ParamStringAttribute"/> to specify string parameter format.
	/// </summary>
	public enum PSFormat {
		///
		None,

		/// <summary>
		/// Keys. See <see cref="keys.send(KKeysEtc[])"/>.
		/// </summary>
		Keys,

		/// <summary>
		/// [Wildcard expression](xref:wildcard_expression).
		/// </summary>
		Wildex,

		/// <summary>
		/// PCRE regular expression.
		/// </summary>
		Regexp,

		/// <summary>
		/// PCRE regular expression replacement string.
		/// </summary>
		RegexpReplacement,

		/// <summary>
		/// .NET regular expression.
		/// </summary>
		NetRegex,

		/// <summary>
		/// Hotkey, except triggers.
		/// </summary>
		Hotkey,

		/// <summary>
		/// Hotkey trigger.
		/// </summary>
		HotkeyTrigger,

		/// <summary>
		/// Trigger modifiers without key.
		/// </summary>
		TriggerMod,

		/// <summary>
		/// Name or path of a script or class file in current workspace.
		/// </summary>
		CodeFile,

		/// <summary>
		/// Name or path of any file or folder in current workspace.
		/// </summary>
		FileInWorkspace,
	}

	/// <summary>
	/// A function parameter with this attribute is a string of the specified format, for example regular expression.
	/// Code editors should help to create correct string arguments: provide tools or reference, show errors.
	/// </summary>
	[AttributeUsage(AttributeTargets.Parameter /*| AttributeTargets.Field | AttributeTargets.Property*/, AllowMultiple = false)]
	public sealed class ParamStringAttribute : Attribute {
		//info: now .NET has similar attribute StringSyntaxAttribute. It was added later.

		///
		public ParamStringAttribute(PSFormat format) => Format = format;

		///
		public PSFormat Format { get; set; }
	}

	///// <summary>
	///// Specifies whether to set, add or remove flags.
	///// </summary>
	//public enum SetAddRemove
	//{
	//	/// <summary>Set flags = the specified value.</summary>
	//	Set = 0,
	//	/// <summary>Add the specified flags, don't change others.</summary>
	//	Add = 1,
	//	/// <summary>Remove the specified flags, don't change others.</summary>
	//	Remove = 2,
	//	/// <summary>Toggle the specified flags, don't change others.</summary>
	//	Xor = 3,
	//}
}

namespace System.Runtime.CompilerServices //the attribute must be in this namespace
{
	///
	[NoDoc]
	[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
	public class IgnoresAccessChecksToAttribute : Attribute {
		///
		public IgnoresAccessChecksToAttribute(string assemblyName) { AssemblyName = assemblyName; }
		///
		public string AssemblyName { get; }
	}
}

//rejected. Better use snippets. Also, users can create own classes for it and put whatever there.
//namespace Au
//{
//	/// <summary>
//	/// Aliases of some frequently used functions of various classes.
//	/// To call them without <c>classname.</c>, you need <c>using static Au.func;</c>.
//	/// </summary>
//	public static class func
//	{
//		///// <summary><inheritdoc cref="print.it(string)"/></summary>
//		//public static void print(string s) => Au.print.it(s);
//		//cannot use function name = class name. Then cannot call other members of that class.

//		/// <summary><inheritdoc cref="print.it(string)"/></summary>
//		public static void write(string s) => print.it(s);

//		/// <summary><inheritdoc cref="keys.send(KKeysEtc[])"/></summary>
//		public static void key([ParamString(PSFormat.keys)] params KKeysEtc[] keysEtc) => keys.send(keysEtc);

//		/// <summary><inheritdoc cref="keys.sendt(string, string)"/></summary>
//		public static void keyt(string text, string html = null) => keys.sendt(text, html);
//	}
//}
