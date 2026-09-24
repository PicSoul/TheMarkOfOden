using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using SoftReferenceableAssets;
using UnityEngine;

namespace MarkOfOden.Fear
{
	/// <summary>
	/// Works out which creatures a player can actually meet, by following every route the game has for
	/// putting one in the world. Run by hand with 'moo creatures'; the result is reviewed and baked into
	/// <see cref="CreatureCatalog"/>, so players never pay for it.
	///
	/// Why a scan rather than a list: the game's files hold creatures nothing ever spawns - test prefabs,
	/// cut content - and nobody can say which from memory. Only following the references can.
	///
	/// Two passes. The first follows the live game outward from what a player walks into - world spawns,
	/// raids, everything the world places on the map, every enabled location and dungeon room, everything
	/// a player can craft, everything that hatches - and then from every creature reached, to whatever it
	/// grows into, breeds, drops or summons, until nothing new turns up. References are followed
	/// generically, through any serialised field, rather than by naming the components that spawn things,
	/// so a spawner, a boss altar, a projectile that spawns ticks on impact and whatever another mod uses
	/// are all found the same way.
	///
	/// The second pass is the evidence for the first. A creature the first pass did not reach is either
	/// cut content or a route the scan does not know about, and the only way to tell is to ask what refers
	/// to it. So everything is searched - every prefab, every location and room including the switched-off
	/// ones, every placement rule, every raid - and each unreached creature is reported with what points at
	/// it. Nothing at all, or only switched-off content: safe to hide. Something live: the scan has a gap,
	/// and hiding that creature would hide a real one.
	///
	/// Locations and dungeon rooms are soft references: not in memory until the game needs them. The scan
	/// loads each, reads it and releases it again, one per frame, so it takes a little while and the game
	/// stays responsive. That cost is why this is a command and not something every player runs.
	/// </summary>
	public static class CreatureScan
	{
		/// <summary>How many objects deep to follow from a root: item, projectile, spawn effect, creature.</summary>
		private const int MaxHops = 5;

		/// <summary>How far the evidence pass follows: the referring object itself, and one step on.</summary>
		private const int EvidenceHops = 1;

		/// <summary>How deep to follow plain data classes inside one component.</summary>
		private const int MaxDataDepth = 6;

		/// <summary>Most referrers reported per creature. The first few say everything.</summary>
		private const int MaxReferrers = 8;

		private sealed class Reached
		{
			public string Via;
			public Heightmap.Biome Biome;
		}

		private static bool _running;
		private static Dictionary<string, Reached> _reached;
		private static Queue<GameObject> _toFollow;
		private static Dictionary<GameObject, int> _bestHop;
		private static Dictionary<string, int> _softBestHop;
		private static HashSet<object> _seenData;
		private static Transform _walking;
		private static int _maxHops;

		// The evidence pass.
		private static bool _collectingEvidence;
		private static HashSet<string> _unreached;
		private static Dictionary<string, List<string>> _referrers;
		private static HashSet<GameObject> _reachedObjects;
		private static HashSet<GameObject> _walkedRoots;

		private static readonly Dictionary<Type, FieldInfo[]> FieldCache = new Dictionary<Type, FieldInfo[]>();

		public static string ReportPath => Path.Combine(Paths.BepInExRootPath, "MarkOfOden-creature-scan.txt");

		public static void Start(Terminal context)
		{
			if (_running)
			{
				context.AddString("A creature scan is already running.");
				return;
			}

			if (ZNetScene.instance == null || ZoneSystem.instance == null || ObjectDB.instance == null)
			{
				context.AddString("Load into a world first; the scan reads the world's own tables.");
				return;
			}

			context.AddString("Scanning every creature source. This reads every location and dungeon room twice and takes a few minutes; keep playing or wait.");
			ZNetScene.instance.StartCoroutine(Run(context));
		}

