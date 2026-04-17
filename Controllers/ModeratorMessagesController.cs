
using grad.Data;
using grad.DTOs;
using grad.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace grad.Controllers
{
    [ApiController]
    [Route("api/moderator/messages")]
    [Authorize(Roles = "Moderator")]
    public class ModeratorMessagesController : ControllerBase
    {
        private readonly AppDbContext _db;

        public ModeratorMessagesController(AppDbContext db)
        {
            _db = db;
        }

       
        [HttpGet("contacts")]
        public async Task<IActionResult> GetContacts()
        {
            var moderatorUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var messages = await _db.Messages
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
                .Where(m => m.SenderId == moderatorUserId || m.ReceiverId == moderatorUserId)
                .ToListAsync();

            var studentUserIds = messages
                .Select(m => m.SenderId == moderatorUserId ? m.ReceiverId : m.SenderId)
                .Distinct()
                .ToList();

            var contacts = new List<ContactDto>();

            foreach (var studentUserId in studentUserIds)
            {
                var thread = messages
                    .Where(m =>
                        (m.SenderId == moderatorUserId && m.ReceiverId == studentUserId) ||
                        (m.SenderId == studentUserId   && m.ReceiverId == moderatorUserId))
                    .ToList();

                var lastMsg = thread.MaxBy(m => m.SentAt);
                if (lastMsg is null) continue;

                var studentUser = lastMsg.SenderId == studentUserId
                    ? lastMsg.Sender
                    : lastMsg.Receiver;

                var unreadCount = thread.Count(m =>
                    m.SenderId == studentUserId &&
                    m.ReceiverId == moderatorUserId &&
                    !m.IsRead);

                contacts.Add(new ContactDto
                {
                    StudentId      = studentUserId,
                    FullName       = studentUser.FullName,
                    AvatarInitials = BuildInitials(studentUser.firstname, studentUser.lastname),
                    LastMessage    = lastMsg.Content.Length > 60
                                        ? lastMsg.Content[..60] + "…"
                                        : lastMsg.Content,
                    LastMessageAt  = lastMsg.SentAt,
                    TimeAgo        = BuildTimeAgo(lastMsg.SentAt),
                    UnreadCount    = unreadCount
                });
            }

            var result = contacts
                .OrderByDescending(c => c.LastMessageAt)
                .ToList();

            return Ok(result);
        }


        [HttpGet("{studentId:guid}")]
        public async Task<IActionResult> GetThread(Guid studentId)
        {
            var moderatorUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            await _db.Messages
                .Where(m =>
                    m.SenderId   == studentId &&
                    m.ReceiverId == moderatorUserId &&
                    !m.IsRead)
                .ExecuteUpdateAsync(setters =>
                    setters.SetProperty(m => m.IsRead, true));

            var thread = await _db.Messages
                .Include(m => m.Sender)
                .Where(m =>
                    (m.SenderId == moderatorUserId && m.ReceiverId == studentId) ||
                    (m.SenderId == studentId       && m.ReceiverId == moderatorUserId))
                .OrderBy(m => m.SentAt)
                .Select(m => new ChatMessageDto
                {
                    Id              = m.Id,
                    SenderId        = m.SenderId,
                    SenderName      = m.Sender.FullName,
                    IsFromModerator = m.SenderId == moderatorUserId,
                    Content         = m.Content,
                    SentAt  = DateTime.SpecifyKind(m.SentAt, DateTimeKind.Utc),
                    TimeAgo = BuildTimeAgo(m.SentAt),
                    IsRead  = m.IsRead
                })
                .ToListAsync();

            return Ok(thread);
        }

      
        [HttpPost("{studentId:guid}")]
        public async Task<IActionResult> SendMessage(
            Guid studentId,
            [FromBody] SendMessageDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Content))
                return BadRequest(new { message = "Message content cannot be empty." });

            var moderatorUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var studentExists = await _db.Users.AnyAsync(u => u.Id == studentId);
            if (!studentExists)
                return NotFound(new { message = "Student not found." });

            var message = new Message
            {
                SenderId   = moderatorUserId,
                ReceiverId = studentId,
                Content    = dto.Content.Trim(),
                SentAt     = DateTime.UtcNow,
                IsRead     = false
            };

            _db.Messages.Add(message);
            await _db.SaveChangesAsync();

            return Ok(new ChatMessageDto
            {
                Id              = message.Id,
                SenderId        = message.SenderId,
                SenderName      = User.FindFirstValue(ClaimTypes.Name) ?? "Moderator",
                IsFromModerator = true,
                Content         = message.Content,
                SentAt          = message.SentAt,
                TimeAgo         = "Just now",
                IsRead          = false
            });
        }

        private static string BuildTimeAgo(DateTime utcTime)
        {
            var utc  = DateTime.SpecifyKind(utcTime, DateTimeKind.Utc);
            var span = DateTime.UtcNow - utc;

            if (span.TotalMinutes < 1)  return "Just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
            if (span.TotalHours < 24)   return $"{(int)span.TotalHours}h ago";
            if (span.TotalDays < 7)     return $"{(int)span.TotalDays}d ago";
            return utc.ToString("MMM dd, yyyy");
        }

        private static string BuildInitials(string first, string last)
        {
            var f = string.IsNullOrWhiteSpace(first) ? "?" : first[0].ToString().ToUpper();
            var l = string.IsNullOrWhiteSpace(last)  ? ""  : last[0].ToString().ToUpper();
            return f + l;
        }
    }
}
