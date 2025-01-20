using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using AForge.Video;
using AForge.Video.DirectShow;
using Microsoft.Win32;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GolfClubSystem.Data;
using GolfClubSystem.Models;
using GolfClubSystem.Services;
using Microsoft.EntityFrameworkCore;

namespace GolfClubSystem.Views.WorkersWindow;

public enum WorkerType
{
    Add,
    Edit
}

public partial class AddEditWorkerWindow : Window
{
    public List<Organization> Organizations { get; set; }
    public List<Zone> Zones { get; set; }
    public List<Schedule> Schedules { get; set; }
    public Worker Worker { get; set; }
    public WorkerType WorkerType { get; set; }
    public bool IsEnable { get; set; }


    private readonly UnitOfWork _unitOfWork = new();

    public AddEditWorkerWindow(Worker? worker, bool isEnable = true)
    {
        IsEnable = isEnable;
        InitializeComponent();

        if (worker is not null)
        {
            Worker = worker;
            WorkerType = WorkerType.Edit;
            WorkerPhoto.Source = LoadImage(worker.PhotoPath);
        }
        else
        {
            Worker = new Worker();
            Worker.StartWork = DateTime.Now;
            WorkerType = WorkerType.Add;
        }

        Organizations = _unitOfWork.OrganizationRepository.GetAll().Where(o => o.DeletedAt == null).ToList();
        Zones = _unitOfWork.ZoneRepository.GetAll().Where(o => o.DeletedAt == null).ToList();
        Schedules = _unitOfWork.ScheduleRepository.GetAll().Where(o => o.DeletedAt == null).ToList();
        DataContext = this;
    }