		private static IEnumerator Run(Terminal context)
		{
			_running = true;
			_reached = new Dictionary<string, Reached>(StringComparer.OrdinalIgnoreCase);
			_toFollow = new Queue<GameObject>();
			_bestHop = new Dictionary<GameObject, int>();
			_softBestHop = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			_seenData = new HashSet<object>(ReferenceComparer.Instance);
			_maxHops = MaxHops;
			_collectingEvidence = false;
			_walkedRoots = new HashSet<GameObject>();

			List<string> problems = new List<string>();
			float started = Time.realtimeSinceStartup;
			int[] counts = new int[2];

			// ======================================================== pass 1: what a player can meet

			// ---- world spawns ------------------------------------------------------------------
			SpawnSystem spawnSystem = ZoneSystem.instance.m_zoneCtrlPrefab != null
				? ZoneSystem.instance.m_zoneCtrlPrefab.GetComponent<SpawnSystem>()
				: null;

			if (spawnSystem == null)
			{
				problems.Add("no SpawnSystem on the zone controller; world spawns not read");
			}
			else
			{
				foreach (SpawnSystem.SpawnData data in SpawnEntries(spawnSystem))
				{
					if (data.m_enabled)
					{
						Record(data.m_prefab, "world spawn", data.m_biome);
					}
				}
			}

			// ---- raids ---------------------------------------------------------------------------
			if (RandEventSystem.instance == null)
			{
				problems.Add("no RandEventSystem; raids not read");
			}
			else
			{
				foreach (RandomEvent ev in RandEventSystem.instance.m_events)
				{
					if (ev == null || !ev.m_enabled) continue;
					foreach (SpawnSystem.SpawnData data in ev.m_spawn)
					{
						if (data != null && data.m_enabled && data.m_prefab != null)
						{
							Record(data.m_prefab, "raid " + ev.m_name, ev.m_biome);
						}
					}
				}
			}

			// ---- what the world scatters over the map: nests, spawners, and everything else ------
			foreach (ZoneSystem.ZoneVegetation placed in ZoneSystem.instance.m_vegetation)
			{
				if (placed != null && placed.m_enable && placed.m_prefab != null)
				{
					Walk(placed.m_prefab, "placed in the world: " + placed.m_prefab.name, placed.m_biome, 0);
				}
			}

			// ---- locations and dungeon rooms -----------------------------------------------------
			yield return ReadLocations(context, problems, true, counts);
			yield return ReadRooms(context, problems, true, counts);

			// ---- what a player can make, and what hatches ---------------------------------------
			foreach (Recipe recipe in ObjectDB.instance.m_recipes)
			{
				if (recipe != null && recipe.m_enabled && recipe.m_item != null)
				{
					Walk(recipe.m_item.gameObject, "crafted item " + recipe.m_item.name, Heightmap.Biome.None, 0);
				}
			}

			foreach (GameObject item in ObjectDB.instance.m_items)
			{
				if (item != null && item.GetComponent<EggGrow>() != null)
				{
					Walk(item, "egg " + item.name, Heightmap.Biome.None, 0);
				}
			}

			// ---- everything a reached creature leads to ---------------------------------------------
			int followed = 0;
			while (_toFollow.Count > 0)
			{
				GameObject creature = _toFollow.Dequeue();
				Walk(creature, "from " + creature.name, _reached[creature.name].Biome, 0);

				if (++followed % 20 == 0)
				{
					yield return null;
				}
			}

			// ======================================================== pass 2: what refers to the rest

			List<GameObject> creatures = AllCreatures();
			_unreached = new HashSet<string>(
				creatures.Where(p => !_reached.ContainsKey(p.name)).Select(p => p.name),
				StringComparer.OrdinalIgnoreCase);

			if (_unreached.Count > 0)
			{
				context.AddString("  " + _unreached.Count + " creatures not reached; now finding what refers to each...");

				_reachedObjects = new HashSet<GameObject>(_bestHop.Keys);
				_reachedObjects.UnionWith(_walkedRoots);
				_referrers = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
				_collectingEvidence = true;
				_maxHops = EvidenceHops;
				_bestHop = new Dictionary<GameObject, int>();
				_softBestHop = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
				_seenData = new HashSet<object>(ReferenceComparer.Instance);

				if (spawnSystem != null)
				{
					foreach (SpawnSystem.SpawnData data in SpawnEntries(spawnSystem))
					{
						Record(data.m_prefab, "world spawn list entry" + (data.m_enabled ? "" : " [switched off]"), data.m_biome);
					}
				}

				if (RandEventSystem.instance != null)
				{
					foreach (RandomEvent ev in RandEventSystem.instance.m_events)
					{
						if (ev == null) continue;
						foreach (SpawnSystem.SpawnData data in ev.m_spawn)
						{
							if (data?.m_prefab != null)
							{
								Record(data.m_prefab, "raid " + ev.m_name + (ev.m_enabled && data.m_enabled ? "" : " [switched off]"), ev.m_biome);
							}
						}
					}
				}

				foreach (ZoneSystem.ZoneVegetation placed in ZoneSystem.instance.m_vegetation)
				{
					if (placed?.m_prefab != null)
					{
						Walk(placed.m_prefab, "placed in the world: " + placed.m_prefab.name + (placed.m_enable ? "" : " [switched off]"), placed.m_biome, 0);
					}
				}

				yield return ReadLocations(context, problems, false, counts);
				yield return ReadRooms(context, problems, false, counts);

				foreach (GameObject item in ObjectDB.instance.m_items)
				{
					if (item != null)
					{
						Walk(item, "item " + item.name, Heightmap.Biome.None, 0);
					}
				}

				int read = 0;
				foreach (GameObject prefab in ZNetScene.instance.m_prefabs.ToList())
				{
					if (prefab != null)
					{
						Walk(prefab, Describe(prefab), Heightmap.Biome.None, 0);
					}

					if (++read % 60 == 0)
					{
						yield return null;
					}
				}
			}

			// ======================================================== report

			string report = Report(creatures, counts[0], counts[1], problems, Time.realtimeSinceStartup - started);
			try
			{
				File.WriteAllText(ReportPath, report, new UTF8Encoding(false));
			}
			catch (Exception e)
			{
				Plugin.Log.LogWarning("Could not write the scan report: " + e.Message);
			}

			Plugin.Log.LogInfo("Creature scan:\n" + report);
			context.AddString("Creature scan done: " + (creatures.Count - _unreached.Count) + " of " + creatures.Count
				+ " creatures reachable. Report in the log and in BepInEx\\MarkOfOden-creature-scan.txt.");

			_running = false;
			_collectingEvidence = false;
			_reached = null;
			_toFollow = null;
			_bestHop = null;
			_softBestHop = null;
			_seenData = null;
			_unreached = null;
			_referrers = null;
			_reachedObjects = null;
			_walkedRoots = null;
		}

