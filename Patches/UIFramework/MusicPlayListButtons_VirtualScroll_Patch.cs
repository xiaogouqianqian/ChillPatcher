using System.Reflection;
using Bulbul;
using DG.Tweening;
using HarmonyLib;
using UnityEngine;

namespace ChillPatcher.Patches.UIFramework
{
    /// <summary>
    /// 修复虚拟滚动中 MusicPlayListButtons 的状态重置问题
    /// 当按钮从对象池中重用时，确保其内部状态被正确重置
    /// </summary>
    [HarmonyPatch(typeof(MusicPlayListButtons))]
    public static class MusicPlayListButtons_VirtualScroll_Patch
    {
        private static FieldInfo _isMouseOverField;
        private static FieldInfo _isDirtyField;
        private static FieldInfo _isDragField;

        // HoldButtonAnimation 的私有字段
        private static FieldInfo _holdButtonIsMouseOveredField;
        private static FieldInfo _holdButtonHoverScaledField;
        private static FieldInfo _holdButtonClickScaledField;
        private static FieldInfo _holdButtonIsActivatedField;
        private static FieldInfo _holdButtonDefaultScaleField;

        static MusicPlayListButtons_VirtualScroll_Patch()
        {
            // 使用反射获取 MusicPlayListButtons 的私有字段
            var type = typeof(MusicPlayListButtons);
            _isMouseOverField = type.GetField("isMouseOver", BindingFlags.NonPublic | BindingFlags.Instance);
            _isDirtyField = type.GetField("isDirty", BindingFlags.NonPublic | BindingFlags.Instance);
            _isDragField = type.GetField("isDrag", BindingFlags.NonPublic | BindingFlags.Instance);

            // 使用反射获取 HoldButtonAnimation 的私有字段
            var holdButtonType = typeof(HoldButtonAnimation);
            _holdButtonIsMouseOveredField = holdButtonType.GetField("isMouseOvered", BindingFlags.NonPublic | BindingFlags.Instance);
            _holdButtonHoverScaledField = holdButtonType.GetField("hoverScaled", BindingFlags.NonPublic | BindingFlags.Instance);
            _holdButtonClickScaledField = holdButtonType.GetField("clickScaled", BindingFlags.NonPublic | BindingFlags.Instance);
            _holdButtonIsActivatedField = holdButtonType.GetField("isActivated", BindingFlags.NonPublic | BindingFlags.Instance);
            _holdButtonDefaultScaleField = holdButtonType.GetField("defaultScale", BindingFlags.NonPublic | BindingFlags.Instance);

            if (_isMouseOverField == null || _isDirtyField == null || _isDragField == null)
            {
                Plugin.Logger.LogError("[MusicPlayListButtons_VirtualScroll_Patch] Failed to find MusicPlayListButtons private fields via reflection");
            }

            if (_holdButtonIsMouseOveredField == null || _holdButtonHoverScaledField == null || 
                _holdButtonClickScaledField == null || _holdButtonIsActivatedField == null || 
                _holdButtonDefaultScaleField == null)
            {
                Plugin.Logger.LogError("[MusicPlayListButtons_VirtualScroll_Patch] Failed to find HoldButtonAnimation private fields via reflection");
            }
        }

        /// <summary>
        /// 在 Setup 方法执行后，重置按钮的内部状态
        /// 这确保了虚拟滚动重用按钮时，所有状态都是干净的
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(nameof(MusicPlayListButtons.Setup))]
        public static void Setup_Postfix(MusicPlayListButtons __instance)
        {
            if (!UIFrameworkConfig.EnableVirtualScroll.Value)
                return;

            try
            {
                // 1. 杀死所有 DOTween 动画（关键！）
                __instance.transform.DOKill();

                // 2. 强制重置 localScale 到原始大小
                __instance.transform.localScale = Vector3.one;

                // 3. 重置 MusicPlayListButtons 的状态字段
                _isMouseOverField?.SetValue(__instance, false);
                _isDirtyField?.SetValue(__instance, true);  // 设置为 true 以触发一次更新
                _isDragField?.SetValue(__instance, false);

                // 4. 重置所有 HoldButtonAnimation 组件的状态
                var holdButtonAnims = __instance.GetComponentsInChildren<HoldButtonAnimation>(true);
                foreach (var holdAnim in holdButtonAnims)
                {
                    if (holdAnim != null)
                    {
                        // 杀死该组件上的 DOTween 动画
                        holdAnim.transform.DOKill();
                        
                        // 重置 transform scale
                        holdAnim.transform.localScale = Vector3.one;

                        // 重置内部状态
                        _holdButtonIsMouseOveredField?.SetValue(holdAnim, false);
                        _holdButtonHoverScaledField?.SetValue(holdAnim, false);
                        _holdButtonClickScaledField?.SetValue(holdAnim, false);
                        _holdButtonIsActivatedField?.SetValue(holdAnim, false);
                        _holdButtonDefaultScaleField?.SetValue(holdAnim, Vector3.one);
                    }
                }

                Plugin.Logger.LogDebug($"[MusicPlayListButtons_VirtualScroll_Patch] Reset state and animations for button: {__instance.AudioInfo?.Title}");
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogError($"[MusicPlayListButtons_VirtualScroll_Patch] Error resetting state: {ex.Message}");
            }
        }
    }
}
