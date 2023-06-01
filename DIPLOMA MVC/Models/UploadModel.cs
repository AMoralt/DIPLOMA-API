using System.ComponentModel.DataAnnotations;

namespace DIPLOMA_MVC.Controllers;

public class UploadModel
{
    public IFormFile File { get; set; }

    [RegularExpression(@"^\[\s*\d+(\s*,\s*\d+)*\s*\]$", ErrorMessage = "Введенные данные не соответствуют ожидаемому формату.")]
    public string Numbers { get; set; }
    
    public bool IsApiRequest { get; set; }
}