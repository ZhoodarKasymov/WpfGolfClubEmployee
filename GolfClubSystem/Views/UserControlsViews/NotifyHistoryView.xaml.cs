using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GolfClubSystem.Data;
using GolfClubSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace GolfClubSystem.Views.UserControlsViews
{
    public partial class NotifyHistoryView : UserControl, INotifyPropertyChanged
    {
        public ObservableCollection<NotifyHistory> Histories { get; set; }
        public List<Organization> Organizations { get; set; }

        public List<Item> Statuses { get; set; } = new()
        {
            new() { Name = "Отметка", Id = 1 },
            new() { Name = "Запрос", Id = 2 }
        };

        private readonly UnitOfWork _unitOfWork = new();
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
            InitializeComponent();
            TodayFilter.Background = new SolidColorBrush(Color.FromRgb(46, 87, 230));
            TodayFilter.Foreground = Brushes.White;
            ApplyTodayFilter();
            Organizations = _unitOfWork.OrganizationRepository.GetAll().Where(o => o.DeletedAt == null).ToList();

            DataContext = this;
            Unloaded += WorkersView_Unloaded;
        }

        private void ApplyFilters()
        {
            var filteredHistories = _unitOfWork.NotifyHistoryRepository
                .GetAll()
                .Include(h => h.Worker)
                .ThenInclude(w => w.Organization)
                .Include(h => h.Worker)
                .ThenInclude(w => w.Zone)
                .Include(h => h.Worker)
                .ThenInclude(w => w.Schedule)
                .AsNoTracking();

            // Apply date filter if startDate and endDate are set
            if (_startDate.HasValue && _endDate.HasValue)
            {
                filteredHistories = filteredHistories.Where(h => h.ArrivalTime >= _startDate.Value 
                                                                 && h.ArrivalTime <= _endDate.Value);
            }

            // Apply search filter
            if (!string.IsNullOrEmpty(SearchBox.Text))
            {
                filteredHistories = filteredHistories.Where(h => h.Worker.FullName.Contains(SearchBox.Text) 
                                                                 || h.Worker.JobTitle.Contains(SearchBox.Text));
            }

            // Apply organization filter
            if (OrganizationFilter.SelectedItem != null)
            {
                var organization = OrganizationFilter.SelectedItem as Organization;
                filteredHistories = filteredHistories.Where(h => h.Worker.OrganizationId == organization!.Id);
            }

            // Apply status filter
            if (StatusFilter.SelectedItem != null)
            {
                var status = StatusFilter.SelectedItem as Item;
                filteredHistories = filteredHistories.Where(h => h.Status == status!.Id);
            }
            
            int total = filteredHistories.Count();

            // Paginate the results
            var pagedHistories = filteredHistories
                .Skip((_currentPage - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            // Update ObservableCollection
            Histories = new ObservableCollection<NotifyHistory>(pagedHistories);
            OnPropertyChanged(nameof(Histories));
            PageNumberText.Text = _currentPage.ToString();
            
            IsPreviousPageEnabled = _currentPage > 1;
            IsNextPageEnabled = (_currentPage * PageSize) < total;
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
            _unitOfWork.Dispose();
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