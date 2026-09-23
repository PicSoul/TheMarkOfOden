using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using BepInEx.Configuration;
using MarkOfOden.Config;
using MarkOfOden.Marks;
using UnityEngine;

namespace MarkOfOden.Fear
{
	/// <summary>
	/// In-game Standings & Stats reference panel.
	/// Displays boss credits, creature kill tallies, notoriety tiers, and live fear dispositions.
	/// Strictly protects against spoilers by only displaying creatures and bosses from biomes the player has visited.
	/// </summary>
	public static class StandingsPanel
	{
		private const int WindowId = 0x4D6F4F; // "MoO"

		private static bool _open;
		private static Vector2 _scroll;
		private static Rect _window = new Rect(0f, 0f, 860f, 680f);
		private static bool _placed;
		private static int _closedOnFrame = -1;

		public static bool ClosedThisFrame => _closedOnFrame == Time.frameCount;
		public static bool IsOpen => _open;

		private static Heightmap.Biome? _selectedBiome = null; // null means "All Discovered"
		private static string _searchFilter = string.Empty;
		private static int _rowIndex = 0;

		// GUIStyles
		private static GUIStyle _windowStyle;
		private static GUIStyle _heading;
		private static GUIStyle _subHeading;
		private static GUIStyle _section;
		private static GUIStyle _itemTitle;
		private static GUIStyle _itemSub;
		private static GUIStyle _badge;
		private static GUIStyle _rowEven;
		private static GUIStyle _rowOdd;
		private static GUIStyle _tabNormal;
		private static GUIStyle _tabActive;
		private static GUIStyle _searchField;
		private static GUIStyle _searchLabel;
		private static GUIStyle _closeButton;
		private static GUIStyle _toolbarBox;
		private static GUIStyle _footerBox;
		private static GUIStyle _footerText;
		private static GUIStyle _divider;

		// Procedural Textures
		private static Texture2D _windowBackdrop;
		private static Texture2D _cardBackdrop;
		private static Texture2D _rowEvenBackdrop;
		private static Texture2D _rowOddBackdrop;
		private static Texture2D _tabNormalBackdrop;
		private static Texture2D _tabActiveBackdrop;
		private static Texture2D _tabHoverBackdrop;
		private static Texture2D _searchBackdrop;
		private static Texture2D _closeBtnBackdrop;
		private static Texture2D _closeBtnHoverBackdrop;
		private static Texture2D _dividerTex;
		private static Texture2D _scrollTrackTex;
		private static Texture2D _scrollThumbTex;

		public static void Toggle()
		{
			if (_open)
			{
				Close();
			}
			else
			{
				Open();
			}
		}

		public static void Open()
		{
			_open = true;
			_scroll = Vector2.zero;
			Cursor.lockState = CursorLockMode.None;
			Cursor.visible = true;
			ZCursor.LockState = CursorLockMode.None;
			ZCursor.Show();
		}

		public static void Close()
		{
			if (!_open)
			{
				return;
			}

			_open = false;
			_closedOnFrame = Time.frameCount;

			// If no other menu or inventory is currently active, restore cursor lock
			if (!InventoryGui.IsVisible())
			{
				if (Menu.instance == null || !Menu.IsVisible())
				{
					Cursor.lockState = CursorLockMode.Locked;
					Cursor.visible = false;
					ZCursor.LockState = CursorLockMode.Locked;
					ZCursor.Hide();
				}
			}
		}

		public static void Draw()
		{
			if (!_open)
			{
				return;
			}

			EnsureStyles();

			if (!_placed)
			{
				_placed = true;
				_window.x = Mathf.Max(10f, (Screen.width - _window.width) * 0.5f);
				_window.y = Mathf.Max(15f, (Screen.height - _window.height) * 0.5f);
			}

			_window.x = Mathf.Clamp(_window.x, 0f, Mathf.Max(0f, Screen.width - _window.width));
			_window.y = Mathf.Clamp(_window.y, 0f, Mathf.Max(0f, Screen.height - _window.height));

			Color prevBg = GUI.backgroundColor;
			GUI.backgroundColor = Color.white;

			_window = GUI.Window(WindowId, _window, DrawWindow, string.Empty, _windowStyle);

			GUI.backgroundColor = prevBg;
		}

