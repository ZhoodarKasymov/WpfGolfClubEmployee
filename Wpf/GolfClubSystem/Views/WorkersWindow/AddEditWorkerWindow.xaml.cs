using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using AForge.Video;
using AForge.Video.DirectShow;
using Microsoft.Win32;
using System.Drawing;
using System.Drawing.Imaging;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Input;
using GolfClubSystem.Models;
using GolfClubSystem.Services;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

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


    private readonly HttpClient _httpClient;
    private readonly IConfigurationRoot _configuration = ((App)Application.Current)._configuration;
    private readonly LoadingService _loadingService;

    public AddEditWorkerWindow(Worker? worker, bool isEnable = true)
    {
        var apiUrl = _configuration.GetSection("ApiUrl").Value
                     ?? throw new Exception("ApiUrl не прописан в конфигах!");
        _httpClient = new HttpClient { BaseAddress = new Uri(apiUrl) };
        _loadingService = LoadingService.Instance;

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
        
        LoadInitialDataAsync();
    }

    private async void LoadInitialDataAsync()
    {
        _loadingService.StartLoading();
        try
        {
            await Task.WhenAll(
                LoadOrganizationsAsync(),
                LoadZonesAsync(),
                LoadSchedulesAsync()
            );

            DataContext = this;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка загрузки данных: {ex.Message}", "Ошибка", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _loadingService.StopLoading();
        }
    }

    private async Task LoadOrganizationsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("api/Hr/organizations");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            Organizations = (JsonConvert.DeserializeObject<List<Organization>>(json) ?? [])
                .Where(o => o.Id != -1) // Exclude "Все"
                .ToList();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка загрузки организаций: {ex.Message}", "Ошибка", MessageBoxButton.OK,
                MessageBoxImage.Error);
            Organizations = new List<Organization>();
        }
    }

    private async Task LoadZonesAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("api/Hr/zones");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            Zones = (JsonConvert.DeserializeObject<List<Zone>>(json) ?? [])
                .Where(z => z.Id != -1) // Exclude "Все"
                .ToList();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка загрузки зон: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            Zones = new List<Zone>();
        }
    }

    private async Task LoadSchedulesAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("api/Hr/schedules");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            Schedules = JsonConvert.DeserializeObject<List<Schedule>>(json) ?? [];
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка загрузки расписаний: {ex.Message}", "Ошибка", MessageBoxButton.OK,
                MessageBoxImage.Error);
            Schedules = new List<Schedule>();
        }
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

        _httpClient.Dispose();
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

    private void NumberTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !IsTextAllowed(e.Text);
    }

    private static bool IsTextAllowed(string text)
    {
        return Regex.IsMatch(text, "^[0-9]+$");
    }

    private async void ButtonBase_OnClick(object sender, RoutedEventArgs e)
    {
        if (WorkerPhoto.Source is null)
        {
            MessageBox.Show("Фотография обьязательная!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        _loadingService.StartLoading();
        try
        {
            // Convert WorkerPhoto.Source to byte array
            byte[] imageBytes;
            var bitmapImage = WorkerPhoto.Source as BitmapImage;
            using (var ms = new MemoryStream())
            {
                var encoder = new JpegBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmapImage));
                encoder.Save(ms);
                imageBytes = ms.ToArray();
            }

            // Create multipart form data
            using (var formContent = new MultipartFormDataContent())
            {
                var workerJson = JsonConvert.SerializeObject(Worker);
                formContent.Add(new StringContent(workerJson, System.Text.Encoding.UTF8, "application/json"),
                    "workerJson");

                // Add image
                formContent.Add(new ByteArrayContent(imageBytes), "image", "worker.jpg");

                HttpResponseMessage response;

                if (WorkerType == WorkerType.Add)
                {
                    response = await _httpClient.PostAsync("api/Hr/workers", formContent);
                }
                else
                {
                    response = await _httpClient.PutAsync($"api/Hr/workers/{Worker.Id}", formContent);
                }

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"API Error: {response.StatusCode}, Details: {errorContent}");
                }
                
                Close();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка сохранения работника: {ex.Message}", "Ошибка", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _loadingService.StopLoading();
        }
    }
}