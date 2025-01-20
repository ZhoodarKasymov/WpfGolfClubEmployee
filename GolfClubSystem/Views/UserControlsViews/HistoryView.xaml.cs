using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Input;
using GolfClubSystem.Data;
using GolfClubSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace GolfClubSystem.Views.UserControlsViews;

public partial class HistoryView : UserControl, INotifyPropertyChanged
{
    public ObservableCollection<Employeehistory> Histories { get; set; }
    private readonly UnitOfWork _unitOfWork = new();
    
    public HistoryView()
    {
        InitializeComponent();
        
        UpdateHistories();
        DataContext = this;
    }

    private void UpdateHistories()
    {
        var employeehistories = _unitOfWork.HistoryRepository.GetAll()
            .Include(h => h.MarkZone)
            .Include(h => h.Worker)
            .ThenInclude(w => w.Organization)
            .Include(h => h.Worker)
            .ThenInclude(w => w.Zone)
            .Include(h => h.Worker)
            .ThenInclude(w => w.Schedule)
            .AsNoTracking()
            .ToList();
        
        Histories = new ObservableCollection<Employeehistory>(employeehistories);
        OnPropertyChanged(nameof(Histories));
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
}