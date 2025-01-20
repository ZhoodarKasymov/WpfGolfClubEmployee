using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using GolfClubSystem.Data;
using GolfClubSystem.Models;
using GolfClubSystem.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace GolfClubSystem.Views.MainWindows
{
    public partial class SendNotifyWindow : Window, INotifyPropertyChanged
    {
        private readonly IConfiguration _configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();
        
        private readonly UnitOfWork _unitOfWork = new();
        private readonly TelegramService _telegramService;
        public ObservableCollection<Worker> Workers { get; set; } = new();

        public string Description { get; set; } =
            "Вам необходимо прибыть в вашу Зону в течение 20 минут. \nПо прибытию пройдите биометрию через терминал.";
        
        private string _searchText;

        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                FilterItems();
            }
        }

        public SendNotifyWindow()
        {
            InitializeComponent();
            DataContext = this;
            UpdateWorkers();
            
            var token = _configuration.GetSection("Token").Value;
            _telegramService = new TelegramService(token);
        }

        private void FilterItems()
        {
            if (string.IsNullOrEmpty(SearchText))
            {
                UpdateWorkers();
            }
            else
            {
                Workers = new ObservableCollection<Worker>(Workers.Where(i => i.FullName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)));
            }

            OnPropertyChanged(nameof(Workers));
        }

        private void UpdateWorkers()
        {
            var workers = _unitOfWork.WorkerRepository.GetAll()
                .Where(w => w.DeletedAt == null)
                .AsNoTracking()
                .ToList();
            
            Workers = new ObservableCollection<Worker>(workers);
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
            var selectedWorker = Workers.Where(w => w.IsSelected).ToList();

            if (!selectedWorker.Any())
            {
                MessageBox.Show("Работники не выбраны!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            foreach (var worker in selectedWorker)
            {
               await _telegramService.SendMessageByUsernameAsync(worker.Id, Description);
            }

            MessageBox.Show("Запрос отправлен", "Успех", MessageBoxButton.OK, MessageBoxImage.None);
            Close();
        }
    }
}
