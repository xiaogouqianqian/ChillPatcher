using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Bulbul;
using ChillPatcher.UIFramework.Core;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace ChillPatcher.UIFramework.Audio
{
    /// <summary>
    /// 音频加载器实现
    /// </summary>
    public class AudioLoader : IAudioLoader
    {
        private static readonly string[] SUPPORTED_FORMATS = { ".mp3", ".wav", ".ogg", ".egg", ".flac", ".aiff", ".aif" };

        public IReadOnlyList<string> SupportedFormats => SUPPORTED_FORMATS;

        public bool IsSupportedFormat(string filePath)
        {
            var extension = Path.GetExtension(filePath)?.ToLower();
            return SUPPORTED_FORMATS.Contains(extension);
        }

        public async Task<GameAudioInfo> LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogWarning($"File not found: {filePath}");
                return null;
            }

            if (!IsSupportedFormat(filePath))
            {
                BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogWarning($"Unsupported format: {filePath}");
                return null;
            }

            try
            {
                var logger = BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework");
                logger.LogInfo($"[AudioLoader] Loading file: {filePath}");
                
                // ✅ 游戏直接传Windows路径，不需要转换为file:/// URI！
                // 使用Harmony反向补丁调用private方法
                var result = await Patches.UIFramework.GameAudioInfo_ReversePatch.DownloadAudioFile(
                    filePath,  // 直接使用Windows路径
                    CancellationToken.None
                );
                var audioClip = result.Item1;
                var title = result.Item2;
                var credit = result.Item3;
                
                logger.LogInfo($"[AudioLoader] DownloadAudioFile result - AudioClip={(audioClip != null ? "OK" : "NULL")}, Title='{title}', Credit='{credit}'");
                
                if (audioClip == null)
                {
                    logger.LogWarning($"Failed to load AudioClip: {filePath}");
                    return null;
                }
                
                // 如果没有title，从文件名提取（使用Path.GetFileNameWithoutExtension）
                if (string.IsNullOrEmpty(title))
                {
                    title = Path.GetFileNameWithoutExtension(filePath);
                }
                
                audioClip.name = title;
                
                // 完全按照游戏的方式创建GameAudioInfo
                return new GameAudioInfo
                {
                    IsUnlocked = true,
                    PathType = AudioMode.LocalPc,
                    AudioClip = audioClip,
                    Tag = AudioTag.Local,
                    Title = title,
                    Credit = credit,
                    LocalPath = filePath,
                    UUID = Guid.NewGuid().ToString()
                };
            }
            catch (Exception ex)
            {
                BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogError($"Failed to load audio from {filePath}: {ex}");
                return null;
            }
        }

        public async Task<List<GameAudioInfo>> LoadFromDirectory(string directoryPath, bool recursive = false)
        {
            if (!Directory.Exists(directoryPath))
            {
                BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogWarning($"Directory not found: {directoryPath}");
                return new List<GameAudioInfo>();
            }

            var result = new List<GameAudioInfo>();
            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

            try
            {
                var audioFiles = Directory.GetFiles(directoryPath, "*.*", searchOption)
                    .Where(IsSupportedFormat)
                    .ToList();

                BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogInfo($"Found {audioFiles.Count} audio files in {directoryPath}");

                foreach (var file in audioFiles)
                {
                    var audioInfo = await LoadFromFile(file);
                    if (audioInfo != null)
                    {
                        result.Add(audioInfo);
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogError($"Failed to load from directory {directoryPath}: {ex}");
                return result;
            }
        }

        public void UnloadAudio(GameAudioInfo audio)
        {
            if (audio == null)
                return;

            try
            {
                audio.UnloadAudioClip();
            }
            catch (Exception ex)
            {
                BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogError($"Failed to unload audio {audio.Title}: {ex}");
            }
        }

        private string GenerateUUID(string filePath)
        {
            // 使用文件路径生成确定性UUID
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(filePath));
                return new Guid(hash).ToString();
            }
        }
    }
}

