using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GolfClubSystem.Data;
using GolfClubSystem.Models;
using GolfClubSystem.Views.WorkersWindow;
using Microsoft.EntityFrameworkCore;

namespace GolfClubSystem.Views.UserControlsViews.AdminControlsViews;

public partial class SchedulerView : UserControl, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<Schedule> Schedules { get; set; }
    private readonly UnitOfWork _unitOfWork = new();
    
    public ICommand EditCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand ShowCommand { get; }
    
    public SchedulerView()
    {
        InitializeComponent();
        EditCommand = new RelayCommand<Schedule>(OnEdit);
        DeleteCommand = new RelayCommand<Schedule>(OnDelete);
        UpdateSchedules();
        DataContext = this;
    }

    private void UpdateSchedules()
    {
        var schedules = _unitOfWork.ScheduleRepository.GetAll()
            .Include(sh => sh.Scheduledays)
            .Where(w => w.DeletedAt == null)
            .AsNoTracking()
            .ToList();
        
        Schedules = new ObservableCollection<Schedule>(schedules);
        OnPropertyChanged(nameof(Schedules));
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
        var result = MessageBox.Show($"Вы уверены удалить расписание: {schedule.Name}?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            var currentSchedule = _unitOfWork.ScheduleRepository.GetAll()
                .FirstOrDefault(o => o.Id == schedule.Id);
            if (currentSchedule is not null)
            {
                currentSchedule.DeletedAt = DateTime.Now;
                await _unitOfWork.SaveAsync();
                UpdateSchedules();
            }
        }
    }
    
    private void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}