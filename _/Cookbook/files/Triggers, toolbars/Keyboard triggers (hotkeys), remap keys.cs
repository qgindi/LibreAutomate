/// To execute some code or run a script, can be used keyboard <help Au.Triggers.ActionTriggers>triggers<> (hotkeys). Examples: F9, Ctrl+K, Ctrl+Shift+Alt+H.
/// 
/// To add a trigger can be used snippet triggerSnippet or menu TT -> New trigger. Add triggers in function <b>HotkeyTriggers</b> in file "Hotkey triggers".
///
/// To open a triggers file for editing can be used the TT menu.
/// 
/// Click the Run button to apply changes after editing.
///
/// You'll find hotkey code examples in file "Hotkey triggers".
/// 
/// To disable or remap keys, use code like this. Note: use <b>keys.more.sendKey</b> or <b>keys.sendL</b>, not <b>keys.send</b>.

hk["CapsLock"] = o => {  }; //just disable. Does not disable Ctrl+CapsLock etc.
hk["?+Ins"] = o => keys.more.sendKey(KKey.Apps); //remap key. Also Ctrl+Ins etc.

/// Tips:
/// - To get code for "run script" or "run/open file or URL" you can drag and drop scripts, files and links to the code editor.
/// - To show hotkey info tools: let the text cursor be in the hotkey string. Then press Ctrl+Shift+Space, or invoke the Keys window from the Code menu or toolbar.
/// 
/// See also recipe <+recipe>Triggers and toolbars<>.
///
/// Also triggers can be used in any script. For example in an .exe program that runs without the editor. <help Au.Triggers.ActionTriggers>Examples<>.
