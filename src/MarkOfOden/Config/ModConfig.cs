using System;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using ServerSync;
using UnityEngine;

namespace MarkOfOden.Config
{
	/// <summary>
	/// Which creatures get told on when they are not afraid of you. An unmarked plate is a statement,
	/// and on something that can kill you it is the wrong one.
	/// </summary>
	public enum UnafraidMarking
	{
		/// <summary>Never. An unmarked plate means only that the mod has not changed anything.</summary>
		Never,

		/// <summary>Only creatures worth killing for food, which are the ones you walk up to on purpose.</summary>
		FoodSources,

		/// <summary>Everything that can fight back, which is most of the world.</summary>
		Armed
	}

	/// <summary>How much of a creature's disposition is spelled out on its name plate.</summary>
	public enum NameplateStyle
	{
		/// <summary>The marker alone. Compact, once you know what the markers mean.</summary>
		Marker,

		/// <summary>The word alone. Nothing to learn, but wider.</summary>
		Word,

		/// <summary>Both, so the marker teaches itself while you read the word.</summary>
		Both
	}

	/// <summary>
	/// Every setting the mod exposes. Anything that changes how creatures judge a player is
	/// server-synced, so a dedicated server stays authoritative and clients cannot tune their own fear.
	/// </summary>
	public static class ModConfig
	{
		private const string SectionGeneral = "1 - General";
		private const string SectionFear = "2 - Fear";
		private const string SectionCower = "3 - Cower";
		private const string SectionMark = "4 - The Mark";
		private const string SectionTables = "5 - Creature tables";
		private const string SectionDisplay = "6 - Display";
		private const string SectionPopups = "7 - Popups";
		private const string SectionStandings = "8 - Standings";
		private const string SectionDebug = "9 - Debug";

		private static ConfigFile _file;
		private static ConfigSync _sync;

		/// <summary>
		/// Bumped whenever a setting's default changes. BepInEx keeps whatever is already in a user's
		/// file, so a changed default never reaches anyone who has run the mod before; this moves them
		/// across, but only where they were still on the old default and had not chosen for themselves.
		/// </summary>
		private const int CurrentConfigVersion = 5;

		/// <summary>
		/// Whether the synced settings are enforced on clients, or merely handed to them.
		///
		/// Synchronising a value and enforcing it are two different things. Without this, the
		/// server hands its values over on connect and a client may still edit them afterwards,
		/// which suits a server among friends. Turning it on makes them read-only for everyone
		/// but an admin, which is what a public server wants.
		/// </summary>
		public static ConfigEntry<bool> LockServerSettings;

		public static ConfigEntry<int> ConfigVersion;
		public static ConfigEntry<bool> Enabled;

		public static ConfigEntry<int> CautiousThreshold;
		public static ConfigEntry<int> AfraidThreshold;
		public static ConfigEntry<int> TerrifiedThreshold;
		public static ConfigEntry<float> StarCourage;
		public static ConfigEntry<float> StarCourageMax;
		public static ConfigEntry<float> InfusionCourage;
		public static ConfigEntry<float> SpecialEffectCourage;
		public static ConfigEntry<float> PackRadius;
		public static ConfigEntry<float> PackCourage;
		public static ConfigEntry<float> PackCourageMax;
		public static ConfigEntry<float> ScanRange;
		public static ConfigEntry<float> ReevaluateInterval;
		public static ConfigEntry<bool> RunWhenAfraid;
		public static ConfigEntry<bool> RaidCreaturesAlwaysAttack;
		public static ConfigEntry<float> BossFightRadius;
		public static ConfigEntry<bool> CorneredCreaturesFightBack;
		public static ConfigEntry<float> RetaliationWindow;
		public static ConfigEntry<float> HelpCallRadius;

		public static ConfigEntry<bool> EnableCower;
		public static ConfigEntry<float> CowerRange;
		public static ConfigEntry<float> CowerStaggerInterval;

		public static ConfigEntry<string> NotorietyThresholds;
		public static ConfigEntry<bool> SeedFromHistory;
		public static ConfigEntry<bool> InheritWorldProgress;
		public static ConfigEntry<int> InheritedWorldTierPenalty;
		public static ConfigEntry<int> MaxNotorietyEntries;

