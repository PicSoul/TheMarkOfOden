using System;
using System.Collections.Generic;
using MarkOfOden.Fear;
using UnityEngine;

namespace MarkOfOden.Marks
{
	/// <summary>
	/// Maps creatures and bosses to their native biomes and provides strict spoiler-protection guards.
	/// Ensures players never see creatures or bosses from biomes they have not personally visited.
	/// </summary>
	public static class BiomeRegistry
	{
		public static readonly Heightmap.Biome[] ProgressionBiomes = new[]
		{
			Heightmap.Biome.Meadows,
			Heightmap.Biome.BlackForest,
			Heightmap.Biome.Swamp,
			Heightmap.Biome.Mountain,
			Heightmap.Biome.Plains,
			Heightmap.Biome.Mistlands,
			Heightmap.Biome.AshLands,
			Heightmap.Biome.DeepNorth,
			Heightmap.Biome.Ocean
		};

		private static readonly Dictionary<string, Heightmap.Biome> CreatureBiomeMap = new Dictionary<string, Heightmap.Biome>(StringComparer.OrdinalIgnoreCase)
		{
			// Meadows & Critters
			{ "Boar", Heightmap.Biome.Meadows },
			{ "Boar_piggy", Heightmap.Biome.Meadows },
			{ "Deer", Heightmap.Biome.Meadows },
			{ "Neck", Heightmap.Biome.Meadows },
			{ "Greyling", Heightmap.Biome.Meadows },
			{ "Hen", Heightmap.Biome.Meadows },
			{ "Chicken", Heightmap.Biome.Meadows },
			{ "Crow", Heightmap.Biome.Meadows },
			{ "Seagal", Heightmap.Biome.Meadows },

			// Black Forest
			{ "Greydwarf", Heightmap.Biome.BlackForest },
			{ "Greydwarf_Shaman", Heightmap.Biome.BlackForest },
			{ "Greydwarf_Elite", Heightmap.Biome.BlackForest },
			{ "Skeleton", Heightmap.Biome.BlackForest },
			{ "Skeleton_Poison", Heightmap.Biome.BlackForest },
			{ "Ghost", Heightmap.Biome.BlackForest },
			{ "Troll", Heightmap.Biome.BlackForest },

			// Swamp
			{ "Draugr", Heightmap.Biome.Swamp },
			{ "Draugr_Ranged", Heightmap.Biome.Swamp },
			{ "Draugr_Elite", Heightmap.Biome.Swamp },
			{ "Blob", Heightmap.Biome.Swamp },
			{ "BlobElite", Heightmap.Biome.Swamp },
			{ "Leech", Heightmap.Biome.Swamp },
			{ "Surtling", Heightmap.Biome.Swamp },
			{ "Wraith", Heightmap.Biome.Swamp },
			{ "Abomination", Heightmap.Biome.Swamp },

			// Mountain
			{ "Wolf", Heightmap.Biome.Mountain },
			{ "Fenring", Heightmap.Biome.Mountain },
			{ "Fenring_Cultist", Heightmap.Biome.Mountain },
			{ "StoneGolem", Heightmap.Biome.Mountain },
			{ "Hatchling", Heightmap.Biome.Mountain },
			{ "Ulv", Heightmap.Biome.Mountain },
			{ "Bat", Heightmap.Biome.Mountain },

			// Plains
			{ "Goblin", Heightmap.Biome.Plains },
			{ "GoblinShaman", Heightmap.Biome.Plains },
			{ "GoblinBrute", Heightmap.Biome.Plains },
			{ "Deathsquito", Heightmap.Biome.Plains },
			{ "Lox", Heightmap.Biome.Plains },
			{ "BlobTar", Heightmap.Biome.Plains },
			{ "Growth", Heightmap.Biome.Plains },

			// Mistlands
			{ "Seeker", Heightmap.Biome.Mistlands },
			{ "SeekerBrood", Heightmap.Biome.Mistlands },
			{ "SeekerBrute", Heightmap.Biome.Mistlands },
			{ "Gjall", Heightmap.Biome.Mistlands },
			{ "Tick", Heightmap.Biome.Mistlands },
			{ "Dverger", Heightmap.Biome.Mistlands },
			{ "DvergerMage", Heightmap.Biome.Mistlands },
			{ "DvergerMageFire", Heightmap.Biome.Mistlands },
			{ "DvergerMageIce", Heightmap.Biome.Mistlands },
			{ "DvergerMageSupport", Heightmap.Biome.Mistlands },
			{ "Hare", Heightmap.Biome.Mistlands },

			// Ashlands
			{ "Charred_Twitcher", Heightmap.Biome.AshLands },
			{ "Charred_Melee", Heightmap.Biome.AshLands },
			{ "Charred_Archer", Heightmap.Biome.AshLands },
			{ "Charred_Mage", Heightmap.Biome.AshLands },
			{ "Charred_Twitcher_Summoned", Heightmap.Biome.AshLands },
			{ "Morgen", Heightmap.Biome.AshLands },
			{ "Volture", Heightmap.Biome.AshLands },
			{ "Asksvin", Heightmap.Biome.AshLands },
			{ "BonemawSerpent", Heightmap.Biome.AshLands },
			{ "FallenValkyrie", Heightmap.Biome.AshLands },
			{ "BlobLava", Heightmap.Biome.AshLands },

			// Deep North
			{ "Moose", Heightmap.Biome.DeepNorth },
			{ "Seal", Heightmap.Biome.DeepNorth },

			// Ocean
			{ "Serpent", Heightmap.Biome.Ocean },

			// ---- Measured rather than remembered ------------------------------------------------
			// Everything below was placed by 'moo creatures', which follows where the game actually
			// spawns each creature. The entries above were written by hand and left gaps, and a
			// creature with no entry fell back to a guess from its faction - which put the Aspects in
			// the Meadows and the Deep North's prisoners in the Mistlands and the Plains, where the
			// viewer's spoiler guard would have shown them early. Where the scan found a creature only
			// in a dungeon room, or only through a raid that runs anywhere, the biome comes from the
			// place that dungeon or that quest belongs to.

			// Meadows and Black Forest
			{ "Skeleton_Meadows", Heightmap.Biome.Meadows },
			{ "Skeleton_Meadows_noarcher", Heightmap.Biome.Meadows },
			{ "Bjorn", Heightmap.Biome.BlackForest },
			{ "Bjorn_sleeping", Heightmap.Biome.BlackForest },
			{ "Ghost_sleeping", Heightmap.Biome.BlackForest },
			{ "TentaRoot", Heightmap.Biome.BlackForest },
			{ "Skeleton_Hildir", Heightmap.Biome.BlackForest },
			{ "Skeleton_Hildir_nochest", Heightmap.Biome.BlackForest },

			// Swamp
			{ "Skeleton_Swamps", Heightmap.Biome.Swamp },
			{ "Writhan", Heightmap.Biome.Swamp },
			{ "BogWitchKvastur", Heightmap.Biome.Swamp },

			// Mountain
			{ "Wolf_cub", Heightmap.Biome.Mountain },
			{ "Skeleton_Mountains", Heightmap.Biome.Mountain },
			{ "Fenring_Cultist_Hildir", Heightmap.Biome.Mountain },
			{ "Fenring_Cultist_Hildir_nochest", Heightmap.Biome.Mountain },

			// Plains
			{ "GoblinArcher", Heightmap.Biome.Plains },
			{ "Lox_Calf", Heightmap.Biome.Plains },
			{ "Unbjorn", Heightmap.Biome.Plains },
			{ "GoblinBruteBros", Heightmap.Biome.Plains },
			{ "GoblinBruteBros_nochest", Heightmap.Biome.Plains },
			{ "GoblinShaman_Hildir", Heightmap.Biome.Plains },
			{ "GoblinShaman_Hildir_nochest", Heightmap.Biome.Plains },

			// Mistlands
			{ "Mistile", Heightmap.Biome.Mistlands },

			// Ashlands
			{ "Asksvin_hatchling", Heightmap.Biome.AshLands },
			{ "Charred_Archer_Fader", Heightmap.Biome.AshLands },
			{ "Charred_Melee_Fader", Heightmap.Biome.AshLands },
			{ "Charred_Melee_Dyrnwyn", Heightmap.Biome.AshLands },
			{ "DvergerAshlands", Heightmap.Biome.AshLands },
			{ "Morgen_NonSleeping", Heightmap.Biome.AshLands },

			// Deep North
			{ "Moose_calf", Heightmap.Biome.DeepNorth },
			{ "Seal_Pup", Heightmap.Biome.DeepNorth },
			{ "Barka", Heightmap.Biome.DeepNorth },
			{ "Elaking", Heightmap.Biome.DeepNorth },
			{ "ElakingLantern", Heightmap.Biome.DeepNorth },
			{ "ElakingMole", Heightmap.Biome.DeepNorth },
			{ "FallenWarrior", Heightmap.Biome.DeepNorth },
			{ "Greydwarf_Frozen", Heightmap.Biome.DeepNorth },
			{ "Greydwarf_Shaman_Frozen", Heightmap.Biome.DeepNorth },
			{ "JotunWarrior", Heightmap.Biome.DeepNorth },
			{ "JotunWarriorDualWield", Heightmap.Biome.DeepNorth },
			{ "JotunWitch", Heightmap.Biome.DeepNorth },
			{ "ShadowPerson", Heightmap.Biome.DeepNorth },
			{ "Skeleton_DeepNorth", Heightmap.Biome.DeepNorth },
			{ "TrollFrost", Heightmap.Biome.DeepNorth },
			{ "BlobMork", Heightmap.Biome.DeepNorth },
			{ "BlobMorkMini", Heightmap.Biome.DeepNorth },
			{ "DvergerDeepNorth", Heightmap.Biome.DeepNorth },
			{ "GoblinDeepNorth", Heightmap.Biome.DeepNorth },
			{ "Tendril", Heightmap.Biome.DeepNorth },
			{ "Aspect_Eikthyr", Heightmap.Biome.DeepNorth },
			{ "Aspect_Elder", Heightmap.Biome.DeepNorth },
			{ "Aspect_Bonemass", Heightmap.Biome.DeepNorth },
			{ "Aspect_Moder", Heightmap.Biome.DeepNorth },
			{ "Aspect_Yagluth", Heightmap.Biome.DeepNorth },
			{ "Aspect_SeekerQueen", Heightmap.Biome.DeepNorth },
			{ "Aspect_Fader", Heightmap.Biome.DeepNorth },
			{ "Aspect_TentaRoot", Heightmap.Biome.DeepNorth },
			{ "BlobAspect", Heightmap.Biome.DeepNorth },
			{ "Skeleton_aspect", Heightmap.Biome.DeepNorth }
		};

