namespace DIPLOMA_MVC.Controllers;

public interface IJsonParser
{
    Task<Point3D[]> ParseAsync(StreamReader reader);
}