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
using GolfClubSystem.Data;
using GolfClubSystem.Models;

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

        Organizations = _unitOfWork.OrganizationRepository.GetAllAsync().Where(o => o.DeletedAt == null).ToList();
        Zones = _unitOfWork.ZoneRepository.GetAllAsync().Where(o => o.DeletedAt == null).ToList();
        Schedules = _unitOfWork.ScheduleRepository.GetAllAsync().Where(o => o.DeletedAt == null).ToList();
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

        if (WorkerType == WorkerType.Add)
        {
            var photoPath = SaveBitmapImage(WorkerPhoto.Source as BitmapImage);
            Worker.PhotoPath = photoPath;
            await _unitOfWork.WorkerRepository.AddAsync(Worker);
        }

        if (WorkerType == WorkerType.Edit)
        {
            var currentWorker = _unitOfWork.WorkerRepository.GetAllAsync().Where(w => w.DeletedAt == null)
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
            }
        }

        await _unitOfWork.SaveAsync();
        Close();
    }

    public static string SaveBitmapImage(BitmapImage bitmapImage, string directoryPath = "C:\\Users\\user\\Downloads")
    {
        // Ensure the directory exists
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        // Generate a new GUID for the file name
        string fileName = Guid.NewGuid().ToString() + ".png"; // Save as PNG
        string fullPath = Path.Combine(directoryPath, fileName);

        // Convert BitmapImage to Bitmap
        using (var memoryStream = new MemoryStream())
        {
            // Save BitmapImage to a memory stream
            BitmapEncoder encoder = new PngBitmapEncoder(); // Use PNG format
            encoder.Frames.Add(BitmapFrame.Create(bitmapImage));
            encoder.Save(memoryStream);

            // Create a Bitmap from the memory stream
            using (var bitmap = new Bitmap(memoryStream))
            {
                // Save the Bitmap to the specified directory
                bitmap.Save(fullPath, ImageFormat.Png);
            }
        }

        return $"http://192.168.0.2:8080/{fileName}";
    }
}