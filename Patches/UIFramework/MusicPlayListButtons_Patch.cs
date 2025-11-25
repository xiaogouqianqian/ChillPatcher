using Bulbul;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ChillPatcher.Patches.UIFramework
{
    /// <summary>
    /// MusicPlayListButtons Patch - 移除拖动柄
    /// </summary>
    [HarmonyPatch]
    public class MusicPlayListButtons_ReorderPatch
    {
        /// <summary>
        /// Patch MusicPlayListButtons.Setup - 清理旧订阅并移除拖动柄
        /// </summary>
        [HarmonyPatch(typeof(MusicPlayListButtons), "Setup", typeof(GameAudioInfo), typeof(FacilityMusic))]
        [HarmonyPrefix]
        static void Setup_Prefix(MusicPlayListButtons __instance)
        {
            if (!UIFrameworkConfig.EnableVirtualScroll.Value)
                return;

            try
            {
                // 清理Button的onClick监听器
                var playButton = Traverse.Create(__instance)
                    .Field("_playMusicbutton")
                    .GetValue<UnityEngine.UI.Button>();
                    
                if (playButton != null)
                {
                    playButton.onClick.RemoveAllListeners();
                }

                Plugin.Log.LogDebug("[ReorderPatch] Cleaned up button listeners");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"Error cleaning subscriptions: {ex}");
            }
        }

        /// <summary>
        /// Patch MusicPlayListButtons.Setup - 移除拖动柄
        /// </summary>
        [HarmonyPatch(typeof(MusicPlayListButtons), "Setup", typeof(GameAudioInfo), typeof(FacilityMusic))]
        [HarmonyPostfix]
        static void Setup_Postfix(MusicPlayListButtons __instance, GameAudioInfo audioInfo, FacilityMusic facilityMusic)
        {
            if (!UIFrameworkConfig.EnableVirtualScroll.Value)
                return;

            try
            {
                // 获取原来的拖动柄
                var reorderTrigger = Traverse.Create(__instance)
                    .Field("reorderTrigger")
                    .GetValue<EventTrigger>();

                if (reorderTrigger != null)
                {
                    // 直接销毁拖动柄GameObject，让HorizontalLayoutGroup重新布局
                    Object.Destroy(reorderTrigger.gameObject);
                    Plugin.Log.LogDebug($"[ReorderPatch] Removed reorder handle for {audioInfo.Title}");
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"Error removing reorder handle: {ex}");
            }
        }
    }
}
