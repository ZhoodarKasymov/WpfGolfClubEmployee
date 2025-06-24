using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GolfClubSystem.Models;
using GolfClubSystem.Services;
using GolfClubSystem.ViewModels;
using GolfClubSystem.Views.WorkersWindow;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Serilog;

namespace GolfClubSystem.Views.UserControlsViews;

public partial class OrganizationsView : UserControl, INotifyPropertyChanged, IDataErrorInfo
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly LoadingService _loadingService;

    public ObservableCollection<Node> Nodes { get; set; } = new();
    public ObservableCollection<Worker> Workers { get; set; } = new();
    public List<Zone> Zones { get; set; }

    private int _currentPage = 1;
    private const int PageSize = 10;

    public Node SelectedNode { get; set; }

    public NodeType SelectedNodeType { get; set; }

    private string _newOrganizationName;

    public string NewOrganizationName
    {
        get => _newOrganizationName;
        set
        {
            _newOrganizationName = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsSaveEnabled));
        }
    }

    public bool IsSaveEnabled => string.IsNullOrWhiteSpace(NewOrganizationName) == false;

    private bool _isDialogOpen;

    public bool IsDialogOpen
    {
        get => _isDialogOpen;
        set
        {
            _isDialogOpen = value;
            OnPropertyChanged();
        }
    }

    private bool _isEmployerShow;

    public bool IsEmployerShow
    {
        get => _isEmployerShow;
        set
        {
            _isEmployerShow = value;
            OnPropertyChanged();
        }
    }

    public ICommand OpenAddOrganizationDialogCommand { get; }
    public ICommand AddNewOrganizationCommand { get; }
    public ICommand CloseDialogCommand { get; }
    public ICommand EditNodeCommand { get; }
    public ICommand DeleteNodeCommand { get; }
    public ICommand AddNodeCommand { get; }
    public ICommand ShowWorkersCommand { get; }

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

    public OrganizationsView()
    {
        _configuration = ((App)Application.Current)._configuration;
        var apiUrl = _configuration.GetSection("ApiUrl").Value
                     ?? throw new Exception("ApiUrl не прописан в конфигах!");
        _httpClient = new HttpClient { BaseAddress = new Uri(apiUrl) };
        _loadingService = LoadingService.Instance;

        InitializeComponent();

        OpenAddOrganizationDialogCommand = new RelayCommand(OpenAddOrganizationDialog);
        AddNewOrganizationCommand = new RelayCommand(AddNewOrganization);
        CloseDialogCommand = new RelayCommand(CloseDialog);
        EditNodeCommand = new RelayCommand<Node>(EditNode);
        DeleteNodeCommand = new RelayCommand<Node>(DeleteNode);
        AddNodeCommand = new RelayCommand<Node>(AddNode);
        ShowWorkersCommand = new RelayCommand<Node>(ShowWorkers);
        EditCommand = new RelayCommand<Worker>(OnEdit);
        DeleteCommand = new RelayCommand<Worker>(OnDelete);
        ShowCommand = new RelayCommand<Worker>(OnShow);

        IsEmployerShow = false;
        Loaded += OrganizationsView_Loaded;
        Unloaded += WorkersView_Unloaded;
    }

    private async void OrganizationsView_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OrganizationsView_Loaded;
        await InitializeDataAsync();
    }

    private async Task InitializeDataAsync()
    {
        _loadingService.StartLoading();
        try
        {
            await Task.WhenAll(
                LoadZonesAsync(),
                UpdateNodes()
            );

            DataContext = this;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка загрузки данных: {ex.Message}", "Ошибка", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _loadingService.StopLoading();
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
            Zones = zones;
            OnPropertyChanged(nameof(Zones));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка загрузки зон: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            Zones = [];
        }
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
            _loadingService.StartLoading();
            try
            {
                var response = await _httpClient.DeleteAsync($"api/Hr/workers/{worker.Id}");
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    MessageBox.Show($"Ошибка удаления сотрудника: {errorContent}", "Ошибка", MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    _loadingService.StopLoading();
                    return;
                }

                ApplyFilters();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка удаления сотрудника: {ex.Message}", "Ошибка", MessageBoxButton.OK,
                    MessageBoxImage.Error);
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

    private async Task UpdateNodes()
    {
        _loadingService.StartLoading();
        try
        {
            var response = await _httpClient.GetAsync("api/Hr/organizations-full");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var organizations = (JsonConvert.DeserializeObject<List<Organization>>(json) ?? [])
                .ToList();

            Nodes = BuildHierarchy(organizations);
            OnPropertyChanged(nameof(Nodes));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка загрузки организаций: {ex.Message}", "Ошибка", MessageBoxButton.OK,
                MessageBoxImage.Error);
            Nodes = new ObservableCollection<Node>();
        }
        finally
        {
            _loadingService.StopLoading();
        }
    }

    private ObservableCollection<Node> BuildHierarchy(ICollection<Organization> organizations, int? parentId = null)
    {
        return new ObservableCollection<Node>(
            organizations
                .Where(o => o.ParentOrganizationId == parentId) // Filter by parentId
                .Select(o => new Node
                {
                    Name = o.Name,
                    Nodes = BuildHierarchy(organizations, o.Id) // Recursively build child nodes
                })
        );
    }

    private void OpenAddOrganizationDialog()
    {
        SelectedNodeType = NodeType.AddNode;
        IsDialogOpen = true;
    }

    private void CloseDialog()
    {
        IsDialogOpen = false;
    }

    private async void AddNewOrganization()
    {
        if (!string.IsNullOrEmpty(NewOrganizationName))
        {
            var type = SelectedNodeType;

            switch (type)
            {
                case NodeType.AddNode:
                {
                    var existNode = FindNodeRecursive(Nodes, NewOrganizationName);

                    if (existNode is not null)
                    {
                        new DialogWindow("Ошибка", "Нельзя дублировать названия организаций!").ShowDialog();
                        return;
                    }

                    var content =
                        new StringContent(JsonConvert.SerializeObject(new Organization { Name = NewOrganizationName }),
                            Encoding.UTF8, "application/json");
                    var response = await _httpClient.PostAsync("api/Hr/organizations", content);

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        new DialogWindow("Ошибка", $"Ошибка: {errorContent}").ShowDialog();
                        _loadingService.StopLoading();
                        return;
                    }

                    Nodes.Add(new Node { Name = NewOrganizationName });
                    break;
                }
                case NodeType.AddSubNode:
                {
                    var selectedOrg = await GetOrganizationByNameAsync(SelectedNode.Name);
                    if (selectedOrg == null)
                    {
                        new DialogWindow("Ошибка", "Выбранная организация не найдена").ShowDialog();
                        return;
                    }

                    var existNode = FindNodeRecursive(Nodes, NewOrganizationName);

                    if (existNode is not null)
                    {
                        new DialogWindow("Ошибка", "Нельзя дублировать названия организаций!").ShowDialog();
                        return;
                    }

                    var content =
                        new StringContent(
                            JsonConvert.SerializeObject(new Organization
                                { Name = NewOrganizationName, ParentOrganizationId = selectedOrg.Id }), Encoding.UTF8,
                            "application/json");
                    var response = await _httpClient.PostAsync("api/Hr/organizations", content);

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        new DialogWindow("Ошибка", $"Ошибка: {errorContent}").ShowDialog();
                        _loadingService.StopLoading();
                        return;
                    }

                    SelectedNode.Nodes.Add(new Node { Name = NewOrganizationName });
                    break;
                }
                case NodeType.EditNode:
                {
                    var selectedOrg = await GetOrganizationByNameAsync(SelectedNode.Name);
                    if (selectedOrg == null)
                    {
                        new DialogWindow("Ошибка", "Выбранная организация не найдена").ShowDialog();
                        return;
                    }

                    var content =
                        new StringContent(
                            JsonConvert.SerializeObject(new Organization
                                { Name = NewOrganizationName, ParentOrganizationId = selectedOrg.Id }), Encoding.UTF8,
                            "application/json");
                    var response = await _httpClient.PutAsync($"api/Hr/organizations/{selectedOrg.Id}", content);

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        new DialogWindow("Ошибка", $"Ошибка: {errorContent}").ShowDialog();
                        _loadingService.StopLoading();
                        return;
                    }

                    await UpdateNodes();
                    break;
                }
            }

            NewOrganizationName = "";
            CloseDialog();
        }
    }

    private async Task<Organization?> GetOrganizationByNameAsync(string name)
    {
        var response = await _httpClient.GetAsync("api/Hr/organizations");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        var organizations = JsonConvert.DeserializeObject<List<Organization>>(json) ?? [];
        return organizations.FirstOrDefault(o => o.Name == name && o.Id != -1);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private Node? FindNodeRecursive(ObservableCollection<Node> nodes, string nodeName)
    {
        foreach (var node in nodes)
        {
            if (node.Name == nodeName)
                return node;

            if (node.Nodes != null)
            {
                var found = FindNodeRecursive(node.Nodes, nodeName);
                if (found != null)
                    return found;
            }
        }

        return null;
    }

    private void EditNode(Node node)
    {
        if (node == null) return;
        NewOrganizationName = node.Name;
        SelectedNode = node;
        SelectedNodeType = NodeType.EditNode;
        IsDialogOpen = true;
    }

    private async void DeleteNode(Node node)
    {
        if (node == null) return;

        var selectedOrg = await GetOrganizationByNameAsync(node.Name);
        var hasWorkers = await CheckHasActiveWorkersAsync(selectedOrg?.Id);

        if (hasWorkers)
        {
            new DialogWindow("Ошибка", "Нельзя удалить организацию у организации есть активные рабочие!").ShowDialog();
        }
        
        var answer = new DialogWindow("Подтверждение", $"Вы уверены удалить организацию: {node.Name}?", "Да", "Нет").ShowDialog();

        if (answer.HasValue && answer.Value)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"api/Hr/organizations/{selectedOrg.Id}");
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    new DialogWindow("Ошибка", $"Ошибка удаления: {errorContent}").ShowDialog();
                    _loadingService.StopLoading();
                    return;
                }

                Nodes.Remove(node);
                await UpdateNodes();
            }
            catch (Exception ex)
            {
                new DialogWindow("Ошибка", $"Ошибка удаления организации!").ShowDialog();
                Log.Error($"Ошибка удаления организации: {ex.Message}", ex);
            }
        }
    }

    private async Task<bool> CheckHasActiveWorkersAsync(int? organizationId)
    {
        if (!organizationId.HasValue) return false;
        var response =
            await _httpClient.GetAsync(
                $"api/Hr/workers-paged?organizationId={organizationId}&pageNumber=1&pageSize=1");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<dynamic>(json);
        return result.TotalCount > 0;
    }

    private void AddNode(Node node)
    {
        if (node == null) return;
        SelectedNode = node;
        SelectedNodeType = NodeType.AddSubNode;
        IsDialogOpen = true;
    }

    private async void ApplyFilters()
    {
        try
        {
            var queryParams = new List<string>();

            if (!string.IsNullOrEmpty(SearchBox.Text))
            {
                queryParams.Add($"search={Uri.EscapeDataString(SearchBox.Text)}");
            }

            if (SelectedNode != null)
            {
                var selectedOrg = await GetOrganizationByNameAsync(SelectedNode.Name);
                if (selectedOrg != null)
                {
                    queryParams.Add($"organizationId={selectedOrg.Id}");
                }
            }

            if (ZonesFilter.SelectedItem != null && (ZonesFilter.SelectedItem as Zone).Id != -1)
            {
                var zone = ZonesFilter.SelectedItem as Zone;
                queryParams.Add($"zoneId={zone.Id}");
            }

            queryParams.Add($"pageNumber={_currentPage}");
            queryParams.Add($"pageSize={PageSize}");

            var queryString = string.Join("&", queryParams.ToArray());
            var response = await _httpClient.GetAsync($"api/Hr/workers-paged?{queryString}");
            response.EnsureSuccessStatusCode();
            var jsonContent = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<PagedWorkersResponse>(jsonContent);

            Workers = new ObservableCollection<Worker>(result?.Workers ?? []);
            OnPropertyChanged(nameof(Workers));
            PageNumberText.Text = _currentPage.ToString();

            IsPreviousPageEnabled = _currentPage > 1;
            IsNextPageEnabled = (_currentPage * PageSize) < result.TotalCount;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка фильтрации сотрудников: {ex.Message}", "Ошибка", MessageBoxButton.OK,
                MessageBoxImage.Error);
            Workers = new ObservableCollection<Worker>();
        }
    }

    private void ShowWorkers(Node node)
    {
        if (node == null) return;
        SelectedNode = node;
        IsEmployerShow = true;
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

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilters();
    }

    private void ZonesFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyFilters();
    }

    public string Error => null; // Not used but required for IDataErrorInfo

    public string this[string columnName]
    {
        get
        {
            if (columnName == nameof(NewOrganizationName))
            {
                if (string.IsNullOrWhiteSpace(NewOrganizationName))
                {
                    return "Название организации не может быть пустым";
                }
            }

            return null;
        }
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
                if (commandProperty.GetValue(vm) is ICommand navigateCommand &&
                    navigateCommand.CanExecute("Organizations"))
                {
                    navigateCommand.Execute("Organizations");
                }
            }
        }
    }
}