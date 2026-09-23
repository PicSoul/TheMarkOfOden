using System;
using HarmonyLib;
using MarkOfOden.Config;
using MarkOfOden.Fear;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MarkOfOden.Patches
{
	/// <summary>
	/// Injects a Norse 'Mark of Oden' tab / button into InventoryGui so players can open the
	/// Standings and Stats page directly from their inventory screen.
	/// </summary>
	[HarmonyPatch(typeof(InventoryGui), "Awake")]
	public static class InventoryGui_Awake_Patch
	{
		private static GameObject _standingsBtnObj;

		private static void Postfix(InventoryGui __instance)
		{
			try
			{
				if (!ModConfig.ShowStandingsInventoryButton.Value || __instance == null || __instance.m_tabUpgrade == null)
				{
					return;
				}

				if (_standingsBtnObj != null)
				{
					return;
				}

				// Clone m_tabUpgrade to inherit native Valheim button styling, sound effects, and text components
				_standingsBtnObj = UnityEngine.Object.Instantiate(__instance.m_tabUpgrade.gameObject, __instance.m_tabUpgrade.transform.parent, false);
				_standingsBtnObj.name = "TheMarkOfOden_StandingsTab";

				RectTransform rt = _standingsBtnObj.GetComponent<RectTransform>();
				RectTransform refRt = __instance.m_tabUpgrade.GetComponent<RectTransform>();
				if (rt != null && refRt != null)
				{
					// Position next to the upgrade tab
					rt.anchoredPosition = refRt.anchoredPosition + new Vector2(refRt.sizeDelta.x + 8f, 0f);
				}

				TMP_Text text = _standingsBtnObj.GetComponentInChildren<TMP_Text>();
				if (text != null)
				{
					text.text = "Mark of Oden";
					text.fontSize = Mathf.Min(text.fontSize, 13f);
				}

				Button btn = _standingsBtnObj.GetComponent<Button>();
				if (btn != null)
				{
					btn.onClick = new Button.ButtonClickedEvent();
					btn.onClick.AddListener(() =>
					{
						StandingsPanel.Toggle();
					});
				}
			}
			catch (Exception e)
			{
				Plugin.Log.LogError("Could not inject Standings button into InventoryGui: " + e);
			}
		}
	}

	/// <summary>
	/// Closes the Standings panel if the inventory GUI is closed while it is open.
	/// </summary>
	[HarmonyPatch(typeof(InventoryGui), "Hide")]
	public static class InventoryGui_Hide_Patch
	{
		private static void Postfix()
		{
			if (StandingsPanel.IsOpen)
			{
				StandingsPanel.Close();
			}
		}
	}
}

