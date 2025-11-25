using System;
using System.Collections.Generic;
using System.Linq;
using Bulbul;
using ChillPatcher.UIFramework.Music;
using HarmonyLib;
using UnityEngine;

namespace ChillPatcher.Patches.UIFramework
{
    /// <summary>
    /// MusicTagListUI补丁：隐藏空Tag + 自定义Tag
    /// </summary>
    [HarmonyPatch(typeof(MusicTagListUI))]
    public class MusicTagListUI_Patches
    {
        private static List<GameObject> _customTagButtons = new List<GameObject>();

        /// <summary>
        /// Setup后处理：隐藏空Tag + 添加自定义Tag按钮
        /// </summary>
        [HarmonyPatch("Setup")]
        [HarmonyPostfix]
        static void Setup_Postfix(MusicTagListUI __instance)
        {
            try
            {
                // 1. 隐藏空Tag功能
                if (PluginConfig.HideEmptyTags.Value)
                {
                    HideEmptyTags(__instance);
                }

                // 2. 添加自定义Tag按钮
                if (PluginConfig.EnableFolderPlaylists.Value)
                {
                    AddCustomTagButtons(__instance);
                }

                // 3. 更新下拉框高度
                if (PluginConfig.HideEmptyTags.Value || PluginConfig.EnableFolderPlaylists.Value)
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
        /// 隐藏没有歌曲的Tag按钮
        /// </summary>
        private static void HideEmptyTags(MusicTagListUI tagListUI)
        {
            // 获取MusicService引用
            var musicService = Traverse.Create(tagListUI)
                .Field("musicService")
                .GetValue<MusicService>();

            if (musicService == null)
                return;

            // 获取所有Tag按钮
            var buttons = Traverse.Create(tagListUI)
                .Field("buttons")
                .GetValue<MusicTagListButton[]>();

            if (buttons == null || buttons.Length == 0)
                return;

            // 获取所有歌曲列表
            var allMusicList = musicService.AllMusicList;
            if (allMusicList == null)
                return;

            // 检查每个Tag按钮
            foreach (var button in buttons)
            {
                var tag = button.Tag;

                // 跳过All（总是显示）
                if (tag == AudioTag.All)
                    continue;

                // 检查是否有歌曲属于这个Tag
                bool hasMusic = allMusicList.Any(audio => audio.Tag.HasFlagFast(tag));

                // 如果没有歌曲，隐藏这个按钮
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

            // 获取按钮容器
            var buttons = Traverse.Create(tagListUI).Field("buttons").GetValue<MusicTagListButton[]>();
            if (buttons == null || buttons.Length == 0)
                return;

            // ✅ 获取MusicService以便同步按钮状态
            var musicService = Traverse.Create(tagListUI).Field("musicService").GetValue<MusicService>();
            if (musicService == null)
            {
                Plugin.Log.LogWarning("[AddCustomTagButtons] MusicService is null, cannot sync button states");
                return;
            }

            // 获取TagList容器（父物体）
            var firstButton = buttons[0];
            var container = firstButton.transform.parent;

            // 获取按钮预制体（克隆第一个按钮）
            var buttonPrefab = firstButton.gameObject;

            // 添加自定义Tag按钮
            var customTags = CustomTagManager.Instance.GetAllTags();
            foreach (var tagKvp in customTags)
            {
                var customTag = tagKvp.Value;

                // 克隆按钮
                var newButtonObj = UnityEngine.Object.Instantiate(buttonPrefab, container);
                newButtonObj.name = $"CustomTag_{customTag.Id}";

                var newButton = newButtonObj.GetComponent<MusicTagListButton>();
                if (newButton != null)
                {
                    // ✅ 设置按钮Tag为实际的位值
                    Traverse.Create(newButton).Field("Tag").SetValue(customTag.BitValue);

                    // ✅ 找到Buttons/TagName子物体并替换为纯Text
                    var buttonsContainer = newButtonObj.transform.Find("Buttons");
                    if (buttonsContainer != null)
                    {
                        // 找到原来的TagName
                        var oldTagName = buttonsContainer.Find("TagName");
                        if (oldTagName != null)
                        {
                            // 保存布局和样式信息
                            var oldRect = oldTagName.GetComponent<RectTransform>();
                            var oldText = oldTagName.GetComponent<TMPro.TMP_Text>();
                            
                            // 记录位置信息
                            Vector2 anchorMin = oldRect.anchorMin;
                            Vector2 anchorMax = oldRect.anchorMax;
                            Vector2 anchoredPosition = oldRect.anchoredPosition;
                            Vector2 sizeDelta = oldRect.sizeDelta;
                            Vector2 pivot = oldRect.pivot;
                            Vector3 localScale = oldRect.localScale;
                            
                            // 记录文本样式
                            TMPro.TMP_FontAsset font = oldText.font;
                            float fontSize = oldText.fontSize;
                            Color color = oldText.color;
                            TMPro.TextAlignmentOptions alignment = oldText.alignment;
                            bool enableAutoSizing = oldText.enableAutoSizing;
                            float fontSizeMin = oldText.fontSizeMin;
                            float fontSizeMax = oldText.fontSizeMax;
                            bool raycastTarget = oldText.raycastTarget;
                            
                        // 销毁旧的TagName（带本地化组件）
                        UnityEngine.Object.Destroy(oldTagName.gameObject);                            // 创建新的TagName（不带本地化组件）
                            var newTagName = new GameObject("TagName");
                            newTagName.transform.SetParent(buttonsContainer, false);
                            
                            // 复制RectTransform
                            var newRect = newTagName.AddComponent<RectTransform>();
                            newRect.anchorMin = anchorMin;
                            newRect.anchorMax = anchorMax;
                            newRect.anchoredPosition = anchoredPosition;
                            newRect.sizeDelta = sizeDelta;
                            newRect.pivot = pivot;
                            newRect.localScale = localScale;
                            
                            // 添加TMP_Text（复制样式但不添加本地化组件）
                            var newText = newTagName.AddComponent<TMPro.TextMeshProUGUI>();
                            newText.text = customTag.DisplayName;  // ← 设置自定义文本
                            newText.font = font;
                            newText.fontSize = fontSize;
                            newText.color = color;
                            newText.alignment = alignment;
                            newText.enableAutoSizing = enableAutoSizing;
                            newText.fontSizeMin = fontSizeMin;
                            newText.fontSizeMax = fontSizeMax;
                            newText.raycastTarget = raycastTarget;
                            
                            // 保存到MusicTagListButton的_text字段
                            Traverse.Create(newButton).Field("_text").SetValue(newText);
                            
                            Plugin.Log.LogInfo($"[CustomTag] Created pure text button: {customTag.DisplayName}");
                        }
                    }

                    // ✅ 设置点击事件（直接操作MusicService.CurrentAudioTag）
                    SetupCustomTagButton(newButton, customTag, tagListUI);
                    
                    // ✅ 同步按钮初始状态 (根据CurrentAudioTag是否包含该位)
                    var currentTag = musicService.CurrentAudioTag.Value;
                    bool isActive = currentTag.HasFlagFast(customTag.BitValue);
                    newButton.SetCheck(isActive);
                    Plugin.Log.LogDebug($"[CustomTag] Button '{customTag.DisplayName}' initial state: {(isActive ? "Checked" : "Unchecked")} (CurrentTag: {currentTag})");

                    _customTagButtons.Add(newButtonObj);
                }
            }

            // ✅ 添加完所有自定义Tag后，强制刷新容器布局
            if (_customTagButtons.Count > 0 && container != null)
            {
                Canvas.ForceUpdateCanvases();
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(container as UnityEngine.RectTransform);
                Plugin.Log.LogInfo($"[AddCustomTagButtons] Added {_customTagButtons.Count} custom tag buttons, forced layout rebuild");
            }
        }

        /// <summary>
        /// 设置自定义Tag按钮点击事件
        /// ✅ 直接操作MusicService.CurrentAudioTag，完全复用游戏筛选逻辑
        /// </summary>
        private static void SetupCustomTagButton(MusicTagListButton button, CustomTag customTag, MusicTagListUI tagListUI)
        {
            var musicService = Traverse.Create(tagListUI).Field("musicService").GetValue<MusicService>();
            if (musicService == null)
                return;

            // 订阅按钮点击
            button.GetComponent<UnityEngine.UI.Button>()?.onClick.AddListener(() =>
            {
                // 获取当前Tag状态
                var currentTag = musicService.CurrentAudioTag.Value;
                bool hasTag = currentTag.HasFlagFast(customTag.BitValue);

                // ✅ 使用位运算切换
                if (hasTag)
                {
                    musicService.CurrentAudioTag.Value = currentTag.RemoveFlag(customTag.BitValue);
                    Plugin.Log.LogInfo($"[CustomTag] Removed: {customTag.DisplayName} ({customTag.BitValue})");
                }
                else
                {
                    musicService.CurrentAudioTag.Value = currentTag.AddFlag(customTag.BitValue);
                    Plugin.Log.LogInfo($"[CustomTag] Added: {customTag.DisplayName} ({customTag.BitValue})");
                }

                // 更新按钮UI
                button.SetCheck(!hasTag);
                
                // ✅ 调用SetTitle更新标题显示
                var setTitleMethod = typeof(MusicTagListUI).GetMethod("SetTitle", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                setTitleMethod?.Invoke(tagListUI, null);
                
                // ✅ CurrentAudioTag变化会自动触发游戏的筛选逻辑！
                // 不需要手动调用ApplyFilter，游戏已经订阅了ReactiveProperty
            });
        }

        /// <summary>
        /// 更新下拉框高度
        /// </summary>
        private static void UpdateDropdownHeight(MusicTagListUI tagListUI)
        {
            var pulldown = Traverse.Create(tagListUI)
                .Field("_pulldown")
                .GetValue<PulldownListUI>();

            if (pulldown == null)
                return;

            // 获取下拉列表的Content
            var pullDownParentRect = Traverse.Create(pulldown)
                .Field("_pullDownParentRect")
                .GetValue<UnityEngine.RectTransform>();

            if (pullDownParentRect == null)
                return;

            // 重新计算打开时的高度（根据内容实际高度）
            var contentTransform = pullDownParentRect.Find("TagList");
            if (contentTransform == null)
                return;

            // ✅ 统计实际显示的按钮数量（排除被HideEmptyTags隐藏的）
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
            
            int customButtonCount = _customTagButtons.Count;
            int totalVisibleButtonCount = visibleNativeButtonCount + customButtonCount;
            
            Plugin.Log.LogInfo($"[UpdateDropdownHeight] Native (visible): {visibleNativeButtonCount}, Custom: {customButtonCount}, Total: {totalVisibleButtonCount}");

            // 强制刷新布局（确保自定义按钮也被计算）
            Canvas.ForceUpdateCanvases();
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(pullDownParentRect);
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(contentTransform as UnityEngine.RectTransform);

            // ✅ 直接根据按钮数量计算内容高度
            const float buttonHeight = 50f;  // 每个按钮的高度
            
            // 应用用户配置的线性公式：finalHeight = a × (按钮数 × 高度) + b
            float a = PluginConfig.TagDropdownHeightMultiplier.Value;
            float b = PluginConfig.TagDropdownHeightOffset.Value;
            float finalHeight = a * (totalVisibleButtonCount * buttonHeight) + b;

            // 更新下拉框打开时的目标高度
            Traverse.Create(pulldown).Field("_openPullDownSizeDeltaY").SetValue(finalHeight);

            Plugin.Log.LogInfo($"Tag dropdown: {a} × ({totalVisibleButtonCount} × {buttonHeight}) + {b} = {finalHeight:F1}");
        }
    }
}
