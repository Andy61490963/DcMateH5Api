# CRUD Helpers

This module provides a lightweight reflection-based SQL builder and service wrapper for basic CRUD operations. SQL strings are generated with parameterization and identifier validation, while execution is delegated to an existing `IDbExecutor`.

## Usage

```csharp
var builder = new CrudSqlBuilder();
var crud = new CrudService(builder, _dbExecutor);

// insert
await crud.InsertAsync("Users", new { Name = "Alice" }, ct);

// update
await crud.UpdateAsync("Users", new { Name = "Bob" }, new { Id = 1 }, ct);

// delete
await crud.DeleteAsync("Users", new { Id = 1 }, ct);

// exists
bool exists = await crud.ExistsAsync("Users", new { Id = 1 }, ct);
```

### Using transactions

```csharp
await _dbExecutor.TxAsync(async (conn, tx, ct) =>
{
    await crud.InsertAsync(conn, tx, "Users", new { Name = "Tx" }, ct);
    await crud.UpdateAsync(conn, tx, "Users", new { Name = "Tx2" }, new { Id = 2 }, ct);
});
```

## Diagnostics

Common errors and their meanings:

* `ArgumentException: setDto has no properties.` – update called with empty DTO.
* `ArgumentException: whereDto has no properties.` – delete/update/exists without WHERE conditions is blocked.
* `Invalid identifier` – table or column name failed validation.

## Safety checklist

* Identifiers are validated by `SafeIdent` to avoid injection.
* All values are passed as Dapper parameters.
* Update and delete require WHERE conditions.
* Parameter names for SET and WHERE use `set_`/`w_` prefixes to avoid collisions.

## Bug guards

* DTOs with no public properties raise `ArgumentException`.
* Schema-qualified table names are supported via `Qualified`.
* Exceptions from database execution are wrapped with operation context for easier diagnostics.

## Performance notes

* Reflection results are cached in a `ConcurrentDictionary` keyed by type.
* Only necessary string allocations are performed when building SQL.
* No I/O occurs during building; service performs execution.
```
