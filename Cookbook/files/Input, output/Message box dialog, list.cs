//dialog.show("aaa", "bbb", "Cancel|OK");
//dialog.show("aaa", "bbb", "Cancel|Delete");

//print.it(_DeletePasswordsDialog());

//	static bool _DeletePasswordsDialog() {
//		return 1 == dialog.show("Delete all saved passwords?",
//			"Saved passwords cannot be decrypted without the key.\nIf you have lost the key, you can delete saved passwords and set a new key.\nDo you want to delete all saved passwords?",
//			"2 Cancel|1 Delete", 0, DIcon.Warning);
//	}

int r=dialog.show(null, "The texts starts or ends with spaces.", "3 Cancel|1 Trim spaces|2 Don't trim");
print.it(r);
