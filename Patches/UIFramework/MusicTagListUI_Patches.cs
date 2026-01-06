using System;
using System.Collections.Generic;
using System.Linq;
using Bulbul; // 确保引用了游戏原本的命名空间
using ChillPatcher.ModuleSystem.Registry;
using ChillPatcher.SDK.Models;
using ChillPatcher.UIFramework.Music;
using Cysharp.Threading.Tasks;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ChillPatcher.Patches.UIFramework
{
    /// <summary>
    /// MusicTagListUI补丁：隐藏空Tag + 自定义Tag + 队列操作按钮
    /// </summary>
    [HarmonyPatch(typeof(MusicTagListUI))]
    public class MusicTagListUI_Patches
    {
        private static List<GameObject> _customTagButtons = new List<GameObject>();

        // 队列操作按钮缓存
        private static GameObject _clearAllQueueButton;
        private static GameObject _clearFutureQueueButton;
        private static GameObject _clearHistoryButton;

        // TodoSwitchFinishButton 缓存
        private static GameObject _todoSwitchFinishButton;
        private static bool _todoSwitchFinishButtonWasActive = false;

        // 缓存的原始状态
        private static MusicTagListUI _cachedTagListUI;
        private static bool _isQueueMode = false;

        /// <summary>
        /// Setup后处理：隐藏空Tag + 添加自定义Tag按钮
        /// </summary>
        [HarmonyPatch("Setup")]
        [HarmonyPostfix]
        static void Setup_Postfix(MusicTagListUI __instance)
        {
            try
            {
                _cachedTagListUI = __instance;

                bool hasModuleTags = TagRegistry.Instance?.GetAllTags()?.Count > 0;

                if (PluginConfig.HideEmptyTags.Value)
                {
                    HideEmptyTags(__instance);
                }

                // 无论如何都调用一次，确保列表状态正确
                AddCustomTagButtons(__instance);

                if (PluginConfig.HideEmptyTags.Value || hasModuleTags)
                {
                    UpdateDropdownHeight(__instance);
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"Error in MusicTagListUI_Patches.Setup_Postfix: {ex}");
            }
        }

        /// <summary>
        /// 刷新自定义 Tag 按钮
        /// </summary>
        public static void RefreshCustomTagButtons()
        {
            try
            {
                var tagListUI = UnityEngine.Object.FindObjectOfType<MusicTagListUI>();

                // 尝试查找隐藏的 UI 对象
                if (tagListUI == null)
                {
                    var allUIs = Resources.FindObjectsOfTypeAll<MusicTagListUI>();
                    if (allUIs != null)
                    {
                        foreach (var ui in allUIs)
                        {
                            if (ui.gameObject.scene.IsValid())
                            {
                                tagListUI = ui;
                                break;
                            }
                        }
                    }
                }

                if (tagListUI == null)
                {
                    Plugin.Log.LogWarning("[RefreshCustomTagButtons] Cannot find MusicTagListUI");
                    return;
                }

                _cachedTagListUI = tagListUI;

                AddCustomTagButtons(tagListUI);
                UpdateDropdownHeight(tagListUI);

                Plugin.Log.LogInfo("[RefreshCustomTagButtons] Custom tag buttons refreshed");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"Error in RefreshCustomTagButtons: {ex}");
            }
        }

        private static void HideEmptyTags(MusicTagListUI tagListUI)
        {
            var musicService = Traverse.Create(tagListUI).Field("musicService").GetValue<MusicService>();
            if (musicService == null) return;

            var buttons = Traverse.Create(tagListUI).Field("buttons").GetValue<MusicTagListButton[]>();
            if (buttons == null || buttons.Length == 0) return;

            var allMusicList = musicService.AllMusicList;
            if (allMusicList == null) return;

            foreach (var button in buttons)
            {
                var tag = button.Tag;
                if (tag == AudioTag.All) continue;

                bool hasMusic = allMusicList.Any(audio => audio.Tag.HasFlagFast(tag));
                if (!hasMusic)
                {
                    button.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// 添加自定义Tag按钮
        /// </summary>
        private static void AddCustomTagButtons(MusicTagListUI tagListUI)
        {
            // 清除旧的自定义按钮
            foreach (var btn in _customTagButtons)
            {
                if (btn != null)
                    UnityEngine.Object.Destroy(btn);
            }
            _customTagButtons.Clear();

            var buttons = Traverse.Create(tagListUI).Field("buttons").GetValue<MusicTagListButton[]>();
            if (buttons == null || buttons.Length == 0)
                return;

            var firstButton = buttons[0];
            var container = firstButton.transform.parent;
            var buttonPrefab = firstButton.gameObject;

            var customTags = TagRegistry.Instance?.GetAllTags() ?? new List<TagInfo>();

            foreach (var customTag in customTags)
            {
                // 克隆按钮
                var newButtonObj = UnityEngine.Object.Instantiate(buttonPrefab, container);
                newButtonObj.name = $"CustomTag_{customTag.TagId}";

                // 强制激活并重置缩放
                newButtonObj.SetActive(true);
                newButtonObj.transform.localScale = UnityEngine.Vector3.one;

                var newButton = newButtonObj.GetComponent<MusicTagListButton>();
                if (newButton != null)
                {
                    // 设置 Tag 位值
                    Traverse.Create(newButton).Field("Tag").SetValue((AudioTag)customTag.BitValue);

                    // ✅ 核心修复：不销毁对象，只移除组件和修改文本
                    // 这样可以完美保留原本的 RectTransform 布局、SiblingIndex（层级顺序）和对齐方式
                    var buttonsContainer = newButtonObj.transform.Find("Buttons");
                    if (buttonsContainer != null)
                    {
                        var tagNameTransform = buttonsContainer.Find("TagName");
                        if (tagNameTransform != null)
                        {
                            var tagNameObj = tagNameTransform.gameObject;

                            // 1. 尝试移除本地化组件（防止它把文字改回去）
                            // 即使找不到类型也不会报错，因为我们先检查GetComponent
                            var localization = tagNameObj.GetComponent<TextLocalizationBehaviour>();
                            if (localization != null)
                            {
                                UnityEngine.Object.DestroyImmediate(localization);
                            }

                            // 2. 修改文本内容
                            var tmpText = tagNameObj.GetComponent<TMPro.TextMeshProUGUI>();
                            if (tmpText != null)
                            {
                                // 使用 Trim() 去除前后空格
                                tmpText.text = customTag.DisplayName.Trim();

                                // 保存引用到按钮脚本
                                Traverse.Create(newButton).Field("_text").SetValue(tmpText);
                            }
                        }
                    }

                    // 设置点击事件
                    SetupCustomTagButton(newButton, customTag, tagListUI);

                    // 同步初始选中状态
                    var currentTag = SaveDataManager.Instance.MusicSetting.CurrentAudioTag.Value;
                    bool isActive = currentTag.HasFlagFast((AudioTag)customTag.BitValue);
                    newButton.SetCheck(isActive);

                    _customTagButtons.Add(newButtonObj);
                }
            }

            // 强制刷新布局
            if (container != null)
            {
                Canvas.ForceUpdateCanvases();
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(container as UnityEngine.RectTransform);
            }
        }

        private static void SetupCustomTagButton(MusicTagListButton button, TagInfo customTag, MusicTagListUI tagListUI)
        {
            button.GetComponent<UnityEngine.UI.Button>()?.onClick.AddListener(() =>
            {
                var currentTag = SaveDataManager.Instance.MusicSetting.CurrentAudioTag.Value;
                bool hasTag = currentTag.HasFlagFast((AudioTag)customTag.BitValue);

                if (customTag.IsGrowableList)
                {
                    HandleGrowableTagClick(customTag, hasTag, currentTag, tagListUI);
                }
                else
                {
                    if (hasTag)
                    {
                        SaveDataManager.Instance.MusicSetting.CurrentAudioTag.Value = currentTag.RemoveFlag((AudioTag)customTag.BitValue);
                    }
                    else
                    {
                        SaveDataManager.Instance.MusicSetting.CurrentAudioTag.Value = currentTag.AddFlag((AudioTag)customTag.BitValue);
                    }
                }

                button.SetCheck(!hasTag);
                tagListUI.SetTitle();
            });
        }

        private static void HandleGrowableTagClick(TagInfo clickedTag, bool wasActive, AudioTag currentTag, MusicTagListUI tagListUI)
        {
            var tagRegistry = TagRegistry.Instance;
            if (tagRegistry == null) return;

            if (wasActive)
            {
                SaveDataManager.Instance.MusicSetting.CurrentAudioTag.Value = currentTag.RemoveFlag((AudioTag)clickedTag.BitValue);
                tagRegistry.SetCurrentGrowableTag(null);
            }
            else
            {
                var newTag = currentTag;
                var otherGrowableTags = tagRegistry.GetGrowableTags();
                foreach (var otherTag in otherGrowableTags)
                {
                    if (otherTag.TagId != clickedTag.TagId)
                    {
                        newTag = newTag.RemoveFlag((AudioTag)otherTag.BitValue);
                        UpdateGrowableTagButtonUI(otherTag.TagId, false, tagListUI);
                    }
                }

                newTag = newTag.AddFlag((AudioTag)clickedTag.BitValue);
                SaveDataManager.Instance.MusicSetting.CurrentAudioTag.Value = newTag;
                tagRegistry.SetCurrentGrowableTag(clickedTag.TagId);
            }
        }

        private static void UpdateGrowableTagButtonUI(string tagId, bool isChecked, MusicTagListUI tagListUI)
        {
            var buttonObj = _customTagButtons.FirstOrDefault(b => b.name == $"CustomTag_{tagId}");
            if (buttonObj != null)
            {
                buttonObj.GetComponent<MusicTagListButton>()?.SetCheck(isChecked);
            }
        }

        private static void UpdateDropdownHeight(MusicTagListUI tagListUI)
        {
            var pulldown = Traverse.Create(tagListUI).Field("_pulldown").GetValue<PulldownListUI>();
            if (pulldown == null) return;

            var pullDownParentRect = Traverse.Create(pulldown).Field("_pullDownParentRect").GetValue<UnityEngine.RectTransform>();
            if (pullDownParentRect == null) return;

            int visibleNativeButtonCount = 0;
            var nativeButtons = Traverse.Create(tagListUI).Field("buttons").GetValue<MusicTagListButton[]>();
            if (nativeButtons != null)
            {
                foreach (var btn in nativeButtons)
                {
                    if (btn != null && btn.gameObject.activeSelf)
                        visibleNativeButtonCount++;
                }
            }

            // 统计所有自定义按钮（它们默认都是 active 的）
            int customButtonCount = _customTagButtons.Count;
            int totalVisibleButtonCount = visibleNativeButtonCount + customButtonCount;

            // 重新计算高度
            const float buttonHeight = 45f;
            float a = PluginConfig.TagDropdownHeightMultiplier.Value;
            float b = PluginConfig.TagDropdownHeightOffset.Value;
            float finalHeight = a * (totalVisibleButtonCount * buttonHeight) + b;

            Traverse.Create(pulldown).Field("_openPullDownSizeDeltaY").SetValue(finalHeight);
        }

        #region 队列模式切换 (保留接口以兼容现有代码)

        public static void SwitchToQueueMode()
        {
            if (_isQueueMode) return;
            _isQueueMode = true;
            RefreshCustomTagButtons();
        }

        public static void SwitchToNormalMode()
        {
            if (!_isQueueMode) return;
            _isQueueMode = false;
            RefreshCustomTagButtons();
        }

        public static bool IsQueueMode => _isQueueMode;

        #endregion
    }
}