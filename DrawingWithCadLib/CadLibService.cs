using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Timers;
using winForms = System.Windows.Forms;
//
using WW;
using WW.Cad.Drawing;
using WW.Cad.IO;
using WW.Cad.Model;
using WW.Cad.Model.Entities;
using WW.Cad.Model.Tables;
using WW.Math;
using WW.Cad.Drawing.GDI;
using WW.Cad.Base;

namespace DrawingWithCadLib;

internal static class CadLibService
{
    public const int SOURCE_DRAWING_FILE = 0;
    public const int SOURCE_DRAWING_BINARY = 1;

    public static void Initialize(string? license = null)
    {
        // For this application to work you will need a trial license.
        //
        // The strong name key file "MyKeyPair.snk" linked in the project is not present in the repository, 
        // you should generate your own strong name key and keep it private.
        //
        // 1) You can generate a strong name key with the following command in the Visual Studio command prompt:
        //     sn -k MyKeyPair.snk
        //
        // 2) The next step is to extract the public key file from the strong name key (which is a key pair):
        //     sn -p MyKeyPair.snk MyPublicKey.snk
        //
        // 3) Display the public key token for the public key: 	
        //     sn -t MyPublicKey.snk
        //
        // 4) Go to the project properties Singing tab, and check the "Sign the assembly" checkbox, 
        //    and choose the strong name key you created.
        //
        // 5) Register and get your trial license from https://www.woutware.com/SoftwareLicenses.
        //    Enter your strong name key public key token that you got at step 3.
        //
        // 6) The software has to be initialized with the trial license string at the start of the program by calling:
        //    WWLicense.SetLicense("<Insert your trial license string here>");
        //

        if (string.IsNullOrEmpty(license))
            license = File.ReadAllText(@"cadlib.license");

        WWLicense.SetLicense(license);
    }

    /// <summary>
    /// Starts timer and sets Interval property to the specified number of milliseconds,
    /// to try automatically close the warning message box of the trial version
    /// </summary>
    public static void StartTimer(double interval = 150)
    {
        Timer timer = new(interval);
        timer.Elapsed += CloseMessageBox;
        timer.AutoReset = false;
        timer.Start();
    }

    /// <summary>
    /// Tries to close the trial version warning message box
    /// </summary>
    private static void CloseMessageBox(object? sender, ElapsedEventArgs e)
    {
        if (sender is not Timer timer) return;
        try
        {
            winForms.SendKeys.SendWait("{Esc}");
            timer.Stop();
        }
        catch (Exception)
        {
            // Nothing
        }
    }

    #region Load

    /// <summary>
    /// Reads DXF or DWG files
    /// </summary>
    private static DxfModel? LoadModel(string filename, bool trialWarningAutoClose)
    {
        DxfModel? model;
        try
        {
            if (trialWarningAutoClose) StartTimer();
            model = CadReader.Read(filename);
        }
        catch (Exception e)
        {
            Debug.WriteLine($"Error occurred loading the model: {e.Message}");
            throw;
        }

        return model;
    }

    /// <summary>
    /// Reads the model from a file, as a block ready for insertion (relocated relative to the origin of coordinates (0, 0, 0))
    /// </summary>
    public static DxfBlock? LoadAsBlock(string filename, int sourceType, double rotationDegrees,
        out Bounds3D? bounds, bool trialWarningAutoClose)
    {
        bounds = null;

        DxfModel? dxfModel = null;

        try
        {
            if (trialWarningAutoClose) StartTimer();
            switch (sourceType)
            {
                case SOURCE_DRAWING_FILE:
                    // Read drawing file
                    dxfModel = LoadModel(filename, trialWarningAutoClose);
                    break;
                case SOURCE_DRAWING_BINARY:
                    // Simulates obtaining the drawing from an SQL database field
                    byte[] fileContent = File.ReadAllBytes(filename);
                    dxfModel = DxfReader.Read(new MemoryStream(fileContent));
                    break;
                default:
                    return null;
            }
        }
        catch (Exception)
        {
            /* Ignore */
        }

        if (dxfModel == null) return null;

        string drawingName = Path.GetFileName(filename);
        string[] excludedEntities = { "DIMENSION", "MTEXT" };
        string[] excludedLayers = { "AM_5", "AM_7" };
        DxfModel dxfClonedModel = CloneAndSimplify(dxfModel, excludedEntities, excludedLayers, drawingName);

        // Instantiate block where the entities will be copied
        string blockName = Path.GetFileNameWithoutExtension(filename);
        DxfBlock block = new(blockName);

        bounds = GetBounds(dxfClonedModel);     // Get original bounds

        // Instantiate matrix needed to translate drawings to the coordinate origin
        Matrix4D matrix = ToTranslatePointZero(bounds);   
        // Upgrade matrix to rotate, if that is required
        if (rotationDegrees != 0d) matrix *= ToRotateZ(rotationDegrees);

        // Transformation configuration
        TransformConfig transformConfig = new();
        
        // Translate and rotate (applying the transform) and add to the block
        foreach (DxfEntity entity in dxfClonedModel.Entities)
        {
            // Relocate drawing relative to coordinates origin (0, 0, 0)
            entity.TransformMe(transformConfig, matrix);

            // Add entity to the resulting model and block
            block.Entities.Add(entity);
        }

        bounds = GetBounds(dxfClonedModel);      // Get new bounds after translation

        return block;
    }