    private BitmapImage LoadImage(string imageUrl)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(imageUrl, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            return bitmap;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return null;
        }
    }

    private void UploadPhoto_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog openFileDialog = new OpenFileDialog
        {
            Filter = "Image Files (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png"
        };

        if (videoSource is { IsRunning: true })
        {
            Task.Run(() =>
            {
                videoSource.SignalToStop();
                videoSource.WaitForStop();

                // Forcefully release resources
                videoSource = null; // This might help the video capture stop faster
            });
        }

        if (openFileDialog.ShowDialog() == true)
        {
            string filePath = openFileDialog.FileName;
            BitmapImage bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(filePath);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();

            WorkerPhoto.Source = bitmap; // Display the image in the Image control
        }
    }

    private VideoCaptureDevice videoSource;

    private void TakePhoto_Click(object sender, RoutedEventArgs e)
    {
        FilterInfoCollection videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
        if (videoDevices.Count == 0)
        {
            MessageBox.Show("Камера не найдена!");
            return;
        }

        videoSource = new VideoCaptureDevice(videoDevices[0].MonikerString);
        videoSource.NewFrame += VideoSource_NewFrame;
        videoSource.Start();
    }

    private void VideoSource_NewFrame(object sender, NewFrameEventArgs eventArgs)
    {
        if (videoSource == null || !videoSource.IsRunning) return;

        using (Bitmap bitmap = (Bitmap)eventArgs.Frame.Clone())
        {
            Dispatcher.Invoke(() =>
            {
                MemoryStream ms = new MemoryStream();
                bitmap.Save(ms, ImageFormat.Bmp);
                ms.Seek(0, SeekOrigin.Begin);

                BitmapImage image = new BitmapImage();
                image.BeginInit();
                image.StreamSource = ms;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.EndInit();

                WorkerPhoto.Source = image; // Display the captured frame in the Image control
            });
        }
    }

    private void CapturePhoto_Click(object sender, RoutedEventArgs e)
    {
        if (videoSource is { IsRunning: true })
        {
            Task.Run(() =>
            {
                videoSource.SignalToStop();
                videoSource.WaitForStop();

                // Forcefully release resources
                videoSource = null; // This might help the video capture stop faster
            });
        }
        else
        {
            MessageBox.Show("Камера не работает!");
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        if (videoSource != null && videoSource.IsRunning)
        {
            Task.Run(() =>
            {
                videoSource.SignalToStop();
                videoSource.WaitForStop();

                // Forcefully release resources
                videoSource = null; // This might help the video capture stop faster
            });
        }
    }

    private void PhoneNumberTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (!char.IsDigit(e.Text, 0) && e.Text != " ")
        {
            e.Handled = true;
        }

        var textBox = sender as TextBox;
        var currentText = textBox.Text;

        if (!currentText.StartsWith("+996"))
        {
            textBox.Text = "+996 " + currentText;
            textBox.SelectionStart = textBox.Text.Length;
            e.Handled = true;
        }
        else
        {
            switch (currentText.Length)
            {
                case 6 when !currentText.Contains(" "):
                    textBox.Text = currentText.Insert(6, " ");
                    textBox.SelectionStart = textBox.Text.Length;
                    e.Handled = true;
                    break;
                case 9 when !currentText.Contains(" "):
                    textBox.Text = currentText.Insert(9, " ");
                    textBox.SelectionStart = textBox.Text.Length;
                    e.Handled = true;
                    break;
                case 12 when !currentText.Contains(" "):
                    textBox.Text = currentText.Insert(12, " ");
                    textBox.SelectionStart = textBox.Text.Length;
                    e.Handled = true;
                    break;
                case >= 14:
                    e.Handled = true;
                    break;
            }
        }
    }

    private async void ButtonBase_OnClick(object sender, RoutedEventArgs e)
    {
        if (WorkerPhoto.Source is null)
        {
            MessageBox.Show("Фотография обьязательная!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var zone = await _unitOfWork.ZoneRepository.GetAll()
            .FirstOrDefaultAsync(z => z.DeletedAt == null && z.Id == Worker.ZoneId);
        var terminalService = new TerminalService(zone.Login, zone.Password);

        if (WorkerType == WorkerType.Add)
        {
            var photoPath = SaveBitmapImage(WorkerPhoto.Source as BitmapImage);
            Worker.PhotoPath = photoPath;
            await _unitOfWork.WorkerRepository.AddAsync(Worker);
            await _unitOfWork.SaveAsync();

            await UpdateAddTerminalEmployee(Worker, photoPath, zone.EnterIp);
            await UpdateAddTerminalEmployee(Worker, photoPath, zone.ExitIp);
        }

        if (WorkerType == WorkerType.Edit)
        {
            var currentWorker = _unitOfWork.WorkerRepository.GetAll().Where(w => w.DeletedAt == null)
                .FirstOrDefault(w => w.Id == Worker.Id);

            if (currentWorker is not null)
            {
                var photoPath = SaveBitmapImage(WorkerPhoto.Source as BitmapImage);
                currentWorker.OrganizationId = Worker.OrganizationId;
                currentWorker.ZoneId = Worker.ZoneId;
                currentWorker.Mobile = Worker.Mobile;
                currentWorker.CardNumber = Worker.CardNumber;
                currentWorker.ScheduleId = Worker.ScheduleId;
                currentWorker.FullName = Worker.FullName;
                currentWorker.StartWork = Worker.StartWork;
                currentWorker.EndWork = Worker.EndWork;
                currentWorker.JobTitle = Worker.JobTitle;
                currentWorker.TelegramUsername = Worker.TelegramUsername;
                currentWorker.AdditionalMobile = Worker.AdditionalMobile;
                currentWorker.PhotoPath = photoPath;
                await _unitOfWork.WorkerRepository.UpdateAsync(currentWorker);
                await _unitOfWork.SaveAsync();

                await terminalService.DeleteUserImageAsync(currentWorker.Id.ToString(), zone.EnterIp);
                await terminalService.DeleteUserImageAsync(currentWorker.Id.ToString(), zone.ExitIp);
                await UpdateAddTerminalEmployee(currentWorker, photoPath, zone.EnterIp);
                await UpdateAddTerminalEmployee(currentWorker, photoPath, zone.ExitIp);
            }
        }

        Close();

        async Task UpdateAddTerminalEmployee(Worker worker, string photoPath, string ip)
        {
            var terminalUserAddedEnter = await terminalService.AddUserInfoAsync(worker, ip);
            if (terminalUserAddedEnter)
            {
                await terminalService.AddUserImageAsync(worker, ip);

                if (worker.CardNumber != null)
                {
                    await terminalService.AddCardInfoAsync(worker, ip);
                }
            }
        }
    }

    public static string SaveBitmapImage(BitmapImage bitmapImage, string directoryPath = "C:\\Users\\user\\Downloads",
        int maxSizeKB = 200)
    {
        // Ensure the directory exists
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        // Generate a new GUID for the file name
        string fileName = Guid.NewGuid().ToString() + ".jpeg"; // Save as JPEG
        string fullPath = Path.Combine(directoryPath, fileName);

        // Convert BitmapImage to Bitmap
        using (var memoryStream = new MemoryStream())
        {
            // Save BitmapImage to a memory stream
            BitmapEncoder encoder = new PngBitmapEncoder(); // Use PNG format for internal storage
            encoder.Frames.Add(BitmapFrame.Create(bitmapImage));
            encoder.Save(memoryStream);

            // Create a Bitmap from the memory stream
            using (var bitmap = new Bitmap(memoryStream))
            {
                // Compress and save as JPEG with the quality setting
                var encoderParams = new EncoderParameters(1);
                var qualityParam = new EncoderParameter(Encoder.Quality, 90L); // Start with 90% quality
                encoderParams.Param[0] = qualityParam;

                // Create JPEG encoder
                var jpegCodec = GetEncoder(ImageFormat.Jpeg);

                // Save the Bitmap with the quality setting
                int quality = 90;
                do
                {
                    using (var outputStream = new MemoryStream())
                    {
                        bitmap.Save(outputStream, jpegCodec, encoderParams);

                        // Check the size and adjust quality if necessary
                        if (outputStream.Length / 1024 > maxSizeKB) // If file is larger than maxSizeKB (in KB)
                        {
                            quality -= 5; // Decrease quality to reduce size
                            qualityParam = new EncoderParameter(Encoder.Quality, quality);
                            encoderParams.Param[0] = qualityParam;
                        }
                        else
                        {
                            // Save the final file
                            File.WriteAllBytes(fullPath, outputStream.ToArray());
                            break;
                        }
                    }
                } while (quality > 10); // Ensure that the quality does not go below 10%
            }
        }

        // Return the URL to access the saved image
        return $"http://192.168.0.2:8080/{fileName}";
    }

    // Helper method to get the JPEG encoder
    private static ImageCodecInfo GetEncoder(ImageFormat format)
    {
        foreach (ImageCodecInfo codec in ImageCodecInfo.GetImageEncoders())
        {
            if (codec.FormatID == format.Guid)
            {
                return codec;
            }
        }

        return null;
    }
}