		public static ConfigEntry<string> CreatureTierOverrides;
		public static ConfigEntry<string> FearlessCreatures;
		public static ConfigEntry<string> NeverFleeCreatures;
		public static ConfigEntry<bool> AutoNeverFleeFoodAnimals;
		public static ConfigEntry<int> FoodAnimalMaxTier;

		public static ConfigEntry<bool> ShowFearOnNameplate;
		public static ConfigEntry<bool> ColourNames;
		public static ConfigEntry<NameplateStyle> NameplateLabels;
		public static ConfigEntry<UnafraidMarking> MarkUnafraid;
		public static ConfigEntry<string> FearSymbols;
		public static ConfigEntry<string> FearLabels;
		public static ConfigEntry<string> FearColours;
		public static ConfigEntry<float> NameplateDistance;

		public static ConfigEntry<bool> EnableProgressionPopups;
		public static ConfigEntry<float> PopupDisplayDuration;
		public static ConfigEntry<bool> EnablePopupAudio;
		public static ConfigEntry<float> PopupScreenHeight;

		public static ConfigEntry<KeyboardShortcut> StandingsKey;
		public static ConfigEntry<bool> ShowStandingsInventoryButton;

		public static ConfigEntry<bool> DebugLogging;

		public static ConfigSync Sync => _sync;

