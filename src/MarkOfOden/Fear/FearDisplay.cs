using System.Collections.Generic;
using MarkOfOden.Config;
using UnityEngine;

namespace MarkOfOden.Fear
{
	/// <summary>
	/// Turns a creature's disposition towards the local player into something readable above its head.
	///
	/// This asks how the creature feels about <em>you</em>, which is not necessarily the player its AI
	/// is currently reacting to, so it keeps its own small cache rather than reusing the evaluator's.
	///
	/// There is one scale here and it is written three times: the colour, the marker and the word all
	/// move together, so a plate can be read by whichever of the three you happen to notice first. That
	/// is not three axes to cross-reference, which would leave unused combinations meaning nothing; it
	/// is the same rung said three ways, which is what keeps it legible at a distance, without colour
	/// vision, and on the very first creature you meet.
	/// </summary>
	public static class FearDisplay
	{
		// Green to cyan to pale ice as a creature comes apart, and one warm colour for the state where
		// it will still fight. The ramp cools and pales in step with the markers, so the colour answers
		// "how far gone" on its own and nobody has to count characters at thirty metres. Vanilla's own
		// meanings are avoided: red is a bad state, orange a value, yellow a keybind.
		private static readonly string[] DefaultColours = { "#9BD46A", "#5FC9D6", "#B8DCEA", "#E8A33C", "#E8A33C" };

		private const int ProvokedState = 3;
		private const int UnafraidState = 4;

		// Direction carries the meaning: down for a creature putting distance between it and you, up
		// for one coming at you. The first three repeat one character so the marker alone still reads
		// as a scale. Plain ASCII on purpose, because the name plate's font is not guaranteed to have
		// a glyph for the arrow characters and a missing one draws as an empty box.
		private static readonly string[] DefaultSymbols = { "\u25BC", "\u25BC\u25BC", "\u25BC\u25BC\u25BC", "\u25B2", "\u25B2" };

		// What the creature is about to do, not what it feels. "Afraid" is a state of mind and leaves
		// you to work out the consequence; "fleeing" is the consequence, and is what you are actually
		// looking at the plate to find out.
		private static readonly string[] DefaultLabels = { "wary", "fleeing", "panicked", "cornered", "unafraid" };

		private sealed class Cached
		{
			public float NextEvaluation;
			public int Level;
		}

		private static readonly Dictionary<MonsterAI, Cached> Cache = new Dictionary<MonsterAI, Cached>();

		private static readonly SplitCache Colours = new SplitCache();
		private static readonly SplitCache Symbols = new SplitCache();
		private static readonly SplitCache Labels = new SplitCache();

		/// <summary>Splits a config string once per change, since this runs per creature per frame.</summary>
		private sealed class SplitCache
		{
			private string _source;
			private string[] _parts;

			public string[] Get(string raw)
			{
				raw = raw ?? string.Empty;
				if (_parts == null || raw != _source)
				{
					_source = raw;
					_parts = raw.Split(',');
				}

				return _parts;
			}
		}

		/// <summary>
		/// Decorates a creature's hover name with its disposition, or returns it untouched.
		/// A creature that feels nothing unusual is left exactly as vanilla renders it.
		/// </summary>
		public static string Decorate(Character creature, string name)
		{
			if (!ModConfig.Enabled.Value || !ModConfig.ShowFearOnNameplate.Value || string.IsNullOrEmpty(name))
			{
				return name;
			}

			int state = Evaluate(creature);
			if (state < 0)
			{
				return name;
			}

			string suffix = Suffix(state);
			string marked = name + (suffix.Length > 0 ? " " + suffix : string.Empty);

			if (!ModConfig.ColourNames.Value)
			{
				// Marker only. An inline colour tag beats whatever colour another mod set on the text
				// component, so leaving it off is how those mods keep working alongside this one.
				return marked;
			}

			// The name is still a localisation token at this point; the HUD localises afterwards, and
			// the tags around it are left alone by that pass.
			string colour = Entry(Colours, ModConfig.FearColours.Value, state, DefaultColours);
			return "<color=" + colour + ">" + marked + "</color>";
		}

