using Bulbul;
using HarmonyLib;
using ChillPatcher.UIFramework.Music;

namespace ChillPatcher.Patches.UIFramework
{
    /// <summary>
    /// MusicPlayListButtons补丁: 隐藏歌单歌曲的删除按钮
    /// </summary>
    [HarmonyPatch(typeof(MusicPlayListButtons))]
    public class MusicPlayListButtons_Patches
    {
        /// <summary>
        /// Patch Setup方法 - 对歌单歌曲隐藏删除按钮
        /// </summary>
        [HarmonyPatch("Setup")]
        [HarmonyPostfix]
        static void Setup_Postfix(MusicPlayListButtons __instance)
        {
            try
            {
                // 获取当前歌曲信息
                var audioInfo = __instance.AudioInfo;
                if (audioInfo == null)
                    return;
                
                // 检查是否是歌单歌曲 (有自定义Tag)
                var customTags = CustomTagManager.Instance.GetSongCustomTags(audioInfo.UUID);
                if (customTags == 0)
                    return;  // 不是歌单歌曲,保持原样
                
                // ✅ 是歌单歌曲,隐藏删除按钮
                var removeInteractableUI = Traverse.Create(__instance)
                    .Field("removeInteractableUI")
                    .GetValue<InteractableUI>();
                
                if (removeInteractableUI != null)
                {
                    removeInteractableUI.gameObject.SetActive(false);
                    Plugin.Log.LogDebug($"[HideDeleteButton] Hidden delete button for playlist song: {audioInfo.Title}");
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"[HideDeleteButton] Error: {ex}");
            }
        }
    }
}