		public static void Init(ConfigFile file, ConfigSync sync)
		{
			_file = file;
			_sync = sync;

			LockServerSettings = Bind(SectionGeneral, "Lock settings to the server", false,
				"Enforce the server's values rather than only handing them out on connect. Off means a client may still change them afterwards; on makes every " +
				"synced setting read-only for anyone who is not an admin. Ignored in single player and on a server without this mod.");
			_sync.AddLockingConfigEntry(LockServerSettings);

			ConfigVersion = _file.Bind(SectionGeneral, "Config version", 0,
				new ConfigDescription("Which set of defaults this file was last brought up to date with. Managed by the mod; there is no reason to edit it."));

			Enabled = Bind(SectionGeneral, "Enabled", true,
				"Master switch. When off, every creature behaves exactly as it does in vanilla.");

			CautiousThreshold = Bind(SectionFear, "Cautious threshold", 1,
				"Threat minus courage at or above this makes a creature cautious: it stops treating you as prey, but holds its ground.");
			AfraidThreshold = Bind(SectionFear, "Afraid threshold", 3,
				"Threat minus courage at or above this makes a creature actively flee.");
			TerrifiedThreshold = Bind(SectionFear, "Terrified threshold", 5,
				"Threat minus courage at or above this makes a creature cower when it is cornered or you are on top of it.");
			StarCourage = Bind(SectionFear, "Star courage", 1f,
				"Courage added per star. A 2-star creature gets this once, a 3-star twice.");
			StarCourageMax = Bind(SectionFear, "Star courage cap", 2f,
				"Most courage stars can contribute in total. The default matches vanilla, where a 3-star creature is the bravest there is. " +
				"Mods that raise creature levels well beyond three would otherwise let stars alone outweigh everything else and switch fear off. " +
				"Raise it if you want high levels to count for more.");
			InfusionCourage = Bind(SectionFear, "Infusion courage", 1f,
				"Courage added to a creature that another mod has infused with an element. Creature Level and Loot Control does this, and an infused creature is genuinely " +
				"deadlier than the plain one, so it should hold its nerve for longer. Ignored when nothing has infused anything.");
			SpecialEffectCourage = Bind(SectionFear, "Special effect courage", 1f,
				"Courage added to a creature another mod has given a special ability, such as being armored or enraged. Same reasoning as infusion courage.");
			PackRadius = Bind(SectionFear, "Pack radius", 12f,
				"How far a creature looks for allies of its own faction when deciding whether it is outnumbered.");
			PackCourage = Bind(SectionFear, "Pack courage per ally", 0.5f,
				"Courage added for each nearby ally. Creatures in a mob are braver; the last one standing breaks.");
			PackCourageMax = Bind(SectionFear, "Pack courage cap", 3f,
				"Maximum total courage that nearby allies can contribute.");
			ScanRange = Bind(SectionFear, "Scan range", 30f,
				"How far a creature looks for a marked player. Also capped by what the creature can actually see or hear.");
			ReevaluateInterval = Bind(SectionFear, "Re-evaluate interval", 0.5f,
				"Seconds between fear re-evaluations for a single creature. Raise this if you see a frame time cost in large fights.");
			RunWhenAfraid = Bind(SectionFear, "Run when afraid", true,
				"Alert a fleeing creature so it sprints away instead of strolling. Vanilla flee speed follows the alerted flag.");
			RaidCreaturesAlwaysAttack = Bind(SectionFear, "Raid creatures always attack", true,
				"Creatures spawned by an active raid ignore fear entirely. Turning this off lets raids break and run, which trivialises them.");
			BossFightRadius = Bind(SectionFear, "Boss fight radius", 60f,
				"Creatures within this distance of an alerted boss ignore fear, so the adds a boss summons keep fighting instead of losing interest partway through. " +
				"Set to 0 to turn this off and let them break like anything else.");
			CorneredCreaturesFightBack = Bind(SectionFear, "Cornered creatures fight back", true,
				"A creature you hit defends itself, however frightened it is, instead of having to be chased down. " +
				"Turn this off and a fleeing creature keeps fleeing while you shoot it in the back.");
			HelpCallRadius = Bind(SectionFear, "Help call radius", 15f,
				"When you pick a fight with a creature, others of its own kind within this distance of it join in, rather than standing and watching a battle beside them. " +
				"Measured from the creature you hit, so shooting one from a distance rallies its packmates and not whatever is near you. Set to 0 to let them ignore it.");
			RetaliationWindow = Bind(SectionFear, "Retaliation window", 10f,
				"Seconds a creature stays angry after being hit. Tracked per attacker, so one player's fight does not enrage it at everyone.");

			EnableCower = Bind(SectionCower, "Enable cower", true,
				"Terrified creatures that cannot escape stop dead and face you instead of running into scenery.");
			CowerRange = Bind(SectionCower, "Cower range", 4f,
				"A terrified creature cowers rather than flees when you are this close.");
			CowerStaggerInterval = Bind(SectionCower, "Cower flinch interval", 0f,
				"Seconds between flinches while cowering, or 0 for none. The flinch borrows the stagger recoil, because vanilla has no cower animation, but that recoil " +
				"is what a creature plays when a blow lands: it reads as stumbling rather than cringing, and it does so on every creature, not just the small ones. " +
				"Standing rooted and staring at you is the better half of cowering anyway, so the flinch is off unless you want it.");

			NotorietyThresholds = Bind(SectionMark, "Notoriety thresholds", "25,100,400",
				"Personal kill counts of a single species at which that species starts to recognise you. Each step adds 1 to your threat against that species only. " +
				"Counts come from the kill history Valheim already keeps for your character, so they include everything you killed before installing this mod.");
			SeedFromHistory = Bind(SectionMark, "Seed from history", true,
				"On a character that predates this mod, work out its boss credits from the kill history Valheim already keeps for it, " +
				"falling back to the boss trophies it carries for kills older than that history. Runs once per character.");
			InheritWorldProgress = Bind(SectionMark, "Inherit world progress", false,
				"Off by default, and deliberately: this is the FleeOnSight behaviour where a fresh character inherits the server's boss kills. " +
				"Turn it on only if you want world progress to count for players who were never there.");
			InheritedWorldTierPenalty = Bind(SectionMark, "Inherited world tier penalty", 2,
				"Tiers subtracted from inherited world progress. Only used when Inherit world progress is on.");
			MaxNotorietyEntries = Bind(SectionMark, "Max notoriety entries", 128,
				"Cap on species tracked in the mark published to other players. Lowest notoriety is dropped first. Keeps the network payload small.");

			CreatureTierOverrides = Bind(SectionTables, "Creature tier overrides", "",
				"Comma separated Name=Tier pairs overriding the built-in table, for example Greyling=0,Troll=3. " +
				"Accepts the prefab name (Greyling) or the localised name token. Run the console command 'moo dump' to print the resolved table.");
			FearlessCreatures = Bind(SectionTables, "Fearless creatures", "",
				"Comma separated names that never feel fear, whatever your mark. Same name format as the tier overrides. " +
				"Nothing is listed by default; see Never flee creatures for the gentler option that food animals use.");
			AutoNeverFleeFoodAnimals = Bind(SectionTables, "Food animals never flee", true,
				"Work out which creatures players hunt for food from their own drop tables, and stop those breaking and running. " +
				"Derived from game data rather than a fixed list, so it covers creatures this mod has never heard of. Run the console command 'moo dump' to see what it found.");
			FoodAnimalMaxTier = Bind(SectionTables, "Food animal max tier", 1,
				"How weak a creature must be to count as prey rather than a threat that happens to drop meat. " +
				"At the default, boar and neck qualify while wolf, lox and serpent do not, so hunting stays easy without the dangerous ones losing their nerve.");
			NeverFleeCreatures = Bind(SectionTables, "Never flee creatures", "$enemy_moose, $enemy_seal",
				"Extra comma separated names that may lose interest in you but never break and run, on top of any found automatically above. " +
				"Moose and seal are listed because players hunt them, but the Deep North rates every creature as dangerous, so they are not detected automatically. Their young inherit this. " +
				"A creature that bolts turns hunting into a chase, while one that charges a Viking who has killed every boss looks absurd; " +
				"ignoring you is the only reading that is neither. They still defend themselves if you hit them.");

			ShowFearOnNameplate = Bind(SectionDisplay, "Show fear on nameplate", true,
				"Colour a creature's name and add a symbol to it according to how it feels about you. " +
				"Turn this off if another HUD mod conflicts, or if you would rather read it from behaviour alone.");
			ColourNames = Bind(SectionDisplay, "Colour names", true,
				"Tint the creature's name as well as marking it. Turn this off to show only the marker and leave the name's colour to the game or to another mod. " +
				"BetterUI's custom alerted status already colours names by alert state, and a marker on its own combines with that rather than overriding it.");
			NameplateLabels = Bind(SectionDisplay, "Nameplate labels", NameplateStyle.Both,
				"Whether a name plate carries the marker, the word, or both. Both is the default because the word needs no explaining and the marker sits next to it " +
				"until you have learned it; switch to Marker once you have, for shorter plates.");
			MarkUnafraid = Bind(SectionDisplay, "Mark unafraid creatures", UnafraidMarking.FoodSources,
				"Which creatures are marked when your name means nothing to them. Leaving a plate unmarked reads as 'this one is no trouble', which is true of a deer and " +
				"badly wrong about a lox, so anything that can actually fight is worth saying out loud. FoodSources marks only what you would kill for meat, which is what " +
				"you walk up to on purpose and where the mistake costs most; Armed marks everything with an attack, which is accurate but lights up most of the world; " +
				"Never restores the older behaviour. Creatures with no attack at all, like deer and hare, are never marked under any of these.");
			FearSymbols = Bind(SectionDisplay, "Fear markers", "\u25BC,\u25BC\u25BC,\u25BC\u25BC\u25BC,\u25B2,\u25B2",
				"Markers for cautious, afraid, terrified, cornered and unafraid, in that order. Down means the creature is putting distance between it and you and up " +
				"means it is not, so direction alone says which way this is about to go; the first three repeat one character so the marker also reads as a scale. " +
				"If your font cannot draw the triangles they will come out as empty boxes, in which case v,vv,vvv,^,^ says the same thing in plain ASCII. " +
				"Leave an entry empty for colour only.");
			FearLabels = Bind(SectionDisplay, "Fear words", "wary,fleeing,panicked,cornered,unafraid",
				"Words for cautious, afraid, terrified, cornered and unafraid, in that order. They name what the creature is about to do rather than what it feels, because " +
				"that is the part you can act on. Cornered and unafraid share a colour and a marker because they amount to the same thing for you; only the word separates " +
				"the one that is angry from the one that never cared. Leave an entry empty to show nothing but the marker for that one state.");
			FearColours = Bind(SectionDisplay, "Fear colours", "#9BD46A,#5FC9D6,#B8DCEA,#E8A33C,#E8A33C",
				"Colours for those five states. The three fleeing ones run green to cyan to pale ice, so the colour says how far gone a creature is without counting " +
				"markers; the last two share the one warm colour, because they are the two that will fight you. Every state is the same rung on all three scales at once, " +
				"so there is no combination to decode, and any one of colour, marker or word is enough on its own.");
			NameplateDistance = Bind(SectionDisplay, "Nameplate distance", 0f,
				"Vanilla only shows name plates within 10m, which is late to learn that something is afraid of you. " +
				"Set a larger distance to see them further out, or 0 to leave the game's own value alone. Affects all name plates, not just frightened ones.");

			EnableProgressionPopups = BindClient(SectionPopups, "Enable progression popups", true,
				"Display an in-game HUD popup notification banner when defeating a boss or crossing a species notoriety threshold.");
			PopupDisplayDuration = BindClient(SectionPopups, "Popup display duration", 4.0f,
				"Seconds the progression banner remains visible before smoothly fading away.");
			EnablePopupAudio = BindClient(SectionPopups, "Enable popup audio", true,
				"Play uplifting sound effects when a progression or notoriety banner appears.");
			PopupScreenHeight = BindClient(SectionPopups, "Popup screen height", 0.88f,
				"How far up the screen the banner sits, where 1 is the very top and 0.5 the middle. " +
				"Kept high by default so a boss kill does not put a panel across the fight that earned it. " +
				"Clamped between 0.5 and 0.95, because the banner is anchored through its middle and would " +
				"otherwise hang off the edge.");

			StandingsKey = BindClient(SectionStandings, "Standings toggle key", new KeyboardShortcut(KeyCode.F4),
				"Key to open the in-game Standings & Stats window.");
			ShowStandingsInventoryButton = BindClient(SectionStandings, "Show inventory button", true,
				"Display a clickable button in the Inventory/Compendium screen to open the Standings & Stats window.");

			DebugLogging = Bind(SectionDebug, "Debug logging", false,
				"Verbose logging of fear decisions. Noisy; prefer the console command 'moo why' for one-off checks.");

			ApplyMigrations();
		}

