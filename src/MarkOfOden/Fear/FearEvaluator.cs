using System.Collections.Generic;
using System.Text;
using MarkOfOden.Compat;
using MarkOfOden.Config;
using MarkOfOden.Marks;
using UnityEngine;

namespace MarkOfOden.Fear
{
	/// <summary>
	/// The single place where a creature decides what it thinks of a player.
	/// Everything else in the mod either feeds this or acts on its answer.
	/// </summary>
	public static class FearEvaluator
	{
		/// <summary>Why a creature is not afraid, for the console command and debug logging.</summary>
		public enum Immunity
		{
			None,
			Disabled,
			TablesNotReady,
			Tamed,
			Boss,
			Raid,
			BossFight,
			Hunting,
			ConfiguredFearless,
			BeingTamed,
			Retaliating,
			NoMark
		}

		private sealed class CachedDecision
		{
			public float NextEvaluation;
			public FearLevel Level;
			public Player Target;
		}

		private static readonly Dictionary<MonsterAI, CachedDecision> Decisions = new Dictionary<MonsterAI, CachedDecision>();

		/// <summary>When each player last hurt each creature. Per attacker, so one player's fight is not another's.</summary>
		private static readonly Dictionary<MonsterAI, Dictionary<Player, float>> LastHurtAt = new Dictionary<MonsterAI, Dictionary<Player, float>>();

		private static readonly Dictionary<MonsterAI, float> NextHelpCall = new Dictionary<MonsterAI, float>();

		private const float HelpCallInterval = 1f;

		/// <summary>Records that a specific player hurt this creature, so it can fight that player back.</summary>
		public static void NoteHurtByPlayer(MonsterAI ai, Player attacker)
		{
			if (ai == null || attacker == null)
			{
				return;
			}

			MarkAngry(ai, attacker);
			CallForHelp(ai, attacker);
		}

		private static void MarkAngry(MonsterAI ai, Player attacker)
		{
			if (!LastHurtAt.TryGetValue(ai, out Dictionary<Player, float> byPlayer))
			{
				byPlayer = new Dictionary<Player, float>();
				LastHurtAt[ai] = byPlayer;
			}

			byPlayer[attacker] = Time.time;

			// Fear decisions are cached for a fraction of a second. Getting hit has to land now, or a
			// creature keeps fleeing for another half second after it should have turned on you.
			if (Decisions.TryGetValue(ai, out CachedDecision cached))
			{
				cached.NextEvaluation = 0f;
			}
		}

		/// <summary>
		/// Brings the neighbours in. A creature that has decided you are too dangerous to provoke will
		/// still not stand and watch while you fight the one beside it, so anything of its own kind
		/// close enough to hear joins in on the same terms.
		///
		/// Centred on the creature that was hit rather than on the player, so shooting something from
		/// across a clearing rallies its own packmates rather than whatever happens to be near you.
		///
		/// Same faction only: a boar has no stake in a Greydwarf's fight.
		///
		/// This runs on whichever client owns the creature that was hit, and marks its neighbours in
		/// that client's own records. In practice a pack and its victim are near the same player and so
		/// owned together; a neighbour owned by a different client would not hear the call.
		/// </summary>
		private static void CallForHelp(MonsterAI victim, Player attacker)
		{
			float radius = ModConfig.HelpCallRadius.Value;
			if (radius <= 0f || !ModConfig.CorneredCreaturesFightBack.Value)
			{
				return;
			}

			// Combat produces a damage event per hit, so the scan is rate limited per victim rather
			// than run for every blow. It still refreshes the neighbours while a fight continues.
			if (NextHelpCall.TryGetValue(victim, out float next) && Time.time < next)
			{
				return;
			}

			NextHelpCall[victim] = Time.time + HelpCallInterval;

			Character hurt = victim.m_character;
			if (hurt == null)
			{
				return;
			}

			Character.Faction faction = hurt.GetFaction();
			Vector3 position = hurt.transform.position;
			float radiusSqr = radius * radius;

			foreach (Character other in Character.GetAllCharacters())
			{
				if (other == null || other == hurt || other.IsDead() || other.IsPlayer() || other.IsTamed())
				{
					continue;
				}

				if (other.GetFaction() != faction)
				{
					continue;
				}

				if ((other.transform.position - position).sqrMagnitude > radiusSqr)
				{
					continue;
				}

				if (other.GetBaseAI() is MonsterAI ally)
				{
					MarkAngry(ally, attacker);
				}
			}
		}

		/// <summary>True while this creature is still angry at this particular player.</summary>
		private static bool IsRetaliatingAgainst(MonsterAI ai, Player player)
		{
			return LastHurtAt.TryGetValue(ai, out Dictionary<Player, float> byPlayer)
				&& byPlayer.TryGetValue(player, out float hurtAt)
				&& Time.time - hurtAt <= ModConfig.RetaliationWindow.Value;
		}

