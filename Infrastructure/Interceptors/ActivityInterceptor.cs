namespace grad.Infrastructure.Interceptors
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Diagnostics;
    using grad.Models;

    public class ActivityInterceptor : SaveChangesInterceptor
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ActivityInterceptor(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            var context = eventData.Context;
            if (context == null) return result;

            var http = _httpContextAccessor.HttpContext;
            if (http == null) return result;

            // var adminIdStr = http.User?.FindFirst("uid")?.Value;
            // if (adminIdStr == null) return result;
            // بدل "uid" ممكن تكون ClaimTypes.NameIdentifier

            var adminIdStr = http.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            var adminId = Guid.Parse(adminIdStr);

            var adminName = http.User?.FindFirst("name")?.Value ?? "System";

            var logs = new List<ActivityLogs>();

            foreach (var entry in context.ChangeTracker.Entries())
            {
                if (entry.State == EntityState.Unchanged)
                    continue;

                string entityName = entry.Entity.GetType().Name;

                string action = entry.State switch
                {
                    EntityState.Added => "added",
                    EntityState.Modified => "updated",
                    EntityState.Deleted => "deleted",
                    _ => null
                };

                if (action == null) continue;

                string entityValue = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "firstname")?.CurrentValue?.ToString()
                                     ?? "record";

                string text = $"{adminName} {action} {entityName}: {entityValue}";

                logs.Add(new ActivityLogs
                {
                    Text = text,
                    UserId = adminId,
                    CreatedAt = DateTime.UtcNow
                });
            }

            if (logs.Any())
            {
                context.Set<ActivityLogs>().AddRange(logs);
            }

            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