		private static void ApplyMigrations()
		{
			if (ConfigVersion.Value >= CurrentConfigVersion)
			{
				return;
			}

			int from = ConfigVersion.Value;

			// Food animals were briefly exempt from fear altogether before being capped instead.
			MoveDefault(FearlessCreatures, "Boar, Deer, Hare, Hen, Chicken", "");

			// Moose and seal are hunted, but the Deep North rates every creature dangerous, so they
			// are not detected automatically and have to be listed.
			MoveDefault(NeverFleeCreatures, "", "$enemy_moose, $enemy_seal");

			// Markers used to point sideways, which said how strongly a creature felt but not which
			// way it was about to move.
			MoveDefault(FearSymbols, "<,<<,<<<,!", "v,vv,vvv,^");

			// The three fleeing states used to share one colour, which left the colour saying only
			// whether a creature would fight and the marker carrying the whole scale by itself.
			MoveDefault(FearColours, "#8FC97A,#8FC97A,#8FC97A,#E8A33C", "#9BD46A,#5FC9D6,#B8DCEA,#E8A33C");

			// A fifth state joined the four, and the markers moved to triangles now that the name
			// plate font has been seen to draw them.
			MoveDefault(FearSymbols, "v,vv,vvv,^", "\u25BC,\u25BC\u25BC,\u25BC\u25BC\u25BC,\u25B2,\u25B2");
			MoveDefault(FearLabels, "wary,fleeing,panicked,cornered", "wary,fleeing,panicked,cornered,unafraid");
			MoveDefault(FearColours, "#9BD46A,#5FC9D6,#B8DCEA,#E8A33C", "#9BD46A,#5FC9D6,#B8DCEA,#E8A33C,#E8A33C");

			// The cower flinch reused the stagger recoil, which is the hurt animation and looks it.
			MoveDefaultValue(CowerStaggerInterval, 2.5f, 0f);

			ConfigVersion.Value = CurrentConfigVersion;
			Plugin.Log.LogInfo("Config brought up to date from version " + from + " to " + CurrentConfigVersion + ".");
		}

