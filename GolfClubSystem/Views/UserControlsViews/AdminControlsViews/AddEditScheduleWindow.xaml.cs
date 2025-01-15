using System.Windows;
using GolfClubSystem.Data;
using GolfClubSystem.Models;
using GolfClubSystem.Views.WorkersWindow;

namespace GolfClubSystem.Views.UserControlsViews.AdminControlsViews;

public partial class AddEditScheduleWindow : Window
{
    public Schedule Schedule { get; set; }
    public WorkerType ScheduleType { get; set; }
    public bool IsEnable { get; set; }


    private readonly UnitOfWork _unitOfWork = new();

    public AddEditScheduleWindow(Schedule? schedule, bool isEnable = true)
    {
        IsEnable = isEnable;
        InitializeComponent();

        if (schedule is not null)
        {
            Schedule = schedule;
            ScheduleType = WorkerType.Edit;
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
                break;
            }
            case WorkerType.Edit:
            {
                var currentSchedule = _unitOfWork.ScheduleRepository.GetAllAsync()
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
                    currentSchedule.PermissionToLateMin = Schedule.PermissionToLateMin;
                    currentSchedule.PermissionToGoEarlyMin = Schedule.PermissionToGoEarlyMin;
                    currentSchedule.Scheduledays = Schedule.Scheduledays;
                    await _unitOfWork.ScheduleRepository.UpdateAsync(currentSchedule);
                }

                break;
            }
        }

        await _unitOfWork.SaveAsync();
        Close();
    }
}