		private static IEnumerable<SpawnSystem.SpawnData> SpawnEntries(SpawnSystem spawnSystem)
		{
			foreach (SpawnSystemList list in spawnSystem.m_spawnLists)
			{
				if (list == null) continue;
				foreach (SpawnSystem.SpawnData data in list.m_spawners)
				{
					if (data?.m_prefab != null)
					{
						yield return data;
					}
				}
			}
		}

		/// <summary>
		/// Every location, loaded, read and released one per frame. In the first pass only the ones the
		/// world actually places; in the second, all of them, labelled by why they would not be.
		/// </summary>
		private static IEnumerator ReadLocations(Terminal context, List<string> problems, bool liveOnly, int[] counts)
		{
			int done = 0;
			foreach (ZoneSystem.ZoneLocation location in ZoneSystem.instance.m_locations.ToList())
			{
				if (location == null || !location.m_prefab.IsValid)
				{
					continue;
				}

				bool live = location.m_enable && location.m_quantity > 0;
				if (liveOnly && !live)
				{
					continue;
				}

				string label = "location " + location.m_prefabName
					+ (!location.m_enable ? " [switched off]" : location.m_quantity <= 0 ? " [never placed]" : "");

				try
				{
					location.m_prefab.Load();
					GameObject asset = location.m_prefab.Asset;
					if (asset != null)
					{
						Walk(asset, label, location.m_biome, 0);
					}
				}
				catch (Exception e)
				{
					problems.Add(label + ": " + e.Message);
				}
				finally
				{
					location.m_prefab.Release();
				}

				done++;
				if (liveOnly) counts[0] = done;
				if (done % 50 == 0)
				{
					context.AddString("  ...read " + done + " locations");
				}

				yield return null;
			}
		}

