using DcMateClassLibrary.Helper;
using DcMateH5Api.Areas.Security.Interfaces;
using DcMateH5Api.Areas.Security.Models;
using DcMateH5Api.Areas.Security.ViewModels;

namespace DcMateH5Api.Areas.Security.Mappers;

public class UserAccountMapper
{
    public static UserAccount MapperRegisterAndDto(RegisterRequestViewModel dto, string salt, IPasswordHasher hasher)
    {
        return new UserAccount
        {
            Id = IdHelper.GenerateNumericId(),
            Account = dto.Account.Trim(),
            Name = dto.Account.Trim(),
            PasswordSalt = salt,
            PasswordHash = hasher.HashPassword(dto.Password, salt),
        };
    }
}