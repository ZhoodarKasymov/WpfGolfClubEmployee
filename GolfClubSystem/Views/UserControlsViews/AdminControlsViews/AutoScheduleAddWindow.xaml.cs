using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using GolfClubSystem.Data;
using GolfClubSystem.Models;
using GolfClubSystem.Views.MainWindows;
using GolfClubSystem.Views.WorkersWindow;
using Microsoft.EntityFrameworkCore;

namespace GolfClubSystem.Views.UserControlsViews.AdminControlsViews
{
    public partial class AutoScheduleAddWindow : Window, INotifyPropertyChanged
    {
        private readonly UnitOfWork _unitOfWork = new();
        public ObservableCollection<Worker> Workers { get; set; } = new();
        public List<Organization> Organizations { get; set; }
        public List<Zone> Zones { get; set; }

        private Organization? _organization;
        
        public NotifyJob Job { get; set; }
        public WorkerType JobType { get; set; }
        
        public List<Schedule> Schedules { get; set; }

        public Organization? Organization
        {
            get => _organization;
            set
            {
                _organization = value;
                OnPropertyChanged();
                FilterItems();
            }
        }

        private Zone? _zone;

        public Zone? Zone
        {
            get => _zone;
            set
            {
                _zone = value;
                OnPropertyChanged();
                FilterItems();
            }
        }

        public string Description { get; set; } =
            "Вам необходимо прибыть в вашу Зону в течение 20 минут. \nПо прибытию пройдите биометрию через терминал.";

