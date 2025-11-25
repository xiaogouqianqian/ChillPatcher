#!/usr/bin/env python3
"""
音频测试文件生成器
使用ffmpeg生成不同格式、采样率、深度的测试音频文件
每个文件发出不同频率的蜂鸣声，方便测试播放功能
支持为每个音频文件生成独特的黑白色块封面（类似二维码）
"""

import os
import subprocess
import json
import random
from pathlib import Path
from datetime import datetime
from PIL import Image, ImageDraw
import hashlib

# 配置
BASE_DIR = r"F:\SteamLibrary\steamapps\common\wallpaper_engine\projects\myprojects\chill_with_you\playlist"

# 音频格式配置
FORMATS = [
    {"ext": "mp3", "codec": "libmp3lame", "bitrate": "192k", "quality": None},
    {"ext": "wav", "codec": "pcm_s16le", "bitrate": None, "quality": None},
    {"ext": "ogg", "codec": "libvorbis", "bitrate": None, "quality": "4"},  # 使用质量模式(0-10)
    {"ext": "flac", "codec": "flac", "bitrate": None, "quality": None},
    {"ext": "aiff", "codec": "pcm_s16be", "bitrate": None, "quality": None},
]

# 采样率配置
SAMPLE_RATES = [22050, 44100, 48000]

# 随机元数据
RANDOM_TITLES = [
    "Midnight Dreams", "Summer Breeze", "Neon Lights", "Lost Horizon",
    "Echoes", "Crystalline", "Reflections", "Ascension", "Wanderlust",
    "Serenity", "Pulse", "Aurora", "Inception", "Odyssey", "Cascade",
    "Mirage", "Velocity", "Tranquility", "Nexus", "Elysium"
]

RANDOM_ARTISTS = [
    "The Soundwaves", "Luna Echo", "Chromatic Shift", "Digital Horizon",
    "Stellar Drift", "Vapor Trail", "Neon Collective", "Echo Chamber",
    "Synth Masters", "The Frequencies", "Audio Spectrum", "Wave Theory",
    "Sound Architects", "Frequency Lab", "Beat Engineers"
]

RANDOM_ALBUMS = [
    "Night Sessions", "Future Sounds", "Electric Dreams", "Soundscapes",
    "Urban Rhythms", "Digital Age", "Audio Experiments", "Frequency Test",
    "Studio Collection", "Sound Library", "Beat Archive", "Audio Vault"
]

# 测试歌单结构
PLAYLISTS = {
    "Rock": {
        "depth": 1,
        "count": 200,
        "freq_range": (200, 400),  # Hz
        "subfolders": {
            "80s": {"count": 100, "freq_range": (400, 600)},
            "Metal": {"count": 150, "freq_range": (100, 200)},
        },
    },
    "Jazz": {"depth": 1, "count": 150, "freq_range": (500, 700)},
    "OST": {"depth": 1, "count": 300, "freq_range": (700, 900)},
    "Classical": {
        "depth": 1,
        "count": 100,
        "freq_range": (900, 1100),
        "subfolders": {
            "Baroque": {"count": 50, "freq_range": (1100, 1300)},
            "Romantic": {"count": 80, "freq_range": (1300, 1500)},
        },
    },
    "Electronic": {"depth": 1, "count": 250, "freq_range": (1500, 1700)},
    "Test_Large": {
        "depth": 1,
        "count": 500,
        "freq_range": (1700, 1900),
        "subfolders": {
            "Sub1": {"count": 200, "freq_range": (1900, 2100)},
            "Sub2": {"count": 200, "freq_range": (2100, 2300)},
            "Sub3": {
                "count": 100,
                "freq_range": (2300, 2500),
                "subfolders": {
                    "Deep": {"count": 50, "freq_range": (2500, 2700)},
                },
            },
        },
    },
}


def check_ffmpeg():
    """检查ffmpeg是否安装"""
    try:
        subprocess.run(
            ["ffmpeg", "-version"],
            capture_output=True,
            check=True,
        )
        return True
    except (subprocess.CalledProcessError, FileNotFoundError):
        print("❌ 错误：未找到ffmpeg，请先安装ffmpeg并添加到PATH")
        print("下载地址：https://ffmpeg.org/download.html")
        return False


def check_pillow():
    """检查PIL/Pillow是否安装"""
    try:
        from PIL import Image
        return True
    except ImportError:
        print("❌ 错误：未找到Pillow库，请先安装：pip install Pillow")
        return False