		private static void DrawWindow(int id)
		{
			_rowIndex = 0;

			// 1. Header Bar
			GUILayout.Space(6f);
			GUILayout.BeginHorizontal();
			GUILayout.Space(10f);
			GUILayout.BeginVertical();
			GUILayout.Label("<b><color=#F5D278>ᛟ   THE MARK OF ODEN — STANDINGS & LORE   ᛟ</color></b>", _heading);

			int tier = MarkLedger.Tier;
			int totalBosses = MarkLedger.AllBossNumbers.Count;
			int speciesCount = VanillaKillStats.SpeciesCount();
			string optOutNotice = MarkLedger.OptedOut ? "  <color=#FFA500>[ Opted Out — Nothing Fears You ]</color>" : string.Empty;

			GUILayout.Label($"<color=#94A3B8>Mark Tier: <b><color=#FCD34D>{tier}</color></b> (Threat +{tier})  •  Boss Credits: <b><color=#FCD34D>{totalBosses}</color></b>  •  Species Tracked: <b><color=#FCD34D>{speciesCount}</color></b>{optOutNotice}</color>", _subHeading);
			GUILayout.EndVertical();

			// Close Button
			if (GUILayout.Button("✕", _closeButton, GUILayout.Width(30f), GUILayout.Height(26f)))
			{
				Close();
			}
			GUILayout.Space(6f);
			GUILayout.EndHorizontal();

			GUILayout.Space(6f);

			// 2. Toolbar (Search & Controls)
			GUILayout.BeginHorizontal(_toolbarBox);
			GUILayout.Space(6f);
			GUILayout.Label("<color=#FCD34D><b>Search:</b></color>", _searchLabel, GUILayout.Width(54f));
			_searchFilter = GUILayout.TextField(_searchFilter, _searchField, GUILayout.Width(220f));
			if (!string.IsNullOrEmpty(_searchFilter))
			{
				if (GUILayout.Button("Clear", _tabNormal, GUILayout.Width(50f), GUILayout.Height(22f)))
				{
					_searchFilter = string.Empty;
				}
			}

			GUILayout.FlexibleSpace();
			GUILayout.Label("<color=#64748B>Drag header to reposition  •  Spoiler protection active</color>", _footerText);
			GUILayout.Space(8f);
			GUILayout.EndHorizontal();

			GUILayout.Space(5f);

			// 3. Biome Filter Tabs (Only discovered biomes!)
			DrawBiomeTabs();

			GUILayout.Space(6f);

			// 4. Scrollable Standings
			_scroll = GUILayout.BeginScrollView(_scroll);

			DrawBossesSection();
			DrawCreaturesSection();

			GUILayout.Space(12f);
			GUILayout.EndScrollView();

			// 5. Footer Bar
			GUILayout.BeginHorizontal(_footerBox);
			GUILayout.Space(8f);
			GUILayout.Label($"Press <color=#FCD34D><b>{Key(ModConfig.StandingsKey)}</b></color> or <color=#FCD34D><b>Esc</b></color> to close", _footerText);
			GUILayout.FlexibleSpace();
			GUILayout.Label("<color=#94A3B8>Kills advance your notoriety with that species, amplifying their terror.</color>", _footerText);
			GUILayout.Space(8f);
			GUILayout.EndHorizontal();

			GUI.DragWindow(new Rect(0f, 0f, 10000f, 45f));
		}

		private static void DrawBiomeTabs()
		{
			List<Heightmap.Biome> discovered = BiomeRegistry.GetDiscoveredBiomes();

			GUILayout.BeginHorizontal();
			GUILayout.Space(8f);

			// "All Discovered" Tab
			bool isAll = _selectedBiome == null;
			GUIStyle allStyle = isAll ? _tabActive : _tabNormal;
			string allLabel = isAll ? "<b><color=#FDE047>All Discovered</color></b>" : "<color=#CBD5E1>All Discovered</color>";
			if (GUILayout.Button(allLabel, allStyle, GUILayout.Height(24f)))
			{
				_selectedBiome = null;
			}

			// Individual Discovered Biome Tabs
			foreach (Heightmap.Biome b in discovered)
			{
				bool isCurrent = _selectedBiome == b;
				GUIStyle style = isCurrent ? _tabActive : _tabNormal;
				string name = BiomeRegistry.BiomeName(b);
				string label = isCurrent ? $"<b><color=#FDE047>{name}</color></b>" : $"<color=#CBD5E1>{name}</color>";
				if (GUILayout.Button(label, style, GUILayout.Height(24f)))
				{
					_selectedBiome = b;
				}
			}

			GUILayout.Space(8f);
			GUILayout.EndHorizontal();
		}

