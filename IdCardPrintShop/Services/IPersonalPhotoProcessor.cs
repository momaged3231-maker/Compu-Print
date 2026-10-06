using System;
using System.Threading.Tasks;
using IdCardPrintShop.Models;
using OpenCvSharp;

namespace IdCardPrintShop.Services
{
    public interface IPersonalPhotoProcessor : IDisposable
    {
        Task<PersonalPhotoItem> ProcessPhotoAsync(string imagePath, PhotoProfile? profile = null);
        PersonalPhotoItem ProcessPhoto(Mat sourceMat, string sourcePath, PhotoProfile? profile = null);
        Mat RenderFinalPhoto(Mat sourceMat, PersonalPhotoParameters parameters, PhotoProfile profile);
        RectD CalculateAutoCrop(int imageWidth, int imageHeight, DetectedFace? face, PhotoProfile profile);
    }
}
