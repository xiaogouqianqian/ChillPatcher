using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bulbul;

namespace ChillPatcher.UIFramework.Data
{
    /// <summary>
    /// 自定义歌单数据管理器 - 管理自定义Tag的收藏和排序
    /// </summary>
    public class CustomPlaylistDataManager : IDisposable
    {
        private static CustomPlaylistDataManager _instance;
        public static CustomPlaylistDataManager Instance => _instance;

        private PlaylistDatabase _database;
        private readonly string _databasePath;

        /// <summary>
        /// 初始化数据管理器
        /// </summary>
        public static void Initialize(string rootDirectory)
        {
            if (_instance != null)
            {
                BepInEx.Logging.Logger.CreateLogSource("CustomPlaylistData").LogWarning("数据管理器已初始化");
                return;
            }

            var dbPath = Path.Combine(rootDirectory, ".playlist_data.db");
            _instance = new CustomPlaylistDataManager(dbPath);
        }

        private CustomPlaylistDataManager(string databasePath)
        {
            _databasePath = databasePath;
            _database = new PlaylistDatabase(databasePath);
            
            BepInEx.Logging.Logger.CreateLogSource("CustomPlaylistData").LogInfo($"数据管理器初始化成功: {databasePath}");
        }

        #region 收藏管理

        /// <summary>
        /// 添加收藏
        /// </summary>
        public bool AddFavorite(string tagId, string songUuid)
        {
            if (string.IsNullOrEmpty(tagId) || string.IsNullOrEmpty(songUuid))
                return false;

            var result = _database.AddFavorite(tagId, songUuid);
            
            if (result)
            {
                BepInEx.Logging.Logger.CreateLogSource("CustomPlaylistData")
                    .LogInfo($"添加收藏: Tag={tagId}, UUID={songUuid}");
            }
            
            return result;
        }

        /// <summary>
        /// 移除收藏
        /// </summary>
        public bool RemoveFavorite(string tagId, string songUuid)
        {
            if (string.IsNullOrEmpty(tagId) || string.IsNullOrEmpty(songUuid))
                return false;

            var result = _database.RemoveFavorite(tagId, songUuid);
            
            if (result)
            {
                BepInEx.Logging.Logger.CreateLogSource("CustomPlaylistData")
                    .LogInfo($"移除收藏: Tag={tagId}, UUID={songUuid}");
            }
            
            return result;
        }

        /// <summary>
        /// 检查是否收藏
        /// </summary>
        public bool IsFavorite(string tagId, string songUuid)
        {
            if (string.IsNullOrEmpty(tagId) || string.IsNullOrEmpty(songUuid))
                return false;

            return _database.IsFavorite(tagId, songUuid);
        }

        /// <summary>
        /// 获取指定Tag的所有收藏
        /// </summary>
        public List<string> GetFavorites(string tagId)
        {
            if (string.IsNullOrEmpty(tagId))
                return new List<string>();

            return _database.GetFavorites(tagId);
        }

        /// <summary>
        /// 切换收藏状态
        /// </summary>
        public bool ToggleFavorite(string tagId, string songUuid)
        {
            if (IsFavorite(tagId, songUuid))
            {
                return RemoveFavorite(tagId, songUuid);
            }
            else
            {
                return AddFavorite(tagId, songUuid);
            }
        }

        #endregion

        #region 播放顺序管理

        /// <summary>
        /// 设置完整播放顺序
        /// </summary>
        public bool SetPlaylistOrder(string tagId, List<string> songUuids)
        {
            if (string.IsNullOrEmpty(tagId) || songUuids == null)
                return false;

            var result = _database.SetPlaylistOrder(tagId, songUuids);
            
            if (result)
            {
                BepInEx.Logging.Logger.CreateLogSource("CustomPlaylistData")
                    .LogInfo($"设置播放顺序: Tag={tagId}, Count={songUuids.Count}");
            }
            
            return result;
        }

        /// <summary>
        /// 获取播放顺序
        /// </summary>
        public List<string> GetPlaylistOrder(string tagId)
        {
            if (string.IsNullOrEmpty(tagId))
                return new List<string>();

            return _database.GetPlaylistOrder(tagId);
        }

        /// <summary>
        /// 添加歌曲到播放顺序
        /// </summary>
        public bool AddToPlaylistOrder(string tagId, string songUuid)
        {
            if (string.IsNullOrEmpty(tagId) || string.IsNullOrEmpty(songUuid))
                return false;

            return _database.AppendToOrder(tagId, songUuid);
        }

