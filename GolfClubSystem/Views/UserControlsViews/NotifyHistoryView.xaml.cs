using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GolfClubSystem.Models;
using GolfClubSystem.Services;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace GolfClubSystem.Views.UserControlsViews
{
     class PagedNotifyHistoryResponse
    {
        public int TotalCount { get; set; }
        public List<NotifyHistory> Histories { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
    }
    
    public partial class NotifyHistoryView : UserControl, INotifyPropertyChanged
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly LoadingService _loadingService;
        
        public ObservableCollection<NotifyHistory> Histories { get; set; }
        public List<Organization> Organizations { get; set; }

        public List<Item> Statuses { get; set; } = new()
        {
            new() { Name = "Все", Id = -1 },
            new() { Name = "Отметка", Id = 1 },
            new() { Name = "Запрос", Id = 2 }
        };

        private int _currentPage = 1;
        private const int PageSize = 10;
        private DateTime? _startDate;
        private DateTime? _endDate;
        
        private bool _isNextPageEnabled;
        public bool IsNextPageEnabled
        {
            get => _isNextPageEnabled;
            set
            {
                _isNextPageEnabled = value;
                OnPropertyChanged(nameof(IsNextPageEnabled));
            }
        }

        private bool _isPreviousPageEnabled;
        public bool IsPreviousPageEnabled
        {
            get => _isPreviousPageEnabled;
            set
            {
                _isPreviousPageEnabled = value;
                OnPropertyChanged(nameof(IsPreviousPageEnabled));
            }
        }

        public NotifyHistoryView()
        {
            _configuration = ((App)Application.Current)._configuration;
            var apiUrl = _configuration.GetSection("ApiUrl").Value
                         ?? throw new Exception("ApiUrl не прописан в конфигах!");
            _httpClient = new HttpClient { BaseAddress = new Uri(apiUrl) };
            _loadingService = LoadingService.Instance;
            
            InitializeComponent();
            TodayFilter.Background = new SolidColorBrush(Color.FromRgb(46, 87, 230));
            TodayFilter.Foreground = Brushes.White;
            ApplyTodayFilter();
            LoadOrganizationsAsync();
            
            DataContext = this;
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

        private async void ApplyFilters()
        {
            _loadingService.StartLoading();
            try
            {
                var queryParams = new List<string>();
                
                if (_startDate.HasValue)
                    queryParams.Add($"startDate={Uri.EscapeDataString(_startDate.Value.ToString("yyyy-MM-ddTHH:mm:ss"))}");
                if (_endDate.HasValue)
                    queryParams.Add($"endDate={Uri.EscapeDataString(_endDate.Value.ToString("yyyy-MM-ddTHH:mm:ss"))}");
                if (OrganizationFilter.SelectedItem is Organization org && org.Id != -1)
                    queryParams.Add($"organizationId={org.Id}");
                if (StatusFilter.SelectedItem is Item status && status.Id != -1)
                    queryParams.Add($"statusId={status.Id}");
                if (!string.IsNullOrEmpty(SearchBox.Text))
                    queryParams.Add($"search={Uri.EscapeDataString(SearchBox.Text)}");
                
                queryParams.Add($"pageNumber={_currentPage}");
                queryParams.Add($"pageSize={PageSize}");

                var queryString = string.Join("&", queryParams);
                var response = await _httpClient.GetAsync($"api/Hr/notify-history-paged?{queryString}");
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                var data = JsonConvert.DeserializeObject<PagedNotifyHistoryResponse>(json);

                Histories = new ObservableCollection<NotifyHistory>(data.Histories);
                IsPreviousPageEnabled = _currentPage > 1;
                IsNextPageEnabled = (_currentPage * PageSize) < data.TotalCount;
                PageNumberText.Text = _currentPage.ToString();

                OnPropertyChanged(nameof(Histories));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки истории: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                Histories = new ObservableCollection<NotifyHistory>();
                OnPropertyChanged(nameof(Histories));
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
            ApplyFilters();
        }

        private void ApplyWeekFilter()
        {
            _endDate = DateTime.Today.AddDays(1);
            _startDate = _endDate.Value.AddDays(-6);
            ApplyFilters();
        }

        private void ApplyMonthFilter()
        {
            _endDate = DateTime.Today.AddDays(1);
            _startDate = _endDate.Value.AddMonths(-1).AddDays(-1);
            ApplyFilters();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void OrganizationFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void StatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
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
                    if (commandProperty.GetValue(vm) is ICommand navigateCommand && navigateCommand.CanExecute("NotifyHistory"))
                    {
                        navigateCommand.Execute("NotifyHistory");
                    }
                }
            }
        }
    }
}