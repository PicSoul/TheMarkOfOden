using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using MarkOfOden.Config;
using MarkOfOden.Marks;
using UnityEngine;

namespace MarkOfOden.Fear
{
	/// <summary>
	/// Every creature a player can meet, written out as a cheat sheet beside the config.
	///
	/// The config's creature lists take a name only the game's files use - "SeekerBrood" for what the
	/// game calls a Seeker Soldier's brood, "$enemy_goblin_deepnorth" for a Fuling - and nobody should
	/// have to go digging for those. So the names are listed where the settings are.
	///
	/// It is a file of its own rather than a section of the config because BepInEx writes the config
	/// from scratch every time a setting is saved: anything added to it by hand, or by this mod, is gone
	/// the next time someone moves a slider. And not in a setting's description, because the in-game
	/// configuration manager shows descriptions as tooltips, and a tooltip a hundred lines long is no
	/// help to anyone.
	///
	/// Written fresh each time a world loads, from the game's own prefab list, so it cannot fall out of
	/// date and it includes creatures other mods add. Spoilers included: the point is to be complete.
	/// </summary>
	public static class CreatureCatalog
	{
		public const string FileName = "picsoul.valheim.markofoden.creatures.txt";

		/// <summary>
		/// Creatures that exist in the game's files but that nothing in normal play ever spawns.
		///
		/// Measured rather than guessed: 'moo creatures' follows every way the game has of putting a
		/// creature in the world - world spawns, raids, every location and dungeon room, boss altars,
		/// breeding, eggs, and anything a creature or a weapon summons - and lists what none of them
		/// reach. That list was reviewed by hand and copied here. Run it again after a game update.
		///
		/// Left out of the cheat sheet and the standings viewer. Nothing else needs to know: a creature
		/// that never spawns never meets a player, so the rest of the mod never sees it.
		/// </summary>
		private static readonly HashSet<string> UnusedPrefabs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			// Nothing refers to these anywhere: not a spawner, location, room, raid, item or creature,
			// not by soft reference, not by name in any text field, and not by name in the game's code.
			// The Dvergr mages the game does spawn are the one DvergerMage prefab; these three are not it.
			"Bat_Swamp", "Deer_White", "DvergerTest", "FrostWisp", "GoblinBrute_Hildir", "Hive", "TheHive",
			"DvergerMageFire", "DvergerMageIce", "DvergerMageSupport",
			"Draugr_sleeping", "Draugr_Ranged_sleeping", "Draugr_Elite_sleeping", "Troll_sleeping",
			"Ghost_Void", "Tendril_back", "TentaRoot_wild",

			// Placed only by content the game has switched off.
			"TrainingDummy",    // the developers' combat ring; the one players build is piece_TrainingDummy
			"Ghost_old",        // TheDarkestHole, a switched-off location
			"Goblin_Gem",       // the gemgoblin raid, switched off, and nothing switches raids on by date

			// Placed only by a spawner that nothing in the game places.
			"Frysling", "Leech_cave",
			"Skeleton_NoArcher", "Skeleton_Swamps_noarcher", "Skeleton_Mountains_noarcher"
		};

