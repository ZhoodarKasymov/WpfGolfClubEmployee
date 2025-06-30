using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GolfClubSystem.Models;
using GolfClubSystem.Services;
using GolfClubSystem.Views.WorkersWindow;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Serilog;

namespace GolfClubSystem.Views.UserControlsViews;

class PagedWorkersResponse
{
    public int TotalCount { get; set; }
    public List<Worker> Workers { get; set; }
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
}

public partial class WorkersView : UserControl, INotifyPropertyChanged
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly LoadingService _loadingService;
    
    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<Worker> Workers { get; set; }
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
        _configuration = ((App)Application.Current)._configuration;
        var apiUrl = _configuration.GetSection("ApiUrl").Value
                     ?? throw new Exception("ApiUrl не прописан в конфигах!");
        _httpClient = new HttpClient { BaseAddress = new Uri(apiUrl) };
        _loadingService = LoadingService.Instance;
        
        InitializeComponent();
        EditCommand = new RelayCommand<Worker>(OnEdit);
        DeleteCommand = new RelayCommand<Worker>(OnDelete);
        ShowCommand = new RelayCommand<Worker>(OnShow);
        LoadInitialDataAsync();
        
        DataContext = this;
        Unloaded += WorkersView_Unloaded;
    }
    
    private async void LoadInitialDataAsync()
    {
        await LoadOrganizationsAsync();
        await LoadZonesAsync();
        ApplyFilters();
    }
    
    private async Task LoadOrganizationsAsync()
    {
        _loadingService.StartLoading();
        try
        {
            var response = await _httpClient.GetAsync("api/Hr/organizations");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            Organizations = JsonConvert.DeserializeObject<List<Organization>>(json) ?? [];
            OrganizationFilter.ItemsSource = Organizations;
            OnPropertyChanged(nameof(Organizations));
        }
        catch (Exception ex)
        {
            new DialogWindow("Ошибка", $"Ошибка загрузки организаций: {ex.Message}").ShowDialog();
            Organizations = [new Organization { Id = -1, Name = "Все" }];
        }
        finally
        {
            _loadingService.StopLoading();
        }
    }

    private async Task LoadZonesAsync()
    {
        _loadingService.StartLoading();
        try
        {
            var response = await _httpClient.GetAsync("api/Hr/zones");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            Zones = JsonConvert.DeserializeObject<List<Zone>>(json) ?? [];
            ZonesFilter.ItemsSource = Zones;
            OnPropertyChanged(nameof(Zones));
        }
        catch (Exception ex)
        {
            new DialogWindow("Ошибка", $"Ошибка загрузки зон: {ex.Message}").ShowDialog();
            Zones = [new Zone { Id = -1, Name = "Все" }];
        }
        finally
        {
            _loadingService.StopLoading();
        }
    }

    private async void ApplyFilters()
    {
        _loadingService.StartLoading();
        
        try
        {
            var queryParams = new List<string>();

            if (!string.IsNullOrEmpty(SearchBox.Text))
                queryParams.Add($"search={Uri.EscapeDataString(SearchBox.Text)}");

            if (OrganizationFilter.SelectedItem is Organization org && org.Id != -1)
                queryParams.Add($"organizationId={org.Id}");

            if (ZonesFilter.SelectedItem is Zone zone && zone.Id != -1)
                queryParams.Add($"zoneId={zone.Id}");
            
            queryParams.Add($"pageNumber={_currentPage}");
            queryParams.Add($"pageSize={PageSize}");

            var queryString = string.Join("&", queryParams);
            var response = await _httpClient.GetAsync($"api/Hr/workers-paged?{queryString}");
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"API Error: {response.StatusCode}, Details: {errorContent}");
            }

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonConvert.DeserializeObject<PagedWorkersResponse>(json);

            Workers = new ObservableCollection<Worker>(data.Workers);
            IsPreviousPageEnabled = _currentPage > 1;
            IsNextPageEnabled = (_currentPage * PageSize) < data.TotalCount;
            PageNumberText.Text = _currentPage.ToString();

            OnPropertyChanged(nameof(Workers));
        }
        catch (Exception ex)
        {
            new DialogWindow("Ошибка", $"Ошибка загрузки работников: {ex.Message}").ShowDialog();
            Workers = new ObservableCollection<Worker>();
            OnPropertyChanged(nameof(Workers));
            Log.Error($"Ошибка загрузки работников: {ex.Message}", ex);
        }
        finally
        {
            _loadingService.StopLoading();
        }
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
        var answer = new DialogWindow("Подтверждение", $"Вы уверены удалить работника: {worker.FullName}?", "Да", "Нет").ShowDialog();

        if (answer.HasValue && answer.Value)
        {
            _loadingService.StartLoading();
            try
            {
                // Delete worker via API
                var deleteResponse = await _httpClient.DeleteAsync($"api/Hr/workers/{worker.Id}");
                if (!deleteResponse.IsSuccessStatusCode)
                {
                    var errorContent = await deleteResponse.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"API Error: {deleteResponse.StatusCode}, Details: {errorContent}");
                }

                ApplyFilters();
            }
            catch (Exception ex)
            {
                new DialogWindow("Ошибка", $"Ошибка удаления работника: {ex.Message}").ShowDialog();
                Log.Error($"Ошибка удаления работника: {ex.Message}", ex);
            }
            finally
            {
                _loadingService.StopLoading();
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
        _httpClient.Dispose();
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