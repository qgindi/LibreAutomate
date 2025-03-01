// file copied from https://www.libreautomate.com/forum/showthread.php?tid=7741 on 2/23/25

/*/ role miniProgram; define SCRIPT; /*/
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Input;

#if SCRIPT
//Passwords.Save("saved", "test");
//Passwords.Delete("Saved");
//print.it(Passwords.Get("saved"));

Passwords.ShowManagerUI();
#endif

/// <summary>
/// Simple password manager for scripts.
/// </summary>
/// <remarks>
/// In scripts, you often need to use passwords, API keys, and other kinds of credentials. This class helps to protect them. In script you use password names, not passwords directly; passwords are saved encrypted.
/// To encrypt passwords uses a user-provided encryption key (aka master password). Saves the key encrypted using Windows data protection API, which makes it undecryptable on other computers and user accounts.
/// Saves everything in <see cref="Folder"/>.
/// Can be used in portable apps too; more info in <see cref="Folder"/> remarks.
/// <para>
/// Obviously, passwords managed by this class are not completely secure. And less secure than when using a password manager software.
/// To use in scripts conveniently, this class saves encrypted passwords (and key) so you don't have to enter them manually each time. They can be decrypted only on the same computer/account.
/// A determined hacker can steal the passwords if he can access that computer/account and somehow know about this class and find the saved data and this code (or just run this code). It's unlikely, but possible.
/// Also this class does not encrypt passwords in memory, and does not clear the password string memory.
/// But this class gives full protection from leaking passwords through script sharing or github etc.
/// Another benefit: when you change a password, will not need to find and update it in each script that uses the password. Instead you can run this script to show the password manager UI and update the password there.
/// </para>
/// <para>
/// To avoid password/key input dialogs at an inconvenient time, call any <b>Passwords</b> method at a convenient time. Or run this file (it calls <b>Passwords.ShowManagerUI</b>).
/// </para>
/// </remarks>
public static class Passwords {
    #region settings. You can edit this code.
    
    /// <summary>
    /// Full path of the password manager data folder.
    /// Default: <c>folders.ThisAppDataLocal + "Pm"</c>.
    /// </summary>
    /// <remarks>
    /// In portable LA the folder is in its drive. The portable setup tool does not copy/update it, unless the path is changed like <c>folders.ThisAppDataRoaming + "Pm"</c> (the tool copies the roaming folder).
    /// Note: the <b>folders.ThisAppDataLocal/Roaming</b> path is different if script role is <b>editorExtension</b>.
    /// </remarks>
    public static string Folder { get; set; } = folders.ThisAppDataLocal + "Pm";
    
    /// <summary>
    /// Parameter <i>optionalEntropy</i> of <see cref="ProtectedData"/> class functions.
    /// </summary>
    /// <remarks>
    /// Used when encrypting and decrypting the password encryption key. The key cannot be decrypted with different entropy than when encrypting it.
    /// Recommended: replace the default value with a unique value of any length. It adds some more security and separates your passwords from passwords of other apps that use this class and same folder.
    /// The names of password data and key files depend on this value. It allows multiple apps (or scripts) to save their passwords separately. Just change the default value used by your app.
    /// </remarks>
    static byte[] _entropy = [39, 212, 196, 74];
    
    /// <summary>
    /// Edit this if want to customize the password input dialog. For example localize the text.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="password"></param>
    /// <param name="save">true to save the password.</param>
    /// <returns><c>true</c> if OK.</returns>
    static bool _PasswordInputDialog(string name, out string password, out bool save) {
        DControls c = new() { Checkbox = "Save", IsChecked = true };
        bool ok = dialog.showInput(out password, null, "Password for " + name, DEdit.Password, controls: c);
        save = c.IsChecked;
        return ok;
    }
    
    /// <summary>
    /// Edit this if want to customize the key input dialog. For example localize the text.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="newKey">If <c>true</c>, the user can enter a new key (there are no saved passwords). If <c>false</c>, the user must enter the same key.</param>
    /// <returns><c>true</c> if OK.</returns>
    static bool _KeyInputDialog(out string key, bool newKey) {
        string info = newKey
            ? "Please enter a key (any text) that will be used to encrypt saved passwords.\nIt will be saved encrypted for this computer/user (undecryptable elsewhere)."
            : "Please enter the same key that was used to encrypt the saved passwords.";
        return dialog.showInput(out key, "Password manager key", info, DEdit.Password);
    }
    
