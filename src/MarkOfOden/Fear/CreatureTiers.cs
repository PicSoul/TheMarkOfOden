using System;
using System.Collections.Generic;
using System.Text;
using MarkOfOden.Config;
using UnityEngine;

namespace MarkOfOden.Fear
{
	/// <summary>
	/// How dangerous each creature believes itself to be, on the same 0-8 scale as a player's mark.
	/// Built once from the prefab list so modded creatures and Deep North additions get a sane tier
	/// without anyone having to maintain a list, then overridden by config where the guess is wrong.
	/// </summary>
	public static class CreatureTiers
	{
		public const int MaxTier = 8;

		/// <summary>Hand-tuned tiers for vanilla creatures, keyed by prefab name.</summary>
		private static readonly Dictionary<string, int> VanillaTiers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
		{
			// Meadows and critters
			{ "Boar", 0 }, { "Boar_piggy", 0 }, { "Deer", 0 }, { "Neck", 0 }, { "Greyling", 0 },
			{ "Hen", 0 }, { "Chicken", 0 }, { "Hare", 0 }, { "Crow", 0 }, { "Seagal", 0 },
			// Black Forest
			{ "Greydwarf", 1 }, { "Greydwarf_Shaman", 2 }, { "Greydwarf_Elite", 2 },
			{ "Skeleton", 1 }, { "Skeleton_Poison", 2 }, { "Ghost", 2 }, { "Troll", 3 },
			// Ocean
			{ "Serpent", 4 },
			// Swamp
			{ "Draugr", 3 }, { "Draugr_Ranged", 3 }, { "Draugr_Elite", 4 },
			{ "Blob", 2 }, { "BlobElite", 4 }, { "Leech", 2 }, { "Surtling", 2 },
			{ "Wraith", 4 }, { "Abomination", 5 },
			// Mountains
			{ "Wolf", 3 }, { "Fenring", 4 }, { "Fenring_Cultist", 5 }, { "StoneGolem", 5 },
			{ "Hatchling", 3 }, { "Ulv", 4 }, { "Bat", 1 },
			// Plains
			{ "Goblin", 4 }, { "GoblinShaman", 4 }, { "GoblinBrute", 5 },
			{ "Deathsquito", 4 }, { "Lox", 5 }, { "BlobTar", 4 }, { "Growth", 4 },
			// Mistlands
			{ "Seeker", 5 }, { "SeekerBrood", 2 }, { "SeekerBrute", 6 }, { "Gjall", 6 },
			{ "Tick", 5 }, { "Dverger", 5 }, { "DvergerMage", 6 },
			{ "DvergerMageFire", 6 }, { "DvergerMageIce", 6 }, { "DvergerMageSupport", 6 },
			// Ashlands
			{ "Charred_Twitcher", 5 }, { "Charred_Melee", 6 }, { "Charred_Archer", 6 },
			{ "Charred_Mage", 7 }, { "Charred_Twitcher_Summoned", 5 },
			{ "Morgen", 7 }, { "Volture", 6 }, { "Asksvin", 6 }, { "BonemawSerpent", 7 },
			{ "FallenValkyrie", 7 }, { "BlobLava", 6 }
			// Deep North creature prefab names are intentionally absent: the faction/health heuristic
			// covers them, and "moo dump" prints what it resolved so they can be pinned down in config.
		};

		/// <summary>
		/// Tier per prefab, keyed by the prefab name hash the ZDO already stores.
		///
		/// Keyed by prefab rather than by creature name because different creatures share a name: the
		/// Deep North reskins a Greydwarf as a far deadlier creature under the same localisation token,
		/// and keying by name meant the Black Forest original inherited its courage.
		/// </summary>
		private static readonly Dictionary<int, int> TierByPrefabHash = new Dictionary<int, int>();

		private static readonly Dictionary<string, int> TierByToken = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		private static readonly Dictionary<string, string> TokenByPrefab = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		private static readonly Dictionary<string, int> HeuristicTiers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		private static readonly Dictionary<string, string> FactionByToken = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		private static readonly Dictionary<string, float> HealthByToken = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

