using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GolfClubSystem.Data;
using GolfClubSystem.Models;
using GolfClubSystem.Views.UserControlsViews.AdminControlsViews;
using GolfClubSystem.Views.WorkersWindow;
using Microsoft.EntityFrameworkCore;

namespace GolfClubSystem.Views.UserControlsViews;

public partial class WorkersView : UserControl, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<Worker> Workers { get; set; }
    private readonly UnitOfWork _unitOfWork = new();
    
    public ICommand EditCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand ShowCommand { get; }
    
    public WorkersView()
    {
        InitializeComponent();
        EditCommand = new RelayCommand<Worker>(OnEdit);
        DeleteCommand = new RelayCommand<Worker>(OnDelete);
        ShowCommand = new RelayCommand<Worker>(OnShow);
        UpdateWorkers();
        DataContext = this;
    }

    private void UpdateWorkers()
    {
        var workers = _unitOfWork.WorkerRepository.GetAllAsync()
            .Include(w => w.Zone)
            .Where(w => w.DeletedAt == null)
            .AsNoTracking()
            .ToList();
        Workers = new ObservableCollection<Worker>(workers);
        OnPropertyChanged(nameof(Workers));
    }


    private void AddEmployeeCommand(object sender, RoutedEventArgs e)
    {
        var window = new AddEditWorkerWindow(null);
        window.ShowDialog();
        UpdateWorkers();
    }
    
    private void OnEdit(Worker worker)
    {
        var window = new AddEditWorkerWindow(worker);
        window.ShowDialog();
        UpdateWorkers();
    }

    private async void OnDelete(Worker worker)
    {
        if (worker == null) return;
        var result = MessageBox.Show($"Вы уверены удалить работника: {worker.FullName}?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            var currentWorker = _unitOfWork.WorkerRepository.GetAllAsync().FirstOrDefault(o => o.Id == worker.Id);
            if (currentWorker is not null)
            {
                currentWorker.DeletedAt = DateTime.Now;
                await _unitOfWork.WorkerRepository.UpdateAsync(currentWorker);
                await _unitOfWork.SaveAsync();
                UpdateWorkers();
            }
        }
    }
    
    private void OnShow(Worker worker)
    {
        var window = new AddEditWorkerWindow(worker, false);
        window.ShowDialog();
    }
    
    
    private void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}