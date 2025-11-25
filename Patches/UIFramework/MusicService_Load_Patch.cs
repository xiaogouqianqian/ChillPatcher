using Bulbul;
using Cysharp.Threading.Tasks;
using HarmonyLib;
using System;
using System.Linq;

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
                    
                    // ✅ 从数据库恢复收藏和排序
                    RestoreFromDatabase(__instance);
                }
                catch (Exception ex)
                {
                    logger.LogError($"❌ 加载自定义歌单失败: {ex}");
                }
            });
        }

        /// <summary>
        /// 从数据库恢复自定义Tag的收藏和排序
        /// </summary>
        private static void RestoreFromDatabase(MusicService musicService)
        {
            try
            {
                var manager = ChillPatcher.UIFramework.Data.CustomPlaylistDataManager.Instance;
                if (manager == null)
                    return;

                var logger = BepInEx.Logging.Logger.CreateLogSource("MusicService_Load_Patch");
                logger.LogInfo("开始从数据库恢复收藏和排序...");

                // 获取所有自定义Tag
                var customTags = ChillPatcher.UIFramework.Music.CustomTagManager.Instance.GetAllTags();
                
                // 获取所有音乐列表
                var allMusicList = HarmonyLib.Traverse.Create(musicService)
                    .Field("_allMusicList")
                    .GetValue<System.Collections.Generic.List<GameAudioInfo>>();

                if (allMusicList == null)
                    return;

                int restoredFavorites = 0;
                int restoredOrder = 0;
                int restoredExcluded = 0;

                foreach (var kvp in customTags)
                {
                    var tagId = kvp.Key;
                    var tag = kvp.Value;

                    // 1. 恢复收藏
                    var favorites = manager.GetFavorites(tagId);
                    foreach (var uuid in favorites)
                    {
                        var audio = allMusicList.Find(a => a.UUID == uuid);
                        if (audio != null && !audio.Tag.HasFlagFast(AudioTag.Favorite))
                        {
                            audio.Tag = audio.Tag | AudioTag.Favorite;
                            restoredFavorites++;
                        }
                    }

                    // 2. 恢复排除列表（注意：不修改Tag，只记录状态）
                    var excludedSongs = manager.GetExcludedSongs(tagId);
                    restoredExcluded += excludedSongs.Count;
                    // 排除状态通过 IsContainsExcludedFromPlaylist 检查时从数据库读取

                    // 3. 恢复播放顺序
                    var playlistOrder = manager.GetPlaylistOrder(tagId);
                    if (playlistOrder.Count > 0)
                    {
                        // 按数据库中的顺序重新排列音乐列表
                        var sameTagMusic = allMusicList
                            .Where(a => ChillPatcher.UIFramework.Data.CustomPlaylistDataManager.GetTagIdFromAudio(a) == tagId)
                            .ToList();

                        // 创建UUID到音乐的映射
                        var uuidToMusic = sameTagMusic.ToDictionary(a => a.UUID);

                        // 按数据库顺序重新排列
                        var sortedMusic = new System.Collections.Generic.List<GameAudioInfo>();
                        foreach (var uuid in playlistOrder)
                        {
                            if (uuidToMusic.TryGetValue(uuid, out var audio))
                            {
                                sortedMusic.Add(audio);
                                restoredOrder++;
                            }
                        }

                        // 将排序后的音乐放回列表
                        foreach (var audio in sameTagMusic)
                        {
                            allMusicList.Remove(audio);
                        }

                        // 重新插入到列表末尾（保持自定义Tag音乐在后面）
                        allMusicList.AddRange(sortedMusic);
                    }
                }

                logger.LogInfo($"✅ 从数据库恢复完成: 收藏={restoredFavorites}, 排除={restoredExcluded}, 排序={restoredOrder}");
            }
            catch (Exception ex)
            {
                BepInEx.Logging.Logger.CreateLogSource("MusicService_Load_Patch")
                    .LogError($"从数据库恢复失败: {ex}");
            }
        }
    }
}