		/// <summary>Everything that drops food, whatever its tier, so the dump can show what the tier gate excluded.</summary>
		private static readonly HashSet<string> DropsFood = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		private static readonly HashSet<string> FearlessTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		private static readonly HashSet<string> NeverFleeTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		private static readonly HashSet<string> AutoNeverFlee = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		/// <summary>Item prefabs that a cooking station turns into something edible.</summary>
		private static readonly HashSet<string> CookableIntoFood = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		/// <summary>Boss global key -> boss number, used only by the world progress fallback.</summary>
		private static readonly Dictionary<string, int> BossTierByKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

		/// <summary>Boss creature name token -> the global key that boss sets on defeat.</summary>
		private static readonly Dictionary<string, string> BossKeyByToken = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		/// <summary>Trophy item prefab -> the boss number it proves, read from each boss's drop table.</summary>
		private static readonly Dictionary<string, int> BossNumberByTrophy = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

		/// <summary>Boss creature name token -> its boss number, for reading the character's kill history.</summary>
		private static readonly Dictionary<string, int> BossNumberByToken = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

		public static bool Ready { get; private set; }

		/// <summary>Rebuilds the whole table from the live prefab list. Safe to call again on config change.</summary>
		public static void Build(ZNetScene scene)
		{
			if (scene == null)
			{
				return;
			}

			TierByPrefabHash.Clear();
			TierByToken.Clear();
			TokenByPrefab.Clear();
			HeuristicTiers.Clear();
			FactionByToken.Clear();
			HealthByToken.Clear();
			DropsFood.Clear();
			BossTierByKey.Clear();
			BossKeyByToken.Clear();
			BossNumberByTrophy.Clear();
			BossNumberByToken.Clear();
			FearlessTokens.Clear();
			NeverFleeTokens.Clear();
			AutoNeverFlee.Clear();
			CookableIntoFood.Clear();

			BuildCookableFoodSet(scene);

			foreach (GameObject prefab in scene.m_prefabs)
			{
				if (prefab == null)
				{
					continue;
				}

				Character character = prefab.GetComponent<Character>();
				if (character == null || string.IsNullOrEmpty(character.m_name))
				{
					continue;
				}

				string token = character.m_name;
				TokenByPrefab[prefab.name] = token;

				if (character.IsBoss())
				{
					RegisterBoss(character, token);
					continue;
				}

				int tier = ResolveDefaultTier(prefab.name, character);
				TierByPrefabHash[prefab.name.GetStableHashCode()] = tier;
				HeuristicTiers[token] = tier;
				FactionByToken[token] = character.m_faction.ToString();

				HealthByToken[token] = character.m_health;

				if (DropsEdible(character))
				{
					DropsFood.Add(token);
					if (tier <= ModConfig.FoodAnimalMaxTier.Value)
					{
						AutoNeverFlee.Add(token);
					}
				}
				if (!TierByToken.TryGetValue(token, out int existing) || tier > existing)
				{
					TierByToken[token] = tier;
				}
			}

			ApplyConfigOverrides();
			InheritFamilyBehaviour(scene);
			Ready = true;

			int guessed = 0;
			foreach (KeyValuePair<string, int> pair in HeuristicTiers)
			{
				if (!VanillaTiers.ContainsValue(pair.Value) || !KnownPrefab(pair.Key))
				{
					guessed++;
				}
			}

			Plugin.Log.LogInfo("Creature tier table built: " + TierByToken.Count + " creatures ("
				+ guessed + " rated by heuristic), " + BossTierByKey.Count + " bosses.");
		}