		private static void DrawBossesSection()
		{
			SectionHeader("BOSSES & ANCIENT POWERS", "ᚦ");

			// Bosses 1 to 7
			for (int bossNumber = 1; bossNumber <= 7; bossNumber++)
			{
				Heightmap.Biome biome = BiomeRegistry.BossBiome(bossNumber);

				// SPOILER GUARD: Do not show if biome is not discovered!
				if (!BiomeRegistry.IsBiomeDiscovered(biome))
				{
					continue;
				}

				// Tab filter
				if (_selectedBiome != null && _selectedBiome.Value != biome)
				{
					continue;
				}

				string bossName = BiomeRegistry.BossDefaultName(bossNumber);
				string biomeName = BiomeRegistry.BiomeName(biome);

				// Search filter
				if (!PassesSearch(bossName) && !PassesSearch(biomeName))
				{
					continue;
				}

				bool slain = MarkLedger.AllBossNumbers.Contains(bossNumber);
				string token = FindBossToken(bossNumber);
				int kills = !string.IsNullOrEmpty(token) ? VanillaKillStats.KillsOf(token) : 0;

				_rowIndex++;
				GUIStyle rowBg = (_rowIndex % 2 == 0) ? _rowEven : _rowOdd;

				GUILayout.BeginHorizontal(rowBg);
				GUILayout.Space(8f);

				// Boss Title
				string orderRoman = ToRoman(bossNumber);
				GUILayout.BeginVertical(GUILayout.Width(260f));
				GUILayout.Label($"<b><color=#FCD34D>{bossName}</color></b>  <size=11><color=#94A3B8>[{biomeName}]</color></size>", _itemTitle);
				GUILayout.Label($"<size=11><color=#64748B>Ancient Hierarchy: Boss {orderRoman} (Tier {bossNumber})</color></size>", _itemSub);
				GUILayout.EndVertical();

				// Status & Kills
				GUILayout.BeginVertical();
				if (slain)
				{
					GUILayout.Label($"<color=#4ADE80><b>✦ SLAIN / DEFEATED</b></color>  •  <color=#CBD5E1>{kills} lifetime kill{(kills == 1 ? "" : "s")}</color>", _badge);
					GUILayout.Label("<size=11><color=#94A3B8>The mark of this boss is etched into your soul. Tier credited.</color></size>", _itemSub);
				}
				else
				{
					GUILayout.Label($"<color=#F59E0B><b>⚔ UNDEFEATED</b></color>  •  <size=11><color=#94A3B8>Lurking within the {biomeName}</color></size>", _badge);
					GUILayout.Label("<size=11><color=#64748B>Defeat this ancient ruler to advance your Mark tier.</color></size>", _itemSub);
				}
				GUILayout.EndVertical();

				GUILayout.Space(8f);
				GUILayout.EndHorizontal();
				GUILayout.Space(1f);
			}
		}