        private string _searchText;

        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                FilterItems();
            }
        }

        public ObservableCollection<PercentItem> Percents { get; set; } = new()
        {
            new PercentItem
            {
                Value = 10
            },
            new PercentItem
            {
                Value = 20
            },
            new PercentItem
            {
                Value = 30
            },
            new PercentItem
            {
                Value = 40
            },
            new PercentItem
            {
                Value = 50
            },
            new PercentItem
            {
                Value = 60
            },
            new PercentItem
            {
                Value = 70
            },
            new PercentItem
            {
                Value = 80
            },
            new PercentItem
            {
                Value = 90
            },
            new PercentItem
            {
                Value = 100
            }
        };

        private PercentItem? _selectedPercent;

        public PercentItem? SelectedPercent
        {
            get => _selectedPercent;
            set
            {
                _selectedPercent = value;
                OnPropertyChanged();
                IsWorkersVisible = _selectedPercent == null;
            }
        }
        
        private TimeOnly? _time;

        public TimeOnly? Time
        {
            get => _time;
            set
            {
                _time = value;
                OnPropertyChanged();
            }
        }

        private bool _isWorkersVisible = true;

        public bool IsWorkersVisible
        {
            get => _isWorkersVisible;
            set => SetField(ref _isWorkersVisible, value);
        }

        public AutoScheduleAddWindow(NotifyJob? job)
        {
            InitializeComponent();
            DataContext = this;
            Organizations = _unitOfWork.OrganizationRepository.GetAll().Where(o => o.DeletedAt == null).ToList();
            Zones = _unitOfWork.ZoneRepository.GetAll().Where(o => o.DeletedAt == null).ToList();
            Schedules = _unitOfWork.ScheduleRepository.GetAll().Where(o => o.DeletedAt == null).ToList();
            
            if (job is not null)
            {
                Job = job;
                JobType = WorkerType.Edit;
            }
            else
            {
                Job = new NotifyJob();
                JobType = WorkerType.Add;
            }
        }

        private void FilterItems()
        {
            var selectedWorkerIds = Workers.Where(worker => worker.IsSelected).Select(worker => worker.Id).ToList();

            var filteredWorkers = Workers.Where(worker =>
                (Organization == null || worker.OrganizationId == Organization.Id) &&
                (Zone == null || worker.ZoneId == Zone.Id)).ToList();

            if (!string.IsNullOrEmpty(SearchText))
            {
                filteredWorkers = filteredWorkers
                    .Where(worker => worker.FullName.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            // Reapply the selection to the filtered workers based on the stored selected worker IDs
            foreach (var worker in filteredWorkers)
            {
                worker.IsSelected = selectedWorkerIds.Contains(worker.Id);
            }

            Workers = new ObservableCollection<Worker>(
                filteredWorkers
                    .OrderBy(worker => worker.FullName)
            );

            OnPropertyChanged(nameof(Workers));
        }

        private void UpdateWorkers(Expression<Func<Worker, bool>>? predicate = null)
        {
            var workers = _unitOfWork.WorkerRepository.GetAll()
                .Where(w => w.DeletedAt == null)
                .AsNoTracking();

            if (predicate != null)
            {
                workers = workers.Where(predicate);
            }
            
            Workers = new ObservableCollection<Worker>(workers.ToList());
            OnPropertyChanged(nameof(Workers));
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

        private async void Submit(object sender, RoutedEventArgs e)
        {
            List<Worker> selectedWorkers;

            if (SelectedPercent != null)
            {
                var totalCountQuery = _unitOfWork.WorkerRepository.GetAll()
                    .Where(w => w.ChatId != null);
                        
                if (Organization != null)
                {
                    totalCountQuery = totalCountQuery.Where(w => w.OrganizationId == Organization.Id);
                }

                if (Zone != null)
                {
                    totalCountQuery = totalCountQuery.Where(w => w.ZoneId == Zone.Id);
                }
                        
                var totalCount = await totalCountQuery.CountAsync();
                var countToFetch = (int)Math.Ceiling(totalCount * (SelectedPercent.Value / 100m));
                        
                selectedWorkers = await totalCountQuery
                    .OrderBy(w => Guid.NewGuid()) // Randomize selection
                    .Take(countToFetch)            // Limit to the count
                    .ToListAsync();
            }
            else
            {
                selectedWorkers = Workers.Where(w => w.IsSelected).ToList();
            }

            if (selectedWorkers.Count == 0)
            {
                MessageBox.Show("Работники не найденны!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var shiftIsExist = _unitOfWork.NotifyJobRepository.GetAll().Any(s => s.ShiftId == Job.ShiftId);
            var countJob = _unitOfWork.NotifyJobRepository.GetAll().Count();

            if (shiftIsExist)
            {
                MessageBox.Show("Авто запрос на такое рассписание уже существует", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (countJob > 2)
            {
                MessageBox.Show("Нельзя больше 2 авто уведомлений создать", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            await _unitOfWork.NotifyJobRepository.AddAsync(new NotifyJob
            {
                OrganizationId = Organization?.Id,
                ZoneId = Zone?.Id,
                Message = Description,
                ShiftId = Job.ShiftId,
                Percentage = SelectedPercent?.Value,
                WorkerIds = JsonSerializer.Serialize(selectedWorkers.Select(w => w.Id).ToList())
            });
            
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _unitOfWork.Dispose();
        }

        private void ComboBox_ZoneChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            var selectedZone = (Zone?)comboBox?.SelectedItem;

            if (selectedZone != null && Organization != null)
            {
                UpdateWorkers(w => w.OrganizationId == Organization.Id && w.ZoneId == selectedZone.Id);
            }
            else if (Organization != null)
            {
                UpdateWorkers(w => w.OrganizationId == Organization.Id);
            }
            else if (selectedZone != null)
            {
                UpdateWorkers(w => w.ZoneId == selectedZone.Id);
            }
            else
            {
                UpdateWorkers();
            }
        }

        private void ComboBox_OrganizationChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            var selectedOrganization = (Organization?)comboBox?.SelectedItem;

            if (selectedOrganization != null && Zone != null)
            {
                UpdateWorkers(w => w.OrganizationId == selectedOrganization.Id && w.ZoneId == Zone.Id);
            }
            else if (selectedOrganization != null)
            {
                UpdateWorkers(w => w.OrganizationId == selectedOrganization.Id);
            }
            else if (Zone != null)
            {
                UpdateWorkers(w => w.ZoneId == Zone.Id);
            }
            else
            {
                UpdateWorkers();
            }
        }
    }
}