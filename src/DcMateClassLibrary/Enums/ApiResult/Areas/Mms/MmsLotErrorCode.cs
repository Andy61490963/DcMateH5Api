using System.ComponentModel.DataAnnotations;

namespace ClassLibrary;

public enum MmsLotErrorCode
{
    [Display(Name = "BadRequest", Description = "The MLOT request is invalid.")]
    BadRequest,

    [Display(Name = "Conflict", Description = "The MLOT data was modified concurrently.")]
    Conflict,

    [Display(Name = "UnhandledException", Description = "Unhandled MLOT exception.")]
    UnhandledException
}
