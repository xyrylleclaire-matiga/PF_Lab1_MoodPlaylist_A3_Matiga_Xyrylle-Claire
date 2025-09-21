using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MoodPlaylist.SQLite.Models
{
    public class Song
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Title is required.")]
        [StringLength(200, ErrorMessage = "Title must be less than 200 characters.")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Artist is required.")]
        [StringLength(200, ErrorMessage = "Artist name must be less than 200 characters.")]
        public string Artist { get; set; } = string.Empty;

        [Required(ErrorMessage = "YouTube URL is required.")]
        [Url(ErrorMessage = "Please enter a valid YouTube URL.")]
        public string YouTubeUrl { get; set; } = string.Empty;

        public int UserId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Display(Name = "Local File Path")]
        public string? LocalFilePath { get; set; }

        public User User { get; set; } = null!;
        public List<SongMood> SongMoods { get; set; } = new();
        public List<PlaylistSong> PlaylistSongs { get; set; } = new();
    }
}
