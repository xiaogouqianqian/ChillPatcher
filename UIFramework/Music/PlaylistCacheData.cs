using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ChillPatcher.UIFramework.Music
{
    /// <summary>
    /// 歌单缓存数据（playlist.json）
    /// </summary>
    [Serializable]
    public class PlaylistCacheData
    {
        /// <summary>
        /// 缓存格式版本
        /// </summary>
        [JsonProperty("version")]
        public int Version { get; set; } = 1;

        /// <summary>
        /// 歌单名称（默认为目录名）
        /// </summary>
        [JsonProperty("playlistName")]
        public string PlaylistName { get; set; }

        /// <summary>
        /// 歌单描述
        /// </summary>
        [JsonProperty("description")]
        public string Description { get; set; } = "";

        /// <summary>
        /// 歌单标签
        /// </summary>
        [JsonProperty("tags")]
        public List<string> Tags { get; set; } = new List<string>();

        /// <summary>
        /// 歌曲列表
        /// </summary>
        [JsonProperty("songs")]
        public List<CachedSongData> Songs { get; set; } = new List<CachedSongData>();

        /// <summary>
        /// 缓存生成时间戳
        /// </summary>
        [JsonProperty("generatedAt")]
        public DateTime GeneratedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// 最后修改时间戳
        /// </summary>
        [JsonProperty("lastModified")]
        public DateTime LastModified { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// 缓存的歌曲数据
    /// </summary>
    [Serializable]
    public class CachedSongData
    {
        /// <summary>
        /// 文件名（相对路径，相对于歌单目录）
        /// </summary>
        [JsonProperty("fileName")]
        public string FileName { get; set; }

        /// <summary>
        /// 歌曲标题（可自定义覆盖）
        /// </summary>
        [JsonProperty("title")]
        public string Title { get; set; }

        /// <summary>
        /// 艺术家（可自定义覆盖）
        /// </summary>
        [JsonProperty("artist")]
        public string Artist { get; set; }

        /// <summary>
        /// 专辑（可自定义覆盖）
        /// </summary>
        [JsonProperty("album")]
        public string Album { get; set; }

        /// <summary>
        /// 时长（秒，可选）
        /// </summary>
        [JsonProperty("duration")]
        public float Duration { get; set; } = 0;

        /// <summary>
        /// 是否启用（用户可禁用某些歌曲）
        /// </summary>
        [JsonProperty("enabled")]
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// 自定义标签
        /// </summary>
        [JsonProperty("tags")]
        public List<string> Tags { get; set; } = new List<string>();

        /// <summary>
        /// 文件最后修改时间（用于检测文件变化）
        /// </summary>
        [JsonProperty("fileModifiedAt")]
        public DateTime FileModifiedAt { get; set; }
    }
}