		public static string BiomeName(Heightmap.Biome biome)
		{
			switch (biome)
			{
				case Heightmap.Biome.Meadows: return "Meadows";
				case Heightmap.Biome.BlackForest: return "Black Forest";
				case Heightmap.Biome.Swamp: return "Swamp";
				case Heightmap.Biome.Mountain: return "Mountain";
				case Heightmap.Biome.Plains: return "Plains";
				case Heightmap.Biome.Mistlands: return "Mistlands";
				case Heightmap.Biome.AshLands: return "Ashlands";
				case Heightmap.Biome.DeepNorth: return "Deep North";
				case Heightmap.Biome.Ocean: return "Ocean";
				default: return biome.ToString();
			}
		}

		public static Heightmap.Biome BossBiome(int bossNumber)
		{
			switch (bossNumber)
			{
				case 1: return Heightmap.Biome.Meadows;
				case 2: return Heightmap.Biome.BlackForest;
				case 3: return Heightmap.Biome.Swamp;
				case 4: return Heightmap.Biome.Mountain;
				case 5: return Heightmap.Biome.Plains;
				case 6: return Heightmap.Biome.Mistlands;
				case 7: return Heightmap.Biome.AshLands;
				case 8: return Heightmap.Biome.DeepNorth;
				default: return Heightmap.Biome.Meadows;
			}
		}

