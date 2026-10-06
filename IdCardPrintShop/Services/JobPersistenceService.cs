using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using IdCardPrintShop.Models;

namespace IdCardPrintShop.Services
{
    public interface IJobPersistenceService
    {
        Task SaveJobAsync(JobOrder job, string filePath);
        Task<JobOrder> LoadJobAsync(string filePath);
    }

    public class JobPersistenceService : IJobPersistenceService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public async Task SaveJobAsync(JobOrder job, string filePath)
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            using var stream = File.Create(filePath);
            await JsonSerializer.SerializeAsync(stream, job, JsonOptions);
        }

        public async Task<JobOrder> LoadJobAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"ملف المشروع غير موجود: {filePath}");
            }

            using var stream = File.OpenRead(filePath);
            var job = await JsonSerializer.DeserializeAsync<JobOrder>(stream, JsonOptions);
            return job ?? throw new InvalidOperationException("فشل قراءة بيانات المشروع.");
        }
    }
}