		public static void Forget(MonsterAI ai)
		{
			if (ai == null)
			{
				return;
			}

			Decisions.Remove(ai);
			LastHurtAt.Remove(ai);
			NextHelpCall.Remove(ai);
		}

		public static void ClearAll()
		{
			Decisions.Clear();
			LastHurtAt.Clear();
			NextHelpCall.Clear();
		}

		/// <summary>
		/// Throttled entry point used by the AI patches. Returns the creature's current feeling about
		/// the nearest player it can actually sense, and that player.
		/// </summary>
		public static FearLevel GetCurrent(MonsterAI ai, out Player target)
		{
			target = null;
			if (ai == null)
			{
				return FearLevel.Normal;
			}

			if (!Decisions.TryGetValue(ai, out CachedDecision cached))
			{
				cached = new CachedDecision();
				Decisions[ai] = cached;
			}

			if (Time.time < cached.NextEvaluation)
			{
				target = cached.Target;
				return cached.Level;
			}

			// Spread re-evaluations out so a whole pack does not recompute on the same frame.
			float interval = Mathf.Max(0.05f, ModConfig.ReevaluateInterval.Value);
			cached.NextEvaluation = Time.time + interval * Random.Range(0.85f, 1.15f);

			Player nearest = FindSensedPlayer(ai);
			cached.Target = nearest;
			cached.Level = nearest == null ? FearLevel.Normal : Evaluate(ai, nearest, out _);

			target = cached.Target;
			return cached.Level;
		}

		/// <summary>Un-throttled evaluation against a specific player. Used by FindEnemy and by 'moo why'.</summary>
		public static FearLevel Evaluate(MonsterAI ai, Player player, out Immunity immunity)
		{
			immunity = Immunity.None;

			if (!ModConfig.Enabled.Value)
			{
				immunity = Immunity.Disabled;
				return FearLevel.Normal;
			}

			if (!CreatureTiers.Ready)
			{
				immunity = Immunity.TablesNotReady;
				return FearLevel.Normal;
			}

			if (ai == null || player == null || ai.m_character == null)
			{
				return FearLevel.Normal;
			}

			Character creature = ai.m_character;

			if (creature.IsTamed() || creature.IsPlayer())
			{
				immunity = Immunity.Tamed;
				return FearLevel.Normal;
			}

			if (creature.IsBoss() || creature.GetFaction() == Character.Faction.Boss)
			{
				immunity = Immunity.Boss;
				return FearLevel.Normal;
			}

			// Raids must not break. A raid whose creatures run away is not a raid.
			if (ModConfig.RaidCreaturesAlwaysAttack.Value && ai.IsEventCreature() && RandEventSystem.HaveActiveEvent())
			{
				immunity = Immunity.Raid;
				return FearLevel.Normal;
			}

			// Creatures fighting alongside a boss keep fighting. A boss whose summons lose interest
			// partway through is not a boss fight.
			if (BossFight.IsInBossFight(creature.transform.position))
			{
				immunity = Immunity.BossFight;
				return FearLevel.Normal;
			}

			// Anything told to hunt the player is on a mission: event hunters and scripted attackers.
			if (ai.HuntPlayer())
			{
				immunity = Immunity.Hunting;
				return FearLevel.Normal;
			}

			if (CreatureTiers.IsFearless(creature))
			{
				immunity = Immunity.ConfiguredFearless;
				return FearLevel.Normal;
			}

			// An animal that has taken your food is already being tamed. Taming only advances while it
			// is calm, so a creature this mod frightened could never finish, and one it talked down
			// would finish without the player ever having to leave it alone. Both are wrong, so a
			// creature in the middle of the process is left to behave exactly as the base game intends.
			if (ai.m_tamable != null && !ai.m_tamable.IsHungry())
			{
				immunity = Immunity.BeingTamed;
				return FearLevel.Normal;
			}

			float threat = Threat(player, creature);
			if (threat <= 0f)
			{
				immunity = Immunity.NoMark;
				return FearLevel.Normal;
			}

			float delta = threat - Courage(ai, creature);

			FearLevel level;
			if (delta >= ModConfig.TerrifiedThreshold.Value) level = FearLevel.Terrified;
			else if (delta >= ModConfig.AfraidThreshold.Value) level = FearLevel.Afraid;
			else if (delta >= ModConfig.CautiousThreshold.Value) level = FearLevel.Cautious;
			else level = FearLevel.Normal;

			// Some creatures may lose interest in you but never break and run. A boar that bolts turns
			// hunting into a chase, and a boar that charges a Yagluth-slayer looks absurd; standing
			// there ignoring you is the only reading that is neither.
			if (level > FearLevel.Cautious && CreatureTiers.NeverFlees(creature))
			{
				level = FearLevel.Cautious;
			}

			// Anything you hit defends itself, however frightened it was a moment ago. Without this,
			// every creature that fears you has to be chased down to be killed, which makes hunting
			// for meat and hides a chore exactly when your mark is high enough to make it trivial.
			if (ModConfig.CorneredCreaturesFightBack.Value && IsRetaliatingAgainst(ai, player))
			{
				immunity = Immunity.Retaliating;
				return FearLevel.Normal;
			}

			return level;
		}

