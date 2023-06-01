using Newtonsoft.Json;

namespace DIPLOMA_MVC.Controllers;

public class JsonParser : IJsonParser
{
    public async Task<Point3D[]> ParseAsync(StreamReader reader)
    {
        var fileContent = await reader.ReadToEndAsync();
        var feature = JsonConvert.DeserializeObject<MyFeature>(fileContent);
        
        if(feature.Type is null)
            throw new NullJsonException("GeoJSON не может быть пустым");
        
        if(feature.Geometry.Type is not "MultiPoint")
            throw new MultiPointException("GeoJSON должен быть типа MultiPoint");

        var array = feature.Geometry.Coordinates
                .Where(p => (int)p[1] is > -85 and < 85)
                .Select(p => new Point3D(p[0], p[1], p[2]))
            ;

        var contains359 = array.Reverse().Any(p => (int)p.X is 359);
        if (contains359 is not false)
        {
            var array360 = array
                    .Where(p => (int)p.X is 0)
                    .Select(p => new Point3D(p.X + 360, p.Y, p.Z))
                ;
            return array.Concat(array360).ToArray();
        }

        return array.ToArray();
    }
}

public class MultiPointException : Exception
{
    public MultiPointException(string str) : base(str)
    {
        
    }
}

public class NullJsonException : Exception
{
    public NullJsonException(string str) : base(str)
    {
        
    }
}