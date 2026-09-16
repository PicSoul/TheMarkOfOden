using System.Collections.Generic;
using MarkOfOden.Config;
using UnityEngine;

namespace MarkOfOden.Fear
{
	/// <summary>
	/// Keeps track of boss fights in progress, so the creatures a boss summons keep fighting.
	///
	/// Adds are not covered by any of the other exemptions: SpawnAbility alerts what it spawns but
	/// never marks it as hunting the player, and they belong to no raid. Without this a late-game
	/// player would watch Fader's charred wander off and the Elder's roots lose interest, which turns
	/// a boss fight into a formality.
	///
	/// Proximity to an alerted boss is used rather than tracking who spawned what, because it holds
	/// however a creature arrived — summoned, wandered in, or spawned by a mod this one knows nothing
	/// about.
	/// </summary>
	public static class BossFight
	{
		private const float RefreshInterval = 1f;

		private static readonly List<Vector3> ActiveBosses = new List<Vector3>();
		private static float _nextRefresh;

		/// <summary>True when a boss is alerted close enough that this position counts as part of its fight.</summary>
		public static bool IsInBossFight(Vector3 position)
		{
			float radius = ModConfig.BossFightRadius.Value;
			if (radius <= 0f)
			{
				return false;
			}

			Refresh();
			if (ActiveBosses.Count == 0)
			{
				return false;
			}

			float radiusSqr = radius * radius;
			foreach (Vector3 boss in ActiveBosses)
			{
				if ((boss - position).sqrMagnitude <= radiusSqr)
				{
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Refreshed once per second for the whole world rather than per creature, so a boss fight with
		/// a screen full of adds costs one scan a second, not one per add per check.
		/// </summary>
		private static void Refresh()
		{
			if (Time.time < _nextRefresh)
			{
				return;
			}

			_nextRefresh = Time.time + RefreshInterval;
			ActiveBosses.Clear();

			foreach (Character character in Character.GetAllCharacters())
			{
				if (character == null || character.IsDead() || !character.IsBoss())
				{
					continue;
				}

				BaseAI ai = character.GetBaseAI();
				if (ai != null && ai.IsAlerted())
				{
					ActiveBosses.Add(character.transform.position);
				}
			}
		}

		public static void Clear()
		{
			ActiveBosses.Clear();
			_nextRefresh = 0f;
		}
	}
}