		/// <summary>
		/// Creatures that exist because a player made them: summoned by a staff, called by a piece, or
		/// built. Listed apart from the wild ones, since nobody goes looking for them in a biome.
		/// </summary>
		private static readonly HashSet<string> PlayerMade = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			"Skeleton_Friendly", "Troll_Summoned", "staff_greenroots_tentaroot", "BlobFrost", "piece_TrainingDummy",
			"Bjorn_spiritcaller", "Boar_spiritcaller", "Moose_spiritcaller", "Wolf_spiritcaller"
		};

		public static string SheetPath => Path.Combine(Paths.ConfigPath, FileName);

		/// <summary>Whether a creature, named by prefab or token, is one players never meet.</summary>
		public static bool IsUnused(string prefabOrToken)
		{
			if (string.IsNullOrEmpty(prefabOrToken))
			{
				return false;
			}

			if (UnusedPrefabs.Contains(prefabOrToken))
			{
				return true;
			}

			string prefab = CreatureTiers.GetPrefabForToken(prefabOrToken);
			return !string.IsNullOrEmpty(prefab) && UnusedPrefabs.Contains(prefab);
		}

		private sealed class Entry
		{
			public string Prefab;
			public string Token;
			public string Display;
			public string Group;
			public int GroupOrder;
			public int Tier;
			public string Tags;
		}

		/// <summary>Writes the sheet. Cheap: one pass over the prefab list, once per world load.</summary>
		public static void WriteCheatSheet()
		{
			try
			{
				if (ZNetScene.instance == null)
				{
					return;
				}

				List<Entry> entries = Collect();
				File.WriteAllText(SheetPath, Render(entries), new UTF8Encoding(false));

				if (ModConfig.DebugLogging.Value)
				{
					Plugin.Log.LogInfo("Wrote " + entries.Count + " creatures to " + FileName + ".");
				}
			}
			catch (Exception e)
			{
				// A cheat sheet is a convenience. Failing to write one must never cost anyone the mod.
				Plugin.Log.LogWarning("Could not write the creature list: " + e.Message);
			}
		}

		private static List<Entry> Collect()
		{
			List<Entry> entries = new List<Entry>();
			HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			foreach (GameObject prefab in ZNetScene.instance.m_prefabs)
			{
				if (prefab == null || !seen.Add(prefab.name))
				{
					continue;
				}

				Character character = prefab.GetComponent<Character>();
				if (character == null || prefab.GetComponent<Player>() != null || UnusedPrefabs.Contains(prefab.name))
				{
					continue;
				}

				string token = character.m_name ?? string.Empty;
				string display = Localization.instance != null ? Localization.instance.Localize(token) : token;

				// Quest bosses carry colour codes in their names - "<color=orange>Brenna</color>" - which
				// mean something to the game's text and nothing in a plain file.
				display = System.Text.RegularExpressions.Regex.Replace(display ?? string.Empty, "<[^>]+>", string.Empty).Trim();
				if (string.IsNullOrEmpty(display) || display.StartsWith("$") || display.StartsWith("["))
				{
					display = "(no name in game)";
				}

				Entry entry = new Entry
				{
					Prefab = prefab.name,
					Token = token,
					Display = display,
					Tier = CreatureTiers.GetTierForNameOrToken(prefab.name)
				};

				List<string> tags = new List<string>();

				if (character.IsBoss())
				{
					entry.Group = "Bosses";
					entry.GroupOrder = 100;
					tags.Add("boss");
				}
				else if (PlayerMade.Contains(prefab.name))
				{
					entry.Group = "Summoned or built by players";
					entry.GroupOrder = 101;
				}
				else
				{
					Heightmap.Biome biome = BiomeRegistry.ResolveCreatureBiome(prefab.name);
					int order = Array.IndexOf(BiomeRegistry.ProgressionBiomes, biome);
					entry.Group = order >= 0 ? BiomeRegistry.BiomeName(biome) : "Elsewhere, or from another mod";
					entry.GroupOrder = order >= 0 ? order : 99;

					if (!CreatureTiers.IsArmedToken(token))
					{
						tags.Add("harmless");
					}
					else if (CreatureTiers.NeverFleesToken(token))
					{
						tags.Add("hunted");
					}
				}

				entry.Tags = string.Join(", ", tags.ToArray());
				entries.Add(entry);
			}

			return entries
				.OrderBy(e => e.GroupOrder)
				.ThenBy(e => e.Display, StringComparer.OrdinalIgnoreCase)
				.ThenBy(e => e.Prefab, StringComparer.OrdinalIgnoreCase)
				.ToList();
		}

		private static string Render(List<Entry> entries)
		{
			int nameWidth = Math.Max(20, entries.Count == 0 ? 0 : entries.Max(e => e.Display.Length)) + 2;
			int prefabWidth = Math.Max(20, entries.Count == 0 ? 0 : entries.Max(e => e.Prefab.Length)) + 2;
			int tokenWidth = Math.Max(20, entries.Count == 0 ? 0 : entries.Max(e => e.Token.Length)) + 2;

			StringBuilder text = new StringBuilder();
			text.AppendLine("The Mark of Oden - every creature, and the name to use for it");
			text.AppendLine("==============================================================");
			text.AppendLine();
			text.AppendLine("The lists in picsoul.valheim.markofoden.cfg - Not hunted animals, Never flee creatures,");
			text.AppendLine("Fearless creatures and Creature tier overrides - want a creature's PREFAB NAME, which is not");
			text.AppendLine("always the name you see in game. Find the creature below and copy its prefab name. The token");
			text.AppendLine("works too. Names are separated by commas, for example:");
			text.AppendLine();
			text.AppendLine("    Not hunted animals = Seeker, SeekerBrute, SeekerBrood, GoblinDeepNorth");
			text.AppendLine();
			text.AppendLine("SPOILERS: every creature is listed, including ones from places you have not been yet.");
			text.AppendLine();
			text.AppendLine("This file is rewritten each time a world loads, from the creatures actually installed, so it");
			text.AppendLine("includes any another mod adds. Editing it changes nothing.");
			text.AppendLine();
			text.AppendLine("Tier:     how brave the creature is; your mark has to beat it before it leaves you alone.");
			text.AppendLine("hunted:   drops food, so it fights to the end and never breaks and runs.");
			text.AppendLine("harmless: has no attack at all and runs from everything, as in the base game.");
			text.AppendLine("boss:     never affected by this mod.");

			string group = null;
			foreach (Entry entry in entries)
			{
				if (entry.Group != group)
				{
					group = entry.Group;
					text.AppendLine();
					text.AppendLine("-- " + group + " " + new string('-', Math.Max(4, 72 - group.Length)));
					text.AppendLine("   " + "Name in game".PadRight(nameWidth) + "Prefab name".PadRight(prefabWidth)
						+ "Token".PadRight(tokenWidth) + "Tier  Tags");
				}

				text.AppendLine("   " + entry.Display.PadRight(nameWidth) + entry.Prefab.PadRight(prefabWidth)
					+ entry.Token.PadRight(tokenWidth) + entry.Tier.ToString().PadRight(6) + entry.Tags);
			}

			text.AppendLine();
			text.AppendLine(entries.Count + " creatures.");
			if (UnusedPrefabs.Count > 0)
			{
				text.AppendLine(UnusedPrefabs.Count + " more exist in the game's files but never appear in normal play, and are left out.");
			}

			return text.ToString();
		}
	}
}
