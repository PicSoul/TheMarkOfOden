using System;
using System.Collections.Generic;
using System.Text;
using MarkOfOden.Config;
using MarkOfOden.Fear;
using MarkOfOden.Marks;
using UnityEngine;

namespace MarkOfOden
{
	/// <summary>
	/// The 'moo' console command. This is the tuning surface: fear is hard to reason about from the
	/// outside, so 'moo why' prints the whole arithmetic for the creature in front of you.
	/// </summary>
	public static class ConsoleCommands
	{
		private static bool _registered;

		public static void Register()
		{
			if (_registered)
			{
				return;
			}

			_registered = true;

			new Terminal.ConsoleCommand(
				"moo",
				"The Mark of Oden. Subcommands: status, why, dump, bosses, optout, optin, reset, tier <0-8>",
				args => Run(args),
				isCheat: false,
				isNetwork: false,
				onlyServer: false,
				isSecret: false,
				allowInDevBuild: true);
		}

		private static void Run(Terminal.ConsoleEventArgs args)
		{
			try
			{
				string sub = args.Length > 1 ? args[1].ToLowerInvariant() : "status";
				switch (sub)
				{
					case "status":
						Status(args.Context);
						break;
					case "why":
						Why(args.Context);
						break;
					case "tier":
						if (RequireAdmin(args.Context))
						{
							SetTier(args);
						}
						break;
					case "dump":
						Report(args.Context, CreatureTiers.DumpTiers());
						break;
					case "bosses":
						Report(args.Context, CreatureTiers.DumpBosses());
						break;
					case "optout":
						SetOptedOut(args.Context, true);
						break;
					case "optin":
						SetOptedOut(args.Context, false);
						break;
					case "reset":
						// Not gated: this only ever recalculates your own mark, and cannot raise it
						// above what the character has actually earned. There is nothing to exploit.
						Rebuild(args.Context);
						break;
					default:
						args.Context.AddString("Unknown subcommand. Try: status, why, dump, bosses, optout, optin, reset, tier <0-8>");
						break;
				}
			}
			catch (Exception e)
			{
				args.Context.AddString("moo failed: " + e.Message);
				Plugin.Log.LogError("Console command failed: " + e);
			}
		}

		/// <summary>
		/// Gate for the subcommands that change your mark. A client on a server could otherwise type
		/// themselves a tier 8 mark and make the world flee, since the mark you publish is what other
		/// players' creatures read.
		///
		/// Server admin rather than devcommands on purpose: devcommands exists to enable cheats, and
		/// nothing here is one. Solo or hosting, you are the source of truth and this always passes.
		/// </summary>
		/// <summary>
		/// Prints to the console and to the log both. The log is the copy that survives being read
		/// later, which matters because these commands exist to explain behaviour someone has just
		/// seen and wants to show to someone else.
		/// </summary>
		private static void Report(Terminal context, string text)
		{
			context.AddString(text);
			Plugin.Log.LogInfo(text);
		}

		private static bool RequireAdmin(Terminal context)
		{
			if (ModConfig.Sync == null || ModConfig.Sync.IsAdmin)
			{
				return true;
			}

			context.AddString("That changes your mark, so it is limited to the server admin.");
			return false;
		}

		/// <summary>
		/// Turns this character's mark off or back on. Not gated: opting out only ever makes a player
		/// less frightening, and it is their own character.
		/// </summary>
		private static void SetOptedOut(Terminal context, bool optedOut)
		{
			if (Player.m_localPlayer == null)
			{
				context.AddString("No local player.");
				return;
			}

			if (MarkLedger.OptedOut == optedOut)
			{
				context.AddString(optedOut ? "Already opted out." : "Already opted in.");
				return;
			}

			MarkLedger.SetOptedOut(optedOut);
			MarkSync.Publish();
			FearEvaluator.ClearAll();
			FearDisplay.ClearAll();

			Report(context, optedOut
				? "Opted out. Nothing will fear you, and your mark is kept for when you opt back in."
				: "Opted back in. Your mark is tier " + MarkLedger.Tier + " again.");
		}

