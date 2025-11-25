using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;

namespace ChillPatcher.UIFramework.Data
{
    /// <summary>
    /// SQLite数据库管理类 - 存储自定义歌单的收藏和排序
    /// </summary>
    public class PlaylistDatabase : IDisposable
    {
        private readonly string _dbPath;
        private SQLiteConnection _connection;
        private readonly object _lock = new object();

        public PlaylistDatabase(string databasePath)
        {
            if (string.IsNullOrEmpty(databasePath))
                throw new ArgumentException("Database path cannot be null or empty");

            _dbPath = databasePath;
            Initialize();
        }

        /// <summary>
        /// 初始化数据库（创建表和索引）
        /// </summary>
        private void Initialize()
        {
            try
            {
                // 确保目录存在
                var directory = Path.GetDirectoryName(_dbPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 创建或打开数据库
                _connection = new SQLiteConnection($"Data Source={_dbPath};Version=3;");
                _connection.Open();

                // 创建表
                CreateTables();
                CreateIndexes();

                BepInEx.Logging.Logger.CreateLogSource("PlaylistDB").LogInfo($"数据库初始化成功: {_dbPath}");
            }
            catch (Exception ex)
            {
                BepInEx.Logging.Logger.CreateLogSource("PlaylistDB").LogError($"数据库初始化失败: {ex}");
                throw;
            }
        }

        /// <summary>
        /// 创建数据表
        /// </summary>
        private void CreateTables()
        {
            lock (_lock)
            {
                using (var cmd = _connection.CreateCommand())
                {
                    // 收藏表
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS CustomFavorites (
                            id INTEGER PRIMARY KEY AUTOINCREMENT,
                            tag_id TEXT NOT NULL,
                            song_uuid TEXT NOT NULL,
                            created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                            UNIQUE(tag_id, song_uuid)
                        )";
                    cmd.ExecuteNonQuery();

                    // 播放顺序表
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS CustomPlaylistOrder (
                            id INTEGER PRIMARY KEY AUTOINCREMENT,
                            tag_id TEXT NOT NULL,
                            song_uuid TEXT NOT NULL,
                            order_index INTEGER NOT NULL,
                            updated_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                            UNIQUE(tag_id, song_uuid)
                        )";
                    cmd.ExecuteNonQuery();

                    // 排除列表表
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS CustomExcludedSongs (
                            id INTEGER PRIMARY KEY AUTOINCREMENT,
                            tag_id TEXT NOT NULL,
                            song_uuid TEXT NOT NULL,
                            created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                            UNIQUE(tag_id, song_uuid)
                        )";
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// 创建索引
        /// </summary>
        private void CreateIndexes()
        {
            lock (_lock)
            {
                using (var cmd = _connection.CreateCommand())
                {
                    cmd.CommandText = "CREATE INDEX IF NOT EXISTS idx_favorites_tag ON CustomFavorites(tag_id)";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = "CREATE INDEX IF NOT EXISTS idx_order_tag ON CustomPlaylistOrder(tag_id, order_index)";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = "CREATE INDEX IF NOT EXISTS idx_excluded_tag ON CustomExcludedSongs(tag_id)";
                    cmd.ExecuteNonQuery();
                }
            }
        }

        #region 收藏操作

        /// <summary>
        /// 添加收藏
        /// </summary>
        public bool AddFavorite(string tagId, string songUuid)
        {
            lock (_lock)
            {
                try
                {
                    using (var cmd = _connection.CreateCommand())
                    {
                        cmd.CommandText = @"
                            INSERT OR IGNORE INTO CustomFavorites (tag_id, song_uuid)
                            VALUES (@tagId, @songUuid)";
                        cmd.Parameters.AddWithValue("@tagId", tagId);
                        cmd.Parameters.AddWithValue("@songUuid", songUuid);
                        
                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
                catch (Exception ex)
                {
                    BepInEx.Logging.Logger.CreateLogSource("PlaylistDB").LogError($"添加收藏失败: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// 移除收藏
        /// </summary>
        public bool RemoveFavorite(string tagId, string songUuid)
        {
            lock (_lock)
            {
                try
                {
                    using (var cmd = _connection.CreateCommand())
                    {
                        cmd.CommandText = @"
                            DELETE FROM CustomFavorites
                            WHERE tag_id = @tagId AND song_uuid = @songUuid";
                        cmd.Parameters.AddWithValue("@tagId", tagId);
                        cmd.Parameters.AddWithValue("@songUuid", songUuid);
                        
                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
                catch (Exception ex)
                {
                    BepInEx.Logging.Logger.CreateLogSource("PlaylistDB").LogError($"移除收藏失败: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// 检查是否收藏
        /// </summary>
        public bool IsFavorite(string tagId, string songUuid)
        {
            lock (_lock)
            {
                try
                {
                    using (var cmd = _connection.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT COUNT(*) FROM CustomFavorites
                            WHERE tag_id = @tagId AND song_uuid = @songUuid";
                        cmd.Parameters.AddWithValue("@tagId", tagId);
                        cmd.Parameters.AddWithValue("@songUuid", songUuid);
                        
                        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                    }
                }
                catch (Exception ex)
                {
                    BepInEx.Logging.Logger.CreateLogSource("PlaylistDB").LogError($"检查收藏失败: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// 获取指定Tag的所有收藏UUID
        /// </summary>
        public List<string> GetFavorites(string tagId)
        {
            lock (_lock)
            {
                var favorites = new List<string>();
                
                try
                {
                    using (var cmd = _connection.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT song_uuid FROM CustomFavorites
                            WHERE tag_id = @tagId
                            ORDER BY created_at DESC";
                        cmd.Parameters.AddWithValue("@tagId", tagId);
                        
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                favorites.Add(reader.GetString(0));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    BepInEx.Logging.Logger.CreateLogSource("PlaylistDB").LogError($"获取收藏列表失败: {ex.Message}");
                }
                
                return favorites;
            }
        }

        #endregion

        #region 播放顺序操作

        /// <summary>
        /// 设置播放顺序（完整替换）
        /// </summary>
        public bool SetPlaylistOrder(string tagId, List<string> songUuids)
        {
            lock (_lock)
            {
                try
                {
                    using (var transaction = _connection.BeginTransaction())
                    {
                        try
                        {
                            // 删除旧顺序
                            using (var cmd = _connection.CreateCommand())
                            {
                                cmd.CommandText = "DELETE FROM CustomPlaylistOrder WHERE tag_id = @tagId";
                                cmd.Parameters.AddWithValue("@tagId", tagId);
                                cmd.ExecuteNonQuery();
                            }

                            // 插入新顺序
                            for (int i = 0; i < songUuids.Count; i++)
                            {
                                using (var cmd = _connection.CreateCommand())
                                {
                                    cmd.CommandText = @"
                                        INSERT INTO CustomPlaylistOrder (tag_id, song_uuid, order_index)
                                        VALUES (@tagId, @songUuid, @orderIndex)";
                                    cmd.Parameters.AddWithValue("@tagId", tagId);
                                    cmd.Parameters.AddWithValue("@songUuid", songUuids[i]);
                                    cmd.Parameters.AddWithValue("@orderIndex", i);
                                    cmd.ExecuteNonQuery();
                                }
                            }

                            transaction.Commit();
                            return true;
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
                catch (Exception ex)
                {
                    BepInEx.Logging.Logger.CreateLogSource("PlaylistDB").LogError($"设置播放顺序失败: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// 获取播放顺序
        /// </summary>
        public List<string> GetPlaylistOrder(string tagId)
        {
            lock (_lock)
            {
                var order = new List<string>();
                
                try
                {
                    using (var cmd = _connection.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT song_uuid FROM CustomPlaylistOrder
                            WHERE tag_id = @tagId
                            ORDER BY order_index ASC";
                        cmd.Parameters.AddWithValue("@tagId", tagId);
                        
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                order.Add(reader.GetString(0));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    BepInEx.Logging.Logger.CreateLogSource("PlaylistDB").LogError($"获取播放顺序失败: {ex.Message}");
                }
                
                return order;
            }
        }

        /// <summary>
        /// 添加歌曲到顺序末尾
        /// </summary>
        public bool AppendToOrder(string tagId, string songUuid)
        {
            lock (_lock)
            {
                try
                {
                    // 获取当前最大索引
                    int maxIndex = -1;
                    using (var cmd = _connection.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT COALESCE(MAX(order_index), -1) FROM CustomPlaylistOrder
                            WHERE tag_id = @tagId";
                        cmd.Parameters.AddWithValue("@tagId", tagId);
                        maxIndex = Convert.ToInt32(cmd.ExecuteScalar());
                    }

                    // 插入新记录
                    using (var cmd = _connection.CreateCommand())
                    {
                        cmd.CommandText = @"
                            INSERT OR REPLACE INTO CustomPlaylistOrder (tag_id, song_uuid, order_index)
                            VALUES (@tagId, @songUuid, @orderIndex)";
                        cmd.Parameters.AddWithValue("@tagId", tagId);
                        cmd.Parameters.AddWithValue("@songUuid", songUuid);
                        cmd.Parameters.AddWithValue("@orderIndex", maxIndex + 1);
                        
                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
                catch (Exception ex)
                {
                    BepInEx.Logging.Logger.CreateLogSource("PlaylistDB").LogError($"添加到播放顺序失败: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// 从播放顺序中移除歌曲
        /// </summary>
        public bool RemoveFromOrder(string tagId, string songUuid)
        {
            lock (_lock)
            {
                try
                {
                    using (var cmd = _connection.CreateCommand())
                    {
                        cmd.CommandText = @"
                            DELETE FROM CustomPlaylistOrder
                            WHERE tag_id = @tagId AND song_uuid = @songUuid";
                        cmd.Parameters.AddWithValue("@tagId", tagId);
                        cmd.Parameters.AddWithValue("@songUuid", songUuid);
                        
                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
                catch (Exception ex)
                {
                    BepInEx.Logging.Logger.CreateLogSource("PlaylistDB").LogError($"从播放顺序移除失败: {ex.Message}");
                    return false;
                }
            }
        }

        #endregion

        #region 排除列表操作

        /// <summary>
        /// 添加到排除列表
        /// </summary>
        public bool AddExcluded(string tagId, string songUuid)
        {
            lock (_lock)
            {
                try
                {
                    using (var cmd = _connection.CreateCommand())
                    {
                        cmd.CommandText = @"
                            INSERT OR IGNORE INTO CustomExcludedSongs (tag_id, song_uuid)
                            VALUES (@tagId, @songUuid)";
                        cmd.Parameters.AddWithValue("@tagId", tagId);
                        cmd.Parameters.AddWithValue("@songUuid", songUuid);
                        
                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
                catch (Exception ex)
                {
                    BepInEx.Logging.Logger.CreateLogSource("PlaylistDB").LogError($"添加排除失败: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// 从排除列表移除
        /// </summary>
        public bool RemoveExcluded(string tagId, string songUuid)
        {
            lock (_lock)
            {
                try
                {
                    using (var cmd = _connection.CreateCommand())
                    {
                        cmd.CommandText = @"
                            DELETE FROM CustomExcludedSongs
                            WHERE tag_id = @tagId AND song_uuid = @songUuid";
                        cmd.Parameters.AddWithValue("@tagId", tagId);
                        cmd.Parameters.AddWithValue("@songUuid", songUuid);
                        
                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
                catch (Exception ex)
                {
                    BepInEx.Logging.Logger.CreateLogSource("PlaylistDB").LogError($"移除排除失败: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// 检查是否在排除列表中
        /// </summary>
        public bool IsExcluded(string tagId, string songUuid)
        {
            lock (_lock)
            {
                try
                {
                    using (var cmd = _connection.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT COUNT(*) FROM CustomExcludedSongs
                            WHERE tag_id = @tagId AND song_uuid = @songUuid";
                        cmd.Parameters.AddWithValue("@tagId", tagId);
                        cmd.Parameters.AddWithValue("@songUuid", songUuid);
                        
                        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                    }
                }
                catch (Exception ex)
                {
                    BepInEx.Logging.Logger.CreateLogSource("PlaylistDB").LogError($"检查排除失败: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// 获取指定Tag的所有排除UUID
        /// </summary>
        public List<string> GetExcludedSongs(string tagId)
        {
            lock (_lock)
            {
                var result = new List<string>();

                try
                {
                    using (var cmd = _connection.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT song_uuid FROM CustomExcludedSongs
                            WHERE tag_id = @tagId
                            ORDER BY created_at";
                        cmd.Parameters.AddWithValue("@tagId", tagId);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                result.Add(reader.GetString(0));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    BepInEx.Logging.Logger.CreateLogSource("PlaylistDB").LogError($"获取排除列表失败: {ex.Message}");
                }

                return result;
            }
        }

        #endregion

        /// <summary>
        /// 清理指定Tag的所有数据
        /// </summary>
        public void ClearTag(string tagId)
        {
            lock (_lock)
            {
                try
                {
                    using (var transaction = _connection.BeginTransaction())
                    {
                        try
                        {
                            using (var cmd = _connection.CreateCommand())
                            {
                                cmd.CommandText = "DELETE FROM CustomFavorites WHERE tag_id = @tagId";
                                cmd.Parameters.AddWithValue("@tagId", tagId);
                                cmd.ExecuteNonQuery();
                            }

                            using (var cmd = _connection.CreateCommand())
                            {
                                cmd.CommandText = "DELETE FROM CustomPlaylistOrder WHERE tag_id = @tagId";
                                cmd.Parameters.AddWithValue("@tagId", tagId);
                                cmd.ExecuteNonQuery();
                            }

                            using (var cmd = _connection.CreateCommand())
                            {
                                cmd.CommandText = "DELETE FROM CustomExcludedSongs WHERE tag_id = @tagId";
                                cmd.Parameters.AddWithValue("@tagId", tagId);
                                cmd.ExecuteNonQuery();
                            }

                            transaction.Commit();
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
                catch (Exception ex)
                {
                    BepInEx.Logging.Logger.CreateLogSource("PlaylistDB").LogError($"清理Tag数据失败: {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                _connection?.Close();
                _connection?.Dispose();
                _connection = null;
            }
        }
    }
}
