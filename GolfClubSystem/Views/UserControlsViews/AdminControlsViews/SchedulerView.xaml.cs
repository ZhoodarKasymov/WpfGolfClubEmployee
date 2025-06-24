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

public partial class SchedulerView : UserControl, INotifyPropertyChanged
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly LoadingService _loadingService;

    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<Schedule> Schedules { get; set; }

    public ICommand EditCommand { get; }
    public ICommand DeleteCommand { get; }

    public SchedulerView()
    {
        _configuration = ((App)Application.Current)._configuration;
        var apiUrl = _configuration.GetSection("ApiUrl").Value
                     ?? throw new Exception("ApiUrl не прописан в конфигах!");
        _httpClient = new HttpClient { BaseAddress = new Uri(apiUrl) };
        _loadingService = LoadingService.Instance;

        InitializeComponent();
        EditCommand = new RelayCommand<Schedule>(OnEdit);
        DeleteCommand = new RelayCommand<Schedule>(OnDelete);
        UpdateSchedules();
        DataContext = this;
    }

    private async void UpdateSchedules()
    {
        _loadingService.StartLoading();
        try
        {
            var response = await _httpClient.GetAsync("api/Admin/schedules");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var schedules = JsonConvert.DeserializeObject<List<Schedule>>(json) ?? [];

            Schedules = new ObservableCollection<Schedule>(schedules);
            OnPropertyChanged(nameof(Schedules));
        }
        finally
        {
            _loadingService.StopLoading();
        }
    }


    private void AddScheduleCommand(object sender, RoutedEventArgs e)
    {
        var window = new AddEditScheduleWindow(null);
        window.ShowDialog();
        UpdateSchedules();
    }

    private void OnEdit(Schedule schedule)
    {
        var window = new AddEditScheduleWindow(schedule);
        window.ShowDialog();
        UpdateSchedules();
    }

    private async void OnDelete(Schedule schedule)
    {
        if (schedule == null) return;
        var answer = new DialogWindow("Подтверждение", $"Вы уверены удалить расписание: {schedule.Name}?", "Да", "Нет")
            .ShowDialog();

        if (answer.HasValue && answer.Value)
        {
            _loadingService.StartLoading();
            try
            {
                var response = await _httpClient.DeleteAsync($"api/Admin/schedule/{schedule.Id}");
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    new DialogWindow("Ошибка", $"Ошибка удаления расписания: {errorContent}").ShowDialog();
                    _loadingService.StopLoading();
                    return;
                }

                UpdateSchedules();
            }
            catch (Exception ex)
            {
                new DialogWindow("Ошибка", $"Ошибка удаления расписания: {ex.Message}").ShowDialog();
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