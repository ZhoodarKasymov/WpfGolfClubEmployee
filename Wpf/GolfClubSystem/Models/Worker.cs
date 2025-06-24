using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;

namespace GolfClubSystem.Models;

public partial class Worker : IDataErrorInfo, INotifyPropertyChanged
{
    public string FullName { get; set; } = null!;

    public string JobTitle { get; set; } = null!;

    public long? ChatId { get; set; }

    public int? OrganizationId { get; set; }

    public string? TelegramUsername { get; set; }

    public string? Mobile { get; set; }

    public string? AdditionalMobile { get; set; }

    public string? CardNumber { get; set; }

    public string? PhotoPath { get; set; }

    public int? ZoneId { get; set; }

    public DateTime? DeletedAt { get; set; }

    public DateTime StartWork { get; set; }

    public DateTime? EndWork { get; set; }

    public int Id { get; set; }

    public uint? ScheduleId { get; set; }

    public virtual ICollection<Employeehistory> Employeehistories { get; set; } = new List<Employeehistory>();

    public virtual Organization? Organization { get; set; }

    public virtual Schedule? Schedule { get; set; }

    public virtual Zone? Zone { get; set; }

    [NotMapped] public bool IsSelected { get; set; }

    [NotMapped] public string Error { get; }

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

    public virtual ICollection<NotifyHistory>? NotifyHistories { get; set; }

    private bool GetValidationErrors()
    {
        return !string.IsNullOrEmpty(FullName)
               && !string.IsNullOrEmpty(JobTitle)
               && !string.IsNullOrEmpty(Mobile)
               && !string.IsNullOrEmpty(TelegramUsername)
               && StartWork != default
               && EndWork != default
               && OrganizationId is not (null or 0)
               && ScheduleId is not (null or 0)
               && ZoneId is not (null or 0);
    }

    [NotMapped]
    public string this[string columnName]
    {
        get
        {
            string result = null;

            switch (columnName)
            {
                case "FullName":
                {
                    if (string.IsNullOrEmpty(FullName))
                    {
                        result = "Поле обязательно для заполнения";
                    }

                    break;
                }
                case "CardNumber":
                {
                    if (string.IsNullOrEmpty(CardNumber))
                    {
                        result = "Поле обязательно для заполнения";
                    }

                    break;
                }
                case "JobTitle":
                {
                    if (string.IsNullOrEmpty(JobTitle))
                    {
                        result = "Поле обязательно для заполнения";
                    }

                    break;
                }
                case "Mobile":
                {
                    if (string.IsNullOrEmpty(Mobile))
                    {
                        result = "Поле обязательно для заполнения";
                    }

                    break;
                }
                case "TelegramUsername":
                {
                    if (string.IsNullOrEmpty(TelegramUsername))
                    {
                        result = "Поле обязательно для заполнения";
                    }

                    break;
                }
                case "AdditionalMobile":
                {
                    if (string.IsNullOrEmpty(AdditionalMobile))
                    {
                        result = "Поле обязательно для заполнения";
                    }

                    break;
                }
                case "StartWork":
                {
                    if (StartWork == default)
                    {
                        result = "Поле обязательно для заполнения";
                    }

                    break;
                }
                case nameof(EndWork):
                {
                    if (EndWork == default)
                    {
                        result = "Поле обязательно для заполнения";
                    }

                    break;
                }
                case "OrganizationId":
                {
                    if (OrganizationId is null or 0)
                    {
                        result = "Поле обязательно для заполнения";
                    }

                    break;
                }
                case "ZoneId":
                {
                    if (ZoneId is null or 0)
                    {
                        result = "Поле обязательно для заполнения";
                    }

                    break;
                }
                case "ScheduleId":
                {
                    if (ScheduleId is null or 0)
                    {
                        result = "Поле обязательно для заполнения";
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