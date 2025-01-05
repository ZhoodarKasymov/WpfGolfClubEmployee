using System.Globalization;
using System.Windows.Controls;

namespace GolfClubSystem.Validations;

public class NotEmptyValidationRule : ValidationRule
{
    public override ValidationResult Validate(object value, CultureInfo cultureInfo)
    {
        if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
        {
            return new ValidationResult(false, "Поле обязательно для заполнения");
        }
        return ValidationResult.ValidResult;
    }
}