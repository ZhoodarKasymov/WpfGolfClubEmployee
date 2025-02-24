using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GolfClubSystem.Data;
using GolfClubSystem.Models;
using GolfClubSystem.Services;
using GolfClubSystem.ViewModels;
using GolfClubSystem.Views.WorkersWindow;
using Microsoft.EntityFrameworkCore;

namespace GolfClubSystem.Views.UserControlsViews;

public partial class OrganizationsView : UserControl, INotifyPropertyChanged, IDataErrorInfo
{
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

    private readonly UnitOfWork _unitOfWork = new();
    
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
        InitializeComponent();
        DataContext = this;

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

        Zones = _unitOfWork.ZoneRepository.GetAll().Where(o => o.DeletedAt == null).ToList();
        IsEmployerShow = false;
        UpdateNodes();
        Unloaded += WorkersView_Unloaded;
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
                }
            }
        }
    }

    private void OnShow(Worker worker)
    {
        var window = new AddEditWorkerWindow(worker, false);
        window.ShowDialog();
    }

    private void UpdateNodes()
    {
        var organizations = BuildHierarchy(_unitOfWork.OrganizationRepository
            .GetAll()
            .Where(o => o.DeletedAt == null).ToList());
        
        Nodes = organizations;
        OnPropertyChanged(nameof(Nodes));
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
                    var existInDb = _unitOfWork.OrganizationRepository.GetAll().Where(o => o.DeletedAt == null)
                        .Any(o => o.Name == NewOrganizationName);

                    if (existNode is not null || existInDb)
                    {
                        MessageBox.Show("Нельзя дублировать названия организаций!", "Ошибка", MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        return;
                    }

                    Nodes.Add(new Node { Name = NewOrganizationName });
                    await _unitOfWork.OrganizationRepository.AddAsync(new Organization { Name = NewOrganizationName });

                    break;
                }
                case NodeType.AddSubNode:
                {
                    var existNode = FindNodeRecursive(Nodes, NewOrganizationName);
                    var existInDb = _unitOfWork.OrganizationRepository.GetAll().Where(o => o.DeletedAt == null)
                        .Any(o => o.Name == NewOrganizationName);

                    if (existNode is not null || existInDb)
                    {
                        MessageBox.Show("Нельзя дублировать названия организаций!", "Ошибка", MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        return;
                    }

                    var selectedOrg = _unitOfWork.OrganizationRepository.GetAll()
                        .FirstOrDefault(o => o.Name == SelectedNode.Name);
                    SelectedNode.Nodes.Add(new Node { Name = NewOrganizationName });
                    selectedOrg?.InverseParentOrganization.Add(new Organization { Name = NewOrganizationName });

                    break;
                }
                case NodeType.EditNode:
                {
                    var selectedOrg = _unitOfWork.OrganizationRepository.GetAll()
                        .FirstOrDefault(o => o.Name == SelectedNode.Name);
                    var existInDb = _unitOfWork.OrganizationRepository.GetAll().Any(o => o.Name == NewOrganizationName);

                    if (existInDb)
                    {
                        MessageBox.Show("Нельзя дублировать названия организаций!", "Ошибка", MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        return;
                    }

                    if (selectedOrg is not null)
                    {
                        selectedOrg.Name = NewOrganizationName;
                        await _unitOfWork.OrganizationRepository.UpdateAsync(selectedOrg);
                        UpdateNodes();
                    }

                    break;
                }
            }

            await _unitOfWork.SaveAsync();
            NewOrganizationName = "";
            CloseDialog();
        }
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
        var result = MessageBox.Show($"Вы уверены удалить организацию: {node.Name}?", "Подтверждение",
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            var selectedOrg = _unitOfWork.OrganizationRepository.GetAll().Where(o => o.DeletedAt == null)
                .FirstOrDefault(o => o.Name == node.Name);
            if (selectedOrg is not null)
            {
                MarkAsDeleted(selectedOrg);
                await _unitOfWork.OrganizationRepository.UpdateAsync(selectedOrg);
                Nodes.Remove(node);
                UpdateNodes();
            }
        }

        void MarkAsDeleted(Organization organization)
        {
            organization.DeletedAt = DateTime.Now;

            // Recursively mark sub-organizations as deleted
            foreach (var subOrg in organization.InverseParentOrganization)
            {
                MarkAsDeleted(subOrg);
            }
        }
    }

    private void AddNode(Node node)
    {
        if (node == null) return;
        SelectedNode = node;
        SelectedNodeType = NodeType.AddSubNode;
        IsDialogOpen = true;
    }

    private void ApplyFilters()
    {
        var filteredHistories = _unitOfWork.WorkerRepository.GetAll()
            .Include(w => w.Zone)
            .Include(w => w.Organization)
            .Where(w => w.DeletedAt == null)
            .AsNoTracking();

        // Apply search filter
        if (!string.IsNullOrEmpty(SearchBox.Text))
        {
            filteredHistories = filteredHistories.Where(w => w.FullName.Contains(SearchBox.Text)
                                                             || w.JobTitle.Contains(SearchBox.Text));
        }

        if (SelectedNode != null)
        {
            filteredHistories = filteredHistories.Where(w => w.Organization!.Name == SelectedNode.Name);
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
        IsNextPageEnabled = (_currentPage * PageSize) < totalWorkers;
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
                if (commandProperty.GetValue(vm) is ICommand navigateCommand && navigateCommand.CanExecute("Organizations"))
                {
                    navigateCommand.Execute("Organizations");
                }
            }
        }
    }
}