using System.Windows;
using GolfClubSystem.Data;
using GolfClubSystem.Models;
using GolfClubSystem.Services;
using GolfClubSystem.Views.WorkersWindow;
using Serilog;

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

                try
                {
                    await terminalService.DeleteUsersAsync(request, Zone.EnterIp);
                    await terminalService.DeleteUsersAsync(request, Zone.ExitIp);
                    await terminalService.DeleteUsersAsync(request, Zone.NotifyIp);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("При удалении из терминала старых работников произошла ошибка", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    Log.Error(ex, ex.Message);
                    return;
                }

                try
                {
                    foreach (var worker in allActiveWorkers)
                    {
                        await UpdateAddTerminalEmployee(worker, worker.PhotoPath!, Zone.EnterIp);
                        await UpdateAddTerminalEmployee(worker, worker.PhotoPath!, Zone.ExitIp);
                        await UpdateAddTerminalEmployee(worker, worker.PhotoPath!, Zone.NotifyIp);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("При добавлении существующих работников в терминалы произошла ошибка", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    Log.Error(ex, ex.Message);
                    return;
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
                    currentZone.NotifyIp = Zone.NotifyIp;
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
                worker.PhotoPath = photoPath;
                await terminalService.AddUserImageAsync(worker, ip);

                if (worker.CardNumber != null)
                {
                    await terminalService.AddCardInfoAsync(worker, ip);
                }
            }
        }
    }
}