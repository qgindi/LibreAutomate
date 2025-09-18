using Au.Controls;
using System.Windows;
using System.Windows.Controls;
using System.Text.Json;

namespace UnsafeTools;

/// <summary>
/// Invokes methods in editor process from this tool process or main editor process.
/// </summary>
static class ToolToEditor {
	/// <summary>
	/// Inserts one or more statements at current line. With correct position, indent, etc.
	/// If editor is null or readonly or not C# file, prints in output.
	/// Async if called from non-main thread.
	/// </summary>
	public static void InsertStatements(LA.InsertCode.InsertCodeParams p) {
		if (p.s == null) return;
		var json = JsonSerializer.Serialize(p);
		WndCopyData.Send<char>(ScriptEditor.WndMsg_, 17, json);
	}
	
	public static void OpenCookbookRecipe(string name) {
		WndCopyData.Send<char>(ScriptEditor.WndMsg_, 18, name);
	}
	
	//TODO: maybe move other WndCopyData.SendX calls here.
}
