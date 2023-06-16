using System;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Media;
//
using WW.Cad.Base;
using WW.Cad.Drawing;
using WW.Cad.Drawing.Wpf;
using WW.Cad.Model;
using WW.Cad.Model.Entities;
using WW.Cad.Model.Tables;
using WW.Drawing;
using WW.Math;


namespace DrawingWithCadLib;

internal class MainWindowViewModel : BaseClass
{
    private double _canvasHeight;
    private double _canvasWidth;
    // This example does not use scaling for the main drawing, because it does not support Zoom
    private readonly double _scaling = 1d;

    public MainWindowViewModel()
    {
        // Drawings included
        double shapeLength = 40;
        double shapeHeight = shapeLength / 2;

        _circle = new ShapeModel(shapeHeight);
        _rectangle = new ShapeModel(shapeLength, shapeHeight);
        _roundedRectangle = new ShapeModel(shapeLength, shapeHeight, 2);
        _slot = new ShapeModel(shapeLength, shapeHeight, shapeHeight / 2);
    }

    #region Properties

	private ShapeModel _circle;
    public ShapeModel Circle
	{
		get => _circle;
		set => SetProperty(ref _circle, value);
	}

	private ShapeModel _rectangle;
	public ShapeModel Rectangle
	{
		get => _rectangle;
		set => SetProperty(ref _rectangle, value);
	}

	private ShapeModel _roundedRectangle;
	public ShapeModel RoundedRectangle
	{
		get => _roundedRectangle;
		set => SetProperty(ref _roundedRectangle, value);
	}

	private ShapeModel _slot;

    public ShapeModel Slot
	{
		get => _slot;
		set => SetProperty(ref _slot, value);
	}

    private bool _showContainer;
    public bool ShowContainer
    {
        get => _showContainer;
        set => SetProperty(ref _showContainer, value);
    }

    private bool _showOriginalOnlyIfFits;
    public bool ShowOriginalOnlyIfFits
    {
        get => _showOriginalOnlyIfFits;
        set => SetProperty(ref _showOriginalOnlyIfFits, value);
    }

    private bool _trialWarningAutoClose = true;
    public bool TrialWarningAutoClose
    {
        get => _trialWarningAutoClose;
        set => SetProperty(ref _trialWarningAutoClose, value);
    }

    private bool _hasChanges;
    public override bool HasChanges
    {
        get => _hasChanges || _circle.HasChanges || _rectangle.HasChanges || _roundedRectangle.HasChanges || _slot.HasChanges;
        set
        {
            if (_hasChanges == value) return;
            
            _hasChanges = value;
            Circle.HasChanges = value;
            Rectangle.HasChanges = value;
            RoundedRectangle.HasChanges = value;
            Slot.HasChanges = value;
            
            NotifyPropertyChanged();
        }
    }

    public int NumberOfTracks { get; set; }
    
    private string _filename = string.Empty;
    public string Filename
    {
        get => _filename;
        set => SetProperty(ref _filename, value);
    }

    private int _sourceType = 1;
    public int DrawingSourceType
    {
        get => _sourceType;
        set => SetProperty(ref _sourceType, value);
    }

    private double _rotationDegrees;
    public double RotationDegrees
    {
        get => _rotationDegrees;
        set => SetProperty(ref _rotationDegrees, value);
    }

    public DxfModel? DxfModel { get; set; }

    private Bounds3D? Bounds { get; set; }
    
    private WpfWireframeGraphics3D? WpfGraphics { get; set; }

    #endregion Properties

    #region Methods