		/// <summary>
		/// Moves a setting to a new default, but only if it still holds the old one. A value the user
		/// chose is never overwritten.
		/// </summary>
		private static void MoveDefault(ConfigEntry<string> entry, string oldDefault, string newDefault)
		{
			if (entry == null || !string.Equals(entry.Value?.Trim(), oldDefault, StringComparison.Ordinal))
			{
				return;
			}

			entry.Value = newDefault;
			Plugin.Log.LogInfo("Updated '" + entry.Definition.Key + "' to its new default.");
		}

		/// <summary>
		/// The same as <see cref="MoveDefault"/> for settings that are not strings, where there is no
		/// trimming to do and an exact match is the only sensible test.
		/// </summary>
		private static void MoveDefaultValue<T>(ConfigEntry<T> entry, T oldDefault, T newDefault)
		{
			if (entry == null || !EqualityComparer<T>.Default.Equals(entry.Value, oldDefault))
			{
				return;
			}

			entry.Value = newDefault;
			Plugin.Log.LogInfo("Updated '" + entry.Definition.Key + "' to its new default.");
		}

		private static ConfigEntry<T> Bind<T>(string section, string key, T defaultValue, string description)
		{
			ConfigEntry<T> entry = _file.Bind(section, key, defaultValue, new ConfigDescription(description));
			SyncedConfigEntry<T> synced = _sync.AddConfigEntry(entry);
			synced.SynchronizedConfig = true;
			return entry;
		}