    /// <summary>
    /// Edit this if want to customize the "Delete all saved passwords?" dialog. For example localize the text.
    /// Called when <b>_KeyInputDialog</b> returns <c>false</c> when <i>newKey</i> <c>false</c>.
    /// </summary>
    /// <returns><c>true</c> to delete.</returns>
    static bool _DeletePasswordsDialog() {
        return 1 == dialog.show("Delete all saved passwords?",
            "Saved passwords cannot be decrypted without the key.\nIf you have lost the key, you can delete saved passwords and set a new key.\nDo you want to delete all saved passwords?",
            "1 Delete|2 Cancel", 0, DIcon.Warning, defaultButton: 2);
    }
    
    #endregion
    
    static string _PasswordsFile => Folder + "\\" + (_filename ??= Hash.MD5(_entropy).ToString()) + ".csv";
    static string _filename;
    
    static string _KeyFile => _PasswordsFile + ".key";
    
    static csvTable _LoadCsv(string file = null) {
        file ??= _PasswordsFile;
        if (!filesystem.exists(file).File) return new() { ColumnCount = 2 };
        var t = csvTable.load(file);
        if (t.ColumnCount < 2) t.ColumnCount = 2;
        return t;
    }
    
    static Dictionary<string, string> _LoadDict() => _LoadCsv().ToDictionary(true, true);
    
    static void _SaveCsv(csvTable t) {
        t.Save(_PasswordsFile);
    }
    
    static void _SaveDict(Dictionary<string, string> d) => _SaveCsv(csvTable.fromDictionary(d));
    
    static byte[] _GetKey() {
        try {
            var data = filesystem.loadBytes(_KeyFile);
            var r1 = ProtectedData.Unprotect(data, _entropy, DataProtectionScope.CurrentUser);
            if (r1.Length == 16) return r1;
        }
        catch { }
        
        return _SetKey();
    }
    
    static bool _KeyInputDialog2(out byte[] key, bool newKey) {
        key = null;
        string s = "";
        while (s.Length < 1) {
            if (!_KeyInputDialog(out s, newKey)) return false;
        }
        
        key = Hash.MD5(s).ToArray();
        return true;
    }
    
    static byte[] _SetKey() {
        var t = _LoadCsv();
        bool newKey = t.RowCount == 0;
        g1:
        if (!_KeyInputDialog2(out var key, newKey)) { //canceled
            if (newKey || !_DeletePasswordsDialog()) throw new OperationCanceledException();
            filesystem.delete(_PasswordsFile);
            filesystem.delete(_KeyFile);
            newKey = true;
            goto g1;
        }
        
        //is data encrypted with this key?
        if (!newKey && t.Rows.All(a => !_Decrypt(a[1], key, out _))) { //if fails to decrypt all passwords, the key is incorrect
            500.ms();
            goto g1;
        }
        
        var p = ProtectedData.Protect(key, _entropy, DataProtectionScope.CurrentUser);
        filesystem.saveBytes(_KeyFile, p);
        return key;
    }
    
    static bool _ChangeKey(ref byte[] key, List<_Item> a) {
        if (!_KeyInputDialog2(out var key2, newKey: true)) return false;
        
        foreach (var v in a) {
            if (_Decrypt(v.EncryptedPassword, key, out string pw))
                v.SetNewPassword(pw, key2);
        }
        
        var p = ProtectedData.Protect(key2, _entropy, DataProtectionScope.CurrentUser);
        filesystem.saveBytes(_KeyFile, p);
        key = key2;
        return true;
    }
    
    static string _Encrypt(string s, byte[] key = null) {
        if (s.NE()) return "";
        return Convert2.AesEncryptS(s, key ?? _GetKey());
    }
    
