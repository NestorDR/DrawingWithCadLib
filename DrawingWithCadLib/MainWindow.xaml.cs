using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;

namespace DrawingWithCadLib
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Private variables
        // Main window's view model class
        private readonly MainWindowViewModel _viewModel;
        #endregion

        public MainWindow()
        {
            InitializeComponent();
            
            StatusBarTextBlock.Text = "Validating WWW license...";
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
            {
                for (int i = 0; i < 50; i++)
                {
                    if (((App)Application.Current).WwwLicenseValidated) break;
                    Thread.Sleep(100);
                }
                StatusBarTextBlock.Text = "WWW license validation completed";
            }));
            
            // Connect to instance of the view model created by the XAML
            _viewModel = (MainWindowViewModel)this.Resources["ViewModel"];
            _viewModel.NumberOfTracks = MainGrid.ColumnDefinitions.Count;
            _viewModel.PropertyChanged += OnPropertyChanged;
            
            // Add an event handler to get notified after text box changes
            AddEventHandler(MainGrid);
        }

        private void AddEventHandler(Panel panel)
        {
            foreach (UIElement child in panel.Children)
            {
                switch (child)
                {
                    case TextBox txt:
                        txt.TextChanged += TextBox_OnTextChanged;
                        break;
                    case Panel childPanel:
                        AddEventHandler(childPanel);
                        break;
                }
            }
        }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            string sampleDrawingFileToInsert = Path.Combine(
                Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location) ?? string.Empty, 
                "SamplesToInsert", 
                "compass.dxf");

            if (File.Exists(sampleDrawingFileToInsert)) _viewModel.Filename = sampleDrawingFileToInsert;
        }

        private void TextBox_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            _viewModel.HasChanges = true;
        }

        private void DxfCanvas_OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Calculate center coordinates for the shapes
            double trackWidth = DxfCanvas.ActualWidth / _viewModel.NumberOfTracks;
            int currentTrack = 1;
                
            bool initializeShapes = _viewModel.Circle.XCoordinate == null;
            if (initializeShapes)
            {
                // On first run initialize both coordinates: X and Y
                double centerYCoordinate = DxfCanvas.ActualHeight * 0.75;
                _viewModel.Circle.SetCoordinates(trackWidth * (currentTrack - 0.5), centerYCoordinate);
                _viewModel.Rectangle.SetCoordinates(trackWidth * (++currentTrack - 0.5), centerYCoordinate);
                _viewModel.RoundedRectangle.SetCoordinates(trackWidth * (++currentTrack - 0.5), centerYCoordinate);
                _viewModel.Slot.SetCoordinates(trackWidth * (++currentTrack - 0.5), centerYCoordinate);
            }
            else
            {
                // Only update X-Coordinate
                _viewModel.Circle.XCoordinate = trackWidth * (currentTrack - 0.5);
                _viewModel.Rectangle.XCoordinate = trackWidth * (++currentTrack - 0.5);
                _viewModel.RoundedRectangle.XCoordinate = trackWidth * (++currentTrack - 0.5);
                _viewModel.Slot.XCoordinate = trackWidth * (++currentTrack - 0.5);
            }
        }

        private void DrawButton_OnClick(object sender, RoutedEventArgs e)
        {
            this.Cursor = Cursors.Wait;
            try
            {
                StatusBarTextBlock.Text = "Drawing...";
                Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
                {
                    _viewModel.DrawLayout(DxfCanvas);
                    DrawButton.IsEnabled = true;
                    StatusBarTextBlock.Text = "Ready";
                }));
            }
            catch (Exception)
            {
                this.Cursor = null;
                throw;
            }
            _viewModel.HasChanges = false;
            this.Cursor = null;
        }

        private void ExportPngButton_OnClick(object sender, RoutedEventArgs e)
        {
            this.Cursor = Cursors.Wait;
            try
            {
                StatusBarTextBlock.Text = "Exporting file...";
                ExportPngButton.IsEnabled = false;
                Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
                {
                    _viewModel.ExportToPng();
                    ExportPngButton.IsEnabled = true;
                    StatusBarTextBlock.Text = "Ready";
                }));
            }
            catch (Exception)
            {
                this.Cursor = null;
                throw;
            }
            _viewModel.HasChanges = false;
            this.Cursor = null;
        }

        private void ClearFileButton_OnClick(object sender, RoutedEventArgs e)
        {
            _viewModel.Filename = string.Empty;
        }

        private void SelectFileButton_OnClick(object sender, RoutedEventArgs e)
        {
            this.Cursor = Cursors.Wait;
            OpenFileDialog fileDialog = new()
            {
                CheckFileExists = true,
                Filter = "DXF files (*.dxf)|*.dxf|DWG files (*.dwg)|*.dwg|All files (*.*)|*.*",
                FilterIndex = 0,
                Title = "Select the file to insert",
            };
            if (fileDialog.ShowDialog() == true) 
                _viewModel.Filename = fileDialog.FileName;
            this.Cursor = null;
        }

        private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(_viewModel.Filename):
                    bool hasFile = !string.IsNullOrWhiteSpace(_viewModel.Filename);
                    ClearFileButton.IsEnabled = hasFile;
                    ExportPngButton.IsEnabled = hasFile;
                    return;
            }
        }
    }
}
