using System;
using System.Collections.Generic;
using MarkOfOden.Config;
using UnityEngine;

namespace MarkOfOden.Marks
{
	/// <summary>
	/// Publishes the local player's mark onto their own ZDO, and reads other players' marks back.
	///
	/// This exists because monster AI runs on whichever client owns the monster, which is usually not
	/// the client of the player being feared. Deed data lives in the player's local save, so it has to
	/// travel. Vanilla solves the identical problem for crown mode by reading ZDOVars.s_crowned off the
	/// player's ZDO, and this mirrors that.
	/// </summary>
	public static class MarkSync
	{
		private static readonly int TierHash = "MoO_tier".GetStableHashCode();
		private static readonly int NotorietyHash = "MoO_noto".GetStableHashCode();

		private const int BytesPerEntry = 5; // int name hash + byte notoriety

		private static readonly Dictionary<ZDOID, CachedMark> Cache = new Dictionary<ZDOID, CachedMark>();

		private sealed class CachedMark
		{
			public string Raw;
			public float NextCheck;
			public Dictionary<int, int> Notoriety = new Dictionary<int, int>();
		}

		/// <summary>Writes the local player's mark to their ZDO. Cheap, and only called when something changed.</summary>
		public static void Publish()
		{
			Player player = Player.m_localPlayer;
			if (player == null || player.m_nview == null || !player.m_nview.IsValid() || !player.m_nview.IsOwner())
			{
				return;
			}

			ZDO zdo = player.m_nview.GetZDO();
			if (zdo == null)
			{
				return;
			}

			// An opted-out player publishes an empty mark rather than a flag of their own. Every client
			// already reads a missing mark as "nothing fears this player", which is exactly the wanted
			// behaviour, so this needs no new key and no agreement between versions about one.
			int tier = MarkLedger.OptedOut ? 0 : MarkLedger.Tier;
			zdo.Set(TierHash, tier);
			zdo.Set(NotorietyHash, MarkLedger.OptedOut ? string.Empty : EncodeNotoriety());

			if (ModConfig.DebugLogging.Value)
			{
				Plugin.Log.LogInfo("Published mark: tier " + tier + (MarkLedger.OptedOut ? " (opted out)" : string.Empty) + ".");
			}
		}

		/// <summary>The mark tier another client published for this player. 0 when the mod is absent there.</summary>
		public static int GetTier(Player player)
		{
			ZDO zdo = GetZdo(player);
			return zdo == null ? 0 : zdo.GetInt(TierHash, 0);
		}

		/// <summary>How well a given species knows this player, according to what that player published.</summary>
		public static int GetNotoriety(Player player, string creatureToken)
		{
			if (string.IsNullOrEmpty(creatureToken))
			{
				return 0;
			}

			ZDO zdo = GetZdo(player);
			if (zdo == null)
			{
				return 0;
			}

			Dictionary<int, int> table = GetNotorietyTable(zdo);
			return table.TryGetValue(creatureToken.GetStableHashCode(), out int notoriety) ? notoriety : 0;
		}

		private static ZDO GetZdo(Player player)
		{
			if (player == null || player.m_nview == null || !player.m_nview.IsValid())
			{
				return null;
			}

			return player.m_nview.GetZDO();
		}

		/// <summary>
		/// Decoding runs per creature per fear check, so the decoded table is cached per player and
		/// only re-read when the raw payload actually changes.
		/// </summary>
		private static Dictionary<int, int> GetNotorietyTable(ZDO zdo)
		{
			ZDOID id = zdo.m_uid;
			if (!Cache.TryGetValue(id, out CachedMark cached))
			{
				cached = new CachedMark();
				Cache[id] = cached;
			}

			if (Time.time < cached.NextCheck)
			{
				return cached.Notoriety;
			}

			cached.NextCheck = Time.time + 0.5f;

			string raw = zdo.GetString(NotorietyHash, string.Empty);
			if (raw == cached.Raw)
			{
				return cached.Notoriety;
			}

			cached.Raw = raw;
			cached.Notoriety = DecodeNotoriety(raw);
			return cached.Notoriety;
		}

		private static string EncodeNotoriety()
		{
			List<KeyValuePair<string, int>> entries = new List<KeyValuePair<string, int>>();
			foreach (KeyValuePair<string, float> pair in VanillaKillStats.All())
			{
				int notoriety = MarkLedger.NotorietyForKills((int)pair.Value);
				if (notoriety > 0)
				{
					entries.Add(new KeyValuePair<string, int>(pair.Key, notoriety));
				}
			}

			// Keep the payload bounded: the best-known species are the ones worth sending.
			int cap = Mathf.Max(0, ModConfig.MaxNotorietyEntries.Value);
			if (entries.Count > cap)
			{
				entries.Sort((a, b) => b.Value.CompareTo(a.Value));
				entries.RemoveRange(cap, entries.Count - cap);
			}

			byte[] buffer = new byte[entries.Count * BytesPerEntry];
			for (int i = 0; i < entries.Count; i++)
			{
				int offset = i * BytesPerEntry;
				int hash = entries[i].Key.GetStableHashCode();
				buffer[offset] = (byte)hash;
				buffer[offset + 1] = (byte)(hash >> 8);
				buffer[offset + 2] = (byte)(hash >> 16);
				buffer[offset + 3] = (byte)(hash >> 24);
				buffer[offset + 4] = (byte)Mathf.Clamp(entries[i].Value, 0, 255);
			}

			return Convert.ToBase64String(buffer);
		}

		private static Dictionary<int, int> DecodeNotoriety(string raw)
		{
			Dictionary<int, int> table = new Dictionary<int, int>();
			if (string.IsNullOrEmpty(raw))
			{
				return table;
			}

			try
			{
				byte[] buffer = Convert.FromBase64String(raw);
				for (int offset = 0; offset + BytesPerEntry <= buffer.Length; offset += BytesPerEntry)
				{
					int hash = buffer[offset] | (buffer[offset + 1] << 8) | (buffer[offset + 2] << 16) | (buffer[offset + 3] << 24);
					table[hash] = buffer[offset + 4];
				}
			}
			catch (Exception e)
			{
				// Another client sent something we cannot read. Treat it as no notoriety at all.
				Plugin.Log.LogWarning("Could not decode a published mark payload: " + e.Message);
			}

			return table;
		}

		public static void ClearCache()
		{
			Cache.Clear();
		}
	}
}
