using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GolfClubSystem.Models;
using GolfClubSystem.Services;
using GolfClubSystem.Views.UserControlsViews;
using Microsoft.Extensions.Configuration;
using Microsoft.Win32;
using Newtonsoft.Json;
using Serilog;

namespace GolfClubSystem.Views.MainWindows
{
    public partial class ExportWindow : Window, INotifyPropertyChanged
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly LoadingService _loadingService;

        private readonly ExcelReports _excelReports;
        public ObservableCollection<Employeehistory> Histories { get; set; }
        public List<Organization> Organizations { get; set; }
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

        public ExportWindow()
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
            _excelReports = new ExcelReports();

            DataContext = this;
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
                new DialogWindow("Ошибка", $"Ошибка загрузки организаций: {ex.Message}").ShowDialog();
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
                    queryParams.Add(
                        $"startDate={Uri.EscapeDataString(_startDate.Value.ToString("yyyy-MM-ddTHH:mm:ss"))}");
                if (_endDate.HasValue)
                    queryParams.Add($"endDate={Uri.EscapeDataString(_endDate.Value.ToString("yyyy-MM-ddTHH:mm:ss"))}");
                if (OrganizationFilter.SelectedItem is Organization org && org.Id != -1)
                    queryParams.Add($"organizationId={org.Id}");
                if (!string.IsNullOrEmpty(SearchBox.Text))
                    queryParams.Add($"search={Uri.EscapeDataString(SearchBox.Text)}");

                queryParams.Add($"pageNumber={_currentPage}");
                queryParams.Add($"pageSize={PageSize}");

                var queryString = string.Join("&", queryParams);
                var response = await _httpClient.GetAsync($"api/Hr/history-paged?{queryString}");
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                var data = JsonConvert.DeserializeObject<PagedHistoryResponse>(json);

                // Materialize the query to a list to allow complex lambda
                var historyList = data?.Histories ?? [];
                int total = data?.TotalCount ?? 0;

                // Paginate the results and calculate NoWorkCount per worker
                var pagedHistories = historyList
                    .GroupBy(x => x.WorkerId)
                    .Select(x =>
                    {
                        var worker = x.First().Worker;
                        var history = x.ToList();
                        int noWorkCount = 0;

                        if (_startDate.HasValue && _endDate.HasValue)
                        {
                            var daysInRange = (_endDate.Value - _startDate.Value).Days;
                            bool isTodayFilter = daysInRange <= 1;

                            if (!isTodayFilter) // Only compute for Week or Month
                            {
                                var workerHistoryByDay = history
                                    .GroupBy(h => h.ArrivalTime.Date)
                                    .ToDictionary(g => g.Key, g => g.Select(h => h.WorkerId).Distinct().ToList());

                                for (var date = _startDate.Value.Date;
                                     date < _endDate.Value.Date;
                                     date = date.AddDays(1))
                                {
                                    var startOfDay = date;
                                    var endOfDay = date.AddHours(23).AddMinutes(59).AddSeconds(59);
                                    var workersInHistory = workerHistoryByDay.ContainsKey(date)
                                        ? workerHistoryByDay[date]
                                        : new List<int>();

                                    var shift = worker.Schedule;
                                    if (shift == null || shift.Scheduledays == null) continue;

                                    var shiftDay = shift.Scheduledays.FirstOrDefault(sd =>
                                        sd.DayOfWeek.ToLower() ==
                                        date.ToString("dddd", new CultureInfo("ru-RU")).ToLower());
                                    if (shiftDay == null || !shiftDay.IsSelected) continue;

                                    if (shift.Holidays?.Any(h => h.HolidayDate.Date == date.Date) ?? false) continue;

                                    TimeSpan startTimeSpan, endTimeSpan;
                                    DateTime shiftStart, shiftEnd;
                                    if (shiftDay.WorkStart > shiftDay.WorkEnd) // Night shift
                                    {
                                        var nightStart = shiftDay.WorkStart.Value;
                                        var nightEnd = shiftDay.WorkEnd.Value;

                                        startTimeSpan = nightStart.ToTimeSpan();
                                        endTimeSpan = nightEnd.ToTimeSpan();

                                        var now = TimeOnly.FromDateTime(DateTime.Now);
                                        if (now >= nightStart)
                                        {
                                            shiftStart = date + startTimeSpan;
                                            shiftEnd = date.AddDays(1) + endTimeSpan;
                                        }
                                        else
                                        {
                                            shiftStart = date.AddDays(-1) + startTimeSpan;
                                            shiftEnd = date + endTimeSpan;
                                        }
                                    }
                                    else // Day shift
                                    {
                                        startTimeSpan = shiftDay.WorkStart.Value.ToTimeSpan();
                                        endTimeSpan = shiftDay.WorkEnd.Value.ToTimeSpan();
                                        shiftStart = date + startTimeSpan;
                                        shiftEnd = date + endTimeSpan;
                                    }

                                    if (shiftStart <= endOfDay && shiftEnd >= startOfDay &&
                                        !workersInHistory.Contains(worker.Id))
                                    {
                                        noWorkCount++;
                                    }
                                }
                            }
                        }

                        return new Employeehistory
                        {
                            WorkHours = x.Sum(g => g.WorkHours),
                            WorkerId = x.Key,
                            Worker = worker,
                            LateHours = x.Count(h => h.Status == 3),
                            EarlyHours = x.Count(h => h.Status == 4),
                            NoWorkCount = noWorkCount
                        };
                    })
                    .Skip((_currentPage - 1) * PageSize)
                    .Take(PageSize)
                    .ToList();

