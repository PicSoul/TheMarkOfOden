using System;
using System.Collections.Generic;
using MarkOfOden.Fear;

namespace MarkOfOden.Marks
{
	/// <summary>
	/// Reads the per-creature kill counts Valheim already keeps for this character.
	///
	/// Since 1.0 the game maintains PlayerProfile.m_playerStats[0].m_enemyStats[0] as a lifetime
	/// "how many of each creature have I killed" table, written by Game.RPC_RegisterKill. Using it
	/// instead of a private tally means an existing character arrives with its history intact rather
	/// than starting from nothing, and there is only one set of numbers to be right.
	///
	/// Index 0 is deliberate: that slot is incremented unconditionally, while the others are only
	/// updated when the run is still achievement-eligible, which no modded game is.
	/// </summary>
	public static class VanillaKillStats
	{
		private static Dictionary<string, float> Table
		{
			get
			{
				PlayerProfile profile = Game.instance != null ? Game.instance.GetPlayerProfile() : null;
				if (profile == null || profile.m_playerStats == null || profile.m_playerStats.Length == 0)
				{
					return null;
				}

				PlayerProfile.PlayerStats stats = profile.m_playerStats[0];
				if (stats == null || stats.m_enemyStats == null || stats.m_enemyStats.Length == 0)
				{
					return null;
				}

				return stats.m_enemyStats[0];
			}
		}

		/// <summary>Lifetime kills of one creature by this character, keyed by Character.m_name or prefab name.</summary>
		public static int KillsOf(string creatureName)
		{
			Dictionary<string, float> table = Table;
			if (table == null || string.IsNullOrEmpty(creatureName))
			{
				return 0;
			}

			if (table.TryGetValue(creatureName, out float kills))
			{
				return (int)kills;
			}

			// If passed a prefab name ("Greydwarf"), try corresponding token ("$enemy_greydwarf")
			if (!creatureName.StartsWith("$", StringComparison.OrdinalIgnoreCase))
			{
				string token = CreatureTiers.GetTokenForPrefab(creatureName);
				if (!string.IsNullOrEmpty(token) && table.TryGetValue(token, out float tokenKills))
				{
					return (int)tokenKills;
				}

				if (table.TryGetValue("$enemy_" + creatureName.ToLowerInvariant(), out float guessKills))
				{
					return (int)guessKills;
				}
			}
			else
			{
				// If passed a token ("$enemy_greydwarf"), try counterpart prefab name ("Greydwarf")
				string prefab = CreatureTiers.GetPrefabForToken(creatureName);
				if (!string.IsNullOrEmpty(prefab) && table.TryGetValue(prefab, out float prefabKills))
				{
					return (int)prefabKills;
				}

				string stripped = creatureName.Replace("$enemy_", "").Replace("$", "");
				if (table.TryGetValue(stripped, out float strippedKills))
				{
					return (int)strippedKills;
				}
			}

			return 0;
		}

		/// <summary>Every creature this character has killed, for publishing and for the status command.</summary>
		public static IEnumerable<KeyValuePair<string, float>> All()
		{
			return Table ?? EmptyTable;
		}

		public static int SpeciesCount()
		{
			Dictionary<string, float> table = Table;
			return table?.Count ?? 0;
		}

		private static readonly Dictionary<string, float> EmptyTable = new Dictionary<string, float>();
	}
}