		private static IEnumerator ReadRooms(Terminal context, List<string> problems, bool liveOnly, int[] counts)
		{
			List<DungeonDB.RoomData> rooms = DungeonDB.GetRooms();
			if (rooms == null)
			{
				problems.Add("no DungeonDB rooms; dungeons not read");
				yield break;
			}

			int done = 0;
			foreach (DungeonDB.RoomData roomData in rooms.ToList())
			{
				if (roomData == null || !roomData.m_prefab.IsValid)
				{
					continue;
				}

				try
				{
					roomData.m_prefab.Load();
					GameObject asset = roomData.m_prefab.Asset;
					Room room = asset != null ? asset.GetComponent<Room>() : null;
					bool live = room == null || room.m_enabled;

					if (asset != null && (live || !liveOnly))
					{
						Walk(asset, "dungeon room " + asset.name + (live ? "" : " [switched off]"), Heightmap.Biome.None, 0);
					}
				}
				catch (Exception e)
				{
					problems.Add("room: " + e.Message);
				}
				finally
				{
					roomData.m_prefab.Release();
				}

				done++;
				if (liveOnly) counts[1] = done;
				if (done % 100 == 0)
				{
					context.AddString("  ...read " + done + " dungeon rooms");
				}

				yield return null;
			}
		}

		// ------------------------------------------------------------------------------------------

		private static void Record(GameObject creaturePrefab, string via, Heightmap.Biome biome)
		{
			if (creaturePrefab == null)
			{
				return;
			}

			// The name the game registers it under. An instance placed inside a location is often called
			// "Troll (1)", and a reference may land on a child of the creature rather than its root.
			string name = Regex.Replace(Utils.GetPrefabName(creaturePrefab), @"\s*\(\d+\)$", string.Empty);
			GameObject registered = ZNetScene.instance.GetPrefab(name);
			if (registered == null)
			{
				return;
			}

			if (_collectingEvidence)
			{
				AddReferrer(name, via);
				return;
			}

			if (_reached.TryGetValue(name, out Reached existing))
			{
				if (existing.Biome == Heightmap.Biome.None && biome != Heightmap.Biome.None)
				{
					existing.Biome = biome;
				}

				return;
			}

			_reached[name] = new Reached { Via = via, Biome = biome };

			// Follow the prefab the game actually spawns, which is the one ZNetScene holds.
			_toFollow.Enqueue(registered);
		}

		private static void AddReferrer(string name, string via)
		{
			if (!_unreached.Contains(name))
			{
				return;
			}

			if (!_referrers.TryGetValue(name, out List<string> list))
			{
				list = new List<string>();
				_referrers[name] = list;
			}

			if (list.Count < MaxReferrers && !list.Contains(via))
			{
				list.Add(via);
			}
		}

		/// <summary>Reads every component under one object, following references outward.</summary>
		private static void Walk(GameObject root, string via, Heightmap.Biome biome, int hop)
		{
			if (root == null)
			{
				return;
			}

			// The object being read, not its transform root: Jotunn and others park their prefabs under
			// one shared hidden parent, so the root would make every mod prefab look like part of this one.
			Transform previous = _walking;
			_walking = root.transform;

			if (!_collectingEvidence && hop == 0)
			{
				_walkedRoots.Add(root);
			}

			try
			{
				foreach (Component component in root.GetComponentsInChildren<Component>(true))
				{
					if (component == null)
					{
						continue; // a missing script
					}

					// A creature placed directly inside something - a location, a room, or a piece of one that
					// a soft-reference spawner drops in - rather than by a spawner.
					if (component is Character placed && component.gameObject != root)
					{
						Record(placed.gameObject, via, biome);
					}

					WalkData(component, via, biome, hop, 0);
				}
			}
			finally
			{
				_walking = previous;
			}
		}

