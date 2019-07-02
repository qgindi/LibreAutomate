using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Reflection;
using Microsoft.Win32;
using System.Runtime.ExceptionServices;
using System.Windows.Forms;
//using System.Drawing;
//using System.Linq;
//using System.Xml.Linq;
using System.Windows.Forms.Design;

using Au;
using Au.Types;
using static Au.AStatic;
using System.Collections;

namespace Au.Controls
{
	/// <summary>
	/// Button without owner-draw style which disables theme animation.
	/// </summary>
	public class AuButton : Button
	{
		public AuButton()
		{
			base.FlatStyle = FlatStyle.System;
		}

		[DefaultValue(FlatStyle.System)]
		public new FlatStyle FlatStyle { get => base.FlatStyle; set => base.FlatStyle = value; }

		[DefaultValue(true)]
		public new bool UseVisualStyleBackColor { get => base.UseVisualStyleBackColor; set => base.UseVisualStyleBackColor = value; }
	}

	/// <summary>
	/// CheckBox without owner-draw style which disables theme animation and draws 1 pixel above normal.
	/// </summary>
	public class AuCheckBox : CheckBox
	{
		public AuCheckBox()
		{
			base.FlatStyle = FlatStyle.System;
		}

		[DefaultValue(FlatStyle.System)]
		public new FlatStyle FlatStyle { get => base.FlatStyle; set => base.FlatStyle = value; }

		[DefaultValue(true)]
		public new bool UseVisualStyleBackColor { get => base.UseVisualStyleBackColor; set => base.UseVisualStyleBackColor = value; }
	}

	/// <summary>
	/// RadioButton without owner-draw style which disables theme animation.
	/// </summary>
	public class AuRadioButton : RadioButton
	{
		public AuRadioButton()
		{
			base.FlatStyle = FlatStyle.System;
		}

		[DefaultValue(FlatStyle.System)]
		public new FlatStyle FlatStyle { get => base.FlatStyle; set => base.FlatStyle = value; }

		[DefaultValue(true)]
		public new bool UseVisualStyleBackColor { get => base.UseVisualStyleBackColor; set => base.UseVisualStyleBackColor = value; }
	}

	//Problem with controls derived from Button: designer sets Text=Name and ignores [DefaultValue] of the Text override.
	//This class sets correct Text, but still need [DefaultValue] to make it non-bold. Cannot set Name.
	class _ButtonDesigner : ControlDesigner
	{
		public override void InitializeNewComponent(IDictionary defaultValues)
		{
			var c = Control as Button;
			string text = c.Text;
			base.InitializeNewComponent(defaultValues);
			c.Text = text;
		}
	}

	/// <summary>
	/// Button that automatically sets its Text = "OK", DialogResult = DialogResult.OK and form's AcceptButton.
	/// </summary>
	[Designer(typeof(_ButtonDesigner))]
	public class AuButtonOK : AuButton
	{
		public AuButtonOK()
		{
			base.DialogResult = DialogResult.OK;
			base.Text = "&OK";
			//base.Name = "_bOK"; //does not work in any way
		}

		[DefaultValue(DialogResult.OK)]
		public override DialogResult DialogResult { get => base.DialogResult; set => base.DialogResult = value; }

		[DefaultValue("&OK")]
		public override string Text { get => base.Text; set => base.Text = value; }

		protected override void OnHandleCreated(EventArgs e)
		{
			var f = FindForm();
			if(f != null) f.AcceptButton = this;
			base.OnHandleCreated(e);
		}

		protected override void OnClick(EventArgs e)
		{
			base.OnClick(e);
			var f = FindForm();
			if(!f.Modal && f.DialogResult == DialogResult.OK) f.Close();
		}
	}

	/// <summary>
	/// Button that automatically sets its Text = "Cancel", DialogResult = DialogResult.Cancel and form's CancelButton.
	/// </summary>
	[Designer(typeof(_ButtonDesigner))]
	public class AuButtonCancel : AuButton
	{
		public AuButtonCancel()
		{
			base.DialogResult = DialogResult.Cancel;
			base.Text = "Cancel";
			//base.Name = "_bCancel";
			base.CausesValidation = false;
		}

		[DefaultValue(DialogResult.Cancel)]
		public override DialogResult DialogResult { get => base.DialogResult; set => base.DialogResult = value; }

		[DefaultValue("Cancel")]
		public override string Text { get => base.Text; set => base.Text = value; }

		[DefaultValue(false)]
		public new bool CausesValidation { get => base.CausesValidation; set => base.CausesValidation = value; }

		protected override void OnHandleCreated(EventArgs e)
		{
			var f = FindForm();
			if(f != null) f.CancelButton = this;
			base.OnHandleCreated(e);
		}

		protected override void OnClick(EventArgs e)
		{
			base.OnClick(e);
			var f = FindForm();
			if(!f.Modal && f.DialogResult == DialogResult.Cancel) f.Close();
		}
	}

	/// <summary>
	/// Makes Padding.Top = 2, to align text with adjacent TextBox and other controls.
	/// </summary>
	public class AuLabel : Label
	{
		public AuLabel()
		{
			base.Padding = new Padding(0, 2, 0, 0);
		}

		public new Padding Padding { get => base.Padding; set => base.Padding = value; }

		bool ShouldSerializePadding() { return base.Padding != new Padding(0, 2, 0, 0); }
	}
}