    public static DxfModel CloneAndSimplify(DxfModel dxfSourceModel,
        string[] excludedEntities, string[] excludedLayers, string drawingName = "Nameless drawing")
    {
        Debug.WriteLine($"{drawingName}. Entities.Count: {dxfSourceModel.Entities.Count}");

        // Clone ignoring dimensions and texts, to avoid errors after transforms
        DxfModel dxfTargetModel = new();
        CloneContext cloneContext = new(dxfSourceModel, dxfTargetModel, ReferenceResolutionType.IgnoreMissing);
        int i = 0;
        foreach (DxfEntity entity in dxfSourceModel.Entities)
        {
            string entityType = entity.EntityType.ToUpper();
            string layerName = entity.Layer.ToString().ToUpper();
            Debug.WriteLine($"{++i}º EntityType: {entityType} - EntityLayer: {layerName}");

            if (excludedEntities.Contains(entityType) || excludedLayers.Contains(layerName)) continue;

            DxfEntity clonedEntity = (DxfEntity)entity.Clone(cloneContext);
            dxfTargetModel.Entities.Add(clonedEntity);
        }

        // Resolves references for all cloned objects if needed.
        // E.g. e hatch entity may reference a circle entity as its boundary.
        // After cloning both hatch and circle entities, this method will restore these references in the cloned objects.
        // If e.g. only the hatch was cloned, the relation will not be restored.
        cloneContext.ResolveReferences();

        Debug.WriteLine($"{drawingName} cloned and simplified. Entities.Count: {dxfTargetModel.Entities.Count}");

        return dxfTargetModel;
    }
    
    /// <summary>
    /// Uses a BoundsCalculator object to get the bounds or dimensions of the CAD model.
    /// </summary>
    public static Bounds3D GetBounds(DxfModel dxfModel)
    {
        BoundsCalculator boundsCalculator = new();
        boundsCalculator.GetBounds(dxfModel);
        return boundsCalculator.Bounds;
    }

    #endregion Load

    #region Transforms

    /// <summary>
    /// Gets a matrix to scale in 2D
    /// </summary>
    /// <param name="bounds">Drawing bounds</param>
    /// <param name="targetAreaDelta">Vector of the three-dimensional space where a drawing will be scaled</param>
    /// <returns></returns>
    public static Matrix4D ToScale2D(Bounds3D bounds, Vector3D targetAreaDelta)
    {
        return DxfUtil.GetScaleTransform(
            bounds.Corner1,
            bounds.Corner2,
            bounds.Center,
            Point3D.Zero,
            (Point3D)targetAreaDelta,
            new Point3D(0.5 * targetAreaDelta.X, 0.5 * targetAreaDelta.Y, 0)
        );
    }
    
    /// <summary>
    /// Gets a matrix to translate/transpose to the space center delimited by specified bounds 
    /// </summary>
    public static Matrix4D ToTranslateCenter(Bounds3D bounds)
    {
        return Transformation4D.Translation(Point3D.Zero - bounds.Center);
    }

    /// <summary>
    /// Gets a matrix to translate/transpose to the coordinate origin (0, 0, 0)
    /// </summary>
    public static Matrix4D ToTranslatePointZero(Bounds3D bounds)
    {
        return Transformation4D.Translation(-(Vector3D)bounds.Min);
    }

    /// <summary>
    /// Gets a matrix to rotate the specified degrees on the Z axis
    /// </summary>
    public static Matrix4D ToRotateZ(double rotationDegrees)
    {
        return Transformation4D.RotateZ(-rotationDegrees * Math.PI / 180d) *
               Transformation4D.RotateY(0) *
               Transformation4D.RotateX(0);
    }

    #endregion Transforms

    #region Create simple shapes
    /// <summary>
    /// Creates a circle for a CAD model from the center coordinate
    /// </summary>
    /// <param name="x">X coordinate for the circle center.</param>
    /// <param name="y">Y coordinate for the circle center.</param>
    /// <param name="radius">Circle radius.</param>
    /// <returns>A <see cref="DxfCircle"/>.</returns>
    public static DxfCircle CreateCircle(double x, double y, double radius)
    {
        DxfCircle circle = new(new Point2D(x, y), radius);
        return circle;
    }

