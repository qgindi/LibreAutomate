namespace Au.More
{
	/// <summary>
	/// Security-related functions, such as enabling privileges.
	/// </summary>
	public static class SecurityUtil
	{
		/// <summary>
		/// Enables or disables a privilege for this process.
		/// </summary>
		/// <returns><c>false</c> if failed. Supports <see cref="lastError"/>.</returns>
		public static bool SetPrivilege(string privilegeName, bool enable, string computer = null) {
			bool ok = false;
			var p = new Api.TOKEN_PRIVILEGES { PrivilegeCount = 1, Privileges = new Api.LUID_AND_ATTRIBUTES { Attributes = enable ? 2u : 0 } }; //SE_PRIVILEGE_ENABLED
			if (Api.LookupPrivilegeValue(computer, privilegeName, out p.Privileges.Luid)) {
				Api.OpenProcessToken(Api.GetCurrentProcess(), Api.TOKEN_ADJUST_PRIVILEGES, out Handle_ hToken);
				Api.AdjustTokenPrivileges(hToken, false, p, 0, null, default);
				ok = 0 == lastError.code;
				hToken.Dispose();
			}
			return ok;
		}
	}
}
