using System;
using System.Collections.Generic;
using System.Linq;
using Bulbul;

namespace ChillPatcher.UIFramework.Music
{
    /// <summary>
    /// 自定义Tag - 使用真实位值代替BaseTag
    /// </summary>
    public class CustomTag
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        
        /// <summary>
        /// 真实的位值（如 CustomTag1 = 32, CustomTag2 = 64）
        /// </summary>
        public AudioTag BitValue { get; set; }

        public CustomTag(string id, string displayName, AudioTag bitValue)
        {
            Id = id;
            DisplayName = displayName;
            BitValue = bitValue;
        }
    }

    /// <summary>
    /// 自定义Tag管理器 - 基于位运算的多标签系统
    /// </summary>
    public class CustomTagManager
    {
        private static CustomTagManager _instance;
        public static CustomTagManager Instance => _instance ??= new CustomTagManager();

        private readonly Dictionary<string, CustomTag> _customTags = new Dictionary<string, CustomTag>();
        
        /// <summary>
        /// 歌曲UUID → 自定义Tags位掩码（支持多标签）
        /// </summary>
        private readonly Dictionary<string, AudioTag> _songTags = new Dictionary<string, AudioTag>();
        
        /// <summary>
        /// 下一个可用的位索引（5-15，共11位）
        /// </summary>
        private int _nextAvailableBit = 5;

        public event Action OnTagsChanged;

        /// <summary>
        /// 注册自定义Tag（自动分配位值）
        /// </summary>
        public CustomTag RegisterTag(string id, string displayName)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("Tag ID cannot be null or empty");

            // 检查是否已注册
            if (_customTags.ContainsKey(id))
            {
                BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogWarning($"[CustomTag] Tag already registered: {id}");
                return _customTags[id];
            }

            // 检查位数限制
            if (_nextAvailableBit > 15)
            {
                throw new InvalidOperationException("已达到自定义Tag上限 (11个)");
            }

            // 分配位值
            var bitValue = (AudioTag)(1 << _nextAvailableBit);
            _nextAvailableBit++;

            var tag = new CustomTag(id, displayName, bitValue);
            _customTags[id] = tag;
            
            OnTagsChanged?.Invoke();
            
            BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogInfo(
                $"[CustomTag] Registered: {displayName} (ID: {id}, Bit: {bitValue}, Value: {(int)bitValue})");
            
            return tag;
        }

        /// <summary>
        /// 取消注册自定义Tag
        /// </summary>
        public void UnregisterTag(string tagId)
        {
            if (!_customTags.TryGetValue(tagId, out var tag))
                return;

            // 从SongTags中移除该位
            var bitValue = tag.BitValue;
            var affectedSongs = 0;
            
            foreach (var songId in _songTags.Keys.ToList())
            {
                if (_songTags[songId].HasFlagFast(bitValue))
                {
                    _songTags[songId] = _songTags[songId].RemoveFlag(bitValue);
                    affectedSongs++;
                    
                    // 如果没有其他自定义Tag，删除条目
                    if (_songTags[songId] == 0)
                        _songTags.Remove(songId);
                }
            }

            _customTags.Remove(tagId);
            OnTagsChanged?.Invoke();
            
            BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogInfo(
                $"[CustomTag] Unregistered: {tagId} ({affectedSongs} songs affected)");
        }

        /// <summary>
        /// 获取所有自定义Tag
        /// </summary>
        public IReadOnlyDictionary<string, CustomTag> GetAllTags()
        {
            return _customTags;
        }

        /// <summary>
        /// 为歌曲添加自定义Tag（支持多标签）
        /// </summary>
        public void AddTagToSong(string songUUID, string customTagId)
        {
            if (!_customTags.TryGetValue(customTagId, out var tag))
            {
                BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogWarning($"[CustomTag] Tag not found: {customTagId}");
                return;
            }

            // 获取当前标签
            if (!_songTags.ContainsKey(songUUID))
                _songTags[songUUID] = (AudioTag)0;
            
            // 使用位运算添加
            _songTags[songUUID] |= tag.BitValue;
            
            // ✅ 同步更新MusicService中的GameAudioInfo.Tag字段
            UpdateGameAudioTag(songUUID);
        }
        
        /// <summary>
        /// 从歌曲移除自定义Tag
        /// </summary>
        public void RemoveTagFromSong(string songUUID, string customTagId)
        {
            if (!_customTags.TryGetValue(customTagId, out var tag))
                return;

            if (!_songTags.ContainsKey(songUUID))
                return;
            
            // 使用位运算移除
            _songTags[songUUID] = _songTags[songUUID].RemoveFlag(tag.BitValue);
            
            // 如果没有其他自定义Tag，删除条目
            if (_songTags[songUUID] == 0)
                _songTags.Remove(songUUID);
            
            // ✅ 同步更新MusicService中的GameAudioInfo.Tag字段
            UpdateGameAudioTag(songUUID);
        }
        
        /// <summary>
        /// 同步更新MusicService中已加载歌曲的Tag字段
        /// 这样游戏筛选逻辑直接读取audio.Tag就包含自定义Tag了！
        /// </summary>
        private void UpdateGameAudioTag(string songUUID)
        {
            try
            {
                // ✅ 从MusicService_Patches获取MusicService实例
                var musicService = ChillPatcher.Patches.UIFramework.MusicService_RemoveLimit_Patch.CurrentInstance;
                if (musicService == null)
                {
                    BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogWarning("[CustomTag] MusicService not ready, Tag update delayed");
                    return;
                }
                
                // 获取_allMusicList
                var allMusicList = HarmonyLib.Traverse.Create(musicService)
                    .Field("_allMusicList")
                    .GetValue<System.Collections.Generic.List<GameAudioInfo>>();
                
                if (allMusicList == null)
                {
                    BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogError("[CustomTag] Failed to get _allMusicList");
                    return;
                }
                
                // 查找对应的GameAudioInfo
                var audio = allMusicList.FirstOrDefault(a => a.UUID == songUUID);
                if (audio == null)
                {
                    BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogDebug($"[CustomTag] Audio not loaded yet: {songUUID}");
                    return;
                }
                
                // ✅ 重新计算并设置Tag字段
                // 需要先移除旧的自定义Tag位，保留游戏原生Tag位
                var gameOnlyTag = audio.Tag & (AudioTag.Original | AudioTag.Special | AudioTag.Other | AudioTag.Favorite | AudioTag.Local);
                var newCustomTags = GetSongCustomTags(songUUID);
                audio.Tag = gameOnlyTag | newCustomTags;
                
                BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogDebug(
                    $"[CustomTag] Updated Tag for {audio.Title}: {audio.Tag} (game: {gameOnlyTag}, custom: {newCustomTags})");
            }
            catch (Exception ex)
            {
                BepInEx.Logging.Logger.CreateLogSource("ChillUIFramework").LogError($"[CustomTag] UpdateGameAudioTag error: {ex}");
            }
        }

        /// <summary>
        /// 获取歌曲的所有自定义Tags（位掩码）
        /// </summary>
        public AudioTag GetSongCustomTags(string songUUID)
        {
            return _songTags.TryGetValue(songUUID, out var tags) ? tags : (AudioTag)0;
        }
        
        /// <summary>
        /// 获取歌曲的完整Tag（游戏Tag + 自定义Tag）
        /// </summary>
        public AudioTag GetFullTags(string songUUID, AudioTag gameTag)
        {
            var customTags = GetSongCustomTags(songUUID);
            return gameTag | customTags; // 位运算合并
        }

        /// <summary>
        /// 检查歌曲是否有指定的自定义Tag
        /// </summary>
        public bool HasCustomTag(string songUUID, string customTagId)
        {
            if (!_customTags.TryGetValue(customTagId, out var tag))
                return false;
            
            if (!_songTags.TryGetValue(songUUID, out var tags))
                return false;
            
            return tags.HasFlagFast(tag.BitValue);
        }

        /// <summary>
        /// 获取指定Tag的所有歌曲UUID（反向查询）
        /// </summary>
        public IEnumerable<string> GetSongsByTag(string customTagId)
        {
            if (!_customTags.TryGetValue(customTagId, out var tag))
                return Enumerable.Empty<string>();

            // 遍历所有歌曲，检查是否包含该位
            return _songTags
                .Where(kvp => kvp.Value.HasFlagFast(tag.BitValue))
                .Select(kvp => kvp.Key);
        }

        /// <summary>
        /// 获取Tag的歌曲数量
        /// </summary>
        public int GetSongCount(string customTagId)
        {
            return GetSongsByTag(customTagId).Count();
        }

        /// <summary>
        /// 清除所有
        /// </summary>
        public void Clear()
        {
            _customTags.Clear();
            _songTags.Clear();
            _nextAvailableBit = 5; // 重置位计数器
            OnTagsChanged?.Invoke();
        }
    }
}
