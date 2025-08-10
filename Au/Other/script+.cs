using System.Text.Json;

namespace Au.Types {
	/// <summary>
	/// <see cref="script.role"/>.
	/// </summary>
	public enum SRole {
		/// <summary>
		/// The task runs as normal <c>.exe</c> program.
		/// It can be started from editor or not. It can run on computers where editor not installed.
		/// </summary>
		ExeProgram,
		
		/// <summary>
		/// The task runs in <c>Au.Task-x64.exe</c> or <c>Au.Task-arm.exe</c> process, started from editor.
		/// </summary>
		MiniProgram,
		
		/// <summary>
		/// The task runs in editor process.
		/// </summary>
		EditorExtension,
	}
	
	/// <summary>
	/// Flags for <see cref="script.setup"/> parameter <i>exception</i>. Defines what to do on unhandled exception.
	/// Default is <c>Print</c>, even if <c>script.setup</c> not called (with default compiler only).
	/// </summary>
	[Flags]
	public enum UExcept {
		/// <summary>
		/// Display exception info in output.
		/// </summary>
		Print = 1,
		
		/// <summary>
		/// Show dialog with exception info.
		/// If editor available, the dialog contains links to functions in the call stack. To close the dialog when a link clicked, add flag <c>Print</c>.
		/// </summary>
		Dialog = 2,
	}
	
	/// <summary>
	/// The default compiler adds this attribute to the assembly.
	/// </summary>
	[AttributeUsage(AttributeTargets.Assembly)]
	public sealed class PathInWorkspaceAttribute : Attribute {
		/// <summary>Path of main source file in workspace, like <c>@"\Script1.cs"</c> or <c>@"\Folder1\Script1.cs"</c>.</summary>
		public readonly string Path;
		
		/// <summary>Full path of main source file.</summary>
		public readonly string FilePath;
		
		///
		public PathInWorkspaceAttribute(string path, string filePath) { Path = path; FilePath = filePath; }
	}
	
	/// <summary>
	/// The default compiler adds this attribute to the main assembly if using non-default references (meta <c>r</c> or <c>nuget</c>). Allows to find them at run time. Only if role <c>miniProgram</c> (default) or <c>editorExtension</c>.
	/// </summary>
	[AttributeUsage(AttributeTargets.Assembly)]
	public sealed class RefPathsAttribute : Attribute {
		/// <summary>Dll paths separated with <c>|</c>.</summary>
		public readonly string Paths;
		
		/// <param name="paths">Dll paths separated with <c>|</c>.</param>
		public RefPathsAttribute(string paths) { Paths = paths; }
	}
	
	/// <summary>
	/// The default compiler adds this attribute to the main assembly if using NuGet packages with native dlls. Allows to find the dlls at run time. Only if role <c>miniProgram</c> (default) or <c>editorExtension</c>.
	/// </summary>
	[AttributeUsage(AttributeTargets.Assembly)]
	public sealed class NativePathsAttribute : Attribute {
		/// <summary>Dll paths separated with <c>|</c>.</summary>
		public readonly string Paths;
		
		/// <param name="paths">Dll paths separated with <c>|</c>.</param>
		public NativePathsAttribute(string paths) { Paths = paths; }
	}
	
	/// <summary>
	/// <see cref="ScriptEditor.GetCommandState"/>.
	/// </summary>
	[Flags]
	public enum ECommandState {
		///
		Checked = 1,
		///
		Disabled = 2,
	}
	
	/// <summary>
	/// For <see cref="ScriptEditor.GetIcon"/>.
	/// </summary>
	public enum EGetIcon {
		/// <summary>
		/// Input is a file or folder in current workspace. Can be relative path in workspace (like <c>@"\Folder\File.cs"</c>) or full path or filename.
		/// Output must be icon name, like <c>"*Pack.Icon color"</c>. See <see cref="ImageUtil.LoadWpfImageElement"/>.
		/// </summary>
		PathToIconName,
		
		/// <summary>
		/// Input is a file or folder in current workspace (see <c>PathToIconName</c>).
		/// Output must be icon XAML.
		/// </summary>
		PathToIconXaml,
		
		/// <summary>
		/// Input is icon name, like <c>"*Pack.Icon color"</c>. See <see cref="ImageUtil.LoadWpfImageElement"/>.
		/// Output must be icon XAML.
		/// </summary>
		IconNameToXaml,
		
		//PathToGdipBitmap,
		//IconNameToGdipBitmap,
	}
	
	/// <summary>
	/// See <see cref="ScriptEditor.GetFileInfo"/>.
	/// </summary>
	/// <param name="name">File name, like <c>"File.cs"</c>.</param>
	/// <param name="path">Path in workspace, like <c>@"\Folder\File.cs"</c>.</param>
	/// <param name="text">File text; null if <i>needText</i> false or if failed to get text. If the file is open in editor, it's the editor text, else it's the saved text.</param>
	/// <param name="kind"> </param>
	/// <param name="id">File id.</param>
	/// <param name="filePath">Full path.</param>
	/// <param name="workspace">Path of the workspace folder.</param>
	public record class EFileInfo(string name, string path, string text, EFileKind kind, uint id, string filePath, string workspace);
	
#pragma warning disable CS1591 //Missing XML comment for publicly visible type or member
	/// <summary>
	/// See <see cref="EFileInfo"/>.
	/// </summary>
	public enum EFileKind { Script, Class, Other }
#pragma warning restore CS1591 //Missing XML comment for publicly visible type or member
}

namespace Au.More {
	/// <summary>
	/// Contains compilation info passed to current <c>preBuild</c>/<c>postBuild</c> script.
	/// </summary>
	/// <param name="outputFile">Full path of the output exe or dll file.</param>
	/// <param name="outputPath">Meta comment <c>outputPath</c>.</param>
	/// <param name="source">Path of this C# code file in the workspace.</param>
	/// <param name="role">Meta comment <c>role</c>.</param>
	/// <param name="optimize">Meta comment <c>optimize</c>.</param>
	/// <param name="platform">Meta comment <c>platform</c>.</param>
	/// <param name="preBuild"><c>true</c> if the script used with meta <c>preBuild</c>, <c>false</c> if with <c>postBuild</c>.</param>
	/// <param name="publish"><c>true</c> when publishing.</param>
	/// <example>
	/// <code><![CDATA[
	/// /*/ role editorExtension; /*/
	/// var c = PrePostBuild.Info;
	/// print.it(c);
	/// print.it(c.outputFile);
	/// ]]></code>
	/// </example>
	public record class PrePostBuild(string outputFile, string outputPath, string source, string role, bool optimize, string platform, bool preBuild, bool publish) {
		/// <summary>
		/// Gets compilation info passed to current <c>preBuild</c>/<c>postBuild</c> script.
		/// </summary>
		public static PrePostBuild Info { get; internal set; }
		
		///
		[Obsolete("Use platform."), EditorBrowsable(EditorBrowsableState.Never)]
		public bool bit32 => platform == "x86";
	}
}
