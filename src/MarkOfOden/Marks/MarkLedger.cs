using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MarkOfOden.Config;
using MarkOfOden.Fear;
using UnityEngine;

namespace MarkOfOden.Marks
{
	/// <summary>
	/// Which bosses this character has personally helped kill, as boss numbers on the game's own
	/// 1-8 progression ladder. Persisted in Player.m_customData, which the game saves with the
	/// character, so the mark follows the character rather than the world.
	///
	/// Species kills are not stored here: Valheim already keeps them per character, so they are read
	/// straight from <see cref="VanillaKillStats"/>.
	/// </summary>
	public static class MarkLedger
	{
		private const string CustomDataKey = "MoO.bosses";
		private const string OptOutKey = "MoO.optout";
		private const string FormatVersion = "v3";

		private static readonly HashSet<int> BossNumbers = new HashSet<int>();

		/// <summary>Set by the console command to override the ledger for tuning. Negative means inactive.</summary>
		public static int ForcedTier = -1;

		/// <summary>
		/// When set, this character publishes no mark at all and nothing fears them.
		///
		/// Stored with the character rather than in config so it survives a relog and belongs to the
		/// player who chose it, not to the machine. It only ever makes them less frightening, so it
		/// needs no permission to use.
		/// </summary>
		public static bool OptedOut { get; private set; }

		public static void SetOptedOut(bool optedOut)
		{
			if (OptedOut == optedOut)
			{
				return;
			}

			OptedOut = optedOut;
			Save();
			Changed?.Invoke();
		}

		public static event Action Changed;

		public static IReadOnlyCollection<int> AllBossNumbers => BossNumbers;

		/// <summary>Highest boss number this character helped bring down, which is the mark tier.</summary>
		public static int Tier
		{
			get
			{
				if (ForcedTier >= 0)
				{
					return Mathf.Clamp(ForcedTier, 0, CreatureTiers.MaxTier);
				}

				int tier = 0;
				foreach (int bossNumber in BossNumbers)
				{
					if (bossNumber > tier)
					{
						tier = bossNumber;
					}
				}

				if (ModConfig.InheritWorldProgress.Value)
				{
					tier = Mathf.Max(tier, InheritedWorldTier());
				}

				return Mathf.Clamp(tier, 0, CreatureTiers.MaxTier);
			}
		}

		/// <summary>
		/// World progress fallback: the FleeOnSight model, deliberately opt-in and penalised, for
		/// servers that would rather everyone benefit from the world's boss kills.
		/// </summary>
		private static int InheritedWorldTier()
		{
			if (ZoneSystem.instance == null)
			{
				return 0;
			}

			int best = 0;
			foreach (KeyValuePair<string, int> pair in CreatureTiers.AllBossKeyTiers)
			{
				if (pair.Value > best && ZoneSystem.instance.GetGlobalKey(pair.Key))
				{
					best = pair.Value;
				}
			}

			return Mathf.Max(0, best - ModConfig.InheritedWorldTierPenalty.Value);
		}

		/// <summary>How well a species knows this character, from Valheim's own lifetime kill counts.</summary>
		public static int NotorietyOf(string creatureName)
		{
			return NotorietyForKills(VanillaKillStats.KillsOf(creatureName));
		}

		private static int[] _thresholds;
		private static string _thresholdsSource;

		/// <summary>Parsed once per config value rather than on every lookup and every payload entry.</summary>
		private static int[] Thresholds
		{
			get
			{
				string raw = ModConfig.NotorietyThresholds.Value;
				if (_thresholds == null || !string.Equals(raw, _thresholdsSource, StringComparison.Ordinal))
				{
					_thresholds = ModConfig.ParseThresholds(raw);
					_thresholdsSource = raw;
				}

				return _thresholds;
			}
		}

		public static int NotorietyForKills(int kills)
		{
			int[] thresholds = Thresholds;
			int notoriety = 0;
			for (int i = 0; i < thresholds.Length; i++)
			{
				if (kills >= thresholds[i])
				{
					notoriety = i + 1;
				}
			}

			return notoriety;
		}

		/// <summary>Called when Valheim credits this character with a boss kill it took part in.</summary>
		public static void CreditBoss(int bossNumber)
		{
			if (bossNumber <= 0 || !BossNumbers.Add(bossNumber))
			{
				return;
			}

			Plugin.Log.LogInfo("Credited boss " + bossNumber + ". Mark tier is now " + Tier + ".");
			Save();
			Changed?.Invoke();
		}

