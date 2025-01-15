using System.Windows;
using GolfClubSystem.Data;
using GolfClubSystem.Models;
using GolfClubSystem.Views.WorkersWindow;

namespace GolfClubSystem.Views.UserControlsViews.AdminControlsViews;

public partial class AddEditZoneWindow : Window
{
    public Zone Zone { get; set; }
    public WorkerType ZoneType { get; set; }
    public bool IsEnable { get; set; }


    private readonly UnitOfWork _unitOfWork = new();

    public AddEditZoneWindow(Zone? zone, bool isEnable = true)
    {
        IsEnable = isEnable;
        InitializeComponent();

        if (zone is not null)
        {
            Zone = zone;
            ZoneType = WorkerType.Edit;
        }
        else
        {
            Zone = new Zone();
            ZoneType = WorkerType.Add;
        }
        
        DataContext = this;
    }

    private async void ButtonBase_OnClick(object sender, RoutedEventArgs e)
    {
        switch (ZoneType)
        {
            case WorkerType.Add:
                await _unitOfWork.ZoneRepository.AddAsync(Zone);
                break;
            case WorkerType.Edit:
            {
                var currentZone = _unitOfWork.ZoneRepository.GetAllAsync().Where(w => w.DeletedAt == null)
                    .FirstOrDefault(w => w.Id == Zone.Id);

                if (currentZone is not null)
                {
                    currentZone.Name = Zone.Name;
                    currentZone.EnterIp = Zone.EnterIp;
                    currentZone.ExitIp = Zone.ExitIp;
                    currentZone.Login = Zone.Login;
                    currentZone.Password = Zone.Password;
                    await _unitOfWork.ZoneRepository.UpdateAsync(currentZone);
                }

                break;
            }
        }

        await _unitOfWork.SaveAsync();
        Close();
    }
}