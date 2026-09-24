using System;
using HarmonyLib;
using MarkOfOden.Config;
using MarkOfOden.Fear;
using UnityEngine;

namespace MarkOfOden.Patches
{
	/// <summary>
	/// Stops a frightened creature from ever picking the player as a target.
	///
	/// This one postfix carries most of the behaviour: with no target the creature never alerts, never
	/// closes in and never attacks, without this mod having to suppress any of those individually.
	/// </summary>
	[HarmonyPatch(typeof(BaseAI), nameof(BaseAI.FindEnemy))]
	public static class BaseAI_FindEnemy_Patch
	{
		private static void Postfix(BaseAI __instance, ref Character __result)
		{
			try
			{
				if (!ModConfig.Enabled.Value || __result == null)
				{
					return;
				}

				if (!(__instance is MonsterAI ai) || !(__result is Player player))
				{
					return;
				}

				if (FearEvaluator.Evaluate(ai, player, out _) >= FearLevel.Cautious)
				{
					__result = null;
				}
			}
			catch (Exception e)
			{
				Plugin.Log.LogError("BaseAI.FindEnemy postfix failed: " + e);
			}
		}
	}

	/// <summary>
	/// Carries out what a creature has decided about you, after the original has run.
	///
	/// Two things happen here. A creature that is losing a fight to someone who outranks it may break
	/// and run - see <see cref="Morale"/>. And a creature that is not willing to fight you drops you as a
	/// target, which is all that leaving you alone takes. Nothing here makes a creature run on sight:
	/// that used to happen, and it made every rank you gained a switch thrown on whole biomes at once.
	///
	/// A postfix rather than a transpiler on purpose: FearMe reaches the same place by IL-matching
	/// m_fleeIfHurtWhenTargetCantBeReached inside UpdateAI, and that method changed shape in 1.0.
	/// Running afterwards is safe because base.UpdateAI has already done the ownership check and the
	/// per-frame timers, and because movement is last-write-wins within a frame.
	/// </summary>
	[HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.UpdateAI))]
	public static class MonsterAI_UpdateAI_Patch
	{
		private static void Postfix(MonsterAI __instance, float dt, ref bool __result)
		{
			try
			{
				// False means base.UpdateAI bailed out: not the owner, or an invalid view. Do nothing.
				if (!__result || !ModConfig.Enabled.Value)
				{
					return;
				}

				Character creature = __instance.m_character;
				if (creature == null || creature.IsDead() || __instance.IsSleeping())
				{
					return;
				}

				// A creature already in a fight stays in it, rather than timing out mid-swing.
				FearEvaluator.NoteStillFighting(__instance);

				// Losing a fight to someone who outranks it. Checked before anything else, because a
				// broken creature's only business is getting away.
				if (Morale.Tick(__instance, dt))
				{
					__result = true;
					return;
				}

				FearLevel level = FearEvaluator.GetCurrent(__instance, out Player player);
				if (level < FearLevel.Cautious || player == null)
				{
					return;
				}

				// Drop a target the creature is no longer willing to fight. Suppressing FindEnemy is not
				// enough on its own: vanilla only assigns m_targetCreature when FindEnemy returns
				// something, so a creature that had already acquired you would otherwise keep attacking.
				if (__instance.m_targetCreature == player)
				{
					__instance.m_targetCreature = null;
					__instance.SetAlerted(false);
				}
			}
			catch (Exception e)
			{
				Plugin.Log.LogError("MonsterAI.UpdateAI postfix failed: " + e);
			}
		}
	}
}
