using System;
using HarmonyLib;
using MarkOfOden.Config;
using MarkOfOden.Fear;
using MarkOfOden.Marks;

namespace MarkOfOden.Patches
{
	/// <summary>Builds the creature tier table once the prefab list exists.</summary>
	[HarmonyPatch(typeof(ZNetScene), "Awake")]
	public static class ZNetScene_Awake_Patch
	{
		private static void Postfix(ZNetScene __instance)
		{
			try
			{
				CreatureTiers.Build(__instance);
			}
			catch (Exception e)
			{
				Plugin.Log.LogError("Could not build the creature tier table: " + e);
			}
		}
	}

	/// <summary>
	/// Builds the creature table again once everything has finished loading.
	///
	/// The first pass runs on ZNetScene.Awake, but mod frameworks register their own creatures from a
	/// postfix on that same method, and Harmony gives no guaranteed order between two mods patching
	/// it. Unity runs Start after every Awake, so by here the prefab list is settled whoever added to
	/// it. Without this, whether modded creatures were known at all came down to plugin load order.
	/// </summary>
	[HarmonyPatch(typeof(Game), "Start")]
	public static class Game_Start_Patch
	{
		private static void Postfix()
		{
			try
			{
				CreatureTiers.Build(ZNetScene.instance);
			}
			catch (Exception e)
			{
				Plugin.Log.LogError("Could not rebuild the creature tier table: " + e);
			}
		}
	}

	/// <summary>Drops all world-scoped state when leaving a world, so nothing leaks into the next one.</summary>
	[HarmonyPatch(typeof(Game), "OnDestroy")]
	public static class Game_OnDestroy_Patch
	{
		private static void Postfix()
		{
			FearEvaluator.ClearAll();
			Morale.ClearAll();
			FearDisplay.ClearAll();
			BossFight.Clear();
			MarkSync.ClearCache();
		}
	}

	/// <summary>Loads the ledger for the character that just spawned and publishes their mark.</summary>
	[HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
	public static class Player_OnSpawned_Patch
	{
		private static void Postfix(Player __instance)
		{
			try
			{
				if (__instance != Player.m_localPlayer)
				{
					return;
				}

				MarkLedger.Load(__instance);
				MarkSync.Publish();
			}
			catch (Exception e)
			{
				Plugin.Log.LogError("Could not load the mark ledger on spawn: " + e);
			}
		}
	}

	/// <summary>Forgets a creature's cached fear when it is destroyed.</summary>
	[HarmonyPatch(typeof(BaseAI), "OnDestroy")]
	public static class BaseAI_OnDestroy_Patch
	{
		private static void Prefix(BaseAI __instance)
		{
			if (__instance is MonsterAI ai)
			{
				FearEvaluator.Forget(ai);
				Morale.Forget(ai);
				FearDisplay.Forget(ai);
			}
		}
	}

	/// <summary>
	/// Records that a player hurt this creature, so it turns and fights instead of being farmed.
	///
	/// This has to hook RPC_Damage rather than Damage: Damage runs on the attacking client, while the
	/// AI that reads this flag runs on the client that owns the creature. In multiplayer those are
	/// routinely different machines, and hooking the wrong one makes creatures forget they were hit.
	/// </summary>
	[HarmonyPatch(typeof(Character), "RPC_Damage")]
	public static class Character_RPC_Damage_Patch
	{
		private static void Postfix(Character __instance, HitData hit)
		{
			try
			{
				if (!ModConfig.Enabled.Value || __instance == null || hit == null)
				{
					return;
				}

				if (__instance.m_nview == null || !__instance.m_nview.IsValid() || !__instance.m_nview.IsOwner())
				{
					return;
				}

				if (!(hit.GetAttacker() is Player attacker))
				{
					return;
				}

				if (__instance.GetBaseAI() is MonsterAI ai)
				{
					FearEvaluator.NoteHurtByPlayer(ai, attacker);
				}
			}
			catch (Exception e)
			{
				Plugin.Log.LogError("Character.RPC_Damage postfix failed: " + e);
			}
		}
	}

	/// <summary>
	/// Where the mark is actually earned.
	///
	/// Valheim 1.0 tracks which players attacked a creature and, on its death, tells each of them
	/// what they killed - on their own client, with the creature name and the boss number from the
	/// game's own 1-8 ladder. That is participation credit, already networked and already tested, so
	/// this mod reads it rather than reimplementing it: no custom RPC, no guessing at boss key names,
	/// and a support player who never landed the killing blow is credited just like the one who did.
	/// </summary>
	[HarmonyPatch(typeof(Game), nameof(Game.RPC_RegisterKill))]
	public static class Game_RPC_RegisterKill_Patch
	{
		private static void Postfix(string enemyName, int bossNumber, bool cheatsUsed)
		{
			try
			{
				if (!ModConfig.Enabled.Value)
				{
					return;
				}

				// Valheim flags creatures that were spawned in. A boss you conjured is not a boss you beat.
				if (cheatsUsed)
				{
					return;
				}

				if (bossNumber > 0)
				{
					MarkLedger.CreditBoss(bossNumber);
					ProgressPopup.ShowBossDefeated(bossNumber, MarkLedger.Tier);
					return;
				}

				// Species kills are counted by the game itself, in the same call that got us here.
				// All this has to do is republish when the count crosses a notoriety threshold.
				int kills = VanillaKillStats.KillsOf(enemyName);
				int oldNotoriety = MarkLedger.NotorietyForKills(kills - 1);
				int newNotoriety = MarkLedger.NotorietyForKills(kills);
				if (newNotoriety > oldNotoriety)
				{
					Plugin.Log.LogInfo(enemyName + " has learned to fear you (" + kills + " killed).");
					MarkSync.Publish();
					ProgressPopup.ShowNotorietyIncreased(enemyName, newNotoriety, kills);
				}
			}
			catch (Exception e)
			{
				Plugin.Log.LogError("Game.RPC_RegisterKill postfix failed: " + e);
			}
		}
	}
}