		private static void WalkData(object data, string via, Heightmap.Biome biome, int hop, int depth)
		{
			foreach (FieldInfo field in FieldsOf(data.GetType()))
			{
				object value;
				try
				{
					value = field.GetValue(data);
				}
				catch
				{
					continue;
				}

				Consider(value, via, biome, hop, depth);
			}
		}

		private static void Consider(object value, string via, Heightmap.Biome biome, int hop, int depth)
		{
			switch (value)
			{
				case null:
					return;

				case GameObject gameObject:
					Follow(gameObject, via, biome, hop);
					return;

				case Component component:
					if (component != null)
					{
						Follow(component.gameObject, via, biome, hop);
					}
					return;

				case SoftReference<GameObject> soft:
					FollowSoft(soft, via, biome, hop);
					return;

				case UnityEngine.Object _:
					return; // materials, meshes, clips: nothing that spawns

				case string text:
					// Some code finds a creature by name rather than by reference. Following can't use
					// that, but as evidence it matters: a creature named by something live is real.
					if (_collectingEvidence && _unreached.Contains(text))
					{
						AddReferrer(text, via + " (names it in text)");
					}
					return;

				case IEnumerable list:
					if (depth >= MaxDataDepth || !_seenData.Add(list)) return;
					foreach (object element in list)
					{
						Consider(element, via, biome, hop, depth + 1);
					}
					return;
			}

			Type type = value.GetType();
			if (type.IsPrimitive || type.IsEnum || type.IsValueType || depth >= MaxDataDepth || !_seenData.Add(value))
			{
				return;
			}

			WalkData(value, via, biome, hop, depth + 1);
		}

		private static void Follow(GameObject target, string via, Heightmap.Biome biome, int hop)
		{
			if (target == null)
			{
				return;
			}

			// References inside the object being read are its own parts, not somewhere else.
			if (_walking != null && target.transform.IsChildOf(_walking))
			{
				return;
			}

			Character creature = target.GetComponentInParent<Character>(true);
			if (creature != null)
			{
				if (creature.GetComponent<Player>() == null)
				{
					Record(creature.gameObject, via, biome);
				}

				return;
			}

			int next = hop + 1;
			if (next > _maxHops || (_bestHop.TryGetValue(target, out int best) && best <= next))
			{
				return;
			}

			_bestHop[target] = next;
			Walk(target, via, biome, next);
		}

		/// <summary>
		/// A prefab held by soft reference: not in memory until something asks for it. Locations and rooms
		/// place a good deal this way, through SoftReferencePrefabSpawner - spawners among it - and skipping
		/// them is what made the fire, ice and support Dvergr mages look as though nothing spawned them.
		///
		/// Its name is known without loading it. If that names a networked prefab the game already holds,
		/// that is followed directly; otherwise it is loaded, read and released again.
		/// </summary>
		private static void FollowSoft(SoftReference<GameObject> soft, string via, Heightmap.Biome biome, int hop)
		{
			if (!soft.IsValid)
			{
				return;
			}

			string name = soft.Name;
			GameObject registered = string.IsNullOrEmpty(name) ? null : ZNetScene.instance.GetPrefab(name);
			if (registered != null)
			{
				Follow(registered, via, biome, hop);
				return;
			}

			int next = hop + 1;
			string key = string.IsNullOrEmpty(name) ? soft.ToString() : name;
			if (next > _maxHops || (_softBestHop.TryGetValue(key, out int best) && best <= next))
			{
				return;
			}

			_softBestHop[key] = next;

			try
			{
				soft.Load();
				GameObject asset = soft.Asset;
				if (asset != null)
				{
					Walk(asset, via, biome, next);
				}
			}
			catch (Exception e)
			{
				Plugin.Log.LogWarning("Creature scan could not read " + key + ": " + e.Message);
			}
			finally
			{
				soft.Release();
			}
		}

