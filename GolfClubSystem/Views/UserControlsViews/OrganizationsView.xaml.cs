using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using GolfClubSystem.Data;
using GolfClubSystem.Models;
using GolfClubSystem.ViewModels;
using MaterialDesignThemes.Wpf;

namespace GolfClubSystem.Views.UserControlsViews;

public partial class OrganizationsView : UserControl
{
    public OrganizationsView()
    {
        InitializeComponent();
        DataContext = new OrganizationsViewModel();
    }
}