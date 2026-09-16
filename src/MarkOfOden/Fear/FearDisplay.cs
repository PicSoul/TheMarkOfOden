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
	/// Colour and symbol both carry the meaning, so the state is still legible without colour vision.
	/// </summary>
	public static class FearDisplay
	{
		// One colour for every state where the creature is backing off, and a different one for the
		// state where it will still fight. Varying colour and marker independently made them look like
		// two separate axes, which invites the question of what an unused combination would mean.
		// Vanilla's own meanings are avoided: red is a bad state, orange a value, yellow a keybind.
		private static readonly string[] DefaultColours = { "#8FC97A", "#8FC97A", "#8FC97A", "#E8A33C" };

		private const int CorneredState = 3;

		// The first three repeat one character so the marker alone reads as a scale.
		private static readonly string[] DefaultSymbols = { "<", "<<", "<<<", "!" };

		private sealed class Cached
		{
			public float NextEvaluation;
			public int Level;
		}

		private static readonly Dictionary<MonsterAI, Cached> Cache = new Dictionary<MonsterAI, Cached>();

		private static readonly SplitCache Colours = new SplitCache();
		private static readonly SplitCache Symbols = new SplitCache();

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

			string symbol = Entry(Symbols, ModConfig.FearSymbols.Value, state, DefaultSymbols);
			string marked = name + (symbol.Length > 0 ? " " + symbol : string.Empty);

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
		/// The display state: 0 cautious, 1 afraid, 2 terrified, 3 cornered, or -1 for nothing to show.
		///
		/// Cornered is its own state rather than being lumped in with normal. A creature fighting back
		/// because you hit it behaves exactly like an unafraid one, so without marking it an unmarked
		/// name plate would mean both "does not care about you" and "is angry at you right now".
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
				cached.Level = CorneredState;
			}
			else
			{
				cached.Level = -1;
			}

			return cached.Level;
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