		/// <summary>
		/// Throws away the stored boss credits and works them out again from this character's history.
		///
		/// A plain wipe would be a trap: the seeding that recovered a character's past kills only runs
		/// when there is nothing stored, so wiping would leave the mark at zero until those bosses were
		/// killed again. Rebuilding gives the same answer a fresh install would, which is what someone
		/// asking to reset actually wants.
		/// </summary>
		public static void Rebuild(Player player)
		{
			BossNumbers.Clear();

			if (player != null)
			{
				SeedFromHistory(player);
			}
			else
			{
				Save();
			}

			Changed?.Invoke();
		}

		public static void Load(Player player)
		{
			BossNumbers.Clear();
			OptedOut = false;

			if (player == null || player.m_customData == null)
			{
				Changed?.Invoke();
				return;
			}

			try
			{
				OptedOut = player.m_customData.TryGetValue(OptOutKey, out string optOut) && optOut == "1";
				if (OptedOut)
				{
					Plugin.Log.LogInfo("This character has opted out; nothing will fear them until 'moo optin'.");
				}

				bool restored = false;

				if (player.m_customData.TryGetValue(CustomDataKey, out string raw) && !string.IsNullOrEmpty(raw))
				{
					string[] sections = raw.Split('|');
					if (sections.Length >= 2 && sections[0] == FormatVersion)
					{
						foreach (string entry in sections[1].Split(','))
						{
							if (int.TryParse(entry.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int bossNumber) && bossNumber > 0)
							{
								BossNumbers.Add(bossNumber);
							}
						}

						restored = true;
					}
					else
					{
						Plugin.Log.LogInfo("Boss credits were written by an older version; working them out again from this character's history.");
					}
				}

				if (!restored && ModConfig.SeedFromHistory.Value)
				{
					SeedFromHistory(player);
				}

				Plugin.Log.LogInfo("Mark loaded: tier " + Tier + " from " + BossNumbers.Count + " boss credits, "
					+ VanillaKillStats.SpeciesCount() + " species killed.");
			}
			catch (Exception e)
			{
				// A corrupt ledger must never stop the character from spawning.
				BossNumbers.Clear();
				Plugin.Log.LogError("Failed to read boss credits, starting fresh: " + e);
			}

			Changed?.Invoke();
		}

		/// <summary>
		/// Gives a character that existed before this mod was installed the mark it has already earned.
		///
		/// The evidence that matters is Valheim's own lifetime kill table: since 1.0 it records every
		/// creature this character helped kill, bosses included, credited by participation. That is
		/// per character and does not care who picked up the loot, which matters because a boss drops
		/// one trophy and in a group only one player gets it.
		///
		/// Trophies are still checked as a second source, for kills that predate the kill table.
		/// Runs once, on the first load with no stored credits.
		/// </summary>
		private static void SeedFromHistory(Player player)
		{
			List<string> evidence = new List<string>();

			foreach (KeyValuePair<string, int> boss in CreatureTiers.AllBossTokens)
			{
				int kills = VanillaKillStats.KillsOf(boss.Key);
				if (kills > 0 && BossNumbers.Add(boss.Value))
				{
					evidence.Add("boss " + boss.Value + " (" + boss.Key + ", killed " + kills + ")");
				}
			}

			foreach (string trophy in player.GetTrophies() ?? new List<string>())
			{
				int bossNumber = CreatureTiers.GetBossNumberForTrophy(trophy);
				if (bossNumber > 0 && BossNumbers.Add(bossNumber))
				{
					evidence.Add("boss " + bossNumber + " (trophy " + trophy + ")");
				}
			}

			// Written even when nothing was found, so this runs once per character rather than every load.
			Save();

			if (evidence.Count > 0)
			{
				Plugin.Log.LogInfo("Seeded the mark from this character's history: " + string.Join(", ", evidence.ToArray())
					+ ". Mark tier is now " + Tier + ".");
			}
			else
			{
				Plugin.Log.LogInfo("No past boss kills found in this character's history; starting at tier 0.");
			}
		}

		public static void Save()
		{
			Player player = Player.m_localPlayer;
			if (player == null || player.m_customData == null)
			{
				return;
			}

			StringBuilder builder = new StringBuilder(FormatVersion).Append('|');
			bool first = true;
			foreach (int bossNumber in BossNumbers)
			{
				if (!first)
				{
					builder.Append(',');
				}

				builder.Append(bossNumber.ToString(CultureInfo.InvariantCulture));
				first = false;
			}

			player.m_customData[CustomDataKey] = builder.ToString();
			player.m_customData[OptOutKey] = OptedOut ? "1" : "0";
		}
	}
}
