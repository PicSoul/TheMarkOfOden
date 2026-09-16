using System;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using MarkOfOden.Compat;
using MarkOfOden.Config;
using MarkOfOden.Fear;
using MarkOfOden.Marks;
using ServerSync;

namespace MarkOfOden
{
	[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
	[BepInDependency(JotunnGuid, BepInDependency.DependencyFlags.SoftDependency)]
	public class Plugin : BaseUnityPlugin
	{
		public const string PluginGuid = "picsoul.valheim.markofoden";
		public const string PluginName = "TheMarkOfOden";
		public const string PluginVersion = "0.1.0";

		/// <summary>
		/// A server running this mod accepts only clients running the same version of it, and turns
		/// away clients without it. ServerSync compares each side's version against the other's
		/// minimum, so setting the minimum to the current version on both sides means the two must
		/// match exactly.
		///
		/// The cost is that every release has to be rolled out to a server and its players together.
		/// That is deliberate: fear is decided on whichever machine owns the creature, so a server
		/// where installs differ is one where the same Greydwarf behaves differently depending on who
		/// happens to be simulating it.
		/// </summary>
		private const string JotunnGuid = "com.jotunn.jotunn";

		public static ManualLogSource Log { get; private set; }

		private Harmony _harmony;

		private readonly ConfigSync _configSync = new ConfigSync(PluginGuid)
		{
			DisplayName = "The Mark of Oden",
			CurrentVersion = PluginVersion,
			MinimumRequiredVersion = PluginVersion,
			ModRequired = true
		};

		private void Awake()
		{
			Log = Logger;

			ModConfig.Init(Config, _configSync);

			// Creature tables and published marks are both derived from config, so they have to be
			// rebuilt whenever the server pushes a change. FleeOnSight needed a restart here; we do not.
			ModConfig.CreatureTierOverrides.SettingChanged += OnTablesChanged;
			ModConfig.FearlessCreatures.SettingChanged += OnTablesChanged;
			ModConfig.NotorietyThresholds.SettingChanged += OnMarkChanged;
			ModConfig.InheritWorldProgress.SettingChanged += OnMarkChanged;
			ModConfig.InheritedWorldTierPenalty.SettingChanged += OnMarkChanged;

			ModConfig.NameplateDistance.SettingChanged += OnNameplateDistanceChanged;

			MarkLedger.Changed += OnMarkChanged;

			_harmony = new Harmony(PluginGuid);
			_harmony.PatchAll(Assembly.GetExecutingAssembly());

			ConsoleCommands.Register();
			JotunnSoft.Init();

			Log.LogInfo(PluginName + " " + PluginVersion + " loaded.");
		}

		private void OnTablesChanged(object sender, EventArgs e)
		{
			try
			{
				if (ZNetScene.instance != null)
				{
					CreatureTiers.Build(ZNetScene.instance);
				}

				FearEvaluator.ClearAll();
				MarkSync.Publish();
			}
			catch (Exception ex)
			{
				Log.LogError("Could not apply a config change: " + ex);
			}
		}

		private void OnNameplateDistanceChanged(object sender, EventArgs e)
		{
			try
			{
				Patches.EnemyHud_Awake_Patch.Apply(EnemyHud.instance);
			}
			catch (Exception ex)
			{
				Log.LogError("Could not apply the nameplate distance: " + ex);
			}
		}

		private void OnMarkChanged(object sender, EventArgs e)
		{
			OnMarkChanged();
		}

		private void OnMarkChanged()
		{
			try
			{
				MarkSync.Publish();
				FearEvaluator.ClearAll();
			}
			catch (Exception ex)
			{
				Log.LogError("Could not publish the mark: " + ex);
			}
		}

		private void OnDestroy()
		{
			MarkLedger.Changed -= OnMarkChanged;
			_harmony?.UnpatchSelf();
		}
	}
}
