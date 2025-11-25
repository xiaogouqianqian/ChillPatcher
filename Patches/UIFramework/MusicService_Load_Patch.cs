using Bulbul;
using Cysharp.Threading.Tasks;
using HarmonyLib;
using System;

namespace ChillPatcher.Patches.UIFramework
{
    /// <summary>
    /// 在MusicService.Load后延迟加载自定义歌单
    /// </summary>
    [HarmonyPatch(typeof(MusicService), "Load")]
    public static class MusicService_Load_Patch
    {
        private static bool _playlistsLoaded = false;
        
        [HarmonyPostfix]
        static void Postfix(MusicService __instance)
        {
            if (_playlistsLoaded)
            {
                return; // 已经加载过了
            }
            
            _playlistsLoaded = true;
            
            var logger = BepInEx.Logging.Logger.CreateLogSource("MusicService_Load_Patch");
            
            // ✅ 延迟加载，确保Unity异步系统完全就绪
            UniTask.Void(async () =>
            {
                try
                {
                    // 等待1秒
                    // await UniTask.Delay(TimeSpan.FromSeconds(1));
                    
                    logger.LogInfo("开始加载自定义歌单...");
                    await Plugin.SetupFolderPlaylistsAsync();
                    logger.LogInfo("✅ 自定义歌单加载完成！");
                }
                catch (Exception ex)
                {
                    logger.LogError($"❌ 加载自定义歌单失败: {ex}");
                }
            });
        }
    }
}
