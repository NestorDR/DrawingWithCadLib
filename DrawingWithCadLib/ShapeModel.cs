using System;

namespace DrawingWithCadLib;

/// <summary>
/// Type of shapes
/// </summary>
internal enum ShapeType
{
    Circle,
    Rectangle,
    RoundedRectangle,
    Slot,
}

/// <summary>
/// Shape class
/// </summary>
internal class ShapeModel : BaseClass
{
    public ShapeModel(ShapeType shapeType)
    {
        _shapeType = shapeType;
    }

    /// <summary>
    ///  New circle
    /// </summary>
    public ShapeModel(double? radius) 
        : this(ShapeType.Circle)
    {
        _radius = radius;
    }

    /// <summary>
    /// New rectangle
    /// </summary>
    public ShapeModel(double? length, double? height) 
        : this(ShapeType.Rectangle)
    {
        _length = length;
        _height = height;
    }

    /// <summary>
    /// New rounded rectangle, or when the radius value = half height, new slot
    /// </summary>
    public ShapeModel(double? length, double? height, double? radius) 
        : this(ShapeType.RoundedRectangle)
    {
        if (RadiusIsHalfHeight(height, radius)) _shapeType = ShapeType.Slot;
        _length = length;
        _height = height;
        _radius = radius;
    }

    #region Properties

    private ShapeType _shapeType;
    public ShapeType ShapeType
    {
        get => _shapeType;
        set
        {
            if (!SetProperty(ref _shapeType, value)) return;
            switch (_shapeType)
            {
                case ShapeType.Circle:
                    Length = null;
                    Height = null;
                    break;
                case ShapeType.Rectangle:
                    Radius = null;
                    break;
                case ShapeType.Slot:
                    Radius = Height / 2;
                    break;
            }
        }
    }

    private double? _xCoordinate;
    public double? XCoordinate
    {
        get => _xCoordinate;
        set => SetProperty(ref _xCoordinate, value);
    }

    private double? _yCoordinate;
    public double? YCoordinate
    {
        get => _yCoordinate;
        set => SetProperty(ref _yCoordinate, value);
    }

    private bool HasCoordinates => _xCoordinate.HasValue && _yCoordinate.HasValue;

    private double? _radius;
    public double? Radius
    {
        get => _radius;
        set
        {
            if (_shapeType == ShapeType.Rectangle) value = null;
            SetProperty(ref _radius, value);
        }
    }

    private double? _length;
    public double? Length
    {
        get => _length;
        set
        {
            if (_shapeType == ShapeType.Circle) value = null;
            SetProperty(ref _length, value);
        }
    }

    private double? _height;
    public double? Height
    {
        get => _height;
        set
        {
            if (_shapeType == ShapeType.Circle) value = null;
            if (SetProperty(ref _height, value) && _shapeType == ShapeType.Slot)
                this.Radius = _height / 2;
        }
    }

    public double? Bottom => _yCoordinate - (_shapeType == ShapeType.Circle ? _radius : _height / 2);

    public double? Left => _xCoordinate - (_shapeType == ShapeType.Circle ? _radius : _length / 2);

    public bool IsSized =>
        _shapeType switch
        {
            ShapeType.Circle => _radius > 0,
            ShapeType.Rectangle => _length > 0 && _height > 0,
            ShapeType.RoundedRectangle => _length > 0 && _height > 0 && _radius >= 0.01,
            ShapeType.Slot => this.Length > 0 && RadiusIsHalfHeight(_height, _radius),
            _ => false
        };

    public bool IsDrawable => this.HasCoordinates && this.IsSized;

    #endregion

    #region Methods

    private static bool RadiusIsHalfHeight(double? height, double? radius) =>
        height > 0 && radius > 0 && Math.Abs(height.Value / 2 - radius.Value) < 0.0001;


    public void SetCoordinates(double xCoordinate, double yCoordinate)
    {
        this.XCoordinate = xCoordinate;
        this.YCoordinate = yCoordinate;
    }

    #endregion
}