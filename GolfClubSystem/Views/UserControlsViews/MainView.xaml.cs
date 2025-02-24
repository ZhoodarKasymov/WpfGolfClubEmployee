using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using GolfClubSystem.Data;
using GolfClubSystem.Models;
using GolfClubSystem.Views.MainWindows;
using LiveCharts;
using LiveCharts.Wpf;
using Microsoft.EntityFrameworkCore;

namespace GolfClubSystem.Views.UserControlsViews;

public partial class MainView : UserControl, INotifyPropertyChanged
{
    private readonly UnitOfWork _unitOfWork = new();
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
        InitializeComponent();
        DataContext = this;
        TodayFilter.Background = new SolidColorBrush(Color.FromRgb(46, 87, 230));
        TodayFilter.Foreground = Brushes.White;
        ApplyTodayFilter();
        Organizations = _unitOfWork.OrganizationRepository.GetAll().Where(o => o.DeletedAt == null).ToList();
        
        Unloaded += WorkersView_Unloaded;
    }

    private void UpdateHistory()
    {
        var allHistory = _unitOfWork.HistoryRepository
            .GetAll(true)
            .Include(h => h.Worker)
            .Include(h => h.MarkZone)
            .AsQueryable();

        var allHistoryNotify = _unitOfWork.NotifyHistoryRepository
            .GetAll(true)
            .Include(h => h.Worker)
            .AsQueryable();

        if (_startDate.HasValue && _endDate.HasValue)
        {
            allHistory = allHistory.Where(h => h.ArrivalTime >= _startDate.Value 
                                               && h.ArrivalTime <= _endDate.Value);

            allHistoryNotify = allHistoryNotify.Where(h => h.ArrivalTime >= _startDate.Value
                                                           && h.ArrivalTime <= _endDate.Value);
        }
        
        // Apply organization filter
        if (OrganizationFilter.SelectedItem != null)
        {
            var organization = OrganizationFilter.SelectedItem as Organization;
            allHistory = allHistory.Where(h => h.Worker.OrganizationId == organization!.Id);
            allHistoryNotify = allHistoryNotify.Where(h => h.Worker.OrganizationId == organization!.Id);
        }

        var groupedHistory = allHistory
            .GroupBy(h => h.MarkZoneId)
            .ToList();

        var totalEmployees = allHistoryNotify.Count();
        var trackedCount = allHistoryNotify.Count(w => w.Status == 1);
        var notifyCount = allHistoryNotify.Count(w => w.Status == 2);

        var percentageMoreOrEqual8Hours = (double)trackedCount / totalEmployees * 100;
        var percentageLessThan8Hours = (double)notifyCount / totalEmployees * 100;
        var percent = (int)(percentageMoreOrEqual8Hours + percentageLessThan8Hours) < 0
            ? 0
            : (int)(percentageMoreOrEqual8Hours + percentageLessThan8Hours);
        
        DonutPercent = $"{percent}%";
        DonutTrackedCount = trackedCount.ToString();
        DonutNotifyCount = notifyCount.ToString();
        
        // Настройка данных для круговой диаграммы
        PieChart.Series = new SeriesCollection
        {
            new PieSeries
            {
                Title = "Отметились",
                Values = new ChartValues<double> { Math.Round(percentageMoreOrEqual8Hours) },
                Fill = new SolidColorBrush(Color.FromRgb(0, 190, 85)),
                DataLabels = true
            },
            new PieSeries
            {
                Title = "Запросов",
                Values = new ChartValues<double> { Math.Round(percentageLessThan8Hours) },
                Fill = new SolidColorBrush(Color.FromRgb(238, 69, 69)),
                DataLabels = true
            }
        };

        // Настройка данных для столбчатой диаграммы
        var chartValues = new ChartValues<double>();
        var labels = new List<string>();

        foreach (var history in groupedHistory)
        {
            chartValues.Add(history.Count());
            labels.Add(history.First().MarkZone?.Name ?? "");
        }

        BarChart.Series = new SeriesCollection
        {
            new ColumnSeries
            {
                Title = "Количество сотрудников",
                Values = chartValues,
                Fill = new SolidColorBrush(Color.FromRgb(121, 135, 255))
            }
        };

        // Установка подписей по осям
        BarChart.AxisX[0].Labels = labels;
        BarChart.AxisY[0].LabelFormatter = value => value.ToString("N0", CultureInfo.InvariantCulture);

        InTime = allHistory.Count(h => h.Status == 1);
        VeryLate = allHistory.Count(h => h.Status == 2);
        Late = allHistory.Count(h => h.Status == 3);
        EarlyLeave = allHistory.Count(h => h.Status == 4);
        NoWorkers = 0;

        OnPropertyChanged(nameof(InTime));
        OnPropertyChanged(nameof(VeryLate));
        OnPropertyChanged(nameof(Late));
        OnPropertyChanged(nameof(EarlyLeave));
        OnPropertyChanged(nameof(NoWorkers));
        OnPropertyChanged(nameof(DonutPercent));
        OnPropertyChanged(nameof(DonutTrackedCount));
        OnPropertyChanged(nameof(DonutNotifyCount));
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
                if (commandProperty.GetValue(vm) is ICommand navigateCommand && navigateCommand.CanExecute("Main"))
                {
                    navigateCommand.Execute("Main");
                }
            }
        }
    }
}