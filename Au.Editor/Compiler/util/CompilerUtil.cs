using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Au.Compiler;

static partial class CompilerUtil {
	/// <summary>
	/// Returns true if the dll file is a .NET assembly (any, not only of the .NET library).
	/// </summary>
	public static bool IsNetAssembly(string path) {
		using var pr = new PEReader(File.OpenRead(path));
		return pr.HasMetadata;
	}

	/// <summary>
	/// Returns true if the dll file is a .NET assembly. Sets isRefOnly = true if it's a reference-only assembly.
	/// </summary>
	public static bool IsNetAssembly(string path, out bool isRefOnly) {
		isRefOnly = false;
		using var pr = new PEReader(File.OpenRead(path));
		if (!pr.HasMetadata) return false;
		var mr = pr.GetMetadataReader();

		foreach (var v in mr.GetAssemblyDefinition().GetCustomAttributes()) {
			var ca = mr.GetCustomAttribute(v);
			var h = ca.Constructor;
			if (h.Kind == HandleKind.MemberReference) {
				var m = mr.GetMemberReference((MemberReferenceHandle)h);
				var t = mr.GetTypeReference((TypeReferenceHandle)m.Parent);
				var s = mr.GetString(t.Name);
				if (s == "ReferenceAssemblyAttribute") { isRefOnly = true; break; }
			}
		}

		return true;
	}
}