		/// <summary>
		/// Gives young creatures the same classification as their adult form.
		///
		/// A piglet is the same kind of creature as a boar, so it should be treated the same way; left
		/// alone it drops no meat of its own, so it would be judged prey-or-not on its own merits and
		/// end up bolting from a player the adults beside it are calmly ignoring.
		///
		/// Both links are followed, because the game does not always provide both: a moose calf knows
		/// what it grows into, while a seal pup does not, and is only reachable from the seal that
		/// breeds it.
		///
		/// Only the classification is inherited, not the tier: a cub really is less dangerous than the
		/// wolf it becomes, and tier is what the creature thinks of its own chances.
		/// </summary>
		private static void InheritFamilyBehaviour(ZNetScene scene)
		{
			foreach (GameObject prefab in scene.m_prefabs)
			{
				if (prefab == null)
				{
					continue;
				}

				Character self = prefab.GetComponent<Character>();
				if (self == null || string.IsNullOrEmpty(self.m_name))
				{
					continue;
				}

				// Upwards: this creature grows into something already classified.
				Growup growup = prefab.GetComponent<Growup>();
				if (growup != null)
				{
					foreach (GameObject adultPrefab in GrownForms(growup))
					{
						if (CopyClassification(adultPrefab, self.m_name))
						{
							break;
						}
					}
				}

				// Downwards: this creature breeds something that has no way to look upwards.
				Procreation procreation = prefab.GetComponent<Procreation>();
				if (procreation != null)
				{
					CopyClassificationTo(self.m_name, procreation.m_offspring);
					CopyClassificationTo(self.m_name, procreation.m_noPartnerOffspring);
				}
			}
		}

		/// <summary>Copies the source creature's classification onto the named creature.</summary>
		private static bool CopyClassification(GameObject sourcePrefab, string targetToken)
		{
			Character source = sourcePrefab != null ? sourcePrefab.GetComponent<Character>() : null;
			if (source == null || string.IsNullOrEmpty(source.m_name))
			{
				return false;
			}

			if (NeverFleeTokens.Contains(source.m_name))
			{
				NeverFleeTokens.Add(targetToken);
				return true;
			}

			if (AutoNeverFlee.Contains(source.m_name))
			{
				AutoNeverFlee.Add(targetToken);
				return true;
			}

			return false;
		}

		private static void CopyClassificationTo(string sourceToken, GameObject targetPrefab)
		{
			Character target = targetPrefab != null ? targetPrefab.GetComponent<Character>() : null;
			if (target == null || string.IsNullOrEmpty(target.m_name))
			{
				return;
			}

			if (NeverFleeTokens.Contains(sourceToken))
			{
				NeverFleeTokens.Add(target.m_name);
			}
			else if (AutoNeverFlee.Contains(sourceToken))
			{
				AutoNeverFlee.Add(target.m_name);
			}
		}

		private static IEnumerable<GameObject> GrownForms(Growup growup)
		{
			if (growup.m_grownPrefab != null)
			{
				yield return growup.m_grownPrefab;
			}

			if (growup.m_altGrownPrefabs == null)
			{
				yield break;
			}

			foreach (Growup.GrownEntry entry in growup.m_altGrownPrefabs)
			{
				if (entry?.m_prefab != null)
				{
					yield return entry.m_prefab;
				}
			}
		}

		private static void RegisterBoss(Character character, string token)
		{
			// m_bossOrder is the game's own 1-8 progression ladder and is what Valheim reports when it
			// credits a kill, so the mark ladder needs no knowledge of boss names or global keys.
			int bossNumber = character.BossOrder();
			if (bossNumber <= 0)
			{
				return;
			}

			BossNumberByToken[token] = Mathf.Clamp(bossNumber, 1, MaxTier);

			if (!string.IsNullOrEmpty(character.m_defeatSetGlobalKey))
			{
				BossKeyByToken[token] = character.m_defeatSetGlobalKey;
				BossTierByKey[character.m_defeatSetGlobalKey] = Mathf.Clamp(bossNumber, 1, MaxTier);
			}

			RegisterBossTrophies(character, bossNumber);
		}