		public static string BossDefaultName(int bossNumber)
		{
			switch (bossNumber)
			{
				case 1: return "Eikthyr";
				case 2: return "The Elder";
				case 3: return "Bonemass";
				case 4: return "Moder";
				case 5: return "Yagluth";
				case 6: return "The Queen";
				case 7: return "Fader";
				default: return "Boss " + bossNumber;
			}
		}

		/// <summary>
		/// Whether the local player has visited or discovered this biome.
		/// Strictly prevents spoilers for undiscovered biomes.
		/// </summary>
		public static bool IsBiomeDiscovered(Heightmap.Biome biome)
		{
			Player player = Player.m_localPlayer;
			if (player == null)
			{
				return false;
			}

			// Meadows is the starting biome, always discovered
			if (biome == Heightmap.Biome.Meadows)
			{
				return true;
			}

			// Check player's native discovered biomes
			if (player.m_knownBiome != null)
			{
				if (player.m_knownBiome.Contains(biome.ToString()) || player.m_knownBiome.Contains(BiomeName(biome)))
				{
					return true;
				}
			}

			// Ocean is discovered once player has explored beyond the starting area
			if (biome == Heightmap.Biome.Ocean && player.m_knownBiome != null && (player.m_knownBiome.Contains("BlackForest") || player.m_knownBiome.Contains("Swamp")))
			{
				return true;
			}

			return false;
		}