		private static void DrawCreaturesSection()
		{
			SectionHeader("CREATURES & NOTORIETY STANDINGS", "ᛉ");

			int markTier = MarkLedger.Tier;
			int[] thresholds = ModConfig.ParseThresholds(ModConfig.NotorietyThresholds.Value);

			// Gather known creatures from CreatureTiers
			// We iterate through every creature registered in the tables
			List<CreatureRowData> rows = new List<CreatureRowData>();

			foreach (KeyValuePair<string, int> pair in CreatureTiers.AllBossTokens)
			{
				// Bosses are handled in boss section
			}

			// Read all species from kill table or pre-mapped creatures
			HashSet<string> processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			HashSet<string> processedBiomeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			// First, add all creatures the player has actually killed
			foreach (KeyValuePair<string, float> killEntry in VanillaKillStats.All())
			{
				string creatureKey = killEntry.Key;
				if (string.IsNullOrEmpty(creatureKey) || processed.Contains(creatureKey))
				{
					continue;
				}

				if (CreatureTiers.AllBossTokens.ContainsKey(creatureKey))
				{
					continue;
				}

				Heightmap.Biome biome = BiomeRegistry.ResolveCreatureBiome(creatureKey);
				if (!BiomeRegistry.IsBiomeDiscovered(biome))
				{
					continue; // SPOILER GUARD!
				}

				string localized = Localization.instance != null ? Localization.instance.Localize(creatureKey) : creatureKey;
				if (string.IsNullOrEmpty(localized) || localized.StartsWith("$"))
				{
					localized = creatureKey.Replace("$enemy_", "").Replace("$", "");
				}

				string biomeNameKey = $"{(int)biome}_{localized}";
				if (processedBiomeNames.Contains(biomeNameKey))
				{
					continue;
				}

				string counterpartPrefab = CreatureTiers.GetPrefabForToken(creatureKey);
				string counterpartToken = CreatureTiers.GetTokenForPrefab(creatureKey);

				processed.Add(creatureKey);
				processedBiomeNames.Add(biomeNameKey);
				if (!string.IsNullOrEmpty(counterpartPrefab)) processed.Add(counterpartPrefab);
				if (!string.IsNullOrEmpty(counterpartToken)) processed.Add(counterpartToken);
				if (creatureKey.StartsWith("$enemy_", StringComparison.OrdinalIgnoreCase)) processed.Add(creatureKey.Substring(7));

				rows.Add(BuildCreatureRow(creatureKey, biome, (int)killEntry.Value, markTier, thresholds));
			}

			// Second, add standard creatures from the visited biomes even if 0 kills
			AddStandardBiomeCreatures(rows, processed, processedBiomeNames, markTier, thresholds);

			// Final deduplication safeguard: guarantee each species appears at most once per biome
			Dictionary<string, CreatureRowData> uniqueRows = new Dictionary<string, CreatureRowData>(StringComparer.OrdinalIgnoreCase);
			foreach (CreatureRowData r in rows)
			{
				string key = $"{(int)r.Biome}_{r.DisplayName}";
				if (!uniqueRows.TryGetValue(key, out CreatureRowData existing))
				{
					uniqueRows[key] = r;
				}
				else
				{
					// Prefer entry with recorded kills
					if (existing.Kills == 0 && r.Kills > 0)
					{
						uniqueRows[key] = r;
					}
					// Always preserve the authentic base tier
					if (r.BaseTier < uniqueRows[key].BaseTier)
					{
						uniqueRows[key].BaseTier = r.BaseTier;
					}
				}
			}
			rows = new List<CreatureRowData>(uniqueRows.Values);

			// Sort rows: first by biome order, then by kills descending, then by name
			rows.Sort((a, b) =>
			{
				int bComp = a.Biome.CompareTo(b.Biome);
				if (bComp != 0) return bComp;
				int kComp = b.Kills.CompareTo(a.Kills);
				if (kComp != 0) return kComp;
				return string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
			});

			foreach (CreatureRowData row in rows)
			{
				// Tab filter
				if (_selectedBiome != null && _selectedBiome.Value != row.Biome)
				{
					continue;
				}

				// Search filter
				if (!PassesSearch(row.DisplayName) && !PassesSearch(row.BiomeName) && !PassesSearch(row.Token))
				{
					continue;
				}

				_rowIndex++;
				GUIStyle rowBg = (_rowIndex % 2 == 0) ? _rowEven : _rowOdd;

				GUILayout.BeginHorizontal(rowBg);
				GUILayout.Space(8f);

				// Column 1: Creature Name & Biome
				GUILayout.BeginVertical(GUILayout.Width(220f));
				GUILayout.Label($"<b>{row.DisplayName}</b>", _itemTitle);
				GUILayout.Label($"<size=11><color=#94A3B8>[{row.BiomeName}]  Base Tier {row.BaseTier}</color></size>", _itemSub);
				GUILayout.EndVertical();

				// Column 2: Kills & Notoriety Progress
				GUILayout.BeginVertical(GUILayout.Width(240f));
				string rankLabel = row.NotorietyRank > 0
					? $"<color=#FCD34D><b>Rank {ToRoman(row.NotorietyRank)}</b></color> (+{row.NotorietyRank} Threat)"
					: "<color=#94A3B8>Rank 0 (No notoriety)</color>";
				GUILayout.Label($"{rankLabel}  •  <b>{row.Kills}</b> kill{(row.Kills == 1 ? "" : "s")}", _itemTitle);
				GUILayout.Label($"<size=11><color=#64748B>{row.ProgressText}</color></size>", _itemSub);
				GUILayout.EndVertical();

				// Column 3: Live Fear Reaction
				GUILayout.BeginVertical();
				GUILayout.Label(row.DispositionBadge, _badge);
				GUILayout.Label($"<size=11><color=#94A3B8>{row.DispositionDetail}</color></size>", _itemSub);
				GUILayout.EndVertical();

				GUILayout.Space(8f);
				GUILayout.EndHorizontal();
				GUILayout.Space(1f);
			}

			if (rows.Count == 0)
			{
				GUILayout.Space(20f);
				GUILayout.BeginHorizontal();
				GUILayout.FlexibleSpace();
				GUILayout.Label("<color=#64748B>No creatures discovered yet in this category.</color>", _subHeading);
				GUILayout.FlexibleSpace();
				GUILayout.EndHorizontal();
			}
		}

		private class CreatureRowData
		{
			public string Token;
			public string DisplayName;
			public Heightmap.Biome Biome;
			public string BiomeName;
			public int BaseTier;
			public int Kills;
			public int NotorietyRank;
			public string ProgressText;
			public string DispositionBadge;
			public string DispositionDetail;
		}