		private static ConfigEntry<T> BindClient<T>(string section, string key, T defaultValue, string description)
		{
			ConfigEntry<T> entry = _file.Bind(section, key, defaultValue, new ConfigDescription(description));
			SyncedConfigEntry<T> synced = _sync.AddConfigEntry(entry);
			synced.SynchronizedConfig = false;
			return entry;
		}

		/// <summary>Parses a Name=Number list. Invalid pairs are skipped rather than failing the whole entry.</summary>
		public static Dictionary<string, int> ParseNameValueList(string raw)
		{
			Dictionary<string, int> result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			if (string.IsNullOrEmpty(raw))
			{
				return result;
			}

			foreach (string chunk in raw.Split(','))
			{
				string pair = chunk.Trim();
				if (pair.Length == 0)
				{
					continue;
				}

				int split = pair.LastIndexOf('=');
				if (split <= 0 || split == pair.Length - 1)
				{
					Plugin.Log.LogWarning("Ignoring malformed entry " + pair + " (expected Name=Number).");
					continue;
				}

				string name = pair.Substring(0, split).Trim();
				string number = pair.Substring(split + 1).Trim();
				if (!int.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
				{
					Plugin.Log.LogWarning("Ignoring entry " + pair + ": " + number + " is not a whole number.");
					continue;
				}

				result[name] = value;
			}

			return result;
		}

		/// <summary>Parses a plain comma separated name list.</summary>
		public static HashSet<string> ParseNameList(string raw)
		{
			HashSet<string> result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			if (string.IsNullOrEmpty(raw))
			{
				return result;
			}

			foreach (string chunk in raw.Split(','))
			{
				string name = chunk.Trim();
				if (name.Length > 0)
				{
					result.Add(name);
				}
			}

			return result;
		}

		/// <summary>Parses the ascending notoriety thresholds. Falls back to the default on junk input.</summary>
		public static int[] ParseThresholds(string raw)
		{
			List<int> values = new List<int>();
			foreach (string chunk in (raw ?? string.Empty).Split(','))
			{
				if (int.TryParse(chunk.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) && value > 0)
				{
					values.Add(value);
				}
			}

			if (values.Count == 0)
			{
				Plugin.Log.LogWarning("Notoriety thresholds were empty or unparseable; using 25,100,400.");
				return new[] { 25, 100, 400 };
			}

			values.Sort();
			return values.ToArray();
		}
	}
}
