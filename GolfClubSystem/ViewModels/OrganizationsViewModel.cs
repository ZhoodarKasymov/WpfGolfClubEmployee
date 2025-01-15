using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using GolfClubSystem.Data;
using GolfClubSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace GolfClubSystem.ViewModels
{
    public class Node
    {
        public Node()
        {
            Nodes = new ObservableCollection<Node>();
        }

        public string Name { get; set; }
        public ObservableCollection<Node> Nodes { get; set; }
    }

    public enum NodeType
    {
        AddNode,
        AddSubNode,
        EditNode
    }
    
    public class OrganizationsViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<Node> Nodes { get; set; } = new();
        public ObservableCollection<Worker> Workers { get; set; } = new();
        
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
            }
        }
        
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

        public ICommand OpenAddOrganizationDialogCommand { get; }
        public ICommand AddNewOrganizationCommand { get; }
        public ICommand CloseDialogCommand { get; }
        public ICommand EditNodeCommand { get; }
        public ICommand DeleteNodeCommand { get; }
        public ICommand AddNodeCommand { get; }
        public ICommand ShowWorkersCommand { get; }

        private readonly UnitOfWork _unitOfWork = new();

        public OrganizationsViewModel()
        {
            OpenAddOrganizationDialogCommand = new RelayCommand(OpenAddOrganizationDialog);
            AddNewOrganizationCommand = new RelayCommand(AddNewOrganization);
            CloseDialogCommand = new RelayCommand(CloseDialog);
            EditNodeCommand = new RelayCommand<Node>(EditNode);
            DeleteNodeCommand = new RelayCommand<Node>(DeleteNode);
            AddNodeCommand = new RelayCommand<Node>(AddNode);
            ShowWorkersCommand = new RelayCommand<Node>(ShowWorkers);

            UpdateNodes();
        }

        private void UpdateNodes()
        {
            var organizations = BuildHierarchy(_unitOfWork.OrganizationRepository.GetAllAsync().Where(o => o.DeletedAt == null).ToList());
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
                        var existInDb = _unitOfWork.OrganizationRepository.GetAllAsync().Where(o => o.DeletedAt == null).Any(o => o.Name == NewOrganizationName);

                        if (existNode is not null || existInDb)
                        {
                            MessageBox.Show("Нельзя дублировать названия организаций!","Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                        
                        Nodes.Add(new Node { Name = NewOrganizationName });
                        await _unitOfWork.OrganizationRepository.AddAsync(new Organization { Name = NewOrganizationName });
                        
                        break;
                    }
                    case NodeType.AddSubNode:
                    {
                        var existNode = FindNodeRecursive(Nodes, NewOrganizationName);
                        var existInDb = _unitOfWork.OrganizationRepository.GetAllAsync().Where(o => o.DeletedAt == null).Any(o => o.Name == NewOrganizationName);

                        if (existNode is not null || existInDb)
                        {
                            MessageBox.Show("Нельзя дублировать названия организаций!","Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                        
                        var selectedOrg = _unitOfWork.OrganizationRepository.GetAllAsync().FirstOrDefault(o => o.Name == SelectedNode.Name);
                        SelectedNode.Nodes.Add(new Node { Name = NewOrganizationName });
                        selectedOrg?.InverseParentOrganization.Add(new Organization{ Name = NewOrganizationName });
                        
                        break;
                    }
                    case NodeType.EditNode:
                    {
                        var selectedOrg = _unitOfWork.OrganizationRepository.GetAllAsync().FirstOrDefault(o => o.Name == SelectedNode.Name);
                        var existInDb = _unitOfWork.OrganizationRepository.GetAllAsync().Any(o => o.Name == NewOrganizationName);

                        if (existInDb)
                        {
                            MessageBox.Show("Нельзя дублировать названия организаций!","Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
            var result = MessageBox.Show($"Вы уверены удалить организацию: {node.Name}?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                var selectedOrg = _unitOfWork.OrganizationRepository.GetAllAsync().Where(o => o.DeletedAt == null).FirstOrDefault(o => o.Name == node.Name);
                if (selectedOrg is not null)
                {
                    MarkAsDeleted(selectedOrg);
                    await _unitOfWork.OrganizationRepository.UpdateAsync(selectedOrg);
                    await _unitOfWork.SaveAsync();
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
        
        private void ShowWorkers(Node node)
        {
            if (node == null) return;
            
            var selectedOrg = _unitOfWork.OrganizationRepository.GetAllAsync()
                .Where(o => o.DeletedAt == null)
                .Include(o => o.Workers.Where(w => w.DeletedAt == null))
                .ThenInclude(w => w.Zone)
                .FirstOrDefault(o => o.Name == node.Name);
            
            if (selectedOrg is not null)
            {
                Workers = new ObservableCollection<Worker>(selectedOrg.Workers);
                OnPropertyChanged(nameof(Workers));
            }
        }
    }
}