		/// <summary>Returns the list of all currently discovered biomes in progression order.</summary>
		public static List<Heightmap.Biome> GetDiscoveredBiomes()
		{
			List<Heightmap.Biome> list = new List<Heightmap.Biome>();
			foreach (Heightmap.Biome b in ProgressionBiomes)
			{
				if (IsBiomeDiscovered(b))
				{
					list.Add(b);
				}
			}

			return list;
		}

		/// <summary>Resolves the native biome for a creature token or prefab name.</summary>
		public static Heightmap.Biome ResolveCreatureBiome(string creatureName)
		{
			if (string.IsNullOrEmpty(creatureName))
			{
				return Heightmap.Biome.Meadows;
			}

			// Direct mapping lookup
			if (CreatureBiomeMap.TryGetValue(creatureName, out Heightmap.Biome direct))
			{
				return direct;
			}

			// Clean token if prefixed with $enemy_
			string clean = creatureName;
			if (clean.StartsWith("$enemy_", StringComparison.OrdinalIgnoreCase))
			{
				clean = clean.Substring(7);
			}

			if (CreatureBiomeMap.TryGetValue(clean, out Heightmap.Biome cleanMatch))
			{
				return cleanMatch;
			}

			// A creature another mod adds has no entry above, but if the world spawns it, where it
			// spawns is known - and is a far better answer than a guess from its faction.
			string spawnPrefab = CreatureTiers.GetPrefabForToken(creatureName);
			Heightmap.Biome spawned = CreatureTiers.SpawnBiomeOf(string.IsNullOrEmpty(spawnPrefab) ? creatureName : spawnPrefab);
			if (spawned != Heightmap.Biome.None)
			{
				return spawned;
			}

			// Fallback: check ZNetScene prefab character faction
			if (ZNetScene.instance != null)
			{
				GameObject prefab = ZNetScene.instance.GetPrefab(creatureName);
				if (prefab != null)
				{
					Character c = prefab.GetComponent<Character>();
					if (c != null)
					{
						return BiomeFromFaction(c.GetFaction());
					}
				}
			}

			return Heightmap.Biome.Meadows;
		}

		private static Heightmap.Biome BiomeFromFaction(Character.Faction faction)
		{
			switch (faction)
			{
				case Character.Faction.AnimalsVeg: return Heightmap.Biome.Meadows;
				case Character.Faction.ForestMonsters: return Heightmap.Biome.BlackForest;
				case Character.Faction.Undead: return Heightmap.Biome.Swamp;
				case Character.Faction.SeaMonsters: return Heightmap.Biome.Ocean;
				case Character.Faction.MountainMonsters: return Heightmap.Biome.Mountain;
				case Character.Faction.PlainsMonsters: return Heightmap.Biome.Plains;
				case Character.Faction.MistlandsMonsters:
				case Character.Faction.Dverger: return Heightmap.Biome.Mistlands;
				case Character.Faction.Demon: return Heightmap.Biome.AshLands;
				case Character.Faction.DeepNorth: return Heightmap.Biome.DeepNorth;
				default: return Heightmap.Biome.Meadows;
			}
		}
	}
}