    static bool _Decrypt(string s, byte[] key, out string r) {
        if (s.NE()) r = "";
        else {
            try { r = Convert2.AesDecryptS(s, key); }
            catch { r = null; return false; }
        }
        return true;
    }
    
    //TODO: in name, replace substring <user> with user SID.
    
    /// <summary>
    /// Encrypts <i>password</i> and saves in the passwords file. Can add or replace.
    /// </summary>
    /// <param name="name">A name for the password. Case-insensitive.</param>
    /// <param name="password"></param>
    /// <exception cref="OperationCanceledException">Key input dialog canceled.</exception>
    /// <exception cref="Exception">Failed to load (if exists) or save the passwords file.</exception>
    public static void Save(string name, string password) {
        _Save(_LoadDict(), name, password);
    }
    
    static void _Save(Dictionary<string, string> d, string name, string password) {
        d[name] = _Encrypt(password);
        _SaveDict(d);
    }
    
    /// <summary>
    /// Deletes one or more passwords from the passwords file.
    /// </summary>
    /// <param name="names">Password names. Case-insensitive.</param>
    /// <exception cref="Exception">Failed to load (if exists) or save the passwords file.</exception>
    public static void Delete(params string[] names) {
        var d = _LoadDict();
        bool deleted = false;
        foreach (var v in names) deleted |= d.Remove(v);
        if (deleted) _SaveDict(d);
    }
    
    /// <summary>
    /// Gets a password from the passwords file and decrypts.
    /// If not found, shows a password input dialog and calls <see cref="Save"/> (optionally).
    /// </summary>
    /// <param name="name">The password's name. Case-insensitive.</param>
    /// <returns>Password.</returns>
    /// <exception cref="OperationCanceledException">Password input dialog canceled. Or key input dialog canceled.</exception>
    /// <exception cref="Exception">Failed to load (if exists) or save the passwords file.</exception>
    public static string Get(string name) {
        var d = _LoadDict();
        if (d.TryGetValue(name, out var s)) {
            var key = _GetKey();
            if (_Decrypt(s, key, out var r)) return r;
        }
        
        if (!_PasswordInputDialog(name, out s, out bool save)) throw new OperationCanceledException();
        if (save) _Save(d, name, s);
        return s;
    }
    
    /// <summary>
    /// Gets all names.
    /// </summary>
    /// <exception cref="Exception">Failed to load (if exists) the passwords file.</exception>
    /// <example>
    /// <code><![CDATA[
    /// //show menu with all password names
    /// var a = Passwords.GetList();
    /// if (a.Any()) {
    ///     var m = new popupMenu("0cfd5f9c-8a23-4534-ad93-5af3ba8c2b41");
    ///     foreach (var v in a) {
    ///         m[v] = o => { print.it(Passwords.Get(v)); };
    ///     }
    ///     m.Show();
    /// }
    /// ]]></code>
    /// </example>
    public static string[] GetList() {
        var d = _LoadDict();
        return d.Keys.ToArray();
    }
    
