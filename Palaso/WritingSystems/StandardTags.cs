﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace Palaso.WritingSystems
{
	public class StandardTags
	{
		public class IanaSubtag
		{
			public IanaSubtag(string type, string subtag, string description)
			{
				Type = type;
				Subtag = subtag;
				Description = description;
			}

			public string Type { get; private set; }

			public string Subtag { get; private set; }

			public string Description { get; private set; }

			public static int CompareByDescription(IanaSubtag x, IanaSubtag y)
			{
				if (x == null)
				{
					if (y == null)
					{
						return 0;
					}
					else
					{
						return -1;
					}
				}
				else
				{
					if (y == null)
					{
						return 1;
					}
					else
					{
						return x.Description.CompareTo(y.Description);
					}
				}
			}
		}

		static StandardTags()
		{
			ValidIso15924Scripts = new List<Iso15924Script>();
			ValidIso639LanguageCodes = new List<Iso639LanguageCode>();
			ValidIso3166Regions = new List<IanaSubtag>();
			ValidRegisteredVariants = new List<IanaSubtag>();
			LoadIanaSubtags();
		}

		public static List<Iso15924Script> ValidIso15924Scripts { get; private set; }

		public static List<Iso639LanguageCode> ValidIso639LanguageCodes { get; private set; }

		public static List<IanaSubtag> ValidIso3166Regions { get; private set; }

		public static List<IanaSubtag> ValidRegisteredVariants { get; private set; }

		private static void LoadIanaSubtags()
		{
			// To help people find Latin as a script tag
			ValidIso15924Scripts.Add(new Iso15924Script("Roman (Latin)", "Latn"));

			string[] ianaSubtagsAsStrings = Resource.IanaSubtags.Split(new[] { "%%" }, StringSplitOptions.None);
			foreach (string ianaSubtagAsString in ianaSubtagsAsStrings)
			{
				string[] subTagComponents = ianaSubtagAsString.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

				if (subTagComponents[0].Contains("File-Date"))
				{
					continue;   //This is the first line of the file.
				}

				CheckIfIanaSubtagFromFileHasExpectedForm(subTagComponents);

				string type = subTagComponents[0].Split(' ')[1];
				string subtag = subTagComponents[1].Split(' ')[1];
				string description = subTagComponents[2].Split(' ')[1];

				switch (type)
				{
					case "language":
						ValidIso639LanguageCodes.Add(
							new Iso639LanguageCode(subtag, description, String.Empty)
							);
						break;
					case "script":
						ValidIso15924Scripts.Add(
							new Iso15924Script(description, subtag)
							);
						break;
					case "region":
						ValidIso3166Regions.Add(
							new IanaSubtag(type, subtag, description)
							);
						break;
					case "variant":
						ValidRegisteredVariants.Add(
							new IanaSubtag(type, subtag, description)
							);
						break;
				}
			}
			ValidIso639LanguageCodes.Sort(Iso639LanguageCode.CompareByName);
			ValidIso15924Scripts.Sort(Iso15924Script.CompareScriptOptions);
			ValidIso3166Regions.Sort(IanaSubtag.CompareByDescription);
			ValidRegisteredVariants.Sort(IanaSubtag.CompareByDescription);
		}

		private static void CheckIfIanaSubtagFromFileHasExpectedForm(string[] subTagComponents)
		{
			if (!subTagComponents[0].Contains("Type:"))
			{
				throw new ApplicationException(
					String.Format(
						"Unable to parse IANA subtag. First line was '{0}' when it should have denoted the type of subtag.",
						subTagComponents[0]));
			}
			if (!subTagComponents[1].Contains("Subtag:") && !subTagComponents[1].Contains("Tag:"))
			{
				throw new ApplicationException(
					String.Format(
						"Unable to parse IANA subtag. Second line was '{0}' when it should have denoted the subtag code.",
						subTagComponents[1]
						)
					);
			}
			if (!subTagComponents[2].Contains("Description:"))
			{
				throw new ApplicationException(
					String.Format(
						"Unable to parse IANA subtag. Second line was '{0}' when it should have contained a description.",
						subTagComponents[2]));
			}
		}

		public static bool IsValidIso639LanguageCode(string languageCodeToCheck)
		{
			if (languageCodeToCheck.Equals("qaa", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			return ValidIso639LanguageCodes.Any(
				code => languageCodeToCheck.Equals(code.Code, StringComparison.OrdinalIgnoreCase) ||
						languageCodeToCheck.Equals(code.ISO3Code, StringComparison.OrdinalIgnoreCase)
				);
		}

		public static bool IsValidIso15924ScriptCode(string scriptTagToCheck)
		{
			return ValidIso15924Scripts.Any(
				code => scriptTagToCheck.Equals(code.Code, StringComparison.OrdinalIgnoreCase)
				);
		}

		public static bool IsValidIso3166Region(string regionCodeToCheck)
		{
			return ValidIso3166Regions.Any(
				code => regionCodeToCheck.Equals(code.Subtag, StringComparison.OrdinalIgnoreCase)
				);
		}

		public static bool IsValidRegisteredVariant(string variantToCheck)
		{
			return ValidRegisteredVariants.Any(
				code => variantToCheck.Equals(code.Subtag, StringComparison.OrdinalIgnoreCase)
				);
		}
	}
}