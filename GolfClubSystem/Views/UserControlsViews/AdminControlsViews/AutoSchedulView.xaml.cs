using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GolfClubSystem.Models;
using GolfClubSystem.Services;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace GolfClubSystem.Views.UserControlsViews.AdminControlsViews;

public partial class AutoSchedulView : UserControl, INotifyPropertyChanged
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly LoadingService _loadingService;
    
    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<NotifyJob> Jobes { get; set; }
    
    public ICommand EditCommand { get; }
    public ICommand DeleteCommand { get; }
    
    public AutoSchedulView()
    {
        _configuration = ((App)Application.Current)._configuration;
        var apiUrl = _configuration.GetSection("ApiUrl").Value
                     ?? throw new Exception("ApiUrl не прописан в конфигах!");
        _httpClient = new HttpClient { BaseAddress = new Uri(apiUrl) };
        _loadingService = LoadingService.Instance;
        
        InitializeComponent();
        EditCommand = new RelayCommand<NotifyJob>(OnEdit);
        DeleteCommand = new RelayCommand<NotifyJob>(OnDelete);
        UpdateZones();
        DataContext = this;
    }

    private async void UpdateZones()
    {
        _loadingService.StartLoading();
        try
        {
            var response = await _httpClient.GetAsync("api/Admin/autoSchedules");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var zones = JsonConvert.DeserializeObject<List<NotifyJob>>(json) ?? [];
            
            Jobes = new ObservableCollection<NotifyJob>(zones);
            OnPropertyChanged(nameof(Jobes));
        }
        finally
        {
            _loadingService.StopLoading();
        }
    }


    private void AddJobCommand(object sender, RoutedEventArgs e)
    {
        var window = new AutoScheduleAddWindow(null);
        window.ShowDialog();
        UpdateZones();
    }
    
    private void OnEdit(NotifyJob zone)
    {
        var window = new AutoScheduleAddWindow(zone);
        window.ShowDialog();
        UpdateZones();
    }

    private async void OnDelete(NotifyJob zone)
    {
        if (zone == null) return;
        
        var answer = new DialogWindow("Подтверждение", $"Вы уверены удалить авто уведомление на: {zone.Time}?", "Да", "Нет").ShowDialog();

        if (answer.HasValue && answer.Value)
        {
            _loadingService.StartLoading();
            try
            {
                var response = await _httpClient.DeleteAsync($"api/Admin/autoSchedules/{zone.Id}");
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    new DialogWindow("Ошибка", $"Ошибка удаления авто уведомления: {errorContent}").ShowDialog();
                    _loadingService.StopLoading();
                    return;
                }

                UpdateZones();
            }
            catch (Exception ex)
            {
                new DialogWindow("Ошибка", $"Ошибка удаления авто уведомления: {ex.Message}").ShowDialog();
            }
            finally
            {
                _loadingService.StopLoading();
            }
        }
    }
    
    
    private void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}