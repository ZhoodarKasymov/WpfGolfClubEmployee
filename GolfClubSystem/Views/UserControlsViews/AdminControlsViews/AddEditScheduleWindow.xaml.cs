using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using GolfClubSystem.Data;
using GolfClubSystem.Models;
using GolfClubSystem.Views.WorkersWindow;
using Microsoft.EntityFrameworkCore;

namespace GolfClubSystem.Views.UserControlsViews.AdminControlsViews;

public partial class AddEditScheduleWindow : Window
{
    public Schedule Schedule { get; set; }
    public WorkerType ScheduleType { get; set; }
    public bool IsEnable { get; set; }
    public ObservableCollection<DateTime> SelectedDates { get; set; } = new();
    
    private readonly UnitOfWork _unitOfWork = new();
    
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _unitOfWork.Dispose();
        SelectedDates.Clear();
    }

    public AddEditScheduleWindow(Schedule? schedule, bool isEnable = true)
    {
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
        switch (ScheduleType)
        {
            case WorkerType.Add:
            {
                await _unitOfWork.ScheduleRepository.AddAsync(Schedule);
                
                if (SelectedDates.Any())
                {
                    var holidays = SelectedDates.Select(sd => new Holiday
                    {
                        ScheduleId = Schedule.Id,
                        HolidayDate = sd
                    }).ToList();
                    
                    Schedule.Holidays = holidays;
                    await _unitOfWork.SaveAsync();
                }
                
                break;
            }
            case WorkerType.Edit:
            {
                var currentSchedule = _unitOfWork.ScheduleRepository.GetAll()
                    .Include(s => s.Holidays)
                    .Where(w => w.DeletedAt == null)
                    .FirstOrDefault(w => w.Id == Schedule.Id);

                if (currentSchedule is not null)
                {
                    currentSchedule.Name = Schedule.Name;
                    currentSchedule.BreakStart = Schedule.BreakStart;
                    currentSchedule.BreakEnd = Schedule.BreakEnd;
                    currentSchedule.PermissibleEarlyLeaveStart = Schedule.PermissibleEarlyLeaveStart;
                    currentSchedule.PermissibleEarlyLeaveEnd = Schedule.PermissibleEarlyLeaveEnd;
                    currentSchedule.PermissibleLateTimeStart = Schedule.PermissibleLateTimeStart;
                    currentSchedule.PermissibleLateTimeEnd = Schedule.PermissibleLateTimeEnd;
                    currentSchedule.PermissionToLateTime = Schedule.PermissionToLateTime;
                    currentSchedule.Scheduledays = Schedule.Scheduledays;
                    
                    if (SelectedDates.Any())
                    {
                        var holidays = SelectedDates.Select(sd => new Holiday
                        {
                            ScheduleId = Schedule.Id,
                            HolidayDate = sd
                        }).ToList();
                        
                        currentSchedule.Holidays.Clear();
                        currentSchedule.Holidays = holidays;
                    }
                    else
                    {
                        currentSchedule.Holidays.Clear();
                    }
                    
                    await _unitOfWork.ScheduleRepository.UpdateAsync(currentSchedule);
                }

                break;
            }
        }

        Close();
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