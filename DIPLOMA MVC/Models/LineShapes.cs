namespace DIPLOMA_MVC.Controllers;

public enum LineShapes
{
    Empty = 0,
    //  ○---○
    //  |  /
    //  | /
    //  |/
    //  ○

    TopRight = 2,
    //  ○---●
    //  |  /
    //  | /
    //  |/
    //  ○

    Bottom = 4,
    //  ○---○
    //  |  /
    //  | /
    //  |/
    //  ●

    TopLeft = 1,
    //  ●---○
    //  |  /
    //  | /
    //  |/
    //  ○

    Top = 3,
    //  ●---●
    //  |  /
    //  | /
    //  |/
    //  ○

    TopRightAndBottom = 5,
    //  ○---●
    //  |  /
    //  | /
    //  |/
    //  ●

    TopLeftAndBottom = 6,
    //  ●---○
    //  |  /
    //  | /
    //  |/
    //  ●

    All = 7,
    //  ●---●
    //  |  /
    //  | /
    //  |/
    //  ●
}