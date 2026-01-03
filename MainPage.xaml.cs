using System.ComponentModel;
using Microsoft.Maui.Controls;
using PooSweep.ViewModels;

namespace PooSweep;

public partial class MainPage : ContentPage
{
    private MainViewModel? _viewModel;

    public MainPage()
    {
        InitializeComponent();

        if (BindingContext is MainViewModel vm)
        {
            _viewModel = vm;
            vm.PropertyChanged += OnVmPropertyChanged;
            UpdateGridLayout(vm.Columns);
        }

        BindingContextChanged += (_, _) =>
        {
            if (_viewModel is not null)
                _viewModel.PropertyChanged -= OnVmPropertyChanged;

            if (BindingContext is MainViewModel vm2)
            {
                _viewModel = vm2;
                vm2.PropertyChanged += OnVmPropertyChanged;
                UpdateGridLayout(vm2.Columns);
            }
        };
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.Columns))
        {
            if (sender is MainViewModel vm)
            {
                UpdateGridLayout(vm.Columns);
            }
        }
        
        if (e.PropertyName == nameof(MainViewModel.LastAnnouncement))
        {
            if (sender is not MainViewModel vm) return;
            if (string.IsNullOrWhiteSpace(vm.LastAnnouncement)) return;

            SemanticScreenReader.Announce(vm.LastAnnouncement);
        }
    }

    private void UpdateGridLayout(int columns)
    {
        System.Diagnostics.Debug.WriteLine($"[MainPage] UpdateGridLayout called with columns = {columns}");
        GameGrid.ItemsLayout = new GridItemsLayout(columns, ItemsLayoutOrientation.Vertical)
        {
            HorizontalItemSpacing = 0,
            VerticalItemSpacing = 0
        };
    }
}