		/// <summary>
		/// Records which trophy each boss drops, so a character that predates this mod can be given
		/// credit for the bosses whose heads it carries. Read from the prefab's own drop table rather
		/// than a hardcoded list, so it covers Deep North and modded bosses too.
		/// </summary>
		private static void RegisterBossTrophies(Character character, int bossNumber)
		{
			CharacterDrop drops = character.GetComponent<CharacterDrop>();
			if (drops == null || drops.m_drops == null)
			{
				return;
			}

			foreach (CharacterDrop.Drop drop in drops.m_drops)
			{
				if (drop == null || drop.m_prefab == null)
				{
					continue;
				}

				ItemDrop item = drop.m_prefab.GetComponent<ItemDrop>();
				if (item == null || item.m_itemData?.m_shared == null)
				{
					continue;
				}

				if (item.m_itemData.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Trophy)
				{
					BossNumberByTrophy[drop.m_prefab.name] = Mathf.Clamp(bossNumber, 1, MaxTier);
				}
			}
		}

		/// <summary>Every boss creature name token and the boss number it is worth.</summary>
		public static IReadOnlyDictionary<string, int> AllBossTokens => BossNumberByToken;

		/// <summary>Trophy prefabs that prove a given boss, for diagnostics.</summary>
		public static IReadOnlyDictionary<string, int> AllBossTrophies => BossNumberByTrophy;

		/// <summary>Boss number proved by holding this trophy, or 0 if it is not a boss trophy.</summary>
		public static int GetBossNumberForTrophy(string trophyPrefabName)
		{
			return BossNumberByTrophy.TryGetValue(trophyPrefabName ?? string.Empty, out int bossNumber) ? bossNumber : 0;
		}

		private static void ApplyConfigOverrides()
		{
			foreach (KeyValuePair<string, int> pair in ModConfig.ParseNameValueList(ModConfig.CreatureTierOverrides.Value))
			{
				int tier = Mathf.Clamp(pair.Value, 0, MaxTier);

				// A name given as a prefab overrides just that creature; given as a shared token it
				// overrides every creature that uses the token.
				if (TokenByPrefab.ContainsKey(pair.Key))
				{
					TierByPrefabHash[pair.Key.GetStableHashCode()] = tier;
				}
				else
				{
					foreach (KeyValuePair<string, string> entry in TokenByPrefab)
					{
						if (string.Equals(entry.Value, pair.Key, StringComparison.OrdinalIgnoreCase))
						{
							TierByPrefabHash[entry.Key.GetStableHashCode()] = tier;
						}
					}
				}

				TierByToken[ResolveToken(pair.Key)] = tier;
			}

			foreach (string name in ModConfig.ParseNameList(ModConfig.FearlessCreatures.Value))
			{
				FearlessTokens.Add(ResolveToken(name));
			}

			foreach (string name in ModConfig.ParseNameList(ModConfig.NeverFleeCreatures.Value))
			{
				NeverFleeTokens.Add(ResolveToken(name));
			}

		}

		/// <summary>Config may name a creature by prefab (Greyling) or by localisation token.</summary>
		private static string ResolveToken(string name)
		{
			return TokenByPrefab.TryGetValue(name, out string token) ? token : name;
		}

		private static int ResolveDefaultTier(string prefabName, Character character)
		{
			if (VanillaTiers.TryGetValue(prefabName, out int known))
			{
				return known;
			}

			int fromFaction = TierFromFaction(character.m_faction);
			int fromHealth = TierFromHealth(character.m_health);
			return Mathf.Clamp(Mathf.RoundToInt((fromFaction + fromHealth) / 2f), 0, MaxTier);
		}

		private static int TierFromFaction(Character.Faction faction)
		{
			switch (faction)
			{
				case Character.Faction.AnimalsVeg: return 0;
				case Character.Faction.ForestMonsters: return 1;
				case Character.Faction.Undead: return 3;
				case Character.Faction.SeaMonsters: return 4;
				case Character.Faction.MountainMonsters: return 4;
				case Character.Faction.Demon: return 5;
				case Character.Faction.PlainsMonsters: return 5;
				case Character.Faction.Dverger: return 5;
				case Character.Faction.MistlandsMonsters: return 6;
				case Character.Faction.DeepNorth: return 8;
				default: return 2;
			}
		}