		private static CreatureRowData BuildCreatureRow(string token, Heightmap.Biome biome, int kills, int markTier, int[] thresholds)
		{
			string localized = Localization.instance != null ? Localization.instance.Localize(token) : token;
			if (string.IsNullOrEmpty(localized) || localized.StartsWith("$"))
			{
				localized = token.Replace("$enemy_", "").Replace("$", "");
			}

			int notoriety = MarkLedger.NotorietyForKills(kills);
			int baseTier = GetBaseTier(token);

			// Progress to next rank
			string progress;
			int nextThreshold = -1;
			foreach (int t in thresholds)
			{
				if (kills < t)
				{
					nextThreshold = t;
					break;
				}
			}

			if (nextThreshold > 0)
			{
				int toGo = nextThreshold - kills;
				progress = $"Progress: {kills} / {nextThreshold} kills ({toGo} to next rank)";
			}
			else
			{
				progress = $"Max Notoriety Achieved ({kills} kills)";
			}

			// Fear evaluation against local player
			float threat = markTier + notoriety;
			float delta = threat - baseTier;

			string badge;
			string detail;

			if (MarkLedger.OptedOut)
			{
				badge = "<color=#FFA500>▲ Normal (Player Opted Out)</color>";
				detail = "Nothing fears you while opted out of The Mark of Oden.";
			}
			else if (baseTier == 0 && (token.Equals("Deer", StringComparison.OrdinalIgnoreCase) || token.Equals("Hare", StringComparison.OrdinalIgnoreCase)))
			{
				badge = "<color=#94A3B8>○ Harmless Prey</color>";
				detail = "Gentle creature with no combat attacks.";
			}
			else if (delta >= ModConfig.TerrifiedThreshold.Value)
			{
				badge = "<color=#B8DCEA><b>▼▼▼ Panicked (Terrified)</b></color>";
				detail = $"Threat exceeds courage by {delta:0}. Cowers or bolts in blind panic.";
			}
			else if (delta >= ModConfig.AfraidThreshold.Value)
			{
				badge = "<color=#5FC9D6><b>▼▼ Fleeing (Afraid)</b></color>";
				detail = $"Threat exceeds courage by {delta:0}. Actively sprints away on sight.";
			}
			else if (delta >= ModConfig.CautiousThreshold.Value)
			{
				badge = "<color=#9BD46A><b>▼ Wary (Cautious)</b></color>";
				detail = $"Threat exceeds courage by {delta:0}. Holds ground, refuses to attack unprovoked.";
			}
			else
			{
				badge = "<color=#E8A33C>▲ Unafraid / Aggressive</color>";
				detail = $"Courage ({baseTier}) withstands threat ({threat:0}). Attacks normally.";
			}

			return new CreatureRowData
			{
				Token = token,
				DisplayName = localized,
				Biome = biome,
				BiomeName = BiomeRegistry.BiomeName(biome),
				BaseTier = baseTier,
				Kills = kills,
				NotorietyRank = notoriety,
				ProgressText = progress,
				DispositionBadge = badge,
				DispositionDetail = detail
			};
		}

		private static void AddStandardBiomeCreatures(List<CreatureRowData> rows, HashSet<string> processed, HashSet<string> processedBiomeNames, int markTier, int[] thresholds)
		{
			// Key vanilla creatures per discovered biome to list even before first kill
			string[][] biomeCreatures = new[]
			{
				new[] { "Boar", "Neck", "Deer", "Greyling" },
				new[] { "Greydwarf", "Greydwarf_Shaman", "Greydwarf_Elite", "Skeleton", "Troll", "Ghost" },
				new[] { "Draugr", "Draugr_Elite", "Blob", "Leech", "Surtling", "Wraith", "Abomination" },
				new[] { "Wolf", "Fenring", "StoneGolem", "Hatchling", "Bat" },
				new[] { "Goblin", "GoblinShaman", "GoblinBrute", "Deathsquito", "Lox", "BlobTar" },
				new[] { "Seeker", "SeekerBrute", "Gjall", "Tick", "Dverger" },
				new[] { "Charred_Twitcher", "Charred_Melee", "Charred_Archer", "Charred_Mage", "Morgen", "Asksvin", "FallenValkyrie" }
			};

			Heightmap.Biome[] biomes = new[]
			{
				Heightmap.Biome.Meadows,
				Heightmap.Biome.BlackForest,
				Heightmap.Biome.Swamp,
				Heightmap.Biome.Mountain,
				Heightmap.Biome.Plains,
				Heightmap.Biome.Mistlands,
				Heightmap.Biome.AshLands
			};

			for (int i = 0; i < biomes.Length; i++)
			{
				Heightmap.Biome b = biomes[i];
				if (!BiomeRegistry.IsBiomeDiscovered(b))
				{
					continue; // STRICT SPOILER GUARD!
				}

				foreach (string creature in biomeCreatures[i])
				{
					if (processed.Contains(creature))
					{
						continue;
					}

					string disp = Localization.instance != null ? Localization.instance.Localize(creature) : creature;
					if (string.IsNullOrEmpty(disp) || disp.StartsWith("$"))
					{
						disp = creature.Replace("$enemy_", "").Replace("$", "");
					}

					string biomeNameKey = $"{(int)b}_{disp}";
					if (processedBiomeNames.Contains(biomeNameKey))
					{
						continue;
					}

					string counterpartTok = CreatureTiers.GetTokenForPrefab(creature);
					if (!string.IsNullOrEmpty(counterpartTok) && processed.Contains(counterpartTok))
					{
						continue;
					}

					processed.Add(creature);
					processedBiomeNames.Add(biomeNameKey);
					if (!string.IsNullOrEmpty(counterpartTok)) processed.Add(counterpartTok);

					int kills = VanillaKillStats.KillsOf(creature);
					rows.Add(BuildCreatureRow(creature, b, kills, markTier, thresholds));
				}
			}
		}

