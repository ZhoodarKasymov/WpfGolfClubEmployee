using System.ComponentModel;
using System.Globalization;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GolfClubSystem.Models;
using GolfClubSystem.Services;
using GolfClubSystem.Views.MainWindows;
using LiveCharts;
using LiveCharts.Wpf;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Serilog;

namespace GolfClubSystem.Views.UserControlsViews;

public partial class MainView : UserControl, INotifyPropertyChanged
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly LoadingService _loadingService;
    public List<Organization> Organizations { get; set; }

    public int InTime { get; set; }
    public int Late { get; set; }
    public int VeryLate { get; set; }
    public int EarlyLeave { get; set; }
    public int NoWorkers { get; set; }

    public string DonutPercent { get; set; }
    public string DonutTrackedCount { get; set; }
    public string DonutNotifyCount { get; set; }

    private DateTime? _startDate;
    private DateTime? _endDate;

    public MainView()
    {
        _configuration = ((App)Application.Current)._configuration;
        var apiUrl = _configuration.GetSection("ApiUrl").Value
                     ?? throw new Exception("ApiUrl не прописан в конфигах!");
        _httpClient = new HttpClient { BaseAddress = new Uri(apiUrl) };
        _loadingService = LoadingService.Instance;
        
        InitializeComponent();
        DataContext = this;
        
        TodayFilter.Background = new SolidColorBrush(Color.FromRgb(46, 87, 230));
        TodayFilter.Foreground = Brushes.White;
        ApplyTodayFilter();

        LoadOrganizationsAsync();
        Unloaded += WorkersView_Unloaded;
    }
    
    private async void LoadOrganizationsAsync()
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
            MessageBox.Show($"Ошибка загрузки организаций: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _loadingService.StopLoading();
        }
    }

    private async void UpdateHistory()
    {
        _loadingService.StartLoading();

        try
        {
            var queryParams = new List<string>();
            
            if (_startDate.HasValue)
                queryParams.Add($"startDate={Uri.EscapeDataString(_startDate.Value.ToString("o"))}");
            if (_endDate.HasValue)
                queryParams.Add($"endDate={Uri.EscapeDataString(_endDate.Value.ToString("o"))}");
            if (OrganizationFilter.SelectedItem is Organization org && org.Id != -1)
                queryParams.Add($"organizationId={org.Id}");
            
            var queryString = string.Join("&", queryParams);
            var response = await _httpClient.GetAsync($"api/Hr/dashboard{(queryString.Length > 0 ? "?" + queryString : "")}");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var data = JsonConvert.DeserializeObject<DashboardResponse>(json);
            
            DonutPercent = data.DonutPercent;
            DonutTrackedCount = data.DonutTrackedCount;
            DonutNotifyCount = data.DonutNotifyCount;
            
            PieChart.Series =
            [
                new PieSeries
                {
                    Title = "Отметились",
                    Values = new ChartValues<double> { data.PieChartData[0].Value },
                    Fill = new SolidColorBrush(Color.FromRgb(0, 190, 85)),
                    DataLabels = true
                },

                new PieSeries
                {
                    Title = "Запросов",
                    Values = new ChartValues<double> { data.PieChartData[1].Value },
                    Fill = new SolidColorBrush(Color.FromRgb(238, 69, 69)),
                    DataLabels = true
                }
            ];
            
            var chartValues = new ChartValues<double>();
            var labels = new List<string>();
            
            foreach (var barData in data.BarChartData)
            {
                chartValues.Add(barData.Count);
                labels.Add(barData.ZoneName);
            }
            
            BarChart.Series =
            [
                new ColumnSeries
                {
                    Title = "Количество сотрудников",
                    Values = chartValues,
                    Fill = new SolidColorBrush(Color.FromRgb(121, 135, 255))
                }
            ];
            
            // Установка подписей по осям
            BarChart.AxisX[0].Labels = labels;
            BarChart.AxisY[0].LabelFormatter = value => value.ToString("N0", CultureInfo.InvariantCulture);
            
            InTime = data.InTime;
            VeryLate = data.VeryLate;
            Late = data.Late;
            EarlyLeave = data.EarlyLeave;
            NoWorkers = data.NoWorkers;
            
            OnPropertyChanged(nameof(DonutPercent));
            OnPropertyChanged(nameof(DonutTrackedCount));
            OnPropertyChanged(nameof(DonutNotifyCount));
            OnPropertyChanged(nameof(PieChart));
            OnPropertyChanged(nameof(BarChart));
            OnPropertyChanged(nameof(InTime));
            OnPropertyChanged(nameof(VeryLate));
            OnPropertyChanged(nameof(Late));
            OnPropertyChanged(nameof(EarlyLeave));
            OnPropertyChanged(nameof(NoWorkers));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка загрузки данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            Log.Error("Ошибка в главной: UpdateHistory", ex);
        }
        finally
        {
            _loadingService.StopLoading();
        }
    }

    private void FilterButton_Click(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;

        // Reset all button backgrounds to gray
        TodayFilter.Background = Brushes.White;
        TodayFilter.Foreground = Brushes.Black;
        WeekFilter.Background = Brushes.White;
        WeekFilter.Foreground = Brushes.Black;
        MonthFilter.Background = Brushes.White;
        MonthFilter.Foreground = Brushes.Black;

        // Set the clicked button's background to blue
        if (button == TodayFilter)
        {
            TodayFilter.Background = new SolidColorBrush(Color.FromRgb(46, 87, 230));
            TodayFilter.Foreground = Brushes.White;
            ApplyTodayFilter();
        }
        else if (button == WeekFilter)
        {
            WeekFilter.Background = new SolidColorBrush(Color.FromRgb(46, 87, 230));
            WeekFilter.Foreground = Brushes.White;
            ApplyWeekFilter();
        }
        else if (button == MonthFilter)
        {
            MonthFilter.Background = new SolidColorBrush(Color.FromRgb(46, 87, 230));
            MonthFilter.Foreground = Brushes.White;
            ApplyMonthFilter();
        }
    }

    private void ApplyTodayFilter()
    {
        _startDate = DateTime.Today;
        _endDate = DateTime.Today.AddDays(1);
        UpdateHistory();
    }

    private void ApplyWeekFilter()
    {
        _endDate = DateTime.Today.AddDays(1);
        _startDate = _endDate.Value.AddDays(-6);
        UpdateHistory();
    }

    private void ApplyMonthFilter()
    {
        _endDate = DateTime.Today.AddDays(1);
        _startDate = _endDate.Value.AddMonths(-1).AddDays(-1);
        UpdateHistory();
    }

    private void OrganizationFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateHistory();
    }

    private void DatePicker_SelectedDateChanged(object sender, RoutedEventArgs e)
    {
        if (StartDatePicker?.SelectedDate is not null && EndDatePicker?.SelectedDate != null)
        {
            _startDate = StartDatePicker.SelectedDate.Value;
            _endDate = EndDatePicker.SelectedDate.Value;

            // Validate date range (must be within 3 months)
            if ((_endDate.Value - _startDate.Value).TotalDays > 90)
            {
                MessageBox.Show("Диапазон дат не должен превышать 3 месяца.");
                return;
            }

            UpdateHistory();
        }
    }

    private void SendNotify_OnClick(object sender, RoutedEventArgs e)
    {
        var window = new SendNotifyWindow();
        window.ShowDialog();
    }
    
    private void Export_OnClick(object sender, RoutedEventArgs e)
    {
        var window = new ExportWindow();
        window.ShowDialog();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void WorkersView_Unloaded(object sender, RoutedEventArgs e)
    {
        _httpClient.Dispose();
    }

    private void ReloadButton_click(object sender, RoutedEventArgs e)
    {
        var parentWindow = Window.GetWindow(this);
        if (parentWindow is { DataContext: INotifyPropertyChanged vm })
        {
            var commandProperty = vm.GetType().GetProperty("NavigateCommand");
            if (commandProperty != null)
            {
                if (commandProperty.GetValue(vm) is ICommand navigateCommand && navigateCommand.CanExecute("Main"))
                {
                    navigateCommand.Execute("Main");
                }
            }
        }
    }
    
    private void RedirectToHistoryOfNotify_click(object sender, RoutedEventArgs e)
    {
        var parentWindow = Window.GetWindow(this);
        if (parentWindow is { DataContext: INotifyPropertyChanged vm })
        {
            var commandProperty = vm.GetType().GetProperty("NavigateCommand");
            if (commandProperty != null)
            {
                if (commandProperty.GetValue(vm) is ICommand navigateCommand && navigateCommand.CanExecute("NotifyHistory"))
                {
                    navigateCommand.Execute("NotifyHistory");
                }
            }
        }
    }

    private class DashboardResponse
    {
        public string DonutPercent { get; set; }
        public string DonutTrackedCount { get; set; }
        public string DonutNotifyCount { get; set; }
        public PieChartData[] PieChartData { get; set; }
        public BarChartData[] BarChartData { get; set; }
        public int InTime { get; set; }
        public int VeryLate { get; set; }
        public int Late { get; set; }
        public int EarlyLeave { get; set; }
        public int NoWorkers { get; set; }
    }

    private class PieChartData
    {
        public string Title { get; set; }
        public double Value { get; set; }
        public string Color { get; set; }
    }

    private class BarChartData
    {
        public int Count { get; set; }
        public string ZoneName { get; set; }
    }
}