    /// <summary>
    /// Creates a rectangle for a CAD model from the left bottom coordinate
    /// </summary>
    /// <param name="x">X coordinate for the left bottom vertex of the rectangle.</param>
    /// <param name="y">Y coordinate for the left bottom vertex of the rectangle.</param>
    /// <param name="length">Length of the base of the rectangle.</param>
    /// <param name="height">Length of the height of the rectangle.</param>
    /// <returns>A <see cref="DxfLwPolyline"/>.</returns>
    public static DxfLwPolyline CreateRectangle(double x, double y, double length, double height)
    {
        DxfLwPolyline rectangle = new();
        DxfLwPolyline.Vertex[] vertices =
        {
                new(x, y),
                new(x + length, y),
                new(x + length, y + height),
                new(x, y + height)
            };
        rectangle.Vertices.AddRange(vertices);
        rectangle.Closed = true;

        return rectangle;
    }

    /// <summary>
    /// Creates a rectangle with round corners for a CAD model from the left bottom coordinate
    /// </summary>
    /// <param name="x">X coordinate for the left bottom vertex of the rectangle.</param>
    /// <param name="y">Y coordinate for the left bottom vertex of the rectangle.</param>
    /// <param name="length">Length of the base of the rectangle.</param>
    /// <param name="height">Length of the height of the rectangle.</param>
    /// <param name="radius">Radius for round corners.</param>
    /// <returns>A <see cref="DxfLwPolyline"/>.</returns>
    public static DxfLwPolyline CreateRoundedRectangle(double x, double y, double length, double height, double radius)
    {
        double bulge = Math.Tan(Math.PI / 8); // bulge for CCW quarter arc, tan of one fourth of 90°

        DxfLwPolyline slot = new();
        DxfLwPolyline.Vertex[] vertices =
        {
                new(new Point2D(x + radius, y)),
                new(new Point2D(x + length - radius, y), bulge),
                new(new Point2D(x + length, y + radius)),
                new(new Point2D(x + length, y + height - radius), bulge),
                new(new Point2D(x + length - radius, y + height)),
                new(new Point2D(x + radius, y + height), bulge),
                new(new Point2D(x, y + height - radius)),
                new(new Point2D(x, y + radius), bulge)
            };
        slot.Vertices.AddRange(vertices);
        slot.Closed = true;

        return slot;
    }

    /// <summary>
    /// Creates a rectangle with round height side for a CAD model from the left bottom coordinate
    /// </summary>
    /// <param name="x">X coordinate for the left bottom vertex of the slot.</param>
    /// <param name="y">Y coordinate for the left bottom vertex of the slot.</param>
    /// <param name="length">Length of the base of the rectangle, or slot width.</param>
    /// <param name="height">Length of the height of the rectangle.</param>
    /// <param name="radius">Radius for round side (the height side).</param>
    /// <returns>A <see cref="DxfLwPolyline"/>.</returns>
    public static DxfLwPolyline CreateSlot(double x, double y, double length, double height, double radius)
    {
        double bulge = Math.Tan(Math.PI / 4); // bulge for CCW quarter arc, tan of one fourth of 90°

        DxfLwPolyline slot = new();
        DxfLwPolyline.Vertex[] vertices =
        {
                new(new Point2D(x + radius, y)),
                new(new Point2D(x + length - radius, y), bulge),
                new(new Point2D(x + length - radius, y + height)),
                new(new Point2D(x + radius, y + height), bulge),
            };
        slot.Vertices.AddRange(vertices);
        slot.Closed = true;

        return slot;
    }

    #endregion Create simple shapes

    #region Export

    public static string? ExportToPng(string filename, bool trialWarningAutoClose)
    {
        DxfModel? dxfModel = LoadModel(filename, trialWarningAutoClose);
        if (dxfModel == null) return null;

        // Remove text, dimensions, layers, ...
        string drawingName = Path.GetFileName(filename);
        string[] excludedEntities = { "DIMENSION", "MTEXT" };
        string[] excludedLayers = { "AM_5", "AM_7" };
        DxfModel dxfClonedModel = CloneAndSimplify(dxfModel, excludedEntities, excludedLayers, drawingName);

        // Image size calculation
        Bounds3D bounds = GetBounds(dxfClonedModel);
        int width = (int)Math.Ceiling(bounds.Delta.X);
        int height = (int)Math.Ceiling(bounds.Delta.Y);
        if (width < 100 || height < 100)
        {
            width *= 2;
            height *= 2;
        }
        Size imageSize = new(width, height);

        // GraphicsConfig allows customization (style, quality or transparency) and control (redraw or interaction with CAD elements)
        //   of various graphic aspects in a CAD visualization.
        GDIGraphics3D graphics = new(GraphicsConfig.BlackBackgroundCorrectForBackColor);
        Bitmap bitmap =
            ImageExporter.CreateAutoSizedBitmap(dxfClonedModel, graphics, Matrix4D.Identity, System.Drawing.Color.Black, imageSize);

        // Export to PNG
        string pngFilename = Path.Combine(Path.GetDirectoryName(filename) ?? string.Empty, Path.GetFileNameWithoutExtension(filename) + ".png");
        using Stream stream = File.Create(pngFilename);
        if (trialWarningAutoClose) StartTimer();
        ImageExporter.EncodeImageToPng(bitmap, stream);

        return Path.GetDirectoryName(pngFilename);
    }

    #endregion Export
}