        /// <summary>
        /// 从播放顺序中移除歌曲
        /// </summary>
        public bool RemoveFromPlaylistOrder(string tagId, string songUuid)
        {
            if (string.IsNullOrEmpty(tagId) || string.IsNullOrEmpty(songUuid))
                return false;

            return _database.RemoveFromOrder(tagId, songUuid);
        }

        #endregion

        #region 排除列表管理

        /// <summary>
        /// 添加到排除列表
        /// </summary>
        public bool AddExcluded(string tagId, string songUuid)
        {
            if (string.IsNullOrEmpty(tagId) || string.IsNullOrEmpty(songUuid))
                return false;

            var result = _database.AddExcluded(tagId, songUuid);
            
            if (result)
            {
                BepInEx.Logging.Logger.CreateLogSource("CustomPlaylistData")
                    .LogInfo($"添加到排除列表: Tag={tagId}, UUID={songUuid}");
            }
            
            return result;
        }

        /// <summary>
        /// 从排除列表移除
        /// </summary>
        public bool RemoveExcluded(string tagId, string songUuid)
        {
            if (string.IsNullOrEmpty(tagId) || string.IsNullOrEmpty(songUuid))
                return false;

            var result = _database.RemoveExcluded(tagId, songUuid);
            
            if (result)
            {
                BepInEx.Logging.Logger.CreateLogSource("CustomPlaylistData")
                    .LogInfo($"从排除列表移除: Tag={tagId}, UUID={songUuid}");
            }
            
            return result;
        }

        /// <summary>
        /// 检查是否在排除列表中
        /// </summary>
        public bool IsExcluded(string tagId, string songUuid)
        {
            if (string.IsNullOrEmpty(tagId) || string.IsNullOrEmpty(songUuid))
                return false;

            return _database.IsExcluded(tagId, songUuid);
        }

        /// <summary>
        /// 获取指定Tag的所有排除歌曲
        /// </summary>
        public List<string> GetExcludedSongs(string tagId)
        {
            if (string.IsNullOrEmpty(tagId))
                return new List<string>();

            return _database.GetExcludedSongs(tagId);
        }

        /// <summary>
        /// 切换排除状态
        /// </summary>
        public bool ToggleExcluded(string tagId, string songUuid)
        {
            if (IsExcluded(tagId, songUuid))
            {
                return RemoveExcluded(tagId, songUuid);
            }
            else
            {
                return AddExcluded(tagId, songUuid);
            }
        }

        #endregion

        #region Tag管理

        /// <summary>
        /// 清理指定Tag的所有数据（取消注册Tag时调用）
        /// </summary>
        public void ClearTag(string tagId)
        {
            if (string.IsNullOrEmpty(tagId))
                return;

            _database.ClearTag(tagId);
            
            BepInEx.Logging.Logger.CreateLogSource("CustomPlaylistData")
                .LogInfo($"清理Tag数据: {tagId}");
        }

        #endregion

        #region 工具方法

        /// <summary>
        /// 检查是否是自定义Tag（位5-15）
        /// </summary>
        public static bool IsCustomTag(AudioTag tag)
        {
            // 移除Local和Favorite标记
            var cleanTag = tag & ~AudioTag.Local & ~AudioTag.Favorite;
            
            // 检查是否在自定义Tag范围内（位5-15）
            int value = (int)cleanTag;
            
            // 如果值为0，不是自定义Tag
            if (value == 0)
                return false;
            
            // 检查最高位是否在5-15范围内
            for (int bit = 5; bit <= 15; bit++)
            {
                if ((value & (1 << bit)) != 0)
                    return true;
            }
            
            return false;
        }

        /// <summary>
        /// 从GameAudioInfo中提取Tag ID
        /// </summary>
        public static string GetTagIdFromAudio(GameAudioInfo audio)
        {
            if (audio == null)
                return null;

            // 从CustomTagManager中查找对应的Tag ID
            var customTags = Music.CustomTagManager.Instance.GetAllTags();
            
            foreach (var kvp in customTags)
            {
                if (audio.Tag.HasFlagFast(kvp.Value.BitValue))
                {
                    return kvp.Key;
                }
            }
            
            return null;
        }

        #endregion

        public void Dispose()
        {
            _database?.Dispose();
            _database = null;
        }
    }
}
