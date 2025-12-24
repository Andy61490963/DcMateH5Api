using System.ComponentModel.DataAnnotations;

namespace ClassLibrary;


public enum LogErrorCode
{
    [Display(Name = "ExecutedFrom 必須小於 ExecutedTo")]
    InvalidParameter
}