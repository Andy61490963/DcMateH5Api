using DcMateH5Api.Areas.Security.Interfaces;
using DcMateH5Api.Areas.Test.Controllers;
using DcMateH5Api.Areas.Test.Models;

namespace DcMateH5Api.Areas.Test.Mappers
{
    public static class UserAccountMapper
    {
        
        public static UserAccount ToNewEntity(TestController.CreateUserInput dto, string salt, IPasswordHasher hasher)
        {
            return new UserAccount
            {
                Id = Guid.NewGuid(),               
                Account = dto.Account.Trim(),
                Name = dto.Name.Trim(),
                PasswordSalt = salt,
                PasswordHash = hasher.HashPassword(dto.Password, salt),
                Role = "Admin"
            };
        }
    }
}