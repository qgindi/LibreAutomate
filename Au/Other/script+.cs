using System.Text.Json;

namespace Au.Types {
	/// <summary>
	/// <see cref="script.role"/>.
	/// </summary>
	public enum SRole {
		/// <summary>
		/// The task runs as normal .exe program.
		/// It can be started from editor or not. It can run on computers where editor not installed.
		/// </summary>
		ExeProgram,
		
		/// <summary>
		/// The task runs in Au.Task.exe process, started from editor.
		/// </summary>
		MiniProgram,
		
		/// <summary>
		/// The task runs in editor process.
		/// </summary>
		EditorExtension,
	}
	
	/// <summary>
	/// Flags for <see cref="script.setup"/> parameter <i>exception</i>. Defines what to do on unhandled exception.
	/// Default is <b>Print</b>, even if <b>script.setup</b> not called (with default compiler only).
	/// </summary>
	[Flags]
	public enum UExcept {
		/// <summary>
		/// Display exception info in output.
		/// </summary>
		Print = 1,
		
		/// <summary>
		/// Show dialog with exception info.
		/// If editor available, the dialog contains links to functions in the call stack. To close the dialog when a link clicked, add flag <b>Print</b>.
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
	/// The default compiler adds this attribute to the main assembly if using non-default references (meta r or nuget). Allows to find them at run time. Only if role miniProgram (default) or editorExtension.
	/// </summary>
	[AttributeUsage(AttributeTargets.Assembly)]
	public sealed class RefPathsAttribute : Attribute {
		/// <summary>Dll paths separated with |.</summary>
		public readonly string Paths;
		
		/// <param name="paths">Dll paths separated with |.</param>
		public RefPathsAttribute(string paths) { Paths = paths; }
	}
	
	/// <summary>
	/// The default compiler adds this attribute to the main assembly if using nuget packages with native dlls. Allows to find the dlls at run time. Only if role miniProgram (default) or editorExtension.
	/// </summary>
	[AttributeUsage(AttributeTargets.Assembly)]
	public sealed class NativePathsAttribute : Attribute {
		/// <summary>Dll paths separated with |.</summary>
		public readonly string Paths;
		
		/// <param name="paths">Dll paths separated with |.</param>
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
		/// Input is a file or folder in current workspace (see <b>PathToIconName</b>).
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
}

namespace Au.More {
	/// <summary>
	/// Contains compilation info passed to current <b>preBuild</b>/<b>postBuild</b> script.
	/// </summary>
	/// <param name="outputFile">Full path of the output exe or dll file.</param>
	/// <param name="outputPath">Meta comment <b>outputPath</b>.</param>
	/// <param name="source">Path of this C# code file in the workspace.</param>
	/// <param name="role">Meta comment <b>role</b>.</param>
	/// <param name="optimize">Meta comment <b>optimize</b>.</param>
	/// <param name="bit32">Meta comment <b>bit32</b>.</param>
	/// <param name="preBuild"><c>true</c> if the script used with meta <b>preBuild</b>, <c>false</c> if with <b>postBuild</b>.</param>
	/// <param name="publish"><c>true</c> when publishing.</param>
	/// <example>
	/// <code><![CDATA[
	/// /*/ role editorExtension; /*/
	/// var c = PrePostBuild.Info;
	/// print.it(c);
	/// print.it(c.outputFile);
	/// ]]></code>
	/// </example>
	public record class PrePostBuild(string outputFile, string outputPath, string source, string role, bool optimize, bool bit32, bool preBuild, bool publish) {
		/// <summary>
		/// Gets compilation info passed to current preBuild/postBuild script.
		/// </summary>
		public static PrePostBuild Info { get; internal set; }
	}
}
