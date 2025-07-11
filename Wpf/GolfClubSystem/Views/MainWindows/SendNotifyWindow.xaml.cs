﻿using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using GolfClubSystem.Models;
using GolfClubSystem.Services;
using GolfClubSystem.Views.UserControlsViews;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Expression = System.Linq.Expressions.Expression;

namespace GolfClubSystem.Views.MainWindows
{
    public class PercentItem
    {
        public int Value { get; set; }
    }

    public partial class SendNotifyWindow : Window, INotifyPropertyChanged
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly LoadingService _loadingService;
        
        public ObservableCollection<Worker> Workers { get; set; }
        public List<Organization> Organizations { get; set; }
        public List<Zone> Zones { get; set; }

        private Organization? _organization;

        public Organization? Organization
        {
            get => _organization;
            set
            {
                _organization = value;
                OnPropertyChanged();
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

        private bool _isWorkersVisible = true;

        public bool IsWorkersVisible
        {
            get => _isWorkersVisible;
            set => SetField(ref _isWorkersVisible, value);
        }

        public SendNotifyWindow()
        {
            _configuration = ((App)Application.Current)._configuration;
            var apiUrl = _configuration.GetSection("ApiUrl").Value
                         ?? throw new Exception("ApiUrl не прописан в конфигах!");
            _httpClient = new HttpClient { BaseAddress = new Uri(apiUrl) };
            _loadingService = LoadingService.Instance;
            
            InitializeComponent();
            
            Loaded += SendNotifyWindow_Loaded;
        }
        
        private async void SendNotifyWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= SendNotifyWindow_Loaded;
            await InitializeDataAsync();
        }

        private async Task InitializeDataAsync()
        {
            try
            {
                await Task.WhenAll(
                    LoadOrganizationsAsync(),
                    LoadZonesAsync(),
                    UpdateWorkers()
                );
                
                DataContext = this;
            }
            catch (Exception ex)
            {
                new DialogWindow("Ошибка", $"Ошибка загрузки данных: {ex.Message}").ShowDialog();
            }
        }

        private async Task LoadOrganizationsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("api/Hr/organizations");
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                var organizations = JsonConvert.DeserializeObject<List<Organization>>(json) ?? [];
                Organizations = [..organizations];
                OnPropertyChanged(nameof(Organizations));
            }
            catch (Exception ex)
            {
                new DialogWindow("Ошибка", $"Ошибка загрузки организаций: {ex.Message}").ShowDialog();
                Organizations = [];
            }
        }

        private async Task LoadZonesAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("api/Hr/zones");
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                var zones = JsonConvert.DeserializeObject<List<Zone>>(json) ?? [];
                Zones = [..zones];
                OnPropertyChanged(nameof(Zones));
            }
            catch (Exception ex)
            {
                new DialogWindow("Ошибка", $"Ошибка загрузки зон: {ex.Message}").ShowDialog();
                Zones = [];
            }
        }

        private async Task UpdateWorkers(int? organizationId = null, int? zoneId = null)
        {
            _loadingService.StartLoading();
            try
            {
                var queryParams = new List<string>();

                if (organizationId.HasValue)
                    queryParams.Add($"organizationId={organizationId.Value}");
                if (zoneId.HasValue)
                    queryParams.Add($"zoneId={zoneId.Value}");
                
                queryParams.Add("pageNumber=1");
                queryParams.Add("pageSize=2000");
                queryParams.Add($"endWorkDate={DateTime.Now.Date:yyyy-MM-dd}");

                var queryString = string.Join("&", queryParams);
                var response = await _httpClient.GetAsync($"api/Hr/workers-paged?{queryString}");
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<PagedWorkersResponse>(json);

                Workers = new ObservableCollection<Worker>(result?.Workers ?? []);
                OnPropertyChanged(nameof(Workers));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки сотрудников: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                Workers = [];
            }
            finally
            {
                _loadingService.StopLoading();
            }
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

        private async void SendNotification_OnClick(object sender, RoutedEventArgs e)
        {
            _loadingService.StartLoading();
            try
            {
                var workers = WorkersListBox.SelectedItems.Cast<Worker>().ToList();
                var notifyRequest = new
                {
                    Description,
                    Percent = SelectedPercent?.Value,
                    WorkerIds = SelectedPercent == null ? workers?.Select(w => w.Id).ToArray() : [],
                    OrganizationId = Organization?.Id != -1 ? Organization?.Id : null,
                    ZoneId = Zone?.Id != -1 ? Zone?.Id : null
                };

                var content = new StringContent(JsonConvert.SerializeObject(notifyRequest), Encoding.UTF8,
                    "application/json");
                var response = await _httpClient.PostAsync("api/Hr/notify", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    new DialogWindow("Ошибка", $"Ошибка отправки уведомления: {errorContent}").ShowDialog();
                    _loadingService.StopLoading();
                    return;
                }

                Close();
            }
            catch (Exception ex)
            {
                new DialogWindow("Ошибка", $"Ошибка отправки уведомления: {ex.Message}").ShowDialog();
            }
            finally
            {
                _loadingService.StopLoading();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _httpClient.Dispose();
        }

        private async void ComboBox_ZoneChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            var selectedZone = (Zone?)comboBox?.SelectedItem;

            if (selectedZone is {Id: not -1} && Organization is {Id: not -1})
            {
               await UpdateWorkers(Organization.Id, selectedZone.Id);
            }
            else if (Organization is {Id: not -1})
            {
                await UpdateWorkers(organizationId: Organization.Id);
            }
            else if (selectedZone is {Id: not -1})
            {
                await UpdateWorkers(zoneId: selectedZone.Id);
            }
            else
            {
                await UpdateWorkers();
            }
        }

        private async void ComboBox_OrganizationChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            var selectedOrganization = (Organization?)comboBox?.SelectedItem;

            if (selectedOrganization is {Id: not -1} && Zone is {Id: not -1})
            {
               await UpdateWorkers(selectedOrganization.Id, Zone.Id);
            }
            else if (selectedOrganization is {Id: not -1})
            {
                await UpdateWorkers(organizationId: selectedOrganization.Id);
            }
            else if (Zone is {Id: not -1})
            {
                await UpdateWorkers(zoneId: Zone.Id);
            }
            else
            {
                await UpdateWorkers();
            }
        }
    }
}public class PredicateVisitor : ExpressionVisitor
{
    public int? OrganizationId { get; private set; }
    public int? ZoneId { get; private set; }

    protected override Expression VisitBinary(BinaryExpression node)
    {
        if (node.NodeType == ExpressionType.Equal)
        {
            if (node.Left is MemberExpression member && node.Right is ConstantExpression constant)
            {
                if (member.Member.Name == "OrganizationId")
                    OrganizationId = (int?)constant.Value;
                else if (member.Member.Name == "ZoneId")
                    ZoneId = (int?)constant.Value;
            }
        }
        return base.VisitBinary(node);
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (node.Method.Name == "op_Equality")
        {
            var left = node.Arguments[0] as MemberExpression;
            var right = node.Arguments[1] as ConstantExpression;
            if (left != null && right != null)
            {
                if (left.Member.Name == "OrganizationId")
                    OrganizationId = (int?)right.Value;
                else if (left.Member.Name == "ZoneId")
                    ZoneId = (int?)right.Value;
            }
        }
        return base.VisitMethodCall(node);
    }
}