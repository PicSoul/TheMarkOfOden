using System;
using HarmonyLib;
using MarkOfOden.Config;
using MarkOfOden.Fear;

namespace MarkOfOden.Patches
{
	/// <summary>
	/// Shows a creature's disposition on the name plate vanilla already draws above it.
	///
	/// A postfix on the hover name is deliberately the smallest possible footprint: it returns text and
	/// cannot change behaviour, and if another mod also postfixes this the two contributions compose
	/// rather than one winning. Patching EnemyHud itself would have meant reaching into a private
	/// nested type, in the one class HUD mods are most likely to be rewriting.
	/// </summary>
	[HarmonyPatch(typeof(Character), nameof(Character.GetHoverName))]
	public static class Character_GetHoverName_Patch
	{
		private static void Postfix(Character __instance, ref string __result)
		{
			try
			{
				__result = FearDisplay.Decorate(__instance, __result);
			}
			catch (Exception e)
			{
				Plugin.Log.LogError("Character.GetHoverName postfix failed: " + e);
			}
		}
	}

	/// <summary>
	/// Optionally widens the range at which name plates appear, because vanilla only shows them within
	/// 10m and a disposition you cannot read until the creature is on top of you is not much use.
	///
	/// This assigns a plain public field rather than patching how the HUD decides what to draw, so it
	/// adds no conflict surface: a mod that rewrites EnemyHud's logic is unaffected, and a mod that
	/// sets the same field simply wins or loses the assignment without anything breaking.
	/// </summary>
	[HarmonyPatch(typeof(EnemyHud), "Awake")]
	public static class EnemyHud_Awake_Patch
	{
		private static void Postfix(EnemyHud __instance)
		{
			try
			{
				Apply(__instance);
			}
			catch (Exception e)
			{
				Plugin.Log.LogError("EnemyHud.Awake postfix failed: " + e);
			}
		}

		public static void Apply(EnemyHud hud)
		{
			if (hud == null)
			{
				return;
			}

			float distance = ModConfig.NameplateDistance.Value;
			if (distance > 0f)
			{
				hud.m_maxShowDistance = distance;
			}
		}
	}
}
