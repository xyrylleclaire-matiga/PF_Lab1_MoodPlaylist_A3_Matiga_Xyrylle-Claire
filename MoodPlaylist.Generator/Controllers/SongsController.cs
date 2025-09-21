using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MoodPlaylist.SQLite.Models;
using MoodPlaylist.SQLite.Services;
using MoodPlaylistGenerator.Services;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

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

        // ====================== INDEX ======================
        [HttpGet]
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

            return View(songs);
        }

        // ====================== DETAILS ======================
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var userId = GetCurrentUserId();
            var song = await _songService.GetSongByIdAsync(id, userId);

            if (song == null) return NotFound();

            ViewBag.YouTubeVideoId = _songService.ExtractYouTubeVideoId(song.YouTubeUrl);
            ViewBag.AssignedMoods = song.SongMoods.Select(sm => sm.Mood).ToList();

            return View(song);
        }

        // ====================== CREATE ======================
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            ViewBag.AvailableMoods = await _songService.GetAllMoodsAsync();
            return View(new Song());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            Song model, // <-- FIXED: removed [Bind(...)]
            IFormFile? mediaFile,
            List<int>? selectedMoodIds)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.AvailableMoods = await _songService.GetAllMoodsAsync();
                return View(model);
            }

            var userId = GetCurrentUserId();
            string? localFilePath = null;
            string? youTubeUrl = model.YouTubeUrl;

            try
            {
                if (mediaFile != null && mediaFile.Length > 0)
                {
                    localFilePath = await _localMediaService.SaveFileAsync(mediaFile);
                    if (string.IsNullOrWhiteSpace(localFilePath))
                    {
                        ModelState.AddModelError("", "Failed to save uploaded file.");
                        ViewBag.AvailableMoods = await _songService.GetAllMoodsAsync();
                        return View(model);
                    }
                    // If a file is uploaded, we clear the YouTubeUrl to avoid ambiguity
                    youTubeUrl = null;
                }

                if (string.IsNullOrWhiteSpace(youTubeUrl) && string.IsNullOrWhiteSpace(localFilePath))
                {
                    ModelState.AddModelError("", "Please provide either a YouTube URL or upload a file.");
                    ViewBag.AvailableMoods = await _songService.GetAllMoodsAsync();
                    return View(model);
                }

                selectedMoodIds ??= new List<int>();

                await _songService.CreateSongAsync(
                    model.Title,
                    model.Artist,
                    youTubeUrl,
                    localFilePath,
                    userId,
                    selectedMoodIds
                );

                TempData["SuccessMessage"] = "Song added successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding song");
                ModelState.AddModelError("", "An error occurred while adding the song. Check logs.");
                ViewBag.AvailableMoods = await _songService.GetAllMoodsAsync();
                return View(model);
            }
        }

        // ====================== EDIT ======================
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
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            [Bind("Id,Title,Artist,YouTubeUrl,LocalFilePath")] Song model,
            IFormFile? mediaFile,
            List<int>? selectedMoodIds)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.AvailableMoods = await _songService.GetAllMoodsAsync();
                return View(model);
            }

            var userId = GetCurrentUserId();
            string? localFilePath = model.LocalFilePath;
            string? youTubeUrl = model.YouTubeUrl;

            try
            {
                if (mediaFile != null && mediaFile.Length > 0)
                {
                    localFilePath = await _localMediaService.SaveFileAsync(mediaFile);
                    youTubeUrl = null;
                }
                else if (!string.IsNullOrWhiteSpace(youTubeUrl))
                {
                    localFilePath = null;
                }

                if (string.IsNullOrWhiteSpace(youTubeUrl) && string.IsNullOrWhiteSpace(localFilePath))
                {
                    ModelState.AddModelError("", "Please provide either a YouTube URL or upload a file.");
                    ViewBag.AvailableMoods = await _songService.GetAllMoodsAsync();
                    return View(model);
                }

                selectedMoodIds ??= new List<int>();

                var updatedSong = await _songService.UpdateSongAsync(
                    model.Id,
                    userId,
                    model.Title,
                    model.Artist,
                    youTubeUrl,
                    localFilePath,
                    selectedMoodIds
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

        // ====================== DELETE ======================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = GetCurrentUserId();
            var success = await _songService.DeleteSongAsync(id, userId);

            if (!success) return NotFound();

            TempData["SuccessMessage"] = "Song deleted successfully!";
            return RedirectToAction(nameof(Index));
        }

        // ====================== PLAY LOCAL ======================
        [HttpGet]
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
