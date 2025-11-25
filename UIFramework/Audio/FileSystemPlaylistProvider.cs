using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Bulbul;
using ChillPatcher.UIFramework.Core;
using ChillPatcher.UIFramework.Music;
using Newtonsoft.Json;
using UnityEngine;

namespace ChillPatcher.UIFramework.Audio
{
    /// <summary>
    /// 文件系统歌单提供器 - 支持JSON缓存
    /// </summary>
    public class FileSystemPlaylistProvider : IPlaylistProvider, IDisposable
    {
        private readonly string _directoryPath;
        private readonly IAudioLoader _audioLoader;
        private PlaylistCacheData _cacheData;
        private List<GameAudioInfo> _runtimeSongs;
        private string _playlistJsonPath;
        private string _rescanFlagPath; // 标志文件路径

        public string Id { get; private set; }
        public string DisplayName { get; private set; }
        public Sprite Icon { get; private set; }
        public AudioTag Tag { get; set; }
        public bool SupportsLiveUpdate => true;
        public string CustomTagId { get; private set; } // 自定义Tag ID

        public event Action OnPlaylistUpdated;

        public FileSystemPlaylistProvider(string directoryPath, IAudioLoader audioLoader, AudioTag tag = AudioTag.Local)
        {
            if (string.IsNullOrEmpty(directoryPath))
                throw new ArgumentException("Directory path cannot be null or empty", nameof(directoryPath));

            if (!Directory.Exists(directoryPath))
                throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");

            _directoryPath = directoryPath;
            _audioLoader = audioLoader ?? throw new ArgumentNullException(nameof(audioLoader));

            // 默认ID和名称
            Id = Path.GetFileName(_directoryPath);
            DisplayName = Id;
            
            // ✅ 注册自定义Tag并获取位值
            CustomTagId = $"playlist_{Id}";
            var customTag = CustomTagManager.Instance.RegisterTag(CustomTagId, DisplayName);
            Tag = customTag.BitValue; // 使用分配的位值
            
            _playlistJsonPath = Path.Combine(_directoryPath, "playlist.json");
            _rescanFlagPath = Path.Combine(_directoryPath, "!rescan_playlist");
        }

        public async Task<List<GameAudioInfo>> BuildPlaylist()
        {
            try
            {
                // 检查是否需要强制重新扫描（标志文件不存在）
                bool forceRescan = ShouldForceRescan();
                
                if (forceRescan)
                {
                    BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogInfo($"[Playlist] 检测到需要重新扫描: {DisplayName}");
                }

                // 优先从缓存加载
                if (!forceRescan && PluginConfig.EnablePlaylistCache.Value && LoadCache())
                {
                    BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogInfo($"[Playlist] 从缓存加载歌单: {DisplayName}");
                    _runtimeSongs = await BuildSongsFromCache();
                }
                else
                {
                    // 增量扫描：合并现有缓存和新文件
                    if (!forceRescan && LoadCache())
                    {
                        BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogInfo($"[Playlist] 增量更新模式: {DisplayName}");
                        _runtimeSongs = await IncrementalScan();
                    }
                    else
                    {
                        // 全新扫描文件系统
                        BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogInfo($"[Playlist] 全新扫描目录: {_directoryPath}");
                        _runtimeSongs = await ScanDirectoryAndBuild();
                    }

                    // 自动生成JSON
                    if (PluginConfig.AutoGeneratePlaylistJson.Value)
                    {
                        GenerateCache();
                    }
                    
                    // 创建标志文件
                    CreateRescanFlag();
                }

                // ⚠️ Tag注册已移到Plugin.SetupFolderPlaylistsAsync统一批量处理
                
                BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogInfo($"[Playlist] 加载完成 '{DisplayName}': {_runtimeSongs.Count} 首歌曲");

                OnPlaylistUpdated?.Invoke();

                return _runtimeSongs;
            }
            catch (Exception ex)
            {
                BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogError($"[Playlist] 构建歌单失败 '{DisplayName}': {ex}");
                return new List<GameAudioInfo>();
            }
        }

        /// <summary>
        /// 强制刷新歌单（重新扫描）
        /// </summary>
        public async Task Refresh()
        {
            _cacheData = null;
            _runtimeSongs = null;
            await BuildPlaylist();
        }

