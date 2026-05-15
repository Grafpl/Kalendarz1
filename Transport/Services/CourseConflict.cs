namespace Kalendarz1.Transport.Services
{
    public enum ConflictLevel { Error, Warning, Info }

    public class CourseConflict
    {
        public string Code { get; set; } = "";
        public ConflictLevel Level { get; set; }
        public string Message { get; set; } = "";
        public string? Detail { get; set; }
    }
}
