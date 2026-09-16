using System;

namespace MarkOfOden.Compat
{
	/// <summary>
	/// Optional Jotunn integration. Jotunn is a soft dependency on purpose: the mod is fully functional
	/// without it, so nobody has to install a framework to use it, but if it is already loaded the mod
	/// registers its strings with Jotunn's localisation so translations can be dropped in.
	///
	/// Everything here is reflection-only. Referencing Jotunn types directly would make the assembly
	/// fail to load when Jotunn is absent.
	/// </summary>
	public static class JotunnSoft
	{
		/// <summary>BetterUI recolours creature names by alert state, which overlaps with the fear tint.</summary>
		private const string BetterUiGuid = "MK_BetterUI";

		/// <summary>Creature Level and Loot Control sets the same name plate distance field.</summary>
		private const string CllcGuid = "org.bepinex.plugins.creaturelevelcontrol";

		public static bool Available { get; private set; }

		public static void Init()
		{
			WarnAboutNameColourOverlap();
			WarnAboutNameplateDistanceOverlap();

			try
			{
				Type localizationManager = Type.GetType("Jotunn.Managers.LocalizationManager, Jotunn", throwOnError: false);
				Available = localizationManager != null;

				Plugin.Log.LogInfo(Available
					? "Jotunn detected; localisation will be registered through it."
					: "Jotunn not present; running standalone.");
			}
			catch (Exception e)
			{
				Available = false;
				Plugin.Log.LogWarning("Jotunn detection failed, continuing standalone: " + e.Message);
			}
		}

		/// <summary>
		/// Points out that another mod owns the name plate distance too.
		///
		/// Both assign the same public field from a postfix on the same method, so whichever runs last
		/// wins and the order is not defined. Left at 0 this mod never assigns it and there is nothing
		/// to collide with, which is why that is the default.
		/// </summary>
		private static void WarnAboutNameplateDistanceOverlap()
		{
			try
			{
				if (Config.ModConfig.NameplateDistance.Value <= 0f)
				{
					return;
				}

				if (BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(CllcGuid))
				{
					Plugin.Log.LogWarning("Creature Level and Loot Control also sets the name plate distance, and whichever mod applies it last wins. "
						+ "Set this mod's 'Nameplate distance' back to 0 and use that mod's own range setting instead.");
				}
			}
			catch (Exception e)
			{
				Plugin.Log.LogWarning("Could not check for name plate distance overlap: " + e.Message);
			}
		}

		/// <summary>
		/// Points out a mod that is actively using the creature name's colour for something else.
		///
		/// An inline colour tag wins over a colour set on the text component, so this mod would quietly
		/// override it. This checks the other mod's setting rather than merely whether it is installed,
		/// because the feature is optional there and warning regardless would be advice to fix a
		/// problem the player does not have.
		/// </summary>
		private static void WarnAboutNameColourOverlap()
		{
			try
			{
				if (!Config.ModConfig.ShowFearOnNameplate.Value || !Config.ModConfig.ColourNames.Value)
				{
					return;
				}

				if (!BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(BetterUiGuid, out BepInEx.PluginInfo info) || info?.Instance == null)
				{
					return;
				}

				if (!info.Instance.Config.TryGetEntry(new BepInEx.Configuration.ConfigDefinition("6 - Enemy HUD", "useCustomAlertedStatus"),
					out BepInEx.Configuration.ConfigEntry<bool> entry) || !entry.Value)
				{
					return;
				}

				Plugin.Log.LogInfo("BetterUI is colouring creature names by alert state, and this mod colours them by fear. "
					+ "Two ways to keep both signals: set BetterUI's useCustomAlertedStatus to false and let it use the vanilla alert icons, "
					+ "or set this mod's 'Colour names' to false and keep only its marker.");
			}
			catch (Exception e)
			{
				Plugin.Log.LogWarning("Could not check for name colour overlap: " + e.Message);
			}
		}

	}
}