		/// <summary>The fields Unity would serialise, which are the ones a prefab can set.</summary>
		private static FieldInfo[] FieldsOf(Type type)
		{
			if (FieldCache.TryGetValue(type, out FieldInfo[] cached))
			{
				return cached;
			}

			List<FieldInfo> fields = new List<FieldInfo>();
			for (Type t = type; t != null && t != typeof(MonoBehaviour) && t != typeof(Component) && t != typeof(object); t = t.BaseType)
			{
				foreach (FieldInfo field in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
				{
					if (field.IsNotSerialized)
					{
						continue;
					}

					bool serialised = field.IsPublic || field.GetCustomAttributes(typeof(SerializeField), true).Length > 0;
					if (!serialised)
					{
						continue;
					}

					Type ft = field.FieldType;
					if (ft.IsPrimitive || ft.IsEnum)
					{
						continue;
					}

					fields.Add(field);
				}
			}

			cached = fields.ToArray();
			FieldCache[type] = cached;
			return cached;
		}

		/// <summary>How a prefab that refers to an unreached creature is named in the report.</summary>
		private static string Describe(GameObject prefab)
		{
			if (prefab.GetComponent<Character>() != null)
			{
				return "creature " + prefab.name + (_reached.ContainsKey(prefab.name) ? " (spawns in game)" : " (itself never spawned)");
			}

			return "prefab " + prefab.name + (_reachedObjects.Contains(prefab) ? " (reached by the scan)" : " (not reached by the scan)");
		}

		private static List<GameObject> AllCreatures()
		{
			return ZNetScene.instance.m_prefabs
				.Where(p => p != null && p.GetComponent<Character>() != null && p.GetComponent<Player>() == null)
				.GroupBy(p => p.name, StringComparer.OrdinalIgnoreCase)
				.Select(g => g.First())
				.OrderBy(p => p.name, StringComparer.OrdinalIgnoreCase)
				.ToList();
		}

		private static string Report(List<GameObject> creatures, int locations, int rooms, List<string> problems, float seconds)
		{
			StringBuilder text = new StringBuilder();
			text.AppendLine("Mark of Oden creature scan, " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
			text.AppendLine("Read " + locations + " live locations and " + rooms + " dungeon rooms, then everything again for evidence, in "
				+ seconds.ToString("0") + "s.");
			text.AppendLine(creatures.Count + " creature prefabs; " + (creatures.Count - _unreached.Count) + " reachable, "
				+ _unreached.Count + " never spawned by anything the scan follows.");

			foreach (string problem in problems)
			{
				text.AppendLine("PROBLEM: " + problem);
			}

			text.AppendLine();
			text.AppendLine("NEVER SPAWNED, and what refers to each:");
			foreach (GameObject prefab in creatures.Where(p => _unreached.Contains(p.name)))
			{
				Character c = prefab.GetComponent<Character>();
				text.AppendLine("  " + prefab.name.PadRight(32) + (c.m_name ?? "").PadRight(34) + Localize(c.m_name));

				if (_referrers != null && _referrers.TryGetValue(prefab.name, out List<string> referrers) && referrers.Count > 0)
				{
					foreach (string referrer in referrers)
					{
						text.AppendLine("      <- " + referrer);
					}
				}
				else
				{
					text.AppendLine("      <- nothing refers to it anywhere");
				}
			}

			text.AppendLine();
			text.AppendLine("REACHABLE:");
			foreach (GameObject prefab in creatures.Where(p => _reached.ContainsKey(p.name)))
			{
				Reached r = _reached[prefab.name];
				Character c = prefab.GetComponent<Character>();
				text.AppendLine("  " + prefab.name.PadRight(32) + Localize(c.m_name).PadRight(30)
					+ (r.Biome == Heightmap.Biome.None ? "-" : r.Biome.ToString()).PadRight(16) + r.Via);
			}

			return text.ToString();
		}

		private static string Localize(string token)
		{
			if (string.IsNullOrEmpty(token)) return "";
			string text = Localization.instance != null ? Localization.instance.Localize(token) : token;
			return string.IsNullOrEmpty(text) ? token : text;
		}

		/// <summary>Identity comparison, so two equal-looking data objects are both read.</summary>
		private sealed class ReferenceComparer : IEqualityComparer<object>
		{
			public static readonly ReferenceComparer Instance = new ReferenceComparer();

			public new bool Equals(object x, object y) => ReferenceEquals(x, y);

			public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
		}
	}
}
