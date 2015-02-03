using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;

namespace SIL.WritingSystems
{
	/// <summary>
	/// This class parses the IANA subtag registry in order to provide a list of valid language, script, region and variant subtags
	/// for use by the Rfc5646Tag and other classes.
	/// </summary>
	public class StandardSubtags
	{
		private class SubtagCollection<T> : KeyedCollection<string, T> where T : Subtag
		{
			public SubtagCollection(IEnumerable<T> items)
				: base(StringComparer.InvariantCultureIgnoreCase)
			{
				foreach (T item in items)
					Add(item);
			}

			protected override string GetKeyForItem(T item)
			{
				return item.Code;
			}
		}

		static StandardSubtags()
		{
			// JohnT: can't find anywhere else to document this, so here goes: TwoToThreeMap is a file adapted from
			// FieldWorks Ethnologue\Data\iso-639-3_20080804.tab, by discarding all but the first column (3-letter
			// ethnologue codes) and the fourth (two-letter IANA codes), and all the rows where the fourth column is empty.
			// I then swapped the columns. So, in this resource, the string before the tab in each line is a 2-letter
			// Iana code, and the string after it is the one we want to return as the corresponding ISO3Code.
			// The following block of code assembles these lines into a map we can use to fill this slot properly
			// when building the main table.
			var twoToThreeMap = new Dictionary<string, string>();
			string[] encodingPairs = LanguageRegistryResources.TwoToThreeCodes.Replace("\r\n", "\n").Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
			foreach (string pair in encodingPairs)
			{
				var items = pair.Split('\t');
				if (items.Length != 2)
					continue;
				twoToThreeMap[items[0]] = items[1];
			}

			var languages = new List<LanguageSubtag>();
			var scripts = new List<ScriptSubtag>();
			var regions = new List<RegionSubtag>();
			var variants = new List<VariantSubtag>();
			string[] ianaSubtagsAsStrings = LanguageRegistryResources.ianaSubtagRegistry.Split(new[] { "%%" }, StringSplitOptions.None);
			foreach (string ianaSubtagAsString in ianaSubtagsAsStrings)
			{
				string[] subTagComponents = ianaSubtagAsString.Replace("\r\n", "\n").Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

				if (subTagComponents[0].Contains("File-Date"))
				{
					continue;   //This is the first line of the file.
				}

				CheckIfIanaSubtagFromFileHasExpectedForm(subTagComponents);

				string type = subTagComponents[0].Split(' ')[1];
				string subtag = subTagComponents[1].Split(' ')[1];
				string description = SubTagComponentDescription(subTagComponents[2]);

				if (subtag.Contains("..")) // do not add private use subtags to the list
				{
					continue;
				}

				/* Note: currently we are only using the first "Description:" line in each entry.
				 * A few script entries contain multiple Description: lines, as in the example below:
				 *
				 * Type: script
				 * Subtag: Deva
				 * Description: Devanagari
				 * Description: Nagari
				 * Added: 2005-10-16
				 *
				 * In the future it may be necessary to build a separate iana script entry collection
				 * that contains duplicate script codes, for the purposes of including all possible
				 * script Descriptions.
				 */
				switch (type)
				{
					case "language":
						string iso3Code;
						if (!twoToThreeMap.TryGetValue(subtag, out iso3Code))
							iso3Code = String.Empty;
						languages.Add(new LanguageSubtag(subtag, description, false, iso3Code));
						break;
					case "script":
						scripts.Add(new ScriptSubtag(subtag, description, false));
						break;
					case "region":
						regions.Add(new RegionSubtag(subtag, description, false));
						break;
					case "variant":
						variants.Add(new VariantSubtag(subtag, description, false, GetVariantPrefixes(subTagComponents)));
						break;
				}
			}

			// These ones are considered non-private in that the user can't edit the code, but they already contain needed X's.
			//variants.Add(new VariantSubtag("fonipa-x-etic", "Phonetic", false, null));
			//variants.Add(new VariantSubtag("fonipa-x-emic", "Phonemic", false, null));
			//variants.Add(new VariantSubtag("x-py", "Pinyin", false, null));
			//variants.Add(new VariantSubtag("x-pyn", "Pinyin Numbered", false, null));
			//variants.Add(new VariantSubtag("x-audio", "Audio", false, null));

			Iso639Languages = new ReadOnlyKeyedCollection<string, LanguageSubtag>(new SubtagCollection<LanguageSubtag>(languages.OrderBy(l => Regex.Replace(l.Name, @"[^\w]", ""))
				.Concat(new[] {new LanguageSubtag(WellKnownSubtags.UnlistedLanguage, "Language Not Listed", false, string.Empty)})));
			Iso15924Scripts = new ReadOnlyKeyedCollection<string, ScriptSubtag>(new SubtagCollection<ScriptSubtag>(scripts.OrderBy(s => s.Name)));
			Iso3166Regions = new ReadOnlyKeyedCollection<string, RegionSubtag>(new SubtagCollection<RegionSubtag>(regions.OrderBy(r => r.Name)));
			RegisteredVariants = new ReadOnlyKeyedCollection<string, VariantSubtag>(new SubtagCollection<VariantSubtag>(variants.OrderBy(v => v.Name)));
			CommonPrivateUseVariants = new ReadOnlyKeyedCollection<string, VariantSubtag>(new SubtagCollection<VariantSubtag>(new[]
			{
				new VariantSubtag(WellKnownSubtags.IpaPhoneticPrivateUse, "Phonetic", true, null),
				new VariantSubtag(WellKnownSubtags.IpaPhonemicPrivateUse, "Phonemic", true, null),
				new VariantSubtag(WellKnownSubtags.AudioPrivateUse, "Audio", true, null)
			}));
		}