    /// <summary>
    /// Shows a dialog window with a data grid where you can add, delete and edit names and passwords.
    /// </summary>
    /// <exception cref="OperationCanceledException">Key input dialog canceled.</exception>
    /// <exception cref="Exception">Failed to load (if exists) or save the passwords file.</exception>
    public static void ShowManagerUI(Window owner = null) {
        var b = new wpfBuilder("Passwords").WinSize(500, 500);
        var w = b.Window;
        b.R.Add(out Menu menu).Margin("T-2 B0");
        var g = new DataGrid {
            AutoGenerateColumns = false,
            CanUserAddRows = true,
            VerticalGridLinesBrush = Brushes.LightGray,
            HorizontalGridLinesBrush = Brushes.LightGray,
        };
        b.Row(-1).Add(g);
        b.R.AddOkCancel();
        b.End();
        
        var colName = new DataGridTextColumn {
            Header = "Name",
            Binding = new Binding("Name"),
            Width = new(1, DataGridLengthUnitType.Star),
            CanUserReorder = false
        };
        g.Columns.Add(colName);
        
        var colPw = new DataGridTextColumn {
            Header = "Password",
            Binding = new Binding("Password"),
            Width = new(1, DataGridLengthUnitType.Star),
            CanUserReorder = false
        };
        g.Columns.Add(colPw);
        
        var key = _GetKey(); //OperationCanceledException
        var t = _LoadCsv();
        var a = t.Rows.Select(o => new _Item(o[0], o[1], key)).ToList();
        g.ItemsSource = a;
        
        //clear the displayed password placeholder text when started editing
        g.PreparingCellForEdit += (o, e) => {
            if (e.Column == colPw) {
                var tb = (TextBox)e.EditingElement;
                if (e.EditingEventArgs is not TextCompositionEventArgs) tb.Clear();
                //tb.Foreground = tb.Background; //hide password
            }
        };
        
        //validate name when ending editing
        g.CellEditEnding += (o, e) => {
            if (e.EditAction == DataGridEditAction.Commit) {
                var tb = (TextBox)e.EditingElement;
                var s = tb.Text;
                
                if (s.Trim() is var s2 && s2 != s) {
                    switch (dialog.show(null, "The texts starts or ends with spaces.", "1 Trim spaces|2 Don't trim|3 Cancel", owner: w, defaultButton: 3)) {
                    case 3: _Cancel(); return;
                    case 1: tb.Text = s = s2; break;
                    }
                }
                
                if (e.Column == colName) {
                    var item = e.Row.Item as _Item;
                    if (a.Any(v => v != item && v.Name.Eqi(s))) {
                        dialog.show("Error", $"Name '{s}' already exists.", owner: w);
                        _Cancel();
                    }
                }
                
                void _Cancel() {
                    e.Cancel = true;
                    
                    //workaround for DataGrid bug: on Tab key starts editing next cell. Then 2 cells are in edit mode.
                    EventHandler<DataGridBeginningEditEventArgs> eh1 = (o, e) => { e.Cancel = true; tb.Focus(); };
                    g.BeginningEdit += eh1;
                    timer.after(1, _ => { g.BeginningEdit -= eh1; });
                }
            }
        };
        
        _CreateMenu();
        
        if (owner != null) {
            w.Owner = owner;
            w.ShowInTaskbar = false;
        }
        if (!b.ShowDialog(owner)) return;
        
        _SaveItems();
        
        void _SaveItems() {
            var t = new csvTable { ColumnCount = 2 };
            foreach (var v in a) t.AddRow(v.Name, v.EncryptedPassword);
            _SaveCsv(t);
        }
        
        void _CreateMenu() {
            //File
            var mFile = _TopItem("_Menu");
            _Item(mFile, "Change key...", o => {
                g.CancelEdit();
                if (a.Any(o => o.Password == "<error>")) { //unlikely
                    dialog.showError("Cannot change the key", "Failed to decrypt some passwords (<error>), therefore cannot encrypt them with a new key.\nEdit or delete the <error> passwords.");
                    return;
                }
                if (_ChangeKey(ref key, a))
                    _SaveItems();
            });
            //TODO2: UI help.
            
            MenuItem _Item(ItemsControl parent, string name, Action<MenuItem> click = null, string tooltip = null) {
                var mi = new MenuItem { Header = name, ToolTip = tooltip };
                if (click != null) mi.Click += (sender, _) => click(sender as MenuItem);
                parent.Items.Add(mi);
                return mi;
            }
            
            MenuItem _TopItem(string name) => _Item(menu, name);
            
            //void _Separator(ItemsControl parent) { parent.Items.Add(new Separator()); }
        }
    }
    
    record class _Item {
        string _pwDisplay;
        
        public _Item(string name, string encPw, byte[] key) {
            Name = name;
            EncryptedPassword = encPw;
            if (!encPw.NE()) _pwDisplay = _Decrypt(EncryptedPassword, key, out _) ? "•…" : "<error>";
        }
        public _Item() { }
        
        public string Name { get; set; }
        public string EncryptedPassword;
        
        public string Password {
            get => _pwDisplay;
            set => SetNewPassword(value);
        }
        
        public void SetNewPassword(string s, byte[] key = null) {
            EncryptedPassword = _Encrypt(s, key);
            _pwDisplay = s.NE() ? null : "•…";
        }
    }
}