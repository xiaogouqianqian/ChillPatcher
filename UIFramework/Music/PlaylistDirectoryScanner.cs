using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ChillPatcher.UIFramework.Audio;
using ChillPatcher.UIFramework.Core;
using UnityEngine;

namespace ChillPatcher.UIFramework.Music
{
    /// <summary>
    /// 歌单目录扫描器 - 递归扫描playlist根目录
    /// </summary>
    public class PlaylistDirectoryScanner
    {
        private readonly string _rootPath;
        private readonly int _maxDepth;
        private readonly IAudioLoader _audioLoader;

        public PlaylistDirectoryScanner(string rootPath, int maxDepth, IAudioLoader audioLoader)
        {
            _rootPath = rootPath ?? throw new ArgumentNullException(nameof(rootPath));
            _maxDepth = maxDepth;
            _audioLoader = audioLoader ?? throw new ArgumentNullException(nameof(audioLoader));
        }

        /// <summary>
        /// 扫描所有歌单目录
        /// </summary>
        public List<FileSystemPlaylistProvider> ScanAllPlaylists()
        {
            var playlists = new List<FileSystemPlaylistProvider>();

            if (!Directory.Exists(_rootPath))
            {
                BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogWarning($"[Scanner] 歌单根目录不存在: {_rootPath}");
                Directory.CreateDirectory(_rootPath);
                BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogInfo($"[Scanner] 已创建根目录: {_rootPath}");
                return playlists;
            }

            BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogInfo($"[Scanner] 开始扫描: {_rootPath} (深度: {_maxDepth})");

            // 递归扫描
            ScanDirectory(_rootPath, 0, playlists);

            BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogInfo($"[Scanner] 扫描完成，发现 {playlists.Count} 个歌单");

            return playlists;
        }

        /// <summary>
        /// 递归扫描目录
        /// </summary>
        private void ScanDirectory(string currentPath, int currentDepth, List<FileSystemPlaylistProvider> playlists)
        {
            // 检查深度限制
            if (currentDepth > _maxDepth)
                return;

            try
            {
                // 获取当前目录的音频文件
                var audioFiles = GetAudioFiles(currentPath);

                // 如果当前目录有音频文件，创建歌单
                if (audioFiles.Any())
                {
                    try
                    {
                        // ✅ 不传入tag参数，让FileSystemPlaylistProvider自动注册并分配位值
                        var provider = new FileSystemPlaylistProvider(
                            currentPath,
                            _audioLoader
                        );

                        playlists.Add(provider);

                        BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogInfo(
                            $"[Scanner] 发现歌单: {provider.DisplayName} ({audioFiles.Count} 个音频文件)"
                        );
                    }
                    catch (Exception ex)
                    {
                        BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogError($"[Scanner] 创建歌单失败 '{currentPath}': {ex.Message}");
                    }
                }

                // 递归扫描子目录
                var subdirectories = Directory.GetDirectories(currentPath);
                foreach (var subdirectory in subdirectories)
                {
                    ScanDirectory(subdirectory, currentDepth + 1, playlists);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogWarning($"[Scanner] 访问被拒绝: {currentPath} - {ex.Message}");
            }
            catch (Exception ex)
            {
                BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogError($"[Scanner] 扫描目录失败: {currentPath} - {ex}");
            }
        }

        /// <summary>
        /// 获取目录中的音频文件（不递归）
        /// </summary>
        private List<string> GetAudioFiles(string directoryPath)
        {
            var audioExtensions = new[] { ".mp3", ".wav", ".ogg", ".egg", ".flac", ".aiff", ".aif" };
            var files = new List<string>();

            try
            {
                files = Directory.GetFiles(directoryPath)
                    .Where(f => audioExtensions.Contains(Path.GetExtension(f).ToLower()))
                    .ToList();
            }
            catch (Exception ex)
            {
                BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogError($"[Scanner] 读取文件列表失败: {directoryPath} - {ex.Message}");
            }

            return files;
        }

        /// <summary>
        /// 获取绝对路径
        /// </summary>
        public static string GetAbsolutePath(string relativePath)
        {
            if (Path.IsPathRooted(relativePath))
                return relativePath;

            // 相对于游戏根目录（.dll所在目录）
            var gameRoot = Path.GetDirectoryName(Application.dataPath); // 通常是 Game_Data 的父目录
            if (string.IsNullOrEmpty(gameRoot))
                gameRoot = Directory.GetCurrentDirectory();

            return Path.GetFullPath(Path.Combine(gameRoot, relativePath));
        }
    }
}

