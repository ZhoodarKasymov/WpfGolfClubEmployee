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

public partial class ZonesView : UserControl, INotifyPropertyChanged
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly LoadingService _loadingService;
    
    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<Zone> Zones { get; set; }
    
    public ICommand EditCommand { get; }
    public ICommand DeleteCommand { get; }
    
    public ZonesView()
    {
        _configuration = ((App)Application.Current)._configuration;
        var apiUrl = _configuration.GetSection("ApiUrl").Value
                     ?? throw new Exception("ApiUrl не прописан в конфигах!");
        _httpClient = new HttpClient { BaseAddress = new Uri(apiUrl) };
        _loadingService = LoadingService.Instance;
        
        InitializeComponent();
        EditCommand = new RelayCommand<Zone>(OnEdit);
        DeleteCommand = new RelayCommand<Zone>(OnDelete);
        UpdateZones();
        DataContext = this;
    }

    private async void UpdateZones()
    {
        _loadingService.StartLoading();
        try
        {
            var response = await _httpClient.GetAsync("api/Admin/zones");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var zones = JsonConvert.DeserializeObject<List<Zone>>(json) ?? [];
            
            Zones = new ObservableCollection<Zone>(zones);
            OnPropertyChanged(nameof(Zones));
        }
        finally
        {
            _loadingService.StopLoading();
        }
    }


    private void AddZoneCommand(object sender, RoutedEventArgs e)
    {
        var window = new AddEditZoneWindow(null);
        window.ShowDialog();
        UpdateZones();
    }
    
    private void OnEdit(Zone zone)
    {
        var window = new AddEditZoneWindow(zone);
        window.ShowDialog();
        UpdateZones();
    }

    private async void OnDelete(Zone zone)
    {
        if (zone == null) return;
        
        var answer = new DialogWindow("Подтверждение", $"Вы уверены удалить зону: {zone.Name}?", "Да", "Нет").ShowDialog();

        if (answer.HasValue && answer.Value)
        {
            _loadingService.StartLoading();
            try
            {
                var response = await _httpClient.DeleteAsync($"api/Admin/zones/{zone.Id}");
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    new DialogWindow("Ошибка", $"Ошибка удаления зоны: {errorContent}").ShowDialog();
                    _loadingService.StopLoading();
                    return;
                }

                UpdateZones();
            }
            catch (Exception ex)
            {
                new DialogWindow("Ошибка", $"Ошибка удаления зоны: {ex.Message}").ShowDialog();
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