		private static int GetBaseTier(string token)
		{
			return CreatureTiers.GetTierForNameOrToken(token);
		}

		private static string FindBossToken(int bossNumber)
		{
			foreach (KeyValuePair<string, int> pair in CreatureTiers.AllBossTokens)
			{
				if (pair.Value == bossNumber)
				{
					return pair.Key;
				}
			}

			return null;
		}

		private static bool PassesSearch(string text)
		{
			if (string.IsNullOrEmpty(_searchFilter))
			{
				return true;
			}

			if (string.IsNullOrEmpty(text))
			{
				return false;
			}

			string clean = Regex.Replace(text, "<.*?>", string.Empty);
			return clean.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0;
		}

		private static void SectionHeader(string title, string rune)
		{
			GUILayout.Space(12f);
			GUILayout.BeginHorizontal();
			GUILayout.Space(4f);
			GUILayout.Label($"<color=#F59E0B>{rune}</color>  <b><color=#FCD34D>{title}</color></b>", _section);
			GUILayout.EndHorizontal();

			GUILayout.Space(2f);
			GUILayout.Label(GUIContent.none, _divider, GUILayout.Height(2f));
			GUILayout.Space(4f);
		}

		private static string ToRoman(int number)
		{
			switch (number)
			{
				case 1: return "I";
				case 2: return "II";
				case 3: return "III";
				case 4: return "IV";
				case 5: return "V";
				case 6: return "VI";
				case 7: return "VII";
				case 8: return "VIII";
				default: return number.ToString();
			}
		}

		private static string Key(ConfigEntry<KeyboardShortcut> entry)
		{
			if (entry == null || entry.Value.MainKey == KeyCode.None)
			{
				return "unbound";
			}

			return entry.Value.MainKey.ToString();
		}

