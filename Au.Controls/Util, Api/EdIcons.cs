
public static class EdIcons {
	public const string
		Script = "*Material.ScriptOutline #73BF00|#87E100",
		//Script = "*Material.Square white %4,1,4,1,f;*Material.ScriptOutline #73BF00|#87E100", //white-filled. In some places looks not good.
		Class = "*Codicons.SymbolClass #4080FF|#84ACFF",
		Folder = "*Material.Folder" + darkYellow,
		FolderOpen = "*Material.FolderOpen" + darkYellow,
		Back = "*EvaIcons.ArrowBack" + black,
		Trigger = "*Codicons.SymbolEvent" + blue,
		Icons = "*FontAwesome.IconsSolid" + blue,
		Undo = "*Ionicons.UndoiOS" + brown,
		Paste = "*Material.ContentPaste" + brown,
		References = "*Material.MapMarkerMultiple" + blue,
		Regex = "*FileIcons.Regex @12" + blue
		;
	
	public const string
		black = " #505050|#EEEEEE",
		blue = " #4080FF|#99CCFF",
		//darkBlue = " #5060FF|#7080FF",
		//lightBlue = " #B0C0FF|#D0E0FF",
		green = " #99BF00|#A7D000",
		green2 = " #40B000|#4FD200",
		brown = " #9F5300|#EEEEEE",
		purple = " #A040FF|#D595FF",
		darkYellow = " #EABB00",
		orange = " #FFA500",
		red = " #FF4040|#FF9595"
		;
	
	public static string FolderIcon(bool open) => open ? FolderOpen : Folder;
	
	public static string FolderArrow(bool open) => open ? "*Material.ChevronDown @9 #404040" : "*Material.ChevronRight @9 #404040";
}
