using System.Collections.Generic;
using MarkOfOden.Config;
using UnityEngine;

namespace MarkOfOden.Fear
{
	/// <summary>
	/// What a terrified creature does when running is not an option: when the player is on top of it,
	/// or the flee pathing has nowhere to go.
	///
	/// Vanilla has no cower animation to borrow, so this is built from pieces that do exist: stop dead,
	/// face the player and refuse to attack. Standing rooted and staring is most of what cowering looks
	/// like, and it costs no animation at all.
	///
	/// An optional flinch stands in for cringing by reusing the stagger recoil, but that recoil is the
	/// animation a creature plays when a blow lands, so it reads as stumbling rather than as fear on
	/// every creature that has one. It is off by default and kept only because it may suit some.
	/// </summary>
	public static class CowerState
	{
		private const float PathTestInterval = 0.5f;

		private static readonly Dictionary<MonsterAI, float> NextFlinch = new Dictionary<MonsterAI, float>();
		private static readonly Dictionary<MonsterAI, float> NextPathTest = new Dictionary<MonsterAI, float>();
		private static readonly Dictionary<MonsterAI, bool> Cornered = new Dictionary<MonsterAI, bool>();

		/// <summary>True when this creature should cower rather than flee right now.</summary>
		public static bool ShouldCower(MonsterAI ai, Player player)
		{
			if (!ModConfig.EnableCower.Value || ai == null || player == null)
			{
				return false;
			}

			float distance = Vector3.Distance(ai.transform.position, player.transform.position);
			if (distance <= ModConfig.CowerRange.Value)
			{
				return true;
			}

			// Nowhere to run: cowering reads far better than grinding into a cliff face.
			// HavePath is a real pathfinding query, so it is sampled rather than run every frame.
			if (!NextPathTest.TryGetValue(ai, out float next) || Time.time >= next)
			{
				NextPathTest[ai] = Time.time + PathTestInterval * Random.Range(0.8f, 1.2f);
				Cornered[ai] = !ai.HavePath(FleeDirectionTarget(ai, player));
			}

			return Cornered.TryGetValue(ai, out bool cornered) && cornered;
		}

		public static void Tick(MonsterAI ai, Player player)
		{
			ai.StopMoving();
			ai.LookAt(player.transform.position);

			if (ModConfig.CowerStaggerInterval.Value <= 0f)
			{
				// Rooted and staring, with no borrowed animation on top of it.
				return;
			}

			float interval = Mathf.Max(0.25f, ModConfig.CowerStaggerInterval.Value);
			if (!NextFlinch.TryGetValue(ai, out float next))
			{
				// Stagger the first flinch so a cornered group does not flinch in lockstep.
				next = Time.time + Random.Range(0f, interval);
				NextFlinch[ai] = next;
				return;
			}

			if (Time.time < next)
			{
				return;
			}

			NextFlinch[ai] = Time.time + interval * Random.Range(0.8f, 1.2f);

			Character creature = ai.m_character;
			if (creature != null && !creature.IsStaggering() && !creature.InAttack())
			{
				creature.Stagger(creature.transform.position - player.transform.position);
			}
		}

		private static Vector3 FleeDirectionTarget(MonsterAI ai, Player player)
		{
			Vector3 away = ai.transform.position - player.transform.position;
			away.y = 0f;
			if (away.sqrMagnitude < 0.01f)
			{
				away = ai.transform.forward;
			}

			return ai.transform.position + away.normalized * ai.m_fleeRange;
		}

		public static void Forget(MonsterAI ai)
		{
			if (ai != null)
			{
				NextFlinch.Remove(ai);
				NextPathTest.Remove(ai);
				Cornered.Remove(ai);
			}
		}

		public static void ClearAll()
		{
			NextFlinch.Clear();
			NextPathTest.Clear();
			Cornered.Clear();
		}
	}
}
