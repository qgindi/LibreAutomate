/// In scripts, it's common to use passwords, API keys, and other sensitive credentials. To safeguard them from theft or leaks (via script sharing, GitHub, etc), you can use this <link https://www.libreautomate.com/forum/showthread.php?tid=7741>password manager<>.

/// This code gets a password. Here <.c>"test"<> is not a password; it's a password's name. The password is saved in a file (encrypted).

/*/ c Passwords.cs; /*/

string pw = Passwords.Get("test");
print.it(pw);

/// The first time it shows a password input dialog and saves the password (encrypted). Also shows a key input dialog if a key for this computer/user still not saved.
/// To avoid password/key input dialogs at an inconvenient time, you can at first save passwords at a convenient time:
/// - Use the password manager dialog: run the password manager source file (<.c>Passwords.cs<>) or call <b>Passwords.ShowManagerUI<>.
/// - Or call <b>Passwords.Get<> or <b>Passwords.Save<>.