		/// <summary>
		/// The display state: 0 wary, 1 broken and running, 3 provoked, 4 unafraid, or -1 for nothing to
		/// show. State 2 belonged to a panicked state that no longer exists; its slot is kept so the lists
		/// in older configs still line up.
		///
		/// Provoked is its own state rather than being lumped in with normal. A creature fighting back
		/// because you hit it behaves exactly like an unafraid one, so without marking it an unmarked
		/// name plate would mean both "does not care about you" and "is angry at you right now". It is
		/// the one state whose marker points the other way, which is what makes it read as the opposite
		/// thing rather than as a fourth step.
		/// </summary>
		private static int Evaluate(Character creature)
		{
			Player player = Player.m_localPlayer;
			if (player == null || creature == null || creature.IsPlayer() || creature.IsTamed() || creature.IsBoss())
			{
				return -1;
			}

			if (!(creature.GetBaseAI() is MonsterAI ai))
			{
				return -1;
			}

			if (!Cache.TryGetValue(ai, out Cached cached))
			{
				cached = new Cached();
				Cache[ai] = cached;
			}

			if (Time.time < cached.NextEvaluation)
			{
				return cached.Level;
			}

			cached.NextEvaluation = Time.time + 0.5f;

			FearLevel level = FearEvaluator.Evaluate(ai, player, out FearEvaluator.Immunity immunity);
			if (level >= FearLevel.Cautious)
			{
				cached.Level = (int)level - 1;
			}
			else if (immunity == FearEvaluator.Immunity.Retaliating)
			{
				cached.Level = ProvokedState;
			}
			else if (MarkAsUnafraid(creature, immunity))
			{
				cached.Level = UnafraidState;
			}
			else
			{
				cached.Level = -1;
			}

			return cached.Level;
		}

		/// <summary>
		/// Whether a creature that is not afraid of you should say so.
		///
		/// An unmarked plate is not the absence of a statement, it is the statement "nothing here has
		/// changed", and a player reads that as safe. That is true of a deer, which has no attack and
		/// would run from you in the base game anyway. It is badly wrong about a lox, which is equally
		/// food, equally unafraid, and will kill you for walking up to it. So the two are split by the
		/// only thing that actually differs: whether the creature can hit back.
		///
		/// The mod being off is not a disposition, so a creature is never marked on the strength of it;
		/// the same goes for tables that have not finished loading, where nothing is known yet.
		/// </summary>
		private static bool MarkAsUnafraid(Character creature, FearEvaluator.Immunity immunity)
		{
			if (immunity == FearEvaluator.Immunity.Disabled || immunity == FearEvaluator.Immunity.TablesNotReady)
			{
				return false;
			}

			if (!CreatureTiers.IsArmed(creature))
			{
				return false;
			}

			switch (ModConfig.MarkUnafraid.Value)
			{
				case UnafraidMarking.Armed:
					return true;

				case UnafraidMarking.FoodSources:
					return CreatureTiers.IsFoodSource(creature);

				default:
					return false;
			}
		}

		/// <summary>
		/// The marker, the word, or both, according to the configured style. Both are joined with a
		/// single space so the marker reads as a prefix to the word rather than as part of the name.
		/// </summary>
		private static string Suffix(int state)
		{
			switch (ModConfig.NameplateLabels.Value)
			{
				case NameplateStyle.Marker:
					return Entry(Symbols, ModConfig.FearSymbols.Value, state, DefaultSymbols);

				case NameplateStyle.Word:
					return Entry(Labels, ModConfig.FearLabels.Value, state, DefaultLabels);

				default:
					string marker = Entry(Symbols, ModConfig.FearSymbols.Value, state, DefaultSymbols);
					string word = Entry(Labels, ModConfig.FearLabels.Value, state, DefaultLabels);

					if (marker.Length == 0) { return word; }
					if (word.Length == 0) { return marker; }

					return marker + " " + word;
			}
		}

		/// <summary>Reads one comma separated entry, falling back to the built-in value.</summary>
		private static string Entry(SplitCache cache, string raw, int index, string[] fallback)
		{
			string[] parts = cache.Get(raw);
			if (index >= 0 && index < parts.Length)
			{
				string value = parts[index].Trim();
				if (value.Length > 0)
				{
					return value;
				}
			}

			return index >= 0 && index < fallback.Length ? fallback[index] : string.Empty;
		}

		public static void Forget(MonsterAI ai)
		{
			if (ai != null)
			{
				Cache.Remove(ai);
			}
		}

		public static void ClearAll()
		{
			Cache.Clear();
		}
	}
}
