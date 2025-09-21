using Microsoft.EntityFrameworkCore;
using MoodPlaylist.SQLite.Data;
using MoodPlaylist.SQLite.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MoodPlaylistGenerator.Services
{
    public class SongService
    {
        private readonly ApplicationDbContext _context;

        public SongService(ApplicationDbContext context)
        {
            _context = context;
        }

        // Get all songs for a user
        public async Task<List<Song>> GetUserSongsAsync(int userId)
        {
            return await _context.Songs
                .Include(s => s.SongMoods)
                .ThenInclude(sm => sm.Mood)
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();
        }

        // Get single song by ID
        public async Task<Song?> GetSongByIdAsync(int songId, int userId)
        {
            return await _context.Songs
                .Include(s => s.SongMoods)
                .ThenInclude(sm => sm.Mood)
                .FirstOrDefaultAsync(s => s.Id == songId && s.UserId == userId);
        }

        // Create new song with optional file upload or YouTube URL
        public async Task<Song> CreateSongAsync(
            string title,
            string artist,
            string? youtubeUrl,
            string? localFilePath,
            int userId,
            List<int> moodIds)
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(artist))
                throw new ArgumentException("Title and Artist are required.");

            if (string.IsNullOrWhiteSpace(youtubeUrl) && string.IsNullOrWhiteSpace(localFilePath))
                throw new ArgumentException("Provide either a YouTube URL or upload a file.");

            var song = new Song
            {
                Title = title,
                Artist = artist,
                YouTubeUrl = youtubeUrl ?? string.Empty,
                LocalFilePath = localFilePath,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Songs.Add(song);
            await _context.SaveChangesAsync();

            // Save mood associations
            if (moodIds.Any())
            {
                foreach (var moodId in moodIds)
                {
                    _context.SongMoods.Add(new SongMood
                    {
                        SongId = song.Id,
                        MoodId = moodId
                    });
                }
                await _context.SaveChangesAsync();
            }

            return await GetSongByIdAsync(song.Id, userId) ?? song;
        }

        // Update existing song
        public async Task<Song?> UpdateSongAsync(
            int songId,
            int userId,
            string title,
            string artist,
            string? youtubeUrl,
            string? localFilePath,
            List<int> moodIds)
        {
            var song = await _context.Songs
                .Include(s => s.SongMoods)
                .FirstOrDefaultAsync(s => s.Id == songId && s.UserId == userId);

            if (song == null) return null;

            song.Title = title;
            song.Artist = artist;
            song.YouTubeUrl = youtubeUrl ?? string.Empty;
            song.LocalFilePath = localFilePath;

            // Clear old moods
            _context.SongMoods.RemoveRange(song.SongMoods);

            // Add new moods
            if (moodIds != null && moodIds.Any())
            {
                foreach (var moodId in moodIds)
                {
                    _context.SongMoods.Add(new SongMood
                    {
                        SongId = song.Id,
                        MoodId = moodId
                    });
                }
            }

            await _context.SaveChangesAsync();
            return await GetSongByIdAsync(song.Id, userId);
        }

        // Delete a song
        public async Task<bool> DeleteSongAsync(int songId, int userId)
        {
            var song = await _context.Songs
                .FirstOrDefaultAsync(s => s.Id == songId && s.UserId == userId);

            if (song == null) return false;

            _context.Songs.Remove(song);
            await _context.SaveChangesAsync();
            return true;
        }

        // Get all moods
        public async Task<List<Mood>> GetAllMoodsAsync()
        {
            return await _context.Moods.OrderBy(m => m.Name).ToListAsync();
        }

        // Get songs by mood
        public async Task<List<Song>> GetSongsByMoodAsync(int moodId, int userId)
        {
            return await _context.Songs
                .Include(s => s.SongMoods)
                .ThenInclude(sm => sm.Mood)
                .Where(s => s.UserId == userId && s.SongMoods.Any(sm => sm.MoodId == moodId))
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();
        }

        // Extract YouTube video ID from URL
        public string ExtractYouTubeVideoId(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return "";

            try
            {
                var uri = new Uri(url);

                if (uri.Host.Contains("youtu.be"))
                    return uri.AbsolutePath.TrimStart('/');

                if (uri.Host.Contains("youtube.com"))
                {
                    var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                    return query["v"] ?? "";
                }
            }
            catch
            {
                return "";
            }

            return "";
        }
    }
}
