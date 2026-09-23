using System;
using System.Collections;
using System.Collections.Generic;
using MarkOfOden.Config;
using MarkOfOden.Marks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MarkOfOden.Fear
{
	/// <summary>
	/// In-game HUD notification banner that cleanly fades in and away when the player beats a boss
	/// or crosses a creature kill count threshold, earning fear and renown across the realm.
	/// Modeled after the Norse-timber floating cabinet in UsefulObliterator.
	/// </summary>
	public class ProgressPopup : MonoBehaviour
	{
		public static ProgressPopup Instance { get; private set; }

		private CanvasGroup _canvasGroup;
		private RectTransform _panelRect;
		private Image _panelImage;
		private Image _iconImage;
		private TMP_Text _iconFallbackText;
		private TMP_Text _titleText;
		private TMP_Text _headlineText;
		private TMP_Text _sublineText;
		private TMP_Text _detailText;

		private AudioSource _audioSource;
		private AudioClip _chimeClip;

		private readonly Queue<PopupData> _queue = new Queue<PopupData>();
		private Coroutine _displayCoroutine;

		public class PopupData
		{
			public string Title;
			public string TitleColor;
			public string Headline;
			public string Subline;
			public string Detail;
			public Sprite Icon;
			public bool IsBoss;
		}

		public static void EnsureCreated()
		{
			if (Instance != null)
			{
				return;
			}

			if (Hud.instance == null)
			{
				return;
			}

			GameObject obj = new GameObject("TheMarkOfOden_ProgressPopup", typeof(RectTransform), typeof(CanvasGroup));
			obj.transform.SetParent(Hud.instance.transform, false);

			// Fill the HUD, so a child's anchor means a fraction of the screen rather than a
			// fraction of this object. A new RectTransform defaults to a 100x100 box pinned to
			// the middle, and the banner anchored itself near the top of THAT - which put it
			// about thirty pixels above the centre of the screen, right where a fight is.
			RectTransform root = obj.GetComponent<RectTransform>();
			root.anchorMin = Vector2.zero;
			root.anchorMax = Vector2.one;
			root.offsetMin = Vector2.zero;
			root.offsetMax = Vector2.zero;

			Instance = obj.AddComponent<ProgressPopup>();
		}

		private void Awake()
		{
			Instance = this;
			_canvasGroup = GetComponent<CanvasGroup>();
			_canvasGroup.alpha = 0f;
			_canvasGroup.blocksRaycasts = false;
			_canvasGroup.interactable = false;

			InitAudio();
			BuildUI();
		}

		private void InitAudio()
		{
			_audioSource = gameObject.AddComponent<AudioSource>();
			_audioSource.playOnAwake = false;
			_audioSource.spatialBlend = 0f; // 2D UI audio
			_chimeClip = GenerateFanfareClip();
		}

		private void BuildUI()
		{
			TMP_FontAsset font = Hud.instance?.m_buildSelection?.font;

			// Look up Valheim's sliced wood panel sprite
			Sprite woodSprite = null;
			if (InventoryGui.instance != null && InventoryGui.instance.m_container != null)
			{
				woodSprite = InventoryGui.instance.m_container.GetComponent<Image>()?.sprite;
			}

			if (woodSprite == null)
			{
				Sprite[] allSprites = Resources.FindObjectsOfTypeAll<Sprite>();
				foreach (Sprite s in allSprites)
				{
					if (s != null && s.name.StartsWith("woodpanel_settings", StringComparison.OrdinalIgnoreCase))
					{
						woodSprite = s;
						break;
					}

					if (s != null && woodSprite == null && s.name.StartsWith("woodpanel", StringComparison.OrdinalIgnoreCase))
					{
						woodSprite = s;
					}
				}
			}

			// Main Banner Panel (positioned at top-center, width 580, height 120)
			GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
			panel.transform.SetParent(transform, false);
			_panelRect = panel.GetComponent<RectTransform>();
			_panelRect.anchorMin = new Vector2(0.5f, BannerHeight());
			_panelRect.anchorMax = new Vector2(0.5f, BannerHeight());
			_panelRect.pivot = new Vector2(0.5f, 0.5f);
			_panelRect.sizeDelta = new Vector2(580f, 120f);

			_panelImage = panel.GetComponent<Image>();
			if (woodSprite != null)
			{
				_panelImage.sprite = woodSprite;
				_panelImage.type = Image.Type.Sliced;
				_panelImage.color = new Color(0.92f, 0.88f, 0.82f, 0.98f);
			}
			else
			{
				_panelImage.color = new Color(0.10f, 0.08f, 0.06f, 0.96f);
			}

			// Outer Bronze Border Outline
			GameObject border = new GameObject("Border", typeof(RectTransform), typeof(Outline));
			border.transform.SetParent(panel.transform, false);
			RectTransform borderRect = border.GetComponent<RectTransform>();
			borderRect.anchorMin = Vector2.zero;
			borderRect.anchorMax = Vector2.one;
			borderRect.sizeDelta = Vector2.zero;
			Outline outline = border.GetComponent<Outline>();
			outline.effectColor = new Color(0.72f, 0.55f, 0.32f, 0.95f);
			outline.effectDistance = new Vector2(2.5f, -2.5f);

			// Corner Rivets
			CreateRivet(panel.transform, new Vector2(0f, 1f), new Vector2(8f, -8f));
			CreateRivet(panel.transform, new Vector2(1f, 1f), new Vector2(-8f, -8f));
			CreateRivet(panel.transform, new Vector2(0f, 0f), new Vector2(8f, 8f));
			CreateRivet(panel.transform, new Vector2(1f, 0f), new Vector2(-8f, 8f));

			// Top Banner Header Plaque
			GameObject headerObj = new GameObject("HeaderPlaque", typeof(RectTransform), typeof(Image));
			headerObj.transform.SetParent(panel.transform, false);
			RectTransform headerRect = headerObj.GetComponent<RectTransform>();
			headerRect.anchorMin = new Vector2(0f, 1f);
			headerRect.anchorMax = new Vector2(1f, 1f);
			headerRect.pivot = new Vector2(0.5f, 1f);
			headerRect.anchoredPosition = new Vector2(0f, -4f);
			headerRect.sizeDelta = new Vector2(-20f, 26f);
			Image headerImg = headerObj.GetComponent<Image>();
			headerImg.color = new Color(0.05f, 0.04f, 0.03f, 0.95f);

			// Header Title Text
			GameObject titleObj = TextObject("TitleText", font);
			titleObj.transform.SetParent(headerObj.transform, false);
			RectTransform titleRect = titleObj.GetComponent<RectTransform>();
			titleRect.anchorMin = Vector2.zero;
			titleRect.anchorMax = Vector2.one;
			titleRect.sizeDelta = Vector2.zero;
			_titleText = titleObj.GetComponent<TMP_Text>();
			if (font != null) _titleText.font = font;
			_titleText.text = "« THE MARK OF ODEN »";
			_titleText.fontSize = 13f;
			_titleText.alignment = TextAlignmentOptions.Center;
			_titleText.color = new Color(1f, 0.85f, 0.35f);

			// Left: Trophy Icon Frame
			GameObject iconFrame = new GameObject("IconFrame", typeof(RectTransform), typeof(Image), typeof(Outline));
			iconFrame.transform.SetParent(panel.transform, false);
			RectTransform frameRect = iconFrame.GetComponent<RectTransform>();
			frameRect.anchorMin = new Vector2(0f, 0f);
			frameRect.anchorMax = new Vector2(0f, 0f);
			frameRect.pivot = new Vector2(0f, 0f);
			frameRect.anchoredPosition = new Vector2(14f, 12f);
			frameRect.sizeDelta = new Vector2(76f, 76f);
			Image frameImg = iconFrame.GetComponent<Image>();
			frameImg.color = new Color(0.04f, 0.03f, 0.03f, 0.95f);
			Outline frameOutline = iconFrame.GetComponent<Outline>();
			frameOutline.effectColor = new Color(0.68f, 0.52f, 0.30f, 0.90f);
			frameOutline.effectDistance = new Vector2(1.5f, -1.5f);

			// Icon Graphic
			GameObject iconObj = new GameObject("IconImage", typeof(RectTransform), typeof(Image));
			iconObj.transform.SetParent(iconFrame.transform, false);
			RectTransform iconRect = iconObj.GetComponent<RectTransform>();
			iconRect.anchorMin = new Vector2(0.5f, 0.5f);
			iconRect.anchorMax = new Vector2(0.5f, 0.5f);
			iconRect.pivot = new Vector2(0.5f, 0.5f);
			iconRect.anchoredPosition = Vector2.zero;
			iconRect.sizeDelta = new Vector2(68f, 68f);
			_iconImage = iconObj.GetComponent<Image>();
			_iconImage.preserveAspect = true;
			_iconImage.color = Color.white;

			// Fallback Glyph (when no sprite)
			GameObject glyphObj = TextObject("FallbackGlyph", font);
			glyphObj.transform.SetParent(iconFrame.transform, false);
			RectTransform glyphRect = glyphObj.GetComponent<RectTransform>();
			glyphRect.anchorMin = Vector2.zero;
			glyphRect.anchorMax = Vector2.one;
			glyphRect.sizeDelta = Vector2.zero;
			_iconFallbackText = glyphObj.GetComponent<TMP_Text>();
			if (font != null) _iconFallbackText.font = font;
			_iconFallbackText.text = "ᛟ";
			_iconFallbackText.fontSize = 32f;
			_iconFallbackText.alignment = TextAlignmentOptions.Center;
			_iconFallbackText.color = new Color(1f, 0.85f, 0.40f);
			_iconFallbackText.gameObject.SetActive(false);

			// Right Content Area (anchored x: 100 to 560, y: 10 to 86)
			// Headline
			GameObject headlineObj = TextObject("Headline", font);
			headlineObj.transform.SetParent(panel.transform, false);
			RectTransform headlineRect = headlineObj.GetComponent<RectTransform>();
			headlineRect.anchorMin = new Vector2(0f, 0f);
			headlineRect.anchorMax = new Vector2(1f, 0f);
			headlineRect.pivot = new Vector2(0f, 0f);
			headlineRect.anchoredPosition = new Vector2(104f, 62f);
			headlineRect.sizeDelta = new Vector2(-118f, 26f);
			_headlineText = headlineObj.GetComponent<TMP_Text>();
			if (font != null) _headlineText.font = font;
			_headlineText.text = "EIKTHYR SLAIN";
			_headlineText.fontSize = 18f;
			_headlineText.alignment = TextAlignmentOptions.Left;
			_headlineText.color = new Color(1f, 0.90f, 0.35f);

			// Subline
			GameObject sublineObj = TextObject("Subline", font);
			sublineObj.transform.SetParent(panel.transform, false);
			RectTransform sublineRect = sublineObj.GetComponent<RectTransform>();
			sublineRect.anchorMin = new Vector2(0f, 0f);
			sublineRect.anchorMax = new Vector2(1f, 0f);
			sublineRect.pivot = new Vector2(0f, 0f);
			sublineRect.anchoredPosition = new Vector2(104f, 38f);
			sublineRect.sizeDelta = new Vector2(-118f, 22f);
			_sublineText = sublineObj.GetComponent<TMP_Text>();
			if (font != null) _sublineText.font = font;
			_sublineText.text = "Mark Tier I Achieved";
			_sublineText.fontSize = 13f;
			_sublineText.alignment = TextAlignmentOptions.Left;
			_sublineText.color = new Color(0.95f, 0.95f, 0.95f);

			// Detail
			GameObject detailObj = TextObject("Detail", font);
			detailObj.transform.SetParent(panel.transform, false);
			RectTransform detailRect = detailObj.GetComponent<RectTransform>();
			detailRect.anchorMin = new Vector2(0f, 0f);
			detailRect.anchorMax = new Vector2(1f, 0f);
			detailRect.pivot = new Vector2(0f, 0f);
			detailRect.anchoredPosition = new Vector2(104f, 14f);
			detailRect.sizeDelta = new Vector2(-118f, 22f);
			_detailText = detailObj.GetComponent<TMP_Text>();
			if (font != null) _detailText.font = font;
			_detailText.text = "Meadows & Forest denizens grow wary of your presence.";
			_detailText.fontSize = 11f;
			_detailText.alignment = TextAlignmentOptions.Left;
			_detailText.color = new Color(0.60f, 0.85f, 0.95f);
		}

		private static void CreateRivet(Transform parent, Vector2 anchor, Vector2 offset)
		{
			GameObject rivet = new GameObject("Rivet", typeof(RectTransform), typeof(Image), typeof(Outline));
			rivet.transform.SetParent(parent, false);
			RectTransform rt = rivet.GetComponent<RectTransform>();
			rt.anchorMin = anchor;
			rt.anchorMax = anchor;
			rt.pivot = anchor;
			rt.anchoredPosition = offset;
			rt.sizeDelta = new Vector2(6f, 6f);
			rivet.GetComponent<Image>().color = new Color(0.85f, 0.68f, 0.40f);
			Outline o = rivet.GetComponent<Outline>();
			o.effectColor = new Color(0.1f, 0.08f, 0.05f, 0.9f);
			o.effectDistance = new Vector2(1f, -1f);
		}

		public static void ShowBossDefeated(int bossNumber, int newTier)
		{
			if (!ModConfig.EnableProgressionPopups.Value)
			{
				return;
			}

			EnsureCreated();
			if (Instance == null)
			{
				return;
			}

			string bossName = BiomeRegistry.BossDefaultName(bossNumber);
			Heightmap.Biome biome = BiomeRegistry.BossBiome(bossNumber);
			Sprite trophy = FindTrophySprite(bossName, bossNumber);

			PopupData data = new PopupData
			{
				Title = "« THE MARK OF ODEN — BOSS SLAIN »",
				TitleColor = "#FFD700",
				Headline = $"<b><color=#FFD700>{bossName.ToUpperInvariant()} SLAIN</color></b>",
				Subline = $"Mark of Oden Tier {newTier} Achieved!",
				Detail = $"Denizens of the {BiomeRegistry.BiomeName(biome)} grow cautious and fear your wrath.",
				Icon = trophy,
				IsBoss = true
			};

			Instance.Enqueue(data);
		}

		public static void ShowNotorietyIncreased(string creatureToken, int newRank, int kills)
		{
			if (!ModConfig.EnableProgressionPopups.Value)
			{
				return;
			}

			EnsureCreated();
			if (Instance == null)
			{
				return;
			}

			string localizedName = Localization.instance != null ? Localization.instance.Localize(creatureToken) : creatureToken;
			if (localizedName.StartsWith("$")) localizedName = creatureToken;
			Sprite icon = FindTrophySprite(creatureToken, 0);

			string rankRoman = newRank == 1 ? "I" : (newRank == 2 ? "II" : "III");
			string rankColor = newRank == 1 ? "#9BD46A" : (newRank == 2 ? "#5FC9D6" : "#B8DCEA");

			PopupData data = new PopupData
			{
				Title = "« THE MARK OF ODEN — NOTORIETY INCREASED »",
				TitleColor = rankColor,
				Headline = $"<b><color={rankColor}>{localizedName.ToUpperInvariant()}: RANK {rankRoman}</color></b>",
				Subline = $"Slaughter Milestone Reached ({kills} Kills)",
				Detail = $"Threat increased (+{newRank}). {localizedName}s sense your legend and are more afraid of you.",
				Icon = icon,
				IsBoss = false
			};

			Instance.Enqueue(data);
		}

		private void Enqueue(PopupData data)
		{
			_queue.Enqueue(data);
			if (_displayCoroutine == null)
			{
				_displayCoroutine = StartCoroutine(ProcessQueueRoutine());
			}
		}

		private IEnumerator ProcessQueueRoutine()
		{
			while (_queue.Count > 0)
			{
				PopupData data = _queue.Dequeue();
				ApplyData(data);

				if (ModConfig.EnablePopupAudio.Value)
				{
					PlaySound();
				}

				// Fade In
				float fadeIn = 0.3f;
				for (float t = 0f; t < fadeIn; t += Time.deltaTime)
				{
					_canvasGroup.alpha = Mathf.Lerp(0f, 1f, t / fadeIn);
					yield return null;
				}
				_canvasGroup.alpha = 1f;

				// Hold
				float duration = Mathf.Max(1.5f, ModConfig.PopupDisplayDuration.Value);
				float timer = 0f;
				while (timer < duration)
				{
					timer += Time.deltaTime;
					yield return null;
				}

				// Fade Out
				float fadeOut = 0.45f;
				for (float t = 0f; t < fadeOut; t += Time.deltaTime)
				{
					_canvasGroup.alpha = Mathf.Lerp(1f, 0f, t / fadeOut);
					yield return null;
				}
				_canvasGroup.alpha = 0f;

				yield return new WaitForSeconds(0.15f);
			}

			_displayCoroutine = null;
		}

		/// <summary>How far up the screen the banner sits, 1 being the very top.</summary>
		/// <remarks>
		/// Read rather than fixed because the right answer depends on the rest of someone's
		/// HUD, and on how much of the screen they are willing to give up mid-fight. Clamped
		/// well short of either edge: the banner is 120 pixels tall and anchored through its
		/// middle, so a value at the extreme would hang half of it off the screen.
		/// </remarks>
		private static float BannerHeight()
		{
			return Mathf.Clamp(ModConfig.PopupScreenHeight.Value, 0.5f, 0.95f);
		}

		/// <summary>Put the banner where the setting says, in case it changed since the last one.</summary>
		private void PlaceBanner()
		{
			if (_panelRect == null)
			{
				return;
			}

			float height = BannerHeight();
			_panelRect.anchorMin = new Vector2(0.5f, height);
			_panelRect.anchorMax = new Vector2(0.5f, height);
		}

		private void ApplyData(PopupData data)
		{
			PlaceBanner();

			_titleText.text = data.Title;
			_headlineText.text = data.Headline;
			_sublineText.text = data.Subline;
			_detailText.text = data.Detail;

			if (data.Icon != null)
			{
				_iconImage.gameObject.SetActive(true);
				_iconImage.sprite = data.Icon;
				_iconFallbackText.gameObject.SetActive(false);
			}
			else
			{
				_iconImage.gameObject.SetActive(false);
				_iconFallbackText.gameObject.SetActive(true);
				_iconFallbackText.text = data.IsBoss ? "ᛟ" : "✦";
			}
		}

		private void PlaySound()
		{
			if (_audioSource != null && _chimeClip != null)
			{
				_audioSource.PlayOneShot(_chimeClip, 0.85f);
			}
		}

		private static Sprite FindTrophySprite(string creatureOrBossName, int bossNumber)
		{
			if (bossNumber > 0)
			{
				foreach (KeyValuePair<string, int> pair in CreatureTiers.AllBossTrophies)
				{
					if (pair.Value == bossNumber)
					{
						Sprite s = GetItemSprite(pair.Key);
						if (s != null) return s;
					}
				}
			}

			string clean = creatureOrBossName.Replace("$enemy_", "").Replace("$", "");
			string[] candidates = new[]
			{
				"Trophy" + clean,
				"Trophy" + creatureOrBossName,
				clean,
				creatureOrBossName
			};

			foreach (string candidate in candidates)
			{
				Sprite s = GetItemSprite(candidate);
				if (s != null) return s;
			}

			if (ZNetScene.instance != null)
			{
				GameObject prefab = ZNetScene.instance.GetPrefab(creatureOrBossName) ?? ZNetScene.instance.GetPrefab(clean);
				if (prefab != null)
				{
					CharacterDrop drops = prefab.GetComponent<CharacterDrop>();
					if (drops != null && drops.m_drops != null)
					{
						foreach (CharacterDrop.Drop d in drops.m_drops)
						{
							if (d?.m_prefab != null && d.m_prefab.name.StartsWith("Trophy", StringComparison.OrdinalIgnoreCase))
							{
								Sprite s = GetItemSprite(d.m_prefab.name);
								if (s != null) return s;
							}
						}
					}
				}
			}

			return null;
		}

		private static Sprite GetItemSprite(string prefabName)
		{
			if (string.IsNullOrEmpty(prefabName)) return null;

			if (ObjectDB.instance != null)
			{
				GameObject itemObj = ObjectDB.instance.GetItemPrefab(prefabName);
				if (itemObj != null)
				{
					ItemDrop id = itemObj.GetComponent<ItemDrop>();
					if (id?.m_itemData?.m_shared?.m_icons != null && id.m_itemData.m_shared.m_icons.Length > 0)
					{
						return id.m_itemData.m_shared.m_icons[0];
					}
				}
			}

			if (ZNetScene.instance != null)
			{
				GameObject prefab = ZNetScene.instance.GetPrefab(prefabName);
				if (prefab != null)
				{
					ItemDrop id = prefab.GetComponent<ItemDrop>();
					if (id?.m_itemData?.m_shared?.m_icons != null && id.m_itemData.m_shared.m_icons.Length > 0)
					{
						return id.m_itemData.m_shared.m_icons[0];
					}
				}
			}

			return null;
		}

		private static AudioClip GenerateFanfareClip()
		{
			int sampleRate = 44100;
			int length = (int)(sampleRate * 1.5f);
			float[] samples = new float[length];
			float[] freqs = { 440f, 554.37f, 659.25f, 880f }; // A4, C#5, E5, A5
			for (int i = 0; i < length; i++)
			{
				float t = (float)i / sampleRate;
				float sample = 0f;
				for (int n = 0; n < freqs.Length; n++)
				{
					float noteStart = n * 0.14f;
					if (t >= noteStart)
					{
						float noteT = t - noteStart;
						float env = Mathf.Exp(-noteT * 3.2f);
						sample += Mathf.Sin(2f * Mathf.PI * freqs[n] * noteT) * env * 0.25f;
					}
				}
				samples[i] = Mathf.Clamp(sample, -1f, 1f);
			}

			AudioClip clip = AudioClip.Create("MarkOfOden_Fanfare", length, 1, sampleRate, false);
			clip.SetData(samples, 0);
			return clip;
		}

		/// <summary>
		/// A text object built inactive, so TextMeshPro has its font before it wakes.
		/// </summary>
		/// <remarks>
		/// new GameObject(name, typeof(TextMeshProUGUI)) hands back an ACTIVE object, so TMP's
		/// Awake runs there and then - before the next line can hand it Valheim's font. Finding
		/// none, it reaches for TMP_Settings.defaultFontAsset, which is LiberationSans SDF and is
		/// not shipped with the game, and logs a warning for every label built.
		///
		/// The text itself was always right, because the real font is assigned a line later. Only
		/// the log suffered, and it suffered a lot: two dozen warnings per panel, which is enough
		/// noise to hide something that matters.
		///
		/// A GameObject that is inactive when a component is added does not run that component's
		/// Awake until it is activated, so building it inactive puts the font in place before TMP
		/// ever goes looking for one.
		/// </remarks>
		private static GameObject TextObject(string name, TMP_FontAsset font, params Type[] extra)
		{
			GameObject obj = new GameObject(name);
			obj.SetActive(false);

			obj.AddComponent<RectTransform>();
			TextMeshProUGUI text = obj.AddComponent<TextMeshProUGUI>();

			// Added after the text, and in the order they were given, so anything needing a
			// Graphic to sit on still finds one.
			foreach (Type component in extra)
			{
				obj.AddComponent(component);
			}

			if (font != null)
			{
				text.font = font;
			}

			obj.SetActive(true);
			return obj;
		}

	}
}