		public static ReadOnlyKeyedCollection<string, ScriptSubtag> Iso15924Scripts { get; private set; }

		public static ReadOnlyKeyedCollection<string, LanguageSubtag> Iso639Languages { get; private set; }

		public static ReadOnlyKeyedCollection<string, RegionSubtag> Iso3166Regions { get; private set; }

		public static ReadOnlyKeyedCollection<string, VariantSubtag> RegisteredVariants { get; private set; }

		public static ReadOnlyKeyedCollection<string, VariantSubtag> CommonPrivateUseVariants { get; private set; }

		private static IEnumerable<string> GetVariantPrefixes(string[] subTagComponents)
		{
			foreach (var line in subTagComponents)
			{
				if (line.StartsWith("Prefix: "))
					yield return line.Substring("Prefix: ".Length).Trim();
			}
		}

		internal static string SubTagComponentDescription(string component)
		{
			string description = component.Substring(component.IndexOf(" ", StringComparison.Ordinal) + 1);
			description = Regex.Replace(description, @"\(alias for ", "(");
			if (description[0] == '(')
			{
				// remove parens if the description begins with an open parenthesis
				description = Regex.Replace(description, @"[\(\)]", "");
			}
			description = Regex.Replace(description, @"/", "|");
			return description;
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
			return Iso639Languages.Contains(languageCodeToCheck);
		}

		public static bool IsValidIso15924ScriptCode(string scriptTagToCheck)
		{
			return Iso15924Scripts.Contains(scriptTagToCheck) || IsPrivateUseScriptCode(scriptTagToCheck);
		}

		public static bool IsPrivateUseScriptCode(string scriptCode)
		{
			var scriptCodeU = scriptCode.ToUpperInvariant();
			return (string.Compare(scriptCodeU, "QAAA", StringComparison.Ordinal) >= 0 && string.Compare(scriptCodeU, "QABX", StringComparison.Ordinal) <= 0);
		}

		public static bool IsValidIso3166RegionCode(string regionCodeToCheck)
		{
			return Iso3166Regions.Contains(regionCodeToCheck) || IsPrivateUseRegionCode(regionCodeToCheck);
		}

		/// <summary>
		/// Determines whether the specified region code is private use. These are considered valid region codes,
		/// but not predefined ones with a known meaning.
		/// </summary>
		/// <param name="regionCode">The region code.</param>
		/// <returns>
		/// 	<c>true</c> if the region code is private use.
		/// </returns>
		public static bool IsPrivateUseRegionCode(string regionCode)
		{
			var regionCodeU = regionCode.ToUpperInvariant();
			return regionCodeU == "AA" || regionCodeU == "ZZ"
				|| (string.Compare(regionCodeU, "QM", StringComparison.Ordinal) >= 0 && string.Compare(regionCodeU, "QZ", StringComparison.Ordinal) <= 0)
				|| (string.Compare(regionCodeU, "XA", StringComparison.Ordinal) >= 0 && string.Compare(regionCodeU, "XZ", StringComparison.Ordinal) <= 0);
		}

		public static bool IsValidRegisteredVariantCode(string variantToCheck)
		{
			return RegisteredVariants.Contains(variantToCheck);
		}
	}
}