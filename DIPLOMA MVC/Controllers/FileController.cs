using System.Collections.Concurrent;
using System.Text;
using DelaunatorSharp;
using Microsoft.AspNetCore.Mvc;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using NetTopologySuite.Operation.Linemerge;
using NetTopologySuite.Simplify;
using Triangle = DelaunatorSharp.Triangle;

namespace DIPLOMA_MVC.Controllers;

public class FileController : Controller
{
    private readonly IJsonParser _parser;

    public FileController(IJsonParser parser)
    {
        _parser = parser;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> Upload(UploadModel model)
    {
        if (!ModelState.IsValid)
        {
            return View("Index", model);
        }
        
        if (model.File.ContentType != "application/json")
        {
            ModelState.AddModelError("File", "Неверный формат файла");
            return View("Index");
        }
        
        try
        {
            using (var reader = new StreamReader(model.File.OpenReadStream()))
            {
                //exception nulljson, multipoint
                var points = await _parser.ParseAsync(reader);
                
                //trianle delone
                var dn = new Delaunator(points);
                var listOfNumbers = model.Numbers
                    .Trim('[', ']')
                    .Split(',')
                    .Select(double.Parse)
                    .ToList();
                
                //calculating
                var threadItems = new ConcurrentDictionary<int, List<LineString>>();
                await Parallel.ForEachAsync(listOfNumbers, async (threshold, _) =>
                {
                    List<LineString> listw = new List<LineString>();
                    foreach (Triangle tr in dn.GetTriangles())
                    {
                        var item = await GetLines(tr.Points.ToArray(), threshold);
                        if(item is not null)
                            listw.Add(item);
                    }
                    threadItems.TryAdd((int)threshold, listw);
                });

                //merging, simplifier, smoothing linestrings
                var feature = new ConcurrentBag<Feature>();
                foreach (var threadItem in threadItems.Values)
                {
                    var merger = new LineMerger();
                    merger.Add(threadItem);
                    IList<Geometry> lineStrings = merger.GetMergedLineStrings();
                    
                    await Parallel.ForEachAsync(lineStrings, (lineString, _) =>
                    {
                        if(lineString.Coordinates.Length <= 3)
                            return ValueTask.CompletedTask;

                        TopologyPreservingSimplifier simplifier = new TopologyPreservingSimplifier(lineString);
                        simplifier.DistanceTolerance = 0.1;
                        Geometry simplifiedGeometry = simplifier.GetResultGeometry();
                        
                        LineString interpolatedLineString = Smooth((simplifiedGeometry as LineString)!);
                        
                        feature.Add(new Feature(interpolatedLineString, new AttributesTable()));
                        return ValueTask.CompletedTask;
                    });
                }
                
                GeoJsonWriter geoJsonWriter = new GeoJsonWriter();
                var str = geoJsonWriter.Write(feature);
                var result = $"{{\"type\": \"FeatureCollection\",\"features\": {str} }}";
                
                if (model.IsApiRequest)
                {
                    // Возвращаем ответ в формате JSON для API запросов
                    return Content(result, "application/json");
                }
                
                HttpContext.Session.SetString("fileContent", result);
                return RedirectToAction("Preview");
            }
        }
        catch (NullJsonException e)
        {
            ModelState.AddModelError(string.Empty, e.Message);
            ViewData["Error"] = e.Message;
            return View("Index");
        }
        catch (MultiPointException e)
        {
            ModelState.AddModelError(string.Empty, e.Message);
            ViewData["Error"] = e.Message;
            return View("Index");
        }
        catch (Exception)
        {
            string errorMessage = "Произошла ошибка при обработке GeoJSON. Пожалуйста, проверьте ваши данные и попробуйте снова.";
            ModelState.AddModelError(string.Empty, errorMessage);
            ViewData["Error"] = errorMessage;
            return View("Index");
        }
    }
    public IActionResult Preview()
    {
        var fileContent = HttpContext.Session.GetString("fileContent");
        return View("Preview", fileContent);
    }
    
    public IActionResult Download()
    {
        var fileContent = HttpContext.Session.GetString("fileContent");
        var content = Encoding.UTF8.GetBytes(fileContent!);
        var contentType = "APPLICATION/octet-stream";
        var fileName = $"{Environment.ProcessorCount}processed_file.json";
        return File(content, contentType, fileName);
    }
    LineShapes GetCaseId(IEnumerable<IPoint> p, double threshold)
    {
        int caseId = 0;
        var p1 = p.ToList();
        
        if (threshold == 0)
        {
            if (((Point3D)p1[0]).Z == 0) 
            {
                caseId |= 1;
            }
            if (((Point3D)p1[1]).Z == 0) 
            {
                caseId |= 2;
            }
            if (((Point3D)p1[2]).Z == 0)
            {
                caseId |= 4;
            }
            return (LineShapes)caseId;
        }
        
        if (((Point3D)p1[0]).Z < threshold)
        {
            caseId |= 1;
        }
        if (((Point3D)p1[1]).Z < threshold) //TODO
        {
            caseId |= 2;
        }
        if (((Point3D)p1[2]).Z < threshold)
        {
            caseId |= 4;
        }
        return (LineShapes)caseId;
    }
    Task<LineString?> GetLines(IPoint[] array, double threshold)
    {
        LineShapes caseId = GetCaseId(array, threshold);

        if (caseId == LineShapes.Empty || caseId == LineShapes.All)
        {
            return Task.FromResult<LineString?>(null);
        }
        var coord = new Coordinate[2];
        if (caseId == LineShapes.TopRight || caseId == LineShapes.TopRightAndBottom)
        {
            coord[0] = InterpolateDiagonal(array[0], array[1], ((Point3D)array[0]).Z, ((Point3D)array[1]).Z, threshold);
            coord[1] = InterpolateDiagonal(array[1], array[2], ((Point3D)array[1]).Z, ((Point3D)array[2]).Z, threshold);
        }

        if (caseId == LineShapes.TopLeft || caseId == LineShapes.TopLeftAndBottom)
        {
            coord[0] = InterpolateDiagonal(array[0], array[1], ((Point3D)array[0]).Z, ((Point3D)array[1]).Z, threshold);
            coord[1] = InterpolateDiagonal(array[0], array[2], ((Point3D)array[0]).Z, ((Point3D)array[2]).Z, threshold);
        }

        if (caseId == LineShapes.Bottom || caseId == LineShapes.Top)
        {
            coord[0] = InterpolateDiagonal(array[2], array[1], ((Point3D)array[2]).Z, ((Point3D)array[1]).Z, threshold);
            coord[1] = InterpolateDiagonal(array[2], array[0], ((Point3D)array[2]).Z, ((Point3D)array[0]).Z, threshold);
        }
        
        var point = new LineString(coord);
        return Task.FromResult(point)!;
    }
    Coordinate InterpolateDiagonal(IPoint start, IPoint end, double startForce, double endForce, double threshold)
    {
        double a = (threshold - startForce ) / (endForce - startForce );
        //double a = 0.5;
        double pX = start.X + (end.X - start.X) * a;
        double pY = start.Y + (end.Y - start.Y) * a;
        
        var factor = Convert.ToDouble(Math.Pow(10, 3));
        Coordinate p = new Coordinate( Math.Ceiling(pX * factor) / factor,  Math.Ceiling(pY * factor) / factor);
        return p;
    }
    
    public LineString Smooth(LineString lineString)
    {
        var smoothedCoordinates = ApplyChaikinsAlgorithm(lineString.Coordinates);
        var simple = new DouglasPeuckerSimplifier(new LineString(smoothedCoordinates.ToArray()));
        simple.DistanceTolerance = 0.3;
        var simplifiedGeometry = simple.GetResultGeometry();
        smoothedCoordinates = ApplyChaikinsAlgorithm(simplifiedGeometry.Coordinates);
        
        return new LineString(smoothedCoordinates.ToArray());
    }
    private IList<Coordinate> ApplyChaikinsAlgorithm(IList<Coordinate> points)
    {
        IList<Coordinate> smoothed = new List<Coordinate>();
        smoothed.Add(points[0]);
        for (int i = 0; i < points.Count - 1; i++)
        {
            Coordinate p1  = points[i];
            Coordinate p2  = points[i + 1];

            Coordinate q = new Coordinate(0.75 * p1.X + 0.25 * p2.X , 0.75 * p1.Y + 0.25 * p2.Y );
            Coordinate r = new Coordinate(0.25 * p1.X + 0.75 * p2.X , 0.25 * p1.Y + 0.75 * p2.Y);
            
            smoothed.Add(q);
            smoothed.Add(r);
        }
        smoothed.Add(points[points.Count - 1]);
        return smoothed;
    }
}