		private static int TierFromHealth(float health)
		{
			if (health <= 20f) return 0;
			if (health <= 50f) return 1;
			if (health <= 100f) return 2;
			if (health <= 200f) return 3;
			if (health <= 400f) return 4;
			if (health <= 800f) return 5;
			if (health <= 1500f) return 6;
			return 7;
		}

		public static int GetTier(Character character)
		{
			if (character == null)
			{
				return MaxTier;
			}

			// The ZDO already carries the prefab hash, so this costs a dictionary lookup and no string work.
			ZDO zdo = character.m_nview != null && character.m_nview.IsValid() ? character.m_nview.GetZDO() : null;
			if (zdo != null && TierByPrefabHash.TryGetValue(zdo.GetPrefab(), out int byPrefab))
			{
				return byPrefab;
			}

			if (!string.IsNullOrEmpty(character.m_name) && TierByToken.TryGetValue(character.m_name, out int byToken))
			{
				return byToken;
			}

			// An unknown creature is treated as top tier, so a mod this one has never seen
			// errs towards vanilla behaviour rather than towards everything fleeing.
			return MaxTier;
		}

		public static bool IsFearless(Character character)
		{
			return character != null && FearlessTokens.Contains(character.m_name);
		}

		/// <summary>Creatures allowed to lose interest in you, but never to break and run.</summary>
		public static bool NeverFlees(Character character)
		{
			if (character == null)
			{
				return false;
			}

			if (NeverFleeTokens.Contains(character.m_name))
			{
				return true;
			}

			return ModConfig.AutoNeverFleeFoodAnimals.Value && AutoNeverFlee.Contains(character.m_name);
		}

		/// <summary>
		/// Collects everything a cooking station turns into food.
		///
		/// Needed because plenty of what players hunt is not edible as it drops - neck tail has to be
		/// grilled before it feeds anyone - so checking only for directly edible drops misses them.
		/// </summary>
		private static void BuildCookableFoodSet(ZNetScene scene)
		{
			foreach (GameObject prefab in scene.m_prefabs)
			{
				CookingStation station = prefab != null ? prefab.GetComponent<CookingStation>() : null;
				if (station == null || station.m_conversion == null)
				{
					continue;
				}

				foreach (CookingStation.ItemConversion conversion in station.m_conversion)
				{
					if (conversion?.m_from == null || conversion.m_to == null)
					{
						continue;
					}

					ItemDrop.ItemData.SharedData cooked = conversion.m_to.m_itemData?.m_shared;
					if (cooked != null && cooked.m_food > 0f)
					{
						CookableIntoFood.Add(conversion.m_from.name);
					}
				}
			}
		}

		/// <summary>
		/// Whether players hunt this creature for food, worked out from its own drop table: something
		/// that drops an edible item, or one that cooks into an edible item, and that is weak enough
		/// to be prey rather than a threat.
		///
		/// Read from game data rather than a hand-written list so it covers creatures this mod has
		/// never heard of, and so it cannot drift out of date when the game adds more.
		/// </summary>
		private static bool DropsEdible(Character character)
		{
			CharacterDrop drops = character.GetComponent<CharacterDrop>();
			if (drops == null || drops.m_drops == null)
			{
				return false;
			}

			foreach (CharacterDrop.Drop drop in drops.m_drops)
			{
				if (drop == null || drop.m_prefab == null)
				{
					continue;
				}

				ItemDrop item = drop.m_prefab.GetComponent<ItemDrop>();
				if (item?.m_itemData?.m_shared == null)
				{
					continue;
				}

				if (item.m_itemData.m_shared.m_food > 0f || CookableIntoFood.Contains(drop.m_prefab.name))
				{
					return true;
				}
			}

			return false;
		}

		public static IReadOnlyDictionary<string, int> AllBossKeyTiers => BossTierByKey;

