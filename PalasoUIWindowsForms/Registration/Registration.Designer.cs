﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.18052
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Palaso.UI.WindowsForms.Registration {


	[global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
	[global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.Editors.SettingsDesigner.SettingsSingleFileGenerator", "11.0.0.0")]
	public sealed partial class Registration : global::System.Configuration.ApplicationSettingsBase {

		private static Registration defaultInstance = ((Registration)(global::System.Configuration.ApplicationSettingsBase.Synchronized(new Registration())));

		public static Registration Default {
			get {
				return defaultInstance;
			}
		}

		[global::System.Configuration.UserScopedSettingAttribute()]
		[global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
		[global::System.Configuration.DefaultSettingValueAttribute("")]
		public string FirstName {
			get {
				return ((string)(this["FirstName"]));
			}
			set {
				this["FirstName"] = value;
			}
		}

		[global::System.Configuration.UserScopedSettingAttribute()]
		[global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
		[global::System.Configuration.DefaultSettingValueAttribute("")]
		public string SirName {
			get {
				return ((string)(this["SirName"]));
			}
			set {
				this["SirName"] = value;
			}
		}

		[global::System.Configuration.UserScopedSettingAttribute()]
		[global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
		[global::System.Configuration.DefaultSettingValueAttribute("")]
		public string Email {
			get {
				return ((string)(this["Email"]));
			}
			set {
				this["Email"] = value;
			}
		}

		[global::System.Configuration.UserScopedSettingAttribute()]
		[global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
		[global::System.Configuration.DefaultSettingValueAttribute("")]
		public string Organization {
			get {
				return ((string)(this["Organization"]));
			}
			set {
				this["Organization"] = value;
			}
		}

		[global::System.Configuration.UserScopedSettingAttribute()]
		[global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
		[global::System.Configuration.DefaultSettingValueAttribute("")]
		public string HowUsing {
			get {
				return ((string)(this["HowUsing"]));
			}
			set {
				this["HowUsing"] = value;
			}
		}

		[global::System.Configuration.UserScopedSettingAttribute()]
		[global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
		[global::System.Configuration.DefaultSettingValueAttribute("0")]
		public int LaunchCount {
			get {
				return ((int)(this["LaunchCount"]));
			}
			set {
				this["LaunchCount"] = value;
			}
		}

		[global::System.Configuration.UserScopedSettingAttribute()]
		[global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
		[global::System.Configuration.DefaultSettingValueAttribute("True")]
		public bool NeedUpgrade {
			get {
				return ((bool)(this["NeedUpgrade"]));
			}
			set {
				this["NeedUpgrade"] = value;
			}
		}
	}
}
