using System.Net.Http;
using System.Text;
using System.Windows;
using GolfClubSystem.Models;
using GolfClubSystem.Services;
using GolfClubSystem.Views.WorkersWindow;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Serilog;

namespace GolfClubSystem.Views.UserControlsViews.AdminControlsViews;

public partial class AddEditZoneWindow : Window
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly LoadingService _loadingService;
    
    public Zone Zone { get; set; }
    public WorkerType ZoneType { get; set; }
    public bool IsEnable { get; set; }

    public AddEditZoneWindow(Zone? zone, bool isEnable = true)
    {
        _configuration = ((App)Application.Current)._configuration;
        var apiUrl = _configuration.GetSection("ApiUrl").Value
                     ?? throw new Exception("ApiUrl не прописан в конфигах!");
        _httpClient = new HttpClient { BaseAddress = new Uri(apiUrl) };
        _loadingService = LoadingService.Instance;
        
        IsEnable = isEnable;
        InitializeComponent();

        if (zone is not null)
        {
            Zone = zone;
            ZoneType = WorkerType.Edit;
        }
        else
        {
            Zone = new Zone();
            ZoneType = WorkerType.Add;
        }

        DataContext = this;
    }

    private async void ButtonBase_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Zone.Name) || string.IsNullOrWhiteSpace(Zone.Login) ||
            string.IsNullOrWhiteSpace(Zone.Password) || string.IsNullOrWhiteSpace(Zone.EnterIp) ||
            string.IsNullOrWhiteSpace(Zone.ExitIp) || string.IsNullOrWhiteSpace(Zone.NotifyIp))
        {
            new DialogWindow("Ошибка", "Все поля обязательны для заполнения.").ShowDialog();
            return;
        }
        
        _loadingService.StartLoading();
        try
        {
            var content = new StringContent(JsonConvert.SerializeObject(Zone), Encoding.UTF8, "application/json");
            HttpResponseMessage response;

            if (ZoneType == WorkerType.Add)
            {
                response = await _httpClient.PostAsync("api/Admin/zones", content);
            }
            else
            {
                response = await _httpClient.PutAsync($"api/Admin/zones/{Zone.Id}", content);
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                var error = JsonConvert.DeserializeObject<dynamic>(errorContent);
                new DialogWindow("Ошибка", error?.Message).ShowDialog();
                _loadingService.StopLoading();
                return;
            }

            Close();
        }
        catch (Exception ex)
        {
            new DialogWindow("Ошибка", $"Произошла ошибка: {ex.Message}").ShowDialog();
            Log.Error(ex, "Error in zone operation");
        }
        finally
        {
            _loadingService.StopLoading();
        }
    }
}