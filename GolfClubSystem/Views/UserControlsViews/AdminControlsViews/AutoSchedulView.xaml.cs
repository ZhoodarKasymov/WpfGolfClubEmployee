using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GolfClubSystem.Data;
using GolfClubSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace GolfClubSystem.Views.UserControlsViews.AdminControlsViews;

public partial class AutoSchedulView : UserControl, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<NotifyJob> Jobes { get; set; }
    private readonly UnitOfWork _unitOfWork = new();
    
    public ICommand EditCommand { get; }
    public ICommand DeleteCommand { get; }
    
    public AutoSchedulView()
    {
        InitializeComponent();
        EditCommand = new RelayCommand<NotifyJob>(OnEdit);
        DeleteCommand = new RelayCommand<NotifyJob>(OnDelete);
        UpdateZones();
        DataContext = this;
    }

    private void UpdateZones()
    {
        var zones = _unitOfWork.NotifyJobRepository
            .GetAll(true)
            .Include(j => j.Zone)
            .Include(j => j.Organization)
            .Include(j => j.Shift)
            .ToList();
        Jobes = new ObservableCollection<NotifyJob>(zones);
        OnPropertyChanged(nameof(Jobes));
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
        var result = MessageBox.Show($"Вы уверены удалить авто уведомление на: {zone.Time}?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            await _unitOfWork.NotifyJobRepository.DeleteAsync(zone.Id);
            UpdateZones();
        }
    }
    
    
    private void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}