		private static void EnsureStyles()
		{
			if (_heading != null)
			{
				return;
			}

			_windowBackdrop = MakeWindowFrame(64, 64);
			_cardBackdrop = MakeCardFrame(32, 32);
			_rowEvenBackdrop = MakeSolidTex(16, 16, new Color(0.12f, 0.14f, 0.18f, 0.40f));
			_rowOddBackdrop = MakeSolidTex(16, 16, new Color(0.06f, 0.07f, 0.09f, 0.20f));
			_tabNormalBackdrop = MakeButtonFrame(24, 24, new Color(0.13f, 0.15f, 0.19f, 0.85f), new Color(0.28f, 0.32f, 0.38f, 0.65f));
			_tabActiveBackdrop = MakeButtonFrame(24, 24, new Color(0.38f, 0.26f, 0.08f, 0.95f), new Color(0.85f, 0.68f, 0.28f, 1.0f));
			_tabHoverBackdrop = MakeButtonFrame(24, 24, new Color(0.22f, 0.25f, 0.32f, 0.90f), new Color(0.45f, 0.50f, 0.60f, 0.80f));
			_searchBackdrop = MakeButtonFrame(20, 20, new Color(0.08f, 0.09f, 0.12f, 0.95f), new Color(0.35f, 0.40f, 0.48f, 0.70f));
			_closeBtnBackdrop = MakeButtonFrame(24, 24, new Color(0.32f, 0.12f, 0.12f, 0.85f), new Color(0.65f, 0.25f, 0.25f, 0.90f));
			_closeBtnHoverBackdrop = MakeButtonFrame(24, 24, new Color(0.55f, 0.15f, 0.15f, 0.95f), new Color(0.95f, 0.40f, 0.40f, 1.0f));
			_dividerTex = MakeDividerTex(32, 2);
			_scrollTrackTex = MakeSolidTex(16, 16, new Color(0.05f, 0.06f, 0.08f, 0.85f));
			_scrollThumbTex = MakeButtonFrame(16, 16, new Color(0.45f, 0.35f, 0.18f, 0.90f), new Color(0.70f, 0.55f, 0.28f, 1.0f));

			_windowStyle = new GUIStyle();
			_windowStyle.normal.background = _windowBackdrop;
			_windowStyle.border = new RectOffset(10, 10, 10, 10);
			_windowStyle.padding = new RectOffset(14, 14, 10, 10);

			_heading = new GUIStyle(GUI.skin.label)
			{
				fontSize = 19,
				fontStyle = FontStyle.Bold,
				alignment = TextAnchor.MiddleLeft,
				richText = true
			};

			_subHeading = new GUIStyle(GUI.skin.label)
			{
				fontSize = 12,
				alignment = TextAnchor.MiddleLeft,
				richText = true
			};

			_section = new GUIStyle(GUI.skin.label)
			{
				fontSize = 14,
				fontStyle = FontStyle.Bold,
				richText = true
			};

			_itemTitle = new GUIStyle(GUI.skin.label)
			{
				fontSize = 13,
				fontStyle = FontStyle.Normal,
				alignment = TextAnchor.MiddleLeft,
				richText = true
			};

			_itemSub = new GUIStyle(GUI.skin.label)
			{
				fontSize = 11,
				alignment = TextAnchor.MiddleLeft,
				richText = true
			};

			_badge = new GUIStyle(GUI.skin.label)
			{
				fontSize = 13,
				fontStyle = FontStyle.Normal,
				alignment = TextAnchor.MiddleLeft,
				richText = true
			};

			_rowEven = new GUIStyle();
			_rowEven.normal.background = _rowEvenBackdrop;
			_rowEven.padding = new RectOffset(4, 4, 4, 4);

			_rowOdd = new GUIStyle();
			_rowOdd.normal.background = _rowOddBackdrop;
			_rowOdd.padding = new RectOffset(4, 4, 4, 4);

			_tabNormal = new GUIStyle(GUI.skin.button)
			{
				fontSize = 12,
				alignment = TextAnchor.MiddleCenter,
				richText = true
			};
			_tabNormal.normal.background = _tabNormalBackdrop;
			_tabNormal.hover.background = _tabHoverBackdrop;
			_tabNormal.border = new RectOffset(5, 5, 5, 5);
			_tabNormal.padding = new RectOffset(8, 8, 2, 2);

			_tabActive = new GUIStyle(GUI.skin.button)
			{
				fontSize = 12,
				fontStyle = FontStyle.Bold,
				alignment = TextAnchor.MiddleCenter,
				richText = true
			};
			_tabActive.normal.background = _tabActiveBackdrop;
			_tabActive.border = new RectOffset(5, 5, 5, 5);
			_tabActive.padding = new RectOffset(8, 8, 2, 2);

			_searchField = new GUIStyle(GUI.skin.textField)
			{
				fontSize = 12,
				alignment = TextAnchor.MiddleLeft
			};
			_searchField.normal.background = _searchBackdrop;
			_searchField.normal.textColor = new Color(0.95f, 0.95f, 0.95f);
			_searchField.border = new RectOffset(4, 4, 4, 4);
			_searchField.padding = new RectOffset(6, 6, 3, 3);

			_searchLabel = new GUIStyle(GUI.skin.label)
			{
				fontSize = 12,
				alignment = TextAnchor.MiddleLeft,
				richText = true
			};

			_closeButton = new GUIStyle(GUI.skin.button)
			{
				fontSize = 13,
				fontStyle = FontStyle.Bold,
				alignment = TextAnchor.MiddleCenter
			};
			_closeButton.normal.background = _closeBtnBackdrop;
			_closeButton.normal.textColor = new Color(0.95f, 0.85f, 0.85f);
			_closeButton.hover.background = _closeBtnHoverBackdrop;
			_closeButton.hover.textColor = Color.white;
			_closeButton.border = new RectOffset(4, 4, 4, 4);

			_toolbarBox = new GUIStyle();
			_toolbarBox.normal.background = _cardBackdrop;
			_toolbarBox.border = new RectOffset(6, 6, 6, 6);
			_toolbarBox.padding = new RectOffset(4, 4, 4, 4);

			_footerBox = new GUIStyle();
			_footerBox.normal.background = _cardBackdrop;
			_footerBox.border = new RectOffset(6, 6, 6, 6);
			_footerBox.padding = new RectOffset(4, 4, 4, 4);

			_footerText = new GUIStyle(GUI.skin.label)
			{
				fontSize = 11,
				alignment = TextAnchor.MiddleLeft,
				richText = true
			};
			_footerText.normal.textColor = new Color(0.65f, 0.70f, 0.78f);

			_divider = new GUIStyle();
			_divider.normal.background = _dividerTex;
			_divider.margin = new RectOffset(4, 4, 2, 4);

			GUI.skin.verticalScrollbar.normal.background = _scrollTrackTex;
			GUI.skin.verticalScrollbarThumb.normal.background = _scrollThumbTex;
			GUI.skin.verticalScrollbarThumb.border = new RectOffset(3, 3, 3, 3);
		}

