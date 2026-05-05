using System.ComponentModel.DataAnnotations;

namespace ClassLibrary;

public enum EqmErrorCode
{
    [Display(Name = "Bad request", Description = "The Eqm request data is invalid.")]
    BadRequest,

    [Display(Name = "Conflict", Description = "The Eqm data was modified concurrently.")]
    Conflict,

    [Display(Name = "Unhandled exception", Description = "Unhandled Eqm exception.")]
    UnhandledException
}
