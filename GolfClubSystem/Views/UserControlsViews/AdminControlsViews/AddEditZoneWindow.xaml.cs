using System.Windows;
using GolfClubSystem.Data;
using GolfClubSystem.Models;
using GolfClubSystem.Services;
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
        var terminalService = new TerminalService(Zone.Login, Zone.Password);
        var allActiveWorkers = _unitOfWork.WorkerRepository.GetAll()
            .Where(w => w.DeletedAt == null && w.EndWork >= DateTime.Now)
            .ToList();
        
        switch (ZoneType)
        {
            case WorkerType.Add:
                var request = new UserInfoDeleteRequest
                {
                    UserInfoDelCond = new UserInfoDelCond
                    {
                        EmployeeNoList = []
                    }
                };
                await terminalService.DeleteUsersAsync(request, Zone.EnterIp);
                await terminalService.DeleteUsersAsync(request, Zone.ExitIp);

                foreach (var worker in allActiveWorkers)
                {
                    await UpdateAddTerminalEmployee(worker, worker.PhotoPath!, Zone.EnterIp);
                    await UpdateAddTerminalEmployee(worker, worker.PhotoPath!, Zone.ExitIp);
                }
                
                await _unitOfWork.ZoneRepository.AddAsync(Zone);
                break;
            case WorkerType.Edit:
            {
                var currentZone = _unitOfWork.ZoneRepository.GetAll().Where(w => w.DeletedAt == null)
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
        
        Close();
        
        async Task UpdateAddTerminalEmployee(Worker worker, string photoPath, string ip)
        {
            var terminalUserAddedEnter = await terminalService.AddUserInfoAsync(worker, ip);
            if (terminalUserAddedEnter)
            {
                await terminalService.AddUserImageAsync(worker, ip);

                if (worker.CardNumber != null)
                {
                    await terminalService.AddCardInfoAsync(worker, ip);
                }
            }
        }
    }
}