def generate_qr_like_cover(seed_string: str, size: int = 300, block_size: int = 30) -> Image.Image:
    """
    生成类似二维码的黑白色块封面
    
    Args:
        seed_string: 用于生成图案的种子字符串（例如文件路径或频率）
        size: 图片尺寸（正方形）
        block_size: 每个色块的大小（像素）
    
    Returns:
        PIL Image 对象
    """
    # 使用MD5哈希生成可重复的随机数种子
    hash_obj = hashlib.md5(seed_string.encode())
    seed = int.from_bytes(hash_obj.digest()[:4], 'big')
    random.seed(seed)
    
    # 计算网格大小
    grid_size = size // block_size
    
    # 创建白色背景图像
    img = Image.new('RGB', (size, size), 'white')
    draw = ImageDraw.Draw(img)
    
    # 生成黑白色块图案
    for y in range(grid_size):
        for x in range(grid_size):
            # 随机决定是否填充黑色
            if random.random() > 0.5:
                x1 = x * block_size
                y1 = y * block_size
                x2 = x1 + block_size
                y2 = y1 + block_size
                draw.rectangle([x1, y1, x2, y2], fill='black')
    
    # 在中心添加一个小的识别标记（让它更像二维码）
    center = size // 2
    marker_size = block_size * 3
    draw.rectangle(
        [center - marker_size//2, center - marker_size//2,
         center + marker_size//2, center + marker_size//2],
        fill='white', outline='black', width=block_size//3
    )
    
    return img


def add_album_art_to_audio(audio_path: str, cover_image: Image.Image) -> bool:
    """
    将封面图片嵌入到音频文件中
    
    Args:
        audio_path: 音频文件路径
        cover_image: PIL Image 对象
    
    Returns:
        成功返回 True，失败返回 False
    """
    try:
        # 保存临时图片
        temp_cover_path = audio_path + ".temp_cover.jpg"
        cover_image.save(temp_cover_path, "JPEG", quality=90)
        
        # 创建临时音频文件路径（保留扩展名）
        ext = os.path.splitext(audio_path)[1]
        temp_audio_path = audio_path.replace(ext, f".temp{ext}")
        
        # 使用ffmpeg添加封面
        # 不同格式需要不同的参数
        ext_lower = ext.lower()
        
        if ext_lower == '.mp3':
            # MP3 使用 stream 0:v 作为封面
            cmd = [
                "ffmpeg", "-i", audio_path, "-i", temp_cover_path,
                "-map", "0:a",  # 音频流
                "-map", "1:0",  # 封面图片流
                "-c:a", "copy",  # 不重新编码音频
                "-c:v", "mjpeg",  # 使用 MJPEG 编码
                "-disposition:v", "attached_pic",  # 标记为封面
                "-y", temp_audio_path
            ]
        elif ext_lower == '.flac':
            cmd = [
                "ffmpeg", "-i", audio_path, "-i", temp_cover_path,
                "-map", "0:a", "-map", "1:0",
                "-c:a", "copy",
                "-c:v", "mjpeg",  # 使用 MJPEG 编码
                "-disposition:v", "attached_pic",
                "-y", temp_audio_path
            ]
        elif ext_lower == '.ogg':
            # OGG 需要重新编码才能添加封面
            cmd = [
                "ffmpeg", "-i", audio_path, "-i", temp_cover_path,
                "-map", "0:a", "-map", "1:0",
                "-c:a", "libvorbis", "-q:a", "4",  # 重新编码
                "-c:v", "copy",
                "-disposition:v", "attached_pic",
                "-metadata:s:v", "title=Album cover",
                "-metadata:s:v", "comment=Cover (front)",
                "-y", temp_audio_path
            ]
        else:
            # WAV 和 AIFF 不支持嵌入封面，跳过
            os.remove(temp_cover_path)
            return False
        
        # 执行命令
        result = subprocess.run(
            cmd,
            capture_output=True,
            check=True,
            creationflags=subprocess.CREATE_NO_WINDOW
        )
        
        # 替换原文件
        os.remove(audio_path)
        os.rename(temp_audio_path, audio_path)
        os.remove(temp_cover_path)
        
        return True
        
    except subprocess.CalledProcessError as e:
        # 显示 ffmpeg 的详细错误信息
        stderr = e.stderr.decode('utf-8', errors='ignore') if e.stderr else "No error output"
        print(f"  ⚠️ 添加封面失败: {os.path.basename(audio_path)}")
        print(f"     FFmpeg 错误: {stderr[-500:]}")  # 只显示最后 500 个字符
        
        # 清理临时文件
        if os.path.exists(temp_cover_path):
            os.remove(temp_cover_path)
        if os.path.exists(temp_audio_path):
            os.remove(temp_audio_path)
        return False
    except Exception as e:
        # 其他错误
        print(f"  ⚠️ 添加封面失败: {os.path.basename(audio_path)} - {str(e)}")
        
        # 清理临时文件
        if os.path.exists(temp_cover_path):
            os.remove(temp_cover_path)
        if os.path.exists(temp_audio_path):
            os.remove(temp_audio_path)
        return False


def generate_audio(
    output_path: str,
    frequency: int,
    duration: float = 5.0,
    sample_rate: int = 44100,
    format_config: dict = None,
    with_cover: bool = True,
):
    """
    生成蜂鸣声音频文件

    Args:
        output_path: 输出文件路径
        frequency: 蜂鸣频率 (Hz)
        duration: 持续时间 (秒)
        sample_rate: 采样率
        format_config: 格式配置 {"ext": "mp3", "codec": "...", "bitrate": "..."}
        with_cover: 是否添加封面
    """
    if format_config is None:
        format_config = FORMATS[0]

    # 构建ffmpeg命令
    cmd = [
        "ffmpeg",
        "-f",
        "lavfi",
        "-i",
        f"sine=frequency={frequency}:duration={duration}:sample_rate={sample_rate}",
        "-c:a",
        format_config["codec"],
    ]

    # 添加比特率或质量参数
    if format_config["bitrate"]:
        cmd.extend(["-b:a", format_config["bitrate"]])
    elif format_config["quality"]:
        cmd.extend(["-q:a", format_config["quality"]])

    # 添加随机元数据
    title = random.choice(RANDOM_TITLES)
    artist = random.choice(RANDOM_ARTISTS)
    album = random.choice(RANDOM_ALBUMS)
    
    cmd.extend(
        [
            "-metadata",
            f"title={title}",
            "-metadata",
            f"artist={artist}",
            "-metadata",
            f"album={album}",
            "-y",  # 覆盖已存在文件
            output_path,
        ]
    )

    # 执行命令（静默输出）
    try:
        subprocess.run(
            cmd, capture_output=True, check=True, creationflags=subprocess.CREATE_NO_WINDOW
        )
        
        # 添加封面（如果格式支持）
        if with_cover:
            ext = format_config["ext"]
            # 只为 MP3 和 FLAC 添加封面（OGG 支持不稳定）
            if ext in ["mp3", "flac"]:
                # 生成独特的封面（使用文件路径+频率作为种子）
                seed = f"{output_path}_{frequency}"
                cover = generate_qr_like_cover(seed)
                if add_album_art_to_audio(output_path, cover):
                    pass  # 封面添加成功
        
        return True
    except subprocess.CalledProcessError as e:
        print(f"  ❌ 生成失败: {output_path}")
        print(f"     错误: {e.stderr.decode('utf-8', errors='ignore')}")
        return False


def create_playlist_json(folder_path: str, audio_files: list):
    """
    创建playlist.json缓存文件

    Args:
        folder_path: 歌单文件夹路径
        audio_files: 音频文件列表 [{"path": "...", "freq": ..., ...}]
    """
    playlist_data = {
        "Version": 1,
        "PlaylistName": Path(folder_path).name,
        "LastModified": datetime.now().isoformat(),
        "Songs": [],
    }

    for i, audio_file in enumerate(audio_files):
        file_path = audio_file["path"]
        freq = audio_file["freq"]
        file_stat = os.stat(file_path)

        # 生成UUID（简化版，基于路径哈希）
        uuid = hashlib.md5(file_path.encode()).hexdigest()

        playlist_data["Songs"].append(
            {
                "UUID": f"{uuid[:8]}-{uuid[8:12]}-{uuid[12:16]}-{uuid[16:20]}-{uuid[20:32]}",
                "Title": f"Test Audio {freq}Hz",
                "Artist": "Test Generator",
                "Credit": "ffmpeg",
                "FilePath": file_path,
                "Duration": 5.0,
                "Enabled": True,
                "Tags": [],
                "FileModifiedAt": datetime.fromtimestamp(file_stat.st_mtime).isoformat(),
            }
        )

    # 保存JSON
    json_path = os.path.join(folder_path, "playlist.json")
    with open(json_path, "w", encoding="utf-8") as f:
        json.dump(playlist_data, f, indent=2, ensure_ascii=False)

    print(f"  ✅ 生成缓存: {json_path}")


def generate_playlist(folder_path: str, config: dict, depth: int = 0, max_depth: int = 3):
    """
    递归生成歌单文件夹

    Args:
        folder_path: 歌单文件夹路径
        config: 歌单配置
        depth: 当前深度
        max_depth: 最大深度
    """
    # 创建文件夹
    os.makedirs(folder_path, exist_ok=True)

    count = config.get("count", 0)
    freq_range = config.get("freq_range", (440, 880))
    audio_files = []

    if count > 0:
        print(f"\n{'  ' * depth}📁 {Path(folder_path).name} ({count}首歌)")

        # 生成音频文件
        for i in range(count):
            # 随机选择格式和采样率
            format_config = random.choice(FORMATS)
            sample_rate = random.choice(SAMPLE_RATES)

            # 随机频率
            frequency = random.randint(freq_range[0], freq_range[1])

            # 文件名
            filename = f"track_{i+1:04d}_{frequency}Hz_{sample_rate}Hz.{format_config['ext']}"
            output_path = os.path.join(folder_path, filename)

            # 生成音频
            if i % 10 == 0:
                print(
                    f"  {'  ' * depth}生成中... {i}/{count} ({format_config['ext']}, {sample_rate}Hz, {frequency}Hz)"
                )

            if generate_audio(output_path, frequency, 5.0, sample_rate, format_config):
                audio_files.append({"path": output_path, "freq": frequency})

        print(f"  {'  ' * depth}✅ 完成: {len(audio_files)}/{count} 首歌")

        # 跳过playlist.json生成（用户不需要）
        # create_playlist_json(folder_path, audio_files)

    # 递归处理子文件夹
    subfolders = config.get("subfolders", {})
    if subfolders and depth < max_depth:
        for subfolder_name, subfolder_config in subfolders.items():
            subfolder_path = os.path.join(folder_path, subfolder_name)
            generate_playlist(subfolder_path, subfolder_config, depth + 1, max_depth)


def main():
    print("=" * 60)
    print("🎵 ChillPatcher 音频测试文件生成器")
    print("=" * 60)

    # 检查ffmpeg
    if not check_ffmpeg():
        return
    
    # 检查Pillow
    if not check_pillow():
        return

    print(f"\n目标目录: {BASE_DIR}")
    print(f"支持格式: {', '.join([f['ext'] for f in FORMATS])}")
    print(f"采样率: {', '.join(map(str, SAMPLE_RATES))} Hz")
    print(f"封面: 黑白色块图案（类似二维码）")

    # 统计总数
    total_songs = 0

    def count_songs(config):
        nonlocal total_songs
        total_songs += config.get("count", 0)
        for subfolder_config in config.get("subfolders", {}).values():
            count_songs(subfolder_config)

    for playlist_config in PLAYLISTS.values():
        count_songs(playlist_config)

    print(f"\n预计生成: {total_songs} 首测试音频")
    print(f"预计用时: ~{total_songs * 0.5:.0f} 秒 (取决于机器性能)")
    print(f"预计空间: ~{total_songs * 0.1:.0f} MB")

    # 确认
    confirm = input("\n开始生成？(y/N): ")
    if confirm.lower() != "y":
        print("❌ 已取消")
        return

    # 生成歌单
    print("\n开始生成测试文件...")
    import time

    start_time = time.time()

    for playlist_name, playlist_config in PLAYLISTS.items():
        playlist_path = os.path.join(BASE_DIR, playlist_name)
        generate_playlist(playlist_path, playlist_config)

    elapsed_time = time.time() - start_time

    print("\n" + "=" * 60)
    print(f"✅ 生成完成！")
    print(f"总计: {total_songs} 首测试音频")
    print(f"用时: {elapsed_time:.1f} 秒")
    print(f"平均: {elapsed_time/total_songs:.2f} 秒/首")
    print("=" * 60)

    # 生成测试报告
    report_path = os.path.join(BASE_DIR, "test_report.txt")
    with open(report_path, "w", encoding="utf-8") as f:
        f.write(f"ChillPatcher 测试音频生成报告\n")
        f.write(f"生成时间: {datetime.now()}\n")
        f.write(f"总计: {total_songs} 首\n")
        f.write(f"用时: {elapsed_time:.1f} 秒\n\n")
        f.write(f"目录结构:\n")
        for playlist_name, playlist_config in PLAYLISTS.items():
            f.write(f"  {playlist_name}: {playlist_config.get('count', 0)} 首\n")
            for subfolder_name, subfolder_config in playlist_config.get("subfolders", {}).items():
                f.write(f"    └─ {subfolder_name}: {subfolder_config.get('count', 0)} 首\n")

    print(f"\n测试报告: {report_path}")


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print("\n\n❌ 用户中断")
    except Exception as e:
        print(f"\n\n❌ 错误: {e}")
        import traceback

        traceback.print_exc()
