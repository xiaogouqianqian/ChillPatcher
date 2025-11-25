using Bulbul;
using HarmonyLib;
using System.IO;
using UnityEngine;
using ChillPatcher.UIFramework.Music;

namespace ChillPatcher.Patches.UIFramework
{
    /// <summary>
    /// GameAudioInfo补丁集合
    /// </summary>
    [HarmonyPatch]
    public class GameAudioInfo_Patches
    {
        /// <summary>
        /// 扩展音频格式支持 - 可配置开关
        /// 默认关闭，不影响原游戏行为和存档
        /// </summary>
        [HarmonyPatch(typeof(GameAudioInfo), "<DownloadAudioFile>g__GetAudioType|18_0")]
        [HarmonyPrefix]
        static bool GetAudioType_Prefix(string uri, ref AudioType __result)
        {
            // 检查配置开关
            if (!UIFrameworkConfig.EnableExtendedFormats.Value)
            {
                return true; // 配置关闭，执行原方法
            }

            var ext = Path.GetExtension(uri)?.ToLower();

            __result = ext switch
            {
                ".mp3" => AudioType.MPEG,
                ".wav" => AudioType.WAV,
                ".ogg" => AudioType.OGGVORBIS,
                ".egg" => AudioType.OGGVORBIS,  // .egg是伪装的Ogg Vorbis（Beat Saber）
                ".flac" => AudioType.OGGVORBIS,  // Unity会尝试作为OGG处理
                ".aiff" => AudioType.AIFF,
                ".aif" => AudioType.AIFF,
                _ => AudioType.UNKNOWN
            };

            Plugin.Log.LogDebug($"Extended format: {ext} -> {__result}");

            // 阻止原方法执行
            return false;
        }
    }
}