        /// <summary>
        /// 加载缓存文件
        /// </summary>
        private bool LoadCache()
        {
            if (!File.Exists(_playlistJsonPath))
                return false;

            try
            {
                var json = File.ReadAllText(_playlistJsonPath);
                _cacheData = JsonConvert.DeserializeObject<PlaylistCacheData>(json);

                if (_cacheData == null)
                    return false;

                // 更新歌单信息
                DisplayName = _cacheData.PlaylistName ?? Path.GetFileName(_directoryPath);
                Id = _directoryPath; // 使用路径作为唯一ID

                BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogInfo($"[Playlist] 缓存加载成功: {DisplayName} (版本 {_cacheData.Version})");
                return true;
            }
            catch (Exception ex)
            {
                BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogWarning($"[Playlist] 缓存解析失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 从缓存构建歌曲列表
        /// </summary>
        private async Task<List<GameAudioInfo>> BuildSongsFromCache()
        {
            var songs = new List<GameAudioInfo>();
            bool needsUpdate = false; // 标记是否需要更新缓存

            foreach (var cachedSong in _cacheData.Songs)
            {
                if (!cachedSong.Enabled)
                    continue;

                var filePath = Path.Combine(_directoryPath, cachedSong.FileName);

                if (!File.Exists(filePath))
                {
                    BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogWarning($"[Playlist] 歌曲文件不存在: {cachedSong.FileName}");
                    continue;
                }

                try
                {
                    // ✅ 检查缓存中是否有UUID
                    string uuid = cachedSong.UUID;
                    if (string.IsNullOrEmpty(uuid))
                    {
                        // 生成新UUID并标记需要更新
                        uuid = Guid.NewGuid().ToString();
                        cachedSong.UUID = uuid;
                        needsUpdate = true;
                        BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogInfo($"[Playlist] 为歌曲 '{cachedSong.FileName}' 生成新UUID: {uuid}");
                    }

                    var audioInfo = await _audioLoader.LoadFromFile(filePath, uuid);

                    if (audioInfo != null)
                    {
                        // 使用缓存的元数据覆盖
                        if (!string.IsNullOrEmpty(cachedSong.Title))
                            audioInfo.Title = cachedSong.Title;
                        if (!string.IsNullOrEmpty(cachedSong.Artist))
                            audioInfo.Credit = cachedSong.Artist;  // Credit字段相当于Artist
                        // Album字段不存在，忽略

                        songs.Add(audioInfo);
                    }
                }
                catch (Exception ex)
                {
                    BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogError($"[Playlist] 加载歌曲失败 '{cachedSong.FileName}': {ex.Message}");
                }
            }

            // ✅ 如果有歌曲缺少UUID，更新缓存文件
            if (needsUpdate && PluginConfig.AutoGeneratePlaylistJson.Value)
            {
                try
                {
                    var json = JsonConvert.SerializeObject(_cacheData, Formatting.Indented);
                    File.WriteAllText(_playlistJsonPath, json);
                    BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogInfo($"[Playlist] 缓存已更新UUID: {_playlistJsonPath}");
                }
                catch (Exception ex)
                {
                    BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogError($"[Playlist] 更新缓存失败: {ex.Message}");
                }
            }

            return songs;
        }

        /// <summary>
        /// 扫描目录并构建歌单
        /// </summary>
        private async Task<List<GameAudioInfo>> ScanDirectoryAndBuild()
        {
            // 仅扫描当前目录，不递归
            var songs = await _audioLoader.LoadFromDirectory(_directoryPath, recursive: false);
            return songs;
        }

        /// <summary>
        /// 生成缓存JSON文件
        /// </summary>
        private void GenerateCache()
        {
            try
            {
                _cacheData = new PlaylistCacheData
                {
                    Version = 1,
                    PlaylistName = DisplayName,
                    Description = $"自动生成于 {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                    Tags = new List<string>(),
                    Songs = new List<CachedSongData>(),
                    GeneratedAt = DateTime.Now,
                    LastModified = DateTime.Now
                };

                // 添加歌曲到缓存
                foreach (var song in _runtimeSongs)
                {
                    var fileName = Path.GetFileName(song.LocalPath);
                    var fileInfo = new FileInfo(song.LocalPath);

                    _cacheData.Songs.Add(new CachedSongData
                    {
                        FileName = fileName,
                        Title = song.Title,
                        Artist = song.Credit,  // Credit字段相当于Artist
                        Album = "",             // GameAudioInfo没有Album字段
                        Duration = 0,
                        Enabled = true,
                        Tags = new List<string>(),
                        FileModifiedAt = fileInfo.LastWriteTime,
                        UUID = song.UUID  // ✅ 保存UUID
                    });
                }

                // 保存JSON
                var json = JsonConvert.SerializeObject(_cacheData, Formatting.Indented);
                File.WriteAllText(_playlistJsonPath, json);

                BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogInfo($"[Playlist] 缓存生成成功: {_playlistJsonPath}");
            }
            catch (Exception ex)
            {
                BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogError($"[Playlist] 缓存生成失败: {ex}");
            }
        }

        /// <summary>
        /// 检查目录是否有更新
        /// </summary>
        public bool HasDirectoryChanged()
        {
            if (_cacheData == null)
                return true;

            var directoryInfo = new DirectoryInfo(_directoryPath);
            return directoryInfo.LastWriteTime > _cacheData.LastModified;
        }

        /// <summary>
        /// 检查是否需要强制重新扫描（标志文件不存在）
        /// </summary>
        private bool ShouldForceRescan()
        {
            // 检查当前歌单目录的标志文件
            return !File.Exists(_rescanFlagPath);
        }

        /// <summary>
        /// 创建标志文件，表示已完成扫描
        /// </summary>
        private void CreateRescanFlag()
        {
            try
            {
                File.WriteAllText(_rescanFlagPath,
                    $"# ChillPatcher Playlist Scan Flag\n" +
                    $"# 此文件标识该歌单已完成扫描\n" +
                    $"# 删除此文件后，下次启动将强制重新扫描并添加新歌曲\n" +
                    $"# 已存在的歌曲UUID不会改变\n" +
                    $"#\n" +
                    $"# Last scanned: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");
            }
            catch (Exception ex)
            {
                BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogWarning(
                    $"[Playlist] 创建标志文件失败 '{_rescanFlagPath}': {ex.Message}");
            }
        }

        /// <summary>
        /// 增量扫描：合并现有缓存和新文件
        /// </summary>
        private async Task<List<GameAudioInfo>> IncrementalScan()
        {
            var songs = new List<GameAudioInfo>();
            var existingUUIDs = new Dictionary<string, CachedSongData>();
            
            // 构建现有歌曲的UUID映射（文件名 -> CachedSongData）
            foreach (var cachedSong in _cacheData.Songs)
            {
                existingUUIDs[cachedSong.FileName.ToLower()] = cachedSong;
            }

            // 扫描当前目录的所有音频文件
            var currentFiles = Directory.GetFiles(_directoryPath)
                .Where(f => _audioLoader.IsSupportedFormat(f))
                .ToList();

            BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogInfo(
                $"[Playlist] 发现 {currentFiles.Count} 个音频文件，缓存中有 {_cacheData.Songs.Count} 首歌曲");

            int newCount = 0;
            int existingCount = 0;

            foreach (var filePath in currentFiles)
            {
                var fileName = Path.GetFileName(filePath);
                var fileNameLower = fileName.ToLower();

                string uuid;
                CachedSongData cachedData = null;

                // 检查是否已存在于缓存中
                if (existingUUIDs.TryGetValue(fileNameLower, out cachedData))
                {
                    // 使用现有UUID
                    uuid = cachedData.UUID;
                    if (string.IsNullOrEmpty(uuid))
                    {
                        // 旧缓存没有UUID，分配新的
                        uuid = Guid.NewGuid().ToString();
                        cachedData.UUID = uuid;
                        BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogInfo(
                            $"[Playlist] 为现有歌曲分配UUID: {fileName} -> {uuid}");
                    }
                    existingCount++;
                }
                else
                {
                    // 新文件，分配新UUID
                    uuid = Guid.NewGuid().ToString();
                    newCount++;
                    BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogInfo(
                        $"[Playlist] 发现新歌曲: {fileName} -> {uuid}");
                }

                try
                {
                    var audioInfo = await _audioLoader.LoadFromFile(filePath, uuid);

                    if (audioInfo != null)
                    {
                        // 如果有缓存数据，使用缓存的元数据
                        if (cachedData != null)
                        {
                            if (!string.IsNullOrEmpty(cachedData.Title))
                                audioInfo.Title = cachedData.Title;
                            if (!string.IsNullOrEmpty(cachedData.Artist))
                                audioInfo.Credit = cachedData.Artist;
                        }

                        songs.Add(audioInfo);
                    }
                }
                catch (Exception ex)
                {
                    BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogError(
                        $"[Playlist] 加载歌曲失败 '{fileName}': {ex.Message}");
                }
            }

            BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogInfo(
                $"[Playlist] 增量扫描完成: 新增 {newCount} 首，保留 {existingCount} 首");

            return songs;
        }

        /// <summary>
        /// 释放资源并取消注册自定义Tag
        /// </summary>
        public void Dispose()
        {
            if (!string.IsNullOrEmpty(CustomTagId))
            {
                CustomTagManager.Instance.UnregisterTag(CustomTagId);
            }
        }
    }
}