                // Update ObservableCollection
                Histories = new ObservableCollection<Employeehistory>(pagedHistories);
                OnPropertyChanged(nameof(Histories));
                PageNumberText.Text = _currentPage.ToString();

                // Update button states
                IsPreviousPageEnabled = _currentPage > 1;
                IsNextPageEnabled = (_currentPage * PageSize) < total;
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

        private void DatePicker_SelectedDateChanged(object sender, RoutedEventArgs e)
        {
            if (StartDatePicker?.SelectedDate is not null && EndDatePicker?.SelectedDate != null)
            {
                _startDate = StartDatePicker.SelectedDate.Value;
                _endDate = EndDatePicker.SelectedDate.Value;

                // Validate date range (must be within 3 months)
                if ((_endDate.Value - _startDate.Value).TotalDays > 31)
                {
                    new DialogWindow("Ошибка", "Диапазон дат не должен превышать 1 месяца.").ShowDialog();
                    _endDate = null;
                    EndDatePicker.SelectedDate = null;
                    return;
                }

                ApplyFilters();
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void OrganizationFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
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

        protected override void OnClosed(EventArgs e)
        {
            _httpClient.Dispose();
            base.OnClosed(e);
        }

        private async void Tabel_Generate(object sender, RoutedEventArgs e)
        {
            _loadingService.StartLoading();

            try
            {
                // Dynamic date range
                var startDate = _startDate.Value;
                var endDate = _endDate.Value;

                var queryParams = new List<string>();

                if (_startDate.HasValue)
                    queryParams.Add(
                        $"startDate={Uri.EscapeDataString(_startDate.Value.ToString("yyyy-MM-ddTHH:mm:ss"))}");
                if (_endDate.HasValue)
                    queryParams.Add($"endDate={Uri.EscapeDataString(_endDate.Value.ToString("yyyy-MM-ddTHH:mm:ss"))}");
                if (OrganizationFilter.SelectedItem is Organization org && org.Id != -1)
                    queryParams.Add($"organizationId={org.Id}");
                if (!string.IsNullOrEmpty(SearchBox.Text))
                    queryParams.Add($"search={Uri.EscapeDataString(SearchBox.Text)}");

                queryParams.Add($"pageNumber=1");
                queryParams.Add($"pageSize=2000");

                var queryString = string.Join("&", queryParams);
                var response = await _httpClient.GetAsync($"api/Hr/history-paged?{queryString}");
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                var data = JsonConvert.DeserializeObject<PagedHistoryResponse>(json);

                // Materialize the query to a list to allow complex lambda
                var historyList = (data?.Histories ?? []).GroupBy(h => h.WorkerId).ToList();
                int daysCount = (endDate - startDate).Days;
                var workers = new List<ExcelReports.WorkerTimesheet>();

                foreach (var employeehistory in historyList)
                {
                    var workerE = employeehistory.First().Worker;

                    var worker = new ExcelReports.WorkerTimesheet
                    {
                        FullName = workerE.FullName,
                        Position = workerE.JobTitle,
                        Days = []
                    };

                    var newStartDate = startDate;

                    for (int i = 0; i < daysCount; i++)
                    {
                        var history = employeehistory.FirstOrDefault(h => h.ArrivalTime.Date == newStartDate.Date);

                        if (history != null)
                        {
                            worker.Days.Add((history.ArrivalTime.ToString("HH:mm"),
                                history.LeaveTime?.ToString("HH:mm") ?? " - ", history.WorkHours ?? 0));
                        }
                        else
                        {
                            worker.Days.Add((" - ", " - ", 0));
                        }

                        newStartDate = newStartDate.AddDays(1);
                    }

                    workers.Add(worker);
                }

                string templatePath =
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports", "TimesheetTemplate.xlsx");
                if (!File.Exists(templatePath))
                {
                    new DialogWindow("Ошибка", "Шаблон отчета не найден!").ShowDialog();
                    return;
                }

                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = "Excel Files (*.xlsx)|*.xlsx",
                    FileName = $"Отчет_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.xlsx",
                    Title = "Сохранить отчет"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    try
                    {
                        _excelReports.GenerateTimesheetReport(templatePath, saveFileDialog.FileName, workers, startDate,
                            endDate);
                        new DialogWindow("Успех", $"Отчет успешно сохранен как: {saveFileDialog.FileName}")
                            .ShowDialog();
                        return;
                    }
                    catch (Exception ex)
                    {
                        new DialogWindow("Ошибка", "При генерации отчета возникла ошибка!").ShowDialog();
                        Log.Error(ex, ex.Message);
                    }
                }
            }
            finally
            {
                _loadingService.StopLoading();
            }
        }
    }
}