		public static string DumpTiers()
		{
			List<string> lines = new List<string>();
			foreach (KeyValuePair<string, int> pair in TierByToken)
			{
				bool overridden = !HeuristicTiers.TryGetValue(pair.Key, out int guess) || guess != pair.Value;
				string faction = FactionByToken.TryGetValue(pair.Key, out string f) ? f : "?";
				float health = HealthByToken.TryGetValue(pair.Key, out float hp) ? hp : 0f;
				lines.Add(pair.Key
					+ " = " + DescribeTiers(pair.Key, pair.Value)
					+ "  [" + faction + "]"
					+ "  hp " + health.ToString("0")
					+ "  prefabs: " + PrefabsFor(pair.Key)
					+ (overridden ? " (override)" : string.Empty)
					+ (FearlessTokens.Contains(pair.Key) ? " FEARLESS" : string.Empty)
					+ (NeverFleeTokens.Contains(pair.Key) ? " NEVER-FLEES" : string.Empty)
					+ (AutoNeverFlee.Contains(pair.Key) ? " FOOD" : string.Empty)
					+ (DropsFood.Contains(pair.Key) && !AutoNeverFlee.Contains(pair.Key) ? " DROPS-FOOD" : string.Empty));
			}

			lines.Sort(StringComparer.OrdinalIgnoreCase);

			StringBuilder builder = new StringBuilder();
			builder.AppendLine("Creature tiers (" + lines.Count + "):");
			foreach (string line in lines)
			{
				builder.AppendLine("  " + line);
			}

			return builder.ToString();
		}

		/// <summary>Whether any prefab behind this creature name is one the built-in table knows.</summary>
		private static bool KnownPrefab(string token)
		{
			foreach (KeyValuePair<string, string> entry in TokenByPrefab)
			{
				if (string.Equals(entry.Value, token, StringComparison.OrdinalIgnoreCase) && VanillaTiers.ContainsKey(entry.Key))
				{
					return true;
				}
			}

			return false;
		}

		/// <summary>The prefab names behind a creature name, which is what config entries should use.</summary>
		private static string PrefabsFor(string token)
		{
			List<string> names = new List<string>();
			foreach (KeyValuePair<string, string> entry in TokenByPrefab)
			{
				if (string.Equals(entry.Value, token, StringComparison.OrdinalIgnoreCase))
				{
					names.Add(entry.Key);
				}
			}

			names.Sort(StringComparer.OrdinalIgnoreCase);
			return names.Count == 0 ? "?" : string.Join("/", names.ToArray());
		}

		/// <summary>Shows every tier in use for a shared name, so collisions are visible in the dump.</summary>
		private static string DescribeTiers(string token, int fallback)
		{
			SortedSet<int> tiers = new SortedSet<int>();
			foreach (KeyValuePair<string, string> entry in TokenByPrefab)
			{
				if (string.Equals(entry.Value, token, StringComparison.OrdinalIgnoreCase)
					&& TierByPrefabHash.TryGetValue(entry.Key.GetStableHashCode(), out int tier))
				{
					tiers.Add(tier);
				}
			}

			if (tiers.Count == 0)
			{
				return fallback.ToString();
			}

			return string.Join("/", new List<int>(tiers).ConvertAll(t => t.ToString()).ToArray());
		}

		public static string DumpBosses()
		{
			StringBuilder builder = new StringBuilder();
			builder.AppendLine("Bosses (" + BossNumberByToken.Count + "):");
			foreach (KeyValuePair<string, int> pair in BossNumberByToken)
			{
				List<string> trophies = new List<string>();
				foreach (KeyValuePair<string, int> trophy in BossNumberByTrophy)
				{
					if (trophy.Value == pair.Value)
					{
						trophies.Add(trophy.Key);
					}
				}

				int kills = Marks.VanillaKillStats.KillsOf(pair.Key);
				builder.AppendLine("  boss " + pair.Value + "  " + pair.Key
					+ "  your kills: " + kills
					+ "  trophy: " + (trophies.Count == 0 ? "none" : string.Join("/", trophies.ToArray()))
					+ (BossKeyByToken.TryGetValue(pair.Key, out string key) ? "  key: " + key : string.Empty));
			}

			return builder.ToString();
		}

	}
}
