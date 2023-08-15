/// In scripts can be used <google>Windows API</google> (functions, structs, constants, COM interfaces, etc). They must be declared somewhere in the script or in class files it uses.
///
/// The editor program has a database containing many declarations. There are several ways to get declarations from it.
/// - Menu Code -> Windows API. For more info, click the [?] button in the dialog.
/// - Undeclared API names in code are red-underlined. Click "Find Windows API..." in the error tooltip.
/// - Usually this is the best way. In code (at the end) type <mono>nat<> and select nativeApiSnippet. It adds class <b>api</b>. Then, wherever you want to use an API function etc, type <mono>api.<> and select it from the list; the declaration will be added to the <b>api</b> class.

api.MessageBox(default, "Text", "Caption", api.MB_TOPMOST);

#pragma warning disable 649, 169 //field never assigned/used
unsafe class api : NativeApi {
[DllImport("user32.dll", EntryPoint="MessageBoxW")]
internal static extern int MessageBox(wnd hWnd, string lpText, string lpCaption, uint uType);

internal const uint MB_TOPMOST = 0x40000;
}
#pragma warning restore 649, 169 //field never assigned/used

/// The declarations in the database are not perfect. Often need to edit them.

/// You can find, downloaded and use other native (aka unmanaged) libraries too, but will need to write declarations of methods/types/etc manually (or find somewhere). Better try to find a .NET library that wraps the native library. Note: use 64-bit dlls; or in script Properties select role exeProgram and check bit32.