    public void DrawLayout(Canvas dxfCanvas)
    {
        if (DxfModel == null)
        {
            // To try automatically close the warning message box of the trial version
            if (_trialWarningAutoClose) CadLibService.StartTimer();
            // Create empty model
            DxfModel = new DxfModel();
        }
        else
        {
            // Clear previous drawings
            dxfCanvas.Children.Clear();
            DxfModel.Entities.Clear();
            DxfModel.ActiveLayout = null;
        }

        // Get Canvas size
        _canvasHeight = dxfCanvas.ActualHeight;
        _canvasWidth = dxfCanvas.ActualWidth;
        
        if (_showContainer)
        {
            // Add a drawings container with the same size as the Canvas
            DxfLwPolyline container = CadLibService.CreateRectangle(0, 0, _canvasWidth, _canvasHeight);
            DxfModel.Entities.Add(container);
        }

        #region Calculate the model's bounds

        Bounds = _showContainer 
            // 1º - Use the "BoundsCalculator" class to calculate the bounds or dimensions of CAD elements or objects.
            ? CadLibService.GetBounds(DxfModel) 
            // Or - Set the bounds directly using the Canvas size
            : new Bounds3D(Point3D.Zero, new Point3D(_canvasWidth, _canvasHeight, 0));

        // For model space this corrects for the RenderTransform scaling of the stroke width (line width in model space is independent of scaling).
        // For paper space the RenderTransform should scale the stroke width, so don't adjust the DPI value (but in this example is not used the paper space)
        Vector3D modelDelta = this.Bounds.Delta;

        #endregion Calculate the model's bounds

        #region Populate DXF model
        
        AddSimpleShapes();

        InsertDrawing(modelDelta);

        #endregion Populate DXF model

        #region Based on model's bounds, determine a proper dots per inch
        
        if (WpfGraphics == null)
        {
            // 2º - GraphicsConfig allows customization (style, quality or transparency) and control (redraw or interaction with CAD elements)
            //      of various graphic aspects in a CAD visualization.
            System.Windows.Media.Color transparent = System.Windows.Media.Colors.Transparent;
            GraphicsConfig graphicsConfig = new(false, new ArgbColor(transparent.R, transparent.G, transparent.B));

            // 3º - The "WpfWireframeGraphics3D" class is used to enable the visualization and manipulation of CAD elements in a WPF UI.
            //      It provides a convenient way to graphically represent 3D models, interact with them, and display real-time updates.
            WpfGraphics = new WpfWireframeGraphics3D
            {
                Config = graphicsConfig
            };

            // DotsPerInch is normally around 90-100 for most displays, and perhaps 300 or 600 for printers
            // [ 100 is roughly the dots per inch value of the screen ] % [ Correction factor because the RenderTransform scales the stroke width ]
            WpfGraphics.Config.DotsPerInch = 100d / ScaleFactor(modelDelta);
        }

        #endregion Based on model's bounds, determine a proper dots per inch

        #region Draw DXF model

        // Center the drawables around (0, 0) to prevent floating point accuracy problems in case the drawing's
        // center is far away from the origin.
        Matrix4D centerTransform = CadLibService.ToTranslateCenter(Bounds);
        
        // Creates the WPF framework elements for specified model.
        WpfGraphics.CreateDrawables(DxfModel, centerTransform);
        
        // Transforms the bounds by specified transform.
        Bounds.Transform(centerTransform);

        WpfGraphics.Draw(dxfCanvas.Children);
        
        UpdateRenderTransform(dxfCanvas);
        
        #endregion Draw DXF model
    }

    /// <summary>
    /// Add shapes included to the CAD model
    /// </summary>
    private void AddSimpleShapes()
    {
        if (DxfModel == null) return;

        DxfLwPolyline polygon;

        // Add a circle
        if (_circle.IsDrawable)
        {
            DxfCircle circle = CadLibService.CreateCircle(_circle.XCoordinate!.Value, _circle.YCoordinate!.Value,
                _circle.Radius!.Value);
            DxfModel.Entities.Add(circle);
        }

        // Add a rectangle
        if (_rectangle.IsDrawable)
        {
            polygon = CadLibService.CreateRectangle(_rectangle.Left!.Value, _rectangle.Bottom!.Value,
                _rectangle.Length!.Value, _rectangle.Height!.Value);
            DxfModel.Entities.Add(polygon);
        }

        // Add a rounded rectangle
        if (_roundedRectangle.IsDrawable)
        {
            polygon = CadLibService.CreateRoundedRectangle(_roundedRectangle.Left!.Value, _roundedRectangle.Bottom!.Value,
                _roundedRectangle.Length!.Value, _roundedRectangle.Height!.Value, _roundedRectangle.Radius!.Value);
            DxfModel.Entities.Add(polygon);
        }

        // Add a slot
        if (_slot.IsDrawable)
        {
            polygon = CadLibService.CreateSlot(_slot.Left!.Value, _slot.Bottom!.Value,
                _slot.Length!.Value, _slot.Height!.Value, _slot.Radius!.Value);
            DxfModel.Entities.Add(polygon);
        }
    }