		public static float Threat(Player player, Character creature)
		{
			return MarkSync.GetTier(player) + MarkSync.GetNotoriety(player, creature.m_name);
		}

		public static float Courage(MonsterAI ai, Character creature)
		{
			float courage = CreatureTiers.GetTier(creature);

			// Capped because level mods raise creatures far past vanilla's three stars. Uncapped, an
			// eight-level creature would draw more courage from stars alone than from everything else
			// combined, and fear would quietly stop happening on those servers.
			float stars = Mathf.Max(0, creature.GetLevel() - 1) * ModConfig.StarCourage.Value;
			courage += Mathf.Min(stars, ModConfig.StarCourageMax.Value);

			courage += PackBonus(ai, creature);

			// A creature another mod has empowered is not the creature its prefab describes.
			courage += CllcCompat.ExtraCourage(creature);

			return courage;
		}

		/// <summary>Allies nearby make a creature brave; the last one standing breaks and runs.</summary>
		public static float PackBonus(MonsterAI ai, Character creature)
		{
			float perAlly = ModConfig.PackCourage.Value;
			float cap = ModConfig.PackCourageMax.Value;
			if (perAlly <= 0f || cap <= 0f)
			{
				return 0f;
			}

			float radiusSqr = ModConfig.PackRadius.Value * ModConfig.PackRadius.Value;
			Vector3 position = creature.transform.position;
			Character.Faction faction = creature.GetFaction();

			int allies = 0;
			foreach (Character other in Character.GetAllCharacters())
			{
				if (other == null || other == creature || other.IsDead() || other.IsPlayer() || other.IsTamed())
				{
					continue;
				}

				if (other.GetFaction() != faction)
				{
					continue;
				}

				if ((other.transform.position - position).sqrMagnitude <= radiusSqr)
				{
					allies++;
				}
			}

			return Mathf.Min(allies * perAlly, cap);
		}

		/// <summary>
		/// The player this creature should be judging.
		///
		/// A creature that already has a player targeted judges that one, sensed or not: vanilla holds
		/// an acquired target until it gives up, so fear has to be able to reach an existing target and
		/// not just block new ones. Otherwise anything that spotted you before it grew afraid keeps
		/// fighting indefinitely, because attacking keeps resetting the give-up timers.
		/// </summary>
		private static Player FindSensedPlayer(MonsterAI ai)
		{
			if (ai.m_targetCreature is Player targeted && !targeted.IsDead())
			{
				return targeted;
			}

			float range = Mathf.Min(ModConfig.ScanRange.Value, Mathf.Max(ai.m_viewRange, ai.m_hearRange));
			Player closest = Player.GetClosestPlayer(ai.transform.position, range);
			if (closest == null || closest.IsDead() || closest.InGhostMode() || closest.InDebugFlyMode())
			{
				return null;
			}

			return ai.CanSenseTarget(closest) ? closest : null;
		}

		/// <summary>Human readable breakdown for the 'moo why' console command.</summary>
		public static string Explain(MonsterAI ai, Player player)
		{
			if (ai == null || ai.m_character == null || player == null)
			{
				return "No creature or no player to compare.";
			}

			Character creature = ai.m_character;
			FearLevel level = Evaluate(ai, player, out Immunity immunity);

			StringBuilder builder = new StringBuilder();
			builder.AppendLine(creature.m_name + " (level " + creature.GetLevel() + ") vs " + player.GetPlayerName());
			builder.AppendLine("  verdict: " + level + (immunity == Immunity.None ? string.Empty : " [immune: " + immunity + "]"));
			builder.AppendLine("  threat  = mark " + MarkSync.GetTier(player) + " + notoriety " + MarkSync.GetNotoriety(player, creature.m_name)
				+ " = " + Threat(player, creature));
			builder.AppendLine("  courage = tier " + CreatureTiers.GetTier(creature)
				+ " + stars " + Mathf.Min(Mathf.Max(0, creature.GetLevel() - 1) * ModConfig.StarCourage.Value, ModConfig.StarCourageMax.Value)
				+ " + pack " + PackBonus(ai, creature)
				+ (CllcCompat.ExtraCourage(creature) > 0f ? " + empowered " + CllcCompat.ExtraCourage(creature) : string.Empty)
				+ " = " + Courage(ai, creature));
			builder.AppendLine("  delta   = " + (Threat(player, creature) - Courage(ai, creature))
				+ " (cautious " + ModConfig.CautiousThreshold.Value
				+ ", afraid " + ModConfig.AfraidThreshold.Value
				+ ", terrified " + ModConfig.TerrifiedThreshold.Value + ")");
			builder.AppendLine("  senses the player: " + ai.CanSenseTarget(player));
			return builder.ToString();
		}
	}
}
