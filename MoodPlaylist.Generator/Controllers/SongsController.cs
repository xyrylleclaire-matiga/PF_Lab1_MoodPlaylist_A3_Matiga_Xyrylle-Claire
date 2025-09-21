using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MoodPlaylist.SQLite.Models;
using MoodPlaylist.SQLite.Services;
using MoodPlaylistGenerator.Services;
using System.IO;
using System.Linq;
using System.Security.Claims;

namespace MoodPlaylistGenerator.Controllers
{
    [Authorize]
    public class SongsController : Controller
    {
        private readonly SongService _songService;
        private readonly ILocalMediaService _localMediaService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<SongsController> _logger;

        public SongsController(
            SongService songService,
            ILocalMediaService localMediaService,
            IConfiguration configuration,
            ILogger<SongsController> logger)
        {
            _songService = songService;
            _localMediaService = localMediaService;
            _configuration = configuration;
            _logger = logger;
        }

        private int GetCurrentUserId() =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

        public async Task<IActionResult> Index(int? moodId, string? search)
        {
            var userId = GetCurrentUserId();
            var songs = await _songService.GetUserSongsAsync(userId);
            var moods = await _songService.GetAllMoodsAsync();

            if (moodId.HasValue)
                songs = await _songService.GetSongsByMoodAsync(moodId.Value, userId);

            if (!string.IsNullOrWhiteSpace(search))
                songs = songs.Where(s =>
                    s.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    s.Artist.Contains(search, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            ViewBag.Moods = moods;
            ViewBag.SelectedMoodId = moodId;
            ViewBag.SearchTerm = search ?? "";

            return View(songs); // just pass list of songs
        }

        public async Task<IActionResult> Details(int id)
        {
            var userId = GetCurrentUserId();
            var song = await _songService.GetSongByIdAsync(id, userId);

            if (song == null) return NotFound();

            ViewBag.YouTubeVideoId = _songService.ExtractYouTubeVideoId(song.YouTubeUrl);
            ViewBag.AssignedMoods = song.SongMoods.Select(sm => sm.Mood).ToList();

            return View(song);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            ViewBag.AvailableMoods = await _songService.GetAllMoodsAsync();
            return View(new Song());
        }

        [HttpPost]
        public async Task<IActionResult> Create(Song model, IFormFile? mediaFile, List<int>? selectedMoodIds)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.AvailableMoods = await _songService.GetAllMoodsAsync();
                return View(model);
            }

            var userId = GetCurrentUserId();
            string? localFilePath = null;

            if (mediaFile is { Length: > 0 })
                localFilePath = await _localMediaService.SaveFileAsync(mediaFile);

            if (string.IsNullOrWhiteSpace(localFilePath) && string.IsNullOrWhiteSpace(model.YouTubeUrl))
            {
                ModelState.AddModelError("", "Please provide either a YouTube URL or upload a file.");
                ViewBag.AvailableMoods = await _songService.GetAllMoodsAsync();
                return View(model);
            }

            try
            {
                await _songService.CreateSongAsync(
                    model.Title,
                    model.Artist,
                    string.IsNullOrWhiteSpace(localFilePath) ? model.YouTubeUrl : null,
                    localFilePath,
                    userId,
                    selectedMoodIds ?? new List<int>()
                );

                TempData["SuccessMessage"] = "Song added successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding song");
                ModelState.AddModelError("", "An error occurred while adding the song.");
                ViewBag.AvailableMoods = await _songService.GetAllMoodsAsync();
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var userId = GetCurrentUserId();
            var song = await _songService.GetSongByIdAsync(id, userId);

            if (song == null) return NotFound();

            ViewBag.AvailableMoods = await _songService.GetAllMoodsAsync();
            return View(song);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(Song model, IFormFile? mediaFile, List<int>? selectedMoodIds)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.AvailableMoods = await _songService.GetAllMoodsAsync();
                return View(model);
            }

            var userId = GetCurrentUserId();
            string? localFilePath = model.LocalFilePath;

            if (mediaFile is { Length: > 0 })
                localFilePath = await _localMediaService.SaveFileAsync(mediaFile);

            if (string.IsNullOrWhiteSpace(localFilePath) && string.IsNullOrWhiteSpace(model.YouTubeUrl))
            {
                ModelState.AddModelError("", "Please provide either a YouTube URL or upload a file.");
                ViewBag.AvailableMoods = await _songService.GetAllMoodsAsync();
                return View(model);
            }

            try
            {
                var updatedSong = await _songService.UpdateSongAsync(
                    model.Id,
                    userId,
                    model.Title,
                    model.Artist,
                    string.IsNullOrWhiteSpace(localFilePath) ? model.YouTubeUrl : null,
                    localFilePath,
                    selectedMoodIds ?? new List<int>()
                );

                if (updatedSong == null) return NotFound();

                TempData["SuccessMessage"] = "Song updated successfully!";
                return RedirectToAction(nameof(Details), new { id = model.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating song {SongId}", model.Id);
                ModelState.AddModelError("", "An error occurred while updating the song.");
                ViewBag.AvailableMoods = await _songService.GetAllMoodsAsync();
                return View(model);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = GetCurrentUserId();
            var success = await _songService.DeleteSongAsync(id, userId);

            if (!success) return NotFound();

            TempData["SuccessMessage"] = "Song deleted successfully!";
            return RedirectToAction(nameof(Index));
        }

        public IActionResult PlayLocal(string filePath)
        {
            var safeFileName = Path.GetFileName(filePath);
            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", safeFileName);

            if (!System.IO.File.Exists(fullPath))
            {
                ViewBag.MediaUrl = _configuration["FallbackVideo:RickRollUrl"];
                ViewBag.IsLocal = false;
            }
            else
            {
                ViewBag.MediaUrl = "/" + safeFileName;
                ViewBag.IsLocal = true;
            }

            return View("Player");
        }
    }
}