		private static void Rebuild(Terminal context)
		{
			Player player = Player.m_localPlayer;
			if (player == null)
			{
				context.AddString("No local player.");
				return;
			}

			int before = MarkLedger.Tier;
			MarkLedger.ForcedTier = -1;
			MarkLedger.Rebuild(player);
			MarkSync.Publish();
			FearEvaluator.ClearAll();

			Report(context, "Mark rebuilt from this character's history: tier " + before + " -> " + MarkLedger.Tier + ".");
		}

		private static void Status(Terminal context)
		{
			Player player = Player.m_localPlayer;
			if (player == null)
			{
				context.AddString("No local player.");
				return;
			}

			StringBuilder builder = new StringBuilder();
			builder.AppendLine("Mark of Oden: " + (ModConfig.Enabled.Value ? "enabled" : "DISABLED")
				+ (MarkLedger.OptedOut ? "  - you are OPTED OUT, nothing fears you" : string.Empty));
			builder.AppendLine("  mark tier: " + MarkLedger.Tier + (MarkLedger.ForcedTier >= 0 ? " (forced)" : string.Empty));
			builder.AppendLine("  published tier: " + MarkSync.GetTier(player));
			List<int> bosses = new List<int>(MarkLedger.AllBossNumbers);
			bosses.Sort();
			builder.AppendLine("  bosses felled: " + (bosses.Count == 0
				? "none"
				: string.Join(", ", bosses.ConvertAll(b => b.ToString()).ToArray())));

			List<KeyValuePair<string, float>> top = new List<KeyValuePair<string, float>>(VanillaKillStats.All());
			top.Sort((a, b) => b.Value.CompareTo(a.Value));
			builder.AppendLine("  species killed: " + top.Count);
			for (int i = 0; i < Mathf.Min(8, top.Count); i++)
			{
				int kills = (int)top[i].Value;
				builder.AppendLine("    " + top[i].Key + " x" + kills + " (notoriety " + MarkLedger.NotorietyForKills(kills) + ")");
			}

			Report(context, builder.ToString());
		}

		private static void Why(Terminal context)
		{
			Player player = Player.m_localPlayer;
			if (player == null)
			{
				context.AddString("No local player.");
				return;
			}

			MonsterAI nearest = null;
			float best = float.MaxValue;
			foreach (Character character in Character.GetAllCharacters())
			{
				if (character == null || character.IsPlayer() || character.IsDead())
				{
					continue;
				}

				if (!(character.GetBaseAI() is MonsterAI ai))
				{
					continue;
				}

				float distance = Vector3.Distance(character.transform.position, player.transform.position);
				if (distance < best)
				{
					best = distance;
					nearest = ai;
				}
			}

			if (nearest == null)
			{
				context.AddString("No creature nearby to ask.");
				return;
			}

			string explanation = FearEvaluator.Explain(nearest, player) + "  distance: " + best.ToString("F1") + "m";
			Report(context, explanation);
		}

		private static void SetTier(Terminal.ConsoleEventArgs args)
		{
			if (args.Length < 3 || !int.TryParse(args[2], out int tier))
			{
				args.Context.AddString("Usage: moo tier <0-" + CreatureTiers.MaxTier + ">, or -1 to go back to the real ledger.");
				return;
			}

			MarkLedger.ForcedTier = tier < 0 ? -1 : Mathf.Clamp(tier, 0, CreatureTiers.MaxTier);
			MarkSync.Publish();
			FearEvaluator.ClearAll();

			args.Context.AddString(MarkLedger.ForcedTier < 0
				? "Forced tier cleared; back to earned marks (tier " + MarkLedger.Tier + ")."
				: "Mark tier forced to " + MarkLedger.ForcedTier + ".");
		}
	}
}
