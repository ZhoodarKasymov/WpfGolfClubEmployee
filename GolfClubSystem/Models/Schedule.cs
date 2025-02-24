using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;

namespace GolfClubSystem.Models;

public partial class Schedule : IDataErrorInfo, INotifyPropertyChanged
{
    public uint Id { get; set; }

    public string Name { get; set; } = null!;

    public TimeOnly? PermissibleLateTimeStart { get; set; }

    public TimeOnly? PermissibleEarlyLeaveStart { get; set; }

    public DateTime? DeletedAt { get; set; }

    public TimeOnly? BreakStart { get; set; }

    public TimeOnly? BreakEnd { get; set; }

    public TimeOnly? PermissibleLateTimeEnd { get; set; }

    public TimeOnly? PermissibleEarlyLeaveEnd { get; set; }

    public TimeOnly? PermissionToLateTime { get; set; }

    public virtual ICollection<Holiday> Holidays { get; set; } = new List<Holiday>();

    public virtual ICollection<Scheduleday> Scheduledays { get; set; } = new List<Scheduleday>();

    public virtual ICollection<Worker> Workers { get; set; } = new List<Worker>();
    
    [NotMapped]
    public string Error { get; }
    
    [NotMapped] private bool _hasError;

    [NotMapped]
    public bool HasError
    {
        get => _hasError;
        set
        {
            if (_hasError != value)
            {
                _hasError = value;
                OnPropertyChanged();
            }
        }
    }

    public virtual ICollection<NotifyJob> NotifyJobs { get; set; } = [];

    private bool GetValidationErrors()
    {
        return !string.IsNullOrEmpty(Name);
    }

    [NotMapped]
    public string this[string columnName]
    {
        get
        {
            string result = null;

            switch (columnName)
            {
                case nameof(Name):
                {
                    if (string.IsNullOrEmpty(Name))
                    {
                        result = "Поле обязательно для заполнения";
                    }

                    break;
                }
                case nameof(Scheduledays):
                {
                    if (!Scheduledays.Any(h => h.IsSelected))
                    {
                        result = "Выберите хотя бы один рабочий день";
                    }

                    if (Scheduledays.Any(h => h.IsSelected && h.WorkStart == null || h.WorkEnd == null))
                    {
                        result = "У выбранных рабочих дней, время работы должно быть заполненно";
                    }

                    break;
                }
            }

            HasError = GetValidationErrors();
            OnPropertyChanged(nameof(HasError));
            return result;
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
}
