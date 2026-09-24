using System.Collections.Generic;
using MarkOfOden.Config;
using UnityEngine;

namespace MarkOfOden.Fear
{
	/// <summary>
	/// Whether a creature that is losing a fight breaks and runs.
	///
	/// Fleeing used to follow from seeing you: outrank a creature by enough and it bolted before a blow
	/// was struck. That made every rank you gained a switch thrown on whole biomes at once, and it needed
	/// a patch for every kind of creature it looked wrong on - food animals first, and then the Seekers
	/// and Fulings that turned out to drop food too. Rules about what kind of creature something is keep
	/// running into cases like that. Rules about how a fight is going do not.
	///
	/// So nothing runs from you until you have hurt it. A creature that outranks you, or matches you,
	/// fights to the end. One you outrank breaks once it is badly enough hurt, and the further you
	/// outrank it the sooner that comes: its courage is read live, so packmates dying around it and the
	/// sun coming up both bring the break closer, which is how "the last one standing breaks" now falls
	/// out of the rule rather than being a rule of its own.
	///
	/// Hunted animals never break. They may ignore you, but provoked they fight to the end, so hunting
	/// stays a fight rather than a chase.
	///
	/// A creature that breaks runs for a while and then settles, and once it has settled it no longer
	/// holds the fight against you: your standing takes over again and it leaves you alone. Hit it again
	/// and it is angry again - and, being as hurt as it was, breaks again at once.
	///
	/// Tracked on whichever client owns the creature, because that is where its AI runs.
	/// </summary>
	public static class Morale
	{
		private sealed class Break
		{
			public Player From;
			public float Until;
		}

		private static readonly Dictionary<MonsterAI, Break> Broken = new Dictionary<MonsterAI, Break>();

		/// <summary>Whether this creature is currently broken and running.</summary>
		public static bool IsBroken(MonsterAI ai)
		{
			return ai != null && Broken.TryGetValue(ai, out Break state) && Time.time < state.Until;
		}

		/// <summary>
		/// The health fraction below which this creature breaks when fighting this player, or 0 if it
		/// never will. Worked out from the same standing the rest of the mod uses, so the night, its
		/// stars and whatever is left of its pack all count.
		/// </summary>
		public static float BreakPoint(MonsterAI ai, Player player)
		{
			if (ai == null || ai.m_character == null || player == null || !ModConfig.MoraleEnabled.Value)
			{
				return 0f;
			}

			if (CreatureTiers.NeverFlees(ai.m_character))
			{
				return 0f;
			}

			if (!FearEvaluator.Standing(ai, player, out _, out float delta))
			{
				return 0f;
			}

			return BreakPointFor(delta);
		}

		/// <summary>
		/// The health fraction at which a creature outranked by this much breaks, or 0 if it does not
		/// outrank it at all. One step past the point where it leaves you alone breaks at the base value,
		/// and every step beyond adds the step value, up to the cap.
		/// </summary>
		public static float BreakPointFor(float delta)
		{
			float over = delta - ModConfig.CautiousThreshold.Value;
			if (over < 0f)
			{
				return 0f;
			}

			float point = ModConfig.MoraleBreakHealth.Value + over * ModConfig.MoraleBreakStep.Value;
			return Mathf.Clamp(point, 0f, Mathf.Clamp01(ModConfig.MoraleBreakCap.Value));
		}

		/// <summary>
		/// Runs every AI update for a creature this client owns. Returns true while it is broken, in which
		/// case the caller should leave its movement alone and let it run.
		/// </summary>
		public static bool Tick(MonsterAI ai, float dt)
		{
			if (ai == null || ai.m_character == null)
			{
				return false;
			}

			if (Broken.TryGetValue(ai, out Break state))
			{
				if (Time.time < state.Until && state.From != null && !state.From.IsDead())
				{
					Run(ai, dt, state.From);
					return true;
				}

				// Settled. It no longer holds the fight against you, so what it thinks of you is back to
				// your standing - which, since it broke, means it leaves you alone.
				Broken.Remove(ai);
				FearEvaluator.ForgetAnger(ai, state.From);
				return false;
			}

			if (!(ai.m_targetCreature is Player player) || !FearEvaluator.IsRetaliatingAgainst(ai, player))
			{
				return false;
			}

			float breakPoint = BreakPoint(ai, player);
			if (breakPoint <= 0f || ai.m_character.GetHealthPercentage() >= breakPoint)
			{
				return false;
			}

			Broken[ai] = new Break { From = player, Until = Time.time + Mathf.Max(1f, ModConfig.MoraleRunTime.Value) };

			if (ModConfig.DebugLogging.Value)
			{
				Plugin.Log.LogInfo(ai.m_character.m_name + " broke at " + (ai.m_character.GetHealthPercentage() * 100f).ToString("0")
					+ "% health (break point " + (breakPoint * 100f).ToString("0") + "%) and is running from " + player.GetPlayerName() + ".");
			}

			Run(ai, dt, player);
			return true;
		}

		/// <summary>A broken creature hit again keeps running rather than settling mid-chase.</summary>
		public static void NoteHurt(MonsterAI ai)
		{
			if (ai != null && Broken.TryGetValue(ai, out Break state))
			{
				state.Until = Time.time + Mathf.Max(1f, ModConfig.MoraleRunTime.Value);
			}
		}

		private static void Run(MonsterAI ai, float dt, Player from)
		{
			if (ModConfig.RunWhenAfraid.Value)
			{
				// Vanilla flee speed follows the alerted flag, so an un-alerted creature ambles away.
				ai.SetAlerted(true);
			}

			ai.Flee(dt, from.transform.position);
			ai.m_targetCreature = null;
			ai.m_updateTargetTimer = Mathf.Max(ai.m_updateTargetTimer, 1f);
		}

		public static void Forget(MonsterAI ai)
		{
			if (ai != null)
			{
				Broken.Remove(ai);
			}
		}

		public static void ClearAll()
		{
			Broken.Clear();
		}
	}
}