		private static Texture2D MakeSolidTex(int w, int h, Color color)
		{
			Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
			Color[] pix = new Color[w * h];
			for (int i = 0; i < pix.Length; i++) pix[i] = color;
			tex.SetPixels(pix);
			tex.Apply();
			return tex;
		}

		private static Texture2D MakeWindowFrame(int w, int h)
		{
			Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
			Color bg = new Color(0.065f, 0.075f, 0.095f, 0.97f);
			Color outerIron = new Color(0.16f, 0.18f, 0.22f, 1f);
			Color highlightIron = new Color(0.26f, 0.29f, 0.35f, 1f);
			Color bronzeOuter = new Color(0.72f, 0.54f, 0.25f, 0.90f);
			Color bronzeInner = new Color(0.48f, 0.34f, 0.15f, 0.70f);
			Color rivetGold = new Color(0.95f, 0.82f, 0.45f, 1f);

			for (int y = 0; y < h; y++)
			{
				for (int x = 0; x < w; x++)
				{
					if (x == 0 || x == w - 1 || y == 0 || y == h - 1)
						tex.SetPixel(x, y, outerIron);
					else if (x == 1 || x == w - 2 || y == 1 || y == h - 2)
						tex.SetPixel(x, y, highlightIron);
					else if ((x == 4 || x == w - 5 || y == 4 || y == h - 5) && (x >= 4 && x <= w - 5 && y >= 4 && y <= h - 5))
						tex.SetPixel(x, y, bronzeOuter);
					else if ((x == 5 || x == w - 6 || y == 5 || y == h - 6) && (x >= 5 && x <= w - 6 && y >= 5 && y <= h - 6))
						tex.SetPixel(x, y, bronzeInner);
					else
						tex.SetPixel(x, y, bg);
				}
			}

			int[] rx = { 5, w - 6 };
			int[] ry = { 5, h - 6 };
			foreach (int cx in rx)
			{
				foreach (int cy in ry)
				{
					tex.SetPixel(cx, cy, rivetGold);
					tex.SetPixel(cx + 1, cy, rivetGold);
					tex.SetPixel(cx - 1, cy, rivetGold);
					tex.SetPixel(cx, cy + 1, rivetGold);
					tex.SetPixel(cx, cy - 1, rivetGold);
				}
			}
			tex.Apply();
			return tex;
		}

		private static Texture2D MakeCardFrame(int w, int h)
		{
			Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
			Color bg = new Color(0.095f, 0.105f, 0.13f, 0.75f);
			Color border = new Color(0.24f, 0.28f, 0.34f, 0.55f);
			for (int y = 0; y < h; y++)
			{
				for (int x = 0; x < w; x++)
				{
					if (x == 0 || x == w - 1 || y == 0 || y == h - 1)
						tex.SetPixel(x, y, border);
					else
						tex.SetPixel(x, y, bg);
				}
			}
			tex.Apply();
			return tex;
		}

		private static Texture2D MakeButtonFrame(int w, int h, Color bg, Color border)
		{
			Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
			Color topHighlight = Color.Lerp(border, Color.white, 0.3f);
			for (int y = 0; y < h; y++)
			{
				for (int x = 0; x < w; x++)
				{
					if (x == 0 || x == w - 1 || y == 0)
						tex.SetPixel(x, y, border);
					else if (y == h - 1)
						tex.SetPixel(x, y, topHighlight);
					else
						tex.SetPixel(x, y, bg);
				}
			}
			tex.Apply();
			return tex;
		}

		private static Texture2D MakeDividerTex(int w, int h)
		{
			Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
			Color center = new Color(0.85f, 0.68f, 0.30f, 0.85f);
			for (int y = 0; y < h; y++)
			{
				for (int x = 0; x < w; x++)
				{
					float f = Mathf.Sin((float)x / (w - 1) * Mathf.PI);
					tex.SetPixel(x, y, new Color(center.r, center.g, center.b, center.a * f));
				}
			}
			tex.Apply();
			return tex;
		}
	}
}
