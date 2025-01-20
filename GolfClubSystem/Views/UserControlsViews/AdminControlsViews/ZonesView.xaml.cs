using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GolfClubSystem.Data;
using GolfClubSystem.Models;
using GolfClubSystem.Services;
using GolfClubSystem.Views.WorkersWindow;
using Microsoft.EntityFrameworkCore;

namespace GolfClubSystem.Views.UserControlsViews.AdminControlsViews;

public partial class ZonesView : UserControl, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<Zone> Zones { get; set; }
    private readonly UnitOfWork _unitOfWork = new();
    
    public ICommand EditCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand ShowCommand { get; }
    
    public ZonesView()
    {
        InitializeComponent();
        EditCommand = new RelayCommand<Zone>(OnEdit);
        DeleteCommand = new RelayCommand<Zone>(OnDelete);
        // ShowCommand = new RelayCommand<Zone>(OnShow);
        UpdateZones();
        DataContext = this;
    }

    private void UpdateZones()
    {
        var zones = _unitOfWork.ZoneRepository.GetAll()
            .Where(w => w.DeletedAt == null)
            .AsNoTracking()
            .ToList();
        Zones = new ObservableCollection<Zone>(zones);
        OnPropertyChanged(nameof(Zones));
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
        var result = MessageBox.Show($"Вы уверены удалить зону: {zone.Name}?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            var currentZone = _unitOfWork.ZoneRepository.GetAll().FirstOrDefault(o => o.Id == zone.Id);
            if (currentZone is not null)
            {
                currentZone.DeletedAt = DateTime.Now;
                await _unitOfWork.SaveAsync();
                UpdateZones();
            }
        }
    }
    
    // private void OnShow(Worker worker)
    // {
    //     var window = new AddEditZoneWindow(worker, false);
    //     window.ShowDialog();
    // }
    
    
    private void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}