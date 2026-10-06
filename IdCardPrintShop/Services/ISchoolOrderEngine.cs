using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IdCardPrintShop.Models;

namespace IdCardPrintShop.Services
{
    public class ScanFolderResult
    {
        public int TotalFiles { get; set; }
        public int ValidCount { get; set; }
        public int UnsupportedCount => UnsupportedFiles.Count;
        public int InvalidCount => InvalidImages.Count;
        public List<StudentBatchItem> Students { get; set; } = new();
        public List<string> UnsupportedFiles { get; set; } = new();
        public List<string> InvalidImages { get; set; } = new();
        public string SourceFolder { get; set; } = string.Empty;
    }

    public class BatchProgressReport
    {
        public int CurrentIndex { get; set; }
        public int TotalCount { get; set; }
        public string CurrentStudentName { get; set; } = string.Empty;
        public double Percentage => TotalCount > 0 ? (double)CurrentIndex / TotalCount * 100.0 : 0;
        public int SuccessCount { get; set; }
        public int WarningCount { get; set; }
        public int FailedCount { get; set; }
    }

    public class BatchProcessingResult
    {
        public bool Success => FailedCount == 0;
        public int TotalStudents { get; set; }
        public int SuccessCount { get; set; }
        public int WarningCount { get; set; }
        public int FailedCount { get; set; }
        public int PagesGenerated { get; set; }
        public string CombinedPdfPath { get; set; } = string.Empty;
        public List<string> IndividualPdfPaths { get; set; } = new();
        public string OutputDirectory { get; set; } = string.Empty;
        public string ReportJsonPath { get; set; } = string.Empty;
        public string ReportTextPath { get; set; } = string.Empty;
        public List<string> Logs { get; set; } = new();
        public List<StudentBatchItem> FailedStudents { get; set; } = new();
    }

    public interface ISchoolOrderEngine
    {
        Task<ScanFolderResult> ScanFolderAsync(
            string folderPath,
            IProgress<int>? progress = null,
            CancellationToken ct = default);

        LayoutPlan CalculateSchoolLayout(
            SchoolPackageTemplate template,
            IReadOnlyList<StudentBatchItem> students);

        Task<BatchProcessingResult> ProcessBatchAsync(
            SchoolOrder order,
            SchoolPackageTemplate template,
            IProgress<BatchProgressReport>? progress = null,
            CancellationToken ct = default);

        Task<BatchProcessingResult> ReprocessFailedAsync(
            SchoolOrder order,
            SchoolPackageTemplate template,
            IProgress<BatchProgressReport>? progress = null,
            CancellationToken ct = default);
    }
}
