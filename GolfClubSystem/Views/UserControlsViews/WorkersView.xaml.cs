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

namespace GolfClubSystem.Views.UserControlsViews;

public partial class WorkersView : UserControl, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<Worker> Workers { get; set; }
    private readonly UnitOfWork _unitOfWork = new();
    public List<Organization> Organizations { get; set; }
    public List<Zone> Zones { get; set; }

    private int _currentPage = 1;
    private const int PageSize = 10;

    public ICommand EditCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand ShowCommand { get; }
    
    private bool _isNextPageEnabled;
    public bool IsNextPageEnabled
    {
        get => _isNextPageEnabled;
        set
        {
            _isNextPageEnabled = value;
            OnPropertyChanged();
        }
    }

    private bool _isPreviousPageEnabled;
    public bool IsPreviousPageEnabled
    {
        get => _isPreviousPageEnabled;
        set
        {
            _isPreviousPageEnabled = value;
            OnPropertyChanged();
        }
    }

    public WorkersView()
    {
        InitializeComponent();
        EditCommand = new RelayCommand<Worker>(OnEdit);
        DeleteCommand = new RelayCommand<Worker>(OnDelete);
        ShowCommand = new RelayCommand<Worker>(OnShow);
        ApplyFilters();

        Organizations = _unitOfWork.OrganizationRepository.GetAll().Where(o => o.DeletedAt == null).ToList();
        Zones = _unitOfWork.ZoneRepository.GetAll().Where(o => o.DeletedAt == null).ToList();
        DataContext = this;
        Unloaded += WorkersView_Unloaded;
    }

    private void ApplyFilters()
    {
        var filteredHistories = _unitOfWork.WorkerRepository.GetAll()
            .Include(w => w.Zone)
            .Where(w => w.DeletedAt == null)
            .AsNoTracking();

        // Apply search filter
        if (!string.IsNullOrEmpty(SearchBox.Text))
        {
            filteredHistories = filteredHistories.Where(w => w.FullName.Contains(SearchBox.Text) 
                                                             || w.JobTitle.Contains(SearchBox.Text));
        }

        // Apply organization filter
        if (OrganizationFilter.SelectedItem != null)
        {
            var organization = OrganizationFilter.SelectedItem as Organization;
            filteredHistories = filteredHistories.Where(w => w.OrganizationId == organization!.Id);
        }

        // Apply status filter
        if (ZonesFilter.SelectedItem != null)
        {
            var zone = ZonesFilter.SelectedItem as Zone;
            filteredHistories = filteredHistories.Where(w => w.ZoneId == zone!.Id);
        }
        
        int totalWorkers = filteredHistories.Count();

        // Paginate the results
        var pagedHistories = filteredHistories
            .Skip((_currentPage - 1) * PageSize)
            .Take(PageSize)
            .ToList();

        // Update ObservableCollection
        Workers = new ObservableCollection<Worker>(pagedHistories);
        OnPropertyChanged(nameof(Workers));
        PageNumberText.Text = _currentPage.ToString();
        
        // Update button states
        IsPreviousPageEnabled = _currentPage > 1;
        IsNextPageEnabled = (_currentPage * PageSize) < totalWorkers; // Disable if no more items
    }

    private void PreviousPage_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPage > 1)
        {
            _currentPage--;
            ApplyFilters();
        }
    }

    private void NextPage_Click(object sender, RoutedEventArgs e)
    {
        _currentPage++;
        ApplyFilters();
    }

    private void AddEmployeeCommand(object sender, RoutedEventArgs e)
    {
        var window = new AddEditWorkerWindow(null);
        window.ShowDialog();
        ApplyFilters();
    }

    private void OnEdit(Worker worker)
    {
        var window = new AddEditWorkerWindow(worker);
        window.ShowDialog();
        ApplyFilters();
    }

    private async void OnDelete(Worker worker)
    {
        if (worker == null) return;
        var result = MessageBox.Show($"Вы уверены удалить работника: {worker.FullName}?", "Подтверждение",
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            var currentWorker = _unitOfWork.WorkerRepository.GetAll().Include(w => w.Zone)
                .FirstOrDefault(o => o.Id == worker.Id);

            if (currentWorker is not null)
            {
                var zones = _unitOfWork.ZoneRepository.GetAll().Where(z => z.DeletedAt == null).ToList();
                
                currentWorker.DeletedAt = DateTime.Now;
                await _unitOfWork.WorkerRepository.UpdateAsync(currentWorker);
                ApplyFilters();

                foreach (var zone in zones)
                {
                    var terminalService = new TerminalService(zone.Login, zone.Password);

                    await terminalService.DeleteUsersAsync(new UserInfoDeleteRequest
                    {
                        UserInfoDelCond = new UserInfoDelCond
                        {
                            EmployeeNoList =
                            [
                                new EmployeeNo
                                {
                                    EmployeeNoValue = currentWorker.Id.ToString()
                                }
                            ]
                        }
                    }, zone.EnterIp);

                    await terminalService.DeleteUsersAsync(new UserInfoDeleteRequest
                    {
                        UserInfoDelCond = new UserInfoDelCond
                        {
                            EmployeeNoList =
                            [
                                new EmployeeNo
                                {
                                    EmployeeNoValue = currentWorker.Id.ToString()
                                }
                            ]
                        }
                    }, zone.ExitIp);
                    
                    await terminalService.DeleteUsersAsync(new UserInfoDeleteRequest
                    {
                        UserInfoDelCond = new UserInfoDelCond
                        {
                            EmployeeNoList =
                            [
                                new EmployeeNo
                                {
                                    EmployeeNoValue = currentWorker.Id.ToString()
                                }
                            ]
                        }
                    }, zone.NotifyIp);
                }
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

    private void WorkersView_Unloaded(object sender, RoutedEventArgs e)
    {
        _unitOfWork.Dispose();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilters();
    }

    private void OrganizationFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyFilters();
    }

    private void ZonesFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyFilters();
    }

    private void ReloadButton_click(object sender, RoutedEventArgs e)
    {
        var parentWindow = Window.GetWindow(this);
        if (parentWindow is { DataContext: INotifyPropertyChanged vm })
        {
            var commandProperty = vm.GetType().GetProperty("NavigateCommand");
            if (commandProperty != null)
            {
                if (commandProperty.GetValue(vm) is ICommand navigateCommand && navigateCommand.CanExecute("Workers"))
                {
                    navigateCommand.Execute("Workers");
                }
            }
        }
    }
}