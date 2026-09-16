using MarkOfOden.Config;

namespace MarkOfOden.Compat
{
	/// <summary>
	/// Reads what Creature Level and Loot Control has done to a creature, so a creature it has made
	/// genuinely deadlier does not flee like the ordinary version of itself.
	///
	/// This reads the creature's ZDO directly rather than calling that mod's API. Its own API methods
	/// do exactly the same read - HasInfusionCreature is a one-line ZDO lookup - so going straight to
	/// the data costs nothing and avoids binding to types that may not be loaded, may move between
	/// versions, or may belong to a build compiled against a different game version. With no such mod
	/// present the keys are simply absent and everything reports none.
	///
	/// It also means this works for a creature owned by another client, since ZDO data is what travels.
	/// </summary>
	public static class CllcCompat
	{
		// The key names are part of that mod's saved data, so they are as stable as the saves it writes.
		private static readonly int InfusionHash = "CL&LC infusion".GetStableHashCode();
		private static readonly int EffectHash = "CL&LC effect".GetStableHashCode();

		private const int None = -1;

		/// <summary>Extra courage from anything another mod has done to make this creature deadlier.</summary>
		public static float ExtraCourage(Character creature)
		{
			ZDO zdo = GetZdo(creature);
			if (zdo == null)
			{
				return 0f;
			}

			float courage = 0f;

			if (zdo.GetInt(InfusionHash, None) != None)
			{
				courage += ModConfig.InfusionCourage.Value;
			}

			if (zdo.GetInt(EffectHash, None) != None)
			{
				courage += ModConfig.SpecialEffectCourage.Value;
			}

			return courage;
		}

		/// <summary>True when this creature has been altered in a way that makes it braver.</summary>
		public static bool IsEmpowered(Character creature)
		{
			ZDO zdo = GetZdo(creature);
			return zdo != null && (zdo.GetInt(InfusionHash, None) != None || zdo.GetInt(EffectHash, None) != None);
		}

		private static ZDO GetZdo(Character creature)
		{
			if (creature == null || creature.m_nview == null || !creature.m_nview.IsValid())
			{
				return null;
			}

			return creature.m_nview.GetZDO();
		}
	}
}
