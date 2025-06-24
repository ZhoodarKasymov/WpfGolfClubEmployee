using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using GolfClubSystem.Models;
using GolfClubSystem.Services;
using GolfClubSystem.Views.WorkersWindow;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Serilog;

namespace GolfClubSystem.Views.UserControlsViews.AdminControlsViews;

public partial class AddEditScheduleWindow : Window
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly LoadingService _loadingService;

    public Schedule Schedule { get; set; }
    public WorkerType ScheduleType { get; set; }
    public bool IsEnable { get; set; }
    public ObservableCollection<DateTime> SelectedDates { get; set; } = new();

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _httpClient.Dispose();
        SelectedDates.Clear();
    }

    public AddEditScheduleWindow(Schedule? schedule, bool isEnable = true)
    {
        _configuration = ((App)Application.Current)._configuration;
        var apiUrl = _configuration.GetSection("ApiUrl").Value
                     ?? throw new Exception("ApiUrl не прописан в конфигах!");
        _httpClient = new HttpClient { BaseAddress = new Uri(apiUrl) };
        _loadingService = LoadingService.Instance;

        IsEnable = isEnable;
        InitializeComponent();

        if (schedule is not null)
        {
            Schedule = schedule;
            ScheduleType = WorkerType.Edit;
            if (schedule.Holidays.Count != 0)
            {
                var holidays = schedule.Holidays.Select(x => x.HolidayDate);
                SelectedDates = new ObservableCollection<DateTime>(holidays);
            }
        }
        else
        {
            Schedule = new Schedule()
            {
                Scheduledays = new List<Scheduleday>
                {
                    new() { DayOfWeek = "Понедельник" },
                    new() { DayOfWeek = "Вторник" },
                    new() { DayOfWeek = "Среда" },
                    new() { DayOfWeek = "Четверг" },
                    new() { DayOfWeek = "Пятница" },
                    new() { DayOfWeek = "Суббота" },
                    new() { DayOfWeek = "Воскресенье" }
                }
            };
            ScheduleType = WorkerType.Add;
        }

        DataContext = this;
    }

    private async void ButtonBase_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Schedule.Name))
        {
            new DialogWindow("Ошибка", "Название расписания обязательно.").ShowDialog();
            return;
        }

        _loadingService.StartLoading();
        try
        {
            var holidays = SelectedDates.Select(sd => new Holiday
            {
                ScheduleId = Schedule.Id,
                HolidayDate = sd,
                Schedule = null
            }).ToList();

            Schedule.Holidays = holidays;

            var content = new StringContent(JsonConvert.SerializeObject(Schedule), Encoding.UTF8, "application/json");
            HttpResponseMessage response;

            if (ScheduleType == WorkerType.Add)
            {
                response = await _httpClient.PostAsync("api/Admin/schedules", content);
            }
            else
            {
                response = await _httpClient.PutAsync($"api/Admin/schedules/{Schedule.Id}", content);
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                var error = JsonConvert.DeserializeObject<dynamic>(errorContent);
                new DialogWindow("Ошибка", error?.Message).ShowDialog();
                Log.Error($"API error: {error.Message}");
                _loadingService.StopLoading();
                return;
            }

            Close();
        }
        catch (Exception ex)
        {
            new DialogWindow("Ошибка", $"Произошла ошибка: {ex.Message}").ShowDialog();
            Log.Error(ex, "Error in schedule operation");
        }
        finally
        {
            _loadingService.StopLoading();
        }
    }

    private void Calendar_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
    {
        foreach (var date in e.AddedItems)
        {
            if (date is DateTime newDate && !SelectedDates.Contains(newDate))
                SelectedDates.Add(newDate);
        }
    }

    private void HolidayButton_Click(object sender, RoutedEventArgs e)
    {
        // Open the popup when the button is clicked
        HolidayPopup.IsOpen = true;
    }

    private void SaveHolidayDates_Click(object sender, RoutedEventArgs e)
    {
        // Save the selected dates
        HolidayPopup.IsOpen = false; // Close the popup after saving
    }

    private void ClearHolidayDates_Click(object sender, RoutedEventArgs e)
    {
        SelectedDates.Clear();
        MultiCalendar.SelectedDates.Clear();
    }
}