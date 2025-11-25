using Bulbul;
using ChillPatcher.UIFramework;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ChillPatcher.Patches.UIFramework
{
    /// <summary>
    /// MusicUI Patches - 集成虚拟滚动（可配置）
    /// 默认开启，不影响存档，仅优化性能
    /// </summary>
    [HarmonyPatch]
    public class MusicUI_VirtualScroll_Patch
    {
        private static bool _componentsInitialized = false;

        /// <summary>
        /// Patch MusicUI.Setup - 初始化虚拟滚动组件
        /// 使用Prefix确保在Setup内部的ViewPlayList调用之前完成初始化
        /// </summary>
        [HarmonyPatch(typeof(MusicUI), "Setup")]
        [HarmonyPrefix]
        static void Setup_Prefix(MusicUI __instance)
        {
            if (_componentsInitialized || !UIFrameworkConfig.EnableVirtualScroll.Value)
                return;

            try
            {
                // 获取UI组件
                var scrollRect = Traverse.Create(__instance)
                    .Field("scrollRect")
                    .GetValue<UnityEngine.UI.ScrollRect>();

                var playListButtonsPrefab = Traverse.Create(__instance)
                    .Field("_playListButtonsPrefab")
                    .GetValue<GameObject>();

                var playListButtonsParent = Traverse.Create(__instance)
                    .Field("_playListButtonsParent")
                    .GetValue<GameObject>();

                if (scrollRect == null || playListButtonsPrefab == null || playListButtonsParent == null)
                {
                    Plugin.Log.LogError("Failed to get UI components for VirtualScroll");
                    return;
                }

                // 初始化虚拟滚动控制器
                var virtualScroll = ChillUIFramework.Music.VirtualScroll as ChillPatcher.UIFramework.Music.VirtualScrollController;
                if (virtualScroll != null)
                {
                    virtualScroll.ItemHeight = 60f; // 从配置读取
                    virtualScroll.BufferCount = UIFrameworkConfig.VirtualScrollBufferSize.Value;
                    virtualScroll.InitializeComponents(scrollRect, playListButtonsPrefab, playListButtonsParent.transform);
                    _componentsInitialized = true;
                    Plugin.Log.LogInfo("VirtualScrollController initialized with MusicUI components (Prefix)");
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"Error initializing VirtualScroll: {ex}");
            }
        }

        /// <summary>
        /// Patch MusicUI.ViewPlayList - 使用虚拟滚动替代原实现
        /// </summary>
        [HarmonyPatch(typeof(MusicUI), "ViewPlayList")]
        [HarmonyPrefix]
        static bool ViewPlayList_Prefix(MusicUI __instance)
        {
            // 如果虚拟滚动未启用或框架未初始化，执行原方法
            if (!UIFrameworkConfig.EnableVirtualScroll.Value || !ChillUIFramework.IsInitialized)
            {
                return true; // 执行原方法
            }

            try
            {
                var virtualScroll = ChillUIFramework.Music.VirtualScroll as ChillPatcher.UIFramework.Music.VirtualScrollController;
                if (virtualScroll == null)
                {
                    Plugin.Log.LogWarning("VirtualScroll is null, falling back to original method");
                    return true; // 执行原方法
                }

                // 获取_facilityMusic
                var facilityMusic = Traverse.Create(__instance)
                    .Field("_facilityMusic")
                    .GetValue<Bulbul.FacilityMusic>();

                if (facilityMusic == null)
                {
                    Plugin.Log.LogError("Failed to get _facilityMusic from MusicUI");
                    return true;
                }

                // 获取播放列表（游戏已经过滤好的）
                var playingList = Traverse.Create(__instance)
                    .Field("_playingList")
                    .GetValue<ObservableCollections.IReadOnlyObservableList<Bulbul.GameAudioInfo>>();

                if (playingList != null)
                {
                    
                    // 设置FacilityMusic引用
                    virtualScroll.SetFacilityMusic(facilityMusic);
                    
                    // 总是设置数据源（即使Count=0，也要清空旧项）
                    virtualScroll.SetDataSource(playingList);
                    
                }

                // **关键：清除dirty标志，防止无限循环**
                Traverse.Create(__instance).Field("isPlaylistDirty").SetValue(false);

                // 阻止原方法执行（我们用虚拟滚动替代）
                return false;
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"Error in ViewPlayList patch: {ex}");
                return true; // 出错时执行原方法
            }
        }

        // TODO: OnChangeMusic Patch需要正确的MusicChangeKind类型
        // 暂时注释，等确认正确的类型签名后再启用
        /*
        /// <summary>
        /// Patch MusicUI.OnChangeMusic - 滚动到当前播放的歌曲
        /// </summary>
        [HarmonyPatch(typeof(MusicUI), "OnChangeMusic")]
        [HarmonyPostfix]
        static void OnChangeMusic_Postfix(MusicUI __instance, string musicTitle)
        {
            if (!UIFrameworkConfig.EnableVirtualScroll.Value || !ChillUIFramework.IsInitialized)
                return;

            try
            {
                var virtualScroll = ChillUIFramework.Music.VirtualScroll;
                var playingList = Traverse.Create(__instance)
                    .Field("_playingList")
                    .GetValue<ObservableCollections.IReadOnlyObservableList<Bulbul.GameAudioInfo>>();

                if (virtualScroll != null && playingList != null)
                {
                    // 找到当前播放歌曲的索引
                    var index = playingList
                        .Select((song, i) => new { song, i })
                        .FirstOrDefault(x => x.song.Title == musicTitle)
                        ?.i ?? -1;

                    if (index >= 0)
                    {
                        virtualScroll.ScrollToItem(index, smooth: true);
                    }
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"Error scrolling to current music: {ex}");
            }
        }
        */
    }
}
