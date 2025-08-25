using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DcMateH5Api.Areas.Test.Interfaces;
using DcMateH5Api.Areas.Test.Models;
using DcMateH5Api.Controllers;
using DcMateH5Api.Helper;
using Microsoft.AspNetCore.Mvc;

namespace DcMateH5Api.Areas.Test.Controllers
{
    [Area("Test")] // 若你要走 Log，就改成 [Area("Log")]
    [ApiController]
    [ApiExplorerSettings(GroupName = SwaggerGroups.Permission)]
    [Route("[area]/[controller]")]
    [Produces("application/json")]
    public class TestController : BaseController
    {
        private readonly ITestService _svc;

        public TestController( ITestService testService )
        {
            _svc = testService;
        }
        // -----------------------
        // Demo：你原本的 GetTest
        // -----------------------
        /// <summary>測試抓一筆（Demo）</summary>
        [HttpGet("test")]
        public async Task<IActionResult> GetTest()
        {
            var users = await _svc.GetTestAsync();
            return Ok(users);
        }

        // -------------
        // CRUD 範例
        // -------------

        /// <summary>建立使用者</summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateUserInput input, CancellationToken ct)
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);
            var id = await _svc.CreateUserAsync(input, ct);
            return CreatedAtAction(nameof(GetById), new { id }, new { id }); // 回傳 201 + 位置
        }

        /// <summary>依 Id 取得使用者</summary>
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        {
            var user = await _svc.GetUserByIdAsync(id, ct);
            return user is null ? NotFound() : Ok(ToDto(user));
        }

        /// <summary>查詢使用者（QueryString：?account=...&name=...）</summary>
        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string? account, [FromQuery] string? name, CancellationToken ct)
        {
            // 至少要有一個條件，避免全表掃
            if (string.IsNullOrWhiteSpace(account) && string.IsNullOrWhiteSpace(name))
                return BadRequest("至少提供一個查詢條件（account 或 name）");

            var list = await _svc.SearchUsersAsync(account, name, ct);
            return Ok(list.Select(ToDto));
        }

        /// <summary>更新使用者（可部分欄位）</summary>
        [HttpPut]
        public async Task<IActionResult> Update([FromBody] UpdateUserInput input, CancellationToken ct)
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);

            var rows = await _svc.UpdateUserAsync(
                input,
                ct: ct);

            if (rows == 0) return NotFound("找不到該使用者或資料未變更");
            return Ok(new { rows });
        }

        /// <summary>刪除使用者</summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            var rows = await _svc.DeleteUserAsync(id, ct);
            return rows == 0 ? NotFound() : Ok(new { rows });
        }

        // -----------------
        // 輸入 / 輸出 DTO
        // -----------------

        /// <summary>建立使用者的輸入</summary>
        public sealed class CreateUserInput
        {
            [Required, MaxLength(100)]
            [Column("AC")]
            public string Account { get; set; } = string.Empty;

            [Required, MaxLength(100)]
            [Column("NAME")]
            public string Name { get; set; } = string.Empty;

            // 先走測試路線：直接收 Hash / Salt。正式建議由後端產生 Hash/Salt。
            [Required]
            [Column("SWD")]
            public string Password { get; set; } = string.Empty;
        }

        /// <summary>更新使用者的輸入</summary>
        [Table("SYS_USER")]
        public sealed class UpdateUserInput
        {
            [Key]
            [Column("ID")]
            public Guid Id { get; init; }
            
            [Required, MaxLength(100)]
            [Column("AC")]
            public string Account { get; set; } = string.Empty;

            [Required, MaxLength(100)]
            [Column("NAME")]
            public string Name { get; set; } = string.Empty;

            // 先走測試路線：直接收 Hash / Salt。正式建議由後端產生 Hash/Salt。
            [Required]
            [Column("SWD")]
            public string Password { get; set; } = string.Empty;
        }

        /// <summary>對外輸出的使用者（不含敏感欄位）</summary>
        public sealed class UserDto
        {
            public Guid Id { get; init; }
            public string Account { get; init; } = string.Empty;
            public string Name { get; init; } = string.Empty;
        }

        // Entity -> DTO（遮掉密碼欄位）
        private static UserDto ToDto(UserAccount u) => new()
        {
            Id = u.Id,
            Account = u.Account,
            Name = u.Name
        };
    }
}