    /// <summary>
    /// Insert drawing read from a DXF file
    /// </summary>
    /// <param name="viewPortDelta">Vector of the three-dimensional space where the drawing will be inserted, is only used to check whether it is possible to display the drawing in its original size in that space.</param>
    private void InsertDrawing(Vector3D viewPortDelta)
    {
        if (DxfModel == null 
            || string.IsNullOrWhiteSpace(_filename) 
            || DrawingSourceType > CadLibService.SOURCE_DRAWING_BINARY) return;

        DxfBlock? dxfBlock =
            CadLibService.LoadAsBlock(_filename, _sourceType, _rotationDegrees, out Bounds3D? bounds, _trialWarningAutoClose);
        if (dxfBlock == null || bounds == null) return;

        bool showOriginalDrawing;
        if (_showOriginalOnlyIfFits)
        {
            // Show original drawing only if it fits into the view port (canvas)
            Vector3D delta = bounds.Delta;
            showOriginalDrawing = delta.X <= viewPortDelta.X
                                  && delta.Y <= viewPortDelta.Y
                                  && delta.Z <= viewPortDelta.Z;
        }
        else
        {
            // Ignore whether or not the original drawing fits into the viewport, show always
            showOriginalDrawing = true;
        }

        if (showOriginalDrawing)
        {
            // Insert the drawing in original size at the origin of coordinates (0, 0, 0)
            DxfInsert dxfInsert = new(dxfBlock);
            DxfModel.Entities.Add(dxfInsert);
        }

        const int LOWER_HALF = 0; // I want to put the drawing in the lower half (arbitrarily selected)
        double trackWidth = _canvasWidth / this.NumberOfTracks;
        double halfTrackHeight = _canvasHeight * 0.5;

        int? firstTrack = null;
        foreach (int track in new[] { 2, 3, 4 })
        {
            firstTrack ??= track;

            Vector3D targetAreaDelta = new(trackWidth, halfTrackHeight * firstTrack.Value / track, 0d);

            double rotationDegrees = _rotationDegrees;
            DxfInsert insert = Scale(dxfBlock, bounds, targetAreaDelta, rotationDegrees);

            Point2D centerCoordinate = new(targetAreaDelta.X * (track - 0.5), halfTrackHeight * (LOWER_HALF + 0.5));
            double displacement =
                (targetAreaDelta.X < targetAreaDelta.Y ? targetAreaDelta.X : targetAreaDelta.Y) * 0.5;
            double left = centerCoordinate.X - displacement;
            double bottom = centerCoordinate.Y - displacement;
            Point3D insertionPoint = new(left, bottom, 0);

            insert.InsertionPoint = insertionPoint;
            DxfModel.Entities.Add(insert);
        }
    }

    /// <summary>
    /// Scales the DxfBlock object to fit in target area
    /// </summary>
    private static DxfInsert Scale(DxfBlock drawingBlock, Bounds3D bounds, Vector3D targetAreaDelta, double rotationDegrees)
    {
        // DxfInsert is the entity required to do the transformations
        DxfInsert dxfInsert = new(drawingBlock);
        
        TransformConfig transformConfig = new();

        Matrix4D matrix = CadLibService.ToScale2D(bounds, targetAreaDelta);

        const bool APPLY_ROTATION = false;  // I was not able to apply the rotation without translation of the result
        if (rotationDegrees != 0d && APPLY_ROTATION) matrix *= CadLibService.ToRotateZ(rotationDegrees);

        // Transform entities (scale)
        dxfInsert.TransformMe(transformConfig, matrix);

        // Transform other attributes such as Text
        foreach (DxfAttribute attribute in dxfInsert.Attributes)
            attribute.TransformMe(transformConfig, matrix);

        return dxfInsert;
    }

    private double ScaleFactor(Vector3D delta)
    {
        return Math.Min(_canvasWidth / delta.X, _canvasHeight / delta.Y);
    }

    private void UpdateRenderTransform(Canvas dxfCanvas)
    {
        // Since zoom will be not supported, then it is not necessary to adjust the GraphicsConfig values.

        MatrixTransform baseTransform = DxfUtil.GetScaleWMMatrixTransform(
            (Point2D)Bounds!.Corner1,
            (Point2D)Bounds.Corner2,
            (Point2D)Bounds.Center,
            new Point2D(1d, _canvasHeight),
            new Point2D(_canvasWidth, 1d),
            new Point2D(0.5d * _canvasWidth, 0.5d * _canvasHeight )
        );

        TransformGroup transformGroup = new();
        transformGroup.Children.Add(baseTransform);
        transformGroup.Children.Add(new TranslateTransform
        {
            X = -_canvasWidth / 2d,
            Y = -_canvasHeight / 2d
        });
        transformGroup.Children.Add(new ScaleTransform
        {
            ScaleX = _scaling,
            ScaleY = _scaling
        });
        transformGroup.Children.Add(new TranslateTransform
        {
            X = _canvasWidth / 2d,
            Y = _canvasHeight / 2d
        });

        /* Also, drag and drop translation will not be supported in this app
        transformGroup.Children.Add(new TranslateTransform
        {
            X = _translation.X * _canvasWidth / 2d,
            Y = -_translation.Y * _canvasHeight / 2d
        });
        */

        dxfCanvas.RenderTransform = transformGroup;
    }

    public void ExportToPng()
    {
        // Visit https://www.woutware.com/Forum/Topic/1962
        //       https://www.woutware.com/Forum/Topic/1766
        string? folder = CadLibService.ExportToPng(_filename, _trialWarningAutoClose);

        try
        {
            if (!string.IsNullOrWhiteSpace(folder)) 
                Process.Start("explorer.exe", folder);
        }
        catch (Exception e)
        {
            Debug.WriteLine($"Error occurred opening the target folder {folder}: {e.Message}");
        }
    }